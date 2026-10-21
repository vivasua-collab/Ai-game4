#nullable enable
// Создано: 2026-10-20 — Этап-4 аудита (BUG-013): производные состояния доменов
// после RestoreState. Симметричен IWorldResettable (сброс world-transition):
// IWorldResettable чистит домен ДО восстановления из сейва, IPostLoadSync —
// пересобирает ПРОИЗВОДНЫЕ трекеры ПОСЛЕ восстановления.
using System;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Пост-загрузочная синхронизация производных состояний. Реализуют сервисы,
/// чьи данные ВОПРОДИЗВОДНЫ от восстановленных реестров (а не сериализуются
/// сами): GameSession вызывает <see cref="OnAfterLoad"/> после успешного
/// RestoreState из сейва И после отката транзакционной загрузки.
/// </summary>
/// <remarks>
/// <para><b>Почему это нужно (Этап-4, BUG-013):</b> NPCService.RestoreState
/// наполняет реестр NPC напрямую, минуя NPCSpawnerService.SpawnNPC — приватный
/// трекер _spawnedIds оставался пустым: ActiveNPCCount врал, лимит
/// MaxActiveNPCs обходился, DespawnNPC восстановленного NPC был no-op.
/// Спавн-фазы имеют SkipOnLoad=true (RestoreState их заменяет), потому
/// синхронизацию нельзя «просто вызвать в фазе спавна».</para>
/// <para><b>Контракт:</b> реализация обязана быть идемпотентной (повторный
/// вызов — no-op) и НЕ бросать исключений в валидных состояниях; исключение
/// провалит загрузку (fail-closed — производное состояние недостоверно).</para>
/// </remarks>
public interface IPostLoadSync
{
    /// <summary>
    /// Домены восстановлены из сейва/снимка — пересобрать производные
    /// трекеры по восстановленным реестрам.
    /// </summary>
    void OnAfterLoad();
}
