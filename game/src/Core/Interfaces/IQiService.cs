#nullable enable
namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Qi management. All Qi values are <c>long</c> (no float).
/// Regen runs in a batch every <c>QI_REGEN_BATCH_TICKS</c> ticks.
/// </summary>
public interface IQiService
{
    long GetCurrentQi(int entityId);
    long GetCoreCapacity(int entityId);
    int GetCultivationLevel(int entityId);

    void AddQi(int entityId, long amount);
    bool ConsumeQi(int entityId, long amount);

    void ProcessRegenBatch();
}
