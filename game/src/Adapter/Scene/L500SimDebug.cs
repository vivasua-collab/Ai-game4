#nullable enable
// L500 (2026-09-15, запрос пользователя): «Основным миром становится мир
// 500 на 500. В него необходимо интегрировать все генерации NPC, в нем я
// буду тестировать боевку». Headless-верификация (GODOT_L500_DEBUG=1):
//   1. Сетка 500×500 собралась (TileService), локация large_world активна.
//   2. Генерации NPC ИНТЕГРИРОВАНЫ: людей ≥ 30 (GenerateStartup ×4),
//      зверей ≥ 15 (SpawnForLocation от площади), групп = 4 (большие карты).
//   3. «Пояс жизни»: ≥ 40% NPC в Чебышёв-радиусе 100 от центра — игрок
//      встречает население, а не марширует по пустыне 1×1 км.
//   4. MaxActiveNPCs=100 не превышен (спавнер не должен резать состав).
// Запуск (в связке с меню-хуком):
//   GODOT_NEWGAME=1 GODOT_NEWGAME_WORLD=large_world GODOT_L500_DEBUG=1 \
//     godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

public partial class L500SimDebug : Node
{
    [Inject] private ITileService? _tiles;
    [Inject] private INPCService? _npcs;
    [Inject] private INPCSpawnerService? _spawner;
    [Inject] private IAnimalService? _animals;
    [Inject] private INPCGroupService? _groups;
    [Inject] private IWorldService? _world;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        GD.Print("[L500Sim] Ready — мир 500×500: интеграция генераций NPC, тест через 10с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        // Сборка большого мира дольше полигона (250k тайлов + ~50 NPC +
        // 24 зверя + 4 группы): ждём щедро, но небесконечно.
        for (int i = 0; i < 100; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null || _tiles == null || _npcs == null || _spawner == null
            || _animals == null || _groups == null || _world == null)
        {
            GD.Print("[L500Sim] VERDICT: FAIL — GameWorldController/DI not wired");
            return;
        }

        bool pass = true;

        // === 1. Локация и сетка =========================================
        bool isLarge = _world.CurrentLocationId == "large_world";
        bool gridOk = _tiles.MapWidth == 500 && _tiles.MapHeight == 500;
        GD.Print($"[L500Sim] 1. локация={_world.CurrentLocationId} (ожидаем large_world), " +
                  $"сетка {_tiles.MapWidth}×{_tiles.MapHeight} (ожидаем 500×500)");
        pass &= isLarge && gridOk;

        // === 2. Генерации интегрированы =================================
        // NOTE: на диких землях идёт эмерджентная конкуренция (волки/бандиты
        // против мирных) — живой счёт дрейфует вниз от заспавненного.
        // Ассерт интеграции — по ЗАСПАВНЕННЫМ (ActiveNPCCount, вкл. павших);
        // живые — отдельная метрика (мир не должен вымирать мгновенно).
        var ids = _npcs.GetAllNPCIds();
        int npcCount = _spawner.ActiveNPCCount;
        int aliveCount = ids.Count;
        var animals = _animals.GetAliveAnimalsInRange(
            new Position2D(250, 250), 400f);
        int animalCount = animals.Count;
        int groupCount = _groups.GetAllGroups().Count;
        bool npcsIntegrated = npcCount >= 30;
        bool aliveOk = aliveCount >= 15;
        bool animalsIntegrated = animalCount >= 15;
        bool groupsIntegrated = groupCount >= 4;
        GD.Print($"[L500Sim] 2. NPC заспавнено={npcCount} (≥30), из них живых={aliveCount} (≥15), " +
                  $"звери={animalCount} (≥15), группы={groupCount} (≥4)");
        pass &= npcsIntegrated && aliveOk && animalsIntegrated && groupsIntegrated;

        // === 3. Пояс жизни ==============================================
        int inRing = 0, counted = 0;
        foreach (var id in ids)
        {
            var st = _npcs.GetNPCState(id);
            if (st == null || !st.IsAlive) continue;
            counted++;
            int dist = Math.Max(Math.Abs(st.Position.X - 250), Math.Abs(st.Position.Y - 250));
            if (dist <= 100) inRing++;
        }
        double ringShare = counted > 0 ? (double)inRing / counted : 0;
        bool ringOk = ringShare >= 0.4;
        GD.Print($"[L500Sim] 3. пояс жизни: {inRing}/{counted} живых NPC в радиусе 100 " +
                  $"от центра ({ringShare:P0}, ожидаем ≥40%)");
        pass &= ringOk;

        // === 4. Кап спавнера ============================================
        bool capOk = npcCount <= 100;
        GD.Print($"[L500Sim] 4. MaxActiveNPCs: {npcCount}/100 ({(capOk ? "ок" : "ПРЕВЫШЕН")})");
        pass &= capOk;
        GD.Print($"[L500Sim] VERDICT: {(pass
            ? "PASS — мир 500×500 основной: сетка/NPC/звери/группы интегрированы, пояс жизни у центра, кап не превышен"
            : "FAIL")}");
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
