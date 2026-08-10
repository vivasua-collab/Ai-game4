# 📖 Глоссарий терминологии

**Создано:** 2026-04-27
**Обновлено:** 2026-05-05 (аудит инвентаря + кросс-системный аудит: +45 терминов, обновление существующих)
**Статус:** 📋 Черновик для доработки

---

## ⚠️ Важно

> Этот документ — **единый справочник терминологии** проекта.
> При обнаружении расхождений в терминах между документами — считать верным термин из этого глоссария.

---

## 🌊 Система Ци

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `coreCapacity` | Ёмкость ядра | Максимальное количество Ци в ядре практика. Формула: `1000 × 1.1^totalSubLevels × qualityMultiplier` | ARCHITECTURE.md, DATA_MODELS.md |
| ~~coreVolume~~ | ~~Объём ядра~~ | ❌ Устаревший термин. Использовать `coreCapacity` | — |
| `currentQi` | Текущее Ци | Текущее количество Ци в ядре (тип: `long`) | DATA_MODELS.md |
| `qiDensity` | Плотность Ци | Качество Ци = `2^(cultivationLevel - 1)`. Растёт ×2 за уровень | ALGORITHMS.md §3.3 |
| `conductivity` | Проводимость меридиан | Скорость прохождения Ци через меридианы. Формула: `coreCapacity / 360` | QI_SYSTEM.md, ALGORITHMS.md |
| `qiRegen` (базовая) | Базовая регенерация микроядра | 10% ёмкости ядра/сутки — **немодифицируема** баффами | BUFF_MODIFIERS_SYSTEM.md, ALGORITHMS.md §2.3 |
| `qiRestoration` | Восстановление Ци | Общая скорость восстановления Ци (медитация, экипировка, формации) — **модифицируема** | BUFF_MODIFIERS_SYSTEM.md |
| ~~qi_regen_buff~~ | ~~Бафф регенерации Ци~~ | ❌ Устаревшее название эффекта. Использовать `qi_restoration_buff` | — |
| `qi_restoration_buff` | Бафф восстановления Ци | Увеличивает скорость восстановления Ци (медитация, поглощение), но ограничена проводимостью | ELEMENTS_SYSTEM.md |
| `cultivationLevel` | Уровень культивации | Основной уровень 1-9 (10 = Вознесение, конец игры) | ARCHITECTURE.md, DATA_MODELS.md, LORE_SYSTEM.md |
| `cultivationSubLevel` | Подуровень культивации | Под-уровень 0-9. totalSubLevels = level×10 + subLevel | DATA_MODELS.md |
| `coreQuality` / `qualityMultiplier` | Качество ядра | Множитель ёмкости ядра в формуле coreCapacity. Варьируется от рождения | DATA_MODELS.md, ARCHITECTURE.md |
| `effectiveQi` | Эффективное Ци | coreCapacity × qiDensity. Реальная боевая мощь практика | ARCHITECTURE.md §«Вместимость ядра» |
| `DormantCore` | Дремлющее ядро | Зачаток духовного центра у каждого смертного. Формируется 16-30 лет. ≥80% = возможно пробуждение | MORTAL_DEVELOPMENT.md §«Дремлющее ядро» |
| `Awakening` | Пробуждение | Переход Смертный (L0) → Практик (L1). Типы: естественное, направленное, артефактное, насильственное | MORTAL_DEVELOPMENT.md §«Пробуждение ядра» |
| `innateElement` | Врождённый элемент | Один из 8 элементов, к которому практик имеет предрасположение. Ограничивает доступные техники | ELEMENTS_SYSTEM.md, DATA_MODELS.md |
| `statBonus` | Бонус от характеристик | Масштабирование урона/эффекта от первичных статов: `statBonus = (characterStat - 10) × coefficient`. Где coefficient зависит от подтипа техники (0.01–0.05). Базовое значение стата = 10 (нейтральное) | ALGORITHMS.md §11 |
| `CultivationLevel` | Уровень культивации (enum) | Enum: None=0, AwakenedCore=1, LifeFlow=2, InternalFire=3, BodySpiritUnion=4, HeartOfHeaven=5, VeilBreaker=6, EternalRing=7, VoiceOfHeaven=8, ImmortalCore=9, Ascension=10 | Enums.cs |
| `CoreQuality` | Качество ядра (enum) | Enum: Fragmented=1, Cracked=2, Flawed=3, Normal=4, Refined=5, Perfect=6, Transcendent=7. Определяет qualityMultiplier | Enums.cs |
| `AwakeningType` | Тип пробуждения (enum) | Enum: None, Natural, Guided, Artifact, Forced | Enums.cs, MORTAL_DEVELOPMENT.md |

---

