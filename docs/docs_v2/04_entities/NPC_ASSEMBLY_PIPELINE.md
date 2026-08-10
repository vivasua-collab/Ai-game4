# Пайплайн сборки NPC

> **Раздел:** Сущности мира — генерация
> **Статус:** Концептуальная спецификация (дизайн-документ)
> **Самостоятельный документ:** не требует иных файлов для понимания.
>
> Этот документ описывает **полный пайплайн генерации и сборки NPC** — от создания души до финальной интеграции в мир. Определяет порядок шагов, формулы, данные и зависимости между подсистемами.

---

## 1. Назначение

NPC собирается **последовательно через 8 шагов**. Каждый шаг производит структуру данных, которая передаётся следующему шагу. Итог — полностью собранный NPC, готовый к регистрации в мире.

```
Душа → Фенотип → Тело → Ци/Проводимость → Экипировка/Усиление → Техники → Инвентарь → Регистрация
```

```
ВХОД: speciesId, roleId, locationLevel, seed

ШАГ 1: ГЕНЕРАЦИЯ ДУШИ
├── Уровень: locationLevel + delta(-2..+1)
├── Возраст: по MortalStage + уровень культивации
├── Качество ядра: CoreQuality (взвешенный рандом)
├── Тип пробуждения: AwakeningType (взвешенный рандом)
└── Результат: SoulData
       │
       ▼
ШАГ 2: ВЫБОР ФЕНОТИПА (видовая принадлежность)
├── SpeciesData из SpeciesRegistry
├── SoulType + Morphology + BodyMaterial + SizeClass
├── Базовые статы вида (STR/AGI/VIT/INT)
└── Источник: ENTITY_TYPES.md §4
       │
       ▼
ШАГ 3: СБОРКА ТЕЛА
├── BodyFactory.CreateBody(morphology, size, vitality)
├── Формирование меридиан (структура)
├── Расчёт HP всех частей тела
└── Источник: BODY_SYSTEM.md
       │
       ▼
ШАГ 4: РАСЧЁТ ЦИ И ПРОВОДИМОСТИ
├── coreCapacity = 1000 × 1.1^totalSubLevels × qualityMult
├── qiDensity = 2^(level-1)
├── conductivity = coreCapacity / 360 × ageMult
├── currentQi = coreCapacity (полное при генерации)
└── Источник: QI_SYSTEM.md
       │
       ▼
ШАГ 5: ЭКИПИРОВКА / УСИЛЕНИЕ ТЕЛА
├── Гуманоидные расы → оружие + броня + зарядник (L3+)
├── Негуманоидные расы → усиление частей тела
├── Spirit/Construct → усиление через Ци
└── См. §5 ниже
       │
       ▼
ШАГ 6: ГЕНЕРАЦИЯ ТЕХНИК И ФОРМАЦИЙ
├── Количество техник: 1–3 (по уровню)
├── Типы: Combat/Defense/Support (по роли)
├── Уровень техник: ≤ уровень NPC
├── Формации: только L5+ (редко)
└── Источник: TECHNIQUE_SYSTEM.md
       │
       ▼
ШАГ 7: ЗАПОЛНЕНИЕ ИНВЕНТАРЯ
├── Расходники (1–3 шт)
├── Хлам/материалы (0–2 шт)
├── Духовные камни (шанс × уровень)
└── См. §7 ниже
       │
       ▼
ШАГ 8: ФИНАЛИЗАЦИЯ
├── Сборка NPCState из всех компонентов
├── Регистрация в NPCService
├── Публикация NPCSpawnedEvent
└── Выход: npcId
```

---

## 2. Шаг 1: Генерация души

### 2.1. Входные параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| `locationLevel` | int    | Уровень локации (0–10) |
| `speciesId`     | string | ID вида из SpeciesRegistry |
| `roleId`        | NPCRole | Роль NPC |
| `seed`          | long   | Seed для детерминированной генерации |

### 2.2. Определение уровня культивации

```
npcLevel = locationLevel + delta
delta = random(-2, +1)   // дельта уровня: -2..+1
npcLevel = max(0, npcLevel)
```

**Дельта уровней (взвешенный рандом):**

| delta | Вес |
|-------|-----|
| −2 | 18% |
| −1 | 36% |
| ±0 | 41% |
| +1 | 5% |

