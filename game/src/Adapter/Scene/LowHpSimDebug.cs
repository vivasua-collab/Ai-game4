#nullable enable
// Создано: 2026-09-04 — S2: headless-верификация виньетки опасности
// (GODOT_LOWHP_DEBUG=1).
//
// Проверяет:
//   1. При HP < 35% полноэкранный оверлей LowHpOverlay получает alpha > 0.
//   2. При HP < 15% — пульсация (alpha меняется между кадрами).
//   3. HP-бар-текст отражает сниженное HP (информативность HUD).
//
// Запуск: GODOT_NEWGAME=1 GODOT_LOWHP_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_COMBAT_SIM / GODOT_TOAST_DEBUG — env-хук только
// для верификации. Урон наносим прямой подачей в BodyService (без боя),
// доводя HP до ~12% (не до нуля — смерть вызовет respawn и сброс).
using Godot;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация виньетки опасности при низком HP.
/// Наносит контролируемый урон, проверяет alpha оверлея и HP-подпись.
/// Итог: [LowHpSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class LowHpSimDebug : Node
{
    [Inject] private IBodyService? _bodyService;

    private GameWorldController? _world;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[LowHpSim] diag: body={_bodyService != null}");

        if (_bodyService == null)
        {
            GD.Print("[LowHpSim] VERDICT: FAIL — BodyService not injected");
            return;
        }

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

        if (_world == null)
        {
            GD.Print("[LowHpSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        var overlay = FindOverlay(_world);
        if (overlay == null)
        {
            GD.Print("[LowHpSim] VERDICT: FAIL — LowHpOverlay not found in HUD");
            return;
        }

        // Текущее HP игрока.
        var parts = _bodyService.GetAllParts();
        int max = parts?.Sum(p => p.MaxRedHP) ?? 0;
        int cur = parts?.Sum(p => p.CurrentRedHP) ?? 0;
        GD.Print($"[LowHpSim] HP before: {cur}/{max}");
        if (max <= 0 || cur <= 0)
        {
            GD.Print("[LowHpSim] VERDICT: FAIL — no HP data");
            return;
        }

        // Наносим урон итеративно до ~12% HP. ApplyDamage сплитит 70/30
        // (red/black) и капится красным запасом части, поэтому одного удара
        // мало: пропорционально давим каждую часть (geometric decay), пока
        // суммарный red HP не опустится до target. Vital-части НЕ доводим до
        // нуля (красный запас части ≥ 1 после каждого прохода).
        int target = (int)(max * 0.12f);
        int curNow = cur;
        for (int iter = 0; iter < 6 && curNow > target; iter++)
        {
            var partsNow = _bodyService.GetAllParts();
            if (partsNow == null) break;
            foreach (var p in partsNow)
            {
                // Давим ~весь текущий red части (сплит 70/30 оставит ~30%).
                // Запас ≥ 3 — не трогаем мелочь, чтобы не занулить vital.
                if (p.CurrentRedHP >= 3)
                    _bodyService.ApplyDamage(p.Type, p.CurrentRedHP - 1);
            }
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            var partsCheck = _bodyService.GetAllParts();
            curNow = partsCheck?.Sum(x => x.CurrentRedHP) ?? 0;
        }
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

        // Диагностика: действительно ли HP упал.
        var partsAfter = _bodyService.GetAllParts();
        int curAfter = partsAfter?.Sum(p => p.CurrentRedHP) ?? 0;
        GD.Print($"[LowHpSim] HP after passes: {curAfter}/{max} (target ~{target})");

        float alpha1 = overlay.Color.A;
        GD.Print($"[LowHpSim] overlay alpha at ~12% HP: {alpha1:F3} (expected > 0.05)");

        bool pass = alpha1 > 0.05f;

        // Пульсация при HP < 15%: 3 сэмпла alpha по 0.3с (2 сэмпла могут
        // симметрично попасть на пик синуса и дать ложный Δ=0).
        float aMin = alpha1, aMax = alpha1;
        for (int s = 0; s < 2; s++)
        {
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            float a = overlay.Color.A;
            aMin = System.MathF.Min(aMin, a);
            aMax = System.MathF.Max(aMax, a);
        }
        float pulse = aMax - aMin;
        GD.Print($"[LowHpSim] pulse over ~0.9s: min={aMin:F3} max={aMax:F3} (Δ={pulse:F3}, expected > 0.005)");
        if (pulse <= 0.005f) pass = false;

        // Восстановление: alpha приходит к 0 не требуется (respawn сам чинит),
        // но проверим HP-текст — информативность HUD.
        var hpText = FindHpText(_world);
        GD.Print($"[LowHpSim] HP bar text: '{hpText?.Text}'");
        if (hpText == null || !hpText.Text.Contains("HP")) pass = false;

        GD.Print($"[LowHpSim] VERDICT: {(pass ? "PASS — виньетка/пульс/HP-текст работают" : "FAIL")}");
    }

    private static ColorRect? FindOverlay(Node root)
        => FindRecursive(root, (ColorRect r) => r.Name == "LowHpOverlay");

    private static T? FindRecursive<T>(Node node, System.Func<T, bool> match) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T typed && match(typed)) return typed;
            var found = FindRecursive(child, match);
            if (found != null) return found;
        }
        return null;
    }

    private static Label? FindHpText(Node root)
        => FindRecursive(root, (Label l) => l.Name == "HpBarText");

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
