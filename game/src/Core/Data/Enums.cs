#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-03-05 12:00:00 UTC — Task 2.2: WeaponHandType.None для зарядников и брони
// Редактировано: 2026-05-09 05:15:31 UTC — Phase 5: добавлены BuffType, BuffApplication, BuffStacking
// Редактировано: 2026-05-09 16:00:00 UTC — Phase 9: добавлены NPCRole, NPCAIState
// Редактировано: 2026-05-09 16:26:00 UTC — Phase 10: добавлены PlayerSleepState, PlayerStance
// Редактировано: 2026-05-10 07:36:53 UTC — аудит P0-02: StorageType перенесён из IInventoryService.cs
// Редактировано: 2026-05-18 — Body доработка: SizeClass, BodyPartFunction, VitalityScalingMode, расширение BodyPartType, StatDomain
// Все перечисления проекта — модулярная архитектура
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) — namespace adapted to CultivationGame.Core.Data
namespace CultivationGame.Core.Data
{
    #region Cultivation

    /// <summary>
    /// Этапы развития смертного (до культивации)
    /// </summary>
    public enum MortalStage
    {
        None = 0,           // Не применимо (практик)
        Newborn = 1,        // Новорождённый (0-7 лет)
        Child = 2,          // Ребёнок (7-16 лет)
        Adult = 3,          // Взрослый (16-30 лет)
        Mature = 4,         // Зрелый (30-50 лет)
        Elder = 5,          // Старец (50+ лет)
        Awakening = 9       // Точка пробуждения
    }

    /// <summary>
    /// Уровни культивации (1-10)
    /// </summary>
    public enum CultivationLevel
    {
        None = 0,               // Смертный (без ядра)
        AwakenedCore = 1,       // Пробуждённое Ядро
        LifeFlow = 2,           // Течение Жизни
        InternalFire = 3,       // Пламя Внутреннего Огня
        BodySpiritUnion = 4,    // Объединение Тела и Духа
        HeartOfHeaven = 5,      // Сердце Небес
        VeilBreaker = 6,        // Разрыв Пелены
        EternalRing = 7,        // Вечное Кольцо
        VoiceOfHeaven = 8,      // Глас Небес
        ImmortalCore = 9,       // Бессмертное Ядро
        Ascension = 10          // Вознесение
    }

    /// <summary>
    /// Тип пробуждения ядра
    /// </summary>
    public enum AwakeningType
    {
        None,               // Не пробуждён
        Natural,            // Естественное (спонтанное)
        Guided,             // Направленное (с учителем)
        Artifact,           // Артефактное (пилюля/камень)
        Forced              // Насильственное (рискованное)
    }

    /// <summary>
    /// Типы перков для NPC (Волна 4: перенесён из Modules.NPC.Data для устранения Core→Module зависимости).
    /// Каждый перк предоставляет постоянный бонус к проводимости.
    /// </summary>
    public enum PerkType
    {
        None = 0,
        GoldenBody = 1,       // Золотое качество тела — +30% проводимости
        MeridianTempering = 2, // Закалка меридиан — +15% проводимости
        CelestialChannels = 3  // Небесные каналы — +20% проводимости
    }

    /// <summary>
    /// Качество ядра культивации
    /// </summary>
    public enum CoreQuality
    {
        Fragmented = 1,     // Осколочное
        Cracked = 2,        // Треснутое
        Flawed = 3,         // С изъяном
        Normal = 4,         // Нормальное
        Refined = 5,        // Очищенное
        Perfect = 6,        // Совершенное
        Transcendent = 7    // Трансцендентное
    }

    #endregion

    #region Elements

    /// <summary>
    /// Элементы (стихии).
    /// Стихии (8 элементов):
    /// - neutral (Нейтральный) — чистый Ци
    /// - fire (Огонь) — горение, DoT
    /// - water (Вода) — замедление, контроль
    /// - earth (Земля) — оглушение, стан
    /// - air (Воздух) — отталкивание
    /// - lightning (Молния) — цепной урон
    /// - void (Пустота) — пробитие, антимагия
    /// - poison (Яд) — DoT, дебаффы (особая стихия)
    /// </summary>
    public enum Element
    {
        Neutral,    // Нейтральный
        Fire,       // Огонь
        Water,      // Вода
        Earth,      // Земля
        Air,        // Воздух
        Lightning,  // Молния
        Void,       // Пустота
        Light,      // Свет
        Poison      // Яд (особая стихия)
    }

