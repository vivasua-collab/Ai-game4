#nullable enable
// Создано: 2026-10-02 — R38 (баг-репорт 02.10 №3/№5/№6): визуальные пруфы
// UI-окон после фиксов раунда.
//
// GODOT_UI_SHOT=prefix (Xvfb + opengl3) — скриншоты:
//   prefix_inventory.png  — инвентарь с доспавненными ЛЕГЕНДАРКАМИ
//                           (баг №3: контраст rarity-цветов на пергаменте);
//   prefix_technique.png  — книга техник (баг №6: блоки по типам без
//                           наложения, скролл);
//   prefix_character.png  — лист персонажа, вкладка «Характеристики»
//                           (баг №5: полные статы).
//
// Паттерн FormationShotSimDebug (R30): инъекция контейнера, доступ к окнам
// через *ForQA-геттеры GWC, Snap() через viewport, завершение Quit().
using Godot;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class UIShotSimDebug : Node
{
    [Inject] private IInventoryService Inventory = null!;
    [Inject] private IEquipmentGenerator EquipmentGenerator = null!;
    [Inject] private IQiService Qi = null!;

    private GameWorldController? _gwc;

    public override void _Ready()
    {
        var shot = System.Environment.GetEnvironmentVariable("GODOT_UI_SHOT");
        if (string.IsNullOrEmpty(shot)) { QueueFree(); return; }
        var container = Scene.GameBoot.Container;
        if (container != null) ContainerAdapter.InjectProperties(this, container);
        _gwc = GetParent() as GameWorldController;
        GD.Print("[UIShot] Ready — скриншоты UI-окон (инвентарь/книга/лист) через 2с");
        _ = RunAsync(shot);
    }

    private async Task RunAsync(string prefix)
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        // ── Подготовка инвентаря: легендарки (rarity-палитра видна в полный рост).
        int level = Qi == null ? 1 : System.Math.Max(1, (int)Qi.CultivationLevel);
        long seed = 777;
        int added = 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryWeapon(level, "sword", seed++), 1) ? 1 : 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryArmor(level, "armor_torso", seed++), 1) ? 1 : 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryArmor(level, "armor_feet", seed++), 1) ? 1 : 0;
        GD.Print($"[UIShot] доспавн легендарок для кадра: {added}/3");

        // ── №3: инвентарь (контраст золота на пергаменте).
        if (_gwc?.InventoryWindowForQA is { } inv)
        {
            inv.Toggle();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_inventory.png");
            inv.Toggle();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }

        // ── №6: книга техник (layout: блоки по типам друг под другом).
        if (_gwc?.TechniqueBookWindowForQA is { } book)
        {
            book.Toggle();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_technique.png");
            book.Toggle();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }

        // ── №5: лист персонажа, вкладка «Характеристики».
        if (_gwc?.CharacterSheetWindowForQA is { } sheet)
        {
            sheet.Toggle();
            sheet.SelectStatsTabForQA();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_character.png");
            sheet.Toggle();
        }

        GD.Print("[UIShot] done");
        GetTree().Quit();
    }

    private void Snap(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(path);
        GD.Print($"[UIShot] shot → {path} ({img.GetWidth()}x{img.GetHeight()})");
    }
}
