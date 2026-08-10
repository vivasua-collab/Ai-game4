# 🧬 NPC Assembly Pipeline — Пайплайн сборки NPC

**Версия:** 1.2
**Дата:** 2026-05-20 (обновлено — разрешения противоречий аудита)
**Статус:** 🟡 В разработке
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)

---

## ⚠️ Назначение документа

> Этот документ описывает **полный пайплайн генерации и сборки NPC** — от создания души
> до финальной интеграции в мир. Определяет порядок шагов, формулы, данные и зависимости
> между подсистемами. Является рабочим черновиком для реализации.

---

## 📋 Краткий обзор

NPC собирается последовательно через **8 шагов**. Каждый шаг производит
структуру данных, которая передаётся следующему шагу. Итог — полностью
собранный NPC, готовый к регистрации в мире.

```
Душа → Фенотип → Тело → Ци/Проводимость → Экипировка/Усиление → Техники → Инвентарь → Регистрация
```

---

## 🔄 Полный пайплайн сборки

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    NPC ASSEMBLY PIPELINE                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ВХОД: speciesId, roleId, locationLevel, seed                          │
│                                                                         │
│  ШАГ 1: ГЕНЕРАЦИЯ ДУШИ                                                 │
│  ├── Уровень: locationLevel + delta(-2..+1)                             │
│  ├── Возраст: по MortalStage + уровень культивации                      │
│  ├── Качество ядра: CoreQuality (взвешенный рандом)                     │
│  ├── Тип пробуждения: AwakeningType (взвешенный рандом)                 │
│  └── Результат: SoulData                                               │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 2: ВЫБОР ФЕНОТИПА (видовая принадлежность)                        │
│  ├── SpeciesData из SpeciesRegistry                                     │
│  ├── SoulType + Morphology + BodyMaterial + SizeClass                   │
│  ├── Базовые статы вида (STR/AGI/VIT/INT)                              │
│  └── Источник: ALGORITHMS.md §25, ENTITY_TYPES.md §4                   │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 3: СБОРКА ТЕЛА                                                    │
│  ├── BodyFactory.CreateBody(morphology, size, vitality)                 │
│  ├── Формирование меридиан (структура)                                  │
│  ├── Расчёт HP всех частей тела                                         │
│  └── Источник: BODY_SYSTEM.md, BodyTemplateProvider                     │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 4: РАСЧЁТ ЦИ И ПРОВОДИМОСТИ                                       │
│  ├── coreCapacity = 1000 × 1.1^totalSubLevels × qualityMult             │
│  ├── qiDensity = 2^(level-1)                                            │
│  ├── conductivity = coreCapacity / 360 × ageMult                        │
│  ├── currentQi = coreCapacity (полное при генерации)                    │
│  └── Источник: QI_SYSTEM.md, ALGORITHMS.md §3-4                        │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 5: ЭКИПИРОВКА / УСИЛЕНИЕ ТЕЛА                                     │
│  ├── Гуманоидные расы → оружие + броня + зарядник (L3+)                │
│  ├── Негуманоидные расы → усиление частей тела                          │
│  ├── Spirit/Construct → усиление через Ци                               │
│  └── См. §5 ниже                                                        │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 6: ГЕНЕРАЦИЯ ТЕХНИК И ФОРМАЦИЙ                                    │
│  ├── Количество техник: 1-3 (по уровню)                                 │
│  ├── Типы: Combat/Defense/Support (по роли)                             │
│  ├── Уровень техник: ≤ уровень NPC                                      │
│  ├── Формации: только L5+ (редко)                                       │
│  └── Источник: TECHNIQUE_SYSTEM.md                                      │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 7: ЗАПОЛНЕНИЕ ИНВЕНТАРЯ                                            │
│  ├── Расходники (1-3 шт)                                                │
│  ├── Хлам/материалы (0-2 шт)                                            │
│  ├── Духовные камни (шанс × уровень)                                    │
│  └── См. §7 ниже                                                        │
│         │                                                                │
│         ▼                                                                │
│  ШАГ 8: ФИНАЛИЗАЦИЯ                                                     │
│  ├── Сборка NPCState из всех компонентов                                 │
│  ├── Регистрация в NPCService                                           │
│  ├── Публикация NPCSpawnedEvent                                         │
│  └── Выход: npcId                                                        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ Шаг 1: Генерация души

### Входные параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| locationLevel | int | Уровень локации (0-10) |
| speciesId | string | ID вида из SpeciesRegistry |
| roleId | NPCRole | Роль NPC |
| seed | long | Seed для детерминированной генерации |

### Определение уровня культивации

```
npcLevel = locationLevel + delta
delta = random(-2, +1)   // дельта уровня: -2..+1
npcLevel = max(0, npcLevel)

// Проверки по виду:
// Spirit — обычно без культивации (ядро = reservoir, не core)
// Construct — обычно L0-L2
// Creature — может быть L0-L6 (редко выше)
```

