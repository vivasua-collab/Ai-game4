#nullable enable
// Создано: 2026-09-10 — R15 «Оружие в руках»: headless-верификация
// (GODOT_WEAPONVIS_DEBUG=1).
//
// Проверяет DoD фазы A+B (план R15 §7):
//   1. Генерация: 7 классов оружия → 7 корректных РАЗЛИЧНЫХ WeaponClassId.
//   2. Fallback: пустой WeaponClassId → парсинг ItemId (легаси R11);
//      нераспознанный формат → generic "sword" (не краш).
//   3. Спрайты: каталог Resolve 3 классов (sword/spear/bow) → icon 32×32,
//      hand 48×48, ключи различимы; 5 тиров ≠ друг друга.
//   4. Композит игрока: стартовый кинжал (StartingGearPhase) → рука видна,
//      ключ dagger|…; экип копья → ключ меняется; анэкип → рука скрыта;
//      повторный экип → восстановлена.
//   5. Хотбар: иконка в слоте 1 (ключ != null), реагирует на смену оружия.
//   6. NPC: часть NPC с оружием → overlay рисуется (ключи != null).
//   7. Facing: DEBUG_SetFacingLeft → PlayerFacingLeft (зеркалирование).
//
// Запуск: GODOT_NEWGAME=1 GODOT_WEAPONVIS_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн — LootSimDebug/HotbarSimDebug (env-хук, public QA-поля, VERDICT).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.UI;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация R15: WeaponClassId, спрайты icon/hand, композит
/// игрока (экип/анэкип/фейсинг), иконки хотбара, overlay NPC.
/// Итог: [WeaponVis] VERDICT: PASS/FAIL.
/// </summary>
public partial class WeaponVisSimDebug : Node
{
    [Inject] private IEquipmentService? _equipment;
    [Inject] private IEquipmentGenerator? _generator;
    [Inject] private INPCService? _npcService;

    private GameWorldController? _world;
    private HotbarPanel? _hotbar;
    private NPCSpriteRenderer? _npcRenderer;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[WeaponVis] diag: equip={_equipment != null}, gen={_generator != null}, npc={_npcService != null}");

        if (_equipment == null || _generator == null)
        {
            GD.Print("[WeaponVis] VERDICT: FAIL — DI injection failed");
            return;
        }

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        _hotbar = FindNode<HotbarPanel>(GetTree().Root);
        _npcRenderer = FindNode<NPCSpriteRenderer>(GetTree().Root);