> **Кап локации:** `npcLevel ≤ locationLevel + 0.9`. В локации L9 не может быть существ выше L9.9. Если delta даёт уровень выше капа — клампим.

### 2.3. Определение возраста

**Смертные (L0):**
```
age = random(MortalStage.Adult.min, MortalStage.Elder.max)   // 16–80 лет
```

**Практики (L1+):**
```
awakeningAge = random(16, 40)
cultivationYears = level × random(10, 30) + random(0, 20)
npcAge = awakeningAge + cultivationYears

// Старение замедляется (AgingMultipliers[level]):
// L1–L2: ×1.0 (нормальное старение)
// L3: ×0.9
// L4: ×0.4 (резкое замедление)
// L5: ×0.3
// L6: ×0.1
// L7+: ×0.0 (старение остановлено)
```

> Драконы и духи имеют другие возрастные диапазоны — см. `SpeciesData.LifespanRange`.

### 2.4. Качество ядра (взвешенный рандом)

| CoreQuality | Множитель ёмкости | Вес (Character) | Вес (Creature) |
|-------------|-------------------|-----------------|----------------|
| Fragmented   | ×0.5  | 5%  | 20% |
| Cracked      | ×0.7  | 15% | 30% |
| Flawed       | ×0.85 | 25% | 25% |
| Normal       | ×1.0  | 35% | 20% |
| Refined      | ×1.2  | 14% | 4%  |
| Perfect      | ×1.5  | 5%  | 1%  |
| Transcendent | ×2.0  | 1%  | 0%  |

> Spirit/Construct используют reservoir, не core. Качество ядра не применяется.
>
> **Единые множители для игрока и NPC:** {0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0}.

### 2.5. Тип пробуждения

| AwakeningType | Вес | Описание |
|---------------|-----|----------|
| `Natural` | 20% | Естественное пробуждение — более плавный рост |
| `Guided`  | 50% | Под руководством наставника — стандартный путь |
| `Artifact`| 20% | Через артефакт — быстрый старт, без бонусов к проводимости |
| `Forced`  | 10% | Принудительное — опасный путь, +1 черта (вкус/особенность) |

> **Тип пробуждения НЕ даёт бонусов/штрафов к проводимости.** Влияние на проводимость — только через ядро (ёмкость) и развитие (перки, медитация). AwakeningType — только flavour/черты.

### 2.6. Структура SoulData

Ключевые поля:
- `CultivationLevel`, `SubLevel`, `Age`, `MortalStage`, `CoreQuality`, `AwakeningType`
- `CoreCapacity`, `Conductivity`, `QiDensity`
- `QualityMultiplier`, `ConductivityGrowthMultiplier`, `MaxLifespan`

---

## 3. Шаг 2: Выбор фенотипа

### 3.1. Источник данных

`SpeciesRegistry.GetSpecies(speciesId)` → `SpeciesData`

Все виды зарегистрированы в SpeciesRegistry:
- **Character:** human, elf, demon, giant
- **Creature:** wolf, tiger, dragon, phoenix, spider
- **Spirit:** ghost
- **Construct:** golem

### 3.2. Выбор вида по роли

| NPCRole | Предпочтительные виды |
|---------|----------------------|
| `Monster`    | wolf, tiger, spider (SoulType.Creature) |
| `Guard`      | human (SoulType.Character) |
| `Merchant`   | human, elf (SoulType.Character) |
| `Cultivator` | human, elf, demon (SoulType.Character) |
| `Elder`      | human, elf (SoulType.Character) |
| `Disciple`   | human, demon (SoulType.Character) |
| `Enemy`      | human, demon (SoulType.Character) |
| `Passerby`   | human (SoulType.Character) |

### 3.3. Данные фенотипа

| Поле | Описание | Пример (human) |
|------|----------|----------------|
| `SoulType`         | Природа сущности     | Character |
| `Morphology`       | Морфология тела      | Humanoid |
| `BodyMaterial`     | Материал тела        | Organic |
| `SizeClass`        | Класс размера        | Medium |
| `BaseStrength`     | Базовая сила         | 10 |
| `BaseAgility`      | Базовая ловкость     | 10 |
| `BaseVitality`     | Базовая живучесть    | 10 |
| `BaseIntelligence` | Базовый интеллект    | 10 |
| `BaseAgeRange`     | Диапазон возраста    | (16, 30) |
| `LifespanRange`    | Диапазон жизни       | (70, 100) |

