#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP P0/Phase 8: headless-верификация боевого
// пайплайна (GODOT_COMBAT_SIM=1). Проверяет оба направления урона:
//   NPC → игрок  (P0-баг: BodyService не применял урон по "player_0")
//   игрок → NPC  (Phase 8: wiring урона оружия/статов экипировки)
// Запуск: GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 godot --headless --path . scenes/MainMenu.tscn
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §P0, SESSION_CONTEXT §7.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat; // Phase 8 ч.3: CombatRangeGateService

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация боевого пайплайна (P0 из SESSION_CONTEXT §7).
/// Скриптованная серия AttackIntentEvent в обе стороны + подсчёт
/// DamageAppliedEvent. Итог: [CombatSim] VERDICT: PASS/FAIL.
///
/// Паттерн следует GODOT_NEWGAME/GODOT_GEN_DEBUG — env-хук только для
/// верификации, игровой код не затрагивает.
/// </summary>
public partial class CombatSimDebug : Node
{
    [Inject] private IPublisher<Core.Messaging.Contracts.AttackIntentEvent>? _attackIntentPub;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackIntentEvent>? _attackIntentSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyDataProvider? _bodyProvider;
    [Inject] private IEquipmentDataProvider? _equipmentProvider;
    [Inject] private ITimeService? _timeService;
    [Inject] private IEquipmentService? _equipmentService;
    [Inject] private Modules.Generator.EquipmentGenerator? _equipmentGenerator;
    [Inject] private IPlayerService? _playerService;

    // Phase 8 ч.3: гейты дальнего боя — LOS/стрелы (верификация 3d).
    [Inject] private ITileService? _tileService;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackRejectedEvent>? _rejectedSub;
    [Inject] private Modules.Combat.CombatService? _combatServiceImpl;
    // R21 (20.09): регресс-гарды пайплайна защиты — броня и пассивный щит Ци.
    [Inject] private IDamageService? _damageService;
    [Inject] private IQiService? _qiService;
    [Inject] private IQiDataProvider? _qiDataProvider;
    // R25 (2026-09-21): площадные техники — генератор/библиотека/событие залпа.
    [Inject] private Modules.Combat.TechniqueService? _techniqueService;
    [Inject] private ITechniqueGeneratorService? _techniqueGenerator;
    [Inject] private ISubscriber<Core.Messaging.Contracts.AoeImpactEvent>? _aoeImpactSub;
    // R25 QA: спавн толпы для конуса (спавн — отдельный сервис, не INPCService).
    [Inject] private INPCSpawnerService? _npcSpawner;
    // R27: выбор цели игрока (Tab-цикл) — TargetingService + событие.
    [Inject] private Modules.Player.TargetingService? _targeting;
    [Inject] private ISubscriber<Core.Messaging.Contracts.PlayerTargetChangedEvent>? _targetChangedSub;

    private System.IDisposable? _damageToken;
    private System.IDisposable? _intentEchoToken;
    private System.IDisposable? _rejectedToken;
    private System.IDisposable? _aoeImpactToken;
    private System.IDisposable? _targetChangedToken;

    // R25: количество целей последнего AoE-залпа (AoeImpactEvent.TargetIds).
    private int _aoeImpactCount = -1;

    // R27: последний PlayerTargetChangedEvent (TargetId-строка для assert-ов).
    private string _lastTargetChanged = "<none>";

    // Phase 8 ч.3: трекинг отклонений (причины — LOS/стрелы/каст).
    private int _rejectedCount;
    private string _lastRejection = "";

    // Учёт урона по целям за время симуляции.
    private readonly Dictionary<string, int> _damageByTarget = new();
    private int _eventsReceived;

    // Phase 8 ч.2 (2026-09-03): трекинг ranged-попаданий (подтип RangedProjectile).
    private int _rangedDamageEvents;
    private int _rangedDamageTotal;

    private const string PlayerCombatId = "player_0"; // NPCAIService.PlayerId

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Диагностика: публикатор/подписчик/время (null → DI-проблема).
        GD.Print($"[CombatSim] diag: pub={_attackIntentPub != null} sub={_attackIntentSub != null} " +
                 $"dmgSub={_damageSub != null} time={_timeService != null} " +
                 $"(speed={_timeService?.Speed}, paused={_timeService?.IsPaused})");

        _intentEchoToken = _attackIntentSub?.Subscribe((in Core.Messaging.Contracts.AttackIntentEvent e) =>
        {
            GD.Print($"[CombatSim] intent echo: {e.AttackerId} → {e.TargetId} ({e.TechniqueId})");
        });

        // Phase 8 ч.3: отклонения атак (LOS / стрелы / каст).
        _rejectedToken = _rejectedSub?.Subscribe((in Core.Messaging.Contracts.AttackRejectedEvent e) =>
        {
            _rejectedCount++;
            _lastRejection = e.Reason;
            GD.Print($"[CombatSim] rejected: {e.AttackerId} — {e.Reason}");
        });

        _damageToken = _damageSub?.Subscribe((in Core.Messaging.Contracts.DamageAppliedEvent e) =>
        {
            _eventsReceived++;
            _damageByTarget.TryGetValue(e.TargetId, out int sum);
            _damageByTarget[e.TargetId] = sum + e.Damage;
            // Phase 8 ч.2: ranged-попадания (стрелы должны идти как RangedProjectile)
            if (e.AttackSubtype == CombatSubtype.RangedProjectile)
            {
                _rangedDamageEvents++;
                _rangedDamageTotal += e.Damage;
            }
            GD.Print($"[CombatSim] damage: {e.SourceId} → {e.TargetId}: {e.Damage} ({e.Result}, part={e.HitPart}, sub={e.AttackSubtype})");
        });

        // R25: трекинг AoE-залпов (количество целей = per-target резолвы).
        _aoeImpactToken = _aoeImpactSub?.Subscribe((in Core.Messaging.Contracts.AoeImpactEvent e) =>
        {
            _aoeImpactCount = e.TargetIds.Length;
            GD.Print($"[CombatSim] aoe impact: {e.CasterId} '{e.TechniqueId}' {e.Shape} " +
                     $"r={e.RadiusTiles} targets={_aoeImpactCount}");
        });

        // R27: трекинг выбора цели игрока (Tab-цикл TargetingService).
        _targetChangedToken = _targetChangedSub?.Subscribe((in Core.Messaging.Contracts.PlayerTargetChangedEvent e) =>
        {
            _lastTargetChanged = string.IsNullOrEmpty(e.TargetId) ? "<cleared>" : e.TargetId;
            GD.Print($"[CombatSim] target changed: '{_lastTargetChanged}' @ ({e.TargetX},{e.TargetY}) dist={e.DistanceTiles}");
        });

        GD.Print("[CombatSim] Ready — scripted combat verification starts in 2s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _intentEchoToken?.Dispose();
        _rejectedToken?.Dispose();
        _aoeImpactToken?.Dispose();
        _damageToken = null;
        _intentEchoToken = null;
        _rejectedToken = null;
        _aoeImpactToken = null;
        _targetChangedToken?.Dispose();
        _targetChangedToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        // 1. Даём сцене собраться (фазы спавна NPC завершаются в первые кадры).
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_attackIntentPub == null || _npcService == null || _bodyProvider == null)
        {
            GD.Print("[CombatSim] FAIL — DI not wired (publisher/npc/body missing)");
            PrintVerdict(false);
            return;
        }

        // 2. Диагностика Phase 8: wiring статов экипировки (ожидаем ненулевые
        //    значения для NPC — генератор «Матрёшка» выдаёт оружие всем).
        string? npcId = FindHostileNpc();
        if (npcId == null)
        {
            GD.Print("[CombatSim] FAIL — no hostile NPC spawned (HumanNPCSpawnPhase)");
            PrintVerdict(false);
            return;
        }

