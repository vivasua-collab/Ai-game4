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

            // BUFF-A3 FIX (аудит-4): собираем затронутые статы ДО очистки —
            // после Clear() публикуем StatModifierChangedEvent для каждой
            // (как в RemoveBuff), иначе подписчики статов не видели сброс к 0.
            HashSet<StatType>? affectedStats = null;
            for (int i = 0; i < buffs.Count; i++)
            {
                var st = buffs[i].AffectedStat;
                if (st != null)
                {
                    affectedStats ??= new HashSet<StatType>();
                    affectedStats.Add(st.Value);
                }
            }

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs[i];
                _removedPub.Publish(new BuffRemovedEvent(entityId, buff.BuffId, buff.Type));
            }

            buffs.Clear();
            _entityBuffs.Remove(entityId);

            // BUFF-A3 FIX: модификаторы после очистки = 0 — уведомляем подписчиков.
            if (affectedStats != null)
            {
                foreach (var stat in affectedStats)
                {
                    _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, stat, GetStatModifier(entityId, stat)));
                }
            }
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
        /// AUDIT-0911 BUF-1 FIX: боевой пайплайн (DamageService слои 3a/3b)
        /// ждёт АДДИТИВНЫЙ ПРОЦЕНТ: множитель = (1000 + modPermil)/1000
        /// (контракт «1200 = ×1.2»). Раньше конвертировался результат
        /// CalculateStatModifier — при baseValue=0/flatSum=0 (чисто процентные
        /// баффы: AttackBoost +20%, Slow −30%, шок, перки) он ВСЕГДА 0 →
        /// слои баффов урона/защиты не работали вовсе. Теперь честная
        /// сумма процентных модификаторов (BuffCalculator.CalculatePercentSum),
        /// с клампом в разумный диапазон множителей (−90%…+300%).
        /// </summary>
        public int GetStatModifierPermil(string entityId, StatType stat)
        {
            var buffs = GetBuffList(entityId);
            if (buffs == null || buffs.Count == 0) return 0;

            float percentSum = BuffCalculator.CalculatePercentSum(buffs, stat);
            // Кламп множителя: полная остановка урона — максимум −90%,
            // раздувание от стаков — максимум +300% (мягкий санити-кап).
            if (percentSum < -0.9f) percentSum = -0.9f;
            if (percentSum > 3.0f) percentSum = 3.0f;
            return (int)(percentSum * 1000f);
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

            // BUFF-A1 FIX (аудит-4): снапшот сущностей ДО итерации. Подписчики
            // публикуемых внутри цикла событий (DoT-тик → урон → смерть →
            // RemoveAllBuffs / ApplyBuff новой сущности) мутируют _entityBuffs
            // во время foreach → InvalidOperationException. Снапшот ссылок:
            // свежеприменённые баффы не тикают в этом кадре (корректно).
            var entitySnapshot = new List<KeyValuePair<string, List<ActiveBuff>>>(_entityBuffs);

            foreach (var kvp in entitySnapshot)
            {
                string entityId = kvp.Key;
                var buffs = kvp.Value;

                // BUFF-A1 FIX: итерируем КОПИЮ списка баффов — подписчик может
                // вызвать Clear()/Remove (RemoveAllBuffs при смерти от DoT), и
                // индексация живого списка вышла бы за границы.
                var buffSnapshot = new List<ActiveBuff>(buffs);
                List<ActiveBuff>? expired = null;

                for (int i = buffSnapshot.Count - 1; i >= 0; i--)
                {
                    var buff = buffSnapshot[i];

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
                        expired ??= new List<ActiveBuff>();
                        expired.Add(buff);
                    }
                }

                if (expired != null)
                {
                    // Отложенная мутация: удаляем из ЖИВОГО списка ПОСЛЕ итерации
                    // (Remove по ссылке безвреден, если подписчик уже удалил сам).
                    foreach (var b in expired) buffs.Remove(b);

                    // BF-A10 + BUFF-A1: событие изменения модификатора при истечении.
                    // Пересчёт ПОСЛЕ фактического удаления — иначе newMod включал
                    // уже истёкший бафф (семантика та же, что в RemoveBuff).
                    foreach (var b in expired)
                    {
                        if (b.AffectedStat != null)
                        {
                            float newMod = GetStatModifier(entityId, b.AffectedStat.Value);
                            _statModifierChangedPub.Publish(new StatModifierChangedEvent(entityId, b.AffectedStat.Value, newMod));
                        }
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
        ///
        /// AUDIT-0911 (BUF-2/3/4/5, BOD-4): нормализация potency-семантики
        /// продюсеров. Раньше: (а) duration&lt;0 → «30 сек» вместо Permanent
        /// (ампутационные дебаффы истекали!); (б) producer-potency игнорировался
        /// маппингом: combat_bleed → «AttackBoost +10%» вместо DoT (кровотечение
        /// не ранило), combat_shock/elemental_void_pierce → бафф +урона
        /// раненому; (в) DoT-тики хардкодом (poison 10/burn 15) вместо
        /// 5% maxHP/5% урона продюсера; (г) Value×Potency двойной учёт:
        /// elemental_slow(Value=−0.3, Potency=300) → TotalValue=−90 = −9000%.
        /// Теперь маппинг вбирает producer-potency в Value/TickDamage,
        /// а buff.Potency = 1 (множитель нейтрален; стеки умножают отдельно).
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

            // Устанавливаем длительность.
            // AUDIT-0911 BUF-5 FIX: duration < 0 = Permanent (контракт
            // IBuffService.ApplyBuff: default −1 = «длитcя пока не снят явно»;
            // SeveredDebuffSystem шлёт −1 — ампутационные дебаффы обязаны жить
            // до приживления, а не 30 тиков). duration == 0 — легаси-дефолт 30.
            if (duration > 0)
            {
                buff.Duration = duration;
                buff.RemainingDuration = duration;
            }
            else if (duration < 0)
            {
                buff.Application = BuffApplication.Permanent;
                buff.Duration = 0f;
                buff.RemainingDuration = 0f;
            }
            else
            {
                buff.Duration = 30f; // По умолчанию 30 сек
                buff.RemainingDuration = buff.Duration;
            }

            // Маппинг ID → тип/статы; producer-potency вбирается в Value/TickDamage.
            MapBuffIdToType(buffId, buff, potency);

            // BF-A02: Potency — множитель мощности НАЛОЖЕНИЯ. Маппинг уже
            // учёл producer-potency (см. комментарий метода) → нейтральный 1.
            // Стеки по-прежнему умножают эффект (TotalValue / TickDamage×Stacks).
            buff.Potency = 1f;

            return buff;
        }

        /// <summary>
        /// Маппинг ID баффа на тип и характеристики.
        /// Упрощённая версия — в продакшене заменить на загрузку из BuffData SO.
        ///
        /// AUDIT-0911: potency-семантика продюсеров (см. CreateBuffFromId):
        /// DoT (bleed/burn/poison) — HP урона за тик; проценты (slow/shock/
        /// void_pierce) — промилле (300 = 30%); ампутации (severed_*) — доля
        /// (−0.15); перки — доля (+0.30 проводимости).
        /// </summary>
        private static void MapBuffIdToType(string buffId, ActiveBuff buff, float potency)
        {
            string id = buffId.ToLowerInvariant();

            // === Ампутационные дебаффы (SeveredDebuffSystem, BOD-4) ===
            // potency несёт величину (доля, отрицательная); суффикс — стат.
            if (id.Contains("severed"))
            {
                buff.IsDebuff = true;
                buff.IsPercentage = true;
                if (id.EndsWith("_str"))
                {
                    buff.Type = BuffType.AttackReduction;
                    buff.AffectedStat = StatType.Strength;
                }
                else if (id.EndsWith("_vit"))
                {
                    buff.Type = BuffType.DefenseReduction;
                    buff.AffectedStat = StatType.Vitality;
                }
                else // _agi и прочие — ловкость (таблица: 90% записей)
                {
                    buff.Type = BuffType.SpeedReduction;
                    buff.AffectedStat = StatType.Agility;
                }
                buff.Value = potency != 0f ? potency : -0.15f;
                return;
            }

            // === Перки проводимости (PerkService) — проводимость, не урон ===
            if (id.Contains("perk") || id.Contains("conductivity"))
            {
                buff.Type = BuffType.CastSpeed; // косметика типа; реальный эффект ведёт PerkService (IQiDataProvider)
                buff.AffectedStat = StatType.Conductivity;
                buff.IsPercentage = true;
                buff.Value = potency != 0f ? potency : 0.1f;
                return;
            }

            // === Кровотечение (CombatConsequencesService, BUF-2) ===
            // Раньше не распознавалось → «AttackBoost +10%» IsDebuff=false:
            // кровотечение не наносило урона и не снималось Purify.
            if (id.Contains("bleed"))
            {
                buff.Type = BuffType.Bleed;
                buff.IsDebuff = true;
                buff.HasTickEffect = true;
                buff.TickInterval = 3f;
                buff.TickDamage = potency > 0f ? potency : 5f; // 5% maxHP/тик от продюсера
                return;
            }

            // === Шок (CombatConsequencesService, BUF-3) ===
            // Раненая сущность (<30% HP): −20% к урону (РАНЬШЕ — +10% бафф!).
            if (id.Contains("shock"))
            {
                buff.Type = BuffType.AttackReduction;
                buff.AffectedStat = StatType.Damage;
                buff.IsDebuff = true;
                buff.IsPercentage = true;
                buff.Value = potency > 0f ? -potency / 1000f : -0.2f; // продюсер: 200‰ = −20%
                return;
            }

            // === Пробитие брони Void (ElementalEffectService, BUF-3) ===
            if (id.Contains("void_pierce") || id.Contains("pierce"))
            {
                buff.Type = BuffType.DefenseReduction;
                buff.AffectedStat = StatType.Armor;
                buff.IsDebuff = true;
                buff.IsPercentage = true;
                buff.Value = potency > 0f ? -potency / 1000f : -0.3f; // продюсер: 300‰ = −30% брони
                return;
            }

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
                // AUDIT-0911 BUF-4: тик из продюсера (3% maxHP), не хардкод 10.
                buff.TickDamage = potency > 0f ? potency : 10f;
            }
            else if (id.Contains("burn") || id.Contains("fire_dot"))
            {
                buff.Type = BuffType.Burn;
                buff.IsDebuff = true;
                buff.Element = Element.Fire;
                buff.HasTickEffect = true;
                buff.TickInterval = 1f;
                // AUDIT-0911 BUF-4: тик из продюсера (5% урона атаки), не хардкод 15.
                buff.TickDamage = potency > 0f ? potency : 15f;
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
                // AUDIT-0911: продюсер шлёт промилле (300 = −30%) — раньше
                // Value=−0.3 × Potency=300 → TotalValue=−90 (−9000%!).
                buff.Value = potency > 0f ? -potency / 1000f : -0.3f;
                buff.IsPercentage = true;
            }
            else if (id.Contains("health_regen") || id.Contains("regen"))
            {
                buff.Type = BuffType.HealthRegen;
                buff.HasTickEffect = true;
                buff.TickInterval = 2f;
                buff.TickHealing = potency > 0f ? potency : 5f;
            }
            else if (id.Contains("qi_restoration") || id.Contains("qi_flux"))
            {
                buff.Type = BuffType.QiRestoration;
                buff.HasTickEffect = true;
                buff.TickInterval = 5f;
                buff.TickHealing = potency > 0f ? potency : 50f;
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
