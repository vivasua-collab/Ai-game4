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

    public void Initialize()
    {
        _dayChangedToken = _dayChangedSub.Subscribe(OnDayChanged);
    }

    // === IResourceService ===

    public bool TrySpawnResource(int x, int y, string resourceId)
    {
        // V1: accept any non-empty resource ID; real impl validates against
        // a catalogue of harvestable ObjectTypes from ObjectDefaults.
        return !string.IsNullOrEmpty(resourceId);
    }

    public bool TryPickup(string resourceId, out ItemData item)
    {
        item = null!;
        if (string.IsNullOrEmpty(resourceId)) return false;
        // Publish an add-item request; consumers (Inventory module) react.
        _itemAddPub.Publish(new ItemAddRequestEvent(resourceId, 1, "pickup"));
        return true;
    }

    public HarvestResult Harvest(int x, int y, in GameTile tile)
    {
        if (!tile.IsHarvestable || tile.ResourceAmount <= 0f || string.IsNullOrEmpty(tile.ResourceId))
            return HarvestResult.Empty;

        int amount = Math.Max(1, (int)(tile.ResourceMax * 0.1f));
        if (amount > tile.ResourceAmount) amount = (int)tile.ResourceAmount;
        if (amount <= 0) return HarvestResult.Empty;

        float remaining = Math.Max(0f, tile.ResourceAmount - amount);
        bool depleted = remaining <= 0f;

        _itemAddPub.Publish(new ItemAddRequestEvent(tile.ResourceId, amount, "harvest"));
        _harvestedPub.Publish(new ResourceHarvestedEvent(
            x, y, tile.ResourceId, tile.ResourceId, amount, remaining));

        return new HarvestResult(tile.ResourceId, amount, remaining, depleted);
    }

    public void RegisterDepletedResource(int x, int y, in GameTile tile)
    {
        if (string.IsNullOrEmpty(tile.ResourceId)) return;
        _depleted.Add(new DepletedResource
        {
            X = x,
            Y = y,
            ResourceId = tile.ResourceId,
            ResourceMax = tile.ResourceMax,
            OriginalObject = tile.Object,
            DayDepleted = _timeService.CurrentDay,
            RespawnDayDelay = 7, // V1: 7 days respawn default
        });
    }

    private void OnDayChanged(in DayChangedEvent e) => RespawnCheck(e.Day);

    /// <summary>Check depleted resources for respawn. Public for testing.</summary>
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
