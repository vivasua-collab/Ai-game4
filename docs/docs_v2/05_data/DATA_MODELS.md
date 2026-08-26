# Модели данных

> **Статус:** Концепция (engine-agnostic).
> **Связанные документы:** `CONFIGURATIONS.md`, `SAVE_SYSTEM.md`, `WORLD_SAVE_SYSTEM.md`, `02_systems/QI_SYSTEM.md`, `04_entities/ENTITY_TYPES.md`.

---

## 1. Обзор

Документ описывает структуры данных проекта: какие сущности существуют, какие поля у них есть, какие типы используются. Все структуры — **pure C# типы** (`readonly struct` / `class`), без движко-специфичных зависимостей.

**Хранение:**
- **Статические пресеты** (техники, предметы, NPC-шаблоны, материалы) — в **data resources** (текстовые файлы ресурсов данных; конкретный формат — JSON или текстовые ресурсные файлы движка, выбор зависит от реализации, не от концепции).
- **Динамические данные** (игрок, NPC сессии, инвентарь, состояние мира) — в памяти + JSON-сериализуемые классы для сохранений.

> Структуры данных — это **нейтральные классы/структуры**, сериализуемые в JSON (или другой текстовый формат). НЕ используются движко-специфичные ресурсные типы; концепция инвариантна относительно движка.

---

## 2. Принципы типов данных

### 2.1 Qi — `long`

Все значения Ци (qiCost, currentQi, coreCapacity, accumulatedQi) используют **`long`** (64-битное целое).

- Ци культиватора L9 может достигать ~524M (effectiveQi).
- `long` обеспечивает точность без потерь на float.
- Никаких `float` для Ци.

### 2.2 Статы — `float`

Характеристики (STR, AGI, INT, VIT, conductivity) используют **`float`**.

- Точность 32-битного float достаточна для статов (диапазон 1–100+).
- Float допускает дробные бонусы от экипировки и техник.

### 2.3 readonly struct contracts

Сообщения между системами (events, commands) — `readonly struct`. Это обеспечивает:
- **Zero GC**: нет аллокаций в hot paths.
- **Семантика значения**: копирование по значению, не по ссылке.
- **Потокобезопасность**: иммутабельные данные.

Подробнее о стратегии производительности — в `01_architecture/PERFORMANCE_STRATEGY.md`.

### 2.4 Сводная таблица типов

| Данные | Тип | Обоснование |
|--------|-----|-------------|
| Qi (Ци) | `long` | Точность, диапазон до ~524M |
| Статы (STR/AGI/INT/VIT) | `float` | Дробные бонусы |
| Здоровье (%) | `float` | 0–100 |
| Усталость (%) | `float` | 0–100 |
| Проводимость | `float` | 0.1–10.0 |
| Координаты тайлов | `int` | Целочисленные |
| Время (тики) | `int` / `long` | Целочисленное |
| Идентификаторы | `string` | Удобство сериализации |
| События/сообщения | `readonly struct` | Zero-GC |
| Списки в hot paths | `Span<T>` / pool | Без аллокаций |

---

## 3. Служебные модели (struct contracts)

### 3.1 StatBonus

```csharp
public readonly struct StatBonus
{
    public readonly StatType Stat;     // STR, AGI, INT, VIT, ...
    public readonly float Value;       // +5, -2, +10.5

    public StatBonus(StatType stat, float value)
    {
        Stat = stat;
        Value = value;
    }
}
```

Бонус характеристики. Используется в экипировке, техниках, баффах.

### 3.2 Position2D

```csharp
public readonly struct Position2D
{
    public readonly int X;   // в тайлах (или в промилле для UI)
    public readonly int Y;

    public Position2D(int x, int y) { X = x; Y = y; }
}
```

Позиция в мире. Для UI используется промилле (integer math, без float для пикселей).

### 3.3 InputFrameData

```csharp
public readonly struct InputFrameData
{
    public readonly bool MoveUp;
    public readonly bool MoveDown;
    public readonly bool MoveLeft;
    public readonly bool MoveRight;
    public readonly bool Interact;
    public readonly bool Attack;
    public readonly int MouseTileX;
    public readonly int MouseTileY;
    // ...
}
```