    /// <summary>
    /// Тип урона
    /// </summary>
    public enum DamageType
    {
        Physical,       // Физический
        Qi,             // Ци
        Elemental,      // Элементальный
        Pure,           // Чистый (игнорирует защиту)
        Void            // Пустотный
    }

    #endregion

    #region Buffs

    /// <summary>
    /// Типы баффов/дебаффов.
    /// ВАЖНО: Первичные характеристики (STR, AGI, INT, VIT) отсутствуют!
    /// ⛔ ConductivityBoost УДАЛЁН — используй формации для environmentMult.
    /// </summary>
    public enum BuffType
    {
        // === ВТОРИЧНЫЕ ХАРАКТЕРИСТИКИ ===
        AttackBoost,        // +X% к урону
        DefenseBoost,       // +X% к защите
        SpeedBoost,         // +X% скорость
        CriticalChance,     // +X% шанс крита
        CriticalDamage,     // +X% крит урон
        Evasion,            // +X% уклонение

        // === РЕГЕНЕРАЦИЯ ===
        HealthRegen,        // Регенерация HP
        QiRestoration,      // Восстановление Ци (НЕ регенерация микроядра!)
        StaminaRegen,       // Регенерация выносливости

        // === ЗАЩИТА ===
        Shield,             // Щит (поглощение урона)
        DamageReduction,    // Снижение урона

        // === ИММУНИТЕТ ===
        ImmunityPoison,     // Иммунитет к яду
        ImmunityStun,       // Иммунитет к оглушению
        ImmunitySlow,       // Иммунитет к замедлению

        // === УСКОРЕНИЕ ===
        AttackSpeed,        // Скорость атаки
        CastSpeed,          // Скорость каста

        // === ДЕБАФФЫ: снижение ===
        AttackReduction,    // -X% к урону
        DefenseReduction,   // -X% к защите
        SpeedReduction,     // -X% скорость

        // === ДЕБАФФЫ: DoT ===
        Poison,             // Отравление
        Burn,               // Горение
        Bleed,              // Кровотечение
        Freeze,             // Заморозка

        // === ДЕБАФФЫ: контроль ===
        Stun,               // Оглушение
        Slow,               // Замедление
        Blind,              // Ослепление
        Silence,            // Безмолвие

        // === ДЕБАФФЫ: специальные ===
        Curse,              // Проклятие
        Vulnerability       // Уязвимость к элементу
    }

    /// <summary>
    /// Способ применения баффа.
    /// </summary>
    public enum BuffApplication
    {
        Instant,            // Мгновенный эффект
        Duration,           // Длительный эффект
        Permanent,          // Постоянный (пока не снят)
        Stacking,           // Накапливающийся
        Refreshing          // Обновляет длительность
    }

    /// <summary>
    /// Поведение при повторном применении.
    /// </summary>
    public enum BuffStacking
    {
        Replace,            // Заменить
        Refresh,            // Обновить таймер
        Stack,              // Добавить стек
        Ignore              // Игнорировать
    }

    #endregion

    #region Techniques

    /// <summary>
    /// Тип техники
    /// Combat | Боевая
    /// Cultivation | Культивация
    /// Defense | Защитная
    /// Support | Поддержка
    /// Healing | Исцеление
    /// Movement | Перемещение
    /// Sensory | Восприятие
    /// Curse | Проклятие
    /// Poison | Яд
    /// Formation | Формация
    /// </summary>
    public enum TechniqueType
    {
        Combat,         // Боевая
        Cultivation,    // Культивация
        Defense,        // Защитная
        Support,        // Поддержка
        Healing,        // Исцеление
        Movement,       // Перемещение
        Sensory,        // Восприятие
        Curse,          // Проклятие
        Poison,         // Яд
        Formation       // Формация
    }

    /// <summary>
    /// Подтип защитной техники
    /// </summary>
    public enum DefenseSubtype
    {
        None,       // Не защитная
        Block,      // Блок
        Parry,      // Парирование
        Shield,     // Щит (активирует Shield-режим Qi Buffer)
        Dodge,      // Уклонение
        Reflect     // Отражение
    }