---

## 4. Шаг 3: Сборка тела

### 4.1. Порядок сборки

```
1. Получить BodyTemplate из BodyTemplateProvider по Morphology
2. Рассчитать множитель HP от Vitality:
   hpMultiplier = 1 + (VIT - 10) × 0.05
3. Рассчитать множитель HP от SizeClass:
   sizeMultiplier = SizeClassHPMultipliers[size]
4. Для каждой части из шаблона:
   effectiveRedHP   = baseFunctionalHP × hpMultiplier × sizeMultiplier
   effectiveBlackHP = baseStructuralHP × hpMultiplier × sizeMultiplier
5. Создать BodyPart с расчётными HP
```

### 4.2. Меридианы (духовная система)

> **Ключевой принцип:** Меридианы привязаны к **ДУШЕ**, а не к телу.

Это духовные практики — каналы, по которым Ци протекает от ядра души через тело практика. Меридианы определяют **проводимость** — скорость прохождения Ци через духовную систему практика.

**Структура меридиан:**
- **Ядро души** (core) — корень системы меридиан, источник Ци.
- **Основной ствол** — определяет базовую проводимость (`coreCapacity / 360`).
- **Ветви** — расходятся от ствола, пронизывают тело практика.
- **Узлы вывода** — точки выхода Ци (для техник).

**Взаимосвязь душа–тело:**
- Меридианы как духовная структура существуют **независимо от тела**.
- Тело выступает как «проводник» — физическая материя, через которую меридианы пропускают Ци.
- Повреждение тела (ранение части) может **затруднить** прохождение Ци через соответствующую ветвь меридианы, но не разрушает саму меридиану.
- Смерть тела → меридианы остаются при душе (для духов/реинкарнации).

> **Следствие:** Spirit (бесплотные) имеют меридианы, но без физического тела — Ци течёт напрямую, проводимость = `coreCapacity / 360` (без возрастного роста, т.к. нет старения тела). Construct (искусственные тела) — меридианы «вплетены» в конструкт при создании, проводимость фиксированная, не растёт.

---

## 5. Шаг 4: Расчёт Ци и проводимости

### 5.1. Формула ёмкости ядра

```
totalSubLevels = (cultivationLevel - 1) × 10 + subLevel
coreCapacity = BASE_CORE_CAPACITY × CORE_CAPACITY_GROWTH^totalSubLevels × qualityMultiplier

// Где:
// BASE_CORE_CAPACITY = 1000
// CORE_CAPACITY_GROWTH = 1.1
// qualityMultiplier = из таблицы CoreQuality (§2.4)
```

### 5.2. Формула проводимости

```
baseConductivity = coreCapacity / 360
finalConductivity = baseConductivity × conductivityGrowthMultiplier × (1 + perkBonuses + meditationBonuses + bodyTypeBonuses)
```

### 5.3. Возраст → рост проводимости

> Проводимость **только увеличивается** с возрастом — меридианы развиваются и укрепляются со временем практики. Деградации меридиан с возрастом НЕТ.

```
conductivityGrowthMultiplier = 1.0 + ageGrowthRate × effectiveAge

// Где:
// ageGrowthRate = 0.001
// effectiveAge = chronologicalAge × levelGrowthFactor(level)

// levelGrowthFactor по уровням культивации:
// L0: 1.0, L1: 1.2, L2: 1.5, L3: 2.0, L4: 3.0, L5: 5.0, L6: 8.0, L7+: 12.0

// Пример: L5 практик в 100 лет:
//   effectiveAge = 100 × 5.0 = 500
//   growthMult = 1.0 + 0.001 × 500 = 1.5
//   Проводимость ×1.5 от базовой
```

**Исключения:**
- Spirit: нет физического тела → проводимость = `coreCapacity / 360` (без возрастного роста).
- Construct: нет меридиан в органическом смысле → проводимость = базовая (без возрастного роста).

### 5.4. Плотность Ци

```
qiDensity = 2^(cultivationLevel - 1)
```

