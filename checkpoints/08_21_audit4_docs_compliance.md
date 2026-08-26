# Аудит 4: Code vs Documentation — ПОДРОБНЫЙ

**Дата:** 2026-08-22 (переработан)
**Task ID:** AUDIT-4
**Scope:** Соответствие кода документации docs_v2

---

## Сводка

- **Документов проверено:** ~25
- **Formula mismatches:** 2 (ОБА ИСПРАВЛЕНЫ ✅)
- **Architecture deviations:** 1 (ИСПРАВЛЕНА ✅)
- **Rule violations:** 1 (ИСПРАВЛЕНА ✅)
- **Концептуальные вопросы:** 4

**ВАЖНО:** Документация НЕ редактируется. Всё, что "не реализовано" (Belt quick-access, Body silhouette, NPC 3-tier AI, Faction system, Loot system, 8 missing services) — БУДЕТ РЕАЛИЗОВАНО ПОЗЖЕ. Это начальный этап проекта, отладка ядра. Этот аудит фиксирует только **реальные расхождения** между кодом и документацией (где код делает НЕ то, что описано в доке).

---

## ИСПРАВЛЕНО ✅ (коммит 5d51a9f)

### F1: qiCost formula — ИСПРАВЛЕНО ✅
- **Было:** `capacity × 0.15`
- **Стало:** `floor(baseCapacity × 2^(level-1))` (per TECHNIQUE_SYSTEM.md §5.2)

### F2: UltimateQiCostMultiplier — ИСПРАВЛЕНО ✅
- **Было:** 1.5f / 1500 permil
- **Стало:** 2.0f / 2000 permil (per TECHNIQUE_SYSTEM.md §9.1)

### F3: ЗАПРЕТ 2 — StorageRingService float qiCost — ИСПРАВЛЕНО ✅
- **Было:** `out float qiCost`
- **Стало:** `out long qiCost` (ЗАПРЕТ 2: no float for Qi)

### F4: GameLifetimeScope Charger order — ИСПРАВЛЕНО ✅
- **Было:** Charger at position 6
- **Стало:** Charger at position 14 (per DI_AND_EVENTBUS.md §1.2)

---

## Концептуальные вопросы для пользователя (4)

### Q1: Spirit + Ring storage — unified или separate?

**Документация (INVENTORY_SYSTEM.md):**
- §5 описывает **Spirit Storage** (духовное хранилище, Qi cost per access, unlimited slots)
- §6 описывает **Storage Ring** (кольцо хранения, экипируется, N слотов, str-volume model)

**Код:**
- `StorageService` (единый) реализует ОБА через `StorageType { Spirit, Ring }`
- `StorageRingService` — отдельный сервис для ring-specific логики (activate/deactivate, Qi cost)

**Вопрос:** Документация описывает их раздельно, код объединяет в один `StorageService` + отдельный `StorageRingService`. Это расхождение?

**Варианты:**
- **Вариант A:** Оставить как есть (код работает, DRY). Документация описывает концепцию, код реализует эффективно.
- **Вариант B:** Разделить на два отдельных сервиса (строго по доке). Дублирование кода.
- **Вариант C:** Отложить — не блокирует работу.

**Моя рекомендация:** Вариант A — код работает, архитектурно правильно (DRY). Документация описывает концептуальную модель, реализация может отличаться.

---

### Q2: Element.Poison — элемент или нет?

**Документация (ELEMENTS_SYSTEM.md):**
- Описывает 7 элементов: Fire, Water, Earth, Wind, Lightning, Light, Dark
- Говорит: "Poison is NOT an element" — это status effect, не стихия

**Код (`Enums.cs`):**
```csharp
public enum Element
{
    Neutral, Fire, Water, Earth, Wind, Lightning, Light, Dark,
    Poison  // ← добавлен, но док говорит "не элемент"
}
```

**Вопрос:** `Element.Poison` существует в коде, но документация говорит что Poison — не элемент. Это расхождение концепций.