    /// <summary>
    /// Подтип боевой техники
    /// </summary>
    public enum CombatSubtype
    {
        None,
        MeleeStrike,        // Удар телом
        MeleeWeapon,        // Удар с оружием
        RangedProjectile,   // Снаряд
        RangedBeam,         // Луч
        RangedAoe,          // Область
        DefenseBlock,       // Блок
        DefenseShield,      // Щит
        DefenseDodge        // Уклонение
    }

    /// <summary>
    /// Грейд техники (качество)
    /// Common ×1.0 | Refined ×1.3 | Perfect ×1.6 | Transcendent ×2.0
    /// Стоимость Ци всегда ×1.0 — не зависит от Grade!
    /// </summary>
    public enum TechniqueGrade
    {
        Common,         // Обычная (×1.0)
        Refined,        // Очищенная (×1.3)
        Perfect,        // Совершенная (×1.6)
        Transcendent    // Трансцендентная (×2.0)
    }

    #endregion

    #region Body

    /// <summary>
    /// Тип души (первичная классификация существ).
    /// Иерархия: SoulType (L1) → Morphology (L2) → Species (L3)
    /// </summary>
    public enum SoulType
    {
        Character,      // Персонаж (органика + полное сознание)
        Creature,       // Существо (органика + инстинкты)
        Spirit,         // Дух (эфирное тело + сознание)
        Artifact,       // Артефакт (минерал + простое сознание)
        Construct       // Конструкт (искусственное тело)
    }

    /// <summary>
    /// Морфология тела (внешняя форма)
    /// </summary>
    public enum Morphology
    {
        Humanoid,       // Гуманоид (2 руки, 2 ноги)
        Quadruped,      // Четвероногое
        Bird,           // Крылатое
        Serpentine,     // Змееподобное
        Arthropod,      // Членистоногое
        Amorphous,      // Бесформенное
        HybridCentaur,  // Кентавр
        HybridMermaid,  // Русалка
        HybridHarpy,    // Гарпия
        HybridLamia     // Ламия
    }

    /// <summary>
    /// Материал тела
    /// </summary>
    public enum BodyMaterial
    {
        Organic,        // Органика (снижение 0%)
        Scaled,         // Чешуя (снижение 30%)
        Chitin,         // Хитин (снижение 20%)
        Mineral,        // Минерал (снижение 50%)
        Ethereal,       // Эфир (снижение 70% физики)
        Chaos           // Хаос (переменное)
        // Примечание: Конструкты НЕ имеют собственного материала.
        // Их материал определяется составом (Mineral, Organic и т.д.)
        // См. ENTITY_TYPES.md §5
    }

    /// <summary>
    /// Класс размера сущности.
    /// Источник: BODY_SYSTEM.md §"Классы размера"
    /// </summary>
    public enum SizeClass
    {
        Tiny,           // < 30 см,  HP ×0.3, STR ×0.1
        Small,          // 30-60 см, HP ×0.5, STR ×0.3
        Medium,         // 60-180 см,HP ×1.0, STR ×1.0
        Large,          // 1.8-3 м,  HP ×1.5, STR ×2.0
        Huge,           // 3-10 м,   HP ×2.0, STR ×5.0
        Gargantuan,     // 10-30 м,  HP ×3.0, STR ×15.0
        Colossal        // 30+ м,    HP ×5.0, STR ×50.0
    }

    /// <summary>
    /// Функции части тела (комбинируемые флаги).
    /// Источник: BODY_SYSTEM.md §"Части тела"
    /// </summary>
    [System.Flags]
    public enum BodyPartFunction
    {
        None          = 0,
        Sensory       = 1 << 0,  // Голова — зрение, слух
        Breathing     = 1 << 1,  // Голова — дыхание
        Circulation   = 1 << 2,  // Торс, Сердце — кровообращение
        Digestion     = 1 << 3,  // Торс — пищеварение
        Manipulation  = 1 << 4,  // Руки, Кисти — манипуляция
        Movement      = 1 << 5,  // Ноги, Ступни — передвижение
        Flight        = 1 << 6,  // Крылья — полёт
        Balance       = 1 << 7,  // Хвост — баланс
        Venom         = 1 << 8,  // Хелицеры — яд
        WebProduction = 1 << 9   // Педипальпы — паутина
    }

