# Глоссарий терминов

> **Раздел:** 00_overview
> **Статус:** Единый справочник терминологии проекта.
> **Принцип:** При расхождениях в терминах между документами — считать верным термин из этого глоссария.

---

## Как пользоваться

Глоссарий организован тематически. Каждая запись содержит:
- **Кодовое имя** (идентификатор в коде).
- **Русское название** (канон).
- **Описание** + формула/число, если применимо.
- **Источник истины** (документ, где описано подробно).

Курсивом (`~~так~~`) помечены устаревшие термины — они приведены для справки и НЕ должны использоваться в новом коде.

---

## 1. Культивация и Ци

| Термин | Русский | Описание | Источник |
|--------|---------|----------|----------|
| `cultivationLevel` | Уровень культивации | Основной уровень 1–9 (10 = Вознесение = финал игры) | `QI_SYSTEM.md` |
| `cultivationSubLevel` | Подуровень | 0–9. `totalSubLevels = level × 10 + subLevel` | `QI_SYSTEM.md` |
| `coreCapacity` | Ёмкость ядра | Макс. Ци в ядре. Формула: `1000 × 1.1^totalSubLevels × qualityMultiplier` | `ALGORITHMS.md` §3 |
| `currentQi` | Текущее Ци | Текущее количество Ци (тип `long`) | `ALGORITHMS.md` |
| `qiDensity` | Плотность Ци | `2^(level-1)`. Растёт ×2 за уровень | `ALGORITHMS.md` §3.3 |
| `effectiveQi` | Эффективное Ци | `coreCapacity × qiDensity`. Реальная боевая мощь практика | `QI_SYSTEM.md` |
| `conductivity` | Проводимость меридиан | Скорость работы с Ци. Формула: `coreCapacity / 360` | `QI_SYSTEM.md` |
| `qiRegen` (базовая) | Регенерация микроядра | 10% ёмкости/сутки — **немодифицируема** баффами | `ALGORITHMS.md` §2.3 |
| `qiRestoration` | Восстановление Ци | Общая скорость (медитация, экипировка, формации) — модифицируема | `BUFF_MODIFIERS_SYSTEM.md` |
| `DormantCore` | Дремлющее ядро | Зачаток духовного центра у каждого смертного. Формируется 16–30 лет. ≥80% → возможно пробуждение | `MORTAL_DEVELOPMENT.md` |
| `Awakening` | Пробуждение | Переход Смертный (L0) → Практик (L1). Типы: Natural, Guided, Artifact, Forced | `MORTAL_DEVELOPMENT.md` |
| `innateElement` | Врождённый элемент | Один из 8 элементов, к которому практик имеет предрасположение | `ELEMENTS_SYSTEM.md` |
| `coreQuality` / `qualityMultiplier` | Качество ядра | Множитель ёмкости ядра. Варьируется от рождения | `QI_SYSTEM.md` |
| `environmentMult` | Множитель среды | Концентрация Ци в области. Увеличивается формациями (НЕ проводимостью!) | `QI_SYSTEM.md`, `FORMATION_SYSTEM.md` |
| `statBonus` | Бонус характеристик | `(characterStat - 10) × coefficient`. База стата = 10 | `ALGORITHMS.md` §11 |
| `CultivationLevel` (enum) | Уровень (enum) | None=0, AwakenedCore=1, LifeFlow=2, InternalFire=3, BodySpiritUnion=4, HeartOfHeaven=5, VeilBreaker=6, EternalRing=7, VoiceOfHeaven=8, ImmortalCore=9, Ascension=10 | — |
| `CoreQuality` (enum) | Качество (enum) | Fragmented=1, Cracked=2, Flawed=3, Normal=4, Refined=5, Perfect=6, Transcendent=7 | — |
| `AwakeningType` (enum) | Тип пробуждения (enum) | None, Natural, Guided, Artifact, Forced | — |

---

## 2. Боевая система

