# NPC Assembly Examples — VContainer+MessagePipe

Создано: 2026-05-23 06:30:00 UTC

> **Архитектура:** VContainer+MessagePipe (модульная).  
> **Старый документ:** `NPC_L6_ASSEMBLY_EXAMPLE.md` (legacy Singleton/ServiceLocator) — ЗАМЕНЁН.  
> **Исходный код:** `NPCAssemblyService.cs`, `SoulGenerator.cs`, `SpeciesRegistry.cs`,  
> `BodyFactory.cs`, `BodyTemplateProvider.cs`, `NPCConfig.cs`, `GameConstants.cs`  
> **ЗАПРЕТ 3.9:** Вся игровая математика — целочисленная (int/long).  
> Исключение: Conductivity (float) — это скорость, не игровой расчёт.

---

## Пайплайн сборки (NPCAssemblyService.Assemble)

```
Шаг 1: SoulGenerator.Generate → SoulData
Шаг 2: SpeciesRegistry.GetSpecies → SpeciesData (фенотип)
Шаг 3: BodyFactory.CreateBody → List<BodyPart> (тело)
Шаг 4: Qi — уже в SoulData (расширенная формула)
Шаг 5: EquipHumanoid / BodyEnhancements
Шаг 6: GenerateTechniques (L1+: 1, L3-4: 2, L5-6: 3, L7+: 4)
Шаг 7: FillInventory
Шаг 8: CalculateTotals → NPCState
```

---

## Формулы расчёта (с подставленными числами)

### 1. Ёмкость ядра (CoreCapacity, тип long)

```
totalSubLevels = (level - 1) × 10 + subLevel
coreCapacity   = (long)(1000 × 1.1^totalSubLevels × qualityMultiplier)
```

### 2. Плотность Ци (QiDensity, тип int)

```
qiDensity = 2^(level - 1)
```

Таблица: L1=1, L2=2, L3=4, L4=8, L5=16, L6=32, L7=64, L8=128, L9=256, L10=512

### 3. Проводимость (Conductivity, тип float)

```
baseConductivity              = coreCapacity / 360
levelGrowthFactor             = ConductivityGrowthFactors[level]
effectiveAge                  = age × levelGrowthFactor
conductivityGrowthMultiplier  = 1.0 + 0.001 × effectiveAge
conductivity                  = baseConductivity × conductivityGrowthMultiplier
```

**ConductivityGrowthFactors (NPCConfig):**  
L0=1.0, L1=1.2, L2=1.5, L3=2.0, L4=3.0, L5=5.0, L6=8.0, L7+=12.0

### 4. Множители качества ядра

| Качество (CoreQuality) | Enum | Множитель |
|------------------------|------|-----------|
| Fragmented (Осколочное)| 1    | 0.5       |
| Cracked (Треснутое)    | 2    | 0.7       |
| Flawed (С изъяном)     | 3    | 0.85      |
| Normal (Нормальное)    | 4    | 1.0       |
| Refined (Очищенное)    | 5    | 1.2       |
| Perfect (Совершенное)  | 6    | 1.5       |
| Transcendent           | 7    | 2.0       |

> **Примечание:** В описании задачи используются названия Good=1.2, Excellent=1.5.  
> В коде: Good → Refined (enum=5, mult=1.2), Excellent → Perfect (enum=6, mult=1.5).

### 5. Статы (CalculateStats, тип int — ЗАПРЕТ 3.9)

```
baseStats (Character): STR=10, AGI=10, VIT=10, INT=10
levelGrowth   = (level - 1) × 2
randomBonus   = rng.Next(-1, 3)   // -1, 0, 1, 2
finalStat     = baseStat + levelGrowth + randomBonus
```

### 6. HP частей тела (BodyFactory, тип int — ЗАПРЕТ 3.9)

```
vitalityMultiplier = 1.0 + (vitality - 10) × 0.05
sizeMultiplier     = SizeClassHPMultipliers[SizeClass]   // Medium=1.0
effectiveRed       = RoundToInt(BaseFunctionalHP × vitalityMultiplier × sizeMultiplier)
effectiveBlack     = RoundToInt(BaseStructuralHP × vitalityMultiplier × sizeMultiplier)
```

> **ВНИМАНИЕ:** Текущий код NPCAssemblyService передаёт `species.BaseVitality` (10 для human)  
> в BodyFactory, а НЕ рассчитанный стат VIT. В примерах ниже используется  
> рассчитанный VIT для демонстрации полного эффекта.  
> **Ссылка:** NPCAssemblyService.cs:94 — `float vitality = species.BaseVitality;`

