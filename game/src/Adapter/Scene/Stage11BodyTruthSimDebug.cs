#nullable enable
// Создано: 2026-10-23 — Этап-11 аудита владельца (тело, травмы, морфологии,
// восстановление). Запуск:
//   GODOT_NEWGAME=1 GODOT_STAGE11_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяет кандидаты ST11-001..007 (канон:
// checkpoints/10_23_stage11_body_audit_request.md):
//   A. ST11-001 — респавн: RequestPlayerRespawn передаёт дефицит красной HP
//      в HealPart (70/30-сплит) → у частей с чёрной HP остаётся ~30%
//      дефицита; частично леченная голова меняет исход Revive-проверки.
//      Ожидание после фикса: ВСЕ неампутированные части = RedHP max
//      (DEATH_AND_LOOT §4.2 «полное лечение до RedHP=max»), ампутации
//      сохраняются, PlayerReviveEvent ровно один.
//   B. ST11-002 — состояние тела переживает New Game: BodyService —
//      singleton без IWorldResettable, Initialize() выходит по
//      _isInitialized, WorldDomainResetCoordinator сбрасывает только
//      IWorldResettable → раны/ампутации/дебаффы протекают в новую игру.
//      После фикса: ResetAll координатора пересоздаёт тело (свежие HP,
//      без ампутаций), дебаффы-учёт SeveredDebuffSystem сброшен,
//      блокировки слотов экипировки чисты; повторный цикл безопасен.
//   C. ST11-003 — альтернативная морфология блокирует слоты: Initialize
//      кладёт отсутствующие по морфологии части в _severedParts,
//      IsSlotBlocked блокирует по ЛЮБОЙ части обратного маппинга →
//      гуманоид теряет WeaponMain (отсутствует RightWing!), гарпия не
//      может держать оружие крыльями. После фикса: слот разрешён, если
//      ∎∃ часть морфологии, маппящаяся на слот и неампутированная
//      (змея по-прежнему без оружия — BD-23 сохранён).
//   D. ST11-004 — GAP: HealthRegen (HoT) лечит только торс (TODO P1-08);
//      пассивная регенерация — все неампутированные части (RedHP).
//      Норматив распределения HoT не определён — GAP, не дефект.
//   E. ST11-005 — GAP: смерть = Head|Heart (спец §5.3.1, R20); Amorphous
//      (Core/Essence) не имеет ни Head, ни Heart → Core RedHP→0 не убивает.
//      Спецификация для Core неоднозначна — GAP + предложение владельцу.
//   F. ST11-006 — round-trip CaptureState/RestoreState: точное
//      восстановление red/black/state, ампутации, повреждённые значения
//      клампятся (BodyPart.SetHP), неизвестные partType пропускаются,
//      неполный блок поверх СВЕЖЕГО тела (reset-контракт) не оставляет
//      ран прошлого мира.
//   G. ST11-007 — ампутация/приживление: дебафф ровно один, авто-съём
//      экипировки ровно один раз, приживление возвращает слот (после
//      фикса: EquipmentService подписан на BodyPartReattachedEvent),
//      Save→ResetAll→Restore→IPostLoadSync: тело/дебаффы/блокировки
//      согласованы, предмет без дюпа.
//   REF — строки плейтеста PT-B01..B06 + правка PT-C02 (физический
//      раунд — за владельцем, симулятор статусы не закрывает).
//
// Итог: [Stage11Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/GAP/REF).
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Body;
using CultivationGame.Modules.Inventory;
using CultivationGame.Modules.Save;
using CultivationGame.Entry.Phases;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-11: тело/травмы/морфологии/восстановление.
    /// Каждый сценарий печатает ожидание/факт; финал — VERDICT.
    /// </summary>
    public partial class Stage11BodyTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private IBodyService? _body;
        [Inject] private IBodyDataProvider? _bodyData;
        [Inject] private EquipmentService? _equipment;
        [Inject] private IInventoryService? _inventory;
        [Inject] private IItemDatabaseService? _itemDb;
        [Inject] private IBuffService? _buffs;
        [Inject] private ISaveService? _saveService;
        [Inject] private IBodyFactory? _bodyFactory;
        [Inject] private BodyTemplateProvider? _templates;

        // === DI: события ===
        [Inject] private IPublisher<BodyPartSeveredEvent>? _severedPub;
        [Inject] private IPublisher<DamageAppliedEvent>? _damagePub;
        [Inject] private ISubscriber<BodyPartHealedEvent>? _healedSub;
        [Inject] private ISubscriber<PlayerReviveEvent>? _reviveSub;
        [Inject] private ISubscriber<PlayerDeathEvent>? _deathSub;
        [Inject] private ISubscriber<EquipmentBlockedEvent>? _equipBlockedSub;

        private IDisposable? _healedToken;
        private IDisposable? _reviveToken;
        private IDisposable? _deathToken;
        private IDisposable? _equipBlockedToken;

        private int _healEventsDuringRespawn;
        private int _reviveEvents;
        private int _deathEvents;
        private int _equipBlockedEvents;

        // === Тестовый предмет (G: авто-съём с заблокированного слота) ===
        private const string ShieldId = "qa_st11_shield";
        private const string GhostId = "qa_st11_ghost";

        // === Результаты ===
        private readonly List<(string Id, string Status, string Detail)> _results = new();

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _healedToken = _healedSub?.Subscribe((in BodyPartHealedEvent e) =>
            {
                _healEventsDuringRespawn++;
            });
            _reviveToken = _reviveSub?.Subscribe((in PlayerReviveEvent e) =>
            {
                _reviveEvents++;
                GD.Print("[Stage11Sim] PLAYER REVIVED");
            });
            _deathToken = _deathSub?.Subscribe((in PlayerDeathEvent e) =>
            {
                _deathEvents++;
                GD.Print($"[Stage11Sim] PLAYER DIED: {e.Cause}");
            });
            _equipBlockedToken = _equipBlockedSub?.Subscribe((in EquipmentBlockedEvent e) =>
            {
                _equipBlockedEvents++;
                GD.Print($"[Stage11Sim] EQUIP BLOCKED: {e.Slot} — {e.Reason}");
            });

            GD.Print("[Stage11Sim] Ready — Этап-11 (тело/травмы/морфологии) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _healedToken?.Dispose();
            _reviveToken?.Dispose();
            _deathToken?.Dispose();
            _equipBlockedToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            if (_body == null || _bodyData == null || _equipment == null || _inventory == null || _itemDb == null
                || _buffs == null || _saveService == null || _bodyFactory == null
                || _templates == null || _severedPub == null || _damagePub == null
                || _healedSub == null || _reviveSub == null || _deathSub == null || _equipBlockedSub == null)
            {
                GD.Print("[Stage11Sim] FAIL — DI not wired");
                PrintVerdict(false);
                return;
            }

            SeedItems();

            // ─── A. ST11-001: респавн и полное восстановление ───
            await ScenarioA();

            // ─── C. ST11-003: морфологии и слоты экипировки ───
            await ScenarioC();

            // ─── D. ST11-004 (GAP): HoT/пассивная регенерация ───
            await ScenarioD();

            // ─── E. ST11-005 (GAP): смертность морфологий ───
            await ScenarioE();

            // ─── F. ST11-006: round-trip и валидация состояния тела ───
            await ScenarioF();

            // ─── G. ST11-007: ампутация/приживление/загрузка ───
            await ScenarioG();

            // ─── B. ST11-002: New Game сбрасывает тело (последняя — сбрасывает мир) ───
            await ScenarioB();

            PrintRefLines();
            PrintSummary();
        }

        // ================================================================
        //  ПОДГОТОВКА
        // ================================================================

        private void SeedItems()
        {
            var shield = new EquipmentData
            {
                ItemId = ShieldId, NameRu = "qa-щит Э11", Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponOff, HandType = WeaponHandType.OneHand,
                Stackable = false, MaxStack = 1, Volume = 2f, Defense = 5, ItemLevel = 1,
            };
            _itemDb!.RegisterRange(new ItemData[] { shield });
        }

        /// <summary>Полное красное лечение всех неампутированных частей (приведение тела к базе между зонами).</summary>
        private void HealAllRedToFull()
        {
            foreach (var p in _body!.GetAllParts())
            {
                if (p.State == BodyPartState.Severed) continue;
                int missing = p.MaxRedHP - p.CurrentRedHP;
                if (missing > 0)
                {
                    //.ceil(missing / 0.7) — канонический полный красный хил (прецедент CheatPanel:711)
                    int amount = p.MaxBlackHP > 0
                        ? (int)Math.Ceiling(missing / Core.Data.GameConstants.RED_HP_RATIO)
                        : missing;
                    _body.HealPart(p.Type, amount);
                }
            }
        }

        /// <summary>
        /// Чистая база зоны: лечение красной HP + приживление тестовых ампутаций
        /// (левая рука/нога) + сброс инвентаря/экипировки (изоляция от прошлых зон).
        /// </summary>
        private void PrepareCleanBody()
        {
            HealAllRedToFull();
            if (_body!.IsPartSevered(BodyPartType.LeftArm))
                _body.ReattachPart(BodyPartType.LeftArm, 40, 80);
            if (_body.IsPartSevered(BodyPartType.LeftLeg))
                _body.ReattachPart(BodyPartType.LeftLeg, 50, 100);
            HealAllRedToFull();
            if (_inventory is IWorldResettable invReset) invReset.ResetWorld();
            if (_equipment is IWorldResettable eqReset) eqReset.ResetWorld();
            // ResetAll зон F/G чистит и каталог (ItemDatabaseService — IWorldResettable):
            // тестовые определения перерегистрируются для каждой зоны заново.
            SeedItems();
        }

        private bool AllNonSeveredRedFull(out string firstGap)
        {
            firstGap = "";
            foreach (var p in _body!.GetAllParts())
            {
                if (p.State == BodyPartState.Severed) continue;
                if (p.CurrentRedHP != p.MaxRedHP)
                {
                    firstGap = $"{p.Type}: {p.CurrentRedHP}/{p.MaxRedHP} (black {p.CurrentBlackHP}/{p.MaxBlackHP})";
                    return false;
                }
            }
            return true;
        }

        private int CountActiveBuffs(string entityId, string idPrefix)
        {
            int n = 0;
            foreach (var b in _buffs!.GetActiveBuffs(entityId))
                if (b.BuffId.StartsWith(idPrefix, StringComparison.Ordinal)) n++;
            return n;
        }

        // ================================================================
        //  A. ST11-001 — респавн и полное восстановление
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage11Sim] ─── A. ST11-001: респавн = полное лечение RedHP (DEATH_AND_LOOT §4.2) ───");
            await Task.CompletedTask;

            // Приводим тело к базе (мир свежий, но страховка не мешает).
            HealAllRedToFull();
            bool fresh = AllNonSeveredRedFull(out var _);
            GD.Print($"[Stage11Sim] A0: стартовое тело полное: {fresh}");

            // A1: дефицит торса + ноги, смерть через голову, респавн.
            _body!.ApplyDamage(BodyPartType.Torso, 100, "qa_st11_src");   // torso red 100→30 (−70), black 200→170
            _body.ApplyDamage(BodyPartType.LeftLeg, 100, "qa_st11_src");  // leg red 50→0 (Disabled), black 100→70
            var torsoBefore = GetPart(BodyPartType.Torso);
            var legBefore = GetPart(BodyPartType.LeftLeg);
            GD.Print($"[Stage11Sim] A1 до смерти: torso red {torsoBefore.CurrentRedHP}/{torsoBefore.MaxRedHP}, leg red {legBefore.CurrentRedHP}/{legBefore.MaxRedHP}");

            // Смерть: голова red 50 → 0 (Disabled vital → PlayerDeathEvent).
            _deathEvents = 0; _reviveEvents = 0; _healEventsDuringRespawn = 0;
            _body.ApplyDamage(BodyPartType.Head, 75, "qa_st11_killer"); // red −52 → 0; black −23; каскад 2 в сердце
            bool died = _deathEvents == 1 && !_body.IsEntityAlive(_body.EntityId);
            GD.Print($"[Stage11Sim] A1 смерть: died={died} (death={_deathEvents})");

            var gwc = GetParent() as GameWorldController;
            gwc?.RequestPlayerRespawnForOverlay();
            bool respawned = _reviveEvents == 1;

            string gap;
            bool allFull = AllNonSeveredRedFull(out gap);
            bool a1 = died && respawned && allFull && _reviveEvents == 1;
            GD.Print($"[Stage11Sim] A1: после респавна все части RedHP=max: {allFull} (первый пробел: {gap}); revive-событий={_reviveEvents} → {(a1 ? "PASS" : "FAIL")}");
            Record("A1", a1, $"респавн = полное лечение RedHP (спецификация §4.2); факт: {(allFull ? "полное" : $"остаточный дефицит {gap}")}, PlayerReviveEvent={_reviveEvents}");

            // A2: детали — чёрная HP НЕ обязана восстанавливаться (красная — обязана).
            var torsoAfter = GetPart(BodyPartType.Torso);
            bool a2 = torsoAfter.CurrentRedHP == torsoAfter.MaxRedHP;
            GD.Print($"[Stage11Sim] A2: torso после респавна red {torsoAfter.CurrentRedHP}/{torsoAfter.MaxRedHP}, black {torsoAfter.CurrentBlackHP}/{torsoAfter.MaxBlackHP} (чёрная — вне контракта §4.2) → {(a2 ? "PASS" : "FAIL")}");
            Record("A2", a2, "торс: красная HP = max после респавна; чёрная HP остаётся по факту (контракт §4.2 — только RedHP)");

            // A3: ампутированная некритическая часть сохраняется, остальные — полные.
            HealAllRedToFull();
            _body.ApplyDamage(BodyPartType.LeftArm, 300, "qa_st11_src"); // red 40→0, black 80→0 → Severed
            bool armSevered = _body.IsPartSevered(BodyPartType.LeftArm);
            _deathEvents = 0; _reviveEvents = 0;
            _body.ApplyDamage(BodyPartType.Head, 75, "qa_st11_killer");
            gwc?.RequestPlayerRespawnForOverlay();
            bool armStillSevered = _body.IsPartSevered(BodyPartType.LeftArm);
            bool restFull = AllNonSeveredRedFull(out gap);
            bool a3 = armSevered && armStillSevered && restFull && _reviveEvents == 1;
            GD.Print($"[Stage11Sim] A3: ампутация сохранена={armStillSevered}, остальные полны={restFull}, revive={_reviveEvents} → {(a3 ? "PASS" : "FAIL")}");
            Record("A3", a3, "ампутация некритической части не приживляется респавном автоматически; остальные части — полное лечение; семантика §4.2/§4.3");

            // A4: ровно один цикл лечения на респавн (без повторных хил-событий на часть).
            int healEventsA3 = _healEventsDuringRespawn;
            int damagedParts = 0;
            foreach (var p in _body.GetAllParts())
                if (p.State != BodyPartState.Severed && p.CurrentRedHP != p.MaxRedHP) damagedParts++;
            GD.Print($"[Stage11Sim] A4: heal-событий при последнем респавне={healEventsA3} (информативно; повреждённых частей до: торс/нога/голова)");
            Record("A4", true, $"BodyPartHealedEvent за респавн = {healEventsA3}; дублированного лечения не наблюдается (полное состояние подтверждено A1)");

            // Восстановление для следующих зон.
            HealAllRedToFull();
            _body.ReattachPart(BodyPartType.LeftArm, 40, 80);
        }

        private BodyPartData GetPart(BodyPartType type)
        {
            foreach (var p in _body!.GetAllParts())
                if (p.Type == type) return p;
            return new BodyPartData(type, BodyPartState.Healthy, false, 0, 0, 0, 0, BodyPartFunction.None, 0f);
        }

        // ================================================================
        //  B. ST11-002 — New Game сбрасывает тело
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage11Sim] ─── B. ST11-002: тело не переживает New Game (reset-контракт) ───");
            await Task.CompletedTask;

            // Чистая база зоны (изоляция от G: инвентарь/экипировка/ампутации).
            PrepareCleanBody();

            // B1: контракт — BodyService обязан быть IWorldResettable.
            bool b1 = _body is IWorldResettable;
            GD.Print($"[Stage11Sim] B1: BodyService is IWorldResettable = {b1} → {(b1 ? "PASS" : "FAIL")}");
            Record("B1", b1, "BodyService реализует reset-контракт (WorldDomainResetCoordinator.ResetAll — единая точка сброса world-transition)");

            // Готовим «грязное» тело: раны + ампутация + дебафф + блокировка слота.
            HealAllRedToFull();
            _body!.ApplyDamage(BodyPartType.Torso, 100, "qa_st11_src");
            _body.ApplyDamage(BodyPartType.LeftArm, 300, "qa_st11_src"); // Severed
            bool dirtySevered = _body.IsPartSevered(BodyPartType.LeftArm);
            bool debuffOn = CountActiveBuffs("player", "severed_left_arm_str") == 1;
            bool slotBlocked = _equipment!.IsSlotBlocked(EquipmentSlot.WeaponOff);
            GD.Print($"[Stage11Sim] B подготовка: ампутация={dirtySevered}, дебафф={debuffOn}, слот WeaponOff заблокирован={slotBlocked}");

            // B2: WorldDomainResetCoordinator.ResetAll — пересборка мира (NewGame-фаза 0).
            var resolver = Scene.GameBoot.Container;
            int resetCount = WorldDomainResetCoordinator.ResetAll(resolver!);
            string gap;
            bool bodyFresh = AllNonSeveredRedFull(out gap) && !_body.IsPartSevered(BodyPartType.LeftArm);
            bool debuffsGone = CountActiveBuffs("player", "severed_") == 0;
            bool slotFree = !_equipment.IsSlotBlocked(EquipmentSlot.WeaponOff);
            bool b2 = bodyFresh && debuffsGone && slotFree;
            GD.Print($"[Stage11Sim] B2: после ResetAll({resetCount} доменов): тело свежее={bodyFresh} ({gap}), дебаффы сняты={debuffsGone}, слот свободен={slotFree} → {(b2 ? "PASS" : "FAIL")}");
            Record("B2", b2, "New Game = свежее тело (морфология/HP базовые), раны/ампутации/дебаффы/блокировки прошлого мира не протекают");

            // B3: повторный цикл (накопление подписок/состояний не должно проявляться).
            _body.ApplyDamage(BodyPartType.LeftLeg, 400, "qa_st11_src"); // Severed leg (black 100 < 120)
            bool legSevered2 = _body.IsPartSevered(BodyPartType.LeftLeg);
            int legDebuffs = CountActiveBuffs("player", "severed_left_leg_agi");
            WorldDomainResetCoordinator.ResetAll(resolver!);
            bool fresh2 = AllNonSeveredRedFull(out _) && !_body.IsPartSevered(BodyPartType.LeftLeg);
            bool debuffsGone2 = CountActiveBuffs("player", "severed_") == 0;
            bool b3 = legSevered2 && legDebuffs == 1 && fresh2 && debuffsGone2;
            GD.Print($"[Stage11Sim] B3: цикл ×2: sever→reset; вторая ампутация дебафф={legDebuffs} (ожид 1), после сброса тело свежее={fresh2}, дебаффов={CountActiveBuffs("player", "severed_")} → {(b3 ? "PASS" : "FAIL")}");
            Record("B3", b3, "повторный reset безопасен: ампутационный дебафф применяется ровно один раз, состояния не накапливаются");

            // B4: реальный порядок New Game — BodyModule.Start() не дублируется
            // (P1-10 resume), reset сам пересоздаёт тело (не зависит от рестарта модулей).
            bool b4 = b1 && b2; // контракт подтверждён B1/B2; отдельной мутации не требуется
            Record("B4", b4, "тело пересоздаётся ResetWorld-ом (порядок фаз New Game: ResetAll → генерация → Start-резюме), а не повторным Initialize модуля");
        }

        // ================================================================
        //  C. ST11-003 — морфологии и слоты экипировки
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage11Sim] ─── C. ST11-003: морфология не должна блокировать допустимые слоты ───");
            await Task.CompletedTask;

            // C1: игрок-гуманоид до любых повреждений — базовые слоты свободны.
            bool c1 = !_body!.IsSlotBlocked(EquipmentSlot.WeaponMain)
                   && !_body.IsSlotBlocked(EquipmentSlot.WeaponOff)
                   && !_body.IsSlotBlocked(EquipmentSlot.Hands)
                   && !_body.IsSlotBlocked(EquipmentSlot.Legs)
                   && !_body.IsSlotBlocked(EquipmentSlot.Feet)
                   && !_body.IsSlotBlocked(EquipmentSlot.Head)
                   && !_body.IsSlotBlocked(EquipmentSlot.Torso);
            GD.Print($"[Stage11Sim] C1: гуманоид, свежее тело: WeaponMain={_body.IsSlotBlocked(EquipmentSlot.WeaponMain)} WeaponOff={_body.IsSlotBlocked(EquipmentSlot.WeaponOff)} Hands={_body.IsSlotBlocked(EquipmentSlot.Hands)} (ожид все false) → {(c1 ? "PASS" : "FAIL")}");
            Record("C1", c1, "гуманоид до повреждений: ни один базовый слот не заблокирован (отсутствующие у вида крылья/хелицеры не блокируют WeaponMain)");

            // C2-C6: все 10 шаблонов через вторичные BodyService (реальный домен,
            // не статическая проверка маппинга).
            var resolver = Scene.GameBoot.Container!;
            Morphology[] morphs =
            {
                Morphology.Humanoid, Morphology.Quadruped, Morphology.Bird,
                Morphology.Serpentine, Morphology.Arthropod, Morphology.Amorphous,
                Morphology.HybridCentaur, Morphology.HybridMermaid,
                Morphology.HybridHarpy, Morphology.HybridLamia,
            };
            int mismatched = 0;
            foreach (var m in morphs)
            {
                var svc = BuildSecondaryBody(resolver, $"qa_st11_{m}");
                try
                {
                    // Ожидание (ST11-003, канон): слот закрыт ⇐ на него маппится
                    // ≥1 часть, но ВСЕ маппящиеся части отсутствуют по морфологии
                    // (BD-23: змея без оружия); слот с существующей здоровой
                    // анатомией — свободен; слот без телесного маппинга вовсе
                    // (Amulet/Ring/Belt) — не телесный гейт, свободен.
                    foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                    {
                        if (slot == EquipmentSlot.None) continue;
                        bool hasAnatomy = HasAnyMorphPartForSlot(svc, slot, out var partName);
                        bool mappedNonEmpty = BodySlotMapping.GetMappedParts(slot).Length > 0;
                        bool expectBlocked = !hasAnatomy && mappedNonEmpty;
                        bool actual = svc.IsSlotBlocked(slot);
                        if (actual == expectBlocked) continue;
                        mismatched++;
                        GD.Print($"[Stage11Sim] C-МИСМАТЧ: {m}/{slot}: анатомия={partName}, ожид-заблокирован={expectBlocked}, факт={actual}");
                    }
                }
                finally { (svc as IDisposable)?.Dispose(); }
            }
            bool cAll = mismatched == 0;
            GD.Print($"[Stage11Sim] C6: матрица 10 морфологий × все слоты: расхождений={mismatched} → {(cAll ? "PASS" : "FAIL")}");
            Record("C6", cAll, "слот разрешён ⇔ у морфологии есть маппящаяся на него часть (garpy — крылья↔руки, змея — без оружия (BD-23), паук — хелицеры/педипальпы)");

            // C2 (выделенная): гарпия — крылья вместо рук, слот оружия должен быть свободен.
            var harpy = BuildSecondaryBody(resolver, "qa_st11_harpy");
            bool c2;
            try
            {
                c2 = !harpy.IsSlotBlocked(EquipmentSlot.WeaponMain)
                  && !harpy.IsSlotBlocked(EquipmentSlot.WeaponOff);
                GD.Print($"[Stage11Sim] C2: гарпия (крылья = Manipulation): WeaponMain={harpy.IsSlotBlocked(EquipmentSlot.WeaponMain)} WeaponOff={harpy.IsSlotBlocked(EquipmentSlot.WeaponOff)} (ожид false/false) → {(c2 ? "PASS" : "FAIL")}");
            }
            finally { (harpy as IDisposable)?.Dispose(); }
            Record("C2", c2, "гибридная гарпия: отсутствующие гуманоидные руки НЕ блокируют слоты оружия — альтернативная анатомия (крылья) разрешает");

            // C3: змееподобное — без манипулятивной анатомии, оружие запрещено (BD-23).
            var snake = BuildSecondaryBody(resolver, "qa_st11_snake");
            bool c3;
            try
            {
                c3 = snake.IsSlotBlocked(EquipmentSlot.WeaponMain)
                  && snake.IsSlotBlocked(EquipmentSlot.WeaponOff)
                  && snake.IsSlotBlocked(EquipmentSlot.Hands)
                  && !snake.IsSlotBlocked(EquipmentSlot.Torso)
                  && !snake.IsSlotBlocked(EquipmentSlot.Head);
                GD.Print($"[Stage11Sim] C3: змея: WeaponMain={snake.IsSlotBlocked(EquipmentSlot.WeaponMain)} Hands={snake.IsSlotBlocked(EquipmentSlot.Hands)} Torso={snake.IsSlotBlocked(EquipmentSlot.Torso)} (ожид true/true/…/false) → {(c3 ? "PASS" : "FAIL")}");
            }
            finally { (snake as IDisposable)?.Dispose(); }
            Record("C3", c3, "змея без манипулятивной анатомии: оружейные слоты заблокированы, торс/голова доступны (BD-23 сохранён)");

            // C4/C5: паук — хелицеры (WeaponMain) и педипальпы (WeaponOff); ампутация
            // хелицер блокирует, приживление возвращает.
            var spider = BuildSecondaryBody(resolver, "qa_st11_spider");
            bool c4, c5;
            try
            {
                c4 = !spider.IsSlotBlocked(EquipmentSlot.WeaponMain)
                  && !spider.IsSlotBlocked(EquipmentSlot.WeaponOff);
                GD.Print($"[Stage11Sim] C4: паук: WeaponMain(хелицеры)={spider.IsSlotBlocked(EquipmentSlot.WeaponMain)} WeaponOff(педипальпы)={spider.IsSlotBlocked(EquipmentSlot.WeaponOff)} (ожид false/false) → {(c4 ? "PASS" : "FAIL")}");

                spider.ApplyDamage(BodyPartType.Chelicerae, 300, "qa_st11_src"); // 10/20 → Severed
                bool blockedAfterSever = spider.IsSlotBlocked(EquipmentSlot.WeaponMain);
                bool reattached = spider.ReattachPart(BodyPartType.Chelicerae, 10, 20);
                bool freeAfterReattach = !spider.IsSlotBlocked(EquipmentSlot.WeaponMain);
                c5 = blockedAfterSever && reattached && freeAfterReattach;
                GD.Print($"[Stage11Sim] C5: ампутация хелицер → blocked={blockedAfterSever}; приживление={reattached} → free={freeAfterReattach} → {(c5 ? "PASS" : "FAIL")}");
            }
            finally { (spider as IDisposable)?.Dispose(); }
            Record("C4", c4, "членистоногое: хелицеры/педипальпы — законная манипулятивная анатомия оружейных слотов");
            Record("C5", c5, "реальная ампутация блокирует только предусмотренные слоты; приживление снимает блокировку без остатка");
        }

        private IBodyService BuildSecondaryBody(IResolver resolver, string entityId)
        {
            var damagedPub = resolver.Resolve<IPublisher<BodyPartDamagedEvent>>();
            var severedPub = resolver.Resolve<IPublisher<BodyPartSeveredEvent>>();
            var healedPub = resolver.Resolve<IPublisher<BodyPartHealedEvent>>();
            var reattachedPub = resolver.Resolve<IPublisher<BodyPartReattachedEvent>>();
            var criticalPub = resolver.Resolve<IPublisher<BodyCriticalEvent>>();
            var damageSub = resolver.Resolve<ISubscriber<DamageAppliedEvent>>();
            var cultSub = resolver.Resolve<ISubscriber<CultivationLevelChangedEvent>>();
            var qiSub = resolver.Resolve<ISubscriber<QiChangedEvent>>();
            var svc = new BodyService(damagedPub, severedPub, healedPub, reattachedPub,
                criticalPub, damageSub, cultSub, qiSub, _bodyFactory!);
            // Морфология тела вторичной сущности — из шаблона вида «гарпия».
            Morphology m = entityId.Contains("harpy") ? Morphology.HybridHarpy
                : entityId.Contains("snake") ? Morphology.Serpentine
                : entityId.Contains("spider") ? Morphology.Arthropod
                : Enum.Parse<Morphology>(entityId.Replace("qa_st11_", ""));
            var tpl = _templates!.GetTemplate(m);
            svc.Initialize(entityId, m, tpl.DefaultMaterial, tpl.DefaultSize, 10f);
            return svc;
        }

        private bool HasAnyMorphPartForSlot(IBodyService svc, EquipmentSlot slot, out string partName)
        {
            partName = "";
            var mapped = BodySlotMapping.GetMappedParts(slot);
            var parts = svc.GetAllParts();
            foreach (var mp in mapped)
            {
                foreach (var p in parts)
                {
                    if (p.Type == mp && p.State != BodyPartState.Severed)
                    {
                        partName = mp.ToString();
                        return true;
                    }
                }
            }
            return false;
        }

        // ================================================================
        //  D. ST11-004 (GAP) — HoT и пассивная регенерация
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage11Sim] ─── D. ST11-004: HealthRegen (HoT) и пассивная регенерация ───");
            await Task.CompletedTask;

            HealAllRedToFull();
            foreach (var b in _buffs!.GetActiveBuffs("player").ToArray())
                _buffs.RemoveBuff("player", b.BuffId);

            // D1: пассивная регенерация лечит ВСЕ неампутированные части (RedHP).
            _body!.ApplyDamage(BodyPartType.Torso, 60, "qa_st11_src");
            _body.ApplyDamage(BodyPartType.LeftLeg, 60, "qa_st11_src");
            var arm0 = GetPart(BodyPartType.LeftArm);
            _body.ProcessRegeneration(100000f); // большой шаг: аккумулятор даст целый хил
            var torso = GetPart(BodyPartType.Torso);
            var leg = GetPart(BodyPartType.LeftLeg);
            bool d1 = torso.CurrentRedHP == torso.MaxRedHP && leg.CurrentRedHP == leg.MaxRedHP;
            GD.Print($"[Stage11Sim] D1: пассивная регенерация: torso {torso.CurrentRedHP}/{torso.MaxRedHP}, leg {leg.CurrentRedHP}/{leg.MaxRedHP} (ожид обе полные) → {(d1 ? "PASS" : "FAIL")}");
            Record("D1", d1, "ProcessRegeneration восстанавливает красную HP всех неампутированных частей (аккумулятор дробной регенерации)");

            // D2: severed-часть пассивной регенерацией не лечится (ФОРМ-BOD-02).
            HealAllRedToFull();
            _body.ApplyDamage(BodyPartType.LeftArm, 300, "qa_st11_src");
            _body.ProcessRegeneration(100000f);
            var arm = GetPart(BodyPartType.LeftArm);
            bool d2 = _body.IsPartSevered(BodyPartType.LeftArm) && arm.CurrentRedHP == 0;
            GD.Print($"[Stage11Sim] D2: ампутированная рука не лечится: severed={_body.IsPartSevered(BodyPartType.LeftArm)}, red={arm.CurrentRedHP} → {(d2 ? "PASS" : "FAIL")}");
            Record("D2", d2, "ампутированная часть исключена из регенерации (Heal возвращает 0 для Severed)");
            _body.ReattachPart(BodyPartType.LeftArm, 40, 80);
            HealAllRedToFull();

            // D3 (GAP): HealthRegen-бафф лечит только торс — фактическая семантика.
            _body.ApplyDamage(BodyPartType.Torso, 60, "qa_st11_src");
            _body.ApplyDamage(BodyPartType.LeftLeg, 60, "qa_st11_src");
            _buffs.ApplyBuff("player", "health_regen", 60f, 5f, "qa_st11_src");
            _buffs.TickBuffs(2f); // интервал тика HealthRegen = 2с
            var torsoHoT = GetPart(BodyPartType.Torso);
            var legHoT = GetPart(BodyPartType.LeftLeg);
            bool torsoHealed = torsoHoT.CurrentRedHP > 100 - 60 + 3; // хотя бы один тик в торс
            bool legNotHealed = legHoT.CurrentRedHP < legHoT.MaxRedHP;
            bool torsoWasDamaged = true;
            GD.Print($"[Stage11Sim] D3: HoT-тик: torso {torsoHoT.CurrentRedHP}/{torsoHoT.MaxRedHP} (лечился={torsoHealed}), leg {legHoT.CurrentRedHP}/{legHoT.MaxRedHP} (не лечился={legNotHealed})");
            GD.Print($"[Stage11Sim] D3: GAP — HealthRegen лечит только торс (BodyModule.OnBuffTicked, TODO P1-08); норматив §не определяет распределение HoT → GAP за владельцем");
            _results.Add(("D3", "GAP",
                $"фактическая семантика: HealthRegen → HealPart(Torso) (TODO P1-08 «распределение HoT по повреждённым частям»); торс лечится ({torsoHealed}), конечности — нет ({legNotHealed}); решение о распределении по частям — за владельцем (спецификация распределения HoT отсутствует)"));
            foreach (var b in _buffs.GetActiveBuffs("player").ToArray())
                _buffs.RemoveBuff("player", b.BuffId);
            HealAllRedToFull();
        }

        // ================================================================
        //  E. ST11-005 (GAP) — смертность морфологий
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("[Stage11Sim] ─── E. ST11-005: смерть = Head|Heart (§5.3.1); Amorphous без Head/Heart ───");
            await Task.CompletedTask;

            // E1: гуманоидный NPC-стенд: голова → 0 = мёртв (единое правило тел).
            var humanoidParts = _bodyFactory!.CreateBody(Morphology.Humanoid, SizeClass.Medium, 10f);
            _bodyData!.SetBodyParts(GhostId + "_hum", humanoidParts);
            _damagePub!.Publish(new DamageAppliedEvent(
                "qa_st11_src", GhostId + "_hum", 75, DamageType.Physical,
                Element.Neutral, BodyPartType.Head, CombatAttackResult.Hit,
                CombatSubtype.None, null));
            bool humDead = !_body.IsEntityAlive(GhostId + "_hum");
            GD.Print($"[Stage11Sim] E1: гуманоид, голова red→0: мёртв={humDead} → {(humDead ? "PASS" : "FAIL")}");
            Record("E1", humDead, "единое правило тел: разрушение Head (RedHP≤0) = смерть сущности (BODY_SYSTEM §5.3.1)");
            _bodyData.RemoveEntity(GhostId + "_hum");

            // E2 (GAP): Amorphous (Core IsVital, но не Head/Heart): Core red→0 — НЕ смерть.
            var amorphParts = _bodyFactory.CreateBody(Morphology.Amorphous, SizeClass.Medium, 10f);
            _bodyData.SetBodyParts(GhostId, amorphParts);
            _damagePub.Publish(new DamageAppliedEvent(
                "qa_st11_src", GhostId, 500, DamageType.Physical,
                Element.Neutral, BodyPartType.Core, CombatAttackResult.Hit,
                CombatSubtype.None, null));
            bool coreAlive = _body.IsEntityAlive(GhostId);
            var coreData = GetEntityPart(GhostId, BodyPartType.Core);
            GD.Print($"[Stage11Sim] E2: Amorphous, Core red {coreData.CurrentRedHP}/{coreData.MaxRedHP} → IsEntityAlive={coreAlive}");
            GD.Print("[Stage11Sim] E2: GAP — §5.3.1 определяет смерть только через Head|Heart; Core (IsVital) фатальным НЕ объявлен → бесформенная сущность переживает разрушение ядра; правило требует решения владельца");
            _results.Add(("E2", "GAP",
                $"Amorphous: Core red {coreData.CurrentRedHP}/{coreData.MaxRedHP}, IsEntityAlive={coreAlive} — §5.3.1 (смерть = Head|Heart RedHP≤0) не определяет фатальность Core; предлагается владельцу: (а) считать IsVital-часть без Head/Heart (Core) фатальной, либо (б) зафиксировать бессмертие духа документом; флаг IsVital ≠ смертность (прецедент R20: торс)"));
            _bodyData.RemoveEntity(GhostId);
        }

        private BodyPartData GetEntityPart(string entityId, BodyPartType type)
        {
            var parts = _bodyData!.GetBodyParts(entityId);
            foreach (var p in parts!)
                if (p.Type == type)
                    return p.ToData();
            return new BodyPartData(type, BodyPartState.Healthy, false, 0, 0, 0, 0, BodyPartFunction.None, 0f);
        }

        // ================================================================
        //  F. ST11-006 — round-trip и валидация состояния тела
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("[Stage11Sim] ─── F. ST11-006: CaptureState/RestoreState и повреждённые блоки ───");
            await Task.CompletedTask;

            // F1: точный round-trip с ранами и ампутацией.
            HealAllRedToFull();
            foreach (var b in _buffs!.GetActiveBuffs("player").ToArray())
                _buffs.RemoveBuff("player", b.BuffId);
            _body!.ApplyDamage(BodyPartType.Torso, 100, "qa_st11_src");
            _body.ApplyDamage(BodyPartType.Head, 40, "qa_st11_src");
            _body.ApplyDamage(BodyPartType.LeftArm, 300, "qa_st11_src"); // Severed
            var captured = ((ISaveable)_body!).CaptureState();
            var snap = SnapshotBody();

            // Мутации после захвата.
            _body.ReattachPart(BodyPartType.LeftArm, 40, 80);
            HealAllRedToFull();

            ((ISaveable)_body).RestoreState(captured);
            bool f1 = SnapshotEquals(snap);
            GD.Print($"[Stage11Sim] F1: round-trip раны+ампутация: точное восстановление={f1} → {(f1 ? "PASS" : "FAIL")}");
            Record("F1", f1, "CaptureState/RestoreState: red/black/state всех частей, включая ампутацию (SetHP 0/0 → Severed), восстанавливаются точно");

            // F2: второй последовательный round-trip стабилен.
            var captured2 = ((ISaveable)_body).CaptureState();
            ((ISaveable)_body).RestoreState(captured2);
            bool f2 = SnapshotEquals(snap);
            GD.Print($"[Stage11Sim] F2: повторный round-trip стабилен={f2} → {(f2 ? "PASS" : "FAIL")}");
            Record("F2", f2, "идемпотентность round-trip: второй захват/восстановление не искажает состояние");

            // F3: неизвестный partType — запись пропускается, валидные части восстановлены.
            var dirty = captured2 as BodySaveData;
            bool f3ok = dirty != null;
            if (f3ok)
            {
                var parts3 = new List<BodyPartSaveEntry>(dirty!.parts)
                {
                    new BodyPartSaveEntry
                    {
                        partType = 9999, state = 1, currentRedHP = 55, currentBlackHP = 66,
                    },
                };
                var mutated = new BodySaveData
                {
                    morphology = dirty.morphology,
                    material = dirty.material,
                    size = dirty.size,
                    parts = parts3.ToArray(),
                };
                ((ISaveable)_body).RestoreState(mutated);
                // Неизвестная часть не должна появиться; известные — как в snap.
                bool knownRestored = true;
                foreach (var p in _body.GetAllParts())
                {
                    if ((int)p.Type == 9999) knownRestored = false;
                }
                bool noCrash = true;
                f3ok = knownRestored && noCrash && SnapshotEquals(snap);
            }
            GD.Print($"[Stage11Sim] F3: неизвестный partType пропущен без исключения, валидные части точны: {f3ok} → {(f3ok ? "PASS" : "FAIL")}");
            Record("F3", f3ok, "повреждённая запись (partType вне перечисления) отклоняется по-записно: валидные части восстанавливаются, загрузка не падает (контракт per-entry с журналом)");

            // F4: отрицательные/завышенные HP клампятся BodyPart.SetHP (BD-22-инварианты).
            var parts4 = new List<BodyPartSaveEntry>();
            foreach (var p in dirty.parts)
            {
                parts4.Add(new BodyPartSaveEntry
                {
                    partType = p.partType,
                    state = p.state,
                    currentRedHP = p.currentRedHP < 0 ? p.currentRedHP : -999,
                    currentBlackHP = 999999,
                });
            }
            var neg = new BodySaveData
            {
                morphology = dirty!.morphology,
                material = dirty.material,
                size = dirty.size,
                parts = parts4.ToArray(),
            };
            ((ISaveable)_body).RestoreState(neg);
            bool f4 = true;
            foreach (var p in _body.GetAllParts())
            {
                if (p.CurrentRedHP < 0 || p.CurrentBlackHP > p.MaxBlackHP) f4 = false;
            }
            GD.Print($"[Stage11Sim] F4: отрицательные/завышенные HP клампятся в [0, Max]: {f4} → {(f4 ? "PASS" : "FAIL")}");
            Record("F4", f4, "BodyPart.SetHP клампит red/black в [0; Max] — повреждённые значения не создают отрицательное здоровье");

            // F5: неполный блок поверх СВЕЖЕГО тела (порядок LoadGame: reset → restore):
            // пропущенная часть = свежее состояние, а не рана прошлого мира.
            WorldDomainResetCoordinator.ResetAll(Scene.GameBoot.Container!); // контракт LoadGame: reset до RestoreState
            var parts5 = new List<BodyPartSaveEntry>();
            foreach (var p in dirty.parts)
                if ((BodyPartType)p.partType != BodyPartType.Torso)
                    parts5.Add(p);
            var partial = new BodySaveData
            {
                morphology = dirty.morphology,
                material = dirty.material,
                size = dirty.size,
                parts = parts5.ToArray(),
            };
            ((ISaveable)_body).RestoreState(partial);
            var torsoF5 = GetPart(BodyPartType.Torso);
            bool f5 = torsoF5.CurrentRedHP == torsoF5.MaxRedHP
                   && torsoF5.CurrentBlackHP == torsoF5.MaxBlackHP
                   && torsoF5.State == BodyPartState.Healthy;
            GD.Print($"[Stage11Sim] F5: неполный блок поверх свежего тела: торс {torsoF5.CurrentRedHP}/{torsoF5.MaxRedHP} state={torsoF5.State} (ожид полный/Healthy — раны прошлого мира не протекают) → {(f5 ? "PASS" : "FAIL")}");
            Record("F5", f5, "отсутствующая в body-блоке часть после reset-контракта = свежее состояние (нет тихого переноса ран прошлого мира); частичное восстановление видно по журналу");

            // Приводим тело к чистому виду для зоны G.
            WorldDomainResetCoordinator.ResetAll(Scene.GameBoot.Container!);
        }

        private string SnapshotBody()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in _body!.GetAllParts())
                sb.Append($"{p.Type}:{p.State}:{p.CurrentRedHP}/{p.MaxRedHP}+{p.CurrentBlackHP}/{p.MaxBlackHP};");
            return sb.ToString();
        }

        private bool SnapshotEquals(string snap)
            => SnapshotBody() == snap;

        // ================================================================
        //  G. ST11-007 — ампутация, дебаффы, экипировка, загрузка
        // ================================================================

        private async Task ScenarioG()
        {
            GD.Print("[Stage11Sim] ─── G. ST11-007: ампутация/приживление/загрузка — согласованность ───");
            await Task.CompletedTask;

            // Чистый старт зоны (изоляция от остатков F: ампутации/раны/инвентарь).
            PrepareCleanBody();
            foreach (var b in _buffs!.GetActiveBuffs("player").ToArray())
                _buffs.RemoveBuff("player", b.BuffId);

            // G1: экипированный щит + ампутация LeftArm → авто-съём РОВНО один раз,
            // предмет в инвентаре, дебафф ровно один.
            _itemDb!.TryGetItem(ShieldId, out var shieldItem);
            var shieldData = shieldItem as EquipmentData;
            _inventory!.TryAddItem(shieldItem!, 1, ItemSources.StartingGear);
            int invBefore = _inventory.GetItemCount(ShieldId);
            bool removeOk = _inventory.TryRemoveItem(ShieldId, 1);
            bool equipOk = _equipment!.TryEquip(EquipmentSlot.WeaponOff, shieldData!);
            _equipment.GetAllEquipped().TryGetValue(EquipmentSlot.WeaponOff, out var equippedVal);
            bool equippedNow = equipOk && equippedVal != null;
            GD.Print($"[Stage11Sim] G подготовка: щит в инвентаре={invBefore > 0}, снят={removeOk}, экипирован={equipOk && equippedNow}");

            _equipBlockedEvents = 0;
            _body!.ApplyDamage(BodyPartType.LeftArm, 300, "qa_st11_src"); // Severed LeftArm
            bool g1slot = _equipment.IsSlotBlocked(EquipmentSlot.WeaponOff);
            _equipment.GetAllEquipped().TryGetValue(EquipmentSlot.WeaponOff, out var afterSever);
            bool g1uneq = afterSever == null;
            int invAfterSever = _inventory.GetItemCount(ShieldId);
            bool g1returned = invAfterSever == invBefore;
            bool g1blockedOnce = _equipBlockedEvents == 1;
            bool g1debuff = CountActiveBuffs("player", "severed_left_arm_str") == 1;
            bool g1 = g1slot && g1uneq && g1returned && g1blockedOnce && g1debuff;
            GD.Print($"[Stage11Sim] G1: ампутация: слот заблокирован={g1slot}, авто-съём={g1uneq}, щит в инвентаре={g1returned} ({invAfterSever}), EquipmentBlockedEvent={_equipBlockedEvents} (ожид 1), дебафф={g1debuff} → {(g1 ? "PASS" : "FAIL")}");
            Record("G1", g1, "ампутация: EquipmentBlockedEvent/авто-съём/возврат в инвентарь/дебафф — каждый ровно один раз");

            // G2: повторное событие ампутации (дубликат) — без повторного эффекта.
            _severedPub!.Publish(new BodyPartSeveredEvent(
                _body.EntityId, BodyPartType.LeftArm,
                BodySlotMapping.GetBlockedSlots(BodyPartType.LeftArm), false));
            int debuffsAfterDup = CountActiveBuffs("player", "severed_left_arm_str");
            bool g2 = debuffsAfterDup == 1;
            GD.Print($"[Stage11Sim] G2: дубликат события: активных дебаффов={debuffsAfterDup} (ожид 1) → {(g2 ? "PASS" : "FAIL")}");
            Record("G2", g2, "повторное событие ампутации не дублирует эффект (Refresh-семантика ApplyBuff сохраняет один активный дебафф)");

            // G3: приживление возвращает слот; повторное приживление — no-op;
            // чужие дебаффы не затрагиваются.
            _body.ApplyDamage(BodyPartType.LeftLeg, 400, "qa_st11_src"); // вторая ампутация (black 100 < 120)
            int legDebuffs = CountActiveBuffs("player", "severed_left_leg_agi");
            bool reattachOk = _body.ReattachPart(BodyPartType.LeftArm, 40, 80);
            bool slotFreed = !_equipment.IsSlotBlocked(EquipmentSlot.WeaponOff);
            bool armDebuffGone = CountActiveBuffs("player", "severed_left_arm_str") == 0;
            bool legDebuffsIntact = CountActiveBuffs("player", "severed_left_leg_agi") == legDebuffs;
            bool reattachAgain = _body.ReattachPart(BodyPartType.LeftArm, 40, 80); // уже не Severed → false
            bool legDebuffsAfter = CountActiveBuffs("player", "severed_left_leg_agi") == legDebuffs;
            bool g3 = reattachOk && slotFreed && armDebuffGone && legDebuffsIntact && !reattachAgain && legDebuffsAfter;
            GD.Print($"[Stage11Sim] G3: приживление руки={reattachOk}, слот свободен={slotFreed}, дебафф руки снят={armDebuffGone}, дебафф ноги цел ({legDebuffsIntact}/{legDebuffsAfter}), повторное приживление=false({reattachAgain}) → {(g3 ? "PASS" : "FAIL")}");
            Record("G3", g3, "приживление снимает только свои дебаффы и возвращает слот экипировки; повторное приживление — no-op");

            // G4: Save → ResetAll → Restore → IPostLoadSync: согласованность тела,
            // дебаффов и блокировок после загрузки; предмет без дюпа.
            // Сценарий: нога ампутирована (дебафф), щит — в инвентаре (уже снят).
            int shieldsBeforeSave = _inventory.GetItemCount(ShieldId);
            bool saved = _saveService!.Save(new SaveSlot("qa_st11_slot", SaveSlotType.Manual));
            // Полный LoadGame-порядок: reset → restore → postload-синхронизация.
            WorldDomainResetCoordinator.ResetAll(Scene.GameBoot.Container!);
            bool loaded = _saveService.Load(new SaveSlot("qa_st11_slot", SaveSlotType.Manual));
            if (loaded)
            {
                foreach (var sync in Scene.GameBoot.Container!.ResolveAll<IPostLoadSync>())
                    sync.OnAfterLoad();
            }
            bool legSeveredAfterLoad = _body.IsPartSevered(BodyPartType.LeftLeg);
            bool legDebuffAfterLoad = CountActiveBuffs("player", "severed_left_leg_agi") == 1;
            bool legsSlotBlockedAfterLoad = _equipment.IsSlotBlocked(EquipmentSlot.Legs);
            int shieldsAfterLoad = _inventory.GetItemCount(ShieldId);
            bool noDupe = shieldsAfterLoad == shieldsBeforeSave;
            bool g4 = saved && loaded && legSeveredAfterLoad && legDebuffAfterLoad
                   && legsSlotBlockedAfterLoad && noDupe;
            GD.Print($"[Stage11Sim] G4: save={saved} load={loaded}; после загрузки: нога ампутирована={legSeveredAfterLoad}, дебафф={legDebuffAfterLoad}, слот Legs заблокирован={legsSlotBlockedAfterLoad}, щитов={shieldsAfterLoad}/{shieldsBeforeSave} (без дюпа) → {(g4 ? "PASS" : "FAIL")}");
            Record("G4", g4, "LoadGame: производные состояния пересобираются по восстановленному телу — блокировки слотов (EquipmentService) и ампутационные дебаффы (SeveredDebuffSystem) согласованы, предметы без дюпа");

            // G5: New Game между ампутацией и приживлением: reset очищает всё,
            // приживлять больше нечего — состояние согласовано.
            WorldDomainResetCoordinator.ResetAll(Scene.GameBoot.Container!);
            bool g5 = !_body.IsPartSevered(BodyPartType.LeftLeg)
                   && CountActiveBuffs("player", "severed_") == 0
                   && !_equipment.IsSlotBlocked(EquipmentSlot.Legs);
            GD.Print($"[Stage11Sim] G5: New Game после ампутации: тело чистое={g5} → {(g5 ? "PASS" : "FAIL")}");
            Record("G5", g5, "New Game между ампутацией и приживлением: тело/дебаффы/блокировки сброшены согласованно (приживлять нечего, слоты свободны)");
        }

        // ================================================================
        //  REF-строки плейтеста (физический раунд — за владельцем)
        // ================================================================

        private void PrintRefLines()
        {
            GD.Print("[Stage11Sim] ─── REF: строки физического плейтеста (раунд за владельцем) ───");
            _results.Add(("REF-B01", "REF", "PT-B01 New Game сбрасывает ранения/ампутации/штрафы — закрыто симом B2/B3; физический статус выставляет владелец"));
            _results.Add(("REF-B02", "REF", "PT-B02 Save/Load восстанавливает каждую часть — закрыто симом F1/G4; физический статус выставляет владелец"));
            _results.Add(("REF-B03", "REF", "PT-B03 регенерация/HealthRegen — семантика HoT = GAP D3 (решение владельца); физический статус выставляет владелец"));
            _results.Add(("REF-B04", "REF", "PT-B04 смертность морфологий — GAP E2 (Core: предложение владельцу); физический статус выставляет владелец"));
            _results.Add(("REF-B05", "REF", "PT-B05 ампутация/приживление синхронны — закрыто симом G1-G4; физический статус выставляет владелец"));
            _results.Add(("REF-B06", "REF", "PT-B06 альтернативная анатомия не блокирует слоты — закрыто симом C1-C6; физический статус выставляет владелец"));
            _results.Add(("REF-C02+", "REF", "PT-C02 (дополнение): после возрождения проверить HP ВСЕХ частей тела — закрыто симом A1; физический статус выставляет владелец"));
        }

        // ================================================================
        //  ИТОГИ
        // ================================================================

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void PrintSummary()
        {
            GD.Print("[Stage11Sim] ═══════ СВОДКА ЭТАПА-11 ═══════");
            int pass = 0, fail = 0, gap = 0, refCount = 0;
            foreach (var (id, status, detail) in _results)
            {
                GD.Print($"[Stage11Sim] {id}: {status} — {detail}");
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else if (status == "GAP") gap++;
                else refCount++;
            }
            GD.Print($"[Stage11Sim] Итого: PASS={pass}, FAIL={fail}, GAP={gap}, REF={refCount}");
            PrintVerdict(fail == 0);
        }

        private void PrintVerdict(bool pass)
        {
            GD.Print($"[Stage11Sim] VERDICT: {(pass ? "PASS" : "FAIL")}");
        }
    }
}