| Термин | Русский | Описание | Источник |
|--------|---------|----------|----------|
| `qiBuffer` | Буфер Ци | Естественная защита Ци от ЛЮБОГО урона (техники и физика). Даже во сне | `ALGORITHMS.md` §2 |
| `levelSuppression` | Подавление уровнем | Множитель, снижающий урон при разнице уровней (0..5+) | `ALGORITHMS.md` §1 |
| `damageReduction` | Снижение урона | Процентное снижение бронёй/материалом. Кап 80% | `ALGORITHMS.md` §5 |
| `materialReduction` | Снижение от материала | Процентное снижение физ. урона от материала тела | `ENTITY_TYPES.md` |
| `effectiveBonus` | Эффективный бонус | Бонус после применения мягкого капа | `ALGORITHMS.md` §6 |
| `decayRate` | Скорость затухания | Параметр мягкого капа, индивидуальный для переменной | `ALGORITHMS.md` §6.1 |
| `ultimateMultiplier` | Множитель ульты | ×2.0 для Ultimate-техник (5% шанс у Transcendent). Стоимость Ци также ×2.0 | `TECHNIQUE_SYSTEM.md` |
| `mastery` | Мастерство техники | 0–100%. Бонус ёмкости: +0%→+50%. Формула прироста: `max(0.1, baseGain × (1 - current/100))` | `TECHNIQUE_SYSTEM.md` |
| `baseCapacity` | Базовая ёмкость техники | Зависит от типа: formation=80, defense=72, melee_strike=64, support/healing=56, melee_weapon=48, movement/curse/poison=40, sensory=32, ranged_*=32, cultivation=null | `TECHNIQUE_SYSTEM.md` |
| `capacity` (техника) | Ёмкость техники | `baseCapacity × 2^(level-1) × (1 + mastery/100 × 0.5)` | `ALGORITHMS.md` §3.2 |
| `DamageType` | Тип урона | Enum: Physical, Qi, Elemental, Pure, Void | — |
| `AttackType` | Тип атаки | Enum: Normal, Technique, Ultimate | — |
| `CombatSubtype` | Подтип боя | None, MeleeStrike, MeleeWeapon, RangedProjectile, RangedBeam, RangedAoe, DefenseBlock, DefenseShield, DefenseDodge. ⚠️ MeleeStrike и MeleeWeapon — разные формулы | — |
| `CombatAttackResult` | Результат атаки | Miss, Dodge, Parry, Block, Hit, CriticalHit, Kill | — |
| `ChargeState` | Состояние зарядки техники | None, Charging, Ready, Firing, Interrupted | — |
| `ChargeInterruptReason` | Причина прерывания | PlayerCancel, DamageInterrupt, StunInterrupt, DeathInterrupt, QiDepleted | — |
| `AIDecision` | Решение боевого AI | BasicAttack, ChargeTechnique, ContinueCharge, UseDefensiveTech, Flee, Wait | — |

---

## 3. Тело

| Термин | Русский | Описание | Источник |
|--------|---------|----------|----------|
| `redHP` | Функциональная HP (красная) | Работоспособность части тела. При 0 — паралич | `BODY_SYSTEM.md`, `ALGORITHMS.md` §9 |
| `blackHP` | Структурная HP (чёрная) | Целостность = функциональная × 2. При 0 — ампутация | `BODY_SYSTEM.md` |
| `bodyMaterial` | Материал тела | Определяет твёрдость и снижение физ. урона. 7 типов | `ENTITY_TYPES.md` §5 |
| `SoulType` | Тип души | Character, Creature, Spirit, Artifact, Construct | `ENTITY_TYPES.md` §2 |
| `Morphology` | Морфология | Humanoid, Quadruped, Bird, Serpentine, Arthropod, Amorphous, HybridCentaur, HybridMermaid, HybridHarpy, HybridLamia | `ENTITY_TYPES.md` §3 |
| `Species` | Вид | Конкретный вид (human, elf, wolf, dragon...) | `ENTITY_TYPES.md` §4 |
| `BodyMaterial` (enum) | Материал тела | Organic, Scaled, Chitin, Mineral, Ethereal, Construct, Chaos | — |
| `BodyPartType` | Тип части тела | Head, Torso, Heart, LeftArm, RightArm, LeftLeg, RightLeg, LeftHand, RightHand, LeftFoot, RightFoot | — |
| `BodyPartState` | Состояние части | Healthy, Bruised, Wounded, Disabled, Severed | — |
| `BodyPartFunction` | Функция части | [Flags]: Sensory, Breathing, Circulation, Movement, Manipulation, Vital, QiChannel | — |
| `SizeClass` | Класс размера | Tiny, Small, Medium, Large, Huge, Gargantuan, Colossal | — |
| `MortalStage` | Стадия смертного | None=0, Newborn=1, Child=2, Adult=3, Mature=4, Elder=5, Awakening=9 | — |
| `StatDomain` | Домен характеристики | Body (STR/AGI/VIT), Soul (INT) | — |
| `VitalityScalingMode` | Режим масштабирования HP от VIT | — | — |