### 7. MaxLifespan (тип int)

```
baseLifespan = species.LifespanRange.Max          // human: 100
levelBonus   = LifespanLevelBonus[level]          // L3:100, L6:800, L7+:2000
latePenalty  = CalcLatePenalty(awakeningAge)
  age ≤ 20  → 0
  20 < age ≤ 40 → (age-20) × 2
  age > 40  → 40 + (age-40) × 5
maxLifespan  = max(1, baseLifespan + levelBonus - latePenalty)
```

**LifespanLevelBonus:** L0:0, L1:+20, L2:+50, L3:+100, L4:+200, L5:+400, L6:+800, L7+:+2000

### 8. Боевые параметры (CalculateBaseDamage/Defense)

```
BaseDamage:
  baseDmg = species.BaseStrength × sizeMult × (1.0 + (level-1) × 0.1)
  baseDmg += enhancementDamageBonus
  baseDmg × (1 + techniqueCount × 0.1)
  result = max(1, (int)baseDmg)

BaseDefense:
  defense = materialReduction × 100 + enhancementArmorBonus
  defense += equipmentSlotCount × 5
  result = max(0, (int)defense)
```

---

## NPC 1: Человек-Культиватор L3.5

```
╔══════════════════════════════════════════════════════╗
║  NPC: Лин Вэй — L3.5 Культиватор
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  === ДУША (SoulData) ===                             ║
║  CultivationLevel:  InternalFire (L3)                ║
║  SubLevel:          5                                ║
║  Age:               68                               ║
║  AwakeningAge:      18                               ║
║  CoreQuality:       Normal (множитель = 1.0)         ║
║  QualityMultiplier: 1.0                              ║
║  AwakeningType:     Guided                           ║
║  CoreCapacity:      10 834 (long)                    ║
║  CurrentQi:         10 834 (long)                    ║
║  QiDensity:         4                                ║
║  Conductivity:      34.17 (float)                    ║
║  ConductivityGrowthMultiplier: 1.136                 ║
║  MaxLifespan:       200                              ║
║  MortalStage:       None (практик)                   ║
║  InnateElement:     Neutral (Character)              ║
║                                                      ║
║  === ХАРАКТЕРИСТИКИ ===                              ║
║  STR: 15  (10 + 4 + 1)                              ║
║  AGI: 14  (10 + 4 + 0)                              ║
║  VIT: 16  (10 + 4 + 2)                              ║
║  INT: 15  (10 + 4 + 1)                              ║
║                                                      ║
║  === ТЕЛО (BodyParts) ===                            ║
║  #   Часть      RedHP  BlackHP  Vital  Функции       ║
║  1   Head         65     130     Да     Sensory|Brea  ║
║  2   Torso       130     260     Да     Circ|Digest   ║
║  3   Heart       104       0     Да     Circulation   ║
║  4   LArm         52     104     Нет    Manipulation  ║
║  5   RArm         52     104     Нет    Manipulation  ║
║  6   LHand        26      52     Нет    Manipulation  ║
║  7   RHand        26      52     Нет    Manipulation  ║
║  8   LLeg         65     130     Нет    Movement      ║
║  9   RLeg         65     130     Нет    Movement      ║
║  10  LFoot        32      65     Нет    Movement      ║
║  11  RFoot        32      65     Нет    Movement      ║
║  ─────────────────────────────────────               ║
║  MaxHealth (ΣRedHP): 649                             ║
║                                                      ║
║  === ЭКИПИРОВКА ===                                  ║
║  WeaponMain: weapon_L3_xxxx (оружие ур.3)            ║
║  WeaponOff:  weapon_L3_xxxx (оружие ур.3)            ║
║  Head:      armor_L3_xxxx (шлем ур.3)               ║
║  Torso:     armor_L3_xxxx (нагрудник ур.3)           ║
║  Belt:      charger_L3_xxxx (зарядник ур.3)          ║
║  Legs:      armor_L3_xxxx (поножи ур.3)              ║
║  Feet:      armor_L3_xxxx (сапоги ур.3)              ║
║                                                      ║
║  === ТЕХНИКИ ===                                     ║
║  Количество: 2 (L3-4 → 2 техники)                    ║
║  1. technique_combat_L3_xxxx (Боевая, Common)        ║
║  2. technique_defense_L3_xxxx (Защитная, Common)     ║
║                                                      ║
║  === ИНВЕНТАРЬ ===                                   ║
║  1. heal_pill_L3 ×2 (Consumable, Common)             ║
║  2. material_iron_scrap ×1 (Material, Common)        ║
║  (Qi-настойка: 10% шанс — не выпал)                 ║
║  (Осколок дух. камня: 10% шанс — не выпал)          ║
║                                                      ║
║  === БОЕВЫЕ ПАРАМЕТРЫ ===                            ║
║  BaseDamage:     14                                  ║
║  BaseDefense:    35                                  ║
║  AggressionLevel: 0.20                               ║
║                                                      ║
║  === ДОПОЛНИТЕЛЬНО ===                               ║
║  SpeciesId:      human                               ║
║  Role:           Cultivator                          ║
║  Category:       Temp                                ║
║  Personality:    Ambitious | Loyal                   ║
║  AIState:        Cultivating                         ║
║  SoulType:       Character                           ║
║  Morphology:     Humanoid                            ║
║  BodyMaterial:   Organic                             ║
╚══════════════════════════════════════════════════════╝
```