        if (_world == null)
        {
            GD.Print("[WeaponVis] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        bool pass = true;

        // === 1. Генерация: 7 классов → 7 WeaponClassId =====================
        string[] classes = { "dagger", "sword", "axe", "spear", "greatsword", "bow", "staff" };
        var seen = new HashSet<string>();
        foreach (var c in classes)
        {
            var w = _generator.GenerateWeapon(3, c, 9100 + c.Length);
            string parsed = WeaponVisualCatalog.WeaponClassOf(w);
            seen.Add(parsed);
            if (parsed != c)
            {
                GD.Print($"[WeaponVis] step1 FAIL: class '{c}' → WeaponClassOf='{parsed}' (expected '{c}')");
                pass = false;
            }
        }
        GD.Print($"[WeaponVis] step1 generation: {seen.Count}/7 distinct WeaponClassId " +
                 $"(field: '{(_generator.GenerateWeapon(1, "spear", 77).WeaponClassId)}')");
        if (seen.Count != 7) pass = false;

        // === 2. Fallback-цепочка ===========================================
        var legacy = new EquipmentData
        {
            ItemId = "eq_wep_spear_L1_abcd_000001",
            Category = ItemCategory.Weapon,
        };
        string legacyClass = WeaponVisualCatalog.WeaponClassOf(legacy);
        var unknown = new EquipmentData
        {
            ItemId = "totally_unknown_format",
            Category = ItemCategory.Weapon,
        };
        string unknownClass = WeaponVisualCatalog.WeaponClassOf(unknown);
        GD.Print($"[WeaponVis] step2 fallback: legacy→'{legacyClass}' (expected spear), unknown→'{unknownClass}' (expected sword)");
        if (legacyClass != "spear") pass = false;
        if (unknownClass != "sword") pass = false;

        // === 3. Спрайты каталога ===========================================
        var swordV = WeaponVisualCatalog.GetOrCreate("sword", 1, ItemRarity.Common);
        var spearV = WeaponVisualCatalog.GetOrCreate("spear", 1, ItemRarity.Common);
        var bowV = WeaponVisualCatalog.GetOrCreate("bow", 2, ItemRarity.Common);
        var swordT5 = WeaponVisualCatalog.GetOrCreate("sword", 5, ItemRarity.Common);
        var swordLeg = WeaponVisualCatalog.GetOrCreate("sword", 5, ItemRarity.Legendary);
        bool sizes = swordV.Icon.GetSize().X == 32 && swordV.Hand.GetSize().X == 48
                     && spearV.Hand.GetSize().X == 48 && bowV.Icon.GetSize().X == 32;
        bool distinct = swordV.Key != spearV.Key && spearV.Key != bowV.Key
                        && swordV.Key != swordT5.Key && swordT5.Key != swordLeg.Key;
        GD.Print($"[WeaponVis] step3 catalog: sizes(32/48)={sizes}, distinctKeys={distinct} " +
                 $"(sword='{swordV.Key}', spear='{spearV.Key}', bow='{bowV.Key}', " +
                 $"T5='{swordT5.Key}', leg='{swordLeg.Key}', cache={WeaponVisualCatalog.CachedEntryCount})");
        if (!sizes || !distinct) pass = false;

        // === 4. Композит игрока ============================================
        // StartingGearPhase экипирует кинжал L1 в WeaponMain.
        string? initialKey = _world.MainHandTextureId;
        bool initialVisible = _world.MainHandVisible;
        GD.Print($"[WeaponVis] step4 initial: visible={initialVisible}, key='{initialKey}' (expected dagger|1|Common)");
        if (!initialVisible || initialKey == null || !initialKey.StartsWith("dagger|")) pass = false;

        // Экип копья (2H) → ключ меняется.
        var spear = _generator.GenerateWeapon(2, "spear", 9201);
        bool equipped = _equipment.TryEquip(EquipmentSlot.WeaponMain, spear);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        string? spearKey = _world.MainHandTextureId;
        GD.Print($"[WeaponVis] step4b equip spear: ok={equipped}, key='{spearKey}' (expected spear|…)");
        if (!equipped || spearKey == null || !spearKey.StartsWith("spear|")) pass = false;

        // Анэкип → рука скрыта.
        bool unequipped = _equipment.TryUnequip(EquipmentSlot.WeaponMain, out _);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        bool hidden = !_world.MainHandVisible && _world.MainHandTextureId == null;
        GD.Print($"[WeaponVis] step4c unequip: ok={unequipped}, visible={_world.MainHandVisible}, key='{_world.MainHandTextureId}' (expected hidden)");
        if (!unequipped || !hidden) pass = false;

        // Повторный экип меча → рука восстановлена (item возвращён в инвентарь;
        // экипируем свежесгенерированный).
        var sword = _generator.GenerateWeapon(2, "sword", 9202);
        _equipment.TryEquip(EquipmentSlot.WeaponMain, sword);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        string? swordKey = _world.MainHandTextureId;
        GD.Print($"[WeaponVis] step4d re-equip sword: visible={_world.MainHandVisible}, key='{swordKey}' (expected sword|…)");
        if (!_world.MainHandVisible || swordKey == null || !swordKey.StartsWith("sword|")) pass = false;

        // === 5. Хотбар: иконки слотов 1-2 ==================================
        if (_hotbar != null)
        {
            string? icon1 = _hotbar.WeaponIconTextureId(1);
            string? icon2 = _hotbar.WeaponIconTextureId(2);
            GD.Print($"[WeaponVis] step5 hotbar: icon1='{icon1}', icon2='{icon2}' (expected sword|…, null)");
            if (icon1 == null || !icon1.StartsWith("sword|")) pass = false;
            if (icon2 != null) pass = false;
        }
        else
        {
            GD.Print("[WeaponVis] step5 hotbar: FAIL — HotbarPanel not found");
            pass = false;
        }

        // === 6. NPC: overlay оружия ========================================
        if (_npcRenderer != null && _npcService != null)
        {
            // Дать рендеру отработать перескан (0.5с) + кадр отрисовки.
            await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
            int withWeapon = _npcRenderer.NPCWeaponVisibleCount;
            string? sampleKey = null;
            foreach (var id in _npcService.GetAllNPCIds())
            {
                sampleKey = _npcRenderer.NPCWeaponTextureIdOf(id);
                if (sampleKey != null) break;
            }
            GD.Print($"[WeaponVis] step6 npc: {withWeapon} NPC with weapon overlay, sample='{sampleKey}'");
            if (withWeapon < 1 || sampleKey == null) pass = false;
        }
        else
        {
            GD.Print("[WeaponVis] step6 npc: FAIL — NPCSpriteRenderer not found");
            pass = false;
        }

        // === 7. Facing (зеркалирование) ====================================
        _world.DEBUG_SetFacingLeft(true);
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        bool facingLeft = _world.PlayerFacingLeft;
        _world.DEBUG_SetFacingLeft(false);
        GD.Print($"[WeaponVis] step7 facing: left={facingLeft} (expected true after set)");
        if (!facingLeft) pass = false;

        GD.Print($"[WeaponVis] VERDICT: {(pass
            ? "PASS — classId/fallback/sprites/composite/hotbar/npc/facing работают"
            : "FAIL")}");
    }

    private static T? FindNode<T>(Node node) where T : Node
    {
        if (node is T typed) return typed;
        foreach (var child in node.GetChildren())
        {
            var found = FindNode<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static GameWorldController? FindWorld(Node node) => FindNode<GameWorldController>(node);
}
