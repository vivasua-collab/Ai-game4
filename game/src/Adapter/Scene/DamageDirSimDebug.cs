#nullable enable
// Создано: 2026-09-04 — S5: headless-верификация стрелок направления урона
// (GODOT_DAMAGEDIR_DEBUG=1).
//
// Проверяет инварианты DamageDirectionIndicator (GameWorldController):
//   1. Урон от NPC вне экрана (восток) → стрелка есть, угол ≈ 0°.
//   2. NPC переместился на север → стрелка следует, угол ≈ -90° (тик поз).
//   3. NPC вернулся на экран → стрелка убрана мгновенно.
//   4. TTL: после 2.6с без урона стрелка исчезает.
//
// Важно: тест вызывает world.ShowDamageDirection() НАПРЯМУЮ (public QA-путь,
// тот же метод, что и OnPlayerDamaged) — НЕ публикует DamageAppliedEvent,
// т.к. его слушают 10+ боевых сервисов (BodyService, NPC AI...) — публикация
// задвоила бы урон.
// Позиции NPC двигаем через INPCService.UpdatePosition (тайлы).
// Запуск: GODOT_NEWGAME=1 GODOT_DAMAGEDIR_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация off-screen индикатора направления атакующего.
/// Итог: [DamageDirSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class DamageDirSimDebug : Node
{
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _player;

    private GameWorldController? _world;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[DamageDirSim] diag: npc={_npcService != null}, player={_player != null}");

        if (_npcService == null || _player == null)
        {
            GD.Print("[DamageDirSim] VERDICT: FAIL — services not injected");
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (_world == null)
        {
            GD.Print("[DamageDirSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        // Живой NPC + позиция игрока (тайлы).
        var npcIds = _npcService.GetAllNPCIds();
        if (npcIds == null || npcIds.Count == 0)
        {
            GD.Print("[DamageDirSim] VERDICT: FAIL — нет NPC в мире");
            return;
        }
        string npcId = npcIds[0];
        var playerTile = _player.Position;
        GD.Print($"[DamageDirSim] diag: npc={npcId}, playerTile=({playerTile.X},{playerTile.Y}), zoom/viewport управляются контроллером");

        bool pass = true;

        // === 1. NPC восточнее игрока (+8 тайлов — вне экрана) ==============
        // Экран: ~6.7×3.75 тайла (zoom 3, 1280×720) — ±8 тайлов точно вне.
        var eastTile = new Position2D(playerTile.X + 8, playerTile.Y);
        _npcService.UpdatePosition(npcId, eastTile);
        bool shown = _world.ShowDamageDirection(npcId);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        int cnt1 = _world.DamageDirCount;
        float? ang1 = _world.DamageDirAngleOf(npcId);
        GD.Print($"[DamageDirSim] step1 east: shown={shown}, count={cnt1}, angle={ang1:F1}° (expected shown=True, count=1, angle≈0°)");
        if (!shown || cnt1 != 1 || ang1 == null || Mathf.Abs(ang1.Value) > 8f) pass = false;

        // === 2. NPC переместился на север — стрелка следует ================
        var northTile = new Position2D(playerTile.X, playerTile.Y - 8);
        _npcService.UpdatePosition(npcId, northTile);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        float? ang2 = _world.DamageDirAngleOf(npcId);
        int cnt2 = _world.DamageDirCount;
        GD.Print($"[DamageDirSim] step2 north: count={cnt2}, angle={ang2:F1}° (expected count=1, angle≈-90°)");
        if (cnt2 != 1 || ang2 == null || Mathf.Abs(Mathf.Abs(ang2.Value) - 90f) > 8f) pass = false;

        // === 3. NPC на экране (рядом с игроком) — стрелка убрана ===========
        var nearTile = new Position2D(playerTile.X + 1, playerTile.Y);
        _npcService.UpdatePosition(npcId, nearTile);
        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        int cnt3 = _world.DamageDirCount;
        GD.Print($"[DamageDirSim] step3 on-screen: count={cnt3} (expected 0 — источник виден)");
        if (cnt3 != 0) pass = false;

        // === 4. TTL: удар вне экрана → 2.6с тишины → стрелка исчезла =======
        _npcService.UpdatePosition(npcId, eastTile);
        _world.ShowDamageDirection(npcId);
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        int cnt4a = _world.DamageDirCount;
        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        int cnt4b = _world.DamageDirCount;
        GD.Print($"[DamageDirSim] step4 ttl: after-hit={cnt4a}, after-2.6s={cnt4b} (expected 1 → 0)");
        if (cnt4a != 1 || cnt4b != 0) pass = false;

        GD.Print($"[DamageDirSim] VERDICT: {(pass ? "PASS — стрелка направления: показать/следовать/на-экране/TTL" : "FAIL")}");
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