## ⚔️ Боевая система

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `qiBuffer` | Буфер Ци | Естественная защита Ци от **ЛЮБОГО урона** (техники Ци и физический) | ALGORITHMS.md §2, COMBAT_SYSTEM.md |
| `levelSuppression` | Подавление уровнем | Множитель, снижающий урон при разнице уровней | ALGORITHMS.md §1 |
| `damageReduction` | Снижение урона | Процентное снижение урона бронёй или материалом. Кап 80% | ALGORITHMS.md §5 |
| `materialReduction` | Снижение от материала | Процентное снижение физ. урона от материала тела | ENTITY_TYPES.md §5 |
| `effectiveBonus` | Эффективный бонус | Бонус после применения мягкого капа | ALGORITHMS.md §6 |
| `decayRate` | Скорость затухания | Параметр мягкого капа, индивидуальный для каждой переменной | ALGORITHMS.md §6.1 |
| `ultimateMultiplier` | Множитель ульты | ×2.0 для Ultimate-техник (5% шанс у Transcendent). Стоимость Ци также ×2.0 | TECHNIQUE_SYSTEM.md §«Ultimate», ALGORITHMS.md §4.2 |
| `mastery` | Мастерство техники | 0-100%. Бонус ёмкости: +0%→+50%. Формула прироста: max(0.1, baseGain × (1 - current/100)) | TECHNIQUE_SYSTEM.md §«Система мастерства» |
| `SpinalAI` | Спинальный AI | Быстрые автоматические реакции NPC (уклонение, щит, бегство). 1-10мс. Приоритеты 30-100 | NPC_AI_SYSTEM.md §«Spinal AI» |
| `NPCCategory` | Категория NPC | Enum: Temp (упрощённое AI), Plot (полное AI), Unique (полное + история) | NPC_AI_SYSTEM.md §«Категории NPC» |
| `DamageType` | Тип урона | Enum: Physical, Qi, Elemental, Pure, Void | Enums.cs, ALGORITHMS.md |
| `AttackType` | Тип атаки | Enum: Normal, Technique, Ultimate | Enums.cs |
| `CombatSubtype` | Подтип боя | Enum: None, MeleeStrike (без оружия), MeleeWeapon (с оружием), RangedProjectile, RangedBeam, RangedAoe, DefenseBlock, DefenseShield, DefenseDodge. ⚠️ MeleeStrike и MeleeWeapon — разные формулы урона (BUG-DMG-01) | Enums.cs |
| `CombatAttackResult` | Результат атаки | Enum: Miss, Dodge, Parry, Block, Hit, CriticalHit, Kill | Enums.cs |
| `CombatStage` | Стадия боя | Enum: None, Initiative, PlayerTurn, EnemyTurn, Resolution, Victory, Defeat | Enums.cs |
| `CombatEventType` | Тип боевого события | Enum: CombatStart, CombatEnd, TurnStart, TurnEnd, AttackStart, AttackHit, AttackMiss, AttackDodged, AttackParried, AttackBlocked, DamageDealt, DamageTaken, QiAbsorbed, QiDepleted, BodyPartHit, BodyPartSevered, Death, TechniqueUsed, TechniqueLearned, CooldownReady | CombatEvents.cs |
| `ChargeState` | Состояние зарядки техники | Enum: None, Charging, Ready, Firing, Interrupted | ChargeState.cs |
| `ChargeInterruptReason` | Причина прерывания зарядки | Enum: PlayerCancel, DamageInterrupt, StunInterrupt, DeathInterrupt, QiDepleted | ChargeState.cs |
| `AIDecision` | Решение боевого AI | Enum: BasicAttack, ChargeTechnique, ContinueCharge, UseDefensiveTech, Flee, Wait | CombatAI.cs |
| `CombatActionType` | Тип боевого действия (для развития) | Enum: Strike, Dodge, Block, TakeDamage, UseTechnique | StatDevelopment.cs |

---

## 🦴 Система тела

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `redHP` | Функциональная HP (красная) | Работоспособность части тела. При 0 — паралич | BODY_SYSTEM.md, ALGORITHMS.md §9 |
| `blackHP` | Структурная HP (чёрная) | Целостность части тела = функциональная HP × 2. При 0 — отрубание | BODY_SYSTEM.md |
| `bodyMaterial` | Материал тела | Определяет твёрдость и снижение физ. урона. 7 типов (включая Construct, Chaos) | ENTITY_TYPES.md §5 |
| `SoulType` | Тип души | Enum: Character, Creature, Spirit, Artifact, Construct | ENTITY_TYPES.md §2, Enums.cs |
| `Morphology` | Морфология | Enum: Humanoid, Quadruped, Bird, Serpentine, Arthropod, Amorphous, HybridCentaur, HybridMermaid, HybridHarpy, HybridLamia | ENTITY_TYPES.md §3, Enums.cs |
| `Species` | Вид | Конкретный вид (human, elf, wolf, dragon...) | ENTITY_TYPES.md §4 |
| `BodyMaterial` | Материал тела (enum) | Enum: Organic, Scaled, Chitin, Mineral, Ethereal, Construct, Chaos. Определяет materialReduction | Enums.cs |
| `BodyPartType` | Тип части тела | Enum: Head, Torso, Heart, LeftArm, RightArm, LeftLeg, RightLeg, LeftHand, RightHand, LeftFoot, RightFoot | Enums.cs |
| `BodyPartState` | Состояние части тела | Enum: Healthy, Bruised, Wounded, Disabled, Severed. ~~Destroyed~~ — удалён (С-08: unreachable) | Enums.cs, BODY_SYSTEM.md |
| `MortalStage` | Стадия смертного | Enum: None=0, Newborn=1, Child=2, Adult=3, Mature=4, Elder=5, Awakening=9 | Enums.cs, MORTAL_DEVELOPMENT.md |

