#nullable enable
// Создано: 2026-10-23 — Этап-14 аудита владельца (диалоги, взаимодействия,
// фокус ввода, модальные окна). Канон:
// checkpoints/10_23_stage14_interaction_focus_audit_request.md ([STAGE 14
// AUDIT] verbatim + пере-скоуп-таблица пре-верификации ST14-PRECHECK).
// Запуск:
//   GODOT_NEWGAME=1 GODOT_STAGE14_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// ДОПОЛНЯЕТ (не дублирует) MODALQA/MODAL2 (pause-инварианты), DIALOGUE
// (базовая печать/выбор), CONTEXT (попапы) — новые аспекты этапа-14:
//   A. ST14-001 — текст-фокус глушит игровые sticky (GuiGetFocusOwner;
//      медитация V/окно B/цифры при фокусе в LineEdit имени сейва).
//   B. ST14-002 — единый предикат модальности (QaAnyModalWindowOpen ==
//      QaWorldMouseBlocked для каждого accessible-окна; SetOverUI-список
//      из того же источника).
//   C. ST14-003 — Esc по ФАКТИЧЕСКОМУ стеку: T→B: Esc закрывает B;
//      B→T: Esc закрывает T (прежде — жёсткий порядок идентификаторов);
//      инвариант: пред-существующая пауза переживает закрытие окна.
//   D. ST14-005 — E над планировочным окном (меню сейвов) не запускает
//      скрытые взаимодействия (диалог/обыск остаются закрытыми).
//   E. ST14-006 — потеря фокуса приложения: held-ввод (движение/бег)
//      сбрасывается нулевым кадром (OS-путь — физический PT-U06, здесь —
//      контракт обработчика через QA-хук).
//   F. ST14-004-edge — GoToNode на несуществующий узел: диагностика +
//      честный EndDialogue (окно закрыто, диалог завершён; прежде старый
//      текст висел «призраком»).
//
// Итог: [Stage14Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/NOT TESTED).
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.Input;
using GodotInput = Godot.Input;
using CultivationGame.Modules.Interaction;

namespace CultivationGame.Adapter.Scene
{
    public partial class Stage14InteractionFocusTruthSimDebug : Node
    {
        [Inject] private IPlayerInputService? _input;
        [Inject] private ITimeService? _time;
        [Inject] private INPCSpawnerService? _spawner;
        [Inject] private DialogueService? _dialogues;
        [Inject] private ISubscriber<MeditationStateChangedEvent>? _meditationSub;
        [Inject] private ISubscriber<DialogueEndedEvent>? _dialogueEndedSub;
        [Inject] private IPublisher<ToastShownEvent>? _toastPub;

        private IDisposable? _meditationToken;
        private IDisposable? _dialogueEndedToken;
        private int _meditationEvents;
        private int _dialogueEndedEvents;

        private readonly List<(string Id, string Status, string Detail)> _results = new();
        private GameWorldController? _world;
        private InputAdapter? _adapter;

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _meditationToken = _meditationSub?.Subscribe((in MeditationStateChangedEvent e) => _meditationEvents++);
            _dialogueEndedToken = _dialogueEndedSub?.Subscribe((in DialogueEndedEvent e) => _dialogueEndedEvents++);

            GD.Print("[Stage14Sim] Ready — Этап-14 (ввод/фокус/модальные окна) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _meditationToken?.Dispose();
            _dialogueEndedToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
            _adapter = FindAdapter(GetTree().Root);
            if (_world == null || _adapter == null || _input == null || _time == null)
            {
                GD.Print("[Stage14Sim] FAIL — GWC/InputAdapter/сервисы не найдены");
                PrintSummary();
                return;
            }
            GD.Print("[Stage14Sim] GWC и InputAdapter найдены; player input service готов");

            await ScenarioA();   // ST14-001 текст-фокус
            await ScenarioB();   // ST14-002 единый предикат
            await ScenarioC();   // ST14-003 Esc-стек
            await ScenarioD();   // ST14-005 E-гейт
            await ScenarioE();   // ST14-006 потеря фокуса
            await ScenarioF();   // ST14-004-edge GoToNode-призрак

            PrintSummary();
        }

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