### Материалы тела (снижение физ. урона)

| Материал | Твёрдость | Снижение физ. урона |
|----------|-----------|---------------------|
| Organic | 3 | 0% |
| Scaled | 6 | 30% |
| Chitin | 5 | 20% |
| Ethereal | 1 | 70% (физики) |
| Mineral | 8 | 50% |
| Chaos | 5 | переменно |

> **Примечание:** Конструкты НЕ имеют собственного материала — определяется составом (Mineral, Organic и т.д.).

---

## 4. Инвентарь и экипировка

| Термин | Русский | Описание | Источник |
|--------|---------|----------|----------|
| `EquipmentSlot` | Слот экипировки | 16 значений: None, Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff, Amulet, RingLeft1, RingLeft2, RingRight1, RingRight2, Charger, Hands, Back | `INVENTORY_SYSTEM.md` |
| `Enchant` | Зачарование | Магический эффект на предмете. 5-й источник бонусов. T1 (+5–10%) → T5 (+40–50%) | `EQUIPMENT_SYSTEM.md` §5.5 |
| `Grade` (экипировка) | Грейд экипировки | 5 уровней: Damaged(×0.5), Common(×1.0), Refined(×1.5), Perfect(×2.5), Transcendent(×4.0) | `EQUIPMENT_SYSTEM.md` §2 |
| `Grade` (техника) | Грейд техники | 4 уровня: Common(×1.0), Refined(×1.3), Perfect(×1.6), Transcendent(×2.0). Без Damaged | `TECHNIQUE_SYSTEM.md` |
| `backpack` | Рюкзак | Переменная ёмкость инвентаря (зависит от рюкзака) | `INVENTORY_SYSTEM.md` |
| `rarity` | Редкость | Common(50%), Uncommon(30%), Rare(15%), Epic(4%), Legendary(1%), Mythic(0.1%) | `DATA_MODELS.md` §6 |
| `durability` | Прочность | Текущая/макс. прочность экипировки | `EQUIPMENT_SYSTEM.md` |
| `DurabilityCondition` | Состояние прочности | Pristine(100%), Good(80–99%), Worn(60–79%), Damaged(20–59%), Broken(<20%) | — |
| `ItemCategory` | Категория предмета | Weapon, Armor, Accessory, Consumable, Material, Technique, Quest, Misc | — |
| `WeaponHandType` | Тип хвата | OneHand, TwoHand. TwoHand → Unequip WeaponOff | — |
| `NestingFlag` | Флаг вложенности (кольца) | [Flags]: None, Spirit, Ring, Any | — |
| `StorageType` | Тип хранилища | Spirit, Ring (доступ через `IStorageService`) | — |

### Слоты экипировки (гуманоид)

| Слот | Описание |
|------|----------|
| Head | Голова — шлем, шапка, корона |
| Torso | Торс — нагрудник, рубашка, роба |
| Belt | Пояс — ремень, пояс зелий, зарядник |
| Legs | Ноги — поножи, штаны |
| Feet | Ступни — сабатоны, сапоги |
| WeaponMain | Основная рука — одноручное или щит |
| WeaponOff | Вторичная рука — одноручное, щит, инструмент |
| Amulet | Амулет (ювелирная система) |
| RingLeft1, RingLeft2 | Кольца левой руки (макс. 2) |
| RingRight1, RingRight2 | Кольца правой руки (макс. 2) |
| Charger | Зарядник Ци (форм-фактор: belt/bracelet/necklace/ring) |
| Hands | Перчатки |
| Back | Плащ/спина |

**Правила:** 7 видимых слотов куклы + 8 скрытых. Макс. 4 кольца (по 2 на руку). 1 амулет. 1 зарядник. Двуручное оружие блокирует WeaponMain и WeaponOff.

---

## 5. Оружие и броня (генерация)

| Термин | Русский | Описание |
|--------|---------|----------|
| `WeaponSubtype` | Подтип оружия | Unarmed, Dagger, Sword, Greatsword, Axe, Spear, Bow, Staff, Hammer, Mace, Crossbow, Wand |
| `WeaponClass` | Класс оружия | Unarmed, Light, Medium, Heavy, Ranged, Magic |
| `WeaponDamageType` | Тип урона оружия | Slashing, Piercing, Blunt, Elemental |
| `ArmorWeightClass` | Весовой класс брони | Light, Medium, Heavy |
| `ArmorSubtype` | Подтип брони | Head, Torso, Arms, Hands, Legs, Feet, Full |
| `DefenseSubtype` | Подтип защиты | None, Block, Parry, Shield, Dodge, Reflect |
| `MaterialTier` | Тир материала | Tier1=1..Tier5=5 |
| `MaterialCategory` | Категория материала | Metal, Leather, Cloth, Wood, Bone, Crystal, Gem, Organic, Spirit, Void |

