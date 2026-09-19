#nullable enable
// MODAL2 (2026-09-19): исчерпывающий перебор стеков модальных окон.
// Причина: повторный баг-репорт пользователя (19.09): «снова сломалось
// перемещение после закрытия инвентаря — как будто блокиратор движения
// не снимает запрет». MODALQA-1 покрывает одиночные циклы + одну пару
// «инвентарь+лавка»; все 30 пар toggle-окон и порядок закрытия не
// перебирались — дыра класса INP-1 могла уцелеть в непокрытой паре.
//
// Headless-верификация (GODOT_MODAL2_DEBUG=1) инварианта
// «пауза ⇔ (стек модальных окон непуст) ∨ (Esc игрока)»:
//   A. 30 пар (нижнее×верхнее): клавиша открыла A → клавиша открыла B
//      поверх → закрытие B методом окна (×/bg-путь, мимо GWC-ветки) →
//      пауза ДОЛЖНА держаться (A держит стек) → закрытие A методом →
//      пауза ДОЛЖНА сняться.
//   B. Обратное закрытие (все пары): A открыта → B поверх → закрыть A
//      методом при живом B → пауза держится → закрыть B КЛАВИШЕЙ
//      (GWC-ветка + Closed) → пауза снята.
//   C. Пауза игрока (инвариант снапшота): Esc ставит паузу (окна
//      закрыты) → B открыта → B закрыта методом → пауза ОСТАЁТСЯ
//      (Esc-пауза переживает окна) → Esc снимает.
//   D. Тройной стек inv→sheet→quest: закрытие в двух порядках —
//      пауза снимается только с последним окном.
// Эмуляция клавиш: Input.ActionPress/Release (headless OK);
// InputAdapter (polling) → stickyKeys → GWC-ветка.
// Запуск: GODOT_NEWGAME=1 GODOT_MODAL2_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System.Collections.Generic;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

public partial class ModalStackSimDebug : Node
{
    [Inject] private ITimeService? _time;

    private const int WaitFrames = 4; // InputAdapter→stickyKeys→GWC→ResetFrameFlags

    /// <summary>Окно перебора: action + вид + методы честных путей закрытия.</summary>
    private sealed class ModalWin
    {
        public required string Action;      // input action (клавиша GWC-ветки)
        public required string Label;       // человекочитаемо
        public required Control Win;        // окно
        public required System.Action CloseByMethod; // «×»/bg-путь (мимо GWC)
        public System.Func<bool> IsOpen => () => Win.Visible;
    }

