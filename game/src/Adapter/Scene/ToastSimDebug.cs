#nullable enable
// Создано: 2026-09-04 — S2: headless-верификация тост-стека (GODOT_TOAST_DEBUG=1).
//
// Проверяет три инварианта стека (замена одного Label, где новое сообщение
// затирало предыдущее — терялась информация для игрока):
//   1. НЕзатирание: 3 разных сообщения → 3 строки одновременно.
//   2. Агрегация: повтор того же текста → счётчик «×N», строка НЕ дублируется.
//   3. Кап: 5 максимум видимых строк (старейшая вытесняется).
//   4. TTL: строки исчезают по истечении длительности.
//
// Запуск: GODOT_NEWGAME=1 GODOT_TOAST_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_COMBAT_SIM / GODOT_CHARGE_SIM — env-хук только для
// верификации, игровой код не затрагивает (кроме public QA-свойств контроллера).
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация тост-стека GameWorldController.
/// Публикует ToastShownEvent-серии, проверяет состояние стека через
/// public QA-свойства (ToastLineCount / LastToastText).
/// Итог: [ToastSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class ToastSimDebug : Node
{
    [Inject] private IPublisher<Core.Messaging.Contracts.ToastShownEvent>? _toastPub;

    private GameWorldController? _world;

    private const float TestDuration = 1.2f; // короткая длительность для теста

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[ToastSim] diag: pub={_toastPub != null}");

        if (_toastPub == null)
        {
            GD.Print("[ToastSim] VERDICT: FAIL — publisher not injected");
            return;
        }

        // Ждём кадр — GameWorldController._Ready уже построил _toastStack
        // (мы добавлены как его ребёнок ПОСЛЕ построения HUD).
        _world = GetParent() as GameWorldController;
        if (_world == null)
        {
            // Fallback: поиск по дереву.
            _world = FindWorld(GetTree().Root);
        }
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

        if (_world == null)
        {
            GD.Print("[ToastSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        bool pass = true;

        // === 1. НЕзатирание: 3 разных сообщения → 3 строки ==================
        Publish("Тост-А: добыча");
        Publish("Тост-Б: вес");
        Publish("Тост-В: техника");
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        int c1 = _world.ToastLineCount;
        GD.Print($"[ToastSim] step1 no-overwrite: lines={c1} (expected 3)");
        if (c1 != 3) pass = false;

        // === 2. Агрегация: тот же текст ×2 → счётчик ×3, строк не прибавилось =
        Publish("Тост-В: техника");
        Publish("Тост-В: техника");
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        int c2 = _world.ToastLineCount;
        string? last = _world.LastToastText;
        GD.Print($"[ToastSim] step2 aggregate: lines={c2} (expected 3), last='{last}'");
        if (c2 != 3) pass = false;
        if (last == null || !last.Contains("×3")) pass = false;

        // === 3. Кап стека: 4 новых → всего не больше 5 ======================
        Publish("Тост-Г");
        Publish("Тост-Д");
        Publish("Тост-Е");
        Publish("Тост-Ж");
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        int c3 = _world.ToastLineCount;
        GD.Print($"[ToastSim] step3 cap: lines={c3} (expected 5)");
        if (c3 != 5) pass = false;

        // === 4. TTL: после TestDuration+запас тестовые строки исчезают =====
        // (считаем только свои «Тост-» строки: комбинированные прогоны с
        // COMBAT_SIM параллельно публикуют боевые тосты — не наша утечка).
        await ToSignal(GetTree().CreateTimer(TestDuration + 1.0), SceneTreeTimer.SignalName.Timeout);
        int c4 = _world.ToastLinesWithPrefix("Тост");
        GD.Print($"[ToastSim] step4 ttl: test lines={c4} (expected 0; total={_world.ToastLineCount})");
        if (c4 != 0) pass = false;

        GD.Print($"[ToastSim] VERDICT: {(pass ? "PASS — стек/агрегация/кап/TTL работают" : "FAIL")}");
    }

    private void Publish(string message)
        => _toastPub!.Publish(new Core.Messaging.Contracts.ToastShownEvent(message, TestDuration));

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
