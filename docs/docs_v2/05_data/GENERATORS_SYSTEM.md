# Система генераторов

> **Статус:** Концепция (engine-agnostic).
> **Связанные документы:** `DATA_MODELS.md`, `CONFIGURATIONS.md`, `02_systems/TECHNIQUE_SYSTEM.md`, `06_player/EQUIPMENT_SYSTEM.md`, `04_entities/NPC_ASSEMBLY_PIPELINE.md`.

---

## 1. Обзор

Система генераторов обеспечивает **процедурное создание** игровых объектов: техник, экипировки, расходников, формаций, камней Ци, NPC.

**Ключевые принципы:**
- **Архитектура «Матрёшка»**: Base × Grade × Specialization — три слоя генерации, накладываемые друг на друга.
- **Детерминированность**: `SeededRandom` — один и тот же seed всегда даёт одинаковый результат.
- **Grade НЕ зависит от уровня**: даже на L1 есть 2% шанс transcendent.
- **Грамматическое согласование**: генератор имён учитывает род русских существительных.

> Реализация — pure C#, без движко-специфичных зависимостей. `SeededRandom` — обёртка над стандартным `System.Random` с явным seed. Генераторы — обычные классы, тестируемые через `dotnet test`.

---

## 2. Архитектура генераторов

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ГЕНЕРАТОРЫ                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    ОСНОВНЫЕ ГЕНЕРАТОРЫ                               │   │
│   │                                                                      │   │
│   │   TechniqueGenerator                                                 │   │
│   │   ├── Генерация техник                                              │   │
│   │   └── Архитектура «Матрёшка»                                        │   │
│   │                                                                      │   │
│   │   EquipmentGenerator                                                 │   │
│   │   ├── Оружие                                                        │   │
│   │   ├── Броня                                                         │   │
│   │   ├── Аксессуары                                                    │   │
│   │   └── Зарядники Ци                                                  │   │
│   │                                                                      │   │
│   │   NPCGenerator                                                       │   │
│   │   ├── Базовые NPC                                                   │   │
│   │   └── Полная сборка с экипировкой                                   │   │
│   │                                                                      │   │
│   │   ConsumableGenerator                                                │   │
│   │   └── Таблетки, эликсиры, еда, свитки                              │   │
│   │                                                                      │   │
│   │   FormationGenerator                                                 │   │
│   │   ├── Боевые формации                                               │   │
│   │   └── Ядра формаций (диски, алтари)                                 │   │
│   │                                                                      │   │
│   │   QiStoneGenerator                                                   │   │
│   │   └── Камни Ци (calm / chaotic, по размеру)                         │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    ПОДДЕРЖИВАЮЩИЕ МОДУЛИ                            │   │
│   │                                                                      │   │
│   │   ├── SeededRandom — детерминированный RNG                          │   │
│   │   ├── GradeSelector — выбор Grade (НЕ зависит от уровня)            │   │
│   │   ├── NameGenerator — имена предметов с грамматическим согласованием│   │
│   │   ├── MaterialRegistry — реестр материалов (~50, 5 тиров)           │   │
│   │   └── NamingDatabase — база данных существительных + прилагательных │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. SeededRandom — детерминированная генерация

### 3.1 Принцип

```csharp
var rng = new SeededRandom(seed: 12345);

var item1 = GenerateItem(rng);   // всегда одинаковый для seed=12345
var item2 = GenerateItem(rng);   // всегда одинаковый для seed=12345
```

`SeededRandom` — обёртка над `System.Random` (или эквивалент), которая:
- инициализируется явно заданным `seed`;
- каждый вызов `Next()` / `NextDouble()` детерминирован;
- одинаковый seed + одинаковая последовательность вызовов = одинаковый результат.

### 3.2 Применение

- **Воспроизводимость багов:** если игрок сообщает о «странном предмете», разработчик может воспроизвести генерацию с тем же seed.
- **Мультиплеер (будущее):** все клиенты генерируют один и тот же мир по одному seed.
- **Тестирование:** тесты фиксируют seed и проверяют конкретные результаты.
- **Сохранения:** для процедурных локаций сохраняется только seed, не сами объекты (см. `WORLD_SAVE_SYSTEM.md`).

### 3.3 Параметры генерации