---

## 🎒 Инвентарь и экипировка

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `EquipmentSlot` | Слот экипировки | 16 значений: None, Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff, Amulet, RingLeft1, RingLeft2, RingRight1, RingRight2, Charger, Hands, Back | ARCHITECTURE.md, INVENTORY_SYSTEM.md, DATA_MODELS.md, Enums.cs |
| `Enchant` | Зачарование | Магический эффект на предмете. 5-й источник бонусов (base, grade, material, set, enchant). 5 тиров: T1 (+5~10%) → T5 (+40~50%) | EQUIPMENT_SYSTEM.md §5.5 |
| `Grade` (экипировка) | Грейд экипировки | 5 уровней: Damaged(×0.5), Common(×1.0), Refined(×1.5), Perfect(×2.5), Transcendent(×4.0) | EQUIPMENT_SYSTEM.md §2, Enums.cs |
| `Grade` (техника) | Грейд техники | 4 уровня: Common(×1.0), Refined(×1.3), Perfect(×1.6), Transcendent(×2.0) (без Damaged) | TECHNIQUE_SYSTEM.md, Enums.cs |
| `backpack` | Рюкзак | Переменная ёмкость инвентаря (зависит от рюкзака, не фиксирована 49) | INVENTORY_SYSTEM.md |
| `rarity` | Редкость (предмета) | Enum ItemRarity: Common(50%), Uncommon(30%), Rare(15%), Epic(4%), Legendary(1%), Mythic(0.1%) | DATA_MODELS.md §6, INVENTORY_SYSTEM.md, Enums.cs |
| `durability` | Прочность | Текущая/макс. прочность экипировки. См. `DurabilityCondition` для состояний | DATA_MODELS.md §6, EQUIPMENT_SYSTEM.md |
| `DurabilityCondition` | Состояние прочности | 5 состояний: Pristine(100%), Good(80-99%), Worn(60-79%), Damaged(20-59%), Broken(<20%). ~~Excellent~~ — удалён (С-01). Эффективность: Constants.cs lines 616-623 | Enums.cs, EQUIPMENT_SYSTEM.md |
| `ItemCategory` | Категория предмета | Enum: Weapon, Armor, Accessory, Consumable, Material, Technique, Quest, Misc. ⚠️ `ItemData.itemType` — string, не enum | Enums.cs |
| `StatBonus` | Бонус характеристики (структура) | Поля: `statName`(string), `value`(float), `isPercentage`(bool). ⚠️ Поле `bonus` переименовано в `value` (FIX С-07 — объединены дублирующие определения из ItemData.cs и WeaponGenerator.cs) | StatBonus.cs |
| `WeaponHandType` | Тип хвата оружия | Enum: OneHand, TwoHand. TwoHand → Unequip WeaponOff (БАГ-ИНВ-12) | Enums.cs |
| `NestingFlag` | Флаг вложенности (кольца) | [Flags] Enum: None, Spirit, Ring, Any | Enums.cs |
| `ItemDatabase` | База данных предметов | Запланированный класс (ИСП-БЛ-06): кэш itemId → ItemData. Решает БАГ-ИНВ-49 (Resources.LoadAll на каждый подбор) | 05_05_combat_loot_fix.md |
| `EquipmentData.stackable` | Стакаемость экипировки | ⚠️ По умолчанию true (наследуется от ItemData) — БАГ-ИНВ-13. EquipmentSOFactory ставит false, но ручные SO — нет | ItemData.cs |

---

## ⚔️ Оружие и броня (генерация)

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `WeaponSubtype` | Подтип оружия | Enum: Unarmed, Dagger, Sword, Greatsword, Axe, Spear, Bow, Staff, Hammer, Mace, Crossbow, Wand. Расширен из первоначальных 5 значений (Н-02) | WeaponGenerator.cs |
| `WeaponClass` | Класс оружия | Enum: Unarmed, Light, Medium, Heavy, Ranged, Magic | WeaponGenerator.cs |
| `WeaponDamageType` | Тип урона оружия | Enum: Slashing, Piercing, Blunt, Elemental | WeaponGenerator.cs |
| `ArmorWeightClass` | Весовой класс брони | Enum: Light, Medium, Heavy. Влияет на подвижность и защиту (Н-03) | ArmorGenerator.cs |
| `ArmorSubtype` | Подтип брони | Enum: Head, Torso, Arms, Hands, Legs, Feet, Full | ArmorGenerator.cs |
| `DefenseSubtype` | Подтип защиты | Enum: None, Block, Parry, Shield, Dodge, Reflect. Используется в CombatSubtype (DefenseBlock/Shield/Dodge) | Enums.cs |
| `MaterialTier` | Тир материала | Enum: Tier1=1, Tier2=2, Tier3=3, Tier4=4, Tier5=5 | Enums.cs |
| `MaterialCategory` | Категория материала | Enum: Metal, Leather, Cloth, Wood, Bone, Crystal, Gem, Organic, Spirit, Void | Enums.cs |

---

