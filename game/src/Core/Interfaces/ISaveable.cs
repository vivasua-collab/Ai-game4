#nullable enable
namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Implemented by services that own persistent state. The Save module's
/// aggregator calls <see cref="CaptureState"/> on every save and
/// <see cref="RestoreState"/> on every load.
/// </summary>
public interface ISaveable
{
    string SaveKey { get; }
    object CaptureState();
    void RestoreState(object state);
}