| Параметр | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `seed` | int | `Date.now()` | Seed генерации |
| `level` | int | — | Уровень (1–9) |
| `grade` | Grade? | null (случайный) | Фиксированный Grade |
| `count` | int | 1 / 10 | Количество |
| `element` | Element? | null (случайный) | Фиксированная стихия |
| `subtype` | string? | null | Фиксированный подтип |

---

## 4. Архитектура «Матрёшка»

> **Три слоя генерации:** Base × Grade × Specialization. Каждый слой накладывается на предыдущий, формируя финальный объект.

### 4.1 Слои

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                 АРХИТЕКТУРА «МАТРЁШКА»                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  СЛОЙ 1: БАЗА (Base)                                                         │
│  ├── Базовые параметры от типа и уровня                                     │
│  ├── qiCost = 10 × 1.5^(level-1)              (для техник)                  │
│  ├── capacity = baseCapacity × 2^(level-1)    (для техник)                  │
│  ├── baseDamage = qiCost (для справки, НЕ для урона!)                       │
│  └── Для экипировки: baseStats(level, rng) → базовые статы                  │
│                                                                              │
│  СЛОЙ 2: GRADE (НЕ зависит от уровня!)                                       │
│  ├── common:      ×1.0 урона/параметров, qiCost ×1.0                        │
│  ├── refined:     ×1.2 урона/параметров, qiCost ×1.0                        │
│  ├── perfect:     ×1.4 урона/параметров, qiCost ×1.0                        │
│  └── transcendent: ×1.6 урона/параметров, qiCost ×1.0                       │
│                                                                              │
│  СЛОЙ 3: СПЕЦИАЛИЗАЦИЯ (Specialization / Бонусы)                             │
│  ├── Материал (для экипировки): material × materialProperties               │
│  ├── Стихийные эффекты (для техник): element + type                         │
│  ├── Сила эффекта от Grade (0% ~ 150%)                                      │
│  ├── isUltimate (5% шанс для transcendent)                                  │
│  ├── Transcendent-эффект (только для transcendent)                          │
│  └── Грамматическое согласование в имени                                    │
│                                                                              │
│  ИТОГ: finalStats = base × gradeMult × specialization                       │
│  ИТОГ (техники): finalDamage = capacity × gradeMult                         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Принципы «Матрёшки»

1. **База определяет «уровень мощности»** (зависит от level и типа).
2. **Grade определяет «качество»** (множитель ×1.0–×1.6, НЕ зависит от level).
3. **Специализация определяет «уникальность»** (материал, стихия, эффекты, имя).

**Ключевая идея:** quality (grade) ортогональна power (level). Транцендентный меч L1 — это редкий, но слабый артефакт. Обычный меч L9 — это массовый, но мощный.

---

## 5. Система Grade

### 5.1 Множители Grade

| Grade | Урон (множитель) | qiCost (множитель) | Шанс выпадения |
|-------|------------------|---------------------|----------------|
| common | ×1.0 | ×1.0 | ~80% |
| refined | ×1.2 | ×1.0 | ~13% |
| perfect | ×1.4 | ×1.0 | ~5% |
| transcendent | ×1.6 | ×1.0 | ~2% |

### 5.2 Grade НЕ зависит от уровня

> **КРИТИЧЕСКИЙ ПРИНЦИП:** распределение грейдов одинаково для L1 и L9.

- L1 transcendent меч существует (с шансом 2%).
- L9 common меч существует (с шансом 80%).
- Уровень влияет на абсолютные значения (baseCapacity, durability), но не на распределение грейдов.

Это означает, что даже новичок может найти легендарный предмет (но слабый по абсолютным параметрам), а мастер может пользоваться обычным (но мощным по абсолютным параметрам).

### 5.3 GradeSelector

```csharp
public class GradeSelector
{
    private static readonly (Grade grade, double chance)[] Distribution =
    {
        (Grade.Common,       0.80),
        (Grade.Refined,      0.13),
        (Grade.Perfect,      0.05),
        (Grade.Transcendent, 0.02),
    };

    public Grade Select(SeededRandom rng)
    {
        var roll = rng.NextDouble();
        var cumulative = 0.0;
        foreach (var (grade, chance) in Distribution)
        {
            cumulative += chance;
            if (roll < cumulative) return grade;
        }
        return Grade.Common;
    }
}
```

---

