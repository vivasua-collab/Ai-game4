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
//   7. R41-8: drag&drop техники из Книги (T) в слот 5 — форс-дроп
//      паттерном TrashDropSimDebug (прямой вызов _CanDropData/_DropData
//      приёмника с payload чипа, БЕЗ SetDragPreview — вне GUI-drag это
//      engine-error); негатив — техника Культивации/неизученная не
//      назначается, слот остаётся пустым.
//   8. R41-10: тайл-инспектор (F4) — послойный текст BuildText строится
//      без исключений и содержит слои (тайл/биом/Ци локации); сам оверлей
//      по умолчанию скрыт и не-модален (Visible=false, QA-нейтрален).
//
// Запуск: GODOT_NEWGAME=1 GODOT_HOTBAR_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_TOAST_DEBUG — env-хук только для верификации.
using Godot;
using System;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
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
    // R41-8: событие назначения слота — ассерт, что форс-дроп дошёл до
    // TechniqueSlotService.AssignSlot и опубликовал TechniqueSlotAssignedEvent
    // (реальный UI обновляется именно по нему).
    [Inject] private ISubscriber<TechniqueSlotAssignedEvent>? _slotAssignedSub;

    private HotbarPanel? _hotbar;
    private IDisposable? _slotAssignedToken;
    private bool _assignedEventSeen;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _slotAssignedToken = _slotAssignedSub?.Subscribe((in TechniqueSlotAssignedEvent e) =>
        {
            if (e.SlotIndex == 5) _assignedEventSeen = true; // R41-8: ждём слот 5
        });

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

        // === 7. R41-8: drag&drop техники из Книги (T) в слот 5 ============
        // Репорт владельца 18.10 №8: «реализовать перетаскивание техник в слот
        // быстрого доступа; только нажатие на кнопки слота — не интуитивно».
        // Книга = FullRect-оверлей → хотбар под ней → дроп-цель — слот-бар
        // самой Книги. Форс-дроп: payload чипа + прямой вызов методов
        // приёмника (SetDragPreview в форс-дропе НЕ вызывается — паттерн
        // TrashDropSimDebug :11-13).
        {
            var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
            var book = world?.TechniqueBookWindowForQA;
            if (book == null)
            {
                GD.Print("[HotbarSim] step7 book-drag: FAIL — TechniqueBookWindow не найдена");
                pass = false;
            }
            else
            {
                book.Open();
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

                bool hasChip = book.HasChipForQA(techId);
                GD.Print($"[HotbarSim] step7a chip: техника '{tech?.Name}' имеет чип-источник drag&drop = {hasChip} (ожидаем True)");
                pass &= hasChip;

                Variant dragData = TechniqueBookWindow.CreateTechniqueDragData(techId);
                bool canDrop = book.CanDropTechniqueForQA(5, dragData);
                GD.Print($"[HotbarSim] step7b CanDropData(слот 5): {canDrop} (ожидаем True — изучена, не-Cultivation)");
                pass &= canDrop;

                _assignedEventSeen = false;
                book.DropTechniqueForQA(5, dragData);
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                string? slot5 = _techSlots.GetTechniqueAtSlot(5);
                bool assignedByDrag = slot5 == techId && _assignedEventSeen;
                GD.Print($"[HotbarSim] step7c DropData: слот5='{slot5}' (ожидаем '{techId}'), TechniqueSlotAssignedEvent={_assignedEventSeen} (ожидаем True)");
                pass &= assignedByDrag;

                // Негатив: техника Культивации (☯ медитация, П5) в номерный
                // слот не назначается — приёмник отвергает ДО сервиса. Если
                // культ-техники в библиотеке нет (нестандартный сид) — негатив
                // на НЕИЗУЧЕННОМ id (тот же отказ по другой причине).
                string? negativeId = ordered.FirstOrDefault(id =>
                    _techniques.GetTechnique(id) is { Type: TechniqueType.Cultivation });
                string negativeReason = negativeId != null ? "Cultivation-техника" : "неизученная техника";
                negativeId ??= "qa_not_learned_technique";
                Variant negativeDrag = TechniqueBookWindow.CreateTechniqueDragData(negativeId);
                bool negativeCanDrop = book.CanDropTechniqueForQA(6, negativeDrag);
                book.DropTechniqueForQA(6, negativeDrag);
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                bool slot6Empty = string.IsNullOrEmpty(_techSlots.GetTechniqueAtSlot(6));
                bool negativeRejected = !negativeCanDrop && slot6Empty;
                GD.Print($"[HotbarSim] step7d негатив ({negativeReason} '{negativeId}'): CanDrop={negativeCanDrop}, слот6 пуст={slot6Empty} (ожидаем False/True — отказ с тостом-причиной)");
                pass &= negativeRejected;

                // Чистим за собой (слот 5 + закрыть Книгу).
                _techSlots.ClearSlot(5);
                book.Close();
            }
        }

        // === 8. R41-10: тайл-инспектор — послойный текст без исключений ===
        // Оверлей строится в SetupHUD GWC (после PerfOverlay), виден только
        // по F4; QA-акцессор дергает расчёт текста напрямую (мышь headless
        // стоит в (0,0) — тайл может быть Void/OOB, это тоже валидный слой).
        {
            var inspector = FindTileInspector(GetTree().Root);
            if (inspector == null)
            {
                GD.Print("[HotbarSim] step8 tile-inspector: FAIL — TileInspectorOverlay не найден в дереве");
                pass = false;
            }
            else
            {
                bool hiddenByDefault = !inspector.Visible;
                string text = inspector.BuildTextForQA();
                bool layersOk = text.Contains("Тайл (") && text.Contains("Биом:")
                    && text.Contains("Ци локации") && text.Contains("Сущности на тайле");
                GD.Print($"[HotbarSim] step8 tile-inspector (R41-10): скрыт-по-умолчанию={hiddenByDefault}, " +
                    $"слои(тайл/биом/Ци/сущности)={layersOk}, длина={text.Length}");
                GD.Print($"[HotbarSim] step8 tile-inspector текст (первые 2 строки): " +
                    $"\n{string.Join("\n", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(2))}");
                pass &= hiddenByDefault && layersOk && text.Length > 50;
            }
        }

        // Чистим слот за собой.
        _techSlots.ClearSlot(3);

        _slotAssignedToken?.Dispose();

        GD.Print($"[HotbarSim] VERDICT: {(pass ? "PASS — assign/empty/cooldown/qi-gate/belt-gate/drag&drop-слота/тайл-инспектор-работают" : "FAIL")}");
    }

    private static TileInspectorOverlay? FindTileInspector(Node node)
    {
        if (node is TileInspectorOverlay overlay) return overlay;
        foreach (var child in node.GetChildren())
        {
            var found = FindTileInspector(child);
            if (found != null) return found;
        }
        return null;
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