        private static InputAdapter? FindAdapter(Node node)
        {
            if (node is InputAdapter a) return a;
            foreach (var child in node.GetChildren())
            {
                var found = FindAdapter(child);
                if (found != null) return found;
            }
            return null;
        }

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void RecordNotTested(string id, string detail)
            => _results.Add((id, "NOT TESTED", detail));

        /// <summary>Физических кадров (InputAdapter опрашивает в _PhysicsProcess;
        /// sticky-действия GWC потребляет в HandleStickyInput того же тика).</summary>
        private async Task PhysFrames(int n)
        {
            for (int i = 0; i < n; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        private async Task PressAction(string action, int frames = 4)
        {
            GodotInput.ActionPress(action);
            await PhysFrames(frames);
            GodotInput.ActionRelease(action);
            await PhysFrames(2);
        }

        private static LineEdit? FindLineEdit(Node root)
        {
            if (root is LineEdit le) return le;
            foreach (var child in root.GetChildren())
            {
                var found = FindLineEdit(child);
                if (found != null) return found;
            }
            return null;
        }

        // ================================================================
        //  A. ST14-001 — текст-фокус глушит игровые действия
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage14Sim] ─── A. ST14-001: клавиатурный фокус в LineEdit ───");
            var menu = _world!.SaveLoadMenuForQA;
            if (menu == null) { RecordNotTested("A", "SaveLoadMenu недоступен"); return; }
            menu.Open(CultivationGame.Adapter.UI.SaveLoadMenuMode.Full);
            await PhysFrames(2);
            var edit = FindLineEdit(menu);
            if (edit == null) { RecordNotTested("A", "LineEdit имени сейва не найден"); menu.Close(); return; }

            edit.GrabFocus();
            await PhysFrames(3);
            bool blocked = _adapter!.QaTextFocusBlocked;
            GD.Print($"[Stage14Sim] A1: фокус в LineEdit → адаптер глушит игровые sticky: {blocked} (ожид true)");

            int medBefore = _meditationEvents;
            await PressAction("meditate");
            bool a2a = _meditationEvents == medBefore;
            GD.Print($"[Stage14Sim] A2a: V (медитация) при фокусе: событий {_meditationEvents - medBefore} (ожид 0) → {(a2a ? "PASS" : "FAIL")}");

            var inv = _world.InventoryWindowForQA;
            await PressAction("inventory");
            bool a2b = inv is { Visible: false };
            GD.Print($"[Stage14Sim] A2b: B (инвентарь) при фокусе: окно не открылось={a2b} → {(a2b ? "PASS" : "FAIL")}");

            await PressAction("interact");
            bool a2c = _world.DialogueWindowForQA is not { IsOpen: true }
                && _world.LootWindowForQA is not { IsOpen: true };
            GD.Print($"[Stage14Sim] A2c: E при фокусе: скрытых взаимодействий нет={a2c} → {(a2c ? "PASS" : "FAIL")}");

            edit.ReleaseFocus();
            await PhysFrames(3);
            bool unblocked = !_adapter.QaTextFocusBlocked;
            await PressAction("inventory");
            bool a3 = unblocked && inv is { Visible: true };
            GD.Print($"[Stage14Sim] A3: фокус снят → глушение снято={unblocked}; B открыл инвентарь={inv is { Visible: true }} → {(a3 ? "PASS" : "FAIL")}");
            if (inv is { Visible: true }) { inv.Toggle(); await PhysFrames(2); }

            bool a = blocked && a2a && a2b && a2c && a3;
            Record("A", a, $"blocked={blocked}, med={a2a}, inv={a2b}, E={a2c}, control={a3}");
            menu.Close();
            await PhysFrames(2);
        }

        // ================================================================
        //  B. ST14-002 — единый предикат модальности
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage14Sim] ─── B. ST14-002: предикаты модальности согласованы ───");
            var w = _world!;
            int checkedWindows = 0; bool allOk = true;

            void CheckOpen(string tag, bool opened, Action close)
            {
                if (!opened) { GD.Print($"[Stage14Sim] B: {tag} — НЕ ОТКРЫЛСЯ (гейт?) — пропускаем"); return; }
                bool any = w.QaAnyModalWindowOpenForQA;
                bool mouse = w.QaWorldMouseBlockedForQA;
                bool ok = any && mouse;
                GD.Print($"[Stage14Sim] B: {tag} открыт: AnyModal={any}, WorldMouseBlocked={mouse} → {(ok ? "PASS" : "FAIL")}");
                allOk &= ok;
                checkedWindows++;
                close();
            }

