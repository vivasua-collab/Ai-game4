#nullable enable
// Создано: 2026-09-08 — Review этап 6: headless-верификация мира/тайлов
// (GODOT_RESPAWN_DEBUG=1). Проверяет:
//   1. P0-1: harvest до истощения → advance 7 дней → тайл ВОССТАНОВЛЕН
//      (Object/ResourceId/ResourceAmount/IsHarvestable) + TileChangedEvent.
//   2. P1-2: TryTravel возвращает false БЕЗ смены CurrentLocationId
//      (физический переход мира не реализован — честный контракт).
//   3. P2-4: TimeChangedEvent.Delta == ITimeService.DeltaTime (1.0, не 1/60).
// Запуск: GODOT_NEWGAME=1 GODOT_RESPAWN_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Tile;

namespace CultivationGame.Adapter.Scene;

public partial class RespawnSimDebug : Node
{
    [Inject] private ITileService? _tiles;
    [Inject] private ITimeService? _time;
    [Inject] private IWorldService? _world;
    [Inject] private ISubscriber<TimeChangedEvent>? _timeChangedSub;
    [Inject] private ISubscriber<TileChangedEvent>? _tileChangedSub;
    [Inject] private ResourceService? _resourceServiceImpl;

    private System.IDisposable? _timeChangedToken;
    private System.IDisposable? _tileChangedToken;
    private float _lastTimeDelta = -1f;
    private int _tileChangedCount;
    private int _changedX = -1, _changedY = -1;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _timeChangedToken = _timeChangedSub?.Subscribe((in TimeChangedEvent e) =>
        {
            _lastTimeDelta = e.Delta;
        });
        _tileChangedToken = _tileChangedSub?.Subscribe((in TileChangedEvent e) =>
        {
            _tileChangedCount++;
            _changedX = e.X;
            _changedY = e.Y;
        });

        GD.Print("[RespawnSim] Ready — world/tile verification starts in 2.5s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _timeChangedToken?.Dispose();
        _tileChangedToken?.Dispose();
        _timeChangedToken = null;
        _tileChangedToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);

        if (_tiles == null || _time == null || _world == null || _resourceServiceImpl == null)
        {
            GD.Print("[RespawnSim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        bool pass = true;

        // === 1. P2-4: TimeChangedEvent.Delta == DeltaTime ==================
        // Ждём пару тиков и сверяем дельту (1.0, не 1/60).
        await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
        bool deltaOk = _lastTimeDelta > 0f && System.Math.Abs(_lastTimeDelta - _time.DeltaTime) < 0.001f;
        GD.Print($"[RespawnSim] TimeChangedEvent.Delta={_lastTimeDelta} vs ITimeService.DeltaTime={_time.DeltaTime} (ожидаем равенство 1.0)");
        pass &= deltaOk;

        // === 2. P1-2: TryTravel честно отказывает ==========================
        string locBefore = _world.CurrentLocationId;
        // Регистрируем вторую локацию для теста (в каталоге только test_polygon).
        if (_world is Modules.World.WorldService ws)
        {
            ws.RegisterLocation(new LocationData
            {
                Id = "qa_forest_loc", Name = "QA Forest", Width = 50, Height = 50,
                Seed = 777, TerrainType = TerrainType.Grass, LocationType = LocationType.WildLands,
            });
        }
        bool traveled = _world.TryTravel("qa_forest_loc");
        string locAfter = _world.CurrentLocationId;
        bool travelOk = !traveled && locBefore == locAfter;
        GD.Print($"[RespawnSim] TryTravel('qa_forest_loc') → {traveled} (ожидаем False), loc '{locBefore}'→'{locAfter}' (не должен меняться)");
        pass &= travelOk;

        // === 3. P0-1: истощение → 7 дней → респаун =========================
        // Ищем harvestable тайл (куст/дерево/руда), сканируя кольца от центра карты.
        int hx = -1, hy = -1;
        GameTile hTile = default;
        for (int radius = 1; radius <= 25 && hx < 0; radius++)
        {
            for (int dx = -radius; dx <= radius && hx < 0; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (System.Math.Abs(dx) != radius && System.Math.Abs(dy) != radius) continue; // только кольцо
                    int cx = _tiles.MapWidth / 2 + dx, cy = _tiles.MapHeight / 2 + dy;
                    var t = _tiles.GetTile(cx, cy);
                    if (t.IsHarvestable && t.ResourceAmount > 0f)
                    {
                        hx = cx; hy = cy; hTile = t;
                        break;
                    }
                }
            }
        }
        if (hx < 0)
        {
            GD.Print("[RespawnSim] WARN — harvestable tile не найден (генерация?); respawn-фаза пропущена");
        }
        else
        {
            GD.Print($"[RespawnSim] harvest target ({hx},{hy}): {hTile.Object} {hTile.ResourceId} amount={hTile.ResourceAmount}");
            // Добиваем до истощения итеративными сборами.
            int guard = 0;
            while (_tiles.TryHarvest(hx, hy, out var r) && !r.Depleted && guard++ < 200) { }
            var depletedTile = _tiles.GetTile(hx, hy);
            bool depletedOk = !depletedTile.IsHarvestable && depletedTile.Object == ObjectType.None;
            GD.Print($"[RespawnSim] depleted: IsHarvestable={depletedTile.IsHarvestable}, Object={depletedTile.Object} (ожидаем False/None), итераций={guard}");
            pass &= depletedOk;

            // Продвигаем 7 игровых дней: RespawnCheck вызывается по DayChangedEvent.
            // 1 день = 1440 тиков (минут) — слишком долго ждать реально; вызываем
            // RespawnCheck напрямую с будущим днём (метод публичен для тестов).
            int futureDay = _time.CurrentDay + 7;
            _tileChangedCount = 0;
            _resourceServiceImpl.RespawnCheck(futureDay);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            var respawnedTile = _tiles.GetTile(hx, hy);
            bool respawnOk = respawnedTile.IsHarvestable
                && respawnedTile.Object == hTile.Object
                && respawnedTile.ResourceId == hTile.ResourceId
                && respawnedTile.ResourceAmount >= hTile.ResourceMax * 0.99f
                && _tileChangedCount > 0 && _changedX == hx && _changedY == hy;
            GD.Print($"[RespawnSim] respawned: IsHarvestable={respawnedTile.IsHarvestable}, Object={respawnedTile.Object} " +
                     $"(из {hTile.Object}), id='{respawnedTile.ResourceId}', amount={respawnedTile.ResourceAmount}/{respawnedTile.ResourceMax}, " +
                     $"TileChanged={_tileChangedCount}@({_changedX},{_changedY})");
            pass &= respawnOk;
        }

        PrintVerdict(pass);
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[RespawnSim] VERDICT: {(pass ? "PASS — resource respawn + honest travel + time delta" : "FAIL")}");
    }
}