**Дельта уровней:**
> - -2 уровень: 18%
> - -1 уровень: 36%
> - ±0 уровень: 41%
> - +1 уровень: 5%
>
> **Ограничение капа локации:** Итоговый уровень NPC не может превышать
> `locationLevel + 0.9` (т.е. в локации L9 не может быть существ выше L9.9).
> Если delta даёт уровень выше капа — клампим до `locationLevel + 0.9`.

### Определение возраста

**Смертные (L0):**
```
age = random(MortalStage.Adult.min, MortalStage.Elder.max)
// Обычно 16-80 лет
```

**Практики (L1+):**
```
awakeningAge = random(16, 40)
cultivationYears = sum(yearsAtLevel[i])

// Упрощённая формула для генерации:
// Каждый уровень культивации ≈ 10-30 лет практики
cultivationYears = level × random(10, 30) + random(0, 20)
npcAge = awakeningAge + cultivationYears

// Старение замедляется: AgingMultipliers[level]
// L1-L2: ×1.0 (нормальное старение)
// L3: ×0.9
// L4: ×0.4 (резкое замедление)
// L5: ×0.3
// L6: ×0.1
// L7+: ×0.0 (старение остановлено)
```

**Специфика по видам:**
> Драконы и духи имеют другие возрастные диапазоны — см. SpeciesData.LifespanRange.

### Качество ядра

Взвешенный рандом:

| CoreQuality | Множитель ёмкости | Вес (Character) | Вес (Creature) |
|-------------|-------------------|-----------------|----------------|
| Fragmented | ×0.5 | 5% | 20% |
| Cracked | ×0.7 | 15% | 30% |
| Flawed | ×0.85 | 25% | 25% |
| Normal | ×1.0 | 35% | 20% |
| Refined | ×1.2 | 14% | 4% |
| Perfect | ×1.5 | 5% | 1% |
| Transcendent | ×2.0 | 1% | 0% |

