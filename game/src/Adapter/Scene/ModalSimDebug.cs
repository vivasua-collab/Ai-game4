#nullable enable
// Аудит-0915 A8 (P2-3): INP-1-класс багов «закрытие окна мимо GWC оставляет
// мир замороженным» не покрывался ни одним из 15 симов регрессии — что
// позволило ×-кнопкам Q/J/F1/T уцелеть после фикса INP-1.
// Headless-верификация (GODOT_MODALQA_DEBUG=1) инварианта
// «пауза ⇔ (стек модальных окон непуст) ∨ (Esc игрока)»:
//   1. B-цикл: клавиша открыла (пауза) → bg-click-закрытие win.Toggle()
//      мимо GWC-ветки → Closed → резюм (сам INP-1 кейс).
//   2. C-цикл: то же для листа персонажа.
//   3. «×»-пути Q/J (аудит A2): клавиша открыла → закрытие методом,
//      который вызывает кнопка «×» (Toggle) → пауза снята.
//   4. «×»-пути F1/T (аудит A2): клавиша открыла → Close() кнопки → пауза снята.
//   5. Идемпотентность двойного резюма: закрытие клавишей (GWC-ветка +
//      Closed-событие) → пауза снята, без побочных эффектов.
//   6. Стек «инвентарь + лавка» (аудит A3): открытие лавки поверх
//      инвентаря НЕ перетирает снапшот «мир был на паузе до окон»;
//      закрытие лавки при живом инвентаре НЕ снимает паузу; закрытие
//      инвентаря — снимает. Прежний инвертированный предикат вешал
//      паузу навсегда.
// Эмуляция клавиш: Input.ActionPress/Release (работает headless);
// InputAdapter (polling в _PhysicsProcess) → stickyKeys → GWC-ветка.
// Запуск: GODOT_NEWGAME=1 GODOT_MODALQA_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Adapter.Scene;

public partial class ModalSimDebug : Node
{
    [Inject] private ITimeService? _time;
    [Inject] private IPublisher<TradeOpenedEvent>? _tradeOpenedPub;
    [Inject] private IPublisher<TradeClosedEvent>? _tradeClosedPub;