    /// <summary>
    /// Режим масштабирования HP от Vitality.
    /// Источник: BODY_SYSTEM.md §"Живучесть"
    /// </summary>
    public enum VitalityScalingMode
    {
        Standard,       // hpMultiplier = 1 + (Vit - 10) × 0.05 — гуманоиды, четвероногие
        Amorphous,      // HP = Qi (Core + Essence), не масштабируется от Vit
        Construct       // HP = baseHP × sizeMultiplier, Vit не влияет
    }

    /// <summary>
    /// Домен характеристики (тело или душа).
    /// Источник: BODY_SYSTEM.md §"Привязка характеристик к телу и душе"
    /// STR/AGI/VIT → Body, INT → Soul
    /// </summary>
    public enum StatDomain
    {
        Body,           // Физические характеристики (STR, AGI, VIT)
        Soul            // Духовные характеристики (INT)
    }

    /// <summary>
    /// Часть тела (все морфологии)
    /// All — специальное значение: усиление применяется ко всем частям тела
    /// </summary>
    public enum BodyPartType
    {
        All = -1,           // Все части (специальный селектор для усилений)

        // === Гуманоид (существующие) ===
        Head,           // Голова
        Torso,          // Торс
        Heart,          // Сердце
        LeftArm,        // Левая рука
        RightArm,       // Правая рука
        LeftLeg,        // Левая нога
        RightLeg,       // Правая нога
        LeftHand,       // Левая кисть
        RightHand,      // Правая кисть
        LeftFoot,       // Левая стопа
        RightFoot,      // Правая стопа

        // === Четвероногие (Quadruped) ===
        FrontLeftLeg,   // Передняя левая нога
        FrontRightLeg,  // Передняя правая нога
        BackLeftLeg,    // Задняя левая нога
        BackRightLeg,   // Задняя правая нога
        Tail,           // Хвост

        // === Птицы (Bird) ===
        LeftWing,       // Левое крыло
        RightWing,      // Правое крыло
        BirdTail,       // Хвост птицы

        // === Змееподобные (Serpentine) ===
        BodySegment1,   // Сегмент тела 1
        BodySegment2,   // Сегмент тела 2
        SerpentineTail, // Хвост змеи

        // === Членистоногие (Arthropod) ===
        Cephalothorax,  // Головогрудь
        Abdomen,        // Брюшко
        Leg1,           // Нога 1
        Leg2,           // Нога 2
        Leg3,           // Нога 3
        Leg4,           // Нога 4
        Leg5,           // Нога 5
        Leg6,           // Нога 6
        Leg7,           // Нога 7
        Leg8,           // Нога 8
        Pedipalps,      // Педипальпы
        Chelicerae,     // Хелицеры

        // === Бесформенные (Spirit/Amorphous) ===
        Core,           // Ядро сознания
        Essence         // Эфирное тело
    }

    /// <summary>
    /// Состояние части тела
    /// Healthy — здорова
    /// Bruised — ушиблена
    /// Wounded — ранена
    /// Disabled — парализована (красная HP = 0)
    /// Severed — отрублена (чёрная HP = 0)
    /// </summary>
    public enum BodyPartState
    {
        Healthy,        // Здорова
        Bruised,        // Ушиблена
        Wounded,        // Ранена
        Disabled,       // Парализована
        Severed         // Отрублена
    }

    #endregion

    #region Equipment

    /// <summary>
    /// Слот экипировки
    /// Видимые слоты куклы (7): Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
    /// Скрытые слоты (заглушки): Amulet, RingLeft1/2, RingRight1/2, Charger, Hands, Back
    /// </summary>
    public enum EquipmentSlot
    {
        None,
        // === Видимые слоты куклы ===
        Head,           // Голова — шлем, шапка, корона
        Torso,          // Торс — нагрудник, рубашка, роба
        Belt,           // Пояс — ремень, пояс зелий, зарядник-пояс
        Legs,           // Ноги — поножи, штаны
        Feet,           // Ступни — сабатоны, сапоги
        WeaponMain,     // Основная рука — одноручное или щит
        WeaponOff,      // Вторичная рука — одноручное, щит или инструмент
        // === Скрытые слоты (заглушки) ===
        Amulet,         // Амулет
        RingLeft1,      // Кольцо левое 1
        RingLeft2,      // Кольцо левое 2
        RingRight1,     // Кольцо правое 1
        RingRight2,     // Кольцо правое 2
        Charger,        // Зарядник Ци
        Hands,          // Перчатки
        Back            // Плащ/спина
    }