Кадр ввода: состояние клавиш + позиция мыши в тайлах. Zero-allocation.

### 3.4 InventorySlot

```csharp
public readonly struct InventorySlot
{
    public readonly string ItemId;   // ссылка на предмет
    public readonly int Count;       // количество в стаке
}
```

Слот инвентаря. Сами предметы — отдельная сущность.

### 3.5 LootEntry

```csharp
public readonly struct LootEntry
{
    public readonly string ItemId;
    public readonly float Chance;    // 0.0–1.0
    public readonly int MinCount;
    public readonly int MaxCount;
}
```

Запись лута: предмет с шансом и диапазоном количества.

---

## 4. Основные сущности

### 4.1 GameSession — Игровая сессия

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| worldId | string | ID мира |
| worldName | string | Название мира |
| startVariant | int | 1=секта, 2=случайный, 3=кастомный |
| worldYear | int | Год по Э.С.М. |
| worldMonth | int | Месяц (1–12) |
| worldDay | int | День (1–30) |
| worldHour | int | Час (0–23) |
| worldMinute | int | Минута (0–59) |
| daysSinceStart | int | Дней от попадания |
| isPaused | bool | Пауза симуляции |
| worldState | JSON | Текущее состояние мира |

### 4.2 Character — Персонаж игрока

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Имя персонажа |
| **Характеристики** |||
| strength | float | Сила |
| agility | float | Ловкость |
| intelligence | float | Интеллект |
| vitality | float | Выносливость |
| conductivity | float | Проводимость меридиан |
| **Культивация** |||
| cultivationLevel | int | Основной уровень (1–9) |
| cultivationSubLevel | int | Под-уровень (0–9) |
| coreCapacity | **long** | Ёмкость ядра |
| coreQuality | float | Качество ядра |
| currentQi | **long** | Текущее Ци |
| accumulatedQi | **long** | Накопленное для прорыва |
| **Физиология** |||
| health | float | Здоровье (%) |
| fatigue | float | Физическая усталость (%) |
| mentalFatigue | float | Ментальная усталость (%) |
| age | int | Возраст (лет) |
| bodyHeight | int | Рост (см) |
| **Память** |||
| hasAmnesia | bool | Амнезия |
| knowsAboutSystem | bool | Знает о системе |
| **Ресурсы** |||
| contributionPoints | int | Очки вклада |
| spiritStones | int | Духовные камни |
| **Система тела (JSON)** |||
| bodyState | JSON | Kenshi-style повреждения |
| statsDevelopment | JSON | Развитие характеристик |

### 4.3 NPCState — Неигровой персонаж

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| isPreset | bool | Предустановленный NPC |
| presetId | string | ID пресета |
| name | string | Имя |
| title | string | Титул |
| age | int | Возраст |
| backstory | string | Предыстория |
| **Культивация** |||
| cultivationLevel | int | Уровень культивации |
| cultivationSubLevel | int | Под-уровень |
| coreCapacity | **long** | Ёмкость ядра |
| currentQi | **long** | Текущее Ци |
| **Характеристики** |||
| strength | float | Сила |
| agility | float | Ловкость |
| intelligence | float | Интеллект |
| conductivity | float | Проводимость |
| vitality | float | Живучесть |
| **Личность** |||
| personality | PersonalityTrait [Flags] | Черты характера |
| motivation | string | Мотивация |
| **Отношения** |||
| attitude | float | Отношение к ГГ (−100..+100) |
| relations | JSON | Отношения с другими |
| factionId | string | ID фракции |
| **Прочее (JSON)** |||
| equipment | JSON | Экипировка |
| techniques | JSON | Техники |

#### PersonalityTrait [Flags]

```csharp
[Flags]
public enum PersonalityTrait
{
    None        = 0,
    Aggressive  = 1 << 0,
    Cautious    = 1 << 1,
    Treacherous = 1 << 2,
    Ambitious   = 1 << 3,
    Loyal       = 1 << 4,
    Pacifist    = 1 << 5,
    Curious     = 1 << 6,
    Vengeful    = 1 << 7,
}
```

