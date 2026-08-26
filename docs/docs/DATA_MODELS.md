# 🗄️ Модели данных: Unity Migration

**Версия:** 1.5  
**Дата:** 2026-07-14  
**Редактировано:** 2026-07-14 — добавлены UI-служебные модели (UIThemeV3, UIFontCache, UISpriteCache, UIPositioning, StatBonus, Position2D, InputFrameData, InventorySlot, LootEntry)  
**Статус:** 📋 Дополнено данными из кода  
**Источники:** ENTITY_TYPES.md, ARCHITECTURE.md, ALGORITHMS.md, код `Assets/Scripts/Core/Data/`  
> Обновлено: Qi-значения → long (Fix-01, согласно ARCHITECTURE.md)

---

## 🆕 UI-служебные модели (добавлено 2026-07-14)

> Эти модели не описаны в оригинальном документе, но присутствуют в коде `Assets/Scripts/Core/Data/`.

### UIThemeV3 (ScriptableObject)

Тема UI V3 «Древний Пергамент». Хранит цвета, спрайты, промилле-размеры.

| Поле | Тип | Назначение |
|------|-----|------------|
| `BackgroundPanel` | Color | Фон панелей |
| `BackgroundScreen` | Color | Фон overlay (полупрозрачный) |
| `BackgroundHeader` | Color | Фон заголовков |
| `BackgroundSection` | Color | Фон секций |
| `BorderColor` | Color | Цвет рамки |
| `AccentGold` | Color | Золотой акцент |
| `AccentJade` | Color | Нефритовый акцент |
| `AccentQi` | Color | Цвет Ци (циан) |
| `TextPrimary` | Color | Основной текст |
| `TextSecondary` | Color | Вторичный текст |
| `TextPositive` | Color | Положительный (зелёный) |
| `TextNegative` | Color | Отрицательный (красный) |
| `TextWarning` | Color | Предупреждение (жёлтый) |
| `PanelPaddingPromille` | int | Padding панелей (5‰ ≈ 10px at 1920) |
| `BarHeightSmallPromille` | int | Высота малой полосы (4‰) |
| `BarHeightMediumPromille` | int | Высота средней полосы (7‰) |
| `FontSizeCaptionPromille` | int | Шрифт caption (12‰) |
| `FontSizeBodyPromille` | int | Шрифт body (14‰) |
| `FontSizeSubtitlePromille` | int | Шрифт subtitle (18‰) |
| `FontSizeTitlePromille` | int | Шрифт title (26‰) |
| `PanelBorderDouble` | Sprite | Спрайт двойной рамки (9-slice) |
| `SectionBorder` | Sprite | Спрайт рамки секции |
| `SlotBorder` / `SlotBorderSelected` | Sprite | Спрайт рамки слота |

**Метод:** `PromilleToPixels(int promille, int referenceSize)` — конвертация промилле в пиксели (integer math).

### UIFontCache