    /// <summary>
    /// Категория предмета
    /// </summary>
    public enum ItemCategory
    {
        Weapon,         // Оружие
        Armor,          // Броня
        Accessory,      // Аксессуар
        Consumable,     // Расходник
        Material,       // Материал
        Technique,      // Свиток техники
        Quest,          // Квестовый предмет
        Misc            // Разное
    }

    /// <summary>
    /// Редкость предмета
    /// </summary>
    public enum ItemRarity
    {
        Common,         // Обычный (50%)
        Uncommon,       // Необычный (30%)
        Rare,           // Редкий (15%)
        Epic,           // Эпический (4%)
        Legendary,      // Легендарный (1%)
        Mythic          // Мифический (0.1%)
    }

    /// <summary>
    /// Грейд экипировки (качество)
    /// Множители ЭФФЕКТИВНОСТИ: Damaged ×0.5 | Common ×1.0 | Refined ×1.3 | Perfect ×1.6 | Transcendent ×2.0
    /// См. EquipmentGradeMultipliers в Constants.cs
    /// </summary>
    public enum EquipmentGrade
    {
        Damaged,        // Повреждённый (×0.5)
        Common,         // Обычный (×1.0)
        Refined,        // Очищенный (×1.3)
        Perfect,        // Совершенный (×1.6)
        Transcendent    // Трансцендентный (×2.0)
    }

    /// <summary>
    /// Состояние прочности
    /// Pristine 100% | Good 80-99% | Worn 60-79% | Damaged 20-59% | Broken <20%
    /// </summary>
    public enum DurabilityCondition
    {
        Pristine,       // 100% — Идеальное
        Good,           // 80-99% — Хорошее
        Worn,           // 60-79% — Изношенное
        Damaged,        // 20-59% — Повреждённое
        Broken          // <20% — Сломанное
    }

    /// <summary>
    /// Флаг вложения — куда можно поместить предмет
    /// </summary>
    public enum NestingFlag
    {
        None,       // Нельзя поместить ни в какое хранилище
        Spirit,     // Можно ТОЛЬКО в духовное хранилище
        Ring,       // Можно ТОЛЬКО в кольцо хранения
        Any         // Можно в любое хранилище (по умолчанию)
    }

    /// <summary>
    /// Тип хвата оружия — определяет, сколько слотов рук занимает.
    /// None — для предметов, не занимающих руки (броня, аксессуары, зарядник).
    /// </summary>
    public enum WeaponHandType
    {
        OneHand = 0,    // Одноручное — занимает 1 слот
        TwoHand = 1,    // Двуручное — занимает оба слота (WeaponMain + WeaponOff)
        None = 2        // Не применимо (броня, аксессуары, зарядник)
    }

    /// <summary>
    /// Тип хранилища предметов.
    /// Перенесён из IInventoryService.cs (аудит P0-02: 1 интерфейс = 1 файл).
    /// </summary>
    public enum StorageType
    {
        Spirit,     // Духовное хранилище
        Ring        // Кольцо хранения
    }

    #endregion

    #region Materials

    /// <summary>
    /// Тир материала (1-5)
    /// </summary>
    public enum MaterialTier
    {
        Tier1 = 1,      // Обычные материалы (Iron, Leather, Cloth)
        Tier2 = 2,      // Качественные материалы (Steel, Silk)
        Tier3 = 3,      // Духовные материалы (Spirit Iron, Jade)
        Tier4 = 4,      // Небесные материалы (Star Metal, Dragon Bone)
        Tier5 = 5       // Первородные материалы (Void Matter)
    }

    /// <summary>
    /// Категория материала
    /// </summary>
    public enum MaterialCategory
    {
        Metal,          // Металл
        Leather,        // Кожа
        Cloth,          // Ткань
        Wood,           // Дерево
        Bone,           // Кость
        Crystal,        // Кристалл
        Gem,            // Драгоценный камень
        Organic,        // Органический
        Spirit,         // Духовный
        Void            // Пустотный
    }

