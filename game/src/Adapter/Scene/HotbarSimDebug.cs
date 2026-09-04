#nullable enable
// Создано: 2026-09-04 — S4: headless-верификация хотбара v2 (GODOT_HOTBAR_DEBUG=1).
//
// Проверяет инварианты новой панели:
//   1. Назначение: техника в слоте 3 → слот показывает её имя (не «—»).
//   2. Пустой слот: слот 4 без техники → «—».
//   3. Кулдаун-виз: DEBUG_SetCooldown → цифры секунд видны, ratio > 0.
//   4. Кулдаун тикает: через 1с label уменьшился, ratio упал.
//   5. Qi-гейт: спуск Ци до нуля → TechQiInsufficient = true.
//   6. Пояс-ряд: видимость = IsBeltEquipped (гейт консистентен).
//
// Запуск: GODOT_NEWGAME=1 GODOT_HOTBAR_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_TOAST_DEBUG — env-хук только для верификации.
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Player;
using CultivationGame.Modules.Inventory;
using CultivationGame.Adapter.UI;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация HotbarPanel v2 (техники + кулдауны + пояс-ряд).
/// Проверяет состояние панели через public QA-API (TechSlotName /
/// TechCooldownRatio / TechCooldownLabel / TechQiInsufficient / BeltRowVisible).
/// Итог: [HotbarSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class HotbarSimDebug : Node
{
    [Inject] private TechniqueService? _techniques;
    [Inject] private TechniqueSlotService? _techSlots;
    [Inject] private Core.Interfaces.IQiService? _qi;
    [Inject] private BeltService? _belt;

    private HotbarPanel? _hotbar;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[HotbarSim] diag: tech={_techniques != null}, slots={_techSlots != null}, qi={_qi != null}, belt={_belt != null}");

        if (_techniques == null || _techSlots == null || _qi == null || _belt == null)
        {
            GD.Print("[HotbarSim] VERDICT: FAIL — services not injected");
            return;
        }

        // Ждём кадры — GameWorldController._Ready построил HUD (HotbarPanel).
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        _hotbar = FindHotbar(GetTree().Root);

        if (_hotbar == null)
        {
            GD.Print("[HotbarSim] VERDICT: FAIL — HotbarPanel not found in tree");
            return;
        }

        bool pass = true;

        // Техника для теста: первая изученная с QiCost > 0 (гейт Ци проверяем
        // только на платной; бесплатная техника всегда доступна — это корректно).
        // NEWGAME-генерация даёт игроку стартовые техники.
        var ordered = _techniques.GetOrderedIds();
        if (ordered == null || ordered.Count == 0)
        {
            GD.Print("[HotbarSim] VERDICT: FAIL — no learned techniques (need GODOT_NEWGAME=1)");
            return;
        }
        string techId = ordered[0];
        foreach (var id in ordered)
        {
            var t = _techniques.GetTechnique(id);
            if (t != null && t.QiCost > 0) { techId = id; break; }
        }
        var tech = _techniques.GetTechnique(techId);
        GD.Print($"[HotbarSim] diag: tech='{tech?.Name}' qiCost={tech?.QiCost} cooldown={tech?.Cooldown}");

        // === 1. Назначение в слот 3 =====================================
        bool assigned = _techSlots.AssignSlot(3, techId);
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        string? name3 = _hotbar.TechSlotName(3);
        GD.Print($"[HotbarSim] step1 assign: slot3='{name3}' (assigned={assigned}, expected non-'—' with name)");
        if (!assigned || name3 == null || name3 == "—" || name3 == "3")
            pass = false;

        // === 2. Пустой слот 4 ============================================
        string? name4 = _hotbar.TechSlotName(4);
        GD.Print($"[HotbarSim] step2 empty: slot4='{name4}' (expected '—')");
        if (name4 != "—") pass = false;

        // === 3. Кулдаун-визуализация =====================================
        // Ставим кулдаун = полный кулдаун техники → ratio ≈ 1.0.
        float totalCd = tech?.Cooldown > 0f ? tech!.Cooldown : 5.0f;
        _techniques.DEBUG_SetCooldown(techId, totalCd);
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        float ratio1 = _hotbar.TechCooldownRatio(3);
        string? cdLabel1 = _hotbar.TechCooldownLabel(3);
        GD.Print($"[HotbarSim] step3 cooldown-on: ratio={ratio1:F2} label='{cdLabel1}' (expected ratio>0.5, label non-empty)");
        if (ratio1 <= 0.5f || string.IsNullOrEmpty(cdLabel1)) pass = false;

        // === 4. Кулдаун тикает ===========================================
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        float ratio2 = _hotbar.TechCooldownRatio(3);
        string? cdLabel2 = _hotbar.TechCooldownLabel(3);
        GD.Print($"[HotbarSim] step4 cooldown-tick: ratio={ratio2:F2} label='{cdLabel2}' (expected < step3 ratio, label non-empty)");
        if (ratio2 >= ratio1 || string.IsNullOrEmpty(cdLabel2)) pass = false;

        // Снимаем кулдаун.
        _techniques.DEBUG_SetCooldown(techId, 0f);
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        string? cdLabel3 = _hotbar.TechCooldownLabel(3);
        GD.Print($"[HotbarSim] step4b cooldown-off: label='{cdLabel3}' (expected empty)");
        if (!string.IsNullOrEmpty(cdLabel3)) pass = false;

        // === 5. Qi-гейт ==================================================
        long savedQi = _qi.CurrentQi;
        if ((tech?.QiCost ?? 0) > 0)
        {
            _qi.TryConsumeQi(savedQi);
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            bool insuff = _hotbar.TechQiInsufficient(3);
            GD.Print($"[HotbarSim] step5 qi-gate: insufficient={insuff} (expected true; qi={_qi.CurrentQi}, cost={tech?.QiCost})");
            if (!insuff) pass = false;
            _qi.AddQi(savedQi);
        }
        else
        {
            GD.Print("[HotbarSim] step5 qi-gate: SKIP — бесплатная техника (QiCost=0, всегда доступна)");
        }

        // === 6. Пояс-ряд: гейт видимости =================================
        bool beltOn = _belt.IsBeltEquipped;
        bool rowVisible = _hotbar.BeltRowVisible;
        GD.Print($"[HotbarSim] step6 belt-gate: equipped={beltOn}, rowVisible={rowVisible} (expected equal)");
        if (beltOn != rowVisible) pass = false;
        if (beltOn)
        {
            var slots = _belt.GetSlots();
            GD.Print($"[HotbarSim] diag: belt slots: {slots?.Count ?? 0}, slot0='{(slots != null && slots.Count > 0 ? _hotbar.BeltSlotText(0) : "n/a")}'");
        }

        // Чистим слот за собой.
        _techSlots.ClearSlot(3);

        GD.Print($"[HotbarSim] VERDICT: {(pass ? "PASS — assign/empty/cooldown/qi-gate/belt-gate работают" : "FAIL")}");
    }

    private static HotbarPanel? FindHotbar(Node node)
    {
        if (node is HotbarPanel panel) return panel;
        foreach (var child in node.GetChildren())
        {
            var found = FindHotbar(child);
            if (found != null) return found;
        }
        return null;
    }
}