    private const int WaitFrames = 4; // InputAdapter→stickyKeys→GWC→ResetFrameFlags

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        GD.Print("[ModalSim] Ready — модальные окна: пауза/резюм тест старт через 2с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null || _time == null)
        {
            GD.Print("[ModalSim] VERDICT: FAIL — GameWorldController/ITimeService not found");
            return;
        }

        var inv = world.InventoryWindowForQA;
        var sheet = world.CharacterSheetWindowForQA;
        var quest = world.QuestWindowForQA;
        var journal = world.EventLogWindowForQA;
        var hotkeys = world.HotkeysWindowForQA;
        var book = world.TechniqueBookWindowForQA;
        var trade = world.TradeWindowForQA;
        if (inv == null || sheet == null || quest == null || journal == null
            || hotkeys == null || book == null || trade == null)
        {
            GD.Print("[ModalSim] VERDICT: FAIL — окна не найдены (DI/UIInitPhase)");
            return;
        }

        bool pass = true;

        // === 1. B: клавиша открыла → bg-click закрыл (INP-1 база) =========
        pass &= await CycleByX(world, "inventory", "B", inv,
            openByWindow: () => { }, closeByWindow: () => inv.Toggle(),
            openedCheck: () => inv.Visible, label: "1. B bg-click");

        // === 2. C: клавиша открыла → bg-click закрыл ======================
        pass &= await CycleByX(world, "character_sheet", "C", sheet,
            openByWindow: () => { }, closeByWindow: () => sheet.Toggle(),
            openedCheck: () => sheet.Visible, label: "2. C bg-click");

        // === 3. Q: клавиша открыла → «×» (Toggle кнопки) закрыл ===========
        pass &= await CycleByX(world, "quest_log", "Q", quest,
            openByWindow: () => { }, closeByWindow: () => quest.Toggle(),
            openedCheck: () => quest.Visible, label: "3. Q ×");

        // === 4. J: клавиша открыла → «×» (Toggle кнопки) закрыл ===========
        pass &= await CycleByX(world, "journal", "J", journal,
            openByWindow: () => { }, closeByWindow: () => journal.Toggle(),
            openedCheck: () => journal.Visible, label: "4. J ×");

        // === 5. F1: клавиша открыла → «×» (Close кнопки) закрыл ===========
        pass &= await CycleByX(world, "help_hotkeys", "F1", hotkeys,
            openByWindow: () => { }, closeByWindow: () => hotkeys.Close(),
            openedCheck: () => hotkeys.Visible, label: "5. F1 ×");

        // === 6. T: клавиша открыла → «×» (Close кнопки) закрыл ============
        pass &= await CycleByX(world, "techniques", "T", book,
            openByWindow: () => { }, closeByWindow: () => book.Close(),
            openedCheck: () => book.Visible, label: "6. T ×");

        // === 7. Идемпотентность: закрытие клавишей = ветка + Closed =======
        await PressAndRelease("inventory");
        await WaitFramesAsync();
        bool openedByKey = inv.Visible && _time.IsPaused;
        await PressAndRelease("inventory"); // закрытие клавишей: GWC-ветка + Closed → 2× резюм
        await WaitFramesAsync();
        bool closedByKey = !inv.Visible && !_time.IsPaused;
        GD.Print($"[ModalSim] 7. идемпотентность двойного резюма: opened={openedByKey}, closed+resumed={closedByKey} (ожидаем True/True)");
        pass &= openedByKey && closedByKey;
        if (_time.IsPaused) _time.Resume(); // самовосстановление на случай FAIL

        // === 8. Стек «инвентарь + лавка» (A3) ==============================
        await PressAndRelease("inventory");
        await WaitFramesAsync();
        bool invOpen = inv.Visible && _time.IsPaused;
        _tradeOpenedPub?.Publish(new TradeOpenedEvent("qa_merchant"));
        await WaitFramesAsync();
        bool tradeOpenOverInv = trade.IsOpen && _time.IsPaused;
        _tradeClosedPub?.Publish(new TradeClosedEvent());
        await WaitFramesAsync();
        bool tradeClosedInvAlive = !trade.IsOpen && inv.Visible && _time.IsPaused;
        inv.Toggle(); // bg-click-закрытие нижнего окна
        await WaitFramesAsync();
        bool stackResolved = !inv.Visible && !_time.IsPaused;
        GD.Print($"[ModalSim] 8. стек инвентарь+лавка: invOpen={invOpen}, tradeOverInv={tradeOpenOverInv}, " +
                  $"tradeClosedInvAlive(paused)={tradeClosedInvAlive}, stackResolved(unpaused)={stackResolved}");
        pass &= invOpen && tradeOpenOverInv && tradeClosedInvAlive && stackResolved;
        if (_time.IsPaused) _time.Resume();

        GD.Print($"[ModalSim] VERDICT: {(pass
            ? "PASS — «×»/bg-click всех 6 окон доходит до резюма; двойной резюм идемпотентен; стек инвентарь+лавка не вешает паузу"
            : "FAIL")}");
    }

    /// <summary>
    /// Цикл «клавиша открыла окно (GWC-ветка ставит паузу) → закрытие
    /// методом окна мимо GWC (как «×»/bg-click) → пауза обязана уйти через
    /// Closed-событие → HandleModalResumeOnClose».
    /// </summary>
    private async System.Threading.Tasks.Task<bool> CycleByX(
        GameWorldController world, string action, string keyName, Control win,
        System.Action openByWindow, System.Action closeByWindow,
        System.Func<bool> openedCheck, string label)
    {
        await PressAndRelease(action);
        await WaitFramesAsync();
        bool opened = openedCheck() && _time!.IsPaused;
        if (!opened)
        {
            GD.Print($"[ModalSim] {label}: окно {keyName} НЕ открылось/пауза НЕ встала — FAIL ветки");
            if (_time.IsPaused) _time.Resume();
            return false;
        }
        closeByWindow(); // путь «×»/bg-click: метод окна, НЕ GWC-ветка
        await WaitFramesAsync();
        bool closedAndResumed = !openedCheck() && !_time.IsPaused;
        GD.Print($"[ModalSim] {label}: открыто+пауза={opened}, закрыто+резюм={closedAndResumed} (ожидаем True/True)");
        if (_time.IsPaused) _time.Resume(); // самовосстановление
        return closedAndResumed;
    }

    private async System.Threading.Tasks.Task PressAndRelease(string action)
    {
        // Godot.Input — квалификация обязательна: голое `Input` внутри
        // CultivationGame.Adapter.* резолвится в namespace Adapter.Input.
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