| Уровень | Плотность |
|---------|-----------|
| L1 | 1 |
| L2 | 2 |
| L3 | 4 |
| L4 | 8 |
| L5 | 16 |
| L6 | 32 |
| L7 | 64 |
| L8 | 128 |
| L9 | 256 |

### 5.5. Максимальная продолжительность жизни

```
maxLifespan = baseLifespan(phenotype) + levelBonus(cultivationLevel) - lateStartPenalty(breakthroughAge)

// LifespanLevelBonus:
// L0: 0, L1: +20, L2: +50, L3: +100, L4: +200,
// L5: +400, L6: +800, L7+: +2000

// lateStartPenalty (по awakeningAge):
// age ≤ 20  → 0
// 20 < age ≤ 40 → (age-20) × 2
// age > 40  → 40 + (age-40) × 5
```

> Практик, прорвавшийся на уровень раньше, живёт дольше практика того же уровня, прорвавшегося позже.

### 5.6. Текущее Ци при генерации

```
currentQi = coreCapacity   // Полное при генерации
```

NPC генерируются с полным Ци — как будто только достигли этого уровня.

### 5.7. Источники развития проводимости

| Источник | Тип | Величина |
|----------|-----|----------|
| Возраст (естественный рост)            | Постоянный | Растёт только вверх (формула выше) |
| Медитация на развитие меридиан         | Постоянный | +X% за цикл медитации (TBD) |
| Тип телосложения (вид)                 | Постоянный | Видовой бонус (TBD) |
| Врождённый перк «Золотое качество тела»| Постоянный | +30% |
| Приобретённый перк «Закалка меридиан»  | Постоянный | +15% |
| Приобретённый перк «Небесные каналы»   | Постоянный | +20% |
| Формация «Круг поглощения»             | Локальный  | +25% environmentMult (НЕ проводимость) |

> ⛔ Временные баффы проводимости (`ConductivityBoost`) — **УДАЛЕНЫ**. См. `02_systems/BUFF_MODIFIERS_SYSTEM.md` и `02_systems/PERK_SYSTEM.md`.

---

## 6. Шаг 5: Экипировка / усиление тела

### 6.1. Два пути

```
ГУМАНОИДНЫЕ РАСЫ (Morphology = Humanoid, Hybrid)
├── Оружие (1 шт)
├── Броня (1–4 слота)
└── Зарядник (L3+)
    через ItemGenerator

НЕГУМАНОИДНЫЕ / SPIRIT (Morphology ≠ Humanoid)
└── Усиление частей тела:
    • NaturalArmor
    • NaturalWeapon
    • BodyHardening
    • QiInfusion
    • SizeGrowth
```

### 6.2. Гуманоидная экипировка

| Слот | Условие | Источник |
|------|---------|----------|
| Оружие (WeaponMain) | Все практики L1+ | ItemGeneratorService |
| Броня Torso | L2+ | ItemGeneratorService |
| Броня Head  | L3+ | ItemGeneratorService |
| Броня Legs  | L3+ | ItemGeneratorService |
| Броня Feet  | L4+ | ItemGeneratorService |
| Зарядник Ци | L3+ | ItemGeneratorService |

### 6.3. Система усиления частей тела (для негуманоидных)

| Тип усиления | Описание | Пример |
|--------------|----------|--------|
| `NaturalArmor`   | Плотность кожи/чешуи | Волк: +20% reduction к Torso |
| `NaturalWeapon`  | Врождённое оружие | Когти (MeleeStrike), Клыки, Хелицеры (Venom) |
| `BodyHardening`  | Закалка части тела | Жёсткий хитин: +30 HP к Cephalothorax |
| `QiInfusion`     | Ци-пропитка части | +50% урон атаки этой частью |
| `SizeGrowth`     | Увеличение размера | Medium → Large (редко, для боссов) |

**Формулы:**
```
NaturalArmor:    effectiveDamage = rawDamage × (1 - naturalArmorReduction)
NaturalWeapon:   naturalWeaponDamage = baseDamage × sizeMultiplier × (1 + qiInfusionBonus)
BodyHardening:   enhancedMaxHP = baseMaxHP + hardeningBonus
QiInfusion:      qiInfusedDamage = baseDamage × (1 + qiInfusionLevel × 0.25)
```

**Усиления по видам (примеры):**