### Пошаговый расчёт NPC 1

**Шаг 1: SoulData**

```
CultivationLevel = L3 (InternalFire), SubLevel = 5

totalSubLevels = (3 - 1) × 10 + 5 = 25

coreCapacity:
  1.1^25 = 10.83471
  coreCapacity = (long)(1000 × 10.83471 × 1.0) = 10834

qiDensity:
  QiDensityByLevel[2] = 4    // index = level-1 = 2

conductivity:
  baseConductivity = 10834 / 360 = 30.0944
  levelGrowthFactor = ConductivityGrowthFactors[3] = 2.0
  effectiveAge = 68 × 2.0 = 136
  conductivityGrowthMultiplier = 1.0 + 0.001 × 136 = 1.136
  conductivity = 30.0944 × 1.136 = 34.167

currentQi = coreCapacity = 10834

maxLifespan:
  baseLifespan = 100 (human LifespanRange.Max)
  levelBonus = LifespanLevelBonus[3] = 100
  latePenalty = CalcLatePenalty(18) = 0   // 18 ≤ 20
  maxLifespan = 100 + 100 - 0 = 200

stats:
  levelGrowth = (3 - 1) × 2 = 4
  STR = 10 + 4 + 1 = 15   // randomBonus = 1
  AGI = 10 + 4 + 0 = 14   // randomBonus = 0
  VIT = 10 + 4 + 2 = 16   // randomBonus = 2
  INT = 10 + 4 + 1 = 15   // randomBonus = 1
```

**Шаг 2: SpeciesData (human)**

```
SoulType=Character, Morphology=Humanoid, Material=Organic, Size=Medium
BaseVitality=10, LifespanRange=(70, 100)
```

**Шаг 3: Body (11 частей, VIT=16)**

```
vitalityMultiplier = 1.0 + (16 - 10) × 0.05 = 1.3
sizeMultiplier = 1.0 (Medium)

Head:   Red=Round(50×1.3×1.0)=65,  Black=Round(100×1.3×1.0)=130
Torso:  Red=Round(100×1.3×1.0)=130, Black=Round(200×1.3×1.0)=260
Heart:  Red=Round(80×1.3×1.0)=104,  Black=0 (CORE-C01: только красная HP)
LArm:   Red=Round(40×1.3×1.0)=52,  Black=Round(80×1.3×1.0)=104
RArm:   Red=Round(40×1.3×1.0)=52,  Black=Round(80×1.3×1.0)=104
LHand:  Red=Round(20×1.3×1.0)=26,  Black=Round(40×1.3×1.0)=52
RHand:  Red=Round(20×1.3×1.0)=26,  Black=Round(40×1.3×1.0)=52
LLeg:   Red=Round(50×1.3×1.0)=65,  Black=Round(100×1.3×1.0)=130
RLeg:   Red=Round(50×1.3×1.0)=65,  Black=Round(100×1.3×1.0)=130
LFoot:  Red=Round(25×1.3×1.0)=32,  Black=Round(50×1.3×1.0)=65
RFoot:  Red=Round(25×1.3×1.0)=32,  Black=Round(50×1.3×1.0)=65

MaxHealth = Σ RedHP = 65+130+104+52+52+26+26+65+65+32+32 = 649
```

**Шаг 5: Экипировка (гуманоид, L3+)**

```
7 слотов: WeaponMain, WeaponOff, Head, Torso, Belt(Charger), Legs, Feet
Все предметы генерируются ItemGenerator для уровня 3
```

**Шаг 6: Техники (L3-4 → 2)**

```
2 техники, генерируемые TechniqueGenerator для уровня 3
```

**Шаг 7: Инвентарь**