## 6. Генератор техник

### 6.1 Параметры

| Параметр | Описание |
|----------|----------|
| type | Тип техники (combat, cultivation, support, ...) |
| level | Уровень (1–9) |
| grade | Фиксированный Grade (или случайный) |
| element | Стихия (или случайная) |
| subtype | Подтип (melee_strike, ranged_projectile, ...) |
| count | Количество |
| seed | Seed генерации |

### 6.2 Формула урона (КРИТИЧЕСКАЯ)

> **Урон = Ёмкость × Grade**

```
finalDamage = capacity × gradeMult

где:
  capacity = baseCapacity(type) × 2^(level-1) × masteryBonus
  gradeMult = множитель Grade (×1.0 ~ ×1.6)
```

### 6.3 Пример расчёта

**melee_strike L5, Grade Perfect, mastery 0%:**

```
baseCapacity = 64 (для melee_strike)
levelMultiplier = 2^(5-1) = 16
masteryBonus = 1.0 (0% mastery)

capacity = 64 × 16 × 1.0 = 1024
gradeMult = 1.4 (Perfect)
finalDamage = 1024 × 1.4 = 1433 урона
```

### 6.4 Дестабилизация

```
При переполнении (qiInput > capacity):
  - Излишки Ци рассеиваются
  - Урон практику = excessQi × 0.5
  - Урон по цели (только melee!) = inputQi × 0.5
  - Для ranged_*: урона по цели НЕТ
```

### 6.5 Ultimate-техники

```csharp
// 5% шанс для transcendent техник
const double ULTIMATE_CHANCE = 0.05;

// Множители
const double ULTIMATE_DAMAGE_MULTIPLIER = 1.3;
const double ULTIMATE_QI_COST_MULTIPLIER = 1.5;

// Маркер в названии
if (isUltimate)
    name = $"⚡ {name}";
```

### 6.6 Типы техник по Tier

| Tier | Типы | Особенности |
|------|------|-------------|
| 1 | combat | Только множители урона, эффекты от стихий |
| 2 | defense, healing | Событийные эффекты (shield, heal) |
| 3 | curse, poison | DoT и дебаффы |
| 4 | support, movement, sensory | Баффы и утилити |
| 5 | cultivation | Специальные эффекты |

### 6.7 Стихийные эффекты

| Стихия | Эффект | Длительность |
|--------|--------|--------------|
| 🔥 Огонь | Горение 5% урона/тик | 3 тика |
| 💧 Вода | Замедление −20% скорости | 2 тика |
| 🪨 Земля | Стан 15% шанс | 1 тик |
| 💨 Воздух | Отброс 3 клетки | — |
| ⚡ Молния | Цепной урон 50% по 2 целям | — |
| 🌑 Пустота | +30% пробития брони | — |

---

## 7. Генератор экипировки

### 7.1 Параметры

| Параметр | Описание |
|----------|----------|
| type | Тип (weapon, armor, charger, accessory, artifact) |
| level | Уровень (1–9) |
| grade | Grade (или случайный) |
| materialId | ID материала (или случайный) |
| count | Количество |

### 7.2 Поток генерации

```
1. selectMaterial(options, rng)         → выбор материала из MaterialRegistry
2. selectGrade(options, rng)            → выбор Grade (НЕ зависит от уровня!)
3. getBaseStats(level, rng)             → генерация базовых статов
4. applyMaterialToStats(base, material) → применение материала
5. applyGradeToStats(stats, grade)      → применение грейда
6. createDurabilityState(material, grade, level)  → расчёт прочности
7. generateBonuses(grade, level, type, rng)       → генерация бонусов
8. generateName(material, grade, level, rng, subtype)  → генерация имени
9. generateRequirements(level, stats)              → требования
```

### 7.3 Архитектура «Матрёшка» для экипировки

```
Base → Material → Grade → Final
EffectiveStats = Base × MaterialProperties × GradeMultipliers
```

### 7.4 Поддерживаемые типы

| Тип | Описание |
|-----|----------|
| weapon | Оружие всех видов (sword, axe, spear, dagger, staff, ...) |
| armor | Броня для всех слотов (head, torso, legs, feet, hands) |
| charger | Зарядники Ци |
| accessory | Аксессуары (amulet, ring) |
| artifact | Артефакты |

---