## 🧪 Расходники

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `ConsumableType` | Тип расходника | Enum: Pill, Elixir, Food, Drink, Poison, Scroll, Talisman | ConsumableGenerator.cs |
| `ConsumableEffectCategory` | Категория эффекта расходника | Enum: Healing, QiRestoration, Buff, Debuff, Cultivation, Permanent. ⚠️ Система ConsumableEffect не реализована (БАГ-ИНВ-31) | ConsumableGenerator.cs |
| `ResourceType` | Тип ресурса (локация) | Enum: Herb, Ore, Wood, Water, SpiritStone, Crystal, Special | LocationData.cs |

---

## ⚡ Техники

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `TechniqueType` | Тип техники | Enum: Combat, Cultivation, Defense, Support, Healing, Movement, Sensory, Curse, Poison, Formation | TECHNIQUE_SYSTEM.md §«Типы техник», Enums.cs |
| `TechniqueSubtype` | Подтип техники | Уточнение типа: melee_strike, melee_weapon, ranged_projectile, ranged_beam, ranged_aoe, shield, block, dodge, reflect, dash, teleport, flight... | TECHNIQUE_SYSTEM.md, CONFIGURATIONS.md |
| `TechniqueGrade` | Грейд техники (enum) | Enum: Common(×1.0), Refined(×1.3), Perfect(×1.6), Transcendent(×2.0). Без Damaged | Enums.cs |
| `baseCapacity` | Базовая ёмкость техники | Зависит от типа: Formation=80, Defense=72, Combat=64/48/32, Support/Healing=56, Movement=40... | TECHNIQUE_SYSTEM.md §«Структурная ёмкость» |
| `capacity` (техника) | Ёмкость техники | Макс. базовое Ци, которое техника обрабатывает: baseCapacity × 2^(level-1) × (1 + mastery/100 × 0.5) | TECHNIQUE_SYSTEM.md §«Структурная ёмкость» |
| `Matryoshka` | Матрёшка | Архитектура **генерации**: 3 слоя (База×Грейд×Специализация). Применяется к экипировке, техникам, расходникам. **Не** система слоёв экипировки (Матрёшка v1 упразднена, см. EQUIPPED_SPRITES_DRAFT.md §6) | ARCHITECTURE.md §«Принцип Матрёшка» |
| `EffectType` | Тип эффекта техники | Enum: Damage, Heal, Buff, Debuff, Shield, Movement, StatBoost, StatReduction, Elemental, Special | TechniqueData.cs |
| `ElementalEffectType` | Тип элементального эффекта | Enum: None, Burn, Slow, Stun, Knockback, Chain, Pierce, Purify, PoisonDot | Enums.cs |

---

## 🔄 Баффы / Формации / Зарядники

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `BuffType` | Тип баффа | Enum категорий: Buff, Debuff, Neutral. ⚠️ Не путать с BuffCategory | BuffData.cs |
| `BuffCategory` | Категория баффа | Enum: General, Combat, Cultivation, Elemental, Poison, Curse, Blessing, Transformation, Environment | BuffData.cs |
| `BuffApplication` | Применение баффа | Enum: Instant, Duration, Permanent, Stacking, Refreshing | BUFF_MODIFIERS_SYSTEM.md ЧАСТЬ A |
| `BuffRemovalType` | Способ снятия баффа | Enum: Time, Action, Combat, Rest, Manual | BuffData.cs |
| `StackType` | Тип стакания баффов | Enum: Refresh, Add, Independent | BuffData.cs |
| `PeriodicType` | Тип периодического эффекта | Enum: Damage, Heal, QiRestore, QiDrain, StatChange | BuffData.cs |
| `SpecialEffectType` | Специальный эффект | Enum: Stun, Slow, Root, Silence, Blind, Immunity, Reflect, Absorb, Shield, Regeneration, Lifesteal, Thorns | BuffData.cs |
| `ConductivityModifier` | ~~Модификатор проводимости~~ | ❌ УДАЛЕНО. ConductivityBoost бафф удалён. Формации управляют environmentMult вместо проводимости | BUFF_MODIFIERS_SYSTEM.md ЧАСТЬ A §«Проводимость» |
| `FormationCore` | Ядро формации | Физический носитель: Disk (переносной, L1-L6) или Altar (стационарный, L5-L9). Содержит контур формации | FORMATION_SYSTEM.md §«Физические носители» |
| `FormationCoreType` | Тип ядра формации | Enum: Disk (переносной), Altar (стационарный), Array (массив), Totem (тотем), Seal (печать). Расширение первоначальных Disk/Altar (С-14) | FormationCoreData.cs |
| `FormationCoreVariant` | Вариант материала ядра | Enum: Stone, Jade, Iron, SpiritIron, Crystal, StarMetal, VoidMatter. Определяет качество и уровень формации | FormationCoreData.cs |
| `FormationType` | Тип формации | Enum: Barrier, Trap, Amplification, Suppression, Gathering, Detection, Teleportation, Summoning | FORMATION_SYSTEM.md §«Типы формаций», FormationCoreData.cs |
| `FormationSize` | Размер формации | Enum: Small(3×3м), Medium(10×10м), Large(30×30м), Great(100×100м), Heavy(300×300м, L6+) | FORMATION_SYSTEM.md §«Размеры формаций», FormationCoreData.cs |
| `contourQi` | Стоимость контура | Ци на прорисовку контура формации: 80 × 2^(level-1). Тратится создателем | FORMATION_SYSTEM.md §«Формулы» |
| `Charger` | Зарядник | Экипировка для хранения камней Ци и контролируемого поглощения. Слот `charger`. Форм-факторы: belt/bracelet/necklace/ring/backpack | CHARGER_SYSTEM.md §1 |
| `ChargerBuffer` | Буфер зарядника | Мгновенный запас Ци: 50-2000. Пополнение через проводимость 5-50 ед/сек. Перегрев 100% → блок 30 сек | CHARGER_SYSTEM.md §2.2, §4 |
| `ChargerFormFactor` | Форм-фактор зарядника | Enum: Belt, Bracelet, Necklace, Ring, Backpack | ChargerData.cs |
| `ChargerPurpose` | Назначение зарядника | Enum: Accumulation, Combat, Hybrid | ChargerData.cs |
| `ChargerMaterial` | Материал зарядника | Enum: Iron, Copper, Silver, SpiritIron, Jade, SpiritJade, DragonBone, VoidMatter | ChargerData.cs |
| `ChargerMode` | Режим зарядника | Enum: Off, On | ChargerData.cs |
| `HeatState` | Состояние перегрева | Enum: Cool, Warm, Hot, Critical, Overheated | ChargerHeat.cs |
| `QiStoneQuality` | Качество камня Ци | Enum: Damaged(×0.5), Common(×1.0), Refined(×1.5), Perfect(×2.5), Transcendent(×4.0). Выровнен с EquipmentGrade (FIX В-16: добавлен Damaged, Raw→Common) | ChargerSlot.cs |
| `QiStoneType` | Тип камня Ци | Enum: Any, Neutral, Fire, Water, Earth, Air, Lightning, Void | FormationCoreData.cs |
| `QiStoneSize` | Размер камня Ци | Enum: Tiny, Small, Medium, Large, Huge | ChargerSlot.cs |