```
L1+: heal_pill ×2 (1-2 шт, rng дало 2)
L3+, 10%: Qi-настойка — не выпал (10% шанс)
0-2 материала: 1 кусок (material_iron_scrap, L3<5 → Common)
L3+, 10%: spirit_stone_shard — не выпал (10% шанс)
```

**Шаг 8: Боевые параметры**

```
BaseDamage:
  baseDmg = 10 (human BaseStrength)
  × 1.0 (Medium sizeMult) = 10
  × (1.0 + 2×0.1) = 1.2 → 12
  enhancement = 0 (гуманоид, нет усилений)
  × (1 + 2×0.1) = 1.2 → 14.4
  → (int)14 = 14

BaseDefense:
  defense = 0 (Organic reduction=0.0 → 0×100=0)
  + 0 (enhancement)
  + 7 × 5 = 35
  → 35

AggressionLevel = 0.2 (Cultivator)
```

---

## NPC 2: Человек-Старейшина L6.3

```
╔══════════════════════════════════════════════════════╗
║  NPC: Чжан Тяньши — L6.3 Старейшина
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  === ДУША (SoulData) ===                             ║
║  CultivationLevel:  VeilBreaker (L6)                 ║
║  SubLevel:          3                                ║
║  Age:               146                              ║
║  AwakeningAge:      16                               ║
║  CoreQuality:       Refined/Good (множитель = 1.2)   ║
║  QualityMultiplier: 1.2                              ║
║  AwakeningType:     Guided                           ║
║  CoreCapacity:      187 486 (long)                   ║
║  CurrentQi:         187 486 (long)                   ║
║  QiDensity:         32                               ║
║  Conductivity:      1 129.08 (float)                 ║
║  ConductivityGrowthMultiplier: 2.168                 ║
║  MaxLifespan:       900                              ║
║  MortalStage:       None (практик)                   ║
║  InnateElement:     Neutral (Character)              ║
║                                                      ║
║  === ХАРАКТЕРИСТИКИ ===                              ║
║  STR: 21  (10 + 10 + 1)                             ║
║  AGI: 20  (10 + 10 + 0)                             ║
║  VIT: 22  (10 + 10 + 2)                             ║
║  INT: 21  (10 + 10 + 1)                             ║
║                                                      ║
║  === ТЕЛО (BodyParts) ===                            ║
║  #   Часть      RedHP  BlackHP  Vital  Функции       ║
║  1   Head         80     160     Да     Sensory|Brea  ║
║  2   Torso       160     320     Да     Circ|Digest   ║
║  3   Heart       128       0     Да     Circulation   ║
║  4   LArm         64     128     Нет    Manipulation  ║
║  5   RArm         64     128     Нет    Manipulation  ║
║  6   LHand        32      64     Нет    Manipulation  ║
║  7   RHand        32      64     Нет    Manipulation  ║
║  8   LLeg         80     160     Нет    Movement      ║
║  9   RLeg         80     160     Нет    Movement      ║
║  10  LFoot        40      80     Нет    Movement      ║
║  11  RFoot        40      80     Нет    Movement      ║
║  ─────────────────────────────────────               ║
║  MaxHealth (ΣRedHP): 800                             ║
║                                                      ║
║  === ЭКИПИРОВКА ===                                  ║
║  WeaponMain: weapon_L6_xxxx (оружие ур.6)            ║
║  WeaponOff:  weapon_L6_xxxx (оружие ур.6)            ║
║  Head:      armor_L6_xxxx (шлем ур.6)               ║
║  Torso:     armor_L6_xxxx (нагрудник ур.6)           ║
║  Belt:      charger_L6_xxxx (зарядник ур.6)          ║
║  Legs:      armor_L6_xxxx (поножи ур.6)              ║
║  Feet:      armor_L6_xxxx (сапоги ур.6)              ║
║                                                      ║
║  === ТЕХНИКИ ===                                     ║
║  Количество: 3 (L5-6 → 3 техники)                    ║
║  1. technique_combat_L6_xxxx (Боевая, Common/Refined)║
║  2. technique_defense_L6_xxxx (Защитная, Refined)    ║
║  3. technique_support_L6_xxxx (Поддержка, Common)    ║
║                                                      ║
║  === ИНВЕНТАРЬ ===                                   ║
║  1. heal_pill_L6 ×2 (Consumable, Common)             ║
║  2. qi_potion_L6 ×1 (Consumable, Uncommon) — 10%     ║
║  3. material_iron_scrap ×2 (Material, Common)        ║
║  4. spirit_stone_shard ×1 (Material, Rare) — 10%     ║
║  (L5+, 5%: spirit_stone_fragment — не выпал)         ║
║                                                      ║
║  === БОЕВЫЕ ПАРАМЕТРЫ ===                            ║
║  BaseDamage:     19                                  ║
║  BaseDefense:    35                                  ║
║  AggressionLevel: 0.15                               ║
║                                                      ║
║  === ДОПОЛНИТЕЛЬНО ===                               ║
║  SpeciesId:      human                               ║
║  Role:           Elder                               ║
║  Category:       Unique                              ║
║  Personality:    Cautious | Curious                  ║
║  AIState:        Idle                                ║
║  SoulType:       Character                           ║
║  Morphology:     Humanoid                            ║
║  BodyMaterial:   Organic                             ║
╚══════════════════════════════════════════════════════╝
```