| Вид | Уровень | Усиления |
|-----|---------|----------|
| Волк     | L1–3 | NaturalWeapon(Когти, +5), NaturalArmor(Torso, +10%) |
| Тигр     | L2–5 | NaturalWeapon(Клыки, +10), NaturalArmor(Torso, +20%) |
| Дракон   | L5+  | NaturalWeapon(Когти, +20), NaturalArmor(All, +30%), QiInfusion(Torso, +50%) |
| Паук     | L0–2 | NaturalWeapon(Хелицеры, +3, Venom), BodyHardening(Cephalothorax, +15) |
| Призрак  | L1+  | QiInfusion(Core, +100%), QiInfusion(Essence, +50%) |
| Голем    | L1–3 | BodyHardening(All, +50), NaturalArmor(All, +25%) |

---

## 7. Шаг 6: Генерация техник и формаций

### 7.1. Количество техник по уровню

| Уровень NPC | Кол-во техник | Примечание |
|-------------|---------------|------------|
| L0 (смертный) | 0 | Смертные не используют техники |
| L1–L2 | 1 | Базовая техника |
| L3–L4 | 1–2 | + защитная техника (50%) |
| L5–L6 | 2–3 | + формация (10%) |
| L7–L8 | 3 | + формация (30%) |
| L9+   | 3+ | + формация (50%) |

### 7.2. Типы техник по роли

| NPCRole | Предпочтительные типы | Примеры |
|---------|----------------------|---------|
| `Monster`    | Combat, Poison | Врождённые атаки зверей |
| `Guard`      | Combat, Defense | Защитные стойки, контратаки |
| `Merchant`   | Support, Healing | Самоисцеление, ускорение |
| `Cultivator` | Combat, Support, Cultivation | Атакующие и поддерживающие |
| `Elder`      | Все типы | Широкий набор |
| `Enemy`      | Combat, Curse | Агрессивные техники |

### 7.3. Grade техник (взвешенный рандом)

| Grade | Вес | Множитель урона |
|-------|-----|-----------------|
| Common       | 60% | ×1.0 |
| Refined      | 30% | ×1.3 |
| Perfect      | 9%  | ×1.6 |
| Transcendent | 1%  | ×2.0 |

> Стоимость Ци всегда ×1.0 — не зависит от Grade.

### 7.4. Генератор техник

Техника — это знание/навык души, **не предмет**. TechniqueGenerator — **отдельный независимый генератор**, не часть ItemGeneratorService.

Архитектура генерации техник (Матрёшка):
1. **База** — тип техники → qiCost, capacity, baseDamage.
2. **Grade** — множитель качества (Common/Refined/Perfect/Transcendent).
3. **Бонусы** — эффекты от Grade и стихии.

### 7.5. Формации

Формации доступны только для NPC L5+ и редко:
- L5–L6: 10% шанс иметь формацию.
- L7–L8: 30% шанс.
- L9+: 50% шанс.

> См. `02_systems/FORMATION_SYSTEM.md`.

---

## 8. Шаг 7: Заполнение инвентаря

### 8.1. Система полного лута

При смерти NPC весь его инвентарь и экипировка становятся доступными для подбора игроком:
1. Экипированные предметы (оружие, броня, зарядник).
2. Расходники из быстрых слотов.
3. Хлам/материалы в рюкзаке.
4. Духовные камни (если есть).

### 8.2. Генерация содержимого инвентаря

| Категория | Кол-во | Условие | Источник |
|-----------|--------|---------|----------|
| Лечебные пилюли            | 1–2 | L1+ | ConsumableGenerator |
| Ци-настойки                | 0–1 | L3+ | ConsumableGenerator |
| Куски материалов           | 0–2 | Все | ItemDatabaseService |
| Духовные камни (осколки)   | 0–1 | L3+ (10%) | Прямая генерация |
| Духовные камни (фрагменты) | 0–1 | L5+ (5%)  | Прямая генерация |

### 8.3. «Хлам» в инвентаре

Хлам — низкоуровневые расходники и материалы, которые NPC носит «на всякий случай»:
- Повреждённые пилюли (лечение 50% от нормы).
- Куски руды (Tier 1–2).
- Обрывки кожи/ткани.
- Простые инструменты.

---

## 9. Шаг 8: Финализация

