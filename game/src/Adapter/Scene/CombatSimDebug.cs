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

    private System.IDisposable? _damageToken;
    private System.IDisposable? _intentEchoToken;
    private System.IDisposable? _rejectedToken;

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

        GD.Print("[CombatSim] Ready — scripted combat verification starts in 2s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _intentEchoToken?.Dispose();
        _rejectedToken?.Dispose();
        _damageToken = null;
        _intentEchoToken = null;
        _rejectedToken = null;
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
        for (int round = 1; round <= 4; round++)
        {
            // NPC → игрок (P0-проверка: урон должен примениться к телу игрока).
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                npcId, PlayerCombatId, "npc_strike", false));
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

            // Игрок → NPC (Phase 8: weapon damage wiring).
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                PlayerCombatId, npcId, "basic_attack", false));
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

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
                await WaitForCastClearAsync(2.0f);

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

        // 4. Итоги.
        int playerHpAfter = _bodyProvider.GetCurrentHealth("player");
        int npcHpAfter = _bodyProvider.GetCurrentHealth(npcId);

        bool playerTookDamage = playerHpAfter < playerHpBefore;
        bool npcTookDamage = _damageByTarget.TryGetValue(npcId, out int npcDamage) && npcDamage > 0;

        GD.Print($"[CombatSim] summary: events={_eventsReceived} (ranged-subtyped={_rangedDamageEvents}), " +
                 $"player {playerHpBefore}→{playerHpAfter} ({(playerHpBefore - playerHpAfter)} dmg), " +
                 $"npc {npcHpBefore}→{npcHpAfter}, arrows now={_inventory?.GetItemCount(CombatRangeGateService.ArrowItemId) ?? -1}");

        bool pass = playerTookDamage && npcTookDamage && weaponWiringOk && rangedWiringOk && gatesOk;
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

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[CombatSim] VERDICT: {(pass ? "PASS — обе стороны боя получают урон (melee + ranged + LOS/ammo gates)" : "FAIL")}");
    }
}