### Пошаговый расчёт NPC 2

**Шаг 1: SoulData**

```
CultivationLevel = L6 (VeilBreaker), SubLevel = 3

totalSubLevels = (6 - 1) × 10 + 3 = 53

coreCapacity:
  1.1^53 = 1.1^50 × 1.1^3
  1.1^50 = 117.39085
  1.1^3  = 1.331
  1.1^53 = 117.39085 × 1.331 = 156.239
  coreCapacity = (long)(1000 × 156.239 × 1.2) = (long)187486.8 = 187486

qiDensity:
  QiDensityByLevel[5] = 32   // index = level-1 = 5

conductivity:
  baseConductivity = 187486 / 360 = 520.794
  levelGrowthFactor = ConductivityGrowthFactors[6] = 8.0
  effectiveAge = 146 × 8.0 = 1168
  conductivityGrowthMultiplier = 1.0 + 0.001 × 1168 = 2.168
  conductivity = 520.794 × 2.168 = 1129.08

currentQi = coreCapacity = 187486

maxLifespan:
  baseLifespan = 100 (human)
  levelBonus = LifespanLevelBonus[6] = 800
  latePenalty = CalcLatePenalty(16) = 0   // 16 ≤ 20
  maxLifespan = 100 + 800 - 0 = 900

stats:
  levelGrowth = (6 - 1) × 2 = 10
  STR = 10 + 10 + 1 = 21   // randomBonus = 1
  AGI = 10 + 10 + 0 = 20   // randomBonus = 0
  VIT = 10 + 10 + 2 = 22   // randomBonus = 2
  INT = 10 + 10 + 1 = 21   // randomBonus = 1
```

**Шаг 2: SpeciesData (human)**

```
SoulType=Character, Morphology=Humanoid, Material=Organic, Size=Medium
BaseVitality=10, LifespanRange=(70, 100)
```

**Шаг 3: Body (11 частей, VIT=22)**

```
vitalityMultiplier = 1.0 + (22 - 10) × 0.05 = 1.6
sizeMultiplier = 1.0 (Medium)

Head:   Red=Round(50×1.6×1.0)=80,  Black=Round(100×1.6×1.0)=160
Torso:  Red=Round(100×1.6×1.0)=160, Black=Round(200×1.6×1.0)=320
Heart:  Red=Round(80×1.6×1.0)=128,  Black=0 (CORE-C01)
LArm:   Red=Round(40×1.6×1.0)=64,  Black=Round(80×1.6×1.0)=128
RArm:   Red=Round(40×1.6×1.0)=64,  Black=Round(80×1.6×1.0)=128
LHand:  Red=Round(20×1.6×1.0)=32,  Black=Round(40×1.6×1.0)=64
RHand:  Red=Round(20×1.6×1.0)=32,  Black=Round(40×1.6×1.0)=64
LLeg:   Red=Round(50×1.6×1.0)=80,  Black=Round(100×1.6×1.0)=160
RLeg:   Red=Round(50×1.6×1.0)=80,  Black=Round(100×1.6×1.0)=160
LFoot:  Red=Round(25×1.6×1.0)=40,  Black=Round(50×1.6×1.0)=80
RFoot:  Red=Round(25×1.6×1.0)=40,  Black=Round(50×1.6×1.0)=80

MaxHealth = Σ RedHP = 80+160+128+64+64+32+32+80+80+40+40 = 800
```

**Шаг 5: Экипировка (гуманоид, L6+)**

```
7 слотов, все предметы генерируются для уровня 6
Грейд экипировки смещён к Refined/Perfect (NPCConfig.EquipmentGradeWeightsByLevel)
```

**Шаг 6: Техники (L5-6 → 3)**

```
3 техники для уровня 6
```

**Шаг 7: Инвентарь**