Черты характера NPC, комбинируемые через [Flags]. Влияют на AI-решения.

### 4.4 Location — Локация

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| description | string | Описание |
| **Координаты** |||
| x | int | Восток(+)/Запад(−) (м) |
| y | int | Север(+)/Юг(−) (м) |
| z | int | Высота(+)/Глубина(−) (логический Z) |
| distanceFromCenter | int | Расстояние от центра (км) |
| **Характеристики** |||
| qiDensity | int | Плотность Ци (ед/м³) |
| qiFlowRate | int | Поток Ци (ед/сек) |
| terrainType | string | mountains, plains, forest, sea, desert, ... |
| locationType | string | region, area, building, room |
| **Размеры** |||
| width | int | Ширина (м) |
| height | int | Высота (м) |

### 4.5 Sect — Секта

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| description | string | Описание |
| locationId | string | ID локации |
| powerLevel | float | Средний уровень культивации старейшин |
| resources | JSON | Ресурсы секты |

---

## 5. Инвентарь и экипировка

### 5.1 InventoryItem — Предмет инвентаря

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameId | string | ID для поиска пресета |
| type | string | weapon_sword, armor_torso, consumable_pill, ... |
| category | string | weapon, armor, accessory, consumable, material |
| rarity | string | common, uncommon, rare, epic, legendary, mythic |
| icon | string | Эмодзи или путь к иконке |
| **Количество** |||
| quantity | int | Количество |
| maxStack | int | Макс. в стаке |
| stackable | bool | Можно стакать |
| **Физика (строчная модель)** |||
| weight | float | Вес (кг) — КЛЮЧЕВОЙ параметр строчной модели |
| volume | float | Объём (литры) — параметр строчной модели |
| location | string | inventory, equipment, storage |
| equipmentSlot | string | Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff, Amulet, RingLeft1, ..., Charger |
| **Equipment V2** |||
| materialId | string | ID материала |
| materialTier | int | Тир (1–5) |
| grade | string | damaged, common, refined, perfect, transcendent |
| durabilityCurrent | int | Текущая прочность |
| durabilityMax | int | Макс. прочность |
| durabilityCondition | string | pristine, good, worn, damaged, broken |
| itemLevel | int | Уровень предмета (1–9) |
| effectiveDamage | int | Итоговый урон |
| effectiveDefense | int | Итоговая защита |
| bonusStats | JSON | Бонусы (источники: base, grade, material, set, enchant) |
| specialEffects | JSON | Особые эффекты |
| enchantId | string | ID зачарования (null = нет) |
| enchantTier | int | Тир зачарования (1–5) |

> Инвентарь использует **строчную модель** (weight + volume), а не сеточную. Поля sizeWidth/sizeHeight/posX/posY удалены.

### 5.2 Equipment — Экипированные предметы

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| characterId | string | ID персонажа |
| slotId | string | См. слоты ниже |
| itemId | string | ID предмета |
| equippedAt | long | Tick времени экипировки |

**Слоты экипировки (EquipmentSlot):**

| Слот | Категория | Описание |
|------|-----------|----------|
| Head | Body Zone | Голова |
| Torso | Body Zone | Торс |
| Belt | Belt | Ремень (заряды/зелья) |
| Legs | Body Zone | Ноги |
| Feet | Body Zone | Обувь |
| WeaponMain | Weapon | Основное оружие |
| WeaponOff | Weapon | Вторичное оружие |
| Amulet | Accessory (макс. 1) | Амулет |
| RingLeft1, RingLeft2 | Ring (макс. 4) | Кольца левой руки |
| RingRight1, RingRight2 | Ring | Кольца правой руки |
| Charger | Charger (макс. 1) | Зарядное устройство |
| Hands | (резерв) | Руки — будущее расширение |
| Back | (резерв) | Спина — будущее расширение |

---

## 6. Техники