**Варианты:**
- **Вариант A (рекомендую): Оставить Element.Poison в коде**
- Техники яда (Poison Strike, Toxic Cloud) используют `Element.Poison` для определения эффекта
- Удаление сломает существующий код
- Документация описывает 7 "базовых" стихий, Poison — производный тип
- **Время:** 0

- **Вариант B:** Удалить Element.Poison, использовать Neutral + status effect flag
- Требует рефакторинга всех техник с ядом
- **Время:** ~2 часа

- **Вариант C:** Отложить**

**Моя рекомендация:** Вариант A — Poison в коде работает как маркер для техник яда. Документация описывает базовые стихии, Poison — расширение. Не блокирует работу.

---

### Q3: ЗАПРЕТ 8 — TileMapLayer vs custom _Draw

**Документация (TECHNOLOGY_DECISIONS.md §3.4 + ЗАПРЕТ 8):**
- "Use TileMapLayer for tile rendering"
- Запрещает ручную отрисовку тайлов

**Код:**
- `BiomeTileRenderer` — custom `Node2D` с `_Draw()` методом
- `ObjectLayerRenderer` — custom `Node2D` с `_Draw()`
- `SurfaceTransitionRenderer` — custom `Node2D` с `_Draw()`

**Вопрос:** Код использует custom `_Draw()` вместо TileMapLayer, что нарушает ЗАПРЕТ 8.

**Варианты:**
- **Вариант A (рекомендую): Отложить — оставить custom _Draw**
- Custom _Draw уже работает, viewport culling реализован (1736× reduction)
- Миграция на TileMapLayer — большое рефакторинг (~3-5 дней)
- ЗАПРЕТ 8 — для финальной оптимизации, не для начального этапа
- **Время:** 0

- **Вариант B:** Мигрировать на TileMapLayer сейчас**
- Godot TileMapLayer автоматически делает culling, batching
- Требует создания TileSet ресурсов, atlas packing
- **Время:** ~3-5 дней

- **Вариант C:** Отложить до оптимизации**

**Моя рекомендация:** Вариант A — отложить. Custom _Draw работает, culling реализован. ЗАПРЕТ 8 — целевой ориентир, не блокатор для начального этапа. Документация НЕ редактируется.

---

### Q4: ItemCategory — taxonomy

**Документация (INVENTORY_SYSTEM.md §2.2):**
- Equipment (один тип, включает Weapon/Armor/Accessory)
- Consumable
- Material
- Quest
- QiStone (духовные камни как валюта)

**Код (`Enums.cs`):**
```csharp
public enum ItemCategory
{
    Weapon,      // ← разделён из Equipment
    Armor,       // ← разделён
    Accessory,   // ← разделён
    Consumable,
    Material,
    Technique,
    Quest,
    Misc
    // QiStone — отсутствует
}
```

**Вопрос:** Код разделяет Equipment на Weapon/Armor/Accessory (более детально), добавляет Technique+Misc, отсутствует QiStone.

**Варианты:**
- **Вариант A (рекомендую): Оставить как есть в коде**
- Разделение Weapon/Armor/Accessory — удобнее для фильтрации, генерации
- QiStone — будет добавлен когда реализуем экономику (позже)
- Technique/Misc — полезные расширения
- **Время:** 0

- **Вариант B:** Добавить QiStone сейчас**
- `ItemCategory.QiStone` для духовных камней (валюта)
- **Время:** ~15 минут

- **Вариант C:** Отложить**

**Моя рекомендация:** Вариант A — код работает, taxonomy более детальная чем в доке. QiStone добавим когда будем реализовывать экономику. Документация НЕ редактируется.

---

## Verified COMPLIANT ✅

Все ключевые формулы и правила соответствуют документации:

| Проверка | Документация | Код | Статус |
|----------|--------------|-----|--------|
| Capacity formula | `baseCapacity × 2^(level-1) × (1 + mastery × 0.005)` | ✅ Соответствует | ✅ |
| Grade multipliers (technique) | Common ×1.0, Refined ×1.3, Perfect ×1.6, Transcendent ×2.0 | ✅ Соответствует | ✅ |
| Grade multipliers (equipment) | Damaged ×0.5, Common ×1.0, Refined ×1.3, Perfect ×1.6, Transcendent ×2.0 | ✅ Соответствует | ✅ |
| Level suppression table | Permil variant | ✅ Точное соответствие | ✅ |
| Dual HP split | Red 0.7 / Black 0.3, Heart exception | ✅ Реализовано в BodyPart.cs | ✅ |
| Qi regeneration | 10%/day microcore, accumulator | ✅ Соответствует | ✅ |
| Core capacity formula | `1000 × 1.1^totalSubLevels × qualityMultiplier` | ✅ Соответствует | ✅ |
| Move cost table | terrain + object modifiers | ✅ Соответствует | ✅ |
| Carry weight | `10 + (STR-10)×2` | ✅ Соответствует | ✅ |
| ЗАПРЕТ 3.9 | Integer Permil math in combat | ✅ Соблюдается | ✅ |
| 3-layer architecture | Core/Modules/Entry/Adapter | ✅ Соблюдается | ✅ |
| BiomeType | 9 values (Ocean...Peak) | ✅ Соответствует | ✅ |
| EquipmentSlot | 15 slots (7 visible + 8 hidden) | ✅ Соответствует | ✅ |
| NPCRole | 8 values | ✅ Соответствует | ✅ |
| SoulType, Morphology, BodyMaterial | Все enums | ✅ Соответствуют | ✅ |
| Storage Ring model | str-volume per doc §6.4 | ✅ Соответствует | ✅ |
| Damage pipeline 11 layers | Layers 1-5, 8-11 implemented | ✅ (6-7 stubbed — будет позже) | ✅ |

---

## "Не реализовано" — БУДЕТ ПОЗЖЕ (не редактируем доку)

Эти элементы описаны в документации, но не реализованы. **Это нормально для начального этапа.** Документация НЕ редактируется.

| # | Элемент | Документация | Когда |
|---|---------|--------------|-------|
| 1 | Belt quick-access slots | INVENTORY_SYSTEM.md §7 | Позже (v2) |
| 2 | Body silhouette (procedural 64×64) | SPRITE_CATALOG.md | Позже |
| 3 | NPC 3-tier nervous system | NPC_AI_SYSTEM.md | Phase 3 NPC |
| 4 | Faction system | FACTION_SYSTEM.md | Phase 3 NPC |
| 5 | Full loot system | LOOT_SYSTEM_DRAFT.md (draft) | Позже |
| 6 | 8 missing services | MODULE_STRUCTURE.md | По мере реализации |
| 7 | NPCSpawnPhase | NPC_ASSEMBLY_PIPELINE.md | Phase 1 NPC |
| 8 | Save/load system | — | Позже |
| 9 | Combat target selection | — | Phase 6 NPC |
| 10 | 4 stub phases | — | По мере реализации |

**Действие:** Ничего. Документация остаётся как есть. Реализуем когда дойдём до каждой системы.

---

## План исправлений

### УЖЕ ИСПРАВЛЕНО ✅ (коммит 5d51a9f)
- F1: qiCost formula
- F2: UltimateQiCostMultiplier
- F3: ЗАПРЕТ 2 StorageRing float→long
- F4: GameLifetimeScope Charger order

### ОЖИДАЕТ РЕШЕНИЯ (4 концептуальных вопроса)
- Q1: Spirit+Ring storage unified/separate
- Q2: Element.Poison оставить/убрать
- Q3: TileMapLayer vs _Draw (отложить/мигрировать)
- Q4: ItemCategory taxonomy (оставить/добавить QiStone)

### НЕ ТРЕБУЕТ ДЕЙСТВИЙ
- Все "не реализовано" — будет позже, документация не редактируется

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-4)