---

## 🧬 Элементы

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `Element` | Стихия | Enum: Neutral, Fire, Water, Earth, Air, Lightning, Void, Light, Poison. Void имеет 2 противоположности (Lightning + Light) | ELEMENTS_SYSTEM.md, Enums.cs |
| `ElementData.oppositeElements` | Противоположные элементы | `List<Element>` — замена устаревшего `oppositeElement`(Element). FIX В-12: Void имеет 2 противоположности | ElementData.cs |
| ~~ElementData.oppositeElement~~ | ~~Противоположный элемент~~ | ❌ Устаревшее поле [Obsolete]. Использовать `oppositeElements` | ElementData.cs |
| `ElementData.affinityElements` | Родственные элементы | `List<Element>` — элементы с пониженным уроном (×0.8) | ElementData.cs |
| `ElementData.weakToElements` | Слабость к элементам | `List<Element>` — элементы с повышенным уроном (×1.5) | ElementData.cs |

---

## 👤 NPC

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `NPCRole` | Роль NPC | Enum: Monster, Guard, **Merchant**, Cultivator, Passerby, Elder, Disciple, Enemy. ⚠️ Не NPCType — в коде используется NPCRole. Merchant определён, но торговля не реализована (БАГ-ИНВ-37) | NPCGenerator.cs |
| `NPCAIState` | Состояние AI NPC | Enum: Idle, Wandering, Patrolling, Following, Fleeing, Attacking, Defending, Meditating, Cultivating, Resting, Trading, Talking, Working, Searching, Guarding | NPCData.cs |
| `Alignment` | Мировоззрение | [Obsolete] Enum: LawfulGood, NeutralGood, ChaoticGood, LawfulNeutral, TrueNeutral, ChaoticNeutral, LawfulEvil, NeutralEvil, ChaoticEvil. Заменён на personalityFlags + baseAttitude | NPCPresetData.cs |
| `BehaviorType` | Тип поведения | Enum: Passive, Defensive, Neutral, Aggressive, Hostile, Friendly | NPCPresetData.cs |
| `Disposition` | Расположение | [Obsolete] Enum: Hostile, Unfriendly, Neutral, Friendly, Allied, Aggressive, Cautious, Treacherous, Ambitious. Заменён на Attitude + PersonalityTrait | Enums.cs |
| `Attitude` | Отношение (enum) | Enum: Hatred, Hostile, Unfriendly, Neutral, Friendly, Allied, SwornAlly. В коде: int -100..+100. ⚠️ CombatTrigger.ShouldEngage проверяет отношение ЦЕЛИ вместо ВЛАДЕЛЬЦА (BUG-NPC-02) | Enums.cs, FACTION_SYSTEM.md |
| `PersonalityTrait` | Черты характера | [Flags] Enum: None=0, Aggressive=1<<0, Cautious=1<<1, Treacherous=1<<2, Ambitious=1<<3, Loyal=1<<4, Pacifist=1<<5, Curious=1<<6, Vengeful=1<<7 | NPC_AI_SYSTEM.md, Enums.cs |
| `SizeClass` | Класс размера | Enum: Tiny, Small, Medium, Large, Huge, Gargantuan, Colossal | SpeciesData.cs |

---