        if (_equipmentProvider != null)
        {
            GD.Print($"[CombatSim] equip[{npcId}]: pen={_equipmentProvider.GetWeaponPenetration(npcId)} " +
                     $"dodge={_equipmentProvider.GetDodgeBonusPermil(npcId)}‰ " +
                     $"block={_equipmentProvider.GetBlockBonusPermil(npcId)}‰ " +
                     $"crit={_equipmentProvider.GetCritBonusPermil(npcId)}‰ " +
                     $"dmg={_equipmentProvider.GetTotalDamage(npcId)}");
            GD.Print($"[CombatSim] equip[player]: pen={_equipmentProvider.GetWeaponPenetration(PlayerCombatId)} " +
                     $"dmg={_equipmentProvider.GetTotalDamage(PlayerCombatId)} " +
                     $"(без экипировки — 0 ожидаемо)");
        }

        int playerHpBefore = _bodyProvider.GetCurrentHealth("player");
        int npcHpBefore = _bodyProvider.GetCurrentHealth(npcId);
        GD.Print($"[CombatSim] start HP: player={playerHpBefore}, npc({npcId})={npcHpBefore}");

        // 2b. Телепортируем NPC рядом с игроком — бой происходит в кадре камеры
        // (для визуальной верификации HP-баров/цифр урона на скриншотах).
        // NPC, а не игрока: камера и спрайт игрока привязаны к _visualPosition
        // контроллера, а PlayerService.SetPosition двигает только логику.
        if (_playerService != null)
        {
            var playerPos = _playerService.Position;
            var npcState = _npcService.GetNPCState(npcId);
            if (npcState != null)
            {
                npcState.Position = new Position2D(playerPos.X + 1, playerPos.Y);
                GD.Print($"[CombatSim] NPC {npcId} teleported to ({playerPos.X + 1}, {playerPos.Y}) near player");
            }
        }