### 9.1. Сборка NPCState

Из всех промежуточных структур собирается финальный `NPCState`:

| Источник | Поля в NPCState |
|----------|-----------------|
| Шаг 1 (Душа)     | CultivationLevel, SubLevel, CoreQuality, MaxQi, CurrentQi, Conductivity, Age, AwakeningType, AwakeningAge, MortalStage, QiDensity, MaxLifespan, Strength, Agility, Vitality, Intelligence |
| Шаг 2 (Фенотип)  | SpeciesId, SoulType, Morphology, BodyMaterial, DisplayName |
| Шаг 3 (Тело)     | BodyParts (List), MaxHealth, CurrentHealth |
| Шаг 5 (Экипировка) | EquipmentIds (Dictionary) |
| Шаг 6 (Техники)   | TechniqueIds (List) |
| Шаг 7 (Инвентарь) | InventorySlots (List) |
| Прочее            | AggressionLevel, CurrentLocation |

### 9.2. Регистрация

- Сборка `NPCState` из всех компонентов.
- Регистрация в `NPCService`.
- Публикация события `NPCSpawnedEvent` через **шину событий**.
- Выход: `npcId` (GUID).

---

## 10. Сводная таблица формул

| Параметр | Формула | Источник |
|----------|---------|----------|
| `npcLevel`               | `locationLevel + delta(-2..+1)` | Pipeline |
| `totalSubLevels`         | `(level - 1) × 10 + subLevel` | QI_SYSTEM |
| `coreCapacity`           | `1000 × 1.1^totalSubLevels × qualityMult` | QI_SYSTEM |
| `qiDensity`              | `2^(level - 1)` | QI_SYSTEM / ALGORITHMS |
| `baseConductivity`       | `coreCapacity / 360` | QI_SYSTEM |
| `finalConductivity`      | `baseCond × growthMult × (1 + perkBonuses + meditationBonuses + bodyTypeBonuses)` | QI_SYSTEM + Pipeline |
| `conductivityGrowthMultiplier` | `1.0 + 0.001 × effectiveAge` (где `effectiveAge = age × levelGrowthFactor(level)`) | Pipeline |
| `hpMultiplier`           | `1 + (VIT - 10) × 0.05` | BODY_SYSTEM |
| `effectiveHP`            | `baseHP × hpMult × sizeMult` | BODY_SYSTEM |
| `MaxHealth`              | `Σ(all BodyPart.MaxRedHP)` | Pipeline |
| `naturalWeaponDamage`    | `baseDamage × sizeMult × (1 + qiInfusion)` | Pipeline |
| `maxLifespan`            | `max(1, baseLifespan + levelBonus − lateStartPenalty)` | Pipeline |

---

## 11. Зависимости от модулей

| Модуль | Что используется |
|--------|------------------|
| Body (BodyFactory)            | Создание тела по морфологии |
| Body (BodyTemplateProvider)   | 10 шаблонов морфологий |
| Body (SpeciesRegistry)        | Реестр видов (11 видов) |
| Qi (GameConstants)            | Формулы Ци, плотность, проводимость |
| Generator (ItemGeneratorService) | Генерация оружия, брони, расходников |
| NPC (NPCService)              | Регистрация NPC |
| NPC (NPCSpawnerService)       | Спавн NPC |
| NPC (NPCState)                | Runtime-состояние NPC |

### 11.1. Что нужно создать

| Компонент | Приоритет | Описание |
|-----------|-----------|----------|
| `SoulData`                | HIGH   | Структура данных души |
| `SoulGenerator`           | HIGH   | Генерация души (уровень, возраст, ядро) |
| `NPCAssemblyService`      | HIGH   | Оркестратор пайплайна сборки |
| `BodyEnhancementSystem`   | MEDIUM | Система усиления частей тела |
| `TechniqueGeneratorService` | MEDIUM | Генерация техник для NPC |
| `NPCSpawnerService` (обновление) | HIGH | Интеграция с NPCAssemblyService |
| `NPCNameGenerator`        | MEDIUM | Генерация имён по роли и культуре |

---

## 12. Открытые вопросы