## 8. Генератор расходников

### 8.1 Типы расходников

| Тип | Название | Стек | Эффекты |
|-----|----------|------|---------|
| pill | Таблетки | 20 | heal_hp, heal_stamina, buff_stat, buff_resistance |
| elixir | Эликсиры | 10 | buff_stat, buff_resistance, cure, special |
| food | Еда | 50 | heal_hp, heal_stamina |
| scroll | Свитки | 5 | special, cure, buff_stat |

> **ВАЖНО:** Расходники НЕ добавляют Ци напрямую — это задача зарядников.

### 8.2 Типы эффектов

| Тип эффекта | Описание | Длительность |
|-------------|----------|--------------|
| heal_hp | Восстановление HP | — |
| heal_stamina | Восстановление сил | — |
| buff_stat | Усиление характеристики | 60 сек × grade |
| buff_resistance | Усиление сопротивления | 120 сек × grade |
| cure | Лечение статуса | — |
| special | Особый эффект | 30 сек × grade |

### 8.3 Система Grade для расходников

```csharp
static readonly Dictionary<Grade, (double effect, double duration)> GradeConfigs =
{
    { Grade.Common,       (effectMultiplier: 1.0, durationMultiplier: 1.0) },
    { Grade.Refined,      (effectMultiplier: 1.2, durationMultiplier: 1.2) },
    { Grade.Perfect,      (effectMultiplier: 1.5, durationMultiplier: 1.5) },
    { Grade.Transcendent, (effectMultiplier: 2.0, durationMultiplier: 2.0) },
};
```

### 8.4 Базовые значения по уровню (heal_hp)

```
L1: 10  →  L5: 80  →  L9: 300
```

---

## 9. Генератор формаций

### 9.1 Боевые формации

| Тип | Описание |
|-----|----------|
| defensive | Защитные формации |
| offensive | Атакующие |
| support | Поддержка |
| special | Особые |

### 9.2 Медитативные формации (ядра)

| Тип | Уровень | Варианты |
|-----|---------|----------|
| Диски (disk) | L1–L6 | stone, jade, iron, spirit_iron |
| Алтари (altar) | L5–L9 | jade, crystal, spirit_crystal, dragon_bone |

### 9.3 Структура ядра

```csharp
class FormationCore
{
    string coreId;
    CoreType coreType;             // disk, altar
    string variant;                // stone, jade, iron, ...
    int levelMin;
    int levelMax;
    int maxSlots;                  // слоты для камней Ци
    int baseConductivity;          // ед/сек
    int maxCapacity;
    bool isImbued;
    string imbuedTechniqueId;
}
```

---

## 10. Генератор камней Ци

### 10.1 Принципы

> Камни Ци характеризуются **только** объёмом Ци и типом Ци (calm/chaotic). «Качество» камней НЕ предусмотрено лором.

### 10.2 Типы

| Тип | Описание | Опасность |
|-----|----------|-----------|
| calm (Спокойная) | Стандартный кристалл, безопасен | 0 / 10 |
| chaotic (Хаотичная) | Неупорядоченная Ци, опасна | 7 / 10 |

### 10.3 Классификация по размеру

| Размер | Объём (см³) | Ци (ед) |
|--------|-------------|---------|
| dust (Пыль) | < 0.1 | 0–102 |
| fragment (Осколок) | 0.1–1 | 102–1024 |
| small (Малый) | 1–8 | 1024–8192 |
| medium (Средний) | 8–27 | 8192–27648 |
| large (Большой) | 27–64 | 27648–65536 |
| huge (Огромный) | 64–125 | 65536–128000 |
| boulder (Глыба) | > 125 | 128000+ |

### 10.4 Физика

- Плотность кристалла: **1024 ед/см³** (постоянная).
- Содержание Ци: `1024 × объём_см³`.
- Не имеет стихийного окраса.

---

## 11. Генератор NPC

### 11.1 Параметры

| Параметр | Описание |
|----------|----------|
| species | Вид (human, elf, wolf, dragon, ...) |
| level | Уровень культивации (1–9) |
| role | Роль (monster, guard, passerby, elder, ...) |
| count | Количество |

### 11.2 Формулы культивации (лор)

