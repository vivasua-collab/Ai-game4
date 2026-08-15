#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Реализация IQiDataProvider — хранилище данных Ци per-entity.
// Отдельный класс от QiService (QiService обслуживает игрока, QiDataProvider — NPC).
// Конструктор без зависимостей (чистое хранилище данных).
// Регистрируется в DI как Singleton: IQiDataProvider → QiDataProvider.
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Реализация IQiDataProvider — хранилище данных Ци per-entity.
/// Отдельный класс от QiService (QiService обслуживает игрока, QiDataProvider — NPC).
/// Конструктор без зависимостей (чистое хранилище данных).
/// </summary>
public class QiDataProvider : IQiDataProvider
{
    // === Внутренняя структура данных сущности Ци ===

    /// <summary>
    /// Данные Ци одной сущности.
    /// </summary>
    private class QiEntityData
    {
        public long CurrentQi;
        public long MaxQi;
        public float Conductivity;
        public int CultivationLevel;
        // Спринт 4 B8: QiBuffer per-entity
        public bool IsQiBufferActive;
        public QiBufferMode QiBufferMode;
        public long QiBufferInvested;
    }

    // === Хранилище per-entity ===
    private readonly Dictionary<string, QiEntityData> _entityData = new();

    // === Конструктор (без зависимостей) ===

    public QiDataProvider()
    {
    }

    // === IQiDataProvider ===

    public long GetCurrentQi(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return 0;
        return data.CurrentQi;
    }

    public long GetMaxQi(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return 0;
        return data.MaxQi;
    }

    public float GetConductivity(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return 0f;
        return data.Conductivity;
    }

    /// <summary>
    /// Установить состояние Ци для сущности (при создании NPC).
    /// P0-4.1 FIX: сохраняет QiBuffer-поля при перезаписи.
    /// </summary>
    public void SetQiState(string entityId, long currentQi, long maxQi, float conductivity)
    {
        if (entityId == null) return;

        // P0-4.1 FIX: Сохраняем QiBuffer-поля перед перезаписью
        bool existingBufferActive = false;
        QiBufferMode existingBufferMode = QiBufferMode.None;
        long existingBufferInvested = 0;

        if (_entityData.TryGetValue(entityId, out var existing))
        {
            existingBufferActive = existing.IsQiBufferActive;
            existingBufferMode = existing.QiBufferMode;
            existingBufferInvested = existing.QiBufferInvested;
        }

        _entityData[entityId] = new QiEntityData
        {
            CurrentQi = currentQi,
            MaxQi = maxQi,
            Conductivity = conductivity,
            CultivationLevel = existing?.CultivationLevel ?? 1,
            IsQiBufferActive = existingBufferActive,
            QiBufferMode = existingBufferMode,
            QiBufferInvested = existingBufferInvested
        };
    }

    public bool HasEntity(string entityId)
    {
        return entityId != null && _entityData.ContainsKey(entityId);
    }

    public void RemoveEntity(string entityId)
    {
        if (entityId != null)
            _entityData.Remove(entityId);
    }

    public int GetCultivationLevel(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return 0;
        return data.CultivationLevel;
    }

    public void SetCultivationLevel(string entityId, int level)
    {
        if (entityId == null) return;
        if (!_entityData.TryGetValue(entityId, out var data)) return;

        data.CultivationLevel = level;
    }

    // === Спринт 4 B8: Per-entity QiBuffer ===

    public bool IsQiBufferActive(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return false;
        return data.IsQiBufferActive;
    }

    public QiBufferMode GetQiBufferMode(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return QiBufferMode.None;
        return data.QiBufferMode;
    }

    public long GetQiBufferInvested(string entityId)
    {
        if (entityId == null || !_entityData.TryGetValue(entityId, out var data))
            return 0;
        return data.QiBufferInvested;
    }

    public void SetQiBufferState(string entityId, bool isActive, QiBufferMode mode, long qiInvested)
    {
        if (entityId == null) return;
        if (!_entityData.TryGetValue(entityId, out var data)) return;

        data.IsQiBufferActive = isActive;
        data.QiBufferMode = mode;
        data.QiBufferInvested = qiInvested;
    }

    // === P0-X1 FIX: NPC Qi расход ===

    /// <summary>
    /// Попытаться списать Ци с NPC-сущности.
    /// P0-X1 FIX: для NPC Qi расход через QiBuffer — прямое списание.
    /// </summary>
    public bool TryConsumeQi(string entityId, long amount)
    {
        if (entityId == null || amount <= 0) return false;
        if (!_entityData.TryGetValue(entityId, out var data)) return false;

        if (data.CurrentQi >= amount)
        {
            data.CurrentQi -= amount;
            return true;
        }
        return false;
    }
}
