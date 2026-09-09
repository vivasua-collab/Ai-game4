#nullable enable
using System;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Implemented by services that own persistent state. The Save module's
/// aggregator calls <see cref="CaptureState"/> on every save and
/// <see cref="RestoreState"/> on every load.
/// </summary>
public interface ISaveable
{
    string SaveKey { get; }

    /// <summary>
    /// R11 P0-Save (внешнее ревью 2026-09-09): конкретный тип state-блока
    /// для persistence round-trip. File-handler пишет JSON через
    /// JsonSerializer, а при чтении значения словаря приходят как
    /// <c>JsonElement</c> — агрегатор десериализует их обратно в ЭТОТ тип,
    /// чтобы <c>RestoreState(state)</c> получил типизированный объект.
    /// Без этого паттерн <c>state is XxxSaveData</c> у всех реализаторов
    /// молча проваливался (JsonElement — не XxxSaveData), и НИ ОДИН блок
    /// состояния не восстанавливался после загрузки сейва.
    /// </summary>
    Type StateType { get; }

    object CaptureState();

    void RestoreState(object state);
}