---

## 6. Техники

| Термин | Русский | Описание |
|--------|---------|----------|
| `TechniqueType` | Тип техники | Combat, Cultivation, Defense, Support, Healing, Movement, Sensory, Curse, Poison, Formation |
| `TechniqueSubtype` | Подтип техники | melee_strike, melee_weapon, ranged_projectile, ranged_beam, ranged_aoe, shield, block, dodge, reflect, dash, teleport, flight... |
| `TechniqueGrade` | Грейд (enum) | Common(×1.0), Refined(×1.3), Perfect(×1.6), Transcendent(×2.0) |
| `Matryoshka` | Матрёшка | Архитектура ГЕНЕРАЦИИ: 3 слоя (База×Грейд×Специализация). НЕ слои экипировки |
| `EffectType` | Тип эффекта | Damage, Heal, Buff, Debuff, Shield, Movement, StatBoost, StatReduction, Elemental, Special |
| `ElementalEffectType` | Элементальный эффект | None, Burn, Slow, Stun, Knockback, Chain, Pierce, Purify, PoisonDot |

---

## 7. Баффы / Формации / Зарядники

| Термин | Русский | Описание |
|--------|---------|----------|
| `BuffType` | Тип баффа | Buff, Debuff, Neutral (категория) |
| `BuffCategory` | Категория баффа | General, Combat, Cultivation, Elemental, Poison, Curse, Blessing, Transformation, Environment |
| `BuffApplication` | Применение | Instant, Duration, Permanent, Stacking, Refreshing |
| `BuffRemovalType` | Способ снятия | Time, Action, Combat, Rest, Manual |
| `StackType` | Тип стакания | Refresh, Add, Independent |
| `PeriodicType` | Тип периодического эффекта | Damage, Heal, QiRestore, QiDrain, StatChange |
| `SpecialEffectType` | Специальный эффект | Stun, Slow, Root, Silence, Blind, Immunity, Reflect, Absorb, Shield, Regeneration, Lifesteal, Thorns |
| `FormationCore` | Ядро формации | Физический носитель: Disk (переносной, L1–L6) или Altar (стационарный, L5–L9) |
| `FormationCoreType` | Тип ядра | Disk, Altar, Array, Totem, Seal |
| `FormationCoreVariant` | Вариант материала ядра | Stone, Jade, Iron, SpiritIron, Crystal, StarMetal, VoidMatter |
| `FormationType` | Тип формации | Barrier, Trap, Amplification, Suppression, Gathering, Detection, Teleportation, Summoning |
| `FormationSize` | Размер | Small(3×3м), Medium(10×10м), Large(30×30м), Great(100×100м), Heavy(300×300м, L6+) |
| `contourQi` | Стоимость контура | Ци на прорисовку: `80 × 2^(level-1)`. Тратится создателем |
| `Charger` | Зарядник | Экипировка для камней Ци. Слот `charger`. Форм-факторы: belt/bracelet/necklace/ring/backpack |
| `ChargerBuffer` | Буфер зарядника | 50–2000 ед. Пополнение 5–50/сек. Перегрев → блок 30 сек |
| `ChargerFormFactor` | Форм-фактор | Belt, Bracelet, Necklace, Ring, Backpack |
| `ChargerPurpose` | Назначение | Accumulation, Combat, Hybrid |
| `ChargerMaterial` | Материал | Iron, Copper, Silver, SpiritIron, Jade, SpiritJade, DragonBone, VoidMatter |
| `ChargerMode` | Режим | Off, On (упрощённая модель) |
| `HeatState` | Состояние перегрева | Cool, Warm, Hot, Critical, Overheated |
| `QiStoneQuality` | Качество камня Ци | Damaged(×0.5), Common(×1.0), Refined(×1.5), Perfect(×2.5), Transcendent(×4.0) |
| `QiStoneType` | Тип камня Ци | Any, Neutral, Fire, Water, Earth, Air, Lightning, Void |
| `QiStoneSize` | Размер камня Ци | Tiny, Small, Medium, Large, Huge |