```
L1+: heal_pill ×2
L3+, 10%: Qi-настойка — выпал!
0-2 материала: 2 куска (material_iron_scrap, L6≥5 → Uncommon)
L3+, 10%: spirit_stone_shard — выпал!
L5+, 5%: spirit_stone_fragment — не выпал (5% шанс)
```

**Шаг 8: Боевые параметры**

```
BaseDamage:
  baseDmg = 10
  × 1.0 (Medium) = 10
  × (1.0 + 5×0.1) = 1.5 → 15
  × (1 + 3×0.1) = 1.3 → 19.5
  → (int)19 = 19

BaseDefense:
  defense = 0 (Organic) + 0 (enhancement) + 7×5 = 35
  → 35

AggressionLevel = 0.15 (Elder)
```

---

## NPC 3: Человек-Страж L9.7

```
╔══════════════════════════════════════════════════════╗
║  NPC: Фэн Тигр — L9.7 Страж
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  === ДУША (SoulData) ===                             ║
║  CultivationLevel:  ImmortalCore (L9)                ║
║  SubLevel:          7                                ║
║  Age:               205                              ║
║  AwakeningAge:      15                               ║
║  CoreQuality:       Perfect/Excellent (множ.=1.5)    ║
║  QualityMultiplier: 1.5                              ║
║  AwakeningType:     Guided                           ║
║  CoreCapacity:      5 988 060 (long)                 ║
║  CurrentQi:         5 988 060 (long)                 ║
║  QiDensity:         256                              ║
║  Conductivity:      57 753.9 (float)                 ║
║  ConductivityGrowthMultiplier: 3.460                 ║
║  MaxLifespan:       2 100                            ║
║  MortalStage:       None (практик)                   ║
║  InnateElement:     Neutral (Character)              ║
║                                                      ║
║  === ХАРАКТЕРИСТИКИ ===                              ║
║  STR: 27  (10 + 16 + 1)                             ║
║  AGI: 26  (10 + 16 + 0)                             ║
║  VIT: 28  (10 + 16 + 2)                             ║
║  INT: 27  (10 + 16 + 1)                             ║
║                                                      ║
║  === ТЕЛО (BodyParts) ===                            ║
║  #   Часть      RedHP  BlackHP  Vital  Функции       ║
║  1   Head         95     190     Да     Sensory|Brea  ║
║  2   Torso       190     380     Да     Circ|Digest   ║
║  3   Heart       152       0     Да     Circulation   ║
║  4   LArm         76     152     Нет    Manipulation  ║
║  5   RArm         76     152     Нет    Manipulation  ║
║  6   LHand        38      76     Нет    Manipulation  ║
║  7   RHand        38      76     Нет    Manipulation  ║
║  8   LLeg         95     190     Нет    Movement      ║
║  9   RLeg         95     190     Нет    Movement      ║
║  10  LFoot        48      95     Нет    Movement      ║
║  11  RFoot        48      95     Нет    Movement      ║
║  ─────────────────────────────────────               ║
║  MaxHealth (ΣRedHP): 951                             ║
║                                                      ║
║  === ЭКИПИРОВКА ===                                  ║
║  WeaponMain: weapon_L9_xxxx (оружие ур.9)            ║
║  WeaponOff:  weapon_L9_xxxx (оружие ур.9)            ║
║  Head:      armor_L9_xxxx (шлем ур.9)               ║
║  Torso:     armor_L9_xxxx (нагрудник ур.9)           ║
║  Belt:      charger_L9_xxxx (зарядник ур.9)          ║
║  Legs:      armor_L9_xxxx (поножи ур.9)              ║
║  Feet:      armor_L9_xxxx (сапоги ур.9)              ║
║                                                      ║
║  === ТЕХНИКИ ===                                     ║
║  Количество: 4 (L7+ → 4 техники)                     ║
║  1. technique_combat_L9_xxxx (Боевая, Refined)       ║
║  2. technique_defense_L9_xxxx (Защитная, Perfect)    ║
║  3. technique_combat_L9_yyyy (Боевая, Refined)       ║
║  4. technique_support_L9_xxxx (Поддержка, Common)    ║
║                                                      ║
║  === ИНВЕНТАРЬ ===                                   ║
║  1. heal_pill_L9 ×1 (Consumable, Uncommon)           ║
║  2. qi_potion_L9 ×1 (Consumable, Uncommon) — 10%     ║
║  3. material_spirit_stone_shard ×1 (Mat., Uncommon)  ║
║  4. spirit_stone_shard ×1 (Material, Rare) — 10%     ║
║  5. spirit_stone_fragment ×1 (Material, Epic) — 5%   ║
║                                                      ║
║  === БОЕВЫЕ ПАРАМЕТРЫ ===                            ║
║  BaseDamage:     25                                  ║
║  BaseDefense:    35                                  ║
║  AggressionLevel: 0.40                               ║
║                                                      ║
║  === ДОПОЛНИТЕЛЬНО ===                               ║
║  SpeciesId:      human                               ║
║  Role:           Guard                               ║
║  Category:       Plot                                ║
║  Personality:    Aggressive | Loyal                  ║
║  AIState:        Patrolling                          ║
║  SoulType:       Character                           ║
║  Morphology:     Humanoid                            ║
║  BodyMaterial:   Organic                             ║
╚══════════════════════════════════════════════════════╝
```