### 6.1 Technique — Техника культивации

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameId | string | ID для поиска |
| description | string | Описание |
| **Классификация** |||
| type | string | combat, cultivation, support, movement, sensory, healing, defense, curse, poison |
| subtype | string | melee_strike, melee_weapon, ranged_projectile, ... |
| element | string | fire, water, earth, air, void, neutral |
| grade | string | common, refined, perfect, transcendent |
| level | int | Уровень техники (1–9) |
| **Параметры** |||
| baseCapacity | **long** | Базовая ёмкость |
| minLevel | int | Мин. уровень развития |
| maxLevel | int | Макс. уровень развития |
| canEvolve | bool | Можно развивать |
| **Требования** |||
| minCultivationLevel | int | Мин. уровень культивации |
| qiCost | **long** | Стоимость Ци |
| physicalFatigueCost | float | Физическая усталость |
| mentalFatigueCost | float | Ментальная усталость |
| statRequirements | JSON | Требования к статам |
| statScaling | JSON | Масштабирование от статов |
| effects | JSON | Эффекты |
| computedValues | JSON | Вычисленные значения |

> **Примечание:** Яд (poison) **не является элементом** — это состояние Ци. Обработка ядов обеспечивается через `technique.type = poison`. Список элементов: 6 значений (fire, water, earth, air, void, neutral) + опционально lightning.

### 6.2 CharacterTechnique — Изученная техника

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| characterId | string | ID персонажа |
| techniqueId | string | ID техники |
| mastery | float | Мастерство (0–100%) |
| quickSlot | int | Слот быстрого доступа |
| learningProgress | float | Прогресс изучения |
| learningSource | string | preset, npc, scroll, insight |

---

## 7. Формации

### 7.1 FormationCore — Ядро формации

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| coreType | string | disk, altar |
| variant | string | stone, jade, iron, spirit_iron, crystal, ... |
| levelMin | int | Мин. уровень формации |
| levelMax | int | Макс. уровень формации |
| maxSlots | int | Слоты для камней Ци |
| baseConductivity | int | Проводимость (ед/сек) |
| maxCapacity | int | Макс. ёмкость |
| isImbued | bool | Внедрена ли формация |
| imbuedTechniqueId | string | ID внедрённой техники |

### 7.2 ActiveFormation — Активная формация

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| sessionId | string | ID сессии |
| techniqueId | string | ID техники |
| coreId | string | ID ядра |
| level | int | Уровень |
| formationType | string | barrier, trap, amplification, suppression, ... |
| size | string | small, medium, large, great, heavy |
| currentQi | **long** | Текущее Ци |
| maxCapacity | **long** | Макс. ёмкость |
| contourQi | **long** | Затрачено на прорисовку |
| creationRadius | int | Радиус создания |
| effectRadius | int | Радиус эффекта |
| drainPerHour | int | Утечка Ци/час |
| stage | string | drawing, imbuing, mounting, filling, active, depleted |
| participants | JSON | Участники наполнения |

---

## 8. Мир и объекты

### 8.1 Building — Здание

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| buildingType | string | house, shop, temple, cave, tower, sect_hq |
| locationId | string | ID локации |
| width | int | Ширина (м) |
| length | int | Длина (м) |
| height | int | Высота (м) |
| isEnterable | bool | Можно войти |
| qiBonus | int | Бонус к медитации (%) |
| comfort | int | Комфорт |
| defense | int | Защита |

### 8.2 WorldObject — Объект мира

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| objectType | string | resource, container, interactable, decoration |
| x, y, z | int | Координаты (м, логический Z) |
| isInteractable | bool | Можно взаимодействовать |
| isCollectible | bool | Можно собрать |
| health | int | Здоровье |
| resourceType | string | herb, ore, wood, water |
| resourceCount | int | Количество ресурса |
| inventory | JSON | Предметы в контейнере |

---

## 9. BodyPart — Часть тела

> Kenshi-style повреждения. Часть тела — отдельная сущность, не просто HP-бар.