Статический кэш шрифтов. Fallback: `Font.CreateDynamicFontFromOSFont` → `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.

| Поле | Тип | Назначение |
|------|-----|------------|
| `_staticFontCache` | `Dictionary<FontKey, Font>` | Статический кэш (без DI) |
| `_staticBuiltinFont` | `Font` | LegacyRuntime.ttf (ленивая загрузка) |
| `_staticTmpAvailable` | `bool` | TMP доступен |
| `_staticTmpUnavailableReason` | `string` | Причина недоступности TMP |

**FontKey** (internal struct): `FontName` (string) + `PixelSize` (int).

**Методы:**
- `GetFont(string fontName, int pixelSize)` — статический, callable без DI
- `EnsureStaticInitialized()` — ленивая инициализация, логирует TMP статус
- `GetStaticBuiltinFont()` — загрузка LegacyRuntime.ttf

**Константы:** `PRIMARY_FONT = "Arial"`, `MONO_FONT = "Consolas"`, `LEGACY_RUNTIME_FONT_PATH = "LegacyRuntime.ttf"`.

### UISpriteCache

Кэш спрайтов темы. Wiring через `WireUIViews()` → `UIThemeV3`.

### UIPositioning

Утилиты позиционирования UI (промилле → пиксели, anchor helpers).

### Прочие служебные модели

| Модель | Тип | Назначение |
|--------|-----|------------|
| `StatBonus` | struct | Бонус характеристики (StatType + value) |
| `Position2D` | struct | Позиция в мире (int x, y — промилле) |
| `InputFrameData` | struct | Кадр ввода (keyboard + mouse state) |
| `InventorySlot` | struct | Слот инвентаря (itemId + count) |
| `LootEntry` | struct | Запись лута (itemId + chance + count) |

---

## 📚 Оригинальные модели данных (legacy + modular)

---

## ⚠️ Важно

> **Это ЧЕРНОВИК теоретического документа.**  
> Документ будет перерабатываться в процессе разработки.  
> **НЕТ КОДА** — только теоретические описания структур данных.
>
> **✅ Переработка инвентаря ЗАВЕРШЕНА:** Поля sizeWidth, sizeHeight, posX, posY — **удалены** из кода. Система переведена на строчную модель. Ограничители: weight (масса) + volume (объём). BackpackData: gridWidth/gridHeight заменены на maxWeight/maxVolume/ownWeight.
>
> **📌 Размерность мира:** См. [WORLD_MAP_SYSTEM.md](./WORLD_MAP_SYSTEM.md) — §0.1, §0.2

---

## 📋 Обзор

Документ описывает структуры данных, которые необходимо перенести из текущего проекта (Prisma/SQLite) в Unity (ScriptableObjects/JSON).

---

## 🏗️ Основные сущности

### 1. GameSession — Игровая сессия

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| worldId | string | ID мира |
| worldName | string | Название мира |
| startVariant | int | 1=секта, 2=случайный, 3=кастомный |
| worldYear | int | Год по Э.С.М. |
| worldMonth | int | Месяц (1-12) |
| worldDay | int | День (1-30) |
| worldHour | int | Час (0-23) |
| worldMinute | int | Минута (0-59) |
| daysSinceStart | int | Дней от попадания |
| isPaused | bool | Пауза симуляции |
| worldState | JSON | Текущее состояние мира |

### 2. Character — Персонаж игрока

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
| cultivationLevel | int | Основной уровень (1-9) |
| cultivationSubLevel | int | Под-уровень (0-9) |
| coreCapacity | long | Ёмкость ядра |
| coreQuality | float | Качество ядра |
| currentQi | long | Текущее Ци |
| accumulatedQi | long | Накопленное для прорыва |
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

### 3. NPC — Неигровые персонажи

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
| coreCapacity | long | Ёмкость ядра |
| currentQi | long | Текущее Ци |
| **Характеристики** |||
| strength | float | Сила |
| agility | float | Ловкость |
| intelligence | float | Интеллект |
| conductivity | float | Проводимость |
| vitality | float | Живучесть |
| **Личность (JSON)** |||
| personality | JSON | Черты характера (PersonalityTrait [Flags] в коде) |
| motivation | string | Мотивация |
| **Отношения** |||
| attitude | float | Отношение к ГГ (-100 до 100) (Fix-07: переименовано из disposition) |
| relations | JSON | Отношения с другими |
| factionId | string | ID фракции |
| **Прочее (JSON)** |||
| equipment | JSON | Экипировка |
| techniques | JSON | Техники |

### 4. Location — Локации

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| description | string | Описание |
| **Координаты 3D** |||
| x | int | Восток(+)/Запад(-) |
| y | int | Север(+)/Юг(-) |
| z | int | Высота(+)/Глубина(-) |
| distanceFromCenter | int | Расстояние от центра |
| **Характеристики места** |||
| qiDensity | int | Плотность Ци (ед/м³) |
| qiFlowRate | int | Поток Ци (ед/сек) |
| terrainType | string | mountains, plains, forest, sea, desert |
| locationType | string | region, area, building, room |
| **Размеры** |||
| width | int | Ширина (м) |
| height | int | Высота (м) |

### 5. Sect — Секты

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| description | string | Описание |
| locationId | string | ID локации |
| powerLevel | float | Средний уровень культивации старейшин |
| resources | JSON | Ресурсы секты |

---

## 📦 Инвентарь и экипировка

### 6. InventoryItem — Предмет инвентаря

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameId | string | ID для поиска пресета |
| type | string | weapon_sword, armor_torso, consumable_pill... |
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
| ~~sizeWidth~~ | ~~int~~ | ⚠️ УДАЛЕНО — было в сеточной модели |
| ~~sizeHeight~~ | ~~int~~ | ⚠️ УДАЛЕНО — было в сеточной модели |
| ~~posX~~ | ~~int~~ | ⚠️ УДАЛЕНО — было в сеточной модели |
| ~~posY~~ | ~~int~~ | ⚠️ УДАЛЕНО — было в сеточной модели |
| location | string | inventory, equipment, storage |
| equipmentSlot | string | Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff, Amulet, RingLeft1, RingLeft2, RingRight1, RingRight2, Charger, Hands, Back |
| **Equipment V2** |||
| materialId | string | ID материала |
| materialTier | int | Тир (1-5) |
| grade | string | damaged, common, refined, perfect, transcendent |
| | | > Грейды экипировки: [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md) §2.1 |
| durabilityCurrent | int | Текущая прочность |
| durabilityMax | int | Макс. прочность |
| durabilityCondition | string | pristine, good, worn, damaged, broken |
| itemLevel | int | Уровень предмета (1-9) |
| effectiveDamage | int | Итоговый урон |
| effectiveDefense | int | Итоговая защита |
| bonusStats | JSON | Бонусы (источники: base, grade, material, set, enchant) |
| specialEffects | JSON | Особые эффекты |
| enchantId | string | ID зачарования (null = нет) |
| enchantTier | int | Тир зачарования (1-5) |

### 7. Equipment — Экипированные предметы

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| characterId | string | ID персонажа |
| slotId | string | См. слоты ниже |
| itemId | string | ID предмета |
| equippedAt | DateTime | Время экипировки |

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
| Amulet | Accessory 🔒 | Амулет (макс. 1) — ЗАГЛУШКА |
| RingLeft1 | Ring 🔒 | Кольцо левое 1 — ЗАГЛУШКА |
| RingLeft2 | Ring 🔒 | Кольцо левое 2 — ЗАГЛУШКА |
| RingRight1 | Ring 🔒 | Кольцо правое 1 — ЗАГЛУШКА |
| RingRight2 | Ring 🔒 | Кольцо правое 2 — ЗАГЛУШКА |
| Charger | Charger 🔒 | Зарядное устройство (макс. 1) — ЗАГЛУШКА |
| Hands | 🔒 Заглушка | Руки (резерв) |
| Back | 🔒 Заглушка | Спина (резерв) |

> **Head/Torso/Legs/Feet** = body zones; **Belt** = для зарядов/зелий; **WeaponMain/Off** = оружие; **Rings** = макс. 4; **Amulet** = 1; **Charger** = 1; **Hands/Back** = будущее расширение.

---

## ⚔️ Техники

### 8. Technique — Техника культивации

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameId | string | ID для поиска |
| description | string | Описание |
| **Классификация** |||
| type | string | combat, cultivation, support, movement, sensory, healing, defense, curse, poison |
| subtype | string | melee_strike, melee_weapon, ranged_projectile... |
| element | string | fire, water, earth, air, void, neutral |
| grade | string | common, refined, perfect, transcendent |
| | | > Грейды техник: [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) §«Система Grade» |
| level | int | Уровень техники (1-9) |
| **Параметры** |||
| baseCapacity | long | Базовая ёмкость |
| minLevel | int | Мин. уровень развития |
| maxLevel | int | Макс. уровень развития |
| canEvolve | bool | Можно развивать |
| **Требования** |||
| minCultivationLevel | int | Мин. уровень культивации |
| qiCost | long | Стоимость Ци |
| physicalFatigueCost | float | Физическая усталость |
| mentalFatigueCost | float | Ментальная усталость |
| statRequirements | JSON | Требования к статам |
| statScaling | JSON | Масштабирование от статов |
| effects | JSON | Эффекты |
| computedValues | JSON | Вычисленные значения |

> **Примечание (S-03):** Яд (poison) **не является элементом** — это состояние Ци. Обработка ядов обеспечивается через `technique.type=poison`. Список элементов остаётся из 7 значений (fire, water, earth, air, void, neutral).

### 9. CharacterTechnique — Изученная техника

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| characterId | string | ID персонажа |
| techniqueId | string | ID техники |
| mastery | float | Мастерство (0-100%) |
| quickSlot | int | Слот быстрого доступа |
| learningProgress | float | Прогресс изучения |
| learningSource | string | preset, npc, scroll, insight |

---

## 🌀 Формации

### 10. FormationCore — Ядро формации

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| coreType | string | disk, altar |
| variant | string | stone, jade, iron, spirit_iron, crystal... |
| levelMin | int | Мин. уровень формации |
| levelMax | int | Макс. уровень формации |
| maxSlots | int | Слоты для камней Ци |
| baseConductivity | int | Проводимость (ед/сек) |
| maxCapacity | int | Макс. ёмкость |
| isImbued | bool | Внедрена ли формация |
| imbuedTechniqueId | string | ID внедрённой техники |

### 11. ActiveFormation — Активная формация

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| sessionId | string | ID сессии |
| techniqueId | string | ID техники |
| coreId | string | ID ядра |
| level | int | Уровень |
| formationType | string | barrier, trap, amplification, suppression... |
| size | string | small, medium, large, great, heavy |
| currentQi | long | Текущее Ци |
| maxCapacity | long | Макс. ёмкость |
| contourQi | long | Затрачено на прорисовку |
| creationRadius | int | Радиус создания |
| effectRadius | int | Радиус эффекта |
| drainPerHour | int | Утечка Ци/час |
| stage | string | drawing, imbuing, mounting, filling, active, depleted |
| participants | JSON | Участники наполнения |

---

## 🗺️ Мир и объекты

### 12. Building — Здание

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

### 13. WorldObject — Объект мира

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| objectType | string | resource, container, interactable, decoration |
| x, y, z | int | Координаты |
| isInteractable | bool | Можно взаимодействовать |
| isCollectible | bool | Можно собрать |
| health | int | Здоровье |
| resourceType | string | herb, ore, wood, water |
| resourceCount | int | Количество ресурса |
| inventory | JSON | Предметы в контейнере |

---

## 📊 Фракции и отношения

### 14. Faction — Фракция

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| nameEn | string | Английское название |
| nationId | string | ID нации |
| description | string | Описание |

### 15. FactionRelation — Отношения фракций

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| sourceId | string | ID фракции-источника |
| targetId | string | ID целевой фракции |
| relationType | string | ally, enemy, neutral, vassal |
| strength | int | Сила отношений (-100 до 100) |

---

## 🔧 Материалы

### 16. Material — Материал

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| name | string | Название |
| tier | int | Тир (1-5) |
| category | string | metal, organic, mineral, wood, crystal |
| properties | JSON | Физические свойства |
| bonuses | JSON | Бонусы материала |
| description | string | Описание |
| rarity | float | Шанс выпадения (0.1-100) |
| source | string | Где добывается |
| requiredLevel | int | Мин. уровень для обработки |

---

## 📁 Файловая структура Unity

### ScriptableObjects

| Файл | Назначение |
|------|------------|
| `ScriptableObjects/CharacterData.asset` | Данные персонажа |
| `ScriptableObjects/NPCData.asset` | Данные NPC |
| `ScriptableObjects/LocationData.asset` | Данные локаций |
| `ScriptableObjects/TechniqueData.asset` | Данные техник |
| `ScriptableObjects/ItemData.asset` | Данные предметов |
| `ScriptableObjects/MaterialData.asset` | Данные материалов |

### JSON для сохранения

| Файл | Назначение |
|------|------------|
| `Saves/session.json` | Текущая сессия |
| `Saves/world_state.json` | Состояние мира |
| `Saves/characters.json` | Персонажи |
| `Saves/npcs.json` | NPC |

---

## 17. SpeciesPreset — Виды существ

### Иерархия типов души

| Уровень | Тип | Описание |
|---------|-----|----------|
| Уровень 1 | SoulType | ПЕРВИЧНЫЙ: character, creature, spirit, artifact, construct |
| Уровень 2 | Morphology | ВТОРИЧНЫЙ: humanoid, quadruped, bird, serpentine, arthropod, amorphous |
| Уровень 3 | Species | КОНКРЕТНЫЙ: human, elf, wolf, dragon |

### Поля пресета вида

| Поле | Тип | Описание |
|------|-----|----------|
| id | string | Уникальный ID |
| soulType | string | character, creature, spirit, artifact, construct |
| morphology | string | humanoid, quadruped, bird, serpentine, arthropod, amorphous, hybrid_centaur, hybrid_mermaid, hybrid_harpy, hybrid_lamia |
| bodyMaterial | string | organic, scaled, chitin, ethereal, mineral, chaos |
| **Характеристики (Range)** ||
| strength | {min, max} | Диапазон силы |
| agility | {min, max} | Диапазон ловкости |
| intelligence | {min, max} | Диапазон интеллекта |
| vitality | {min, max} | Диапазон жизнеспособности |
| **Способности** ||
| canCultivate | bool | Может культивировать |
| innateQiGeneration | bool | Врождённая генерация Ци |
| speechCapable | bool | Может говорить |
| toolUse | bool | Использует инструменты |
| learningRate | float | Скорость обучения (0.1-2.0) |
| **Культивация** ||
| coreCapacityBase | {min, max} | Базовая ёмкость ядра |
| maxCultivationLevel | int | Макс. уровень культивации |
| conductivityBase | float | Базовая проводимость |
| **Прочее** ||
| sizeClass | string | tiny, small, medium, large, huge |
| innateTechniques | JSON[] | Врождённые техники |
| weaknesses | string[] | Слабости |
| resistances | string[] | Сопротивления |
| lifespan | int | Продолжительность жизни |

### Типы материалов тела

> **Источник истины:** [ENTITY_TYPES.md](./ENTITY_TYPES.md) §5 "Материалы тела"

Материалы тела и их свойства — в ENTITY_TYPES.md.

---

## 📚 Связанные документы

- [ARCHITECTURE.md](./ARCHITECTURE.md) — Общая архитектура Unity
- [SAVE_SYSTEM.md](./SAVE_SYSTEM.md) — Система сохранений
- [CONFIGURATIONS.md](./CONFIGURATIONS.md) — Конфигурации
- [ALGORITHMS.md](./ALGORITHMS.md) — Алгоритмы и формулы

---

*Документ создан: 2026-03-30*  
*Обновлено: 2026-04-27 — Аудит: С-3 (гибридные морфологии), М-2 (источники)*  
*Редактировано: 2026-04-27 — Переход на строчную модель инвентаря, удаление grid-полей, добавление volume*
*Статус: Черновик для доработки*  
*Только теория — код будет в отдельных файлах*