### Пошаговый расчёт NPC 3

**Шаг 1: SoulData**

```
CultivationLevel = L9 (ImmortalCore), SubLevel = 7

totalSubLevels = (9 - 1) × 10 + 7 = 87

coreCapacity:
  1.1^87 = 1.1^80 × 1.1^7
  1.1^80 ≈ 2048.98
  1.1^7  = 1.94872
  1.1^87 ≈ 2048.98 × 1.94872 ≈ 3993.04
  coreCapacity = (long)(1000 × 3993.04 × 1.5) = (long)5989560 = 5989560

qiDensity:
  QiDensityByLevel[8] = 256   // index = level-1 = 8

conductivity:
  baseConductivity = 5989560 / 360 = 16637.67
  levelGrowthFactor = ConductivityGrowthFactors[7] = 12.0
    // L9 > массива (8 элементов, индексы 0-7), используется последний
  effectiveAge = 205 × 12.0 = 2460
  conductivityGrowthMultiplier = 1.0 + 0.001 × 2460 = 3.46
  conductivity = 16637.67 × 3.46 = 57526.3

currentQi = coreCapacity = 5989560

maxLifespan:
  baseLifespan = 100 (human)
  levelBonus = LifespanLevelBonus[7] = 2000   // L7+ → последний элемент
  latePenalty = CalcLatePenalty(15) = 0        // 15 ≤ 20
  maxLifespan = 100 + 2000 - 0 = 2100

stats:
  levelGrowth = (9 - 1) × 2 = 16
  STR = 10 + 16 + 1 = 27   // randomBonus = 1
  AGI = 10 + 16 + 0 = 26   // randomBonus = 0
  VIT = 10 + 16 + 2 = 28   // randomBonus = 2
  INT = 10 + 16 + 1 = 27   // randomBonus = 1
```

**Шаг 2: SpeciesData (human)**

```
SoulType=Character, Morphology=Humanoid, Material=Organic, Size=Medium
BaseVitality=10, LifespanRange=(70, 100)
```

**Шаг 3: Body (11 частей, VIT=28)**

```
vitalityMultiplier = 1.0 + (28 - 10) × 0.05 = 1.9
sizeMultiplier = 1.0 (Medium)

Head:   Red=Round(50×1.9×1.0)=95,  Black=Round(100×1.9×1.0)=190
Torso:  Red=Round(100×1.9×1.0)=190, Black=Round(200×1.9×1.0)=380
Heart:  Red=Round(80×1.9×1.0)=152,  Black=0 (CORE-C01)
LArm:   Red=Round(40×1.9×1.0)=76,  Black=Round(80×1.9×1.0)=152
RArm:   Red=Round(40×1.9×1.0)=76,  Black=Round(80×1.9×1.0)=152
LHand:  Red=Round(20×1.9×1.0)=38,  Black=Round(40×1.9×1.0)=76
RHand:  Red=Round(20×1.9×1.0)=38,  Black=Round(40×1.9×1.0)=76
LLeg:   Red=Round(50×1.9×1.0)=95,  Black=Round(100×1.9×1.0)=190
RLeg:   Red=Round(50×1.9×1.0)=95,  Black=Round(100×1.9×1.0)=190
LFoot:  Red=Round(25×1.9×1.0)=48,  Black=Round(50×1.9×1.0)=95
  // 25×1.9=47.5 → RoundToInt(47.5)=48 (банковское округление к чётному)
RFoot:  Red=Round(25×1.9×1.0)=48,  Black=Round(50×1.9×1.0)=95

MaxHealth = Σ RedHP = 95+190+152+76+76+38+38+95+95+48+48 = 951
```

**Шаг 5: Экипировка (гуманоид, L9+)**