> **Spirit/Construct** используют reservoir, не core. Качество ядра не применяется.
>
> **⚠️ Важно (решение ПРОТИВОРЕЧИЯ #1):** Множители CoreQuality ЕДИНЫ для игрока и NPC.
> Источник истины — QiBreakthroughCalculator (код) = {0.5, 0.7, 0.85, 1.0, **1.2**, **1.5**, **2.0**}.
> NPCConfig ДОЛЖЕН использовать те же значения. Все модули расчёта — универсальные.

### Тип пробуждения

| AwakeningType | Вес | Описание |
|---------------|-----|----------|
| Natural | 20% | Естественное пробуждение — более плавный рост |
| Guided | 50% | Под руководством наставника — стандартный путь |
| Artifact | 20% | Через артефакт — быстрый старт, без бонусов к проводимости |
| Forced | 10% | Принудительное — опасный путь, +1 черта (вкус/особенность) |

> **Примечание (решение ПРОТИВОРЕЧИЯ #2):** Тип пробуждения НЕ даёт бонусов/штрафов к проводимости.
> Влияние на проводимость — только через ядро (ёмкость) и развитие (перки, медитация).
> Это решение окончательное. NPC_ASSEMBLY_PIPELINE.md — приоритетный документ по данному вопросу.
> Об эффектах AwakeningType на другие параметры (статы, стартовое Ци) — будем думать в будущем.
>
> Источник: MORTAL_DEVELOPMENT.md §«Стартовые варианты» (v1.2, обновлено 2026-05-20)

### Структура данных: SoulData

```csharp
class SoulData
{
    CultivationLevel CultivationLevel;
    int SubLevel;
    int Age;
    MortalStage MortalStage;
    CoreQuality CoreQuality;
    AwakeningType AwakeningType;  // Только для flavour/черт, НЕ влияет на проводимость

    // Расчётные параметры Ци
    long CoreCapacity;
    float Conductivity;
    int QiDensity;

    // Множители
    float QualityMultiplier;
    float ConductivityGrowthMultiplier;  // Рост проводимости с возрастом (только увеличивается)
    int MaxLifespan;  // Макс. продолжительность жизни = f(фенотип, уровень практика)
}
```

---

## 2️⃣ Шаг 2: Выбор фенотипа

### Источник данных

`SpeciesRegistry.GetSpecies(speciesId)` → `SpeciesData`

Все виды уже зарегистрированы в SpeciesRegistry (реализовано):
- Character: human, elf, demon, giant
- Creature: wolf, tiger, dragon, phoenix, spider
- Spirit: ghost
- Construct: golem

### Выбор вида по роли

| NPCRole | Предпочтительные виды |
|---------|----------------------|
| Monster | wolf, tiger, spider (SoulType.Creature) |
| Guard | human (SoulType.Character) |
| Merchant | human, elf (SoulType.Character) |
| Cultivator | human, elf, demon (SoulType.Character) |
| Elder | human, elf (SoulType.Character) |
| Disciple | human, demon (SoulType.Character) |
| Enemy | human, demon (SoulType.Character) |
| Passerby | human (SoulType.Character) |

> **Позже:** Добавить weighted-выбор вида по роли + локацию.

### Данные фенотипа (из SpeciesData)

| Поле | Описание | Пример (human) |
|------|----------|----------------|
| SoulType | Природа сущности | Character |
| Morphology | Морфология тела | Humanoid |
| BodyMaterial | Материал тела | Organic |
| SizeClass | Класс размера | Medium |
| BaseStrength | Базовая сила | 10 |
| BaseAgility | Базовая ловкость | 10 |
| BaseVitality | Базовая живучесть | 10 |
| BaseIntelligence | Базовый интеллект | 10 |
| BaseAgeRange | Диапазон возраста | (16, 30) |
| LifespanRange | Диапазон жизни | (70, 100) |

---

## 3️⃣ Шаг 3: Сборка тела

### Генератор тел: BodyFactory (РЕАЛИЗОВАН)

`BodyFactory.CreateBody(morphology, size, vitality)` → `List<BodyPart>`

Фабрика уже существует и работает через BodyTemplateProvider.

### Порядок сборки тела

```
1. Получить BodyTemplate из BodyTemplateProvider по Morphology
2. Рассчитать множитель HP от Vitality:
   hpMultiplier = 1 + (VIT - 10) × 0.05
3. Рассчитать множитель HP от SizeClass:
   sizeMultiplier = SizeClassHPMultipliers[size]
4. Для каждой части из шаблона:
   effectiveRedHP = baseFunctionalHP × hpMultiplier × sizeMultiplier
   effectiveBlackHP = baseStructuralHP × hpMultiplier × sizeMultiplier
5. Создать BodyPart с расчётными HP
```

### Меридианы (духовная система)

> **Ключевой принцип:** Меридианы привязаны к **ДУШЕ**, а не к телу.
> Это духовные практики — каналы, по которым Ци протекает от ядра души
> через тело практика. Меридианы определяют **проводимость** — скорость
> прохождения Ци через духовную систему практика.

**Структура меридиан:**
- **Ядро души** (core) — корень системы меридиан, источник Ци
- **Основной ствол** — определяет базовую проводимость (coreCapacity / 360)
- **Ветви** — расходятся от ствола, пронизывают тело практика
- **Узлы вывода** — точки выхода Ци (для техник)

**Взаимосвязь душа–тело:**
- Меридианы как духовная структура **существуют независимо от тела**
- Тело выступает как «проводник» — физическая материя, через которую меридианы пропускают Ци
- Повреждение тела (ранение части) может **затруднить** прохождение Ци через соответствующую ветвь меридианы, но не разрушает саму меридиану
- Смерть тела → меридианы остаются при душе (для духов/реинкарнации)

> **Следствие:** Spirit (бесплотные) имеют меридианы, но без физического тела —
> Ци течёт напрямую, проводимость = coreCapacity / 360 (без возрастного роста, т.к. нет старения тела).
> Construct (искусственные тела) — меридианы «вплетены» в конструкт при создании,
> проводимость фиксированная, не растёт.

### Генератор тел: проверка наличия

✅ **BodyFactory** — реализован, находится в `Modules/Body/BodyFactory.cs`
✅ **BodyTemplateProvider** — реализован, все морфологии (10 шт.)
✅ **SpeciesRegistry** — реализован, 11 видов

**НЕ ХВАТАЕТ:**
- ❌ Интеграция BodyFactory в пайплайн NPC (NPCSpawnerService не использует BodyFactory)
- ❌ Система меридиан (только формулы проводимости, нет структуры)

---

## 4️⃣ Шаг 4: Расчёт Ци и проводимости

### Формула ёмкости ядра

```
totalSubLevels = (cultivationLevel - 1) × 10 + subLevel
coreCapacity = BASE_CORE_CAPACITY × CORE_CAPACITY_GROWTH^totalSubLevels × qualityMultiplier

// Где:
// BASE_CORE_CAPACITY = 1000
// CORE_CAPACITY_GROWTH = 1.1
// qualityMultiplier = из таблицы CoreQuality (§1 выше)
```

> **Источник истины:** QI_SYSTEM.md §«Ёмкость ядра», ALGORITHMS.md §3

### Формула проводимости

```
baseConductivity = coreCapacity / 360
finalConductivity = baseConductivity × conductivityGrowthMultiplier × (1 + perkBonuses + meditationBonuses + bodyTypeBonuses)
```

### Источники развития проводимости

Проводимость складывается из нескольких источников:

| Источник | Тип | Описание |
|----------|-----|----------|
| Ёмкость ядра (coreCapacity) | Базовый | Определяет `baseConductivity = coreCapacity / 360` |
| Рост с возрастом | Только увеличивается | Проводимость растёт со временем — меридианы тренируются естественным образом |
| Медитация на развитие меридиан | Бонусный | Целенаправленная тренировка проводимости |
| Тип телосложения | Бонусный | Некоторые виды имеют врождённую предрасположенность |
| Перки | Бонусный | Постоянные бонусы от врождённых/приобретённых перков |

> **Ключевой принцип:** Проводимость ТОЛЬКО УВЕЛИЧИВАЕТСЯ.
> Деградации меридиан с возрастом НЕТ.
>
> **Источник истины:** QI_SYSTEM.md §«Проводимость»

### Возраст → рост проводимости

Проводимость **только увеличивается** с возрастом — меридианы развиваются
и укрепляются со временем практики. Чем дольше практикуешь, тем лучше
проводимость. Нет деградации.

Уровень практика действует как **кратный коэффициент** на эффективный возраст:
практик более высокого уровня извлекает больше пользы из каждого года практики.

```
// Рост проводимости с возрастом (решение ПРОТИВОРЕЧИЯ #4 — РАСШИРЕННАЯ формула):
conductivityGrowthMultiplier = 1.0 + ageGrowthRate × effectiveAge

// Где effectiveAge учитывает уровень практика:
// Чем выше уровень — тем быстрее растёт проводимость

// effectiveAge = chronologicalAge × levelGrowthFactor(level)
// ageGrowthRate = 0.001
//
// levelGrowthFactor по уровням культивации:
// L0: 1.0, L1: 1.2, L2: 1.5, L3: 2.0, L4: 3.0, L5: 5.0, L6: 8.0, L7+: 12.0
//
// Пример: L5 практик в 100 лет:
//   effectiveAge = 100 × 5.0 = 500
//   growthMult = 1.0 + 0.001 × 500 = 1.5
//   Проводимость ×1.5 от базовой
```

**Исключения:**
- Spirit: нет физического тела → проводимость = coreCapacity / 360 (без возрастного роста)
- Construct: нет меридиан в органическом смысле → проводимость = базовая (без возрастного роста)

### Система максимальной продолжительности жизни

Максимальная продолжительность жизни зависит от:
1. **Фенотипа (вида)** — базовый диапазон (SpeciesData.LifespanRange)
2. **Уровня практика** — прорыв на уровень продлевает жизнь

**Ключевой принцип:** Практик, прорвавшийся на уровень раньше,
живёт дольше практика того же уровня, прорвавшегося позже.

```
// Пример:
// Практик А: прорвался на L5 в 20 лет → макс. жизнь ~400 лет
// Практик Б: прорвался на L5 в 40 лет → макс. жизнь ~350 лет
// Разница: ранний прорыв даёт бонус к продолжительности жизни

maxLifespan = baseLifespan(phenotype) + levelBonus(cultivationLevel) - lateStartPenalty(breakthroughAge)

// Где:
// baseLifespan(phenotype) — из SpeciesData.LifespanRange.max
// levelBonus(level) — бонус за каждый уровень культивации (TBD)
// lateStartPenalty(age) — штраф за поздний прорыв (TBD)

// Конкретные формулы — продумаем в процессе
```

> **TBD:** Формулы расчёта maxLifespan, levelBonus, lateStartPenalty.
> Будут уточнены после определения баланса продолжительности жизни по видам.

### Проводимость: правила прокачки

> **Важно:** Проводимость МОЖНО развивать через **перки** (постоянно)
> и **медитацию на развитие меридиан** (постоянный бонус после завершения).
> Временные баффы проводимости (ConductivityBoost) — **УДАЛЕНЫ**.
> Для увеличения поглощения Ци используйте формации (environmentMult).

| Источник | Тип | Величина |
|----------|-----|----------|
| Возраст (естественный рост) | Постоянный | Растёт только вверх, формула TBD |
| Медитация на развитие меридиан | Постоянный | +X% за цикл медитации (TBD) |
| Тип телосложения (вид) | Постоянный | Видовой бонус (TBD) |
| Врождённый перк «Золотое качество тела» | Постоянный | +30% |
| Приобретённый перк «Закалка меридиан» | Постоянный | +15% |
| Приобретённый перк «Небесные каналы» | Постоянный | +20% |
| Формация «Круг поглощения» | Локальный | +25% environmentMult |

> **Источник:** QI_SYSTEM.md §«Развитие проводимости»

### Плотность Ци

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

### Быстрый расчёт развития ядра для NPC

При генерации NPC нужно «отмотать» развитие ядра назад — рассчитать,
какой уровень Ци был бы у практика данного уровня и возраста.

```
// Расчёт для NPC:
// currentQi = coreCapacity (полное при генерации — решение ПРОТИВОРЕЧИЯ #5)
// NPC генерируются с полным Ци — как будто только достигли этого уровня

// Рандомизация Qi — балансировка Фазы 4 (НЕ используется при генерации)
```

---

## 5️⃣ Шаг 5: Экипировка / Усиление тела

### Два пути в зависимости от морфологии

```
┌─────────────────────────────────────────────────────────────────────────┐
│          ВЫБОР ПУТИ ЭКИПИРОВКИ/УСИЛЕНИЯ                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌───────────────────────┐    ┌────────────────────────────────┐      │
│  │   ГУМАНОИДНЫЕ РАСЫ    │    │   НЕГУМАНОИДНЫЕ / SPIRIT       │      │
│  │   (Morphology =       │    │   (Morphology ≠ Humanoid)      │      │
│  │    Humanoid, Hybrid)  │    │                                 │      │
│  │                        │    │                                 │      │
│  │  → Оружие (1 шт)      │    │  → Усиление частей тела:       │      │
│  │  → Броня (1-4 слота)  │    │    • NaturalArmor              │      │
│  │  → Зарядник (L3+)     │    │    • NaturalWeapon              │      │
│  │                        │    │    • BodyHardening              │      │
│  │  через ItemGenerator   │    │    • QiInfusion                │      │
│  │  Service               │    │    • SizeGrowth                 │      │
│  │                        │    │                                 │      │
│  └───────────────────────┘    └────────────────────────────────┘      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Гуманоидная экипировка

| Слот | Условие | Источник |
|------|---------|----------|
| Оружие (WeaponMain) | Все практики L1+ | ItemGeneratorService.GenerateWeaponForLevel() |
| Броня Torso | L2+ | ItemGeneratorService.GenerateArmorForLevel() |
| Броня Head | L3+ | ItemGeneratorService.GenerateArmorForLevel() |
| Броня Legs | L3+ | ItemGeneratorService.GenerateArmorForLevel() |
| Броня Feet | L4+ | ItemGeneratorService.GenerateArmorForLevel() |
| Зарядник Ци | L3+ | ItemGeneratorService (TODO: GenerateChargerForLevel) |

> **ItemGeneratorService** — реализован, но базовый (только Common grade).
> Нужна доработка: случайный grade, материалы, бонусные свойства.

### Система усиления частей тела (ПЛАН)

Для негуманоидных рас вместо экипировки:

| Тип усиления | Описание | Пример |
|--------------|----------|--------|
| NaturalArmor | Плотность кожи/чешуи | Волк: +20% reduction к Torso |
| NaturalWeapon | Врождённое оружие | Когти (MeleeStrike), Клыки, Хелицеры (Venom) |
| BodyHardening | Закалка части тела | Жёсткий хитин: +30 HP к Cephalothorax |
| QiInfusion | Ци-пропитка части | +50% урон атаки этой частью |
| SizeGrowth | Увеличение размера | Medium → Large (редко, для боссов) |

**Формулы:**

```
// NaturalArmor: снижает входящий урон на % для конкретной части тела
effectiveDamage = rawDamage × (1 - naturalArmorReduction)

// NaturalWeapon: добавляет урон к атаке частью тела
naturalWeaponDamage = baseDamage × sizeMultiplier × (1 + qiInfusionBonus)

// BodyHardening: добавляет плоский HP к части
enhancedMaxHP = baseMaxHP + hardeningBonus

// QiInfusion: множитель урона/защиты для Ци-пропитанной части
qiInfusedDamage = baseDamage × (1 + qiInfusionLevel × 0.25)
```

**Архитектура (план):**

```csharp
// Привязывается к BodyPart
class BodyEnhancement
{
    EnhancementType Type;     // NaturalArmor, NaturalWeapon, BodyHardening, QiInfusion, SizeGrowth
    float Value;              // Величина эффекта (% для armor, плоский для HP)
    BodyPartType TargetPart;  // К какой части применяется
    string Description;       // Описание для UI
}

// Сервис применения усилений
class BodyEnhancementSystem
{
    // Применить усиления к телу на основе вида и уровня
    List<BodyEnhancement> GenerateEnhancements(SpeciesData species, int level);
    
    // Рассчитать итоговые статы части тела с усилениями
    EnhancedBodyPartStats CalculateEnhancedStats(BodyPart part, List<BodyEnhancement> enhancements);
}
```

**Усиления по видам (примеры):**

| Вид | Уровень | Усиления |
|-----|---------|----------|
| Волк | L1-3 | NaturalWeapon(Когти, +5), NaturalArmor(Torso, +10%) |
| Тигр | L2-5 | NaturalWeapon(Клыки, +10), NaturalArmor(Torso, +20%) |
| Дракон | L5+ | NaturalWeapon(Когти, +20), NaturalArmor(All, +30%), QiInfusion(Torso, +50%) |
| Паук | L0-2 | NaturalWeapon(Хелицеры, +3, Venom), BodyHardening(Cephalothorax, +15) |
| Призрак | L1+ | QiInfusion(Core, +100%), QiInfusion(Essence, +50%) |
| Голем | L1-3 | BodyHardening(All, +50), NaturalArmor(All, +25%) |

---

## 6️⃣ Шаг 6: Генерация техник и формаций

### Количество техник по уровню

| Уровень NPC | Кол-во техник | Примечание |
|-------------|---------------|------------|
| L0 (смертный) | 0 | Смертные не используют техники |
| L1-L2 | 1 | Базовая техника |
| L3-L4 | 1-2 | + защитная техника (50%) |
| L5-L6 | 2-3 | + формация (10%) |
| L7-L8 | 3 | + формация (30%) |
| L9+ | 3+ | + формация (50%) |

### Типы техник по роли

| NPCRole | Предпочтительные типы | Примеры |
|---------|----------------------|---------|
| Monster | Combat, Poison | Врождённые атаки зверей |
| Guard | Combat, Defense | Защитные стойки, контратаки |
| Merchant | Support, Healing | Самоисцеление, ускорение |
| Cultivator | Combat, Support, Cultivation | Атакующие и поддерживающие |
| Elder | Все типы | Широкий набор |
| Enemy | Combat, Curse | Агрессивные техники |

### Grade техник

Взвешенный рандом (для генерации):

| Grade | Вес | Множитель урона |
|-------|-----|-----------------|
| Common | 60% | ×1.0 |
| Refined | 30% | ×1.3 |
| Perfect | 9% | ×1.6 |
| Transcendent | 1% | ×2.0 |

> Стоимость Ци всегда ×1.0 — не зависит от Grade!

### Генератор техник

> **Решение:** TechniqueGenerator — **отдельный независимый генератор**,
> НЕ часть ItemGeneratorService. Техники — это знания/навыки души, а не предметы.

**Существующий код:** `TechniqueGenerator.cs` — реализован в Legacy (заморожен).
Может быть использован как основа для нового TechniqueGeneratorService.

**Архитектура генерации техник (Матрёшка):**
1. **База** — тип техники → qiCost, capacity, baseDamage
2. **Grade** — множитель качества (Common/Refined/Perfect/Transcendent)
3. **Бонусы** — эффекты от Grade и стихии

**Источник данных:**
- docs/TECHNIQUE_SYSTEM.md — типы, Grade, формулы
- docs_old/technique-system-v2.md — подробная механика, дестабилизация, подавление уровнем
- docs/ALGORITHMS.md §3-4 — расчёт урона, плотность Ци

**План:** Создать TechniqueGeneratorService в Modules/Generator,
использующий формулы из TECHNIQUE_SYSTEM.md и ALGORITHMS.md §3-4.
Независим от ItemGeneratorService — техника ≠ предмет.

### Формации

Формации доступны только для NPC L5+ и редко:
- L5-L6: 10% шанс иметь формацию
- L7-L8: 30% шанс
- L9+: 50% шанс

> **Источник:** FORMATION_SYSTEM.md

---

## 7️⃣ Шаг 7: Заполнение инвентаря

### Система полного лута

> **Глобально:** У нас должна быть система полного лута при убийстве.
> Черновики были в старой и временной документации.

При смерти NPC весь его инвентарь и экипировка становятся доступными
для подбора игроком. Это включает:
1. Экипированные предметы (оружие, броня, зарядник)
2. Расходники из быстрых слотов
3. Хлам/материалы в рюкзаке
4. Духовные камни (если есть)

### Генерация содержимого инвентаря

| Категория | Кол-во | Условие | Источник |
|-----------|--------|---------|----------|
| Лечебные пилюли | 1-2 | L1+ | ConsumableGenerator |
| Ци-настойки | 0-1 | L3+ | ConsumableGenerator |
| Куски материалов | 0-2 | Все | ItemDatabaseService |
| Духовные камни (осколки) | 0-1 | L3+ (10%) | Прямая генерация |
| Духовные камни (фрагменты) | 0-1 | L5+ (5%) | Прямая генерация |

### «Хлам» в инвентаре

Хлам — это низкоуровневые расходники и материалы, которые NPC носит «на всякий случай»:
- Повреждённые пилюли (лечение 50% от нормы)
- Куски руды (Tier 1-2)
- Обрывки кожи/ткани
- Простые инструменты

---

## 8️⃣ Шаг 8: Финализация

### Сборка NPCState

Из всех промежуточных структур собирается финальный NPCState:

```csharp
// Псевдокод сборки
var state = new NPCState
{
    // Из Шага 1 (Душа)
    CultivationLevel = soul.CultivationLevel,
    SubLevel = soul.SubLevel,
    CoreQuality = soul.CoreQuality,
    MaxQi = soul.CoreCapacity,
    CurrentQi = soul.CoreCapacity,       // ПРОТИВОРЕЧИЕ #5: полное ядро при генерации
    Conductivity = soul.Conductivity,
    Age = soul.Age,
    AwakeningType = soul.AwakeningType,  // ПРОТИВОРЕЧИЕ #2: НЕ влияет на проводимость
    AwakeningAge = soul.AwakeningAge,     // Фаза 3 (3.4): возраст пробуждения
    MortalStage = soul.MortalStage,
    QiDensity = soul.QiDensity,
    MaxLifespan = soul.MaxLifespan,       // Фаза 1 (1.B): макс. продолжительность жизни

    // Из Шага 1 (Статы — Фаза 3, задача 3.G)
    Strength = soul.Strength,
    Agility = soul.Agility,
    Vitality = soul.Vitality,
    Intelligence = soul.Intelligence,

    // Из Шага 2 (Фенотип)
    SpeciesId = speciesId,                // Фаза 1 (1.3): идентификатор вида
    SoulType = species.SoulType,
    Morphology = species.Morphology,
    BodyMaterial = species.Material,
    DisplayName = generatedName,          // Фаза 2 (2.4): сгенерированное имя

    // Из Шага 3 (Тело) — BodyParts = List<BodyPart> (решение ПРОТИВОРЕЧИЯ #6)
    // Единая система через BodyParts для всех (игрок + NPC)
    // MaxHealth = sum(BodyParts.MaxRedHP)
    BodyParts = bodyParts,
    MaxHealth = CalculateTotalHealth(bodyParts),
    CurrentHealth = CalculateTotalHealth(bodyParts),

    // Из Шага 5 (Экипировка) — Фаза 1 (1.C/1.3)
    EquipmentIds = generatedEquipment,    // Dictionary<EquipmentSlot, string>

    // Из Шага 6 (Техники) — Фаза 1 (1.C/1.3)
    TechniqueIds = generatedTechniques,   // List<string>

    // Из Шага 7 (Инвентарь) — Фаза 1 (1.C/1.3)
    InventorySlots = generatedInventory,  // List<InventorySlot>

    // Прочие поля
    AggressionLevel = CalculateAggression(role, species), // Фаза 1 (1.3): 0..1
    CurrentLocation = locationId,                       // Фаза 3 (3.B): текущая локация
};
```

<!-- Обновлено: 2026-05-20 — Фаза 3: добавлены AwakeningAge, статы, уточнены типы -->

---

## 📊 Сводная таблица формул

| Параметр | Формула | Источник |
|----------|---------|----------|
| npcLevel | locationLevel + delta(-2..+1) | Новый |
| totalSubLevels | (level-1) × 10 + subLevel | QI_SYSTEM.md |
| coreCapacity | 1000 × 1.1^totalSubLevels × qualityMult | QI_SYSTEM.md |
| qiDensity | 2^(level-1) | ALGORITHMS.md §3.3 |
| baseConductivity | coreCapacity / 360 | QI_SYSTEM.md |
| finalConductivity | baseCond × growthMult × (1+perkBonuses+meditationBonuses+bodyTypeBonuses) | QI_SYSTEM.md + Новый |
| hpMultiplier | 1 + (VIT-10) × 0.05 | BODY_SYSTEM.md |
| effectiveHP | baseHP × hpMult × sizeMult | BODY_SYSTEM.md |
| MaxHealth | Σ(all BodyPart.MaxRedHP) | Новый |
| naturalWeaponDamage | baseDamage × sizeMult × (1+qiInfusion) | Новый |

---

## 🔗 Зависимости от существующих модулей

| Модуль | Статус | Что используется |
|--------|--------|------------------|
| Body (BodyFactory) | ✅ Реализован | Создание тела по морфологии |
| Body (BodyTemplateProvider) | ✅ Реализован | 10 шаблонов морфологий |
| Body (SpeciesRegistry) | ✅ Реализован | 11 видов |
| Qi (GameConstants) | ✅ Реализован | Формулы Ци, плотность, проводимость |
| Generator (ItemGeneratorService) | ✅ Реализован (базовый) | Генерация оружия, брони, расходников |
| NPC (NPCService) | ✅ Реализован | Регистрация NPC |
| NPC (NPCSpawnerService) | ✅ Реализован (упрощённый) | Спавн NPC |
| NPC (NPCState) | ✅ Реализован | Runtime-состояние NPC |

### Что нужно создать

| Компонент | Приоритет | Описание |
|-----------|-----------|----------|
| SoulData | HIGH | Структура данных души |
| SoulGenerator | HIGH | Генерация души (уровень, возраст, ядро) |
| NPCAssemblyService | HIGH | Оркестратор пайплайна сборки |
| BodyEnhancementSystem | MEDIUM | Система усиления частей тела |
| TechniqueGeneratorService | MEDIUM | Генерация техник для NPC |
| NPCSpawnerService (обновление) | HIGH | Интеграция с NPCAssemblyService |

---

## 📚 Связанные документы

### Основная документация

| Документ | Связь |
|----------|-------|
| [NPC.md](./NPC.md) | Общая система NPC, GeneratedNPC |
| [BODY_SYSTEM.md](./BODY_SYSTEM.md) | Система тела, Vitality, SizeClass |
| [QI_SYSTEM.md](./QI_SYSTEM.md) | Ци, ядро, проводимость, меридианы |
| [ALGORITHMS.md](./ALGORITHMS.md) | Формулы, мягкие капы, §23-25 |
| [ENTITY_TYPES.md](./ENTITY_TYPES.md) | Иерархия SoulType → Morphology → Species |
| [MORTAL_DEVELOPMENT.md](./MORTAL_DEVELOPMENT.md) | Развитие смертных, пробуждение |
| [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) | Техники, ёмкость, Grade |
| [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md) | Экипировка, Grade, прочность |
| [GENERATORS_SYSTEM.md](./GENERATORS_SYSTEM.md) | Генераторы предметов |
| [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md) | Боевой пайплайн |

### Временная документация

| Документ | Связь |
|----------|-------|
| [NPC_L6_ASSEMBLY_EXAMPLE.md](../docs_temp/NPC_L6_ASSEMBLY_EXAMPLE.md) | Пример сборки NPC L6 (Legacy) |
| [BREAKTHROUGH_MODELS_COMPARISON.md](../docs_temp/BREAKTHROUGH_MODELS_COMPARISON.md) | Модели прорыва |

### Старая документация

| Документ | Связь |
|----------|-------|
| [soul-system.md](../docs_old/soul-system.md) | Иерархия типов (старый источник) |
| [random_npc.md](../docs_old/random_npc.md) | Временные NPC (Phaser-эра, TypeScript) |

---

## 🔧 Открытые вопросы

1. **Проводимость по возрасту:** ~~Деградация удалена~~ — проводимость только растёт.
   Формулы роста и множителя уровня — **ОПРЕДЕЛЕНЫ** (решение ПРОТИВОРЕЧИЯ #4):
   `growthMultiplier = 1.0 + 0.001 × effectiveAge`, `effectiveAge = age × levelGrowthFactor(level)`.
   Используется РАСШИРЕННАЯ формула проводимости (не простая из QI_SYSTEM.md).

2. **Дельта уровней:** Утверждено — -2:18% / -1:36% / 0:41% / +1:5%.
   Ограничение: уровень NPC ≤ locationLevel + 0.9.

3. **Система меридиан:** ~~Нужна ли структура привязки меридиан к частям тела~~
   Решено: меридианы привязаны к **ДУШЕ**, не к телу. Тело — проводник.
   Повреждение тела может затруднить прохождение Ци через ветвь,
   но не разрушает меридиану. Конкретная механика ранение→проводимость — TBD.

4. **Генератор техник:** ~~Создавать отдельный TechniqueGeneratorService или расширить ItemGeneratorService?~~
   Решено: **отдельный независимый генератор**. Техника ≠ предмет.

5. **Полный лут:** Нужен документ, описывающий систему лута при убийстве NPC.

6. **Инвентарь NPC:** Как NPC хранит предметы? Через InventoryService?
   Или собственный словарь?

7. **Унифицированные модули (решение ПРОТИВОРЕЧИЯ #1):** Все этапы создания NPC и игрока
   должны использовать одни и те же модули расчёта с одними и теми же формулами.
   Данные для игрока имеют приоритет. CoreQuality множители = {0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0}.

8. **Единая система урона (решение ПРОТИВОРЕЧИЯ #3):** Все сущности (игрок + NPC)
   получают урон через единую систему BodyParts. NPCCombatAdapter НЕ вычитает HP напрямую.
   Весь урон → DamageService → BodyParts → пересчёт CurrentHealth.

9. **AwakeningType и проводимость (решение ПРОТИВОРЕЧИЯ #2):** Окончательно решено —
   AwakeningType НЕ даёт бонусов/штрафов к проводимости. Об эффектах на другие
   параметры — будем думать в будущем.

---

*Создано: 2026-05-20 07:05:00 UTC*
*Обновлено: 2026-05-20 — Разрешены 6 противоречий аудита (CoreQuality, AwakeningType, урон, проводимость, currentQi, BodyParts)*
