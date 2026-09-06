#nullable enable
// Создано: 2026-09-06 — S6: headless-верификация UX окна диалога
// (GODOT_DIALOGUE_DEBUG=1).
//
// Проверяет S6-фиксы DialogueWindow:
//   1. Окно открывается через QA-путь контроллера; имя NPC ≠ «???».
//   2. Подсказка честная: упоминает цифры, E и Esc.
//   3. Панель имеет ширину ≈900 (фикс BottomWide-бага) и варианты
//      укладываются в панель по вертикали (фикс переполнения).
//   4. Индикатор «▼» виден пока текст печатается и гаснет по завершении;
//      клик-Advance() (тот же путь, что E) досрочно допечатывает текст.
//   5. Select(0) ведёт на следующий узел (диалог жив, текст меняется).
//   6. Close() закрывает окно и завершает диалог в сервисе.
//
// GODOT_DIALOGUE_HOLD=1 — не закрывать окно после проверок (скриншоты).
// Запуск: GODOT_NEWGAME=1 GODOT_DIALOGUE_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация UX диалогового окна. Итог: [DialogueSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class DialogueSimDebug : Node
{
    [Inject] private INPCService? _npcService;

    private GameWorldController? _world;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[DialogueSim] diag: npc={_npcService != null}");

        if (_npcService == null)
        {
            GD.Print("[DialogueSim] VERDICT: FAIL — services not injected");
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (_world == null)
        {
            GD.Print("[DialogueSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        // === 1. Найти NPC с диалогом и открыть окно (QA-путь контроллера) ===
        string? dlgNpc = null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (_world.DialogueDebugOpen(id)) { dlgNpc = id; break; }
        }
        if (dlgNpc == null)
        {
            GD.Print("[DialogueSim] VERDICT: FAIL — ни один NPC не имеет диалога");
            return;
        }

        var win = _world.DialogueWindowForQA;
        if (win == null)
        {
            GD.Print("[DialogueSim] VERDICT: FAIL — DialogueWindow не найдена");
            return;
        }

        bool pass = true;

        bool opened = win.IsOpen;
        string npcName = win.NpcNameText;
        GD.Print($"[DialogueSim] step1 open: npc={dlgNpc}, window={opened}, name='{npcName}' (expected opened=True, name≠'??')");
        if (!opened || string.IsNullOrEmpty(npcName) || npcName == "???") pass = false;

        // === 2. Честная подсказка ==========================================
        string hint = win.HintText;
        bool hintOk = hint.Contains("1") && hint.Contains("E") && hint.Contains("Esc");
        GD.Print($"[DialogueSim] step2 hint: '{hint}' (expected цифры+E+Esc)");
        if (!hintOk) pass = false;

        // === 3. Геометрия: ширина ≈900, варианты влезают ===================
        float w = win.PanelWidthActual;
        float h = win.PanelHeightActual;
        bool fit = win.ChoicesFitPanel;
        int choices = win.ChoiceButtonCount;
        GD.Print($"[DialogueSim] step3 geometry: width={w:F0} (≈900), height={h:F0} (≥190), choices={choices}, fit={fit}");
        if (Mathf.Abs(w - 900f) > 2f || h < 190f || !fit || choices < 1) pass = false;

        // === 4. Индикатор печати + клик-Advance ============================
        // Даём окну кадры на _Process (индикатор обновляется там).
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bool typing0 = win.TypingIndicatorVisible;
        // Клик по панели = Advance (тот же public-путь, что и клавиша E):
        // первый вызов досрочно допечатывает текст.
        win.Advance();
        await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
        bool typing1 = win.TypingIndicatorVisible;
        bool fullShown = win.BodyText == win.FullTextForQA;
        GD.Print($"[DialogueSim] step4 typing: before={typing0}, afterAdvance={typing1}, fullShown={fullShown} (expected True→False, full=True)");
        if (!typing0 || typing1 || !fullShown) pass = false;

        // === 5. Выбор варианта 1 → следующий узел ==========================
        string textBefore = win.BodyText;
        win.Select(0);
        // После смены узла typewriter перезапущен — текст снова печатается.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        bool stillOpen = win.IsOpen;
        string textAfter = win.BodyText;
        bool textFlowing = textAfter.Length > 0 && textAfter != textBefore;
        GD.Print($"[DialogueSim] step5 select: open={stillOpen}, textFlowing={textFlowing} ('{textBefore[..System.Math.Min(20, textBefore.Length)]}…' → '{textAfter[..System.Math.Min(20, textAfter.Length)]}…')");
        if (!stillOpen || !textFlowing) pass = false;

        // === 6. Закрытие (или удержание для скриншота) =====================
        if (System.Environment.GetEnvironmentVariable("GODOT_DIALOGUE_HOLD") == "1")
        {
            // VLM-скриншот: ждём ~2с печати текста (индикатор ▼ виден).
            string? shot = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT");
            if (!string.IsNullOrEmpty(shot))
            {
                await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng(shot);
                GD.Print($"[DialogueSim] screenshot: {shot}");
            }
            GD.Print("[DialogueSim] HOLD: окно остаётся открытым (скриншот)");
            GD.Print($"[DialogueSim] VERDICT: {(pass ? "PASS — подсказка/геометрия/индикатор/выбор" : "FAIL")}");
            return;
        }

        win.Close();
        bool openImmediate = win.IsOpen;
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        bool openAfter = win.IsOpen;
        int visibleWindows = CountVisibleDialogueWindows();
        GD.Print($"[DialogueSim] step6 close: immediate={openImmediate}, after0.1s={openAfter}, visibleWindows={visibleWindows} (expected False/False/0)");
        if (openImmediate || openAfter || visibleWindows != 0) pass = false;

        GD.Print($"[DialogueSim] VERDICT: {(pass ? "PASS — подсказка честная, геометрия ок, индикатор печати, выбор узла, закрытие" : "FAIL")}");
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

    /// <summary>Число видимых DialogueWindow в дереве (диагностика переоткрытия).</summary>
    private int CountVisibleDialogueWindows()
    {
        int n = 0;
        CountRecursive(GetTree().Root, ref n);
        return n;
    }

    private static void CountRecursive(Node node, ref int n)
    {
        if (node is UI.DialogueWindow { Visible: true }) n++;
        foreach (var child in node.GetChildren())
            CountRecursive(child, ref n);
    }
}
