#nullable enable
// Создано: 2026-10-23 — Этап-16 аудита (рендеринг, анимация, синхронизация
// визуального состояния). Канон: upload/audit_10_20_e16.txt ([STAGE 16 AUDIT]).
// Запуск:
//   GODOT_NEWGAME=1 GODOT_STAGE16_DEBUG=1 \
//     godot --headless --path . scenes/MainMenu.tscn
// Живой рендер (замеры реального _Draw — walk-рейшо зверей):
//   GODOT_STAGE16_DEBUG=1 + Xvfb + --rendering-driver opengl3
//
// ДОПОЛНЯЕТ (не дублирует) ANIMQA (загрузчик/fallback/базовые состояния),
// WEAPONVIS (экипировка/facing-флаг), COMBAT_SIM (боевые VFX-счётчики),
// FORM/FormationShot (формации) — новые инварианты этапа-16:
//   A. ST16-001 — one-shot-таймеры игрока НЕ переживают death/respawn
//      (melee/ranged/hit; пауза-вариант; death-hold sanity).
//   B. ST16-002 — живые пути подключённых состояний + D2-ряд +
//      GAP-регистрация будущих фаз (cast G3/guard G4/dash-sleep-breakthrough G5).
//   C. ST16-003 — канонический ключ звериного melee-листа
//      (animal_{species}_attack, SPRITE_DELIVERY §5) + цепочка npc-ключей.
//   D. ST16-004 — гарантированно живой зверь + доменный сигнал движения
//      (AnimalEntity.Target != null) vs кадровая дельта (мерцание walk);
//      при живом рендере — фактический walk-рейшо по GetAnimalAnimInfo.
//   E. ST16-005 — ZIndex/порядок формации: контур НАД объектами, ПОД
//      телами/игроком (RENDER_LAYERS §3.4/§3.5 + класс-комментарий рендерера).
//   F. ST16-006 — жизненный цикл Shield: one-shot каста ≠ persistent
//      Qi-буфер; стаки на AbsorbDamage-репаблише запрещены; деактивация
//      буфера НЕ убивает чужой one-shot.
//   G. ST16-007 — per-entity кэши рендереров чистятся после смерти/деспавна
//      (длительная сессия spawn/despawn — рост запрещён).
//   H. ST16-008 — невалидная геометрия → диагностика+fallback; негативный
//      кэш/ResetCache; реестр 13/6/5; честность ANIMQA-зверей (code-ref).
//   I. ST16-009 — синхрон аниматора тела и MainHand (старт/длительность)
//      + fallback неизвестного класса.
//
// Итог: [Stage16Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/NOT TESTED).
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene
{
    public partial class Stage16RenderAnimationSyncTruthSimDebug : Node
    {
        [Inject] private ITimeService? _time;
        [Inject] private INPCSpawnerService? _spawner;
        [Inject] private INPCService? _npcService;
        [Inject] private AnimalService? _animalService;
        [Inject] private IQiBufferService? _qiBuffer;
        [Inject] private IQiService? _qiService;
        [Inject] private IPublisher<AttackIntentEvent>? _attackIntentPub;
        [Inject] private IPublisher<DamageAppliedEvent>? _damagePub;
        [Inject] private IPublisher<PlayerDeathEvent>? _deathPub;
        [Inject] private IPublisher<MeditationStateChangedEvent>? _medPub;
        [Inject] private IPublisher<TechniqueCastResultEvent>? _castResultPub;
        [Inject] private ISubscriber<QiBufferStateChangedEvent>? _qiBufferStateSub;
        private IDisposable? _qiBufferStateToken;
        private readonly List<string> _qiBufferEntityIds = new();

        private readonly List<(string Id, string Status, string Detail)> _results = new();
        private GameWorldController? _world;
        private NPCSpriteRenderer? _npcRenderer;
        private AnimalSpriteRenderer? _animalRenderer;
        private TechniqueEffectRenderer? _fxRenderer;
        private FormationVisualRenderer? _formationRenderer;

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
            {
                ContainerAdapter.InjectProperties(this, container);
                _qiBufferStateToken = _qiBufferStateSub?.Subscribe((in QiBufferStateChangedEvent e) =>
                    _qiBufferEntityIds.Add($"{(e.IsActive ? "on" : "off")}:{e.EntityId}"));
            }

            GD.Print("[Stage16Sim] Ready — Этап-16 (рендер/анимация/синхронизация) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
            _npcRenderer = FindNode<NPCSpriteRenderer>(GetTree().Root);
            _animalRenderer = FindNode<AnimalSpriteRenderer>(GetTree().Root);
            _fxRenderer = FindNode<TechniqueEffectRenderer>(GetTree().Root);
            _formationRenderer = FindNode<FormationVisualRenderer>(GetTree().Root);

            if (_world == null || _world.PlayerAnimatorForQA == null)
            {
                GD.Print("[Stage16Sim] FAIL — GameWorldController/PlayerAnimator не найдены");
                PrintSummary();
                return;
            }
            GD.Print($"[Stage16Sim] GWC OK; npcR={_npcRenderer != null}, animalR={_animalRenderer != null}, " +
                     $"fxR={_fxRenderer != null}, formR={_formationRenderer != null}");

            await ScenarioB();   // ST16-002 живые состояния + D2-ряд + GAP
            await ScenarioA();   // ST16-001 one-shot × death/respawn
            await ScenarioI();   // ST16-009 синхрон тела/оружия
            await ScenarioC();   // ST16-003 канон звериного melee-ключа
            await ScenarioD();   // ST16-004 доменный сигнал движения зверя
            await ScenarioE();   // ST16-005 порядок формации
            await ScenarioG();   // ST16-007 кэши рендереров
            await ScenarioH();   // ST16-008 геометрия/кэш/реестр
            await ScenarioF();   // ST16-006 Shield-жизненный цикл (последний: Ци)

            PrintSummary();
        }

        // === HARNESS ========================================================

        private static GameWorldController? FindWorld(Node node)
        {
            if (node is GameWorldController w) return w;
            foreach (var child in node.GetChildren())
            {
                var found = FindWorld(child);
                if (found != null) return found;
            }
            return null;
        }

        private static T? FindNode<T>(Node node) where T : Node
        {
            if (node is T t) return t;
            foreach (var child in node.GetChildren())
            {
                var found = FindNode<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void RecordGap(string id, string detail)
            => _results.Add((id, "GAP", detail));

        private void RecordNotTested(string id, string detail)
            => _results.Add((id, "NOT TESTED", detail));

        private void RecordDisproved(string id, string detail)
            => _results.Add((id, "DISPROVED", detail));

        /// <summary>Физические кадры (GWC._PhysicsProcess тикает аниматор). </summary>
        private async Task PhysFrames(int n)
        {
            for (int i = 0; i < n; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        private void PrintSummary()
        {
            int pass = 0, fail = 0, gap = 0, nt = 0, dis = 0;
            GD.Print("──────────────────────────────────────────────────────");
            foreach (var r in _results)
            {
                GD.Print($"[Stage16Sim] {r.Id}: {r.Status} — {r.Detail}");
                switch (r.Status)
                {
                    case "PASS": pass++; break;
                    case "FAIL": fail++; break;
                    case "GAP": gap++; break;
                    case "NOT TESTED": nt++; break;
                    case "DISPROVED": dis++; break;
                }
            }
            GD.Print("──────────────────────────────────────────────────────");
            GD.Print($"[Stage16Sim] Итог: {pass} PASS / {fail} FAIL / {gap} GAP / " +
                     $"{nt} NOT TESTED / {dis} DISPROVED");
            GD.Print($"[Stage16Sim] VERDICT: {(fail > 0 ? "FAIL" : "PASS")}");
        }

        // ================================================================
        //  A. ST16-001 — one-shot-таймеры не переживают death/respawn
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage16Sim] ─── A. ST16-001: one-shot × death/respawn ───");
            var anim = _world!.PlayerAnimatorForQA!;

            // A1: melee → death → respawn → idle (не продолжать старую атаку).
            anim.NotifyMeleeAttack();
            await PhysFrames(5);                       // замах разогнался (~0.08с)
            anim.NotifyDeath();
            await PhysFrames(2);
            anim.NotifyRespawn();
            await PhysFrames(3);                       // 3 кадра на ре-резолв
            bool a1 = anim.AnimId == "player_idle";
            GD.Print($"[Stage16Sim] A1 melee→death→respawn: AnimId='{anim.AnimId}' (ожид player_idle) → " +
                     $"{(a1 ? "PASS" : "FAIL — СТАРАЯ АТАКА ДОИГРЫВАЕТСЯ ПОСЛЕ РЕСПАВНА")}");
            Record("ST16-001/A1 melee", a1, $"AnimId='{anim.AnimId}', ожид 'player_idle'");

            // A2: ranged → death → respawn.
            anim.NotifyRangedAttack();
            await PhysFrames(3);
            anim.NotifyDeath();
            await PhysFrames(2);
            anim.NotifyRespawn();
            await PhysFrames(3);
            bool a2 = anim.AnimId == "player_idle";
            GD.Print($"[Stage16Sim] A2 ranged→death→respawn: AnimId='{anim.AnimId}' → {(a2 ? "PASS" : "FAIL")}");
            Record("ST16-001/A2 ranged", a2, $"AnimId='{anim.AnimId}'");

            // A3: hit → death → respawn.
            anim.NotifyHit();
            await PhysFrames(3);
            anim.NotifyDeath();
            await PhysFrames(2);
            anim.NotifyRespawn();
            await PhysFrames(3);
            bool a3 = anim.AnimId == "player_idle";
            GD.Print($"[Stage16Sim] A3 hit→death→respawn: AnimId='{anim.AnimId}' → {(a3 ? "PASS" : "FAIL")}");
            Record("ST16-001/A3 hit", a3, $"AnimId='{anim.AnimId}'");

            // A4: пауза в момент one-shot → death/respawn → снятие паузы.
            anim.NotifyMeleeAttack();
            await PhysFrames(3);
            _time?.Pause();
            anim.NotifyDeath();
            await PhysFrames(5);                       // заморожено: таймеры стоят
            anim.NotifyRespawn();
            await PhysFrames(2);
            _time?.Resume();
            await PhysFrames(3);
            bool a4 = anim.AnimId == "player_idle";
            GD.Print($"[Stage16Sim] A4 pause→death→respawn→unpause: AnimId='{anim.AnimId}' → {(a4 ? "PASS" : "FAIL")}");
            Record("ST16-001/A4 pause", a4, $"после снятия паузы AnimId='{anim.AnimId}'");

            // A5 (sanity): death-hold + реальный PlayerDeathEvent-путь.
            bool aliveWired = true;
            if (_deathPub != null)
            {
                _deathPub.Publish(new PlayerDeathEvent("qa_stage16"));
                await PhysFrames(3);
                aliveWired = anim.AnimId == "player_death";
                GD.Print($"[Stage16Sim] A5 PlayerDeathEvent→GWC→NotifyDeath: AnimId='{anim.AnimId}' → " +
                         $"{(aliveWired ? "PASS" : "FAIL")}");
            }
            anim.NotifyRespawn();
            await PhysFrames(2);
            Record("ST16-001/A5 death-hold", aliveWired && anim.AnimId == "player_idle",
                $"PlayerDeathEvent→'{anim.AnimId}'→respawn→idle OK");
        }

        // ================================================================
        //  B. ST16-002 — живые состояния + D2-ряд + GAP будущих фаз
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage16Sim] ─── B. ST16-002: подключённые состояния игрока ───");
            var anim = _world!.PlayerAnimatorForQA!;

            // B1: медитация (реальное событие → GWC → FrameState.Meditation).
            if (_medPub != null)
            {
                _medPub.Publish(new MeditationStateChangedEvent(true, 1f));
                await PhysFrames(3);
                bool medOn = anim.AnimId == "player_meditate";
                _medPub.Publish(new MeditationStateChangedEvent(false, 0f));
                await PhysFrames(3);
                bool medOff = anim.AnimId == "player_idle";
                GD.Print($"[Stage16Sim] B1 медитация: on='{anim.AnimId}' … " +
                         $"medOn={medOn}, после off='{anim.AnimId}' → {(medOn && medOff ? "PASS" : "FAIL")}");
                Record("ST16-002/B1 meditate", medOn && medOff, "MeditationStateChangedEvent→player_meditate→idle");
            }
            else RecordNotTested("ST16-002/B1 meditate", "publisher недоступен");

            // B2: полный FrameState-свип — walk/run/idle и НЕ-достижимость
            // будущих фаз (cast/guard/dash/sleep/breakthrough не резолвятся).
            var future = new HashSet<string> { "player_cast", "player_guard", "player_dash",
                                                "player_sleep", "player_breakthrough" };
            var seen = new HashSet<string>();
            var states = new[]
            {
                PlayerAnimator.FrameState.Default,
                new(true, false, 1f, false),
                new(true, true, 1f, false),
                new(false, false, 1f, true),
                new(true, true, 2f, true),
            };
            foreach (var st in states)
            {
                anim.Update(1f / 60f, st);
                seen.Add(anim.AnimId);
            }
            bool sweepOk = seen.Contains("player_walk") && seen.Contains("player_run")
                        && seen.Contains("player_idle") && seen.Contains("player_meditate");
            bool noFuture = true;
            foreach (var f in future) if (seen.Contains(f)) noFuture = false;
            GD.Print($"[Stage16Sim] B2 свип: {string.Join(",", seen)}; будущие-фазы не резолвятся={noFuture} → " +
                     $"{(sweepOk && noFuture ? "PASS" : "FAIL")}");
            Record("ST16-002/B2 sweep", sweepOk && noFuture,
                "walk/run/idle/meditate достижимы; cast/guard/dash/sleep/breakthrough — нет");

            // B3: PNG-семантика по диску (G1/G2 доставлены: idle+walk — PNG;
            // остальные — процедурный fallback, состояние не ломается).
            anim.Update(1f / 60f, PlayerAnimator.FrameState.Default);
            bool idlePng = anim.AnimId == "player_idle" && anim.IsPng && anim.FrameCount == 4;
            anim.Update(1f / 60f, new PlayerAnimator.FrameState(true, false, 1f, false));
            bool walkPng = anim.AnimId == "player_walk" && anim.IsPng && anim.FrameCount == 6;
            anim.NotifyMeleeAttack();
            anim.Update(1f / 60f, PlayerAnimator.FrameState.Default);
            bool meleeFallback = anim.AnimId == "player_melee" && !anim.IsPng && anim.FrameCount == 0;
            GD.Print($"[Stage16Sim] B3 диск: idle png={idlePng}, walk png={walkPng}, melee fallback={meleeFallback} → " +
                     $"{(idlePng && walkPng && meleeFallback ? "PASS" : "FAIL")}");
            Record("ST16-002/B3 png-vs-fallback", idlePng && walkPng && meleeFallback,
                "доставленные idle/walk=PNG(4/6 кадров), недоставленный melee=procedural-состояние живо");

            // B4: D2-ряд — FrameAt(row) индексация и клэмп.
            var grid = SpriteSheetCache.LoadSheetDirect("res://resources/sprites/_qa/qa_grid_5x6.png", 10, false);
            bool b4 = grid != null && grid.Rows == 5 && grid.FrameCount == 6
                   && grid.FrameAt(0.5f, 2) == 2 * 6 + 3
                   && grid.FrameAt(0f, 99) / grid.FrameCount == 4
                   && anim.Row == 2;
            GD.Print($"[Stage16Sim] B4 D2-ряд: grid={(grid != null ? $"{grid.Rows}x{grid.FrameCount}" : "null")}, " +
                     $"frame(0.5,row2)={grid?.FrameAt(0.5f, 2)}, clampRow99→row {grid?.FrameAt(0f, 99) / (grid?.FrameCount ?? 1)}, " +
                     $"PlayerRow={anim.Row} → {(b4 ? "PASS" : "FAIL")}");
            Record("ST16-002/B4 D2-row", b4, "qa_grid_5x6: Rows=5, row=2 side, клэмп ряда работает");

            // B5: GAP-регистрация будущих фаз (план §5.1/манифест):
            RecordGap("ST16-002/B5 cast(G3)", "триггер зарядки — фаза G3 (манифест player_cast); реестр готов");
            RecordGap("ST16-002/B6 guard(G4)", "G-стойка — фаза G4 (манифест player_guard); реестр готов");
            RecordGap("ST16-002/B7 dash(G5)", "код-задача I-4 плана; фаза G5 (манифест player_dash)");
            RecordGap("ST16-002/B8 sleep(G5)", "код-задача I-4 плана; фаза G5 (манифест player_sleep)");
            RecordGap("ST16-002/B9 breakthrough(G5)", "прорыв — только UI; фаза G5 (манифест player_breakthrough)");
        }

        // ================================================================
        //  C. ST16-003 — канон звериного melee-ключа + цепочка NPC
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage16Sim] ─── C. ST16-003: звериный melee-ключ (канон attack) ───");
            if (_npcRenderer == null || _spawner == null || _attackIntentPub == null || _npcService == null)
            {
                RecordNotTested("ST16-003/C1", "рендерер/спавнер/паблишер недоступны");
                return;
            }

            // C1: квадрупед-NPC (wolf-Monster) замахивается → ключ листа
            // должен быть канонический animal_wolf_attack (манифест/§5 доставки).
            // Спавн ВДАЛИ от игрока: доменный удар отклонит range-гейт,
            // а замах-визуал рендерер записывает без проверки дистанции.
            Position2D pos = new(20, 20);
            string wolfId;
            try
            {
                wolfId = _spawner.SpawnNPC("wolf", NPCRole.Monster, 1, pos, 1601);
            }
            catch (Exception ex)
            {
                RecordNotTested("ST16-003/C1", $"SpawnNPC('wolf') упал: {ex.Message}");
                return;
            }
            var wolf = _npcService.GetNPC(wolfId);
            if (wolf == null) { RecordNotTested("ST16-003/C1", "wolf-NPC не найден после спавна"); return; }

            _attackIntentPub.Publish(new AttackIntentEvent(
                wolfId, "player", "basic_attack", false, 1000, false, 0, 0));
            await PhysFrames(2);   // _npcSwings[wolfId] установлен синхронно; кадры для стабильности

            var beastAnim = _npcRenderer.ResolveNpcAnim(wolfId, NPCRole.Monster, "wolf", moving: false);
            string triedKey = _npcRenderer.QaLastTriedSheetKey;
            bool c1 = triedKey == "animal_wolf_attack";
            GD.Print($"[Stage16Sim] C1 квадрупед-melee: пробуемый ключ листа='{triedKey}' " +
                     $"(канон='animal_wolf_attack'; AnimId='{beastAnim.AnimId}') → " +
                     $"{(c1 ? "PASS" : "FAIL — НЕКАНОНИЧЕСКИЙ КЛЮЧ: поставка G3 промахнётся")}");
            Record("ST16-003/C1 quad-melee-key", c1, $"резолв попытался '{triedKey}', канон 'animal_wolf_attack'");

            // C2: гуманоидный NPC — цепочка npc_{role}_melee → npc_base_melee.
            string guardId;
            try
            {
                guardId = _spawner.SpawnNPC("human", NPCRole.Guard, 1, new Position2D(26, 26), 1602);
            }
            catch (Exception ex)
            {
                RecordNotTested("ST16-003/C2", $"SpawnNPC('human') упал: {ex.Message}");
                return;
            }
            _attackIntentPub.Publish(new AttackIntentEvent(
                guardId, "player", "basic_attack", false, 1000, false, 0, 0));
            await PhysFrames(2);
            _ = _npcRenderer.ResolveNpcAnim(guardId, NPCRole.Guard, beastSpecies: null, moving: false);
            string humanKey = _npcRenderer.QaLastTriedSheetKey;
            bool c2 = humanKey == "npc_base_melee";
            GD.Print($"[Stage16Sim] C2 гуманоид-melee: финальный ключ цепочки='{humanKey}' → " +
                     $"{(c2 ? "PASS" : "FAIL")}");
            Record("ST16-003/C2 npc-key-chain", c2, $"npc_{NPCRole.Guard}_melee → '{humanKey}'");

            // C3: hit/death/cast NPC — фаза G3, триггеров нет (GAP по плану §6.1).
            // Смерть NPC → немедленный CorpseSpriteRenderer (живой жизненный
            // цикл) — задержку визуальной смерти НЕ вводим (директива аудита).
            RecordGap("ST16-003/C3 npc_hit(G3)", "лист в манифесте G3; триггер урона NPC не подключён");
            RecordGap("ST16-003/C4 npc_cast(G3)", "лист в манифесте G3; триггер каста NPC не подключён");
            RecordGap("ST16-003/C5 npc_death(G3)", "смерть→труп сразу (CorpseSpriteRenderer); hold-кадр — при доставке G3, presentation-only");
        }

        // ================================================================
        //  D. ST16-004 — доменный сигнал движения зверя (Target != null)
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage16Sim] ─── D. ST16-004: сигнал движения зверя ───");
            if (_animalRenderer == null || _animalService == null)
            {
                RecordNotTested("ST16-004/D1", "рендерер/сервис зверей недоступны");
                return;
            }

            // D1: ГАРАНТИРОВАННО живой зверь (требование аудита).
            AnimalEntity wolf = _animalService.SpawnAnimal("wolf", new Position2D(30, 30));
            bool d1 = wolf.IsAlive && wolf.Target == null;
            GD.Print($"[Stage16Sim] D1 живой зверь: {wolf} → {(d1 ? "PASS" : "FAIL")}");
            Record("ST16-004/D1 live-animal", d1, $"спавн волкаQA {wolf.EntityId}: IsAlive, Target=null");

            // D2a: семантика сигналов на РЕАЛЬНОМ пути зверя. Зверь прыгает
            // 1 тайл/тик (мы тикаем 1 раз на 15 кадров = 1Гц тики при 15Гц
            // редраве). Доменный сигнал = Target!=null (весь путь);
            // кадровая дельта = только кадр сразу после прыжка.
            var target = new Position2D(wolf.Position.X + 8, wolf.Position.Y);
            wolf.Target = target;
            wolf.MoveCooldownTicks = 0;

            int samples = 0, frameDeltaMoving = 0, domainMoving = 0;
            var lastSample = new Vector2(wolf.Position.X, wolf.Position.Y);
            bool arrived = false;
            for (int i = 0; i < 15 * 20 && !arrived; i++)   // ≤20 «секунд»
            {
                if (i > 0 && i % 15 == 0)
                {
                    wolf.MoveCooldownTicks = 0;
                    _animalService.Tick(i / 15);            // 1Гц игровой тик
                }
                var cur = new Vector2(wolf.Position.X, wolf.Position.Y);
                bool fd = AnimalSpriteRenderer.QaFrameDeltaMoving(lastSample, cur);
                bool dm = wolf.Target != null;
                samples++;
                if (fd) frameDeltaMoving++;
                if (dm) domainMoving++;
                lastSample = cur;
                if (wolf.Target == null && i > 15) arrived = true;
                await PhysFrames(1);                       // 1/60с — внутри «кадра» 15Гц
            }
            float fdPct = samples > 0 ? 100f * frameDeltaMoving / samples : 0f;
            float dmPct = samples > 0 ? 100f * domainMoving / samples : 0f;
            bool arrivedOk = arrived || wolf.Position == target;
            GD.Print($"[Stage16Sim] D2a путь {samples} семплов: кадр-дельта moving {fdPct:F1}% vs " +
                     $"домен(Target) {dmPct:F1}%; прибыл={arrivedOk}");
            bool d2a = arrivedOk && dmPct > 50f;
            GD.Print($"[Stage16Sim] D2a → {(d2a ? "доменный сигнал воспроизводит путь целиком" : "FAIL — путь не воспроизведён")}");
            // Мерцание (кадр-дельта видит ~1/15 пути) — ДАННЫЕ для вердикта
            // ST16-004; фактический гейт рендера — D2b (AnimId из _Draw).
            bool flickerReproduced = arrivedOk && fdPct < 50f && dmPct > 50f;
            Record("ST16-004/D2a domain-signal", d2a,
                $"кадр-дельта {fdPct:F1}% vs домен {dmPct:F1}% — мерцание " +
                (flickerReproduced
                    ? "ПОДТВЕРЖДЕНО (рендер живёт на кадровой дельте)"
                    : "не воспроизведено"));

            // D2b: фактический рендер. headless _Draw ВЫПОЛНЯЕТСЯ (пруф — G2:
            // кролики у камеры заполняют _lastAnimInfo), поэтому метрика
            // честная — НО (постфикс-ревизия сценария) D2a уводит волка на
            // (38,30), ЗА видимую область камеры (игрок 25,25; вид ≈x∈[16..34])
            // → culling (BUG-007) отбраковывает зверя ДО резолва → _lastAnimInfo
            // хранит СТАРУЮ idle-запись и 45 семплов читают её (walk 0% в
            // ОБЕИХ фазах — артефакт сценария, не сигнала). Возврат волка в
            // кадр + короткая цель в границах вида: 2 тика × 2 тайла < 6 —
            // прибытие не наступает, Target != null весь блок семплов.
            wolf.Position = new Position2D(27, 28);   // рядом с игроком — в кадре
            wolf.Target = null;
            wolf.MoveCooldownTicks = 0;
            int walkFrames = 0, idleFrames = 0, drawn = 0;
            for (int i = 0; i < 45; i++)
            {
                if (i > 0 && i % 15 == 0)
                {
                    wolf.MoveCooldownTicks = 0;
                    if (wolf.Target == null) wolf.Target = new Position2D(wolf.Position.X + 6, wolf.Position.Y);
                    _animalService.Tick(100 + i / 15);
                }
                var info = _animalRenderer.GetAnimalAnimInfo(wolf.EntityId);
                if (info != null)
                {
                    drawn++;
                    // ST16-004: метрика — доменный сигнал движения, использованный
                    // РЕАЛЬНЫМ _Draw (AnimalAnimInfo.Moving = Target != null).
                    // НЕ AnimId.Contains("walk"): недоставленный walk-лист —
                    // процедурный fallback с AnimId '…_static' (доставка PNG —
                    // отдельный контракт G3, Presence зафиксировано в D3).
                    if (info.Value.Moving) walkFrames++;
                    else idleFrames++;
                }
                await PhysFrames(4);   // 4 физ-кадра ≈ 1 кадр редрава 15Гц
            }
            if (drawn == 0)
            {
                RecordNotTested("ST16-004/D2b walk-ratio",
                    "рендер не дал ни одного кадра по зверю (culling/кадр) — метрика снимается в Xvfb-прогоне");
                GD.Print("[Stage16Sim] D2b: кадр по зверю не получен — NOT TESTED (см. Xvfb-артефакты)");
            }
            else
            {
                float walkPct = 100f * walkFrames / drawn;
                // Путь перезапускается каждый тик-15 — волк в пути почти всегда
                // (Target перезаказывается), ожидаем walk-сигнал ≫ idle.
                bool d2b = walkPct > 50f;
                GD.Print($"[Stage16Sim] D2b живой рендер: {drawn} кадров, walk-сигнал {walkPct:F1}% → " +
                         $"{(d2b ? "PASS" : "FAIL — рендер показывает walk редкими кадрами")}");
                Record("ST16-004/D2b walk-ratio", d2b,
                    $"{drawn} кадров рендера: walk-сигнал {walkPct:F1}% (доменный Target, не PNG)");
            }

            // D3: walk/idle резолв (ANIMQA-контракт) на живом звере.
            var a1 = _animalRenderer.ResolveAnimalAnim(wolf.EntityId, wolf.Species, wolf.Size, moving: true);
            var a2 = _animalRenderer.ResolveAnimalAnim(wolf.EntityId, wolf.Species, wolf.Size, moving: false);
            bool d3 = (a1.AnimId.Contains("walk") || a1.AnimId.Contains("static"))
                   && (a2.AnimId.Contains("idle") || a2.AnimId.Contains("static"));
            GD.Print($"[Stage16Sim] D3 резолв: moving=true→'{a1.AnimId}', false→'{a2.AnimId}' → {(d3 ? "PASS" : "FAIL")}");
            Record("ST16-004/D3 walk-idle-resolve", d3, $"true→'{a1.AnimId}', false→'{a2.AnimId}'");

            // D4: run/attack/death — фаза G3 (GAP): рендер сейчас walk/idle.
            RecordGap("ST16-004/D4 animal_run(G3)", "лист в манифесте G3; триггер бега не подключён");
            RecordGap("ST16-004/D5 animal_attack(G3)", "лист в манифесте G3; триггер атаки не подключён");
            RecordGap("ST16-004/D6 animal_death(G3)", "лист в манифесте G3; смерть→труп (саван) сразу");

            // D7: направление зверя — D1-ограничение (боковой силуэт+FlipH
            // при доставке; сейчас процедурный без facing) — GAP плана §6.2.
            RecordGap("ST16-004/D7 facing(D1)", "боковой силуэт + FlipH — контракт доставки D1 (манифест)");
        }

        // ================================================================
        //  E. ST16-005 — порядок формации
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("[Stage16Sim] ─── E. ST16-005: ZIndex/порядок формации ───");
            await PhysFrames(1);
            if (_formationRenderer == null)
            {
                RecordNotTested("ST16-005/E1", "FormationVisualRenderer не найден");
                return;
            }

            // E1: формация на земле — ZIndex в пределах Objects-страты (3),
            // НЕ выше Player (4). Класс-комментарий: «ниже персонажей».
            int zi = _formationRenderer.ZIndex;
            bool e1 = zi <= (int)RenderLayer.Objects;
            GD.Print($"[Stage16Sim] E1 ZIndex формации={zi} (Objects={RenderLayer.Objects}, Player={RenderLayer.Player}) " +
                     $"→ {(e1 ? "PASS" : "FAIL — КОНТУР РИСУЕТСЯ ПОВЕРХ ИГРОКА")}");
            Record("ST16-005/E1 zindex", e1,
                $"ZIndex={zi}; ожид ≤{(int)RenderLayer.Objects} (над Terrain, под Player/телами)");

            // E2: порядок сиблингов — формация ДО предметов/тел/NPC в дереве
            // (равный ZIndex: кто раньше — тот ниже; паттерн трупы-до-NPC).
            var worldRoot = _formationRenderer.GetParent();
            int formIdx = _formationRenderer.GetIndex();
            int? groundIdx = null, npcIdx = null, animalIdx = null;
            foreach (var child in worldRoot.GetChildren())
            {
                if (child is GroundItemRenderer) groundIdx = ((Node)child).GetIndex();
                else if (child is NPCSpriteRenderer) npcIdx = ((Node)child).GetIndex();
                else if (child is AnimalSpriteRenderer) animalIdx = ((Node)child).GetIndex();
            }
            bool beforeBodies = (groundIdx == null || formIdx < groundIdx)
                             && (npcIdx == null || formIdx < npcIdx)
                             && (animalIdx == null || formIdx < animalIdx);
            bool e2 = e1 && beforeBodies;
            GD.Print($"[Stage16Sim] E2 порядок: формация#{formIdx} vs ground#{groundIdx}, npc#{npcIdx}, " +
                     $"animal#{animalIdx} → {(beforeBodies ? "под телами" : "НАД телами (равный Z: позже в дереве)")}");
            Record("ST16-005/E2 sibling-order", e2, $"formation#{formIdx} < ground#{groundIdx}/npc#{npcIdx}/animal#{animalIdx}");

            // E3: Y-sort — известный пробел (план §3.2: не реализован; канон
            // формула RENDER_LAYERS §3.4) — вне объёма фикса слоёв, GAP.
            RecordGap("ST16-005/E3 y-sort", "Y-sort не реализован (план §3.2); канон-формула §3.4 RENDER_LAYERS; batch-рендеры требуют индивидуальной сортировки внутри _Draw");
        }

        // ================================================================
        //  G. ST16-007 — кэши рендереров (длительная сессия)
        // ================================================================

        private async Task ScenarioG()
        {
            GD.Print("[Stage16Sim] ─── G. ST16-007: per-entity кэши рендереров ───");
            if (_npcRenderer == null || _spawner == null || _npcService == null)
            {
                RecordNotTested("ST16-007/G1", "NPC-рендерер/спавнер недоступны");
                return;
            }

            // G1: NPC — циклы спавн→резолв→деспавн: устаревших записей нет.
            int baseNpc = _npcRenderer.QaPerEntityCacheEntries;
            var spawned = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                string id = _spawner.SpawnNPC("human", NPCRole.Passerby, 1, new Position2D(28 + i, 28), 1700 + i);
                spawned.Add(id);
                _ = _npcRenderer.ResolveNpcAnim(id, NPCRole.Passerby, beastSpecies: null, moving: true);
                await PhysFrames(2);
            }
            int withAlive = _npcRenderer.QaPerEntityCacheEntries;
            foreach (var id in spawned) _spawner.DespawnNPC(id);
            await PhysFrames(45);   // 0.75с — окно перескана 0.5с + запас
            int afterDespawn = _npcRenderer.QaPerEntityCacheEntries;
            bool g1 = afterDespawn <= baseNpc + 2;   // +2: следы живых NPC мира
            GD.Print($"[Stage16Sim] G1 NPC кэш: база={baseNpc}, +6 живых={withAlive}, после деспавна={afterDespawn} → " +
                     $"{(g1 ? "PASS (очищен)" : "FAIL — УСТАРЕВШИЕ ЗАПИСИ РАСТУТ БЕЗ ОГРАНИЧЕНИЯ")}");
            Record("ST16-007/G1 npc-cache", g1,
                $"кэш {baseNpc}→{withAlive}→{afterDespawn} (после деспавна 6 NPC)");

            // G2: звери — циклы спавн→резолв→смерть (реальный DamageAppliedEvent).
            // Кролики — РЯДОМ с игроком (в кадре камеры): _Draw заполняет
            // кэш; после смертей записи обязаны уйти (чистка), а не расти.
            if (_animalRenderer == null || _animalService == null || _damagePub == null)
            {
                RecordNotTested("ST16-007/G2", "звериный рендерер/сервис/паблишер недоступны");
                return;
            }
            int baseAnimal = _animalRenderer.QaPerEntityCacheEntries;
            var animals = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                var a = _animalService.SpawnAnimal("rabbit", new Position2D(25 + i % 3, 26 + i / 3));
                animals.Add(a.EntityId);
                _ = _animalRenderer.ResolveAnimalAnim(a.EntityId, a.Species, a.Size, moving: true);
                await PhysFrames(1);
            }
            int aliveAnimal = _animalRenderer.QaPerEntityCacheEntries;
            int drawnRabbits = 0;
            foreach (var id in animals)
                if (_animalRenderer.GetAnimalAnimInfo(id) != null) drawnRabbits++;
            GD.Print($"[Stage16Sim] G2 предусловие: нарисованных кроликов={drawnRabbits}/6 (кэш заполнен)");
            foreach (var id in animals)
            {
                _damagePub.Publish(new DamageAppliedEvent(
                    "qa_stage16", id, 999999, DamageType.Physical,
                    Element.Neutral, BodyPartType.Head, CombatAttackResult.Kill));
            }
            await PhysFrames(45);   // окно чистки (0.5с цикл + запас)
            int deadAnimal = _animalRenderer.QaPerEntityCacheEntries;
            int staleRabbits = 0;
            foreach (var id in animals)
                if (_animalRenderer.GetAnimalAnimInfo(id) != null) staleRabbits++;
            // Инвариант: записи УМЕРШИХ сущностей не остаются (чистка), и
            // суммарный кэш не растёт от смертей (deadAnimal ≤ aliveAnimal).
            bool g2 = drawnRabbits >= 3 && staleRabbits == 0 && deadAnimal <= aliveAnimal;
            GD.Print($"[Stage16Sim] G2 звериный кэш: база={baseAnimal}, +6 живых={aliveAnimal}, " +
                     $"после смертей={deadAnimal}, устаревших записей кроликов={staleRabbits} → " +
                     $"{(g2 ? "PASS (очищен)" : "FAIL — УСТАРЕВШИЕ ЗАПИСИ РАСТУТ / НЕ ЧИСТЯТСЯ")}");
            Record("ST16-007/G2 animal-cache", g2,
                $"кэш {baseAnimal}→{aliveAnimal}→{deadAnimal}; stale-кроликов={staleRabbits}/{drawnRabbits} нарисованных");

            // G3: culling-чистота (BUG-007): отсечённые сущности не обновляют
            // кэш движения — 1 кадр walk при возврате, самокоррекция —
            // документированное поведение (комментарий рендерера) — GAP.
            RecordGap("ST16-007/G3 culling-1frame",
                "отсечение не обновляет кэш движения: возврат на экран даёт ≤1 кадр walk; самокоррекция (BUG-007)");
        }

        // ================================================================
        //  H. ST16-008 — геометрия/негативный кэш/реестр/честность
        // ================================================================

        private async Task ScenarioH()
        {
            GD.Print("[Stage16Sim] ─── H. ST16-008: PNG/fallback/реестр ───");
            await PhysFrames(1);

            // H1: невалидная геометрия → диагностика + fallback (не краш).
            // 30×30 не кратно 64 → LoadSheet печатает PrintErr и возвращает null.
            var badPath = "res://resources/sprites/_qa/qa_bad_geometry_stage16.png";
            var img = Image.CreateEmpty(30, 30, false, Image.Format.Rgba8);
            img.SavePng(badPath);
            var bad = SpriteSheetCache.LoadSheetDirect(badPath, 10, true);
            bool h1 = bad == null;
            GD.Print($"[Stage16Sim] H1 плохая геометрия 30×30: sheet={(bad != null ? "ЗАГРУЖЕН (НЕ ОК)" : "null+диагностика")} → " +
                     $"{(h1 ? "PASS" : "FAIL")}");
            Record("ST16-008/H1 bad-geometry", h1, "30×30 → PrintErr + null → процедурный fallback");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(badPath));

            // H2: негативный кэш + ResetCache (ключ, который НИГДЕ раньше не
            // резолвился — иначе кэш уже горячий и попыток не будет).
            int before = SpriteSheetCache.LoadAttemptCount;
            var miss = SpriteSheetCache.TryGetSheet("animal_dragon_sleep");   // нет PNG → null (кэш)
            var miss2 = SpriteSheetCache.TryGetSheet("animal_dragon_sleep");  // второй раз — из негативного кэша
            bool h2a = miss == null && miss2 == null;
            int attemptsAfterMiss = SpriteSheetCache.LoadAttemptCount - before;
            SpriteSheetCache.ResetCache();
            var miss3 = SpriteSheetCache.TryGetSheet("animal_dragon_sleep");
            bool h2 = h2a && attemptsAfterMiss == 1
                   && SpriteSheetCache.LoadAttemptCount == 1;
            GD.Print($"[Stage16Sim] H2 негативный кэш: попыток на 2 резолва={attemptsAfterMiss} (ожид 1), " +
                     $"ResetCache→повторная попытка={(SpriteSheetCache.LoadAttemptCount == 1 ? "да" : "НЕТ")} → {(h2 ? "PASS" : "FAIL")}");
            Record("ST16-008/H2 negative-cache", h2,
                $"2 резолва={attemptsAfterMiss} попытка; ResetCache перечитывает");

            // H3: реестр 13/6/5 (ANIMQA step7-контракт; уникальные ключи).
            int p = 0, n = 0, a = 0;
            foreach (var id in SpriteSheetCache.RegisteredAnimIds)
            {
                if (id.StartsWith("player_")) p++;
                else if (id.StartsWith("npc_")) n++;
                else if (id.StartsWith("animal_")) a++;
            }
            bool h3 = p == 13 && n == 6 && a == 5;
            GD.Print($"[Stage16Sim] H3 реестр: player={p}/13, npc={n}/6, animal={a}/5 → {(h3 ? "PASS" : "FAIL")}");
            Record("ST16-008/H3 registry", h3, $"player={p}, npc={n}, animal={a}");

            // H4: честность ANIMQA-зверей: отсутствие живых зверей = WARN,
            // НЕ PASS (SpriteAnimSimDebug step6: beastChecked=false → WARN).
            RecordDisproved("ST16-008/H4 animqa-honesty",
                "SpriteAnimSimDebug step6: нет живых зверей → WARN в лог, не PASS (код:249-274)");
        }

        // ================================================================
        //  I. ST16-009 — синхрон тела и оружия
        // ================================================================

        private async Task ScenarioI()
        {
            GD.Print("[Stage16Sim] ─── I. ST16-009: тело/MainHand синхрон ───");
            var anim = _world!.PlayerAnimatorForQA!;
            if (_attackIntentPub == null || _spawner == null)
            {
                RecordNotTested("ST16-009/I1", "паблишер/спавнер недоступны");
                return;
            }

            // Цель для замаха (GWC требует NPC-цель в AttackIntentEvent).
            string targetId = _spawner.SpawnNPC("human", NPCRole.Passerby, 1, new Position2D(27, 27), 1801);

            // I1: один AttackIntentEvent запускает И аниматор, И MainHand
            // в одном кадре (общая точка: GWC.OnAttackIntentForSwing).
            _attackIntentPub.Publish(new AttackIntentEvent(
                "player", targetId, "basic_attack", false, 1000, false, 0, 0));
            await PhysFrames(1);
            bool bodyMelee = anim.AnimId == "player_melee";
            bool handSwinging = _world.QaMainHandSwinging;
            bool i1 = bodyMelee && handSwinging;
            GD.Print($"[Stage16Sim] I1 старт: тело='{anim.AnimId}', MainHand свинг={handSwinging} → " +
                     $"{(i1 ? "PASS (один кадр)" : "FAIL — рассинхрон старта")}");
            Record("ST16-009/I1 sync-start", i1, $"AttackIntentEvent → тело '{anim.AnimId}' + свинг {handSwinging}");

            // I2: длительности совпадают (0.42с оба): после 0.45с — оба off.
            await PhysFrames(30);   // ~0.5с
            bool bodyDone = anim.AnimId != "player_melee";
            bool handDone = !_world.QaMainHandSwinging;
            bool i2 = bodyDone && handDone;
            GD.Print($"[Stage16Sim] I2 финал: тело='{anim.AnimId}' (не melee={bodyDone}), свинг завершён={handDone} → " +
                     $"{(i2 ? "PASS" : "FAIL")}");
            Record("ST16-009/I2 sync-duration", i2, "0.42с тело ≡ 0.42с MainHand (MainHandSwingSec)");

            // I3: фактический FlipH/позиционирование — скриншоты (Xvfb) +
            // WEAPONVIS (facing-флаг); headless-сим геометрию не рисует.
            RecordNotTested("ST16-009/I3 fliph-visual",
                "фактический FlipH/offset — скриншоты Xvfb + WEAPONVIS (флаг facing)");

            // I4: неизвестный класс → документированный fallback sword.
            var junk = new EquipmentData
            {
                ItemId = "eq_wep_junk_000001",
                NameRu = "Мусорный предмет",
                Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponMain,
                Rarity = ItemRarity.Common,
                MaterialTier = 1,
                WeaponClassId = "plasma_rifle",
            };
            var vis = WeaponVisualCatalog.Resolve(junk);
            bool i4 = vis != null && vis.Key.StartsWith("sword|");
            GD.Print($"[Stage16Sim] I4 неизвестный класс: ключ='{vis?.Key}' → {(i4 ? "PASS (fallback sword)" : "FAIL")}");
            Record("ST16-009/I4 unknown-class", i4, $"WeaponClassOf('plasma_rifle') → '{vis?.Key}'");
        }

        // ================================================================
        //  F. ST16-006 — Shield-жизненный цикл (последний: трогает Ци)
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("[Stage16Sim] ─── F. ST16-006: Shield one-shot vs Qi-буфер ───");
            if (_fxRenderer == null || _qiBuffer == null || _castResultPub == null || _qiService == null)
            {
                RecordNotTested("ST16-006/F1", "VFX-рендерер/QiBuffer/Qi/паблишер недоступны");
                return;
            }

            // Предусловие: свежему игроку может не хватать Ци на буфер
            // (MIN_QI_FOR_BUFFER=10, инвестиция 50) — пополняем до активации.
            _qiService.AddQi(1000);

            // F1: активация буфера (persistent) + успешный каст Defense
            // (one-shot 2.5с) = ровно 2 визуала (1+1).
            _qiBufferEntityIds.Clear();
            _qiBuffer.Activate(50, QiBufferMode.Shield);
            await PhysFrames(2);
            GD.Print($"[Stage16Sim] F1 события QiBufferStateChanged: [{string.Join(", ", _qiBufferEntityIds)}]");
            int afterActivate = _fxRenderer.QaShieldVisualCount;
            _castResultPub.Publish(new TechniqueCastResultEvent(
                "qi_shield", true, "", 0, 0, 0, 0,
                TechniqueType.Defense, Element.Water, 4));
            await PhysFrames(2);
            int afterCast = _fxRenderer.QaShieldVisualCount;
            bool f1 = afterActivate == 1 && afterCast == 2;
            GD.Print($"[Stage16Sim] F1 активация={afterActivate}, +каст={afterCast} (ожид 1 и 2) → " +
                     $"{(f1 ? "PASS" : "FAIL")}");
            Record("ST16-006/F1 activation+cast", f1,
                $"QiBuffer persistent={afterActivate}, с one-shot каста={afterCast}");

            // F2: поглощённые удары НЕ стакают persistent-щит
            // (QiBufferService.AbsorbDamage репаблишит IsActive=true).
            _qiBuffer.AbsorbDamage(3, DamageType.Physical);
            await PhysFrames(2);
            int afterAbs1 = _fxRenderer.QaShieldVisualCount;
            _qiBuffer.AbsorbDamage(3, DamageType.Physical);
            await PhysFrames(2);
            int afterAbs2 = _fxRenderer.QaShieldVisualCount;
            bool f2 = afterAbs1 == 2 && afterAbs2 == 2;
            GD.Print($"[Stage16Sim] F2 AbsorbDamage×2: {afterCast}→{afterAbs1}→{afterAbs2} (ожид 2→2→2) → " +
                     $"{(f2 ? "PASS" : "FAIL — СТАКИ ПОВТОРНЫХ АКТИВАЦИЙ")}");
            Record("ST16-006/F2 no-stacking", f2,
                $"после 2 поглощений счётчик {afterAbs2} (ожид 2: 1 persistent + 1 one-shot)");

            // F3: деактивация буфера убирает ТОЛЬКО persistent — чужой
            // one-shot живёт свои 2.5с.
            _qiBuffer.Deactivate();
            await PhysFrames(2);
            int afterDeact = _fxRenderer.QaShieldVisualCount;
            bool f3 = afterDeact == 1;
            GD.Print($"[Stage16Sim] F3 деактивация: {afterAbs2}→{afterDeact} (ожид 1 — one-shot жив) → " +
                     $"{(f3 ? "PASS" : "FAIL — ОДНОРАЗОВЫЙ ЭФФЕКТ УБИТ ПРЕЖДЕВРЕМЕННО")}");
            Record("ST16-006/F3 deactivate-sparing", f3,
                $"после деактивации буфера осталось {afterDeact} (ожид 1 one-shot)");

            // F4: one-shot доигрывает свои 2.5с и уходит сам.
            await PhysFrames(180);   // ~3с > 2.5с
            int afterExpiry = _fxRenderer.QaShieldVisualCount;
            bool f4 = afterExpiry == 0;
            GD.Print($"[Stage16Sim] F4 истечение one-shot (2.5с): {afterDeact}→{afterExpiry} → {(f4 ? "PASS" : "FAIL")}");
            Record("ST16-006/F4 oneshot-expiry", f4, $"через 3с счётчик {afterExpiry} (ожид 0)");

            // Чистка: буфер деактивирован (F3), Qi-событий больше не ждём.
            _qiBufferStateToken?.Dispose();
            _qiBufferStateToken = null;
        }
    }
}