```csharp
// Плотность Ци = 2^(level - 1)
qiDensity = Math.Pow(2, cultivationLevel - 1);

// Объём ядра
coreVolume = baseVolume * qiDensity;

// Качество ядра
coreQuality = Math.Floor(meridianConductivity * 10) / 10;
```

### 11.3 Пайплайн сборки NPC

```
NPCGenerator (оркестратор)
├── 1. SoulGenerator           → генерация души (SoulType, Morphology, Species)
├── 2. BodyFactory              → генерация тела (BodyPart[])
├── 3. TechniqueGenerator       → техники (V2 «Матрёшка») × slots
├── 4. FormationGenerator       → формации × formationSlots
├── 5. EquipmentGenerator       → экипировка (V2)
├── 6. ConsumableGenerator      → расходники в инвентаре
├── 7. NPCNameGenerator         → имя + титул
└── 8. PersonalityGenerator     → PersonalityTrait [Flags]
```

Подробнее — в `04_entities/NPC_ASSEMBLY_PIPELINE.md`.

---

## 12. Грамматическое согласование имён

### 12.1 Проблема

Старые генераторы не учитывали грамматический род русских существительных:

```
❌ «Улучшенный секира»   (мужской + женский)
✅ «Улучшенная секира»   (женский + женский)

❌ «Лёгкий мантия»      (мужской + женский)
✅ «Лёгкая мантия»      (женский + женский)

❌ «Огненный копьё»     (мужской + средний)
✅ «Огненное копьё»     (средний + средний)
```

### 12.2 Решение: NamingDatabase

**GrammaticalGender** (enum):

```csharp
public enum GrammaticalGender
{
    Masculine,   // Мужской (меч, топор, посох)
    Feminine,    // Женский (секира, катана, мантия)
    Neuter,      // Средний (копьё, кольцо, ожерелье)
    Plural       // Множественное (перчатки, сапоги)
}
```

**NounWithGender** (struct):

```csharp
public readonly struct NounWithGender
{
    public readonly string Noun;
    public readonly GrammaticalGender Gender;

    public NounWithGender(string noun, GrammaticalGender gender)
    {
        Noun = noun;
        Gender = gender;
    }
}
```

**AdjectiveForms** (struct):

```csharp
public readonly struct AdjectiveForms
{
    public readonly string Masculine;   // Пылающий
    public readonly string Feminine;    // Пылающая
    public readonly string Neuter;      // Пылающее
    public readonly string Plural;      // Пылающие

    public string GetForm(GrammaticalGender gender)
    {
        return gender switch
        {
            GrammaticalGender.Masculine => Masculine,
            GrammaticalGender.Feminine  => Feminine,
            GrammaticalGender.Neuter    => Neuter,
            GrammaticalGender.Plural    => Plural,
            _ => Masculine,
        };
    }
}
```

### 12.3 Примеры данных

**Оружие (с указанием рода):**

```csharp
{
    WeaponSubtype.Sword, new[] {
        new NounWithGender("меч",    GrammaticalGender.Masculine),
        new NounWithGender("клинок", GrammaticalGender.Masculine),
        new NounWithGender("катана", GrammaticalGender.Feminine),
    }
},
{
    WeaponSubtype.Spear, new[] {
        new NounWithGender("копьё",    GrammaticalGender.Neuter),
        new NounWithGender("алебарда", GrammaticalGender.Feminine),
        new NounWithGender("глефа",    GrammaticalGender.Feminine),
    }
},
```

**Прилагательные (Grade):**

```csharp
{
    Grade.Refined, new AdjectiveForms {
        Masculine = "Улучшенный",
        Feminine  = "Улучшенная",
        Neuter    = "Улучшенное",
        Plural    = "Улучшенные",
    }
},
{
    Grade.Transcendent, new AdjectiveForms {
        Masculine = "Трансцендентный",
        Feminine  = "Трансцендентная",
        Neuter    = "Трансцендентное",
        Plural    = "Трансцендентные",
    }
},
```

**Прилагательные (Element):**

```csharp
{
    Element.Fire, new AdjectiveForms {
        Masculine = "Огненный",
        Feminine  = "Огненная",
        Neuter    = "Огненное",
        Plural    = "Огненные",
    }
},
```

### 12.4 Примеры до / после