## 📊 Развитие

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `virtualDelta` | Виртуальная дельта | Накопленный прогресс развития, не закреплён в реальной характеристике. Кап: STR/AGI/VIT=10, INT=15 | STAT_THRESHOLD_SYSTEM.md §«Виртуальная дельта» |
| `threshold` | Порог развития | Опыт для +1 к характеристике: floor(currentStat / 10). Чем выше стат, тем больше усилий | STAT_THRESHOLD_SYSTEM.md §«Формула порога» |
| `MAX_STAT_VALUE` | Макс. характеристика | Константа 1000. Жёсткий кап развития. GameConstants.MAX_STAT_VALUE | ARCHITECTURE.md, STAT_THRESHOLD_SYSTEM.md §«Баланс» |
| `consolidation` | Закрепление | Конвертация виртуальной дельты в реальную характеристику при сне. Мин. 4 часа, макс +0.20 за 8 часов | STAT_THRESHOLD_SYSTEM.md §«Закрепление при сне» |
| `StatDevelopment` | Развитие характеристик | Структура данных: реальные статы (STR/AGI/INT/VIT) + виртуальные дельты + методы AddDelta/ConsolidateSleep | STAT_THRESHOLD_SYSTEM.md §«Реализация в коде» |
| `StatType` | Тип характеристики | Enum: Strength, Agility, Intelligence, Vitality | StatDevelopment.cs |
| `TrainingType` | Тип тренировки | Enum: General, Physical, Sparring, Meditation, BodyHardening | StatDevelopment.cs |

---

## 🌍 Мир (World Structure)

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `Chunk` | Чанк | Единица сохранения мира, 100×100 км. Один файл сохранения. Содержит 100 секторов (10×10) | WORLD_MAP_SYSTEM.md §0.1, WORLD_SAVE_SYSTEM.md §2 |
| `Sector` | Сектор | Единица карты мира, 10×10 км. Ячейка на глобальной карте для навигации. Содержит 1-10 локаций | WORLD_MAP_SYSTEM.md §0.1, §2.1 |
| `Tile` | Тайл | Единица навигации, **2×2 м** — единый стандарт проекта. Содержит данные о проходимости, объектах, субъектах | WORLD_MAP_SYSTEM.md §0.2, TILE_SYSTEM.md |
| `Location` | Локация | Единица загрузки, переменный размер (100 м — 10 км). Unity сцена. Содержит до 25M тайлов | WORLD_MAP_SYSTEM.md §0.1, LOCATION_MAP_SYSTEM.md |
| `Region` | Регион | Группа связанных секторов с общими характеристиками (Wilderness, Civilized, Sacred, Cursed, Contested, Restricted) | WORLD_MAP_SYSTEM.md §2.2 |
| `TerrainType` | Тип местности | Enum: None, Grass, Dirt, Stone, Water_Shallow, Water_Deep, Sand, Snow, Ice, Lava, Void | WORLD_SYSTEM.md, TileEnums.cs |
| `BiomeType` | Тип биома | Enum: Mountains, Plains, Forest, Sea, Desert, Swamp, Tundra, Jungle, Volcanic, Spiritual | Enums.cs |
| `Climate` | Климатическая зона | Enum: Tundra, Temperate, Desert, Jungle, Mountain, Volcanic, Swamp, Holy, Cursed | WORLD_MAP_SYSTEM.md §2.3 |
| `LocationType` | Тип локации | Enum: Region, Area, Building, Room, Dungeon, Secret | Enums.cs |
| `BuildingType` | Тип здания | Enum: House, Shop, Temple, Cave, Tower, SectHQ, Dojo, Forge, AlchemyLab, Library | Enums.cs |
| `FogOfWar` | Фог войны | Система видимости: Hidden → Explored → Visible → Current. Радиус базовый 1 сектор | WORLD_MAP_SYSTEM.md §2.4 |
| `dangerLevel` | Уровень опасности | Число 1-9, определяет уровень врагов и риски в секторе | WORLD_MAP_SYSTEM.md §2.1 |
| `Transition` | Переход | Смена уровня детализации: Мировая карта ↔ Локация ↔ Здание. Включает сохранение позиции и загрузку сцены | TRANSITION_SYSTEM.md §1 |
| `TileObjectCategory` | Категория объекта тайла | Enum: None, Vegetation, Rock, Water, Building, Furniture, Interactive, Decoration | TileEnums.cs |
| `TileObjectType` | Тип объекта тайла | Enum с диапазонами: Trees(100-121), Rocks(200-210), Water(300-310), Buildings(400-411), Interactables(500-530) | TileEnums.cs |
| `GameTileFlags` | Флаги тайла | [Flags] Enum: None, Passable, Swimable, Flyable, BlocksVision, ProvidesCover, Interactable, Harvestable, Dangerous | TileEnums.cs |
| `HarvestableCategory` | Категория добычи | Enum: None, Wood, Stone, Ore, Plant | Harvestable.cs |

---