```
7 слотов, все предметы генерируются для уровня 9
Грейд экипировки: Perfect/Transcendent вероятнее
  (NPCConfig.EquipmentGradeWeightsByLevel[5] для L9: {0,10,30,40,20})
```

**Шаг 6: Техники (L7+ → 4)**

```
4 техники для уровня 9
```

**Шаг 7: Инвентарь**

```
L1+: heal_pill ×1
L3+, 10%: Qi-настойка — выпал!
0-2 материала: 1 кусок (material_spirit_stone_shard, L9≥5 → Uncommon)
L3+, 10%: spirit_stone_shard — выпал!
L5+, 5%: spirit_stone_fragment — выпал! (5% шанс)
```

**Шаг 8: Боевые параметры**

```
BaseDamage:
  baseDmg = 10
  × 1.0 (Medium) = 10
  × (1.0 + 8×0.1) = 1.8 → 18
  × (1 + 4×0.1) = 1.4 → 25.2
  → (int)25 = 25

BaseDefense:
  defense = 0 (Organic) + 0 (enhancement) + 7×5 = 35
  → 35

AggressionLevel = 0.4 (Guard)
```

---

## Сводная таблица сравнения

| Параметр              | L3.5 Культиватор  | L6.3 Старейшина    | L9.7 Страж         |
|-----------------------|--------------------|---------------------|---------------------|
| **Уровень**           | 3.5                | 6.3                 | 9.7                 |
| **CoreQuality**       | Normal (×1.0)      | Refined/Good (×1.2) | Perfect/Exc. (×1.5) |
| **CoreCapacity**      | 10 834             | 187 486             | 5 988 060           |
| **CurrentQi**         | 10 834             | 187 486             | 5 988 060           |
| **QiDensity**         | 4                  | 32                  | 256                 |
| **Conductivity**      | 34.17              | 1 129.08            | 57 753.9            |
| **Age**               | 68                 | 146                 | 205                 |
| **MaxLifespan**       | 200                | 900                 | 2 100               |
| **STR**               | 15                 | 21                  | 27                  |
| **AGI**               | 14                 | 20                  | 26                  |
| **VIT**               | 16                 | 22                  | 28                  |
| **INT**               | 15                 | 21                  | 27                  |
| **MaxHealth**         | 649                | 800                 | 951                 |
| **Техники**           | 2                  | 3                   | 4                   |
| **Слоты экипировки**  | 7                  | 7                   | 7                   |
| **BaseDamage**        | 14                 | 19                  | 25                  |
| **BaseDefense**       | 35                 | 35                  | 35                  |
| **AggressionLevel**   | 0.20               | 0.15                | 0.40                |

---

## Известные расхождения и замечания

### 1. Vitality для BodyFactory

**Текущий код** (`NPCAssemblyService.cs:94`):
```csharp
float vitality = species.BaseVitality;  // human = 10
```

Передаётся `species.BaseVitality` (=10 для человека), а НЕ рассчитанный стат VIT.  
Если использовать species.BaseVitality=10, то `vitalityMultiplier = 1.0` и все  
гуманоиды имеют одинаковое HP тела (500), независимо от уровня.

**В примерах выше** использован рассчитанный VIT для демонстрации масштабирования.  
Для точного соответствия коду — все три NPC имели бы MaxHealth=500.

### 2. ConductivityGrowthFactors

**NPCConfig** определяет: `{1.0, 1.2, 1.5, 2.0, 3.0, 5.0, 8.0, 12.0}` (L0-L7+).  
Старая документация указывала: `{0.0, 0.5, 0.8, 1.2, 1.5, 2.0, 2.5, 3.0}`.  
В примерах используются текущие значения из кода.

### 3. Названия качеств ядра

| Код (CoreQuality enum) | Документация задачи | Множитель |
|------------------------|---------------------|-----------|
| Refined (5)            | Good                | 1.2       |
| Perfect (6)            | Excellent           | 1.5       |

### 4. Банковское округление (RoundToInt)

`Mathf.RoundToInt` использует банковское округление (к чётному):  
`47.5 → 48`, `32.5 → 32`. Это влияет на HP конечностей при нецелых произведениях.

### 5. BaseDefense не масштабируется

Для всех гуманоидов с Organic материалом BaseDefense = 35 (7 слотов × 5),  
поскольку `BodyMaterialReduction[Organic] = 0.0`. Уровень не влияет на защиту  
в текущей реализации CalculateBaseDefense.

### 6. Точность 1.1^totalSubLevels

Код использует `Math.Pow(1.1, totalSubLevels)` (double precision).  
Значения в примерах — приближённые. Фактические значения зависят от  
IEEE 754 double precision вычислений.