| Было | Стало |
|------|-------|
| Улучшенный катана | Улучшенная катана |
| Совершенный секира | Совершенная секира |
| Трансцендентный копьё | Трансцендентное копьё |
| Лёгкий мантия | Лёгкая мантия |
| Тяжёлый кираса | Тяжёлая кираса |
| Улучшенный перчатки | Улучшенные перчатки |
| Огненный защита | Огненная защита |
| Громовой стена | Громовая стена |
| Водяной исцеление | Водяное исцеление |

---

## 13. Статистика генерации

### 13.1 Лимиты генерации

| Тип | Лимит тест | Лимит prod | Формула |
|-----|------------|------------|---------|
| Техники (combat) | 125 | 405 | 5 подтипов × 5 уровней |
| Техники (другие) | 50–100 | 150–225 | по типу |
| Формации | ~500 | ~2000 | по типу |
| NPC | 100 | 500 | по species |

### 13.2 Производительность генерации

| Операция | Время (типично) |
|----------|-----------------|
| Генерация 1 техники | < 1 ms |
| Генерация 1 предмета | < 1 ms |
| Генерация 1 NPC (полный) | ~5–10 ms |
| Генерация здания (среднее) | ~150 ms |
| Генерация локации (1×1 км) | ~100–500 ms |

---

## 14. Хранение пресетов

### 14.1 Концепция Base + Modifiers

Вместо хранения полных объектов:

```csharp
// Базовый объект (фиксированные характеристики)
interface BaseTechnique {
    id: string;
    name: string;
    type: TechniqueType;
    element: Element;
    level: number;
    baseDamage: number;
    baseQiCost: number;
}

// Модификаторы (флаги + значения)
interface TechniqueModifiers {
    effects: { burning?, freezing?, stun?, ... };
    effectValues: { burningDamage?, stunDuration?, ... };
    penalties: { qiCostMultiplier? };
    bonuses: { damageMultiplier? };
}
```

### 14.2 Размер хранения

| Подход | 1 техника | 2046 техник |
|--------|-----------|-------------|
| Полный JSON | ~800 байт | ~1.6 MB |
| Base + Modifiers | ~150 байт | ~300 KB |
| **Экономия** | | **81%** |

### 14.3 Структура файлов пресетов

```
presets/
├── techniques/
│   ├── combat/
│   │   ├── melee-strike/level-*.json
│   │   ├── melee-weapon/level-*.json
│   │   └── ranged/level-*.json
│   ├── defense/level-*.json
│   ├── cultivation/level-*.json
│   └── ... (все типы)
├── items/
│   ├── weapons.json
│   ├── armor.json
│   ├── accessories.json
│   ├── consumables.json
│   └── chargers.json
├── npcs/
│   ├── human.json
│   └── ... (~50 species)
├── formations/
│   ├── defensive.json
│   ├── offensive.json
│   ├── support.json
│   └── special.json
├── materials.json
├── qi_stones.json
├── counters.json
└── manifest.json
```

---

## 15. Принципы системы генераторов

1. **«Матрёшка» (Base × Grade × Specialization).** Три слоя, ортогональные друг другу.
2. **SeededRandom** — детерминированная генерация. Один seed = одинаковый результат.
3. **Grade НЕ зависит от уровня.** 2% transcendent даже на L1.
4. **Грамматическое согласование** в NameGenerator. Учитывает род русских существительных.
5. **Base + Modifiers** для хранения пресетов — 81% экономии по размеру.
6. **NPC-генерация = оркестрация** нескольких генераторов (technique + equipment + consumable + ...).
7. **Камни Ци** — без качества, только объём + тип (calm/chaotic).

---

## 16. Связанные документы

| Документ | Связь |
|----------|-------|
| `02_systems/TECHNIQUE_SYSTEM.md` | Система техник, грейды техник |
| `06_player/EQUIPMENT_SYSTEM.md` | Экипировка, материалы, грейды экипировки |
| `06_player/INVENTORY_SYSTEM.md` | Инвентарь, расходники |
| `04_entities/NPC_ASSEMBLY_PIPELINE.md` | Сборка NPC |
| `04_entities/ENTITY_TYPES.md` | SoulType, Morphology, Species |
| `DATA_MODELS.md` | Структуры данных для генерируемых объектов |
| `CONFIGURATIONS.md` | Пресеты материалов, грейдов, стихий |
| `08_content/NAME_GENERATOR.md` | Полная теория генерации имён |