            var inv = w.InventoryWindowForQA;
            inv?.Toggle(); await PhysFrames(2);
            CheckOpen("inventory(B)", inv is { Visible: true }, () => { if (inv is { Visible: true }) inv.Toggle(); });
            await PhysFrames(2);

            var sheet = w.CharacterSheetWindowForQA;
            sheet?.Toggle(); await PhysFrames(2);
            CheckOpen("characterSheet(C)", sheet is { Visible: true }, () => { if (sheet is { Visible: true }) sheet.Toggle(); });
            await PhysFrames(2);

            var quest = w.QuestWindowForQA;
            quest?.Toggle(); await PhysFrames(2);
            CheckOpen("quest(Q)", quest is { Visible: true }, () => { if (quest is { Visible: true }) quest.Toggle(); });
            await PhysFrames(2);

            var journal = w.EventLogWindowForQA;
            journal?.Toggle(); await PhysFrames(2);
            CheckOpen("eventLog(J)", journal is { Visible: true }, () => { if (journal is { Visible: true }) journal.Toggle(); });
            await PhysFrames(2);

            var hotkeys = w.HotkeysWindowForQA;
            hotkeys?.Open(); await PhysFrames(2);
            CheckOpen("hotkeys(F1)", hotkeys is { Visible: true }, () => hotkeys?.Close());
            await PhysFrames(2);

            var book = w.TechniqueBookWindowForQA;
            book?.Open(); await PhysFrames(2);
            CheckOpen("techniqueBook(T)", book is { Visible: true }, () => book?.Close());
            await PhysFrames(2);

            var cult = w.CultivationWindowForQA;
            cult?.Toggle(); await PhysFrames(2);
            CheckOpen("cultivation(K)", cult is { Visible: true }, () => { if (cult is { Visible: true }) cult.Toggle(); });
            await PhysFrames(2);

            var menu = w.SaveLoadMenuForQA;
            menu?.Open(CultivationGame.Adapter.UI.SaveLoadMenuMode.Full); await PhysFrames(2);
            CheckOpen("saveLoad(F5)", menu is { Visible: true }, () => menu?.Close());
            await PhysFrames(2);

            var charger = w.ChargerWindowForQA;
            charger?.Toggle(); await PhysFrames(2);
            CheckOpen("charger(H)", charger is { Visible: true }, () => { if (charger is { Visible: true }) charger.Toggle(); });
            await PhysFrames(2);

            // Базовая линия: всё закрыто → оба предиката false.
            bool baselineOk = !w.QaAnyModalWindowOpenForQA && !w.QaWorldMouseBlockedForQA;
            GD.Print($"[Stage14Sim] B: базовая линия (все закрыты): AnyModal={!w.QaAnyModalWindowOpenForQA}, WorldMouseBlocked={!w.QaWorldMouseBlockedForQA} → {(baselineOk ? "PASS" : "FAIL")}");