> **28 типов BuffType** в коде. НЕТ первичных характеристик (STR/AGI/INT/VIT). НЕТ `ConductivityBoost` (удалён — формации управляют `environmentMult`).

### Что баффы НЕ могут модифицировать

| Запрещённая цель | Причина |
|------------------|---------|
| Первичные характеристики (STR/AGI/INT/VIT) | Развиваются только через действия |
| `coreCapacity` | Определяется уровнем культивации |
| `qiDensity` | Определяется уровнем культивации |
| `qiRegen` (базовая микроядра) | Только от микро-ядра |

---

## 8. Стихии

| Элемент | Русское | Противоположность | Сродство |
|---------|---------|-------------------|----------|
| Fire | Огонь | Water | Air |
| Water | Вода | Fire | Lightning |
| Earth | Земля | Air | Fire |
| Air | Воздух | Earth | Fire, Lightning |
| Lightning | Молния | **Void** | Water, Air |
| Void | Пустота | **Lightning, Light** | — |
| Light | Свет | **Void** | Water, Air |
| Neutral | Нейтральный | — | — |

> **Poison (Яд)** — НЕ стихия, а состояние Ци. Не имеет противоположностей. Реализуется через `technique.type = poison`. Единственные стихийные взаимодействия с ядом: Fire→Poison ×1.2 (выжигание токсинов), Light→Poison ×1.2 (очищение).

### Множители стихийного урона (Вариант А, принят 2026-04-10)

| Взаимодействие | Множитель |
|----------------|-----------|
| Противоположные (Fire↔Water, Earth↔Air, Lightning↔Void, Light↔Void) | ×1.5 атакующий, ×0.8 сродство |
| Fire → Poison | ×1.2 (выжигание токсинов, одностороннее) |
| Light → Poison | ×1.2 (очищение, одностороннее) |
| Void → All | ×1.2 (поглощение) |
| Neutral → All | ×1.0 (без бонусов) |

---

## 9. NPC

| Термин | Русский | Описание |
|--------|---------|----------|
| `NPCRole` | Роль NPC | Monster, Guard, Merchant, Cultivator, Passerby, Elder, Disciple, Enemy |
| `NPCAIState` | Состояние AI | Idle, Wandering, Patrolling, Following, Fleeing, Attacking, Defending, Meditating, Cultivating, Resting, Trading, Talking, Working, Searching, Guarding |
| `Attitude` | Отношение | Hatred, Hostile, Unfriendly, Neutral, Friendly, Allied, SwornAlly. В коде: int −100..+100 |
| `PersonalityTrait` | Черты характера | [Flags]: Aggressive, Cautious, Treacherous, Ambitious, Loyal, Pacifist, Curious, Vengeful (8 черт) |
| `BehaviorType` | Тип поведения | Passive, Defensive, Neutral, Aggressive, Hostile, Friendly |
| `NPCCategory` | Категория NPC | Temp (только память), Plot (сохранение), Unique (полное + история) |
| `SpinalAI` | Спинальный AI | Быстрые рефлексы (уклонение, щит, бегство). 1–10 мс |
| `Alignment`, `Disposition` | ~~Устаревшие~~ | Заменены на `personalityFlags + baseAttitude` |

---

## 10. Развитие характеристик

| Термин | Русский | Описание |
|--------|---------|----------|
| `virtualDelta` | Виртуальная дельта | Накопленный прогресс, не закреплён в реальной характеристике. Кап: STR/AGI/VIT=10, INT=15 |
| `threshold` | Порог развития | Опыт для +1: `floor(currentStat / 10)`. Чем выше стат, тем больше усилий |
| `MAX_STAT_VALUE` | Макс. характеристика | Константа 1000. Жёсткий кап развития |
| `consolidation` | Закрепление | Конвертация виртуальной дельты в реальный стат при сне. Мин. 4 часа, макс +0.20 за 8 часов |
| `StatType` | Тип характеристики | Strength, Agility, Intelligence, Vitality |
| `StatDomain` | Домен | Body (STR/AGI/VIT), Soul (INT) |
| `TrainingType` | Тип тренировки | General, Physical, Sparring, Meditation, BodyHardening |

### Стартовые статы по видам (взрослые, ±20% вариация)