## ⏰ Время

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `tick` | Тик | 1 тик = 1 минута игрового времени. Фундаментальная единица времени | TIME_SYSTEM.md, ARCHITECTURE.md §«Система времени» |
| `Season` | Сезон | Enum: `warm \| cold`. Тёплый = месяцы 1-9, Холодный = месяцы 10-12 | TIME_SYSTEM.md §4, LORE_SYSTEM.md |
| `TimeOfDay` | Время суток | Enum: Dawn, Morning, Noon, Afternoon, Evening, Night, Midnight | TIME_SYSTEM.md §4, Enums.cs |
| `TimeSpeed` | Скорость времени | Enum: Paused(0), Normal(1 тик/сек), Fast(5), VeryFast(15) | TIME_SYSTEM.md §2, Enums.cs |
| `WorldTime` | Игровое время | Структура: totalMinutes, year, month, day, hour, minute, season. Летосчисление Э.С.М. | TIME_SYSTEM.md §4 |
| `ESM` | Э.С.М. | Эра Сердца Мира — летосчисление мира. Текущий год: 1864 | LORE_SYSTEM.md, TIME_SYSTEM.md, MORTAL_DEVELOPMENT.md |

---

## 🏛️ Фракции

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `Nation` | Государство | Территория с границами и законами. Типы: monarchy, republic, theocracy, federation, warlord | FACTION_SYSTEM.md §«Государство» |
| `Faction` | Фракция | Альянс сект с общей идеологией. Идеологии: righteous, demonic, neutral, pragmatic, isolationist | FACTION_SYSTEM.md §«Фракция» |
| `Sect` | Секта | Организация культиваторов. Типы: orthodox, unorthodox, demonic, neutral, scholarly, martial. Статусы: official, underground, exiled, nomadic, independent | FACTION_SYSTEM.md §«Секта» |
| `FactionRelation` | Отношения фракций | Структура: sourceId, targetId, relationType(ally/enemy/neutral/vassal), strength(-100..+100) | FACTION_SYSTEM.md §«Отношения» |
| `FactionType` | Тип фракции | Enum: Sect, Clan, Guild, Empire, Alliance, Independent, Criminal, Religious | FactionData.cs |
| `FactionRelationType` | Тип отношений фракций | Enum: Ally, Enemy, Neutral, Vassal, Overlord, Rival | Enums.cs |
| `Attitude` | Отношение (NPC) | Числовое значение -100..+100 отношения NPC к ГГ. Формула включает personalAttitude, sectRelation, factionRelation, nationRelation | FACTION_SYSTEM.md §«Расчёт отношений», NPC_AI_SYSTEM.md |
| `PersonalityTrait` | Черты характера | [Flags] enum: Aggressive, Cautious, Treacherous, Ambitious, Loyal, Pacifist, Curious, Vengeful | NPC_AI_SYSTEM.md, Enums.cs |
| `RequirementType` | Требование фракции | Enum: Stat, Quest, Item, Reputation, Recommendation | FactionData.cs |
| `BenefitType` | Выгода фракции | Enum: StatBonus, Discount, TechniqueAccess, ResourceAccess, QuestReward, TrainingBonus | FactionData.cs |

---

## ⭐ Перки

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `Perk` | Перк | Постоянная пассивная способность. Отличается от баффа (временный) и навыка (развивается) | PERK_SYSTEM.md §1 |
| `PerkCategory` | Категория перка | Enum: Innate (врождённый, вне слотов), Acquired (приобретённый, основные слоты), Cursed (проклятый, отд. слоты до 3) | PERK_SYSTEM.md §3, §7.1 |
| `environmentMult` | Множитель среды | Множитель концентрации Ци в области. Увеличивается формациями (НЕ проводимость!). absorbedQi = meditationTime × finalConductivity × environmentMult | QI_SYSTEM.md, FORMATION_SYSTEM.md |

---

## 📦 Журнал / Квесты

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `JournalEntry` | Запись журнала | Структура: id, title, category, rarity, isDiscovered, completionLevel(0-100%), unlockedFacts | JOURNAL_SYSTEM.md §«JournalEntry» |
| `JournalCategory` | Категория журнала | Enum: Characters, Locations, Techniques, Creatures, Items, Lore, Factions, Notes | JOURNAL_SYSTEM.md §«Категории журнала» |
| `EntryRarity` | Редкость записи | Enum: Common, Uncommon, Rare, Epic, Legendary | JOURNAL_SYSTEM.md §«JournalCategory» |
| `QuestType` | Тип квеста | Enum: Main, Side, Daily, Cultivation, Faction, Hidden, Chain | QuestData.cs |
| `QuestState` | Состояние квеста | Enum: Locked, Available, Active, Completed, Failed, Abandoned | QuestData.cs |
| `QuestObjectiveType` | Тип цели квеста | Enum: Kill, Collect, Deliver, Escort, Explore, Defeat, Cultivation, Talk, Use, Defend, Survive, Reach, Learn, **Craft**, Meditate. ⚠️ Craft определён, но GameEvents.TriggerItemCrafted() никогда не вызывается (БАГ-ИНВ-33) | QuestObjective.cs |
| `ObjectiveState` | Состояние цели | Enum: Locked, Active, InProgress, Completed, Failed | QuestObjective.cs |

---

## 💾 Сохранение / Состояние игры

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `SaveSlot` | Слот сохранения | Enum: Slot1, Slot2, Slot3, AutoSave, QuickSave | Enums.cs |
| `SaveType` | Тип сохранения | Enum: Manual, Auto, Quick, Checkpoint | Enums.cs |
| `GameState` | Состояние игры | Enum: None, MainMenu, Loading, Playing, Paused, Inventory, Combat, Dialog, Cutscene, Settings | Enums.cs |