    #endregion

    #region NPC

    /// <summary>
    /// Категория NPC (определяет сохранение)
    /// </summary>
    public enum NPCCategory
    {
        Temp,           // Временный (только в памяти)
        Plot,           // Сюжетный (сохраняется в файл)
        Unique          // Уникальный (полная история)
    }

    /// <summary>
    /// Отношение NPC к игроку (числовое -100..+100)
    /// </summary>
    public enum Attitude
    {
        Hatred,         // -100..-51 — атака без предупреждения
        Hostile,        // -50..-21  — атака если спровоцирован
        Unfriendly,     // -20..-10  — избегание
        Neutral,        // -9..9     — безразличие
        Friendly,       // 10..49    — помощь, торговля
        Allied,         // 50..79    — лояльность
        SwornAlly       // 80..100   — самопожертвование
    }

    /// <summary>
    /// Характер NPC (комбинируемые черты)
    /// </summary>
    [System.Flags]
    public enum PersonalityTrait
    {
        None        = 0,
        Aggressive  = 1 << 0,   // Склонен к атаке, первый удар
        Cautious    = 1 << 1,   // Избегает рисков, защита
        Treacherous = 1 << 2,   // Может предать при возможности
        Ambitious   = 1 << 3,   // Ищет власть, лидерство
        Loyal       = 1 << 4,   // Не предаёт никогда
        Pacifist    = 1 << 5,   // Избегает боя
        Curious     = 1 << 6,   // Исследует, задаёт вопросы
        Vengeful    = 1 << 7    // Помнит обиды, мстит
    }

    /// <summary>
    /// Роль NPC (определяет поведение по умолчанию)
    /// </summary>
    public enum NPCRole
    {
        Monster,        // Монстр — агрессия, блуждание
        Guard,          // Страж — патруль, защита
        Merchant,       // Торговец — торговля
        Cultivator,     // Культиатор — медитация, развитие
        Elder,          // Старейшина — обучение, мудрость
        Disciple,       // Ученик — обучение, практика
        Enemy,          // Враг — агрессия, атака
        Passerby        // Прохожий — бездействие
    }

    /// <summary>
    /// Состояние AI NPC
    /// </summary>
    public enum NPCAIState
    {
        Idle,           // Бездействие
        Wandering,      // Случайное блуждание
        Patrolling,     // Патруль по точкам
        Following,      // Следование за целью
        Fleeing,        // Бегство
        Attacking,      // Атака цели
        Defending,      // Защита
        Meditating,     // Медитация
        Cultivating,    // Культивация
        Resting,        // Отдых
        Trading,        // Торговля
        Talking,        // Разговор
        Working,        // Работа
        Searching,      // Поиск
        Guarding        // Охрана
    }

    /// <summary>
    /// Тип отношения между фракциями
    /// </summary>
    public enum FactionRelationType
    {
        Ally,           // Союзник
        Enemy,          // Враг
        Neutral,        // Нейтрал
        Vassal,         // Вассал
        Overlord,       // Сюзерен
        Rival           // Соперник
    }

    #endregion

    #region World

    /// <summary>
    /// Тип локации
    /// </summary>
    public enum LocationType
    {
        Region,         // Регион (большая область)
        Area,           // Область (часть региона)
        Building,       // Здание
        Room,           // Комната
        Dungeon,        // Подземелье
        Secret,         // Секретная область
        // Additional location categories (migrated from Ai-game3):
        Village,        // Деревня
        Farm,           // Ферма / тестовый полигон
        WildLands,      // Дикие земли
        Sect,           // Секта
        City            // Город
    }

    /// <summary>
    /// Тип биома (мировая местность)
    /// </summary>
    public enum BiomeType
    {
        Mountains,      // Горы
        Plains,         // Равнины
        Forest,         // Лес
        Sea,            // Море
        Desert,         // Пустыня
        Swamp,          // Болото
        Tundra,         // Тундра
        Jungle,         // Джунгли
        Volcanic,       // Вулканическая
        Spiritual       // Духовная область
    }