| Species | SoulType | STR | AGI | VIT | INT | Material | Size |
|---------|----------|-----|-----|-----|-----|----------|------|
| Human | Character | 10 | 10 | 10 | 10 | Organic | Medium |
| Elf | Character | 8 | 12 | 8 | 12 | Organic | Medium |
| Demon | Character | 14 | 10 | 12 | 8 | Organic | Medium |
| Giant | Character | 18 | 6 | 16 | 4 | Organic | Huge |
| Wolf | Creature | 8 | 14 | 10 | 4 | Organic | Medium |
| Tiger | Creature | 14 | 12 | 12 | 4 | Organic | Large |
| Dragon | Creature | 20 | 10 | 18 | 10 | Scaled | Huge |
| Phoenix | Creature | 8 | 16 | 8 | 12 | Ethereal | Large |
| Spider | Creature | 4 | 12 | 4 | 2 | Chitin | Tiny |
| Ghost | Spirit | — | — | — | 12 | Ethereal | — |
| Golem | Construct | 16 | 4 | 20 | 2 | Mineral | Large |

---

## 11. Мир

| Термин | Русский | Описание |
|--------|---------|----------|
| `Chunk` | Чанк | Единица сохранения мира, 100×100 км. Один файл. Содержит 100 секторов |
| `Sector` | Сектор | Единица карты мира, 10×10 км. Содержит 1–10 локаций |
| `Tile` | Тайл | Единица навигации, **2×2 м** (единый стандарт). Содержит данные о проходимости, объектах |
| `Location` | Локация | Единица загрузки, переменный размер (100 м – 10 км). До 25M тайлов |
| `Region` | Регион | Группа связанных секторов: Wilderness, Civilized, Sacred, Cursed, Contested, Restricted |
| `TerrainType` | Тип местности | None, Grass, Dirt, Stone, Water_Shallow, Water_Deep, Sand, Snow, Ice, Lava, Void |
| `BiomeType` | Биом | Mountains, Plains, Forest, Sea, Desert, Swamp, Tundra, Jungle, Volcanic, Spiritual |
| `Climate` | Климат | Tundra, Temperate, Desert, Jungle, Mountain, Volcanic, Swamp, Holy, Cursed |
| `LocationType` | Тип локации | Region, Area, Building, Room, Dungeon, Secret |
| `BuildingType` | Тип здания | House, Shop, Temple, Cave, Tower, SectHQ, Dojo, Forge, AlchemyLab, Library |
| `FogOfWar` | Фог войны | Hidden → Explored → Visible → Current. Базовый радиус 1 сектор |
| `dangerLevel` | Уровень опасности | Число 1–9, определяет уровень врагов и риски в секторе |
| `Transition` | Переход | Смена уровня детализации: Мировая карта ↔ Локация ↔ Здание |
| `TileObjectCategory` | Категория объекта | None, Vegetation, Rock, Water, Building, Furniture, Interactive, Decoration |
| `GameTileFlags` | Флаги тайла | [Flags]: None, Passable, Swimable, Flyable, BlocksVision, ProvidesCover, Interactable, Harvestable, Dangerous |
| `HarvestableCategory` | Категория добычи | None, Wood, Stone, Ore, Plant |

### Типы локаций

| Тип | Описание | Опасность |
|-----|----------|-----------|
| Town | Город, безопасная зона | Низкая |
| Village | Деревня | Низкая |
| Wilderness | Дикая местность | Средняя |
| Dungeon | Подземелье | Высокая |
| Sect Territory | Территория секты | Зависит от отношений |

---

## 12. Время

| Термин | Русский | Описание |
|--------|---------|----------|
| `tick` | Тик | 1 тик = 1 минута игрового времени. Фундаментальная единица |
| `Season` | Сезон | `warm \| cold`. Тёплый = месяцы 1–9, Холодный = 10–12 |
| `TimeOfDay` | Время суток | Dawn, Morning, Noon, Afternoon, Evening, Night, Midnight |
| `TimeSpeed` | Скорость | Paused(0), Normal(1 тик/сек), Fast(5), VeryFast(15) |
| `WorldTime` | Игровое время | Структура: totalMinutes, year, month, day, hour, minute, season |
| `ESM` | Э.С.М. | Эра Сердца Мира — летосчисление. Стартовый год: 1864 |

### Длительности действий (в тиках)

| Действие | Тиков |
|----------|-------|
| Движение (1 клетка) | 1 |
| Атака | 1 |
| Ход боя | 2 |
| Медитация | 30–480 |
| Прорыв | 480 (8 игровых часов) |
| Разговор | 5 |
| Сбор ресурсов | 10 |

### Время суток (часы)

| Фаза | Часы |
|------|------|
| Ночь | 0–4 |
| Рассвет | 5–6 |
| Утро | 7–11 |
| День | 12–16 |
| Вечер | 17–19 |
| Сумерки | 20–23 |