    private List<ModalWin> _wins = new();

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        GD.Print("[ModalStackSim] Ready — перебор стеков модальных окон через 2с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null || _time == null)
        {
            GD.Print("[ModalStackSim] VERDICT: FAIL — GameWorldController/ITimeService not found");
            return;
        }

        var inv = world.InventoryWindowForQA;
        var sheet = world.CharacterSheetWindowForQA;
        var quest = world.QuestWindowForQA;
        var journal = world.EventLogWindowForQA;
        var hotkeys = world.HotkeysWindowForQA;
        var book = world.TechniqueBookWindowForQA;
        if (inv == null || sheet == null || quest == null || journal == null
            || hotkeys == null || book == null)
        {
            GD.Print("[ModalStackSim] VERDICT: FAIL — окна не найдены (DI/UIInitPhase)");
            return;
        }

        _wins = new List<ModalWin>
        {
            new() { Action = "inventory",      Label = "B",  Win = inv,     CloseByMethod = inv.Toggle     },
            new() { Action = "character_sheet", Label = "C",  Win = sheet,  CloseByMethod = sheet.Toggle  },
            new() { Action = "quest_log",      Label = "Q",  Win = quest,   CloseByMethod = quest.Toggle   },
            new() { Action = "journal",        Label = "J",  Win = journal, CloseByMethod = journal.Toggle },
            new() { Action = "help_hotkeys",   Label = "F1", Win = hotkeys, CloseByMethod = hotkeys.Close  },
            new() { Action = "techniques",     Label = "T",  Win = book,    CloseByMethod = book.Close     },
        };

        int fails = 0;

        // === A. Все пары: A открыта → B поверх → закрыть B (метод) →
        //         пауза держится → закрыть A (метод) → пауза снята. ======
        fails += await SuiteA();

        // === B. Все пары: A открыта → B поверх → закрыть A (метод, при
        //         живом B) → пауза держится → закрыть B (клавиша) → снята.
        fails += await SuiteB();

        // === C. Пауза игрока (Esc) переживает окна =======================
        fails += await SuiteC();

        // === D. Тройной стек =============================================
        fails += await SuiteD();

        GD.Print($"[ModalStackSim] VERDICT: {(fails == 0
            ? "PASS — 30×2 пар стеков: пауза держится до последнего окна и снимается с ним; Esc-пауза переживает окна; тройной стек чист"
            : $"FAIL — {fails} провалов (см. выше)")}");
    }

    /// <summary>A: нижнее держит паузу после закрытия верхнего; снятие — с последним.</summary>
    private async System.Threading.Tasks.Task<int> SuiteA()
    {
        int fails = 0;
        foreach (var bottom in _wins)
        foreach (var top in _wins)
        {
            if (bottom == top) continue;
            await OpenByKey(bottom);
            await OpenByKey(top);

            top.CloseByMethod(); // «×»/bg-click верхнего мимо GWC
            await WaitFramesAsync();
            bool holdsAfterTop = bottom.IsOpen() && _time!.IsPaused;

            bottom.CloseByMethod(); // нижнее — последний в стеке
            await WaitFramesAsync();
            bool resolved = !bottom.IsOpen() && !top.IsOpen() && !_time.IsPaused;

            if (!holdsAfterTop || !resolved)
            {
                fails++;
                GD.Print($"[ModalStackSim] A. FAIL пара [{bottom.Label}+{top.Label}]: " +
                          $"пауза после закрытия {top.Label} = {_time.IsPaused} (ожид. true), " +
                          $"резюм после закрытия {bottom.Label} = {!_time.IsPaused} (ожид. true)");
            }
            if (_time.IsPaused) _time.Resume(); // самовосстановление
        }
        GD.Print($"[ModalStackSim] A. 30 пар «верхнее закрыть первым»: fails={fails}");
        return fails;
    }

    /// <summary>B: закрытие НИЖНЕГО при живом верхнем не снимает паузу;
    /// верхнее клавишей — снимает (GWC-ветка + Closed, идемпотентно).</summary>
    private async System.Threading.Tasks.Task<int> SuiteB()
    {
        int fails = 0;
        foreach (var bottom in _wins)
        foreach (var top in _wins)
        {
            if (bottom == top) continue;
            await OpenByKey(bottom);
            await OpenByKey(top);

            bottom.CloseByMethod(); // нижнее под живым верхним
            await WaitFramesAsync();
            bool holdsAfterBottom = top.IsOpen() && _time!.IsPaused;

            await PressAndRelease(top.Action); // верхнее клавишей — последнее
            await WaitFramesAsync();
            bool resolved = !bottom.IsOpen() && !top.IsOpen() && !_time.IsPaused;

            if (!holdsAfterBottom || !resolved)
            {
                fails++;
                GD.Print($"[ModalStackSim] B. FAIL пара [{bottom.Label}+{top.Label}]: " +
                          $"пауза после закрытия {bottom.Label} (при живом {top.Label}) = {_time.IsPaused} (ожид. true), " +
                          $"резюм после закрытия {top.Label} клавишей = {!_time.IsPaused} (ожид. true)");
            }
            if (_time.IsPaused) _time.Resume();
        }
        GD.Print($"[ModalStackSim] B. 30 пар «нижнее закрыть первым»: fails={fails}");
        return fails;
    }

    /// <summary>C: Esc-пауза ставится до окон, НЕ снимается их закрытием,
    /// снимается повторным Esc (инвариант снапшота _wasPausedBefore*).</summary>
    private async System.Threading.Tasks.Task<int> SuiteC()
    {
        var b = _wins[0]; // инвентарь
        await PressAndRelease("pause"); // окна закрыты → GWC: Time.Pause()
        await WaitFramesAsync();
        bool escPaused = _time!.IsPaused;

        await OpenByKey(b); // снапшот = true (мир был запаузен вне окон)
        b.CloseByMethod();
        await WaitFramesAsync();
        bool survivesWindows = !b.IsOpen() && _time.IsPaused; // пауза жива

        await PressAndRelease("pause"); // AnyModal=false → GWC: Resume
        await WaitFramesAsync();
        bool escResumed = !_time.IsPaused;

        GD.Print($"[ModalStackSim] C. Esc-пауза: ставится={escPaused}, переживает окно={survivesWindows}, " +
                  $"снимается Esc={escResumed} (ожидаем True/True/True)");
        if (_time.IsPaused) _time.Resume();
        return (escPaused && survivesWindows && escResumed) ? 0 : 1;
    }

    /// <summary>D: тройной стек, два порядка закрытия.</summary>
    private async System.Threading.Tasks.Task<int> SuiteD()
    {
        int fails = 0;
        var inv = _wins[0]; var sheet = _wins[1]; var quest = _wins[2];

        // D1: правильный порядок — сверху вниз.
        await OpenByKey(inv); await OpenByKey(sheet); await OpenByKey(quest);
        sheet.CloseByMethod();
        await WaitFramesAsync();
        bool mid1 = _time!.IsPaused; // inv+quest держат
        inv.CloseByMethod();
        await WaitFramesAsync();
        bool mid2 = _time.IsPaused; // quest держит
        quest.CloseByMethod();
        await WaitFramesAsync();
        bool resolved1 = !_time.IsPaused;
        if (!mid1 || !mid2 || !resolved1) { fails++; GD.Print($"[ModalStackSim] D1. FAIL: mid1={mid1}, mid2={mid2}, resolved={resolved1}"); }
        if (_time.IsPaused) _time.Resume();

        // D2: «перемешанный» порядок — закрыть среднее, нижнее, верхнее.
        await OpenByKey(inv); await OpenByKey(sheet); await OpenByKey(quest);
        sheet.CloseByMethod();
        await WaitFramesAsync();
        inv.CloseByMethod();
        await WaitFramesAsync();
        quest.CloseByMethod(); // последнее
        await WaitFramesAsync();
        bool resolved2 = !_time.IsPaused;
        if (!resolved2) { fails++; GD.Print("[ModalStackSim] D2. FAIL: перемешанный порядок не снял паузу"); }
        if (_time.IsPaused) _time.Resume();

        GD.Print($"[ModalStackSim] D. тройной стек (2 порядка): fails={fails}");
        return fails;
    }

    private async System.Threading.Tasks.Task OpenByKey(ModalWin w)
    {
        await PressAndRelease(w.Action);
        await WaitFramesAsync();
        if (!w.IsOpen())
            GD.Print($"[ModalStackSim] WARN: {w.Label} не открылся клавишей (проверь InputAdapter/action)");
    }

    private async System.Threading.Tasks.Task PressAndRelease(string action)
    {
        // Godot.Input — квалификация обязательна (Adapter.Input конфликт).
        Godot.Input.ActionPress(action);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Godot.Input.ActionRelease(action);
    }

    private async System.Threading.Tasks.Task WaitFramesAsync()
    {
        for (int i = 0; i < WaitFrames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static GameWorldController? FindWorld(Node node)
    {
        if (node is GameWorldController world) return world;
        foreach (var child in node.GetChildren())
        {
            var found = FindWorld(child);
            if (found != null) return found;
        }
        return null;
    }
}