```csharp
public class BodyPart
{
    public string PartId { get; }            // "head", "torso", "left_arm", ...
    public string Name { get; }
    public BodyPartType Type { get; }         // Head, Torso, Arm, Leg, Organ, ...
    public float MaxHealth { get; }
    public float CurrentHealth { get; set; }  // 0–MaxHealth
    public float Bleeding { get; set; }       // 0–100 (%)
    public float Pain { get; set; }           // 0–100 (%)
    public bool IsMissing { get; set; }       // ампутирована
    public bool IsBandaged { get; set; }
}
```

Подробнее — в `02_systems/BODY_SYSTEM.md`.

---

## 10. Фракции и отношения

### 10.1 Faction — Фракция

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameEn | string | Английское название |
| nationId | string | ID нации |
| description | string | Описание |

### 10.2 FactionRelation — Отношения фракций

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| sourceId | string | ID фракции-источника |
| targetId | string | ID целевой фракции |
| relationType | string | ally, enemy, neutral, vassal |
| strength | int | Сила отношений (−100..+100) |

---

## 11. Материалы

### 11.1 Material — Материал

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| tier | int | Тир (1–5) |
| category | string | metal, organic, mineral, wood, crystal, leather, cloth, bone, spirit, void |
| properties | JSON | Физические свойства |
| bonuses | JSON | Бонусы материала |
| description | string | Описание |
| rarity | float | Шанс выпадения (0.1–100) |
| source | string | Где добывается |
| requiredLevel | int | Мин. уровень для обработки |

### 11.2 MaterialProperties (вложенный объект)

| Поле | Тип | Описание |
|------|-----|----------|
| baseDurability | int | Базовая прочность (30–600) |
| weight | float | Вес единицы (кг) |
| hardness | int | Твёрдость (1–10) |
| flexibility | float | Гибкость (0–1) |
| qiConductivity | float | Проводимость Ци (ед/сек) |
| qiRetention | int | Сохранение Ци (% в час) |
| damageBonus | int | Бонус к урону (%) |
| penetrationBonus | int | Бонус к пробитию (%) |
| defenseBonus | int | Бонус к защите (%) |
| resistanceBonus | JSON | Бонус к сопротивлениям |
| bufferCapacityMult | float | Множитель ёмкости буфера |
| heatResistance | int | Термостойкость (0–100) |
| specialProperties | string[] | Особые свойства |

---

## 12. SpeciesPreset — Виды существ

### 12.1 Иерархия типов души

| Уровень | Тип | Описание |
|---------|-----|----------|
| Уровень 1 | SoulType | ПЕРВИЧНЫЙ: character, creature, spirit, artifact, construct |
| Уровень 2 | Morphology | ВТОРИЧНЫЙ: humanoid, quadruped, bird, serpentine, arthropod, amorphous |
| Уровень 3 | Species | КОНКРЕТНЫЙ: human, elf, wolf, dragon |

### 12.2 Поля пресета вида

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| soulType | string | character, creature, spirit, artifact, construct |
| morphology | string | humanoid, quadruped, bird, serpentine, arthropod, amorphous, hybrid_centaur, hybrid_mermaid, hybrid_harpy, hybrid_lamia |
| bodyMaterial | string | organic, scaled, chitin, ethereal, mineral, chaos |
| **Характеристики (Range)** |||
| strength | {min, max} | Диапазон силы |
| agility | {min, max} | Диапазон ловкости |
| intelligence | {min, max} | Диапазон интеллекта |
| vitality | {min, max} | Диапазон жизнеспособности |
| **Способности** |||
| canCultivate | bool | Может культивировать |
| innateQiGeneration | bool | Врождённая генерация Ци |
| speechCapable | bool | Может говорить |
| toolUse | bool | Использует инструменты |
| learningRate | float | Скорость обучения (0.1–2.0) |
| **Культивация** |||
| coreCapacityBase | {min, max} | Базовая ёмкость ядра |
| maxCultivationLevel | int | Макс. уровень культивации |
| conductivityBase | float | Базовая проводимость |
| **Прочее** |||
| sizeClass | string | tiny, small, medium, large, huge |
| innateTechniques | JSON[] | Врождённые техники |
| weaknesses | string[] | Слабости |
| resistances | string[] | Сопротивления |
| lifespan | int | Продолжительность жизни |

