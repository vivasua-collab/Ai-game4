#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Save service — owns the save/load pipeline and orchestrates the
    /// <see cref="ISaveable"/> aggregator. All operations take a
    /// <see cref="SaveSlot"/> value (name + type).
    /// </summary>
    public interface ISaveService
    {
        bool Save(SaveSlot slot);
        bool Load(SaveSlot slot);
        bool HasSave(SaveSlot slot);
        bool DeleteSave(SaveSlot slot);
        IReadOnlyList<SaveInfo> GetAllSaves();

        /// <summary>
        /// Этап-4 (P1, 20.10.2026): pre-flight проверка слота БЕЗ побочных
        /// эффектов — формат (мета/версия/обязательные блоки) проверяется
        /// ДО сброса активного мира. false — слот повреждён/неполон/
        /// неподдерживаем; причина — в <see cref="LastError"/>. Домены не
        /// трогаются (RestoreState не вызывается).
        /// </summary>
        bool Validate(SaveSlot slot);

        /// <summary>
        /// Этап-4 (P1, транзакционная загрузка): снять in-memory резервную
        /// копию ВСЕХ доменов (CaptureState без записи файла и без
        /// SaveStartedEvent — снимок неразрушающий). Возвращает false, если
        /// хоть один домен не снялся (откат будет невозможен).
        /// </summary>
        bool CaptureBackup();

        /// <summary>
        /// Этап-4 (P1): откат к снимку <see cref="CaptureBackup"/> — вызывается
        /// В СПАРКЕ со сбросом доменов (WorldDomainResetCoordinator.ResetAll)
        /// ПОСЛЕ неудачной загрузки: сброс вычищает частично-восстановленное,
        /// RestoreBackup возвращает предыдущий мир. Снимок при этом очищается.
        /// </summary>
        bool RestoreBackup();

        /// <summary>Этап-4 (P1): освободить снимок без восстановления.</summary>
        void DiscardBackup();

        /// <summary>
        /// R11 P1-Save (review): описание последней ошибки Save/Load
        /// (null = успех). SaveModule публикует ЭТО в SaveCompletedEvent /
        /// LoadCompletedEvent — раньше success=true писался безусловно,
        /// независимо от реального результата операции.
        /// </summary>
        string? LastError { get; }
    }
}