---

## 🗒️ Прочее

| Термин (код) | Русский | Описание | Источник истины |
|-------------|---------|----------|-----------------|
| `GrammaticalGender` | Грамматический род | Enum: Masculine=0, Feminine=1, Neuter=2, Plural=3. Для генератора имён | GrammaticalGender.cs |
| `LogCategory` | Категория лога | Enum: System, Combat, Qi, Body, Tile, Player, NPC, Render, Save, Formation, Inventory, Interaction, UI, Harvest, Generator, Network | GameLogger.cs |
| `LogLevel` | Уровень лога | Enum: Debug=0, Info=1, Warning=2, Error=3 | GameLogger.cs |

---

## ⚠️ Устаревшие / переименованные термины

| Устаревший термин | Замена | Причина | Ссылка |
|-------------------|--------|---------|--------|
| ~~StatBonus.bonus~~ | `StatBonus.value` | Объединение дублирующих определений (С-07) | StatBonus.cs |
| ~~ElementData.oppositeElement~~ | `ElementData.oppositeElements` | Void имеет 2 противоположности (В-12) | ElementData.cs |
| ~~NPCType~~ | `NPCRole` | В коде используется NPCRole, не NPCType | NPCGenerator.cs |
| ~~ItemType~~ | `ItemCategory` / `ItemData.itemType` (string) | Enum = ItemCategory; поле ItemData.itemType = string | Enums.cs, ItemData.cs |
| ~~Rarity~~ | `ItemRarity` | Каноническое имя enum в коде | Enums.cs |
| ~~BodyPartState.Destroyed~~ | Удалён | Unreachable состояние (С-08) | Enums.cs |
| ~~DurabilityCondition.Excellent~~ | Удалён | Заменён на 5-состояний: Pristine/Good/Worn/Damaged/Broken (С-01) | Enums.cs |
| ~~QiStoneQuality.Raw~~ | `QiStoneQuality.Common` | Выравнивание с EquipmentGrade (В-16) | ChargerSlot.cs |
| ~~Disposition~~ | `Attitude + PersonalityTrait` | Объединение двух систем (устаревший [Obsolete]) | Enums.cs |
| ~~Alignment~~ | `personalityFlags + baseAttitude` | Мировоззрение вычисляется из черт (устаревший [Obsolete]) | NPCPresetData.cs |
| ~~ConductivityModifier~~ | Удалён | ConductivityBoost бафф удалён; формации управляют environmentMult | BUFF_MODIFIERS_SYSTEM.md |

---

## 🔄 Источники истины (иерархия)

| Приоритет | Документ | Область |
|-----------|----------|---------|
| 1 | ALGORITHMS.md | Формулы, расчёты, таблицы подавления, мягкие капы |
| 2 | ENTITY_TYPES.md | Типы сущностей, морфологии, материалы тела |
| 3 | EQUIPMENT_SYSTEM.md | Грейды, прочность, слоты экипировки |
| 4 | ELEMENTS_SYSTEM.md | Стихии, взаимодействия, ограничения |
| 5 | BUFF_MODIFIERS_SYSTEM.md | Баффы/дебаффы, модификаторы, ограничения |
| 6 | CHARGER_SYSTEM.md | Параметры зарядников |
| 7 | ARCHITECTURE.md | Общая архитектура (ссылки на 1-6) |
| 8 | Остальные документы | Конкретные системы (ссылаются на 1-7) |

**Специализированные источники истины:**

| Область | Документ | Примечание |
|---------|----------|------------|
| Размерность мира | WORLD_MAP_SYSTEM.md | 🔧 В разработке |
| Календарь/время | TIME_SYSTEM.md | |
| Пороги статов | STAT_THRESHOLD_SYSTEM.md | |
| Пороги культивации | STAT_THRESHOLD_SYSTEM.md | Раздел «Пороги культивации» внутри файла |
| Грейды техник | TECHNIQUE_SYSTEM.md | |
| Лор | LORE_SYSTEM.md | Подчиняется TIME_SYSTEM для календарных фактов |

> **Принцип:** Документ ниже по иерархии НЕ может противоречить документу выше.

---

*Документ создан: 2026-04-27*
*Обновлено: 2026-04-27 — Добавлено 60 терминов (аудит СТ-2), включена WORLD_MAP_SYSTEM.md (СТ-3), обновлены ссылки BUFF_SYSTEM→BUFF_MODIFIERS_SYSTEM (СТ-1)*
*Обновлено: 2026-05-05 — Добавлено 45 терминов из аудита инвентаря (7 прогонов): FormationCoreType, WeaponSubtype, ArmorWeightClass, DefenseSubtype, QiStoneQuality, DurabilityCondition, CombatSubtype, ItemCategory, NPCRole, ChargeState, AIDecision, все enum зарядников/формаций/брони/расходников, квесты, сохранение; обновлены существующие (StatBonus.value, ElementData.oppositeElements, EquipmentGrade множители); добавлена таблица устаревших терминов*
*Статус: Черновик для доработки*
