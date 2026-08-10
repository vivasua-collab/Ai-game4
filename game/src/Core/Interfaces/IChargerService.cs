#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Qi-stone charger: slots, buffer, thermal balance.</summary>
public interface IChargerService
{
    void RegisterCharger(int chargerId, Position2D position, int maxSlots);
    bool InsertStone(int chargerId, int slotIndex, string stoneId);
    void ProcessTick();
}