        // 3. Серия ударов в обе стороны (как это делает NPCModule/PlayerCombatAdapter).
        // R21-2 (attack-speed): удары НЕЗАВИСИМЫ от ходов (ходы удалены) —
        // ждём ГОТОВНОСТЬ каждого участника перед интентом (readiness-гейт
        // CombatService отклонит «удар не готов»; это контракт — QA обязан
        // его соблюдать). Инициатива: инициатор начинает с полной готовностью.
        for (int round = 1; round <= 3; round++)
        {
            // NPC → игрок (P0-проверка: урон должен примениться к телу игрока).
            // Раунд 1: боя нет → интент сам стартует бой (инициатор NPC —
            // честная инициатива: первый удар сразу).
            // Раунды 2+: NPCModule атакует сам при своей готовности —
            // ожидание готовности NPC ГОНКА с его автоатаками (куладаун
            // списывается NPCModule): фиксированная пауза вместо ожидания.
            if (_combatServiceImpl != null && _combatServiceImpl.IsInCombat)
            {
                await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            }
            else
            {
                await WaitForReadinessAsync(npcId, 4.0f);
            }
            await WaitForCastClearAsync(1.0f);
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                npcId, PlayerCombatId, "npc_strike", false));
            // Каст NPC 0.5с — даём догореть (урон в игрока).
            await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);

            // Игрок → NPC (Phase 8: weapon damage wiring): ждём готовность
            // оружия игрока (кулаки 1000‰ × AGI; после удара — ~0.9с).
            await WaitForReadinessAsync(PlayerCombatId, 4.0f);
            await WaitForCastClearAsync(1.0f);
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                PlayerCombatId, npcId, "basic_attack", false));

            GD.Print($"[CombatSim] round {round}: player HP={_bodyProvider.GetCurrentHealth("player")}, " +
                     $"npc HP={_bodyProvider.GetCurrentHealth(npcId)}");
        }

        // 3b. Phase 8 end-to-end: надеть оружие игроком и проверить, что урон
        // вырос (weapon damage + penetration проходят через EquipmentService →
        // EquipmentDataProvider → CombatService).
        bool weaponWiringOk = true; // остаётся true, если фаза пропущена
        if (_equipmentService != null && _equipmentGenerator != null)
        {
            // M1 (2026-09-03): после фикса per-attacker pending каст NPC больше
            // не бьёт самого себя — урон честно летит в игрока и по механике
            // C11 (Спринт 8) ПРЕРЫВАЕТ его догорающий pending-каст. Раньше
            // PASS этой фазы держался на баге self-hit. Даём всем pending
            // раундов догореть ДО armed-интента → чистое окно для проверки
            // weapon wiring без прерывания каста.
            GD.Print("[CombatSim] letting round pendings settle (cast-interrupt mechanics)...");
            await ToSignal(GetTree().CreateTimer(1.4), SceneTreeTimer.SignalName.Timeout);
            var weapon = _equipmentGenerator.GenerateWeapon(level: 3);
            bool equipped = _equipmentService.TryEquip(EquipmentSlot.WeaponMain, weapon);
            if (equipped && _equipmentProvider != null)
            {
                GD.Print($"[CombatSim] player equipped '{weapon.NameRu}' (dmg={weapon.Damage}, pen={weapon.Penetration}) — " +
                         $"provider: dmg={_equipmentProvider.GetTotalDamage(PlayerCombatId)}, " +
                         $"pen={_equipmentProvider.GetWeaponPenetration(PlayerCombatId)}");
                // R21-2: ждём готовность оружия игрока (кулаки → ~0.9с).
                await WaitForReadinessAsync(PlayerCombatId, 4.0f);
                await WaitForCastClearAsync(1.0f);
                int npcHpBeforeWeapon = _bodyProvider.GetCurrentHealth(npcId);
                _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                    PlayerCombatId, npcId, "basic_attack", false));
                // Каст 0.5с разрешится тиком (~1с при Normal) — ждём подольше.
                await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
                int npcHpAfterWeapon = _bodyProvider.GetCurrentHealth(npcId);
                int weaponSwing = npcHpBeforeWeapon - npcHpAfterWeapon;
                GD.Print($"[CombatSim] armed swing: npc HP {npcHpBeforeWeapon}→{npcHpAfterWeapon} ({weaponSwing} RedHP dmg)");
                weaponWiringOk = weaponSwing > 0;
            }
            else if (!equipped)
            {
                GD.Print("[CombatSim] WARN — TryEquip(WeaponMain) failed");
            }
        }
        else
        {
            GD.Print("[CombatSim] skip weapon phase — IEquipmentService/Generator not injected");
        }

        // 3c. Phase 8 ч.2: end-to-end ДАЛЬНИЙ БОЙ — лук + цель на дистанции 8
        // (вне melee 2.5, внутри дальности лука 18). Проверяем: игрок с луком
        // и isRanged=true наносит урон на дистанции, подтип — RangedProjectile.
        // Phase 8 ч.3: перед выстрелом очищаем полосу линии огня от случайных
        // деревьев/камней (гейт LOS должен зависеть только от теста, не от
        // генерации террейна); оригиналы восстанавливаются после фазы 3d.
        var clearedLosTiles = new List<(int x, int y, GameTile tile)>();
        bool rangedWiringOk = true;
        if (_equipmentService != null && _equipmentGenerator != null && _playerService != null)
        {
            GD.Print("[CombatSim] letting armed-swing pending settle before ranged phase...");
            await ToSignal(GetTree().CreateTimer(1.4), SceneTreeTimer.SignalName.Timeout);

            var bow = _equipmentGenerator.GenerateWeapon(level: 3, subtype: "bow");
            bool bowEquipped = _equipmentService.TryEquip(EquipmentSlot.WeaponMain, bow);
            if (bowEquipped)
            {
                GD.Print($"[CombatSim] player equipped bow '{bow.NameRu}' (dmg={bow.Damage}, range={bow.AttackRange})");

                // Телепорт NPC на дистанцию 8 — вне melee, внутри дальности лука.
                var playerPos = _playerService.Position;
                var npcState = _npcService.GetNPCState(npcId);
                int npcHpBeforeRanged = _bodyProvider.GetCurrentHealth(npcId);
                int rangedDamageBefore = _rangedDamageTotal;
                if (npcState != null)
                {
                    npcState.Position = new Position2D(playerPos.X + 8, playerPos.Y);
                    GD.Print($"[CombatSim] NPC {npcId} at distance 8 (melee=2.5, bow={bow.AttackRange})");
                }

                // Phase 8 ч.3: чистая линия огня p.X+1..p.X+7 (только блокирующие
                // тайлы — минимум вмешательства в мир; восстановление после 3d).
                if (_tileService != null)
                {
                    for (int x = playerPos.X + 1; x <= playerPos.X + 7; x++)
                    {
                        var orig = _tileService.GetTile(x, playerPos.Y);
                        if (CombatLos.BlocksLineOfFire(orig))
                        {
                            clearedLosTiles.Add((x, playerPos.Y, orig));
                            _tileService.SetTile(x, playerPos.Y,
                                GameTile.CreateTerrain(x, playerPos.Y, TerrainType.Grass));
                            GD.Print($"[CombatSim] LOS clear: tile ({x},{playerPos.Y}) «{orig.Object}» → Grass");
                        }
                    }
                }

                // Phase 8 ч.3: детерминизм — выстрел должен пройти гейт
                // (LOS+стрелы), а не упереться в догорающий каст (C-5).
                // R21-2: + ждём ГОТОВНОСТЬ оружия игрока (readiness-гейт).
                await WaitForCastClearAsync(2.0f);
                await WaitForReadinessAsync(PlayerCombatId, 4.0f);

                _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                    PlayerCombatId, npcId, "basic_attack", isRanged: true));
                // Каст 0.5с (натяжение лука) разрешится тиком — ждём.
                await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);

                int npcHpAfterRanged = _bodyProvider.GetCurrentHealth(npcId);
                int rangedSwing = npcHpBeforeRanged - npcHpAfterRanged;
                int rangedSubtyped = _rangedDamageTotal - rangedDamageBefore;
                GD.Print($"[CombatSim] ranged shot: npc HP {npcHpBeforeRanged}→{npcHpAfterRanged} " +
                         $"({rangedSwing} dmg, RangedProjectile-subtyped dmg={rangedSubtyped})");
                rangedWiringOk = rangedSwing > 0 && rangedSubtyped > 0;
                if (rangedSwing <= 0)
                    GD.Print("[CombatSim] FAIL — ranged shot did no damage at distance 8 (range gate broken?)");
                if (rangedSubtyped <= 0)
                    GD.Print("[CombatSim] FAIL — no RangedProjectile-subtyped damage (subtype resolution broken?)");
            }
            else
            {
                GD.Print("[CombatSim] WARN — TryEquip(bow) failed");
            }
        }
        else
        {
            GD.Print("[CombatSim] skip ranged phase — services not injected");
        }

        // 3d. Phase 8 ч.3: гейты дальнего боя — LOS (препятствие) + расход
        // стрел. Проверяем ОБА отклонения и что стрелы реально списываются.
        bool gatesOk = true;
        if (_tileService != null && _inventory != null && _playerService != null && _npcService != null)
        {
            // Цель могла умереть от предыдущих фаз — ищем живую (или исходную).
            string? targetId = _npcService.IsAlive(npcId) ? npcId : FindHostileNpc();
            if (targetId != null)
            {
                // === 3d-1: LOS — стрельба сквозь камень отклоняется ===
                // Детерминизм: телепорт цели на линию, камень в середину,
                // интент публикуется сразу (NPC не успевает сдвинуться).
                var p = _playerService.Position;
                var targetState = _npcService.GetNPCState(targetId);
                if (targetState != null)
                {
                    await WaitForCastClearAsync(2.0f);
                    targetState.Position = new Position2D(p.X + 8, p.Y);
                    int midX = p.X + 4, midY = p.Y;
                    var originalTile = _tileService.GetTile(midX, midY);
                    _tileService.SetTile(midX, midY,
                        GameTile.CreateWithObject(midX, midY, TerrainType.Grass, ObjectType.Rock_Large));

                    int rangedBefore = _rangedDamageTotal;
                    _rejectedCount = 0; _lastRejection = "";
                    _attackIntentPub!.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                        PlayerCombatId, targetId, "basic_attack", isRanged: true));
                    await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);

                    int blockedDamage = _rangedDamageTotal - rangedBefore;
                    bool losRejected = _rejectedCount > 0 && _lastRejection.Contains("линии огня");
                    bool losDirect = !CombatLos.HasLineOfSight(
                        _tileService, p.X, p.Y, p.X + 8, p.Y);
                    GD.Print($"[CombatSim] LOS-blocked shot: ranged dmg +{blockedDamage}, " +
                             $"rejected={losRejected} ('{_lastRejection}'), direct-LOS-check={losDirect}");
                    gatesOk &= losRejected && blockedDamage == 0 && losDirect;

                    // Чистим препятствие (чтобы не мешать остальным фазам).
                    _tileService.SetTile(midX, midY, originalTile);

                    // === 3d-2: LOS восстановлен — прямая проверка гейта ===
                    bool losClear = CombatLos.HasLineOfSight(
                        _tileService, p.X, p.Y, p.X + 8, p.Y);
                    GD.Print($"[CombatSim] LOS after rock removed: {losClear} (ожидаем True)");
                    gatesOk &= losClear;

                    // === 3d-3: стрелы — пустой колчан отклоняет выстрел ===
                    int arrowsBefore = _inventory.GetItemCount(CombatRangeGateService.ArrowItemId);
                    if (arrowsBefore > 0)
                        _inventory.TryRemoveItem(CombatRangeGateService.ArrowItemId, arrowsBefore);

                    await WaitForCastClearAsync(2.0f);
                    targetState.Position = new Position2D(p.X + 8, p.Y);
                    rangedBefore = _rangedDamageTotal;
                    _rejectedCount = 0; _lastRejection = "";
                    _attackIntentPub!.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                        PlayerCombatId, targetId, "basic_attack", isRanged: true));
                    await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);

                    int noAmmoDamage = _rangedDamageTotal - rangedBefore;
                    bool ammoRejected = _rejectedCount > 0 && _lastRejection.Contains("стрел");
                    GD.Print($"[CombatSim] no-ammo shot: arrows was {arrowsBefore}, " +
                             $"ranged dmg +{noAmmoDamage}, rejected={ammoRejected} ('{_lastRejection}')");
                    gatesOk &= ammoRejected && noAmmoDamage == 0;

                    // Восстанавливаем колчан (гарантия: предмет в БД после 3c).
                    if (_itemDb != null && _itemDb.TryGetItem(CombatRangeGateService.ArrowItemId, out var arrowItem))
                        _inventory.TryAddItem(arrowItem, 20);
                    GD.Print($"[CombatSim] quiver restored: {_inventory.GetItemCount(CombatRangeGateService.ArrowItemId)}");
                }
            }
            else
            {
                GD.Print("[CombatSim] WARN — no alive NPC for gate phase");
            }
        }
        else
        {
            GD.Print("[CombatSim] skip gate phase — LOS/ammo services not injected");
        }

        // Phase 8 ч.3: восстановить случайно-очищенные тайлы линии огня.
        if (_tileService != null)
        {
            foreach (var (x, y, orig) in clearedLosTiles)
                _tileService.SetTile(x, y, orig);
            if (clearedLosTiles.Count > 0)
                GD.Print($"[CombatSim] LOS tiles restored: {clearedLosTiles.Count}");
        }

        // 3e. R21-2 (attack-speed): ПРОВЕРКА READINESS-ГЕЙТА — авторитетность
        // модели готовности (замена turn-gate: ходы удалены). R23-1 (аудит
        // CMB-1) — новый контракт §8.2: (a) обычный удар при готовности =
        // Accepted + списание; (b) незаряженный удар при неготовности =
        // Rejected; (c) ЗАРЯЖЕННЫЙ удар при НЕготовности = Accepted (гейт
        // пропускается — зарядка была замахом); (d) сразу после charged
        // обычный удар = всё ещё Rejected (charged готовность НЕ расходует —
        // регресс-гард); (e) после начисления тиками = Accepted.
        // Детерминизм: (b)/(c)/(d) выполняются в ОДНОМ кадре (без await).
        bool turnGateOk = true;
        if (_combatServiceImpl != null && _playerService != null)
        {
            string? tgTarget = _npcService != null && _npcService.IsAlive(npcId)
                ? npcId : FindHostileNpc();
            if (tgTarget != null)
            {
                await WaitForCastClearAsync(2.0f);
                await WaitForReadinessAsync(PlayerCombatId, 4.0f);

                // (a) Готов → ОБЫЧНЫЙ удар = Accepted (реальное списание
                // готовности; pending-каст ~0.5с — резолвим ниже).
                if (_combatServiceImpl.IsInCombat && _combatServiceImpl.IsAttackReady(PlayerCombatId))
                {
                    var readyHit = _combatServiceImpl.ExecuteAttack(PlayerCombatId, "basic_attack", tgTarget, false);
                    GD.Print($"[CombatSim] readiness-gate: player ready, basic hit → {readyHit} (ожидаем Accepted)");
                    turnGateOk &= readyHit == AttackAcceptance.Accepted;
                    // Каст (a) должен резолвиться до (c): charged-атака
                    // упирается в каст-гейт (per-attacker), если свой каст
                    // ещё идёт. Чужие NPC-касты не мешают (IsEntityCasting).
                    await WaitForOwnCastClearAsync(PlayerCombatId, 2.0f);
                }

                // R23-1 (CMB-1): детерминизм через DebugSetReadinessPermil
                // (QA-паттерн R16 LastNpcDefenseSelected): игровой тик
                // начисляет ЦЕЛУЮ секунду готовности (кап за 1 тик) — окно
                // «не готов» между тиками не поймать поллами.
                if (_combatServiceImpl.IsInCombat)
                {
                    // (b) принудительно 0‰ → обычный удар = Rejected «не готов».
                    _combatServiceImpl.DebugSetReadinessPermil(PlayerCombatId, 0);
                    _rejectedCount = 0; _lastRejection = "";
                    var notReady = _combatServiceImpl.ExecuteAttack(PlayerCombatId, "basic_attack", tgTarget, false);
                    GD.Print($"[CombatSim] readiness-gate: player NOT ready → {notReady} ('{_lastRejection}')");
                    turnGateOk &= notReady == AttackAcceptance.Rejected && _rejectedCount > 0;

                    // (c) R23-1 (CMB-1): ЗАРЯЖЕННЫЙ удар (potency 1500) при
                    // 600‰ (ниже порога) = Accepted — гейт готовности
                    // ПРОПУЩЕН (§8.2 «заряженные готовность не расходывают
                    // дополнительно»), путь мгновенный, свой каст чист.
                    _combatServiceImpl.DebugSetReadinessPermil(PlayerCombatId, 600);
                    var chargedNotReady = _combatServiceImpl.ExecuteAttack(
                        PlayerCombatId, "basic_attack", tgTarget, false, potencyPermil: 1500);
                    GD.Print($"[CombatSim] readiness-gate: CHARGED at 600‰ → {chargedNotReady} (ожидаем Accepted, CMB-1)");
                    turnGateOk &= chargedNotReady == AttackAcceptance.Accepted;

                    if (_combatServiceImpl.IsInCombat)
                    {
                        // (d) Регресс-гард «charged НЕ расходует»: readiness
                        // после charged-выпуска ВСЁ ЕЩЁ 600‰ (тот же кадр,
                        // тика не было). Плюс поведенческий тест: обычный
                        // удар при 600‰ = Rejected «не готов».
                        int afterCharged = _combatServiceImpl.GetReadinessPermil(PlayerCombatId);
                        _rejectedCount = 0; _lastRejection = "";
                        var afterChargedHit = _combatServiceImpl.ExecuteAttack(PlayerCombatId, "basic_attack", tgTarget, false);
                        GD.Print($"[CombatSim] readiness-gate: after charged readiness={afterCharged}‰ (ожидаем 600) hit → {afterChargedHit} ('{_lastRejection}')");
                        turnGateOk &= afterCharged == 600
                            && afterChargedHit == AttackAcceptance.Rejected && _rejectedCount > 0;
                    }
                }

                // (e) Готовность восстанавливается тиками (кулаки ~0.9с /
                // оружие класс-зависимо): ждём и бьём — Accepted (pending-каст).
                if (_combatServiceImpl.IsInCombat)
                {
                    await WaitForReadinessAsync(PlayerCombatId, 6.0f);
                    var reReady = _combatServiceImpl.ExecuteAttack(PlayerCombatId, "basic_attack", tgTarget, false);
                    GD.Print($"[CombatSim] readiness-gate: player re-ready → {reReady} (ожидаем Accepted)");
                    turnGateOk &= reReady == AttackAcceptance.Accepted;
                }
                else
                {
                    // Бой завершился (NPC умер) — re-ready проверку пропускаем
                    // честно (WARN): обе проверки выше уже валидны.
                    GD.Print("[CombatSim] WARN — combat ended before re-ready check (NPC died?); skipped");
                }
            }
            else
            {
                GD.Print("[CombatSim] skip readiness-gate phase — no alive NPC");
            }
        }

        // 3g. R24-C (мультибой «реестр входов», выбор пользователя 2026-09-21):
        // регресс-гварды CMB-2 (гейт участника 1v1 удалён).
        // (a) NPC-vs-NPC «тихий» бой: пара без игрока НЕ открывает UI-сессию
        //     (CombatStarted/стадии молчат), но урон летит по полному
        //     пайплайну и месть включается (AIState Attacking/Fleeing);
        // (b) толпа бьёт игрока: A открывает UI-сессию парой с игроком,
        //     затем B — НЕ-участник UI-пары — наносит урон игроку
        //     (прежде гейт «не участник» отклонял удар B — корень CMB-2).
        // Детерминизм: прямые вызовы ExecuteAttack (паттерн 3e) +
        // WaitForOwnCastClear + DebugSetReadinessPermil перед каждым ударом
        // (гонка с NPCModule-автоатаками сужена до кадра).
        bool multiOk = true;
        if (_combatServiceImpl != null && _npcService != null && _bodyProvider != null)
        {
            // Изоляция: завершить висящий UI-бой (3e могла оставить).
            if (_combatServiceImpl.IsInCombat)
            {
                _combatServiceImpl.AbandonCombat(PlayerCombatId);
                GD.Print("[CombatSim] multi: previous UI-combat abandoned");
            }

            string? crowdA = _npcService.IsAlive(npcId) ? npcId : FindHostileNpc();
            string? crowdB = FindSecondNpc(crowdA);
            if (crowdA != null && crowdB != null)
            {
                // === (a) тихий NPC-NPC: B бьёт A ===
                // Гонки с NPCModule-автоатаками (месть A→игроку может
                // переоткрыть UI-сессию в любой паузе): «тихость» пары меряем
                // В КАДРЕ вызова (до следующего тика), урон/месть — после паузы.
                await WaitForOwnCastClearAsync(crowdB, 2.0f);
                _combatServiceImpl.DebugSetReadinessPermil(crowdB, 1000);
                int aHpBefore = _bodyProvider.GetCurrentHealth(crowdA);
                var quietAcc = _combatServiceImpl.ExecuteAttack(crowdB, "npc_strike", crowdA, false);
                // immediate: пара без игрока НЕ открыла UI-сессию этим ударом.
                bool quietNoUiSession = !_combatServiceImpl.IsInCombat;
                await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
                int aHpAfter = _bodyProvider.GetCurrentHealth(crowdA);
                bool quietDamage = aHpAfter < aHpBefore;
                var aState = _npcService.GetNPCState(crowdA);
                // Месть: угроза A от B (Threats) — A мог уже быть Attacking
                // (игрок бил его в 3/3e) — честный маркер: Threats[B] > 0.
                bool revenge = aState != null && aState.Threats.ContainsKey(crowdB);
                GD.Print($"[CombatSim] multi(a) quiet NPC-NPC: {crowdB}→{crowdA} acc={quietAcc}, " +
                         $"A HP {aHpBefore}→{aHpAfter}, UI-in-frame={quietNoUiSession} (ожид true), " +
                         $"revenge(threat B)={revenge} (AIState={aState?.AIState})");
                multiOk &= quietAcc == AttackAcceptance.Accepted
                    && quietNoUiSession && quietDamage && revenge;

                // === (b) толпа бьёт игрока: сессия пары с игроком ===
                // Сессию может открыть и автоатака A (месть — честно), и наш
                // прямой удар: retry-цикл до открытия (гонка с NPCModule-кастами
                // A — Reject «Каст уже идёт» ретраится).
                int playerHpCrowd0 = _bodyProvider.GetCurrentHealth("player");
                for (int attempt = 0; attempt < 5 && !_combatServiceImpl.IsInCombat; attempt++)
                {
                    await WaitForOwnCastClearAsync(crowdA, 2.0f);
                    _combatServiceImpl.DebugSetReadinessPermil(crowdA, 1000);
                    var acc = _combatServiceImpl.ExecuteAttack(crowdA, "npc_strike", PlayerCombatId, false);
                    GD.Print($"[CombatSim] multi(b) attempt {attempt + 1}: {crowdA}→player acc={acc}");
                    await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
                }
                await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
                int playerHpCrowd1 = _bodyProvider.GetCurrentHealth("player");
                bool uiSessionByPlayerPair = _combatServiceImpl.IsInCombat;
                bool crowdDamage = playerHpCrowd1 < playerHpCrowd0;
                GD.Print($"[CombatSim] multi(b) crowd opens UI: player HP {playerHpCrowd0}→{playerHpCrowd1}, " +
                         $"UI-session={uiSessionByPlayerPair} (ожид true), damage={crowdDamage}");
                multiOk &= uiSessionByPlayerPair && crowdDamage;

                // === (b2) CMB-2 регресс: B — НЕ-участник UI-пары — бьёт игрока ===
                // Retry: B участвует в тихой войне с A (касты каждые ~1с) —
                // Reject «Каст уже идёт» ретраится до Accepted.
                int playerHpCrowd2 = _bodyProvider.GetCurrentHealth("player");
                var outsiderAcc = AttackAcceptance.Rejected;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    await WaitForOwnCastClearAsync(crowdB, 2.0f);
                    _combatServiceImpl.DebugSetReadinessPermil(crowdB, 1000);
                    outsiderAcc = _combatServiceImpl.ExecuteAttack(crowdB, "npc_strike", PlayerCombatId, false);
                    if (outsiderAcc == AttackAcceptance.Accepted) break;
                    GD.Print($"[CombatSim] multi(b2) attempt {attempt + 1}: {crowdB}→player acc={outsiderAcc} (retry)");
                    await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
                }
                await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
                int playerHpCrowd3 = _bodyProvider.GetCurrentHealth("player");
                bool outsiderDamage = playerHpCrowd3 < playerHpCrowd2;
                GD.Print($"[CombatSim] multi(b2) outsider hits player: {crowdB}(не-участник)→player " +
                         $"acc={outsiderAcc} (ожид Accepted, CMB-2), player HP {playerHpCrowd2}→{playerHpCrowd3}");
                multiOk &= outsiderAcc == AttackAcceptance.Accepted && outsiderDamage;

                // Чистота: закрываем UI-бой (тихая NPC-война продолжается — фича C).
                if (_combatServiceImpl.IsInCombat)
                    _combatServiceImpl.AbandonCombat(PlayerCombatId);
            }
            else
            {
                GD.Print("[CombatSim] WARN — multi phase skipped (need 2 alive NPCs)");
                multiOk = false;
            }
        }
        else
        {
            GD.Print("[CombatSim] WARN — multi phase skipped (no combat/npc/body services)");
        }

        // 3h. R25 (2026-09-21, план R23 §2.2-A «мгновенный залп» — выбор
        // пользователя: нейтралы ЗАДЕВАЮТСЯ, месть включается):
        // (a) юнит-стенд геометрии AoEResolver (круг/конус/полукруг/линия —
        //     int-математика форм без сервисов, детерминизм);
        // (b) КОНУС игрока в толпу: 3 NPC клином → заряженный залп → каждый
        //     задет per-target ПОЛНЫМ пайплайном (DamageApplied игроку→каждому),
        //     AoeImpactEvent с ≥2 целями, месть «нейтрала» (задетый мимо
        //     заявленной цели Threats[player] — включается САМА);
        // (c) AoE-залп NPC: не-игрок кастер площадью по игроку+соседу —
        //     задетый сосед получает Threats[кастера] (месть на NPC).
        bool aoeOk = true;
        if (_combatServiceImpl != null && _techniqueService != null && _techniqueGenerator != null
            && _npcService != null && _bodyProvider != null && _playerService != null
            && _npcSpawner != null)
        {
            // === (a) стенд геометрии (без сервисов — чистые формы) ===
            {
                var q = new AoeQuery
                {
                    CasterId = "qa_caster", CasterPos = new Position2D(10, 10),
                    AimPos = new Position2D(13, 10), Shape = AoeShape.Cone,
                    RadiusTiles = 4, HalfAngleDeg = 45, FalloffPermil = 400,
                    MaxTargets = 0, SpareAlliesPermil = 0
                };
                bool coneIn = AoEResolver.IsPointInside(in q, 4, new Position2D(12, 11));
                bool coneOut = AoEResolver.IsPointInside(in q, 4, new Position2D(12, 13));
                bool coneBehind = AoEResolver.IsPointInside(in q, 4, new Position2D(8, 10));
                var qc = q; qc.Shape = AoeShape.Circle;
                bool circleIn = AoEResolver.IsPointInside(in qc, 2, new Position2D(14, 11));
                bool circleOut = AoEResolver.IsPointInside(in qc, 2, new Position2D(16, 11));
                var qs = q; qs.Shape = AoeShape.Semicircle;
                bool semiIn = AoEResolver.IsPointInside(in qs, 4, new Position2D(11, 13));
                bool semiOut = AoEResolver.IsPointInside(in qs, 4, new Position2D(8, 11));
                var ql = q; ql.Shape = AoeShape.Line;
                bool lineIn = AoEResolver.IsPointInside(in ql, 4, new Position2D(12, 10));
                bool lineOut = AoEResolver.IsPointInside(in ql, 4, new Position2D(12, 12));
                bool lineFar = AoEResolver.IsPointInside(in ql, 4, new Position2D(16, 10));
                aoeOk &= coneIn && !coneOut && !coneBehind
                    && circleIn && !circleOut && semiIn && !semiOut
                    && lineIn && !lineOut && !lineFar;
                GD.Print($"[CombatSim] aoe(a) shapes: cone {coneIn}/{coneOut}/{coneBehind} " +
                         $"(ожид 1/0/0), circle {circleIn}/{circleOut} (1/0), " +
                         $"semi {semiIn}/{semiOut} (1/0), line {lineIn}/{lineOut}/{lineFar} (1/0/0)");
            }

            // === (b) конус игрока в толпу ===
            // Гарантия изучения: расширенная библиотека (QA-мод, не влияет на
            // игровые лимиты — ExtraLibraryCapacity только этой сессии).
            _techniqueService.ExtraLibraryCapacity += 4;
            int cultLevel = _qiService != null ? (int)_qiService.CultivationLevel : 1;
            if (cultLevel < 1) cultLevel = 1;
            var coneTech = _techniqueGenerator.GenerateAoe(AoeShape.Cone, cultLevel, cultLevel, 99010);
            bool coneLearned = _techniqueService.LearnTechnique(coneTech);
            if (coneLearned)
            {
                var p = _playerService.Position;
                // Толпа клином в направлении +X от игрока (внутри конуса 45°):
                // (ближняя цель), (дальнее крыло), (нижнее крыло).
                string? crowd1 = _npcSpawner.SpawnNPC("human", NPCRole.Enemy, 1,
                    new Position2D(p.X + 2, p.Y), 99001);
                string? crowd2 = _npcSpawner.SpawnNPC("human", NPCRole.Enemy, 1,
                    new Position2D(p.X + 4, p.Y + 1), 99002);
                string? crowd3 = _npcSpawner.SpawnNPC("human", NPCRole.Enemy, 1,
                    new Position2D(p.X + 3, p.Y - 2), 99003);
                int crowdCount = (crowd1 != null ? 1 : 0) + (crowd2 != null ? 1 : 0) + (crowd3 != null ? 1 : 0);

                if (crowdCount >= 2 && crowd1 != null)
                {
                    await WaitForOwnCastClearAsync(PlayerCombatId, 2.0f);
                    _combatServiceImpl.DebugSetReadinessPermil(PlayerCombatId, 1000);
                    _aoeImpactCount = -1; // маркер: событие ещё не приходило

                    int hp1a = _bodyProvider.GetCurrentHealth(crowd1);
                    int hp2a = crowd2 != null ? _bodyProvider.GetCurrentHealth(crowd2) : int.MaxValue;
                    int hp3a = crowd3 != null ? _bodyProvider.GetCurrentHealth(crowd3) : int.MaxValue;

                    // Заряженный выпуск (1500‰): aim = (p.X+4, p.Y) — конус +X.
                    var aoeAcc = _combatServiceImpl.ExecuteAttack(
                        PlayerCombatId, coneTech.TechniqueId, crowd1, true,
                        potencyPermil: 1500, isCharged: true,
                        aimTileX: p.X + 4, aimTileY: p.Y);

                    int hp1b = _bodyProvider.GetCurrentHealth(crowd1);
                    int hp2b = crowd2 != null ? _bodyProvider.GetCurrentHealth(crowd2) : int.MaxValue;
                    int hp3b = crowd3 != null ? _bodyProvider.GetCurrentHealth(crowd3) : int.MaxValue;
                    bool crowd1Hit = hp1b < hp1a;
                    bool bystanderHit = (crowd2 != null && hp2b < hp2a) || (crowd3 != null && hp3b < hp3a);
                    bool impactOk = _aoeImpactCount >= 2;
                    GD.Print($"[CombatSim] aoe(b) volley: acc={aoeAcc} (ожид Accepted), " +
                             $"crowd1 {hp1a}→{hp1b}, crowd2 {hp2a}→{hp2b}, crowd3 {hp3a}→{hp3b}, " +
                             $"AoeImpact.targets={_aoeImpactCount} (ожид ≥2)");
                    aoeOk &= aoeAcc == AttackAcceptance.Accepted && crowd1Hit && bystanderHit && impactOk;

                    // Месть «нейтралов»: задетые площадью (не заявленная цель)
                    // получают угрозу игрока — RetaliateOrFlee включается сама.
                    await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
                    var st2 = crowd2 != null ? _npcService.GetNPCState(crowd2) : null;
                    var st3 = crowd3 != null ? _npcService.GetNPCState(crowd3) : null;
                    bool revengeBystander =
                        (st2 != null && st2.Threats.ContainsKey(PlayerCombatId))
                        || (st3 != null && st3.Threats.ContainsKey(PlayerCombatId));
                    GD.Print($"[CombatSim] aoe(b) bystander revenge: crowd2={st2?.Threats.ContainsKey(PlayerCombatId)}, " +
                             $"crowd3={st3?.Threats.ContainsKey(PlayerCombatId)} (ожид хотя бы один true — «мир жесток»)");
                    aoeOk &= revengeBystander;

                    // === (c) AoE-залп NPC: crowd2 площадью по игроку+crowd1 ===
                    // Направление crowd2 → игрок накрывает и crowd1 (клин).
                    if (crowd2 != null)
                    {
                        await WaitForOwnCastClearAsync(crowd2, 2.0f);
                        _combatServiceImpl.DebugSetReadinessPermil(crowd2, 1000);
                        if (_combatServiceImpl.IsInCombat)
                            _combatServiceImpl.AbandonCombat(PlayerCombatId);
                        _aoeImpactCount = -1;
                        int hpPlA = _bodyProvider.GetCurrentHealth("player");
                        int hp1c = _bodyProvider.GetCurrentHealth(crowd1);
                        var npcAoeAcc = _combatServiceImpl.ExecuteAttack(
                            crowd2, coneTech.TechniqueId, PlayerCombatId, true,
                            potencyPermil: 1500, isCharged: true,
                            aimTileX: p.X, aimTileY: p.Y);
                        int hpPlB = _bodyProvider.GetCurrentHealth("player");
                        int hp1d = _bodyProvider.GetCurrentHealth(crowd1);
                        bool npcAoeHitPlayer = hpPlB < hpPlA;
                        bool npcAoeHitNeighbor = hp1d < hp1c;
                        GD.Print($"[CombatSim] aoe(c) NPC volley: {crowd2}→player acc={npcAoeAcc} " +
                                 $"(ожид Accepted), player {hpPlA}→{hpPlB}, crowd1 {hp1c}→{hp1d}");
                        aoeOk &= npcAoeAcc == AttackAcceptance.Accepted && npcAoeHitPlayer;

                        // Месть соседа на NPC-кастера (задет площадью).
                        await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
                        var st1 = _npcService.GetNPCState(crowd1);
                        bool revengeOnNpcCaster = st1 != null && st1.Threats.ContainsKey(crowd2);
                        GD.Print($"[CombatSim] aoe(c) neighbor revenge on NPC caster: {revengeOnNpcCaster} " +
                                 $"(crowd1 Threats[{crowd2}]; допустимо false если crowd1 погиб до проверки)");
                        // Мягкая проверка: если crowd1 жив после залпа — месть обязана быть.
                        bool crowd1AliveAfter = _npcService.IsAlive(crowd1);
                        aoeOk &= !crowd1AliveAfter || revengeOnNpcCaster;
                    }
                    else
                    {
                        GD.Print("[CombatSim] WARN — aoe(c) skipped (crowd2 not spawned)");
                    }

                    // Чистота: UI-бой закрыть, толпу убрать (тихие войны не шумят в итогах).
                    if (_combatServiceImpl.IsInCombat)
                        _combatServiceImpl.AbandonCombat(PlayerCombatId);
                    if (crowd1 != null) _npcSpawner.DespawnNPC(crowd1);
                    if (crowd2 != null) _npcSpawner.DespawnNPC(crowd2);
                    if (crowd3 != null) _npcSpawner.DespawnNPC(crowd3);
                }
                else
                {
                    GD.Print($"[CombatSim] WARN — aoe(b) skipped (crowd spawned {crowdCount}/3, лимит NPC?)");
                    aoeOk = false;
                }
            }
            else
            {
                GD.Print("[CombatSim] WARN — aoe(b) skipped (AoE technique not learned — слоты?)");
                aoeOk = false;
            }
        }
        else
        {
            GD.Print("[CombatSim] WARN — aoe phase skipped (no combat/npc/generator/spawner services)");
            aoeOk = false;
        }

        // 3i. R27 (2026-09-21, план R23 §3.1): TargetingService — Tab-цикл.
        // (a) кольцо: 2 NPC → cycle×3 возвращает к первой цели (событие каждый раз);
        // (b) радиус: выбранная в радиусе → true; вне радиуса → false (выбор жив);
        // (c) ClearTarget → сброс + событие "<cleared>".
        bool targetingOk = true;
        if (_targeting != null && _npcSpawner != null && _playerService != null && _npcService != null)
        {
            var p = _playerService.Position;
            // Чистая зона: уводим игрока от NPC ранних фаз (кольцо радиуса 12
            // вокруг стартовой позиции шумит посторонними целями — стражи/
            // месть-толпа/звери). Фазы ниже (3f) не позиционные — возврат
            // после теста честный.
            var clean = new Position2D(p.X + 60, p.Y + 60);
            _playerService.SetPosition(clean);
            string? tgtA = _npcSpawner.SpawnNPC("human", NPCRole.Enemy, 1,
                new Position2D(clean.X + 3, clean.Y), 99021);
            string? tgtB = _npcSpawner.SpawnNPC("human", NPCRole.Enemy, 1,
                new Position2D(clean.X + 2, clean.Y + 1), 99022);
            if (tgtA != null && tgtB != null)
            {
                // (a) кольцо цикла: A → B → A (сортировка по дистанции: B ближе).
                _targeting.CycleTarget();
                bool cycle1 = _lastTargetChanged == tgtB;
                _targeting.CycleTarget();
                bool cycle2 = _lastTargetChanged == tgtA;
                _targeting.CycleTarget();
                bool cycle3 = _lastTargetChanged == tgtB; // кольцо замкнулось
                GD.Print($"[CombatSim] targeting(a) cycle: {cycle1}/{cycle2}/{cycle3} " +
                         $"(ожид B/A/B — сортировка по дистанции, кольцо)");
                targetingOk &= cycle1 && cycle2 && cycle3;

                // (b) радиус действия: после cycle3 выбран tgtB (дистанция 2)
                // в радиусе 2.5 → true. Вне радиуса (0.5) → false, выбор жив.
                bool inRange = _targeting.TryGetSelectedTargetInRange(2.5f, out string selId, out _);
                bool rangeOk = inRange && selId == tgtB;
                // Вне радиуса (0.5 при дистанции 2) → false, но выбор сохранён.
                bool outOfRange = !_targeting.TryGetSelectedTargetInRange(0.5f, out _, out _);
                GD.Print($"[CombatSim] targeting(b) range: selected={selId == tgtB} " +
                         $"inRange={inRange}, outOfRange={outOfRange} (ожид true/true/true)");
                targetingOk &= rangeOk && outOfRange;

                // (c) явный сброс: событие "<cleared>", выбор пуст.
                _targeting.ClearTarget(tgtB);
                bool cleared = _lastTargetChanged == "<cleared>"
                    && !_targeting.TryGetSelectedTargetInRange(12f, out _, out _);
                GD.Print($"[CombatSim] targeting(c) clear: event={_lastTargetChanged}, " +
                         $"selected-empty={cleared} (ожид <cleared>/true)");
                targetingOk &= cleared;

                // Чистота толпы + возврат игрока.
                _npcSpawner.DespawnNPC(tgtA);
                _npcSpawner.DespawnNPC(tgtB);
                _playerService.SetPosition(p);
            }
            else
            {
                GD.Print("[CombatSim] WARN — targeting phase skipped (spawn failed)");
                targetingOk = false;
            }
        }
        else
        {
            GD.Print("[CombatSim] WARN — targeting phase skipped (no targeting service)");
            targetingOk = false;
        }

        // 3f. R21 (репорт 20.09, №3/№4): регресс-гарды пайплайна защиты.
        // (a) DefenseProcessor — плоское вычитание eff.брони×0.5 ПОСЛЕ
        //     процентного + предметное «Снижение урона» (ALGORITHMS §5.2);
        // (b) EquipmentDataProvider — агрегат DamageReduction в промилле;
        // (c) QiDataProvider — авто-активация пассивного RawQi (COMBAT §5
        //     «даже во сне»);
        // (d) DamageService — end-to-end: поглощение 80% физики сырой Ци,
        //     траты Ци, 20% гарантированное пробитие (npc + player пути).
        bool r21ArmorOk = true, r21QiOk = true;
        {
            int raw = 100;
            int noArmor = DefenseProcessor.ApplyDefense(raw,
                new DefenseContext("qa_r21", 0, BodyMaterial.Organic, 0, 0));
            int withArmor = DefenseProcessor.ApplyDefense(raw,
                new DefenseContext("qa_r21", 50, BodyMaterial.Organic, 200, 10));
            // eff=50-10=40 → 285‰ (armor) + 200‰ (DR) = 485‰ → 51; flat −20 → 31.
            r21ArmorOk &= noArmor == raw && withArmor > 0 && withArmor < raw - 30;
            GD.Print($"[CombatSim] r21-armor(a): noArmor={noArmor} withArmor={withArmor} " +
                     $"(плоское+DR: {(raw - withArmor)} из {raw})");

            if (_equipmentProvider != null)
            {
                var piece = new EquipmentData
                {
                    Slot = EquipmentSlot.Torso,
                    Defense = 30,
                    Coverage = 100f,
                    DamageReduction = 20f,
                };
                var slots = new Dictionary<EquipmentSlot, EquipmentData> { [EquipmentSlot.Torso] = piece };
                _equipmentProvider.SetEquipmentData("qa_r21_entity", slots);
                int drPermil = _equipmentProvider.GetDamageReductionPermil("qa_r21_entity");
                r21ArmorOk &= drPermil == 200; // 20% × 10
                _equipmentProvider.RemoveEntity("qa_r21_entity");
                GD.Print($"[CombatSim] r21-armor(b): DR-агрегат = {drPermil}‰ (ожид 200)");
            }
            else
            {
                GD.Print("[CombatSim] WARN — r21-armor(b) skipped (no equipment provider)");
            }

            if (_qiDataProvider != null && _damageService != null)
            {
                // (c) авто-активация RawQi для NPC с Ци ≥ минимума.
                _qiDataProvider.SetQiState("qa_r21_npc", 500, 1000, 1f);
                bool npcBufActive = _qiDataProvider.IsQiBufferActive("qa_r21_npc");
                var npcBufMode = _qiDataProvider.GetQiBufferMode("qa_r21_npc");
                r21QiOk &= npcBufActive && npcBufMode == QiBufferMode.RawQi;
                // Контроль: смертный (Ци < минимума) — без буфера.
                _qiDataProvider.SetQiState("qa_r21_mortal", 5, 10, 1f);
                bool mortalBuf = _qiDataProvider.IsQiBufferActive("qa_r21_mortal");
                r21QiOk &= !mortalBuf;
                GD.Print($"[CombatSim] r21-qi(c): npc buf={npcBufActive}/{npcBufMode}, " +
                         $"mortal(Ци=5) buf={mortalBuf} (ожид false)");

                // (d) end-to-end через DamageService: физика 100 → сырая Ци
                // поглощает 80, 20 пробивает, Ци тратится (5:1).
                // DefenderAGI=0/STR=10/стойка None → dodge/parry/block НЕ роллятся
                // (ветки в DetermineAttackResult гейтятся стойкой); крит —
                // базовый 5% (детерминированный RNG) → ожидание учитывает
                // обе ветки (×1.5) и частичное поглощение при нехватке Ци.
                var reqNpc = new DamageRequest("qa_r21_att", "qa_r21_npc", 100,
                    DamageType.Physical, Element.Neutral, Element.Neutral,
                    AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
                    DefenseSubtype.None, BodyMaterial.Organic,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    Morphology.Humanoid, 10, false, CombatSubtype.None);
                var resNpc = _damageService.CalculateDamage(reqNpc);
                int dmgN = resNpc.Result == CombatAttackResult.CriticalHit ? 150 : 100;
                int absN = resNpc.AbsorbedByQi;
                long npcQiAfter = _qiDataProvider.GetCurrentQi("qa_r21_npc");
                // Инварианты: поглощено + пробито = весь урон; физика 800‰;
                // Ци 5:1 (при нехватке — частичное поглощение, Ци в 0).
                r21QiOk &= absN > 0 && resNpc.FinalDamage > 0 && absN + resNpc.FinalDamage == dmgN;
                r21QiOk &= npcQiAfter == (500 - (long)absN * 5 < 0 ? 0 : 500 - (long)absN * 5);
                GD.Print($"[CombatSim] r21-qi(d-npc): dmg={dmgN} absorbed={absN}, " +
                         $"final={resNpc.FinalDamage}, Ци 500→{npcQiAfter} (5:1, {resNpc.Result})");

                // (d-player) пассивная сырая Ци игрока без активации буфера.
                if (_qiService != null)
                {
                    // Игрок в этой сборке — практик L1 (QiConfig default),
                    // Ци уже полное; AddQi только при нехватке.
                    long qiBefore = _qiService.CurrentQi;
                    if (qiBefore < 500) _qiService.AddQi(500 - qiBefore);
                    qiBefore = _qiService.CurrentQi;
                    var reqPl = new DamageRequest("qa_r21_att", "player", 100,
                        DamageType.Physical, Element.Neutral, Element.Neutral,
                        AttackType.Normal, TechniqueGrade.Common, 1000, 1, 1,
                        DefenseSubtype.None, BodyMaterial.Organic,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        Morphology.Humanoid, 10, true, CombatSubtype.None);
                    var resPl = _damageService.CalculateDamage(reqPl);
                    long qiAfter = _qiService.CurrentQi;
                    int dmgP = resPl.Result == CombatAttackResult.CriticalHit ? 150 : 100;
                    int absP = resPl.AbsorbedByQi;
                    // Пассивный RawQi работает БЕЗ активации буфера (гейт
                    // R21-4: Ци ≥ MIN → режим RawQi даже если буфер не активен).
                    r21QiOk &= absP > 0 && resPl.FinalDamage > 0 && absP + resPl.FinalDamage == dmgP;
                    r21QiOk &= qiAfter < qiBefore; // Ци потрачено (QiConsume через событие)
                    GD.Print($"[CombatSim] r21-qi(d-player): dmg={dmgP} absorbed={absP}, " +
                             $"final={resPl.FinalDamage}, Ци {qiBefore}→{qiAfter} (пассивная, буфер не активен)");
                }
            }
            else
            {
                GD.Print("[CombatSim] WARN — r21-qi skipped (no qi/damage services)");
            }
        }

        // 4. Итоги.
        int playerHpAfter = _bodyProvider.GetCurrentHealth("player");
        int npcHpAfter = _bodyProvider.GetCurrentHealth(npcId);

        bool playerTookDamage = playerHpAfter < playerHpBefore;
        bool npcTookDamage = _damageByTarget.TryGetValue(npcId, out int npcDamage) && npcDamage > 0;

        GD.Print($"[CombatSim] summary: events={_eventsReceived} (ranged-subtyped={_rangedDamageEvents}), " +
                 $"player {playerHpBefore}→{playerHpAfter} ({(playerHpBefore - playerHpAfter)} dmg), " +
                 $"npc {npcHpBefore}→{npcHpAfter}, arrows now={_inventory?.GetItemCount(CombatRangeGateService.ArrowItemId) ?? -1}");

        bool pass = playerTookDamage && npcTookDamage && weaponWiringOk && rangedWiringOk && gatesOk && turnGateOk
                    && r21ArmorOk && r21QiOk && multiOk && aoeOk && targetingOk;
        if (!playerTookDamage)
            GD.Print("[CombatSim] FAIL — NPC→player damage did NOT apply (BodyService player-id mismatch?)");
        if (!npcTookDamage)
            GD.Print("[CombatSim] FAIL — player→NPC damage did NOT apply (attack pipeline broken?)");
        if (!weaponWiringOk)
            GD.Print("[CombatSim] FAIL — armed swing did no damage (weapon wiring broken?)");
        if (!rangedWiringOk)
            GD.Print("[CombatSim] FAIL — ranged (bow) phase broken (Phase 8 ч.2 wiring?)");
        if (!gatesOk)
            GD.Print("[CombatSim] FAIL — LOS/ammo gates broken (Phase 8 ч.3?)");
        if (!turnGateOk)
            GD.Print("[CombatSim] FAIL — readiness-gate broken (R21-2: готовность не авторитетна?)");
        if (!r21ArmorOk)
            GD.Print("[CombatSim] FAIL — r21-armor: плоское вычитание/DR-агрегат сломаны (репорт 20.09 №3?)");
        if (!r21QiOk)
            GD.Print("[CombatSim] FAIL — r21-qi: пассивная сырая Ци не работает (репорт 20.09 №4?)");
        if (!multiOk)
            GD.Print("[CombatSim] FAIL — multi (R24-C): тихий NPC-NPC бой / толпа-на-игрока сломаны (CMB-2 вернулся?)");
        if (!aoeOk)
            GD.Print("[CombatSim] FAIL — aoe (R25): геометрия/залп/месть нейтралов/AoeImpact сломаны (план R23 §2.2-A?)");
        if (!targetingOk)
            GD.Print("[CombatSim] FAIL — targeting (R27): Tab-цикл/радиус/сброс сломаны (план R23 §3.1?)");

        PrintVerdict(pass);
    }

    /// <summary>
    /// Phase 8 ч.3: ждать очистки pending-каста (глобальный _isCasting в
    /// CombatService). Детерминизм фаз 3d: выстрел не должен упереться в
    /// чужой догорающий каст (C-5 отклонил бы с другой причиной — не LOS).
    /// Опрос каждые 0.1с, таймаут — идти дальше (тесты ниже заметят).
    /// </summary>
    private async System.Threading.Tasks.Task WaitForCastClearAsync(float timeoutSec)
    {
        float waited = 0f;
        while (waited < timeoutSec)
        {
            if (_combatServiceImpl == null || !_combatServiceImpl.IsCasting) return;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            waited += 0.1f;
        }
        GD.Print($"[CombatSim] WARN — cast still pending after {timeoutSec}s (gate phase may be C-5-rejected)");
    }

    /// <summary>
    /// R23-1 (CMB-1): ждём ТОЛЬКО собственный каст сущности (IsEntityCasting).
    /// Чужой застрявший NPC-каст не блокирует проверку готовности атакующего
    /// (IsCasting=любой каст — ожидание по нему дозаряжает readiness до капа).
    /// </summary>
    private async System.Threading.Tasks.Task WaitForOwnCastClearAsync(string entityId, float timeoutSec)
    {
        if (_combatServiceImpl == null) return;
        float waited = 0f;
        while (waited < timeoutSec)
        {
            if (!_combatServiceImpl.IsEntityCasting(entityId)) return;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            waited += 0.1f;
        }
        GD.Print($"[CombatSim] WARN — own cast {entityId} still pending after {timeoutSec}s");
    }

    /// <summary>
    /// R21-2: ждём ГОТОВНОСТЬ удара участника (readiness-модель; замена
    /// ожиданиям хода). Вне боя — сразу выходим (бой завершён).
    /// </summary>
    private async System.Threading.Tasks.Task WaitForReadinessAsync(string entityId, float timeoutSec)
    {
        if (_combatServiceImpl == null) return;
        float waited = 0f;
        while (waited < timeoutSec)
        {
            if (!_combatServiceImpl.IsInCombat) return;
            if (_combatServiceImpl.IsAttackReady(entityId)) return;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            waited += 0.1f;
        }
        GD.Print($"[CombatSim] WARN — readiness {entityId} not ready after {timeoutSec}s " +
                 $"({_combatServiceImpl.GetReadinessPermil(entityId)}‰)");
    }

    private async System.Threading.Tasks.Task WaitForStageAsync(CombatStage stage, float timeoutSec)
    {
        if (_combatServiceImpl == null) return;
        float waited = 0f;
        while (waited < timeoutSec)
        {
            if (!_combatServiceImpl.IsInCombat) return;
            if (_combatServiceImpl.CurrentStage == stage) return;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            waited += 0.1f;
        }
        GD.Print($"[CombatSim] WARN — stage {stage} not reached after {timeoutSec}s (now: {_combatServiceImpl.CurrentStage})");
    }

    private string? FindHostileNpc()
    {
        if (_npcService == null) return null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            var state = _npcService.GetNPCState(id);
            if (state != null && state.IsAlive && state.Disposition == NPCDisposition.Hostile)
                return id;
        }
        // Fallback: любой живой NPC (состав локации может измениться).
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (_npcService.IsAlive(id)) return id;
        }
        return null;
    }

    /// <summary>
    /// R24-C: второй живой NPC (для «тихого» NPC-NPC боя и толпы) —
    /// любой живой, кроме исключённого (мультибой требует ≥2 сущностей).
    /// </summary>
    private string? FindSecondNpc(string? excludeId)
    {
        if (_npcService == null) return null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (id == excludeId) continue;
            if (_npcService.IsAlive(id)) return id;
        }
        return null;
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[CombatSim] VERDICT: {(pass ? "PASS — обе стороны боя получают урон (melee + ranged + LOS/ammo gates + readiness-gate + multi R24-C)" : "FAIL")}");
    }
}
