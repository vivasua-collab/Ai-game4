#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Структуры данных модуля зарядников Ци.
// Адаптировано из Legacy/Charger/ChargerData.cs + ChargerSlot.cs
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Charger
{
    // === Перечисления зарядника (модуль-специфичные) ===

    /// <summary>
    /// Форм-фактор зарядника.
    /// Источник: CHARGER_SYSTEM.md §1.1
    /// </summary>
    public enum ChargerFormFactor
    {
        Belt,       // Пояс-накопитель (3-8 слотов, буфер 500)
        Bracelet,   // Браслет-накопитель (2-4 слота, буфер 200)
        Necklace,   // Ожерелье-накопитель (1-3 слота, буфер 1000)
        Ring,       // Кольцо-накопитель (1 слот, буфер 50)
        Backpack    // Ранец-накопитель (6-15 слотов, буфер 2000)
    }

    /// <summary>
    /// Назначение зарядника.
    /// Источник: CHARGER_SYSTEM.md §1.2
    /// </summary>
    public enum ChargerPurpose
    {
        Accumulation,   // Медитационный (×0.8 скорость, ×1.5 буфер)
        Combat,         // Боевой (×1.5 скорость, ×0.7 буфер)
        Hybrid          // Универсальный (×1.0 скорость, ×1.0 буфер)
    }

    /// <summary>
    /// Материал зарядника.
    /// Источник: CHARGER_SYSTEM.md §6.1
    /// </summary>
    public enum ChargerMaterial
    {
        Iron,           // Тир 1: Проводимость 5, Прочность 100
        Copper,         // Тир 1: Проводимость 8, Прочность 80
        Silver,         // Тир 2: Проводимость 15, Прочность 90
        SpiritIron,     // Тир 3: Проводимость 25, Прочность 200
        Jade,           // Тир 3: Проводимость 20, Прочность 150
        SpiritJade,     // Тир 3: Проводимость 35, Прочность 300
        DragonBone,     // Тир 4: Проводимость 50, Прочность 1000
        VoidMatter      // Тир 5: Проводимость 100, Прочность 2000
    }

    /// <summary>
    /// Качество камня Ци.
    /// Выровнено с EquipmentGrade (Damaged/Common/Refined/Perfect/Transcendent)
    /// </summary>
    public enum QiStoneQuality
    {
        Damaged,        // Повреждённый (×0.5)
        Common,         // Обычный (×1.0)
        Refined,        // Очищенный (×1.5)
        Perfect,        // Совершенный (×2.5)
        Transcendent    // Трансцендентный (×4.0)
    }

    /// <summary>
    /// Размер камня Ци.
    /// </summary>
    public enum QiStoneSize
    {
        Tiny,       // Крошечный (100 Ци)
        Small,      // Малый (500 Ци)
        Medium,     // Средний (2000 Ци)
        Large,      // Большой (10000 Ци)
        Huge        // Огромный (50000 Ци)
    }

    // === Структуры данных ===

    /// <summary>
    /// Данные слота для камня Ци.
    /// </summary>
    [Serializable]
    public struct ChargerSlotConfig
    {
        public int Index;
        public QiStoneQuality MinQualityRequired;
        public QiStoneSize MaxSizeAllowed;
        public bool IsActive;
        public bool IsSealed;
        public float AbsorptionBonus;       // Бонус поглощения (0-1)
        public float QiRetention;           // Сохранение Ци (0-1, обычно 0.9-1.0)
    }

    /// <summary>
    /// Данные буфера Ци зарядника.
    /// </summary>
    [Serializable]
    public class ChargerBufferConfig
    {
        public long Capacity;               // Ёмкость буфера (50-2000)
        public float Conductivity;          // Проводимость (5-100 ед/сек)
        public float EfficiencyLoss;        // Потери (0.1 = 10%)
    }

    /// <summary>
    /// Статистика использования буфера (результат операции).
    /// </summary>
    public struct ChargerBufferResult
    {
        public long QiFromCore;             // Ци из ядра практика
        public long QiFromBuffer;           // Ци из буфера зарядника
        public long QiRemaining;            // Остаток в буфере
        public long QiLost;                 // Потери (10%)
        public bool WasBufferUsed;          // Использован ли буфер
        public bool WasBufferDepleted;      // Опустошён ли буфер
    }

    // === Утилиты ===

    /// <summary>
    /// Статические конфигурации зарядника по форм-фактору и материалу.
    /// Извлечено из Legacy ChargerData.cs.
    /// </summary>
    public static class ChargerConfigs
    {
        /// <summary>Получить базовое количество слотов по форм-фактору</summary>
        public static (int minSlots, int maxSlots, int baseBuffer) GetFormFactorConfig(ChargerFormFactor ff)
        {
            return ff switch
            {
                ChargerFormFactor.Belt => (3, 8, 500),
                ChargerFormFactor.Bracelet => (2, 4, 200),
                ChargerFormFactor.Necklace => (1, 3, 1000),
                ChargerFormFactor.Ring => (1, 1, 50),
                ChargerFormFactor.Backpack => (6, 15, 2000),
                _ => (1, 3, 100)
            };
        }

        /// <summary>Получить проводимость материала</summary>
        public static float GetMaterialConductivity(ChargerMaterial mat)
        {
            return mat switch
            {
                ChargerMaterial.Iron => 5f,
                ChargerMaterial.Copper => 8f,
                ChargerMaterial.Silver => 15f,
                ChargerMaterial.SpiritIron => 25f,
                ChargerMaterial.Jade => 20f,
                ChargerMaterial.SpiritJade => 35f,
                ChargerMaterial.DragonBone => 50f,
                ChargerMaterial.VoidMatter => 100f,
                _ => 5f
            };
        }

        /// <summary>Получить прочность материала</summary>
        public static int GetMaterialDurability(ChargerMaterial mat)
        {
            return mat switch
            {
                ChargerMaterial.Iron => 100,
                ChargerMaterial.Copper => 80,
                ChargerMaterial.Silver => 90,
                ChargerMaterial.SpiritIron => 200,
                ChargerMaterial.Jade => 150,
                ChargerMaterial.SpiritJade => 300,
                ChargerMaterial.DragonBone => 1000,
                ChargerMaterial.VoidMatter => 2000,
                _ => 100
            };
        }

        /// <summary>Получить сохранение Ци материала (%)</summary>
        public static float GetMaterialQiRetention(ChargerMaterial mat)
        {
            return mat switch
            {
                ChargerMaterial.Iron => 0.95f,
                ChargerMaterial.Copper => 0.90f,
                ChargerMaterial.Silver => 0.92f,
                ChargerMaterial.SpiritIron => 0.98f,
                ChargerMaterial.Jade => 0.97f,
                ChargerMaterial.SpiritJade => 0.99f,
                ChargerMaterial.DragonBone => 0.995f,
                ChargerMaterial.VoidMatter => 1.0f,
                _ => 0.95f
            };
        }

        /// <summary>Множитель скорости по назначению</summary>
        public static float GetPurposeSpeedMultiplier(ChargerPurpose purpose)
        {
            return purpose switch
            {
                ChargerPurpose.Accumulation => 0.8f,
                ChargerPurpose.Combat => 1.5f,
                ChargerPurpose.Hybrid => 1.0f,
                _ => 1.0f
            };
        }

        /// <summary>Множитель буфера по назначению</summary>
        public static float GetPurposeBufferMultiplier(ChargerPurpose purpose)
        {
            return purpose switch
            {
                ChargerPurpose.Accumulation => 1.5f,
                ChargerPurpose.Combat => 0.7f,
                ChargerPurpose.Hybrid => 1.0f,
                _ => 1.0f
            };
        }

        /// <summary>Базовое Ци камня по размеру</summary>
        public static long GetStoneBaseQi(QiStoneSize size)
        {
            return size switch
            {
                QiStoneSize.Tiny => 100,
                QiStoneSize.Small => 500,
                QiStoneSize.Medium => 2000,
                QiStoneSize.Large => 10000,
                QiStoneSize.Huge => 50000,
                _ => 500
            };
        }

        /// <summary>Множитель качества камня</summary>
        public static float GetStoneQualityMultiplier(QiStoneQuality quality)
        {
            return quality switch
            {
                QiStoneQuality.Damaged => 0.5f,
                QiStoneQuality.Common => 1.0f,
                QiStoneQuality.Refined => 1.5f,
                QiStoneQuality.Perfect => 2.5f,
                QiStoneQuality.Transcendent => 4.0f,
                _ => 1.0f
            };
        }

        /// <summary>Базовая скорость высвобождения камня</summary>
        public static float GetStoneBaseRate(QiStoneSize size)
        {
            return size switch
            {
                QiStoneSize.Tiny => 50f,
                QiStoneSize.Small => 80f,
                QiStoneSize.Medium => 120f,
                QiStoneSize.Large => 160f,
                QiStoneSize.Huge => 200f,
                _ => 80f
            };
        }
    }
}