---

## 13. Фракции

| Термин | Русский | Описание |
|--------|---------|----------|
| `Nation` | Государство | Территория с границами. Типы: monarchy, republic, theocracy, federation, warlord |
| `Faction` | Фракция | Альянс сект с идеологией: righteous, demonic, neutral, pragmatic, isolationist |
| `Sect` | Секта | Организация культиваторов. Типы: orthodox, unorthodox, demonic, neutral, scholarly, martial. Статусы: official, underground, exiled, nomadic, independent |
| `FactionRelation` | Отношения фракций | sourceId, targetId, relationType(ally/enemy/neutral/vassal), strength(−100..+100). Реализуется как readonly struct |
| `FactionType` | Тип фракции | Sect, Clan, Guild, Empire, Alliance, Independent, Criminal, Religious |
| `FactionRelationType` | Тип отношений | Ally, Enemy, Neutral, Vassal, Overlord, Rival |
| `RequirementType` | Требование фракции | Stat, Quest, Item, Reputation, Recommendation |
| `BenefitType` | Выгода фракции | StatBonus, Discount, TechniqueAccess, ResourceAccess, QuestReward, TrainingBonus |

---

## 14. Перки и журналы

| Термин | Русский | Описание |
|--------|---------|----------|
| `Perk` | Перк | Постоянная пассивная способность. Отличается от баффа (временный) и навыка (развивается) |
| `PerkCategory` | Категория перка | Innate (врождённый), Acquired (приобретённый), Cursed (проклятый, до 3 слотов) |
| `JournalEntry` | Запись журнала | id, title, category, rarity, isDiscovered, completionLevel(0–100%), unlockedFacts |
| `JournalCategory` | Категория журнала | Characters, Locations, Techniques, Creatures, Items, Lore, Factions, Notes |
| `EntryRarity` | Редкость записи | Common, Uncommon, Rare, Epic, Legendary |
| `QuestType` | Тип квеста | Main, Side, Daily, Cultivation, Faction, Hidden, Chain |
| `QuestState` | Состояние квеста | Locked, Available, Active, Completed, Failed, Abandoned |
| `QuestObjectiveType` | Тип цели | Kill, Collect, Deliver, Escort, Explore, Defeat, Cultivation, Talk, Use, Defend, Survive, Reach, Learn, Craft, Meditate |
| `ObjectiveState` | Состояние цели | Locked, Active, InProgress, Completed, Failed |

---

## 15. Сохранение / Состояние игры

| Термин | Русский | Описание |
|--------|---------|----------|
| `SaveSlot` | Слот сохранения | Slot1, Slot2, Slot3, AutoSave, QuickSave |
| `SaveType` | Тип сохранения | Manual, Auto, Quick, Checkpoint |
| `GameState` | Состояние игры | None, MainMenu, Loading, Playing, Paused, Inventory, Combat, Dialog, Cutscene, Settings |
| `ISaveable` | Сохраняемый интерфейс | Контракт: `SaveKey`, `CaptureState`, `RestoreState`. Реализуется сервисами, желающими сохранять состояние |
| `SaveDataAggregator` | Агрегатор сохранений | Собирает данные от всех `ISaveable` через шину событий при `SaveRequestedEvent` |

### Триггеры автосохранения

- Смена локации
- Получение новой техники
- Получение важного предмета
- Прорыв уровня культивации
- Завершение боя

---

## 16. Расходники

| Термин | Русский | Описание |
|--------|---------|----------|
| `ConsumableType` | Тип расходника | Pill, Elixir, Food, Drink, Poison, Scroll, Talisman |
| `ConsumableEffectCategory` | Категория эффекта | Healing, QiRestoration, Buff, Debuff, Cultivation, Permanent |
| `ResourceType` | Тип ресурса | Herb, Ore, Wood, Water, SpiritStone, Crystal, Special |

---

## 17. Устаревшие термины (НЕ использовать)

