#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Tile;

/// <summary>
/// ResourceService — manages harvestable resources on tiles: spawn, harvest,
/// pickup, and respawn scheduling.
///
/// Implements <see cref="IResourceService"/>. Cross-module integration is
/// via EventBus only (ResourceHarvestedEvent, ResourceRespawnedEvent,
/// ItemAddRequestEvent). DayChangedEvent subscription drives respawn checks.
/// </summary>
public sealed class ResourceService : IResourceService, IDisposable
{
    [Inject] private readonly ITimeService _timeService = null!;
    [Inject] private readonly ISubscriber<DayChangedEvent> _dayChangedSub = null!;
    [Inject] private readonly IPublisher<ResourceRespawnedEvent> _respawnedPub = null!;
    [Inject] private readonly IPublisher<ResourceHarvestedEvent> _harvestedPub = null!;
    [Inject] private readonly IPublisher<ItemAddRequestEvent> _itemAddPub = null!;

    private readonly List<DepletedResource> _depleted = new();
    private IDisposable? _dayChangedToken;

    /// <summary>
    /// AUDIT-0911 WT-1 FIX: подписка DayChangedEvent — РАНЬШЕ Initialize()
    /// никто не вызывал (TileModule стартовал только TileService.Initialize):
    /// респаун ресурсов был МЁРТВ в живой игре (вскрывался только QA-симом,
    /// который звал RespawnCheck напрямую). Идемпотентен.
    /// </summary>
    public void Initialize()
    {
        _dayChangedToken ??= _dayChangedSub.Subscribe(OnDayChanged);
    }

    // === IResourceService ===

    public bool TrySpawnResource(int x, int y, string resourceId)
    {
        // V1: accept any non-empty resource ID; real impl validates against
        // a catalogue of harvestable ObjectTypes from ObjectDefaults.
        return !string.IsNullOrEmpty(resourceId);
    }

    public bool RequestPickup(string itemId)
    {
        // Review этап 6 (P2-5): честная КОМАНДА (было TryPickup(out ItemData)
        // с item = null! — вызывающий получал null при «успехе"). Выдача предмета —
        // асинхронный ответ InventoryModule на ItemAddRequestEvent.
        if (string.IsNullOrEmpty(itemId)) return false;
        _itemAddPub.Publish(new ItemAddRequestEvent(itemId, 1, "pickup"));
        return true;
    }

    public HarvestResult Harvest(int x, int y, in GameTile tile)
    {
        if (!tile.IsHarvestable || tile.ResourceAmount <= 0f || string.IsNullOrEmpty(tile.ResourceId))
            return HarvestResult.Empty;

        // Use HarvestAmount from ObjectDefaults (fallback to 10% of ResourceMax).
        string itemId = tile.ResourceId;
        int amount = 1;
        if (ObjectDefaults.TryGet(tile.Object, out var objInfo))
        {
            itemId = objInfo.ItemId;
            amount = objInfo.HarvestAmount > 0 ? objInfo.HarvestAmount : Math.Max(1, (int)(tile.ResourceMax * 0.1f));
        }
        else
        {
            amount = Math.Max(1, (int)(tile.ResourceMax * 0.1f));
        }

        if (amount > tile.ResourceAmount) amount = (int)tile.ResourceAmount;
        if (amount <= 0) return HarvestResult.Empty;

        float remaining = Math.Max(0f, tile.ResourceAmount - amount);
        bool depleted = remaining <= 0f;

        // Publish ItemAddRequest with ITEM ID (not resource ID) so InventoryModule
        // can resolve it via IItemDatabaseService.TryGetItem().
        _itemAddPub.Publish(new ItemAddRequestEvent(itemId, amount, "harvest"));

        // NOTE: ResourceHarvestedEvent is published by TileService.TryHarvest (sole publisher).
        // Do NOT publish here to avoid double-publish bug.

        return new HarvestResult(itemId, amount, remaining, depleted);
    }

    public void RegisterDepletedResource(int x, int y, in GameTile tile)
    {
        if (string.IsNullOrEmpty(tile.ResourceId)) return;

        // AUDIT-0911 WT-7 FIX: задержка респауна — из ObjectDefaults
        // («единый источник истины»: берёза 5, куст 3, камень 14, руда 30,
        // трава 2), не хардкод 7. RespawnDays == 0 — не респаунится вовсе.
        int respawnDays = 7;
        if (ObjectDefaults.TryGet(tile.Object, out var objInfo) && objInfo.RespawnDays > 0)
            respawnDays = objInfo.RespawnDays;

        _depleted.Add(new DepletedResource
        {
            X = x,
            Y = y,
            ResourceId = tile.ResourceId,
            ResourceMax = tile.ResourceMax,
            OriginalObject = tile.Object,
            // AUDIT-0911 WT-2 FIX: АБСОЛЮТНЫЙ день (TotalMinutes/1440), НЕ
            // день месяца: CurrentDay оборачивается 30→1 — ресурс, истощённый
            // ≥24-го числа, не респаунился НИКОГДА (разность уходила в минус).
            DayDepleted = AbsoluteDay(),
            RespawnDayDelay = respawnDays,
        });
    }

    private int AbsoluteDay() => _timeService.CurrentTime.TotalMinutes / 60 / 24;

    private void OnDayChanged(in DayChangedEvent e)
    {
        // AUDIT-0911 WT-2: e.Day — день МЕСЯЦА (оборачивается); сверяем по
        // абсолютному дню (см. RegisterDepletedResource).
        RespawnCheck(AbsoluteDay());
    }

    /// <summary>
    /// Check depleted resources for respawn. Public for testing.
    /// AUDIT-0911 WT-2: currentDay — АБСОЛЮТНЫЙ день (дней от старта мира,
    /// TotalMinutes/1440). День месяца (1–30, с оборотом) давал вечное
    /// «не.respawится» для ресурсов, истощённых в конце месяца.
    /// </summary>
    public void RespawnCheck(int currentDay)
    {
        for (int i = _depleted.Count - 1; i >= 0; i--)
        {
            var d = _depleted[i];
            if (currentDay - d.DayDepleted >= d.RespawnDayDelay)
            {
                _respawnedPub.Publish(new ResourceRespawnedEvent(
                    d.X, d.Y, d.OriginalObject, d.ResourceMax, d.ResourceId));
                _depleted.RemoveAt(i);
            }
        }
    }

    public void Dispose()
    {
        _dayChangedToken?.Dispose();
        _dayChangedToken = null;
        _depleted.Clear();
    }

    private struct DepletedResource
    {
        public int X;
        public int Y;
        public string ResourceId;
        public float ResourceMax;
        public ObjectType OriginalObject;
        public int DayDepleted;
        public int RespawnDayDelay;
    }
}
