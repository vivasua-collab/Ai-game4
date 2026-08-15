#nullable enable
// Создано: 2026-05-09
// Перечисления формаций — модульная архитектура
// Перенесено из Legacy/FormationData.cs и FormationCoreData.cs
namespace CultivationGame.Core.Data
{
    #region Formation Core Types

    /// <summary>
    /// Тип физического ядра формации.
    /// Disk — переносной (L1-L6), Altar — стационарный (L5-L9).
    /// </summary>
    public enum FormationCoreType
    {
        Disk,       // Диск (портативный)
        Altar,      // Алтарь (стационарный)
        Array,      // Массив (расширение)
        Totem,      // Тотем (расширение)
        Seal        // Печать (расширение)
    }

    /// <summary>
    /// Материал ядра формации.
    /// Определяет проводимость, ёмкость, доступные уровни.
    /// </summary>
    public enum FormationCoreVariant
    {
        Stone,          // Камень (L1-L2)
        Jade,           // Нефрит (L2-L4)
        Iron,           // Железо (L3-L5)
        SpiritIron,     // Духовное железо (L4-L6)
        Crystal,        // Кристалл (L6-L7)
        StarMetal,      // Звёздный металл (расширение)
        VoidMatter      // Пустотная материя (расширение)
    }

    #endregion

    #region Formation Types

    /// <summary>
    /// Тип формации — определяет назначение и эффекты.
    /// </summary>
    public enum FormationType
    {
        Barrier,            // Защитный барьер
        Trap,               // Ловушка
        Amplification,      // Усиление союзников
        Suppression,        // Подавление врагов
        Gathering,          // Сбор ресурсов
        Detection,          // Обнаружение
        Teleportation,      // Телепортация
        Summoning           // Призыв
    }

    /// <summary>
    /// Размер формации — определяет ёмкость, радиус и стоимость.
    /// </summary>
    public enum FormationSize
    {
        Small,      // 3×3 м, радиус 50 м, множитель ×10
        Medium,     // 10×10 м, радиус 200 м, множитель ×50
        Large,      // 30×30 м, радиус 600 м, множитель ×200
        Great,      // 100×100 м, радиус 1000 м, множитель ×1000
        Heavy       // 300×300 м, радиус 5000 м, множитель ×10000 (только L6+)
    }

    #endregion

    #region Formation Lifecycle

    /// <summary>
    /// Стадия жизненного цикла формации.
    /// None → Drawing → Filling → Active → Depleted
    /// </summary>
    public enum FormationStage
    {
        None,       // Неактивна
        Drawing,    // Прорисовка контура (затраты contourQi)
        Filling,    // Наполнение ёмкости (внесение Ци участниками)
        Active,     // Активна (эффекты применяются, Ци расходуется)
        Depleted    // Истощена (QiPool = 0, требует перезарядки)
    }

    #endregion

    #region Formation Effects

    /// <summary>
    /// Тип эффекта формации.
    /// Определяет, как формация воздействует на сущности в зоне действия.
    /// </summary>
    public enum FormationEffectType
    {
        Buff,       // Бафф союзников
        Debuff,     // Дебафф врагов
        Damage,     // Периодический урон врагам
        Heal,       // Периодическое исцеление союзников
        Control,    // Контроль (заморозка, оглушение и т.д.)
        Shield,     // Щит (поглощение урона)
        Summon      // Призыв существ
    }

    /// <summary>
    /// Тип контроля формации.
    /// </summary>
    public enum ControlType
    {
        None,       // Нет контроля
        Freeze,     // Заморозка (полная остановка)
        Slow,       // Замедление
        Root,       // Обездвиживание
        Stun,       // Оглушение
        Silence,    // Безмолвие (запрет техник)
        Blind       // Ослепление (снижение точности)
    }

    #endregion
}