1. **Проводимость по возрасту:** Формулы роста и множителя уровня — определены (`growthMultiplier = 1.0 + 0.001 × effectiveAge`, `effectiveAge = age × levelGrowthFactor(level)`).
2. **Дельта уровней:** Утверждено — `-2:18% / -1:36% / 0:41% / +1:5%`. Ограничение: `npcLevel ≤ locationLevel + 0.9`.
3. **Система меридиан:** Меридианы привязаны к **ДУШЕ**, не к телу. Тело — проводник. Конкретная механика ранение→проводимость — TBD.
4. **Генератор техник:** Отдельный независимый генератор. Техника ≠ предмет.
5. **Полный лут:** Нужен документ, описывающий систему лута при убийстве NPC.
6. **Инвентарь NPC:** Как NPC хранит предметы? Через InventoryService или собственный словарь.
7. **Унифицированные модули:** Все этапы создания NPC и игрока должны использовать одни и те же модули расчёта. `CoreQuality` множители = {0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0}.
8. **Единая система урона:** Все сущности (игрок + NPC) получают урон через единую систему BodyParts. NPCCombatAdapter НЕ вычитает HP напрямую. Весь урон → DamageService → BodyParts → пересчёт CurrentHealth.
9. **AwakeningType и проводимость:** Окончательно решено — AwakeningType НЕ даёт бонусов/штрафов к проводимости.

---

## 13. Пример сборки (концептуальный)

**Человек-Культиватор L3.5:**
- CultivationLevel: InternalFire (L3), SubLevel: 5
- Age: 68, AwakeningAge: 18
- CoreQuality: Normal (множитель = 1.0)
- AwakeningType: Guided
- CoreCapacity: 10 834 (long)
- CurrentQi: 10 834 (полное при генерации)
- QiDensity: 4
- Conductivity: 34.17 (float)
- ConductivityGrowthMultiplier: 1.136
- MaxLifespan: 200
- MortalStage: None (практик)
- InnateElement: Neutral (Character)

**Человек L6.3 Культиватор (пример):**
- Max Ци: 146 974
- Плотность Ци: 32 (2^5)
- Проводимость: 408.26 ед/сек
- HP: 3165/3165
- Части тела: 11 (Head, Torso, Heart, LeftArm, RightArm, LeftLeg, RightLeg, LeftHand, RightHand, LeftFoot, RightFoot)
- Итого урон: 368
- Итого защита: 91
- Оружие: [Refined] Улучшенный Духовное железо Меч (урон 68)
- Для прорыва нужно: 1 469 740 Ци

---

## 14. Архитектурное представление

Пайплайн сборки реализуется как **оркестрирующий сервис** `NPCAssemblyService`:
- точка входа — метод `Assemble(speciesId, roleId, locationLevel, seed)`;
- последовательно вызывает генераторы/фабрики каждого шага;
- результат — `NPCState`, регистрируемый в `NPCService`;
- все генераторы — pure C#, регистрируются в DI-контейнере;
- кросс-модульные взаимодействия — через **шину событий**.

Производительность:
- Сборка одного NPC — ~1–5 мс (одноразовая операция при спавне).
- Не должна выполняться в hot-path (только при спавне/загрузке).
- Может выполняться на worker thread.

Детерминизм:
- Все генераторы используют `seed`-based random (SeededRandom).
- Одинаковый seed + одинаковые входы → одинаковый NPC.

---

## 15. Связанные документы

- `04_entities/NPC.md` — Общая система NPC, конфигурация
- `04_entities/ENTITY_TYPES.md` — Иерархия типов (SoulType → Morphology → Species)
- `04_entities/NPC_AI_SYSTEM.md` — Теория AI
- `02_systems/BODY_SYSTEM.md` — Система тела, Vitality, SizeClass
- `02_systems/QI_SYSTEM.md` — Ци, ядро, проводимость, меридианы
- `02_systems/TECHNIQUE_SYSTEM.md` — Техники, ёмкость, Grade
- `06_player/EQUIPMENT_SYSTEM.md` — Экипировка, Grade, прочность
- `02_systems/FORMATION_SYSTEM.md` — Формации (для L5+)
- `05_data/GENERATORS_SYSTEM.md` — Генераторы предметов
- `09_workflow/ALGORITHMS.md` — Формулы, мягкие капы, §23–25

---

*Концептуальный документ. Все шаги, формулы и зависимости — канонические и обязательны к реализации.*