    /// <summary>
    /// Тип здания
    /// </summary>
    public enum BuildingType
    {
        House,          // Дом
        Shop,           // Лавка
        Temple,         // Храм
        Cave,           // Пещера
        Tower,          // Башня
        SectHQ,         // Штаб-квартира секты
        Dojo,           // Додзё
        Forge,          // Кузница
        AlchemyLab,     // Алхимическая лаборатория
        Library         // Библиотека
    }

    #endregion

    #region Time

    /// <summary>
    /// Скорость игрового времени
    /// </summary>
    public enum TimeSpeed
    {
        Paused = 0,     // Пауза
        Normal = 1,     // 1 сек = 1 минута (1 tps)
        Fast = 5,       // 1 сек = 5 минут (5 tps)
        Quick = 15      // 1 сек = 15 минут (15 tps)
    }

    /// <summary>
    /// Время суток (6 диапазонов)
    /// Ночь(0-4), Рассвет(5-6), Утро(7-11), День(12-16), Вечер(17-19), Сумерки(20-23)
    /// </summary>
    public enum TimeOfDay
    {
        Night,          // Ночь (0-4)
        Dawn,           // Рассвет (5-6)
        Morning,        // Утро (7-11)
        Day,            // День (12-16)
        Evening,        // Вечер (17-19)
        Twilight        // Сумерки (20-23)
    }

    #endregion

    #region Combat

    /// <summary>
    /// Тип атаки (для подавления уровнем)
    /// Normal | Обычная атака
    /// Technique | Техника
    /// Ultimate | Ultimate-техника
    /// </summary>
    // Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B1: добавлены MeleeStrike/MeleeWeapon/Ranged для stat scaling
    public enum AttackType
    {
        Normal,         // Обычная атака
        MeleeStrike,    // Безоружная атака ближнего боя (STR scaling) — Спринт 3 B1
        MeleeWeapon,    // Атака оружием ближнего боя (AGI scaling) — Спринт 3 B1
        Ranged,         // Дальнобойная атака (INT scaling) — Спринт 3 B1
        Technique,      // Техника
        Ultimate        // Ultimate-техника
    }

    /// <summary>
    /// Результат атаки
    /// </summary>
    public enum CombatAttackResult
    {
        Miss,           // Промах
        Dodge,          // Уклонение
        Parry,          // Парирование
        Block,          // Блок
        Hit,            // Попадание
        CriticalHit,    // Критическое попадание
        Kill            // Убийство
    }

    /// <summary>
    /// Стадия боя
    /// </summary>
    public enum CombatStage
    {
        None,           // Не в бою
        Initiative,     // Определение инициативы
        PlayerTurn,     // Ход игрока
        EnemyTurn,      // Ход врага
        Resolution,     // Разрешение действий
        Victory,        // Победа
        Defeat,         // Поражение
        Flee            // Побег из боя
    }

    /// <summary>
    /// Тип стихийного эффекта
    /// </summary>
    public enum ElementalEffectType
    {
        None,
        Burn,       // Горение (Fire)
        Slow,       // Замедление (Water)
        Stun,       // Оглушение (Earth)
        Knockback,  // Отталкивание (Air)
        Chain,      // Цепной урон (Lightning)
        Pierce,     // Пробитие (Void)
        Purify,     // Очищение (Light)
        PoisonDot   // Отравление (Poison)
    }

    #endregion

    #region Save

    // NOTE: SaveSlot was previously an enum (Slot1/Slot2/Slot3/AutoSave/QuickSave).
    // Migrated to a struct in Structs.cs that combines a name + SaveSlotType,
    // so multiple manual saves can coexist. SaveSlotType enum remains here.

    /// <summary>
    /// Тип сохранения
    /// </summary>
    public enum SaveType
    {
        Manual,         // Ручное
        Auto,           // Автосохранение
        Quick,          // Быстрое
        Checkpoint      // Контрольная точка
    }

    #endregion

    #region Player

    /// <summary>
    /// Состояние сна игрока
    /// </summary>
    public enum PlayerSleepState
    {
        Awake,          // Бодрствует
        FallingAsleep,  // Засыпает (таймер перехода)
        Sleeping,       // Спит
        WakingUp        // Пробуждается
    }

    /// <summary>
    /// Состояние игрока (боевая стойка)
    /// </summary>
    public enum PlayerStance
    {
        Normal,         // Обычное
        Combat,         // Боевая стойка
        Meditating,     // Медитация
        Sleeping        // Сон
    }