| Устаревший | Замена | Причина |
|------------|--------|---------|
| ~~StatBonus.bonus~~ | `StatBonus.value` | Объединение дублирующих определений |
| ~~ElementData.oppositeElement~~ | `oppositeElements` | Void имеет 2 противоположности |
| ~~NPCType~~ | `NPCRole` | В коде используется NPCRole |
| ~~ItemType~~ | `ItemCategory` / `ItemData.itemType` (string) | Enum vs строковое поле |
| ~~Rarity~~ | `ItemRarity` | Каноническое имя |
| ~~BodyPartState.Destroyed~~ | Удалён | Unreachable состояние |
| ~~DurabilityCondition.Excellent~~ | Удалён | 5 состояний вместо 6 |
| ~~QiStoneQuality.Raw~~ | `Common` | Выравнивание с EquipmentGrade |
| ~~Disposition~~ | `Attitude + PersonalityTrait` | Объединение двух систем |
| ~~Alignment~~ | `personalityFlags + baseAttitude` | Вычисляется из черт |
| ~~ConductivityModifier~~ | Удалён | ConductivityBoost бафф удалён; формации управляют `environmentMult` |
| ~~coreVolume~~ | `coreCapacity` | Унификация имени |
| ~~qi_regen_buff~~ | `qi_restoration_buff` | Уточнение: восстановление, не базовая регенерация |

---

## 18. Архитектурные термины (engine-agnostic)

| Термин | Русский | Описание |
|--------|---------|----------|
| **Hub-and-Spoke** | Звезда | Архитектура: 16 модулей общаются только через ядро (Core), межмодульные связи запрещены |
| **Core** | Ядро | Центральный слой: интерфейсы, контракты, данные, константы, DI-конфигурация |
| **Module** | Модуль | Независимая единица логики. 16 модулей: Body, Buff, Charger, Combat, Formation, Inventory, NPC, Player, Qi, Tile, World, Quest, Interaction, UI, Save, Generator |
| **ModuleServices** | Сервисы модуля | Статический класс `XxxModuleServices.Register(builder)`. Регистрирует внутренние сервисы модуля в корневом scope |
| **readonly struct контракт** | Контракт сообщения | Zero-GC сообщение между модулями. Все поля readonly |
| **DI-контейнер** | Контейнер зависимостей | Разрешает зависимости через интерфейсы. Прямые синглтоны и ServiceLocator запрещены |
| **Шина событий** | Event bus | Pub/sub для межмодульной коммуникации. Все контракты — readonly struct |
| **ISaveable** | Сохраняемый сервис | Контракт для сервисов, желающих сохранять состояние |
| **Scene Assembly Phase** | Фаза сборки сцены | Шаг программной инициализации игровой сцены. 10 фаз: CoreValidation → TileMapGen → WorldInit → PlayerSpawn → NPCSpawn → FormationInit → ChargerInit → QuestInit → UIInit → Finalize |
| **GameSession** | Игровая сессия | Управление жизненным циклом: NewGame / LoadGame / Pause / Resume / SaveAndQuit / QuitWithoutSaving |
| **Tick-based sim** | Тиковая симуляция | Симуляция с фиксированным шагом, отвязанная от frame rate. 1 тик = 1 минута игрового времени |
| **Zero GC per frame** | Нулевая аллокация GC в кадре | Design goal: все hot-path аллокации исключены. Сообщения — readonly struct |
| **Per-entity DataProvider** | Провайдер данных сущности | Кэш данных по EntityId для O(1) доступа. Обновляется через события шины |
| **Matryoshka generation** | Генерация «Матрёшка» | 3-слойная генерация: База × Грейд × Специализация |

---

## 19. Иерархия источников истины

| Приоритет | Документ | Область |
|-----------|----------|---------|
| 1 | `09_workflow/ALGORITHMS.md` | Формулы, расчёты, мягкие капы |
| 2 | `04_entities/ENTITY_TYPES.md` (TODO) | Типы сущностей, морфологии, материалы |
| 3 | `06_player/EQUIPMENT_SYSTEM.md` (TODO) | Грейды, прочность, слоты экипировки |
| 4 | `02_systems/ELEMENTS_SYSTEM.md` (TODO) | Стихии, взаимодействия, ограничения |
| 5 | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (TODO) | Баффы/дебаффы, модификаторы, ограничения |
| 6 | `02_systems/CHARGER_SYSTEM.md` (TODO) | Параметры зарядников |
| 7 | `01_architecture/ARCHITECTURE.md` | Общая архитектура (ссылки на 1–6) |
| 8 | Остальные документы | Конкретные системы (ссылаются на 1–7) |

**Принцип:** Документ ниже по иерархии НЕ может противоречить документу выше.

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источник: `docs/GLOSSARY.md` v от 2026-05-05 + расширения из `docs/ARCHITECTURE.md`, `docs/ALGORITHMS.md`, `docs_old/matryoshka-architecture.md`.*
