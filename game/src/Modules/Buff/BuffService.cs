#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-09 — BF-A03/A04/A07/A08/A10/BF-I03/BF-I05: исправления багов
// Реализация IBuffService.
// Перенесено из legacy BuffManager.cs с адаптацией под VContainer + MessagePipe.
// God Object (1614 LOC) разбит на BuffService + BuffCalculator + BuffTickProcessor.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff
{
    /// <summary>
    /// Реализация IBuffService.
    /// Управляет наложением, снятием, тиканием и расчётом модификаторов баффов.
    /// ⛔ НЕ модифицирует: первичные статы, coreCapacity, qiDensity, qiRegen.
    /// </summary>
    public class BuffService : IBuffService
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IPublisher<BuffAppliedEvent> _appliedPub;
        private readonly IPublisher<BuffRemovedEvent> _removedPub;
        private readonly IPublisher<BuffExpiredEvent> _expiredPub;
        // BF-A10: Добавлен публикатор события изменения модификатора
        private readonly IPublisher<StatModifierChangedEvent> _statModifierChangedPub;
        private readonly BuffTickProcessor _tickProcessor;

        // === Состояние ===
        private readonly Dictionary<string, List<ActiveBuff>> _entityBuffs = new();
        private BuffConfig _config;
        private bool _isConfigured;

        // BF-A03: Маппинг: эффект → тип иммунитета, который его блокирует
        private static readonly Dictionary<BuffType, BuffType> EffectToImmunityMap = new()
        {
            { BuffType.Poison, BuffType.ImmunityPoison },
            { BuffType.Stun, BuffType.ImmunityStun },
            { BuffType.Slow, BuffType.ImmunitySlow },
            { BuffType.Burn, BuffType.ImmunityPoison },  // Горение: яд-иммунитет также защищает
            { BuffType.Bleed, BuffType.ImmunityPoison },  // Кровотечение: яд-иммунитет также защищает
            { BuffType.Freeze, BuffType.ImmunitySlow },    // Заморозка: замедление-иммунитет также защищает
            { BuffType.Blind, BuffType.ImmunityStun },     // Ослепление: стан-иммунитет также защищает
            { BuffType.Silence, BuffType.ImmunityStun },   // Безмолвие: стан-иммунитет также защищает
        };

        // === Конструктор (VContainer) ===

        public BuffService(
            IPublisher<BuffAppliedEvent> appliedPub,
            IPublisher<BuffRemovedEvent> removedPub,
            IPublisher<BuffExpiredEvent> expiredPub,
            IPublisher<StatModifierChangedEvent> statModifierChangedPub,
            BuffTickProcessor tickProcessor)
        {
            _appliedPub = appliedPub;
            _removedPub = removedPub;
            _expiredPub = expiredPub;
            _statModifierChangedPub = statModifierChangedPub;
            _tickProcessor = tickProcessor;
        }

        /// <summary>
        /// Настроить сервис конфигурацией.
        /// Вызывается из BuffModule.IStartable.Start().
        /// </summary>
        public void Configure(BuffConfig config)
        {
            _config = config;
            _isConfigured = true;
        }

        // === IBuffService: Управление баффами ===

        public bool ApplyBuff(string entityId, string buffId, float duration = -1f, float potency = 1f)
        {
            // BF-I05: Если не настроен — используем конфиг по умолчанию
            if (!_isConfigured)
            {
                Configure(new BuffConfig());
            }

            var buffs = GetOrCreateBuffList(entityId);

            // Проверяем лимит
            if (buffs.Count >= _config?.MaxBuffsPerEntity && !buffs.Exists(b => b.BuffId == buffId))
                return false;

            // Ищем существующий бафф с таким ID
            int existingIndex = buffs.FindIndex(b => b.BuffId == buffId);

            if (existingIndex >= 0)
            {
                var existing = buffs[existingIndex];
                switch (existing.StackingBehavior)
                {
                    case BuffStacking.Replace:
                        buffs.RemoveAt(existingIndex);
                        break;
                    case BuffStacking.Refresh:
                        existing.RemainingDuration = duration > 0 ? duration : existing.Duration;
                        _appliedPub.Publish(new BuffAppliedEvent(entityId, buffId, existing.Type, existing.RemainingDuration, potency));
                        return true;
                    case BuffStacking.Stack:
                        if (existing.CurrentStacks < existing.MaxStacks)
                        {
                            existing.CurrentStacks++;
                            // BF-I03: Стек учитывает potency — добавляем значение
                            existing.Value += existing.Potency;
                            existing.RemainingDuration = duration > 0 ? duration : existing.Duration;
                            _appliedPub.Publish(new BuffAppliedEvent(entityId, buffId, existing.Type, existing.RemainingDuration, potency));
                            // BF-A10: Публикуем событие изменения модификатора
                            if (existing.AffectedStat != null)
                            {
                                float newMod = GetStatModifier(entityId, existing.AffectedStat.Value);
                                _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, existing.AffectedStat.Value, newMod));
                            }
                            return true;
                        }
                        return false; // Максимальный стек
                    case BuffStacking.Ignore:
                        return false;
                }
            }

            // Создаём новый бафф
            var newBuff = CreateBuffFromId(buffId, entityId, duration, potency);
            if (newBuff == null) return false;

            // BF-A07: Мгновенные баффы применяются сразу и не добавляются в активный список
            if (newBuff.Application == BuffApplication.Instant)
            {
                _appliedPub.Publish(new BuffAppliedEvent(entityId, buffId, newBuff.Type, 0f, potency));
                // Мгновенный эффект — не сохраняем в списке
                return true;
            }

            buffs.Add(newBuff);
            _appliedPub.Publish(new BuffAppliedEvent(entityId, buffId, newBuff.Type, newBuff.RemainingDuration, potency));
            // BF-A10: Публикуем событие изменения модификатора
            if (newBuff.AffectedStat != null)
            {
                float newMod = GetStatModifier(entityId, newBuff.AffectedStat.Value);
                _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, newBuff.AffectedStat.Value, newMod));
            }
            return true;
        }

        public bool RemoveBuff(string entityId, string buffId)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null) return false;

            int index = buffs.FindIndex(b => b.BuffId == buffId);
            if (index < 0) return false;

            var buff = buffs[index];
            buffs.RemoveAt(index);
            _removedPub.Publish(new BuffRemovedEvent(entityId, buffId, buff.Type));
            // BF-A10: Публикуем событие изменения модификатора
            if (buff.AffectedStat != null)
            {
                float newMod = GetStatModifier(entityId, buff.AffectedStat.Value);
                _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, buff.AffectedStat.Value, newMod));
            }
            return true;
        }

        public void RemoveAllBuffs(string entityId)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null) return;

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs[i];
                _removedPub.Publish(new BuffRemovedEvent(entityId, buff.BuffId, buff.Type));
            }

            buffs.Clear();
            _entityBuffs.Remove(entityId);
        }

        public bool HasBuff(string entityId, string buffId)
        {
            var buffs = GetBuffList(entityId);
            return buffs != null && buffs.Exists(b => b.BuffId == buffId);
        }

        // === IBuffService: Запросы модификаторов ===

        public float GetStatModifier(string entityId, StatType stat)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null || buffs.Count == 0) return 0f;

            return BuffCalculator.CalculateCappedModifier(buffs, stat);
        }

        /// <summary>
        /// Аудит CRIT-1: модификатор в промилле (ЗАПРЕТ 3.9).
        /// Конвертирует float-результат GetStatModifier в промилле: 0.2 → 200, -0.3 → -300.
        /// Для боево1 пайплайна: (1000 + modPermil) / 1000 = множитель.
        /// </summary>
        public int GetStatModifierPermil(string entityId, StatType stat)
        {
            float mod = GetStatModifier(entityId, stat);
            return (int)(mod * 1000f);
        }

        // BF-A04: Исправлена inconsystency единиц — оба типа в диапазоне 0.0-1.0
        public float GetElementResistance(string entityId, Element element)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null) return 0f;

            float total = 0f;
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].Type == BuffType.Vulnerability && buffs[i].Element == element)
                {
                    // Уязвимость = отрицательное сопротивление (уже в долях: -0.3 = -30%)
                    total -= buffs[i].IsPercentage ? buffs[i].TotalValue : buffs[i].TotalValue * 0.01f;
                }
                else if (buffs[i].Type == BuffType.DamageReduction && buffs[i].Element == element)
                {
                    // Снижение урона (нормализуем к 0-1 диапазону)
                    total += buffs[i].IsPercentage ? buffs[i].TotalValue : buffs[i].TotalValue * 0.01f;
                }
            }
            return total;
        }

        // BF-A03: Исправлена логика — маппинг эффектов на типы иммунитетов
        public bool HasImmunity(string entityId, BuffType effectType)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null) return false;

            // Определяем, какой тип иммунитета блокирует данный эффект
            if (!EffectToImmunityMap.TryGetValue(effectType, out var immunityType))
            {
                // Если эффект сам является иммунитетом — проверяем напрямую
                immunityType = effectType;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].Type == immunityType) return true;
            }
            return false;
        }

        public IReadOnlyList<ActiveBuffData> GetActiveBuffs(string entityId)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null || buffs.Count == 0) return _emptyDataList;

            var result = new List<ActiveBuffData>(buffs.Count);
            for (int i = 0; i < buffs.Count; i++)
            {
                result.Add(buffs[i].ToData());
            }
            return result.AsReadOnly();
        }

        // === IBuffService: Тикание ===

        public void TickBuffs(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            List<string> expiredEntities = null;

            foreach (var kvp in _entityBuffs)
            {
                string entityId = kvp.Key;
                var buffs = kvp.Value;

                for (int i = buffs.Count - 1; i >= 0; i--)
                {
                    var buff = buffs[i];

                    // Обновляем таймер
                    if (buff.Application != BuffApplication.Permanent && buff.Application != BuffApplication.Instant)
                    {
                        buff.RemainingDuration -= deltaTime;
                    }

                    // Обработка тиков (DoT/HoT)
                    if (buff.HasTickEffect)
                    {
                        _tickProcessor.ProcessTick(buff, deltaTime);
                    }

                    // Проверяем истечение
                    if (buff.IsExpired)
                    {
                        _expiredPub.Publish(new BuffExpiredEvent(entityId, buff.BuffId, buff.Type));
                        // BF-A10: Публикуем событие изменения модификатора при истечении
                        if (buff.AffectedStat != null)
                        {
                            float newMod = GetStatModifier(entityId, buff.AffectedStat.Value);
                            _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, buff.AffectedStat.Value, newMod));
                        }
                        buffs.RemoveAt(i);
                    }
                }

                if (buffs.Count == 0)
                {
                    expiredEntities ??= new List<string>();
                    expiredEntities.Add(entityId);
                }
            }

            // Убираем пустые списки
            if (expiredEntities != null)
            {
                foreach (var entityId in expiredEntities)
                {
                    _entityBuffs.Remove(entityId);
                }
            }
        }

        // === Внутренние методы ===

        private List<ActiveBuff> GetOrCreateBuffList(string entityId)
        {
            if (!_entityBuffs.TryGetValue(entityId, out var list))
            {
                list = new List<ActiveBuff>();
                _entityBuffs[entityId] = list;
            }
            return list;
        }

        private List<ActiveBuff> GetBuffList(string entityId)
        {
            return _entityBuffs.TryGetValue(entityId, out var list) ? list : null;
        }

        /// <summary>
        /// Создать ActiveBuff из ID баффа.
        /// В будущем будет загружать из BuffData ScriptableObject или JSON.
        /// Пока — создаёт по ID с эвристикой.
        /// </summary>
        private ActiveBuff CreateBuffFromId(string buffId, string entityId, float duration, float potency)
        {
            // Определяем тип баффа по ID (эвристика для прототипа)
            // В продакшене — загрузка из BuffData SO / JSON базы
            var buff = new ActiveBuff
            {
                BuffId = buffId,
                EntityId = entityId,
                Application = BuffApplication.Duration,
                StackingBehavior = BuffStacking.Refresh,
                MaxStacks = 1,
                CurrentStacks = 1,
                IsDebuff = false,
                Element = Element.Neutral
            };

            // Устанавливаем длительность
            if (duration > 0)
            {
                buff.Duration = duration;
                buff.RemainingDuration = duration;
            }
            else
            {
                buff.Duration = 30f; // По умолчанию 30 сек
                buff.RemainingDuration = buff.Duration;
            }

            // Маппинг ID → тип/стата (упрощённый для прототипа)
            MapBuffIdToType(buffId, buff);

            // BF-A02: Сохраняем potency в отдельном поле
            buff.Potency = potency;

            return buff;
        }

        /// <summary>
        /// Маппинг ID баффа на тип и характеристики.
        /// Упрощённая версия — в продакшене заменить на загрузку из BuffData SO.
        /// </summary>
        private static void MapBuffIdToType(string buffId, ActiveBuff buff)
        {
            string id = buffId.ToLowerInvariant();

            if (id.Contains("attack_boost") || id.Contains("rage"))
            {
                buff.Type = BuffType.AttackBoost;
                buff.AffectedStat = StatType.Damage;
                buff.Value = 0.2f;
                buff.IsPercentage = true;
            }
            else if (id.Contains("defense_boost") || id.Contains("iron_skin"))
            {
                buff.Type = BuffType.DefenseBoost;
                buff.AffectedStat = StatType.Defense;
                buff.Value = 0.2f;
                buff.IsPercentage = true;
            }
            else if (id.Contains("speed_boost") || id.Contains("swift"))
            {
                buff.Type = BuffType.SpeedBoost;
                buff.AffectedStat = StatType.Speed;
                buff.Value = 0.2f;
                buff.IsPercentage = true;
            }
            else if (id.Contains("poison"))
            {
                buff.Type = BuffType.Poison;
                buff.IsDebuff = true;
                buff.HasTickEffect = true;
                buff.TickInterval = 1f;
                buff.TickDamage = 10f;
            }
            else if (id.Contains("burn") || id.Contains("fire_dot"))
            {
                buff.Type = BuffType.Burn;
                buff.IsDebuff = true;
                buff.Element = Element.Fire;
                buff.HasTickEffect = true;
                buff.TickInterval = 1f;
                buff.TickDamage = 15f;
            }
            else if (id.Contains("stun"))
            {
                buff.Type = BuffType.Stun;
                buff.IsDebuff = true;
            }
            else if (id.Contains("slow") || id.Contains("ice"))
            {
                buff.Type = BuffType.Slow;
                buff.IsDebuff = true;
                buff.AffectedStat = StatType.Speed;
                buff.Value = -0.3f;
                buff.IsPercentage = true;
            }
            else if (id.Contains("health_regen") || id.Contains("regen"))
            {
                buff.Type = BuffType.HealthRegen;
                buff.HasTickEffect = true;
                buff.TickInterval = 2f;
                buff.TickHealing = 5f;
            }
            else if (id.Contains("qi_restoration") || id.Contains("qi_flux"))
            {
                buff.Type = BuffType.QiRestoration;
                buff.HasTickEffect = true;
                buff.TickInterval = 5f;
                buff.TickHealing = 50f;
            }
            else if (id.Contains("shield"))
            {
                buff.Type = BuffType.Shield;
                buff.Value = 100f;
                buff.IsPercentage = false;
            }
            else
            {
                // BF-A08: Неизвестный ID баффа — предупреждение вместо тихого AttackBoost
                Console.WriteLine($"[BuffService] Неизвестный buffId '{buffId}', применяется модификатор по умолчанию (AttackBoost +10%)");
                buff.Type = BuffType.AttackBoost;
                buff.AffectedStat = StatType.Damage;
                buff.Value = 0.1f;
                buff.IsPercentage = true;
            }
        }

        private static readonly IReadOnlyList<ActiveBuffData> _emptyDataList = new List<ActiveBuffData>().AsReadOnly();
    }
}