### 12.3 Типы материалов тела

| Материал | Снижение урона | Примеры |
|----------|----------------|---------|
| organic | 0% | Люди, эльфы |
| scaled | 10% | Драконы, змеи |
| chitin | 20% | Пауки, скорпионы |
| ethereal | 70% | Призраки, духи |
| mineral | 50% | Големы |
| chaos | 30% | Хаотические существа |

---

## 13. Система ID

### 13.1 Форматы ID

**1. Префикс + счётчик (генерируемые объекты):**
- Формат: `{PREFIX}_{NUMBER:06d}`
- Пример: `MS_0512`, `WP_0042`, `NPC_000042`

### 13.2 Префиксы по типам

**Техники:**

| Тип | Префикс | Пример |
|-----|---------|--------|
| Удар телом | MS | MS_0512 |
| Оружейная | MW | MW_0042 |
| Дальняя | RG | RG_0123 |
| Защитная | DF | DF_0042 |
| Культивация | CU | CU_0010 |
| Поддержка | SP | SP_0042 |

**Предметы:**

| Тип | Префикс | Пример |
|-----|---------|--------|
| Оружие | WP | WP_0042 |
| Броня | AR | AR_0123 |
| Аксессуар | AC | AC_0007 |
| Расходник | CS | CS_0512 |
| Материал | MT | MT_0089 |
| Камень Ци | QS | QS_0033 |
| Зарядник | CH | CH_0015 |

**NPC:**

| Тип | Префикс | Пример |
|-----|---------|--------|
| Сгенерированный | NP | NP_000042 |
| Preset | NPC_PRESET | NPC_PRESET_00001 |
| Временный | TEMP | TEMP_083452 |

### 13.3 Система счётчиков

```json
// presets/counters.json
{
  "counters": {
    "MS": 1024,
    "MW": 512,
    "WP": 100,
    "NPC": 100
  }
}
```

---

## 14. Стратегии хранения

### 14.1 Хранить полностью (в сохранении сессии)

- Игровые персонажи (макс. 100).
- Каталог техник игрока.
- Инвентарь игроков.
- Состояния сессий.

### 14.2 Хранить по ID (в сохранении)

- NPC и монстры (до 100,000 на сессию).
- Техники NPC (массив ID).
- Экипировка NPC (массив ID).

### 14.3 Генерировать заранее (data resources / JSON)

- Пресеты техник (~2046 шт).
- Пресеты предметов (~2046 шт).
- Шаблоны NPC (~50 шт).
- Материалы (~50 шт).

### 14.4 Размер хранения пресетов

| Подход | 1 техника | 2046 техник |
|--------|-----------|-------------|
| Полный JSON | ~800 байт | ~1.6 MB |
| Base + Modifiers | ~150 байт | ~300 KB |
| **Экономия** | | **81%** |

---

## 15. Принципы моделей данных

1. **`long` для Qi, `float` для статов.** Не смешивать.
2. **`readonly struct` для сообщений между системами** (zero GC).
3. **Префикс + счётчик для ID** генерируемых объектов. Универсально, не зависит от движка.
4. **Хранение пресетов в data resources** (JSON или текстовые ресурсные файлы движка). НЕ используются движко-специфичные ресурсные типы.
5. **Строчная модель инвентаря** (weight + volume), не сеточная.
6. **Иерархия типов души** (SoulType → Morphology → Species) для существ.

---

## 16. Связанные документы

| Документ | Связь |
|----------|-------|
| `CONFIGURATIONS.md` | Конфигурационные пресеты (уровни, техники, материалы) |
| `SAVE_SYSTEM.md` | Сохранение и сериализация |
| `WORLD_SAVE_SYSTEM.md` | Чанковое сохранение мира |
| `02_systems/QI_SYSTEM.md` | Ци как `long`, регенерация |
| `02_systems/BODY_SYSTEM.md` | BodyPart, Kenshi-style повреждения |
| `04_entities/ENTITY_TYPES.md` | SoulType, Morphology, Species |