    #endregion

    #region Quest

    /// <summary>
    /// Тип квеста
    /// </summary>
    public enum QuestType
    {
        Main,           // Основной (сюжетный)
        Side,           // Побочный
        Daily,          // Ежедневный
        Weekly,         // Еженедельный
        Chain,          // Цепочка квестов
        AutoGenerated   // Автосгенерированный
    }

    /// <summary>
    /// Статус квеста
    /// </summary>
    public enum QuestStatus
    {
        NotStarted,     // Не начат
        Active,         // Активен
        Completed,      // Завершён
        Failed,         // Провален
        Abandoned       // Брошен
    }

    /// <summary>
    /// Тип цели квеста
    /// </summary>
    public enum QuestObjectiveType
    {
        KillEnemy,              // Убить N врагов
        GatherItem,             // Собрать N предметов
        ReachLocation,          // Достичь локации
        TalkToNPC,              // Поговорить с NPC
        CraftItem,              // Скрафтить предмет
        ReachCultivationLevel,  // Достичь уровня культивации
        SurviveDays,            // Выжить N дней
        DefeatBoss              // Победить босса
    }

    /// <summary>
    /// Тип награды за квест
    /// </summary>
    public enum QuestRewardType
    {
        Item,           // Предмет
        Qi,             // Ци
        Experience,     // Опыт (будущее)
        Technique,      // Свиток техники
        FactionRep      // Репутация фракции
    }

    #endregion

    #region UI

    /// <summary>
    /// Состояние игры (для UI)
    /// </summary>
    public enum GameState
    {
        None,           // Sentinel state для инициализации
        MainMenu,       // Главное меню
        Loading,        // Загрузка
        Playing,        // Игра
        Paused,         // Пауза
        Inventory,      // Инвентарь
        Combat,         // Бой
        Dialog,         // Диалог
        Cutscene,       // Катсцена
        Settings,       // Настройки
        CharacterPanel, // Панель персонажа
        Map             // Карта мира
    }

    #endregion

    // ========================================================================
    // Ai-game4 additions — enums preserved from the previous stub that were
    // NOT present in Ai-game3 source. Kept for backward compatibility with
    // already-migrated Modules/Entry/Adapter code.
    // ========================================================================

    #region Ai-game4 Compatibility

    /// <summary>Направление взгляда/движения сущности.</summary>
    public enum Direction
    {
        North,
        South,
        East,
        West,
        Northeast,
        Northwest,
        Southeast,
        Southwest,
    }

    /// <summary>Сезон года (тёплый/холодный).</summary>
    public enum Season
    {
        Warm,
        Cold,
    }

    /// <summary>Тип воды на тайле.</summary>
    public enum WaterType
    {
        None,
        Fresh,
        Salt,
        Spiritual,
        Poisoned,
    }

    /// <summary>Тип сознания сущности.</summary>
    public enum ConsciousnessType
    {
        Full,
        Instinct,
        Simple,
    }

    /// <summary>
    /// Подтип техники (Ai-game4 alias of CombatSubtype).
    /// Сохраняет плоскую модель (CultivationMeditate и т.д.) до полной миграции Modules.
    /// </summary>
    public enum TechniqueSubtype
    {
        MeleeStrike,
        MeleeWeapon,
        RangedProjectile,
        RangedBeam,
        RangedAoe,
        DefenseBlock,
        DefenseDodge,
        DefenseCounter,
        CultivationMeditate,
        CultivationBreakthrough,
        SupportHeal,
        SupportBuff,
        SupportDebuff,
    }

    /// <summary>Категория игрового предмета (расширенная).</summary>
    public enum GameItemType
    {
        Consumable,
        Material,
        Equipment,
        QiStone,
        Key,
        Quest,
    }

    /// <summary>Слои рендеринга (значения используются как ZIndex).</summary>
    public enum RenderLayer
    {
        Default = 0,
        Background = 1,
        Terrain = 2,
        Objects = 3,
        Player = 4,
        UI = 5,
    }

    /// <summary>Тип слота сохранения.</summary>
    public enum SaveSlotType
    {
        Manual,
        AutoSave,
        QuickSave,
    }

    #endregion
}