            if (checkedWindows >= 6)
                Record("B", allOk && baselineOk, $"окон проверено={checkedWindows}, все согласованы={allOk}, baseline={baselineOk}");
            else
                RecordNotTested("B", $"доступных окон < 6 ({checkedWindows}) — гейты спавна");
        }

        // ================================================================
        //  C. ST14-003 — Esc по фактическому стеку
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage14Sim] ─── C. ST14-003: Esc закрывает фактический верх ───");
            var w = _world!;
            var book = w.TechniqueBookWindowForQA;
            var inv = w.InventoryWindowForQA;
            if (book == null || inv == null) { RecordNotTested("C", "окна недоступны"); return; }

            // C1: T открыт ПЕРВЫМ, B ПОВЕРХ → Esc должен закрыть B.
            book.Open(); await PhysFrames(2);
            inv.Toggle(); await PhysFrames(2);
            await PressAction("pause");
            bool c1 = inv is { Visible: false } && book is { Visible: true };
            GD.Print($"[Stage14Sim] C1: T→B, Esc: инвентарь закрыт={inv is { Visible: false}}, книга открыта={book is { Visible: true}} → {(c1 ? "PASS" : "FAIL")}");

            // C2: повторный Esc закрывает книгу (следующий в стеке).
            await PressAction("pause");
            bool c2 = book is { Visible: false };
            GD.Print($"[Stage14Sim] C2: второй Esc: книга закрыта={c2} → {(c2 ? "PASS" : "FAIL")}");
            await PhysFrames(2);

            // C3: обратный порядок — B первым, T поверх → Esc закрывает T.
            inv.Toggle(); await PhysFrames(2);
            book.Open(); await PhysFrames(2);
            await PressAction("pause");
            bool c3a = book is { Visible: false } && inv is { Visible: true };
            await PressAction("pause");
            bool c3b = inv is { Visible: false };
            bool c3 = c3a && c3b;
            GD.Print($"[Stage14Sim] C3: B→T, Esc: первым закрыт T={c3a}, вторым B={c3b} → {(c3 ? "PASS" : "FAIL")}");
            await PhysFrames(2);

            // C4: инвариант паузы: пред-существующая Esc-пауза переживает
            // закрытие окна (гейт _wasPausedBeforeInventory). Окно открываем
            // ИГРОВЫМ путём (клавиша T → HandleModalPauseOnOpen делает
            // снапшот паузы) — прямой book.Open() снапшот не делает и
            // проверял бы не тот путь.
            _time!.Pause();
            await PressAction("techniques"); // T: открывает книгу поверх паузы
            bool c4open = book is { Visible: true };
            await PressAction("pause");      // Esc закрывает книгу
            bool c4 = c4open && _time.IsPaused; // пауза НЕ снята закрытием окна
            _time.Resume();
            GD.Print($"[Stage14Sim] C4: T поверх паузы (книга открыта={c4open}), Esc-закрытие: пауза пережила={_time.IsPaused} (ожид true) → {(c4 ? "PASS" : "FAIL")}");

            Record("C", c1 && c2 && c3 && c4, $"T→B={c1}, second-Esc={c2}, B→T={c3}, pause-invariant={c4}");
        }

        // ================================================================
        //  D. ST14-005 — E над планировочным окном
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage14Sim] ─── D. ST14-005: E не запускает скрытые взаимодействия ───");
            var menu = _world!.SaveLoadMenuForQA;
            if (menu == null) { RecordNotTested("D", "SaveLoadMenu недоступен"); return; }

            menu.Open(CultivationGame.Adapter.UI.SaveLoadMenuMode.Full);
            await PhysFrames(2);
            await PressAction("interact");
            bool d1 = _world.DialogueWindowForQA is not { IsOpen: true }
                && _world.LootWindowForQA is not { IsOpen: true };
            GD.Print($"[Stage14Sim] D1: E при открытом меню сейвов: диалог/обыск не открылись={d1} → {(d1 ? "PASS" : "FAIL")}");

            // Контроль: меню закрыто → предикаты пускают E к миру (поблизости
            // нет NPC/трупов — E уходит в безобидный HandlePickup; позитивный
            // путь взаимодействия покрыт QUEST/LOOT-симами).
            menu.Close(); await PhysFrames(2);
            bool d2 = !_world.QaAnyModalWindowOpenForQA;
            GD.Print($"[Stage14Sim] D2: меню закрыто → мир доступен для E={d2} (контроль предиката) → {(d2 ? "PASS" : "FAIL")}");

            Record("D", d1 && d2, $"gated={d1}, control={d2}");
        }

        // ================================================================
        //  E. ST14-006 — потеря фокуса приложения
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("[Stage14Sim] ─── E. ST14-006: потеря фокуса сбрасывает held-ввод ───");
            if (_input == null || _adapter == null) { RecordNotTested("E", "сервисы недоступны"); return; }

            GodotInput.ActionPress("move_right");
            GodotInput.ActionPress("run");
            await PhysFrames(4);
            bool heldBefore = _input.MoveDirection.X > 0f && _input.RunHeld;
            GD.Print($"[Stage14Sim] E1: held-ввод активен: move={_input.MoveDirection}, run={_input.RunHeld} → {heldBefore}");

            _adapter.QaSimulateFocusLoss();
            await PhysFrames(2);
            bool cleared = _input.MoveDirection == default && !_input.RunHeld;
            GD.Print($"[Stage14Sim] E2: после потери фокуса (клавиши ЕЩЁ зажаты): move={_input.MoveDirection}, run={_input.RunHeld} → сброшено={cleared} {(cleared ? "PASS" : "FAIL")}");

            GodotInput.ActionRelease("move_right");
            GodotInput.ActionRelease("run");
            await PhysFrames(2);

            // E3 (контроль): после «возврата фокуса» действие снова требует
            // АКТУАЛЬНОГО ввода — повторное нажатие двигает.
            GodotInput.ActionPress("move_right");
            await PhysFrames(3);
            bool e3 = _input.MoveDirection.X > 0f;
            GodotInput.ActionRelease("move_right");
            await PhysFrames(2);
            GD.Print($"[Stage14Sim] E3: новое нажатие после потери фокуса двигает={e3} → {(e3 ? "PASS" : "FAIL")}");

            // Реальный OS-путь (alt-tab) headless не воспроизводится —
            // физический сценарий PT-U06; здесь проверен контракт обработчика.
            Record("E", heldBefore && cleared && e3,
                $"held={heldBefore}, cleared={cleared}, re-press={e3}; OS-путь — PT-U06 (физический)");
        }

        // ================================================================
        //  F. ST14-004-edge — GoToNode на несуществующий узел
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("[Stage14Sim] ─── F. ST14-004-edge: узел-призрак завершает диалог ───");
            if (_dialogues == null || _spawner == null) { RecordNotTested("F", "DI недоступен"); return; }

            string? npcId = _spawner.SpawnNPC("human", CultivationGame.Core.Data.NPCRole.Passerby, 1,
                new CultivationGame.Core.Data.Position2D(30, 30), 2026102401);
            if (npcId == null) { RecordNotTested("F", "спавн NPC не удался"); return; }

            // Диалог: start → (линейный) → ghost_node (несуществующий).
            var nodes = new System.Collections.Generic.Dictionary<string, CultivationGame.Modules.Interaction.Data.DialogueNode>
            {
                ["start"] = new CultivationGame.Modules.Interaction.Data.DialogueNode
                {
                    NodeId = "start",
                    Text = "Первый узел этапа-14.",
                    NextNodeId = "ghost_node",
                },
            };
            _dialogues.RegisterDialogue("qa_stage14_ghost_dialog", nodes);
            // Полный ИГРОВОЙ путь: MapNpcDialogue + DialogueDebugOpen
            // (TryStartNpcDialogue + снапшот паузы + открытие окна).
            _dialogues.MapNpcDialogue(npcId, "qa_stage14_ghost_dialog");
            bool started = _world!.DialogueDebugOpen(npcId);
            await PhysFrames(6); // typewriter печатает
            bool winOpen = _world.DialogueWindowForQA is { IsOpen: true };
            GD.Print($"[Stage14Sim] F1: игровой путь DialogueDebugOpen: старт={started}, окно открыто={winOpen}");

            int endedBefore = _dialogueEndedEvents;
            _dialogues.AdvanceDialogue();   // докончить печать либо линейный переход
            await PhysFrames(4);
            _dialogues.AdvanceDialogue();   // линейный переход на ghost_node → EndDialogue
            await PhysFrames(4);
            bool f2 = _world.DialogueWindowForQA is not { IsOpen: true }
                && _dialogueEndedEvents > endedBefore;
            GD.Print($"[Stage14Sim] F2: переход на несуществующий узел: окно закрыто={_world.DialogueWindowForQA is not { IsOpen: true}}, DialogueEnded +{_dialogueEndedEvents - endedBefore} → {(f2 ? "PASS" : "FAIL")}");

            Record("F", started && winOpen && f2, $"start={started}, ghost-end={f2}");
        }

        // ================================================================
        //  Итоги
        // ================================================================

        private void PrintSummary()
        {
            int pass = 0, fail = 0, nt = 0;
            foreach (var (_, status, _) in _results)
            {
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else nt++;
            }
            GD.Print($"[Stage14Sim] Итого: PASS={pass}, FAIL={fail}, NOT TESTED={nt}");
            GD.Print($"[Stage14Sim] VERDICT: {(fail == 0 && pass > 0 ? "PASS" : "FAIL")}");
        }
    }
}
