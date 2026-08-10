# 🎭 Черновик: Equipped-спрайты — Подготовка и реализация

**Дата:** 2026-04-29  
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)  
**Статус:** 📝 Черновик  
**Решение:** ✅ Упрощённая модель — 1 слот = 1 предмет = 1 equipped-спрайт (без слоёв экипировки)  
**Редактировано:** 2026-04-29 06:05:00 UTC

---

## 📎 Перекрёстные ссылки

| Документ | Описание |
|----------|----------|
| **`docs/SPRITE_INDEX.md`** §5 | Полный перечень существующих icon-спрайтов экипировки (21 файл) |
| **`docs_temp/EQUIPMENT_SPRITES_AUDIT.md`** | Аудит недостающих спрайтов, план генерации icon + equipped |
| **`docs_temp/CharacterSpriteMirroring.md`** | Зеркалирование спрайтов персонажей (flipX / scale) |

---

## 📌 Постановка задачи

Отображать экипировку **на персонаже** в реальном времени: когда игрок надевает предмет, визуальное представление персонажа изменяется — поверх базового спрайта накладываются слои надетой брони/оружия.

**Два состояния предмета:**
1. **На земле (Drop)** — иконка предмета в мире и инвентаре ✅ *(реализовано)*
2. **Надетый (Equipped)** — визуальный слой на персонаже ❌ *(НЕ реализовано)*

---

## 🔴 Текущее состояние (аудит)

### Что существует

| Компонент | Статус | Описание |
|-----------|--------|----------|
| Icon-спрайты (Equipment/Icons/) | ✅ | 128×128 px, для инвентаря и дропа → **перечень см. `docs/SPRITE_INDEX.md`** §5 |
| Исходные спрайты (Equipment/) | ✅ | 1024×1024 px, для генерации иконок → **перечень см. `docs/SPRITE_INDEX.md`** §5 |
| `ItemData.icon: Sprite` | ✅ | Единое поле иконки, используется в UI |
| `EquipmentController.OnEquipmentEquipped` | ✅ | Событие экипировки |
| `EquipmentController.OnEquipmentUnequipped` | ✅ | Событие снятия |
| PlayerVisual (SpriteRenderer) | ✅ | Базовый спрайт персонажа, слой «Player» |

### Чего НЕ существует

| Компонент | Статус | Описание |
|-----------|--------|----------|
| Equipped-спрайты (папка и файлы) | ❌ | Нет ни одного equipped-спрайта |
| `EquipmentData.equippedSprite: Sprite` | ❌ | Нет поля для equipped-спрайта |
| `EquipmentVisualController` | ❌ | Нет контроллера визуальных слоёв |
| Система наложения слоёв | ❌ | Нет механизма overlay |
| Sorting Group для персонажа | ❌ | Нет группировки player+equipment |
| Слой «Equipment» | ❌ | Нет sorting layer для экипировки |

### Ключевая проблема

**`ItemData` содержит только `public Sprite icon`** — единственное поле для визуального отображения. При экипировке предмета изменяется только иконка в UI-слоте куклы, а **спрайт персонажа остаётся статичным**.

---

## 🏗️ Архитектура решения

### Подход: Overlay Layering (наложение слоёв)

Выбран подход с **отдельными SpriteRenderer** для каждого слота экипировки, наложенными поверх базового спрайта персонажа. Альтернативы рассмотрены в §Приложение А.

#### Иерархия GameObject

```
Player (GameObject)
├── Visual (SpriteRenderer)                ← Базовый спрайт персонажа
│   sortingLayer="Player", sortingOrder=0
│
├── Shadow (SpriteRenderer)                ← Тень
│   sortingLayer="Player", sortingOrder=-1
│
├── EquipmentVisual (GameObject)            ← Контейнер экипировки [SortingGroup]
│   ├── Eq_Head (SpriteRenderer)           ← Шлем/капюшон
│   │   sortingLayer="Player", sortingOrder=10
│   ├── Eq_Torso (SpriteRenderer)          ← Нагрудник/роба
│   │   sortingLayer="Player", sortingOrder=20
│   ├── Eq_Legs (SpriteRenderer)           ← Поножи/штаны
│   │   sortingLayer="Player", sortingOrder=30
│   ├── Eq_Feet (SpriteRenderer)           ← Сапоги
│   │   sortingLayer="Player", sortingOrder=40
│   ├── Eq_Hands (SpriteRenderer)          ← Перчатки
│   │   sortingLayer="Player", sortingOrder=50
│   ├── Eq_Belt (SpriteRenderer)           ← Пояс
│   │   sortingLayer="Player", sortingOrder=25
│   ├── Eq_WeaponMain (SpriteRenderer)     ← Оружие в основной руке
│   │   sortingLayer="Player", sortingOrder=60
│   └── Eq_WeaponOff (SpriteRenderer)      ← Оружие во второй руке
│       sortingLayer="Player", sortingOrder=55
│
└── (существующие компоненты PlayerController, EquipmentController и т.д.)
```

#### Sorting Group

Контейнер `EquipmentVisual` получает компонент **SortingGroup**, который гарантирует:
- Все дочерние SpriteRenderer рендерятся как **единая группа**
- Спрайты экипировки **не перемешиваются** с объектами мира (деревья, NPC)
- SortingGroup рендерится ПОСЛЕ Terrain и Objects, но ДО UI

> **Unity Docs (Sprite Sorting):** «Sorting Groups prevent 2D GameObjects from mixing in sorting layers» — группа рендерится целиком, без интерливинга с другими объектами на том же слое.

### Sorting Layers — обновлённая конфигурация

```
Текущая:  Default → Background → Terrain → Objects → Player → UI
Новая:    Default → Background → Terrain → Objects → Player → UI
```

**Изменение НЕ требуется.** Equipment-слои используют тот же sorting layer «Player» с разными `sortingOrder`. SortingGroup гарантирует корректный порядок.

---

## 📐 Спецификация Equipped-спрайтов

### Размеры и формат

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| **Размер холста** | 256×256 px | Базовый персонаж = 128×128 при PPU=64 → 2 юнита. Equipped = 2× для детализации |
| **PPU (Pixels Per Unit)** | 64 | Совпадает с базовым спрайтом персонажа (PlayerVisual) |
| **Формат** | PNG, RGBA | Прозрачный фон — обязательное требование для overlay |
| **Стиль** | Xianxia fantasy, контурная обводка | Совпадает с существующими AI-спрайтами |
| **Pivot** | (0.5, 0.25) — центр снизу | Совпадает с PlayerVisual.CreateCircleSprite() |
| **Фон** | Полностью прозрачный | Спрайт — overlay, не должен закрывать персонажа |

### Почему 256×256, а не 1024×1024?

| Размер | Плюсы | Минусы |
|--------|-------|--------|
| 1024×1024 | Максимальная детализация | Oversized для overlay, ~4 МБ VRAM на спрайт, дольше генерация |
| 256×256 | Оптимальный размер, ~256 КБ VRAM, быстрая генерация | Менее детализирован |
| 128×128 | Минимальный VRAM | Недостаточно для оружейных слоёв (посох, копьё) |

**Выбор:** 256×256 — баланс качества и производительности. При PPU=64 это 4×4 юнита — достаточно для любого слота.

### Требования к содержимому спрайтов

Каждый equipped-спрайт должен:

1. **Совпадать по позиции с персонажем** — голова в верхней части, ноги в нижней
2. **Иметь прозрачные области** — только часть тела, которую закрывает предмет
3. **Учитывать pivot** — точка привязки (0.5, 0.25) = центр снизу
4. **Не выходить за границы** — 256×256 холст, центрированный относительно персонажа

#### Примеры расположения на холсте 256×256

```
┌──────────────────────┐
│         Head          │ ← Шлем: только верхняя часть (y=160..256)
│     ┌──────┐          │
│     │ helmet│          │
│     └──────┘          │
│                        │
│   ┌──────────────┐    │ ← Torso: центральная часть (y=80..180)
│   │   armor/robe  │    │
│   └──────────────┘    │
│                        │
│      ┌────┐  ┌────┐   │ ← Feet: нижняя часть (y=0..50)
│      │boot │  │boot │   │
│      └────┘  └────┘   │
└──────────────────────┘
     ↑ Pivot (128, 64) — (0.5, 0.25) в нормализованных координатах
```

---

## 🗂️ Структура папок

```
Sprites/
├── Equipment/
│   ├── Icons/                    ← 128×128 иконки (инвентарь/дроп) ✅ СУЩЕСТВУЕТ
│   │   ├── weapon_*.png
│   │   ├── armor_*.png
│   │   └── accessory_*.png
│   ├── Sources/                  ← 1024×1024 исходники ✅ СУЩЕСТВУЕТ (как parent)
│   │   ├── weapon_*.png
│   │   ├── armor_*.png
│   │   └── accessory_*.png
│   └── Equipped/                 ← 256×256 спрайты надетой экипировки ❌ НОВОЕ
│       ├── Head/
│       │   ├── eq_helmet_iron.png
│       │   ├── eq_helmet_medium.png
│       │   └── eq_hood_cloth.png
│       ├── Torso/
│       │   ├── eq_robe_cloth.png
│       │   ├── eq_robe_spirit.png
│       │   ├── eq_vest_leather.png
│       │   ├── eq_chainmail.png
│       │   ├── eq_torso_iron.png
│       │   └── eq_full_plate.png
│       ├── Legs/
│       │   ├── eq_pants_cloth.png
│       │   ├── eq_pants_leather.png
│       │   └── eq_greaves_iron.png
│       ├── Feet/
│       │   ├── eq_boots_cloth.png
│       │   ├── eq_boots_leather.png
│       │   ├── eq_boots_chain.png
│       │   └── eq_sabatons_plate.png
│       ├── Hands/
│       │   ├── eq_gloves_leather.png
│       │   ├── eq_gloves_chain.png
│       │   └── eq_gauntlets_plate.png
│       ├── Arms/
│       │   ├── eq_bracers_cloth.png
│       │   ├── eq_bracers_chain.png
│       │   └── eq_bracers_plate.png
│       ├── Belt/
│       │   └── eq_belt_leather.png
│       ├── WeaponMain/
│       │   ├── eq_sword_iron.png
│       │   ├── eq_sword_spirit.png
│       │   ├── eq_axe_iron.png
│       │   ├── eq_spear_iron.png
│       │   ├── eq_bow_wood.png
│       │   ├── eq_staff_wood.png
│       │   ├── eq_staff_jade.png
│       │   ├── eq_dagger_iron.png
│       │   ├── eq_greatsword_iron.png
│       │   ├── eq_hammer_iron.png
│       │   ├── eq_mace_iron.png
│       │   ├── eq_wand_wood.png
│       │   ├── eq_crossbow_iron.png
│       │   └── eq_claws.png
│       └── Accessories/
│           ├── eq_ring_bronze.png
│           ├── eq_ring_silver.png
│           ├── eq_ring_jade.png
│           └── eq_amulet_jade.png
```

**Именование:** `eq_` префикс отличает equipped-спрайты от icon-спрайтов.

---

## 💻 Изменения в коде

### 1. EquipmentData.cs — добавить поле `equippedSprite`

```csharp
// В класс EquipmentData (после существующих полей)

[Header("Visuals")]
[Tooltip("Спрайт надетой экипировки (overlay на персонаже)")]
public Sprite equippedSprite;
```

**Обоснование:** Поле хранит ссылку на equipped-спрайт. Inspector позволяет назначить спрайт вручную или через генератор.

### 2. Новый компонент: EquipmentVisualController.cs

```csharp
// ============================================================================
// EquipmentVisualController.cs — Визуальное отображение экипировки на персонаже
// ============================================================================

using UnityEngine;
using CultivationGame.Core;
using CultivationGame.Data.ScriptableObjects;
using CultivationGame.Inventory;

namespace CultivationGame.Player
{
    /// <summary>
    /// Управляет визуальными слоями экипировки на персонаже.
    /// Создаёт отдельные SpriteRenderer для каждого слота,
    /// обновляет спрайты при экипировке/снятии.
    /// </summary>
    [RequireComponent(typeof(EquipmentController))]
    public class EquipmentVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot; // Visual-объект игрока
        
        [Header("Settings")]
        [SerializeField] private bool useSortingGroup = true;
        [SerializeField] private string sortingLayerName = "Player";
        
        // Слоты → SpriteRenderer
        private Dictionary<EquipmentSlot, SpriteRenderer> equipmentRenderers;
        private GameObject equipmentContainer;
        private SortingGroup sortingGroup;
        private EquipmentController equipmentController;
        
        // SortingOrder для каждого слота (определяет порядок наложения)
        private static readonly Dictionary<EquipmentSlot, int> SlotSortingOrder = new()
        {
            { EquipmentSlot.Feet,        10 },
            { EquipmentSlot.Legs,        20 },
            { EquipmentSlot.Belt,        25 },
            { EquipmentSlot.Torso,       30 },
            { EquipmentSlot.Hands,       40 },
            { EquipmentSlot.Head,        50 },
            { EquipmentSlot.WeaponOff,   55 },
            { EquipmentSlot.WeaponMain,  60 },
        };
        
        private void Awake()
        {
            equipmentController = GetComponent<EquipmentController>();
            CreateEquipmentVisuals();
        }
        
        private void OnEnable()
        {
            equipmentController.OnEquipmentEquipped += OnEquipped;
            equipmentController.OnEquipmentUnequipped += OnUnequipped;
        }
        
        private void OnDisable()
        {
            equipmentController.OnEquipmentEquipped -= OnEquipped;
            equipmentController.OnEquipmentUnequipped -= OnUnequipped;
        }
        
        private void CreateEquipmentVisuals()
        {
            // Создаём контейнер
            equipmentContainer = new GameObject("EquipmentVisual");
            equipmentContainer.transform.SetParent(visualRoot);
            equipmentContainer.transform.localPosition = Vector3.zero;
            equipmentContainer.transform.localRotation = Quaternion.identity;
            equipmentContainer.transform.localScale = Vector3.one;
            
            // SortingGroup — предотвращает смешивание с другими объектами
            if (useSortingGroup)
                sortingGroup = equipmentContainer.AddComponent<SortingGroup>();
            
            equipmentRenderers = new Dictionary<EquipmentSlot, SpriteRenderer>();
            
            // Создаём SpriteRenderer для каждого видимого слота
            foreach (var slot in EquipmentController.VisibleSlots)
            {
                var obj = new GameObject($"Eq_{slot}");
                obj.transform.SetParent(equipmentContainer.transform);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = SlotSortingOrder.GetValueOrDefault(slot, 0);
                sr.sprite = null;
                sr.enabled = false; // Скрыт, пока нет экипировки
                
                // Unlit шейдер — рендерит без Light2D (как PlayerVisual)
                Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (spriteShader != null)
                    sr.material = new Material(spriteShader);
                
                equipmentRenderers[slot] = sr;
            }
        }
        
        private void OnEquipped(EquipmentSlot slot, EquipmentInstance instance)
        {
            if (!equipmentRenderers.TryGetValue(slot, out var sr))
                return;
            
            if (instance?.equipmentData?.equippedSprite != null)
            {
                sr.sprite = instance.equipmentData.equippedSprite;
                sr.enabled = true;
            }
        }
        
        private void OnUnequipped(EquipmentSlot slot, EquipmentInstance instance)
        {
            if (!equipmentRenderers.TryGetValue(slot, out var sr))
                return;
            
            sr.sprite = null;
            sr.enabled = false;
        }
        
        /// <summary>
        /// Зеркалирует все спрайты экипировки (при повороте персонажа).
        /// Вызывается из CharacterSpriteController.
        /// </summary>
        public void SetFlipX(bool flipX)
        {
            foreach (var kvp in equipmentRenderers)
            {
                if (kvp.Value != null)
                    kvp.Value.flipX = flipX;
            }
        }
    }
}
```

### 3. CharacterSpriteController.cs — интеграция с EquipmentVisualController

Добавить вызов `SetFlipX` при зеркалировании:

```csharp
// В метод ApplyFacingDirection():
private EquipmentVisualController equipmentVisual;

private void Awake()
{
    // ... existing code ...
    equipmentVisual = GetComponentInParent<EquipmentVisualController>();
}

private void ApplyFacingDirection()
{
    Vector3 scale = _originalScale;
    scale.x = Mathf.Abs(scale.x) * _facingDirection;
    transform.localScale = scale;
    
    // Зеркалируем экипировку
    if (equipmentVisual != null)
        equipmentVisual.SetFlipX(_facingDirection < 0);
}
```

### 4. PlayerVisual.cs — ссылка на Visual Root

PlayerVisual уже создаёт `visualObj` с именем «Visual». EquipmentVisualController должен получить на него ссылку через Inspector или автоматически:

```csharp
// В EquipmentVisualController.Awake() — автоматический поиск:
if (visualRoot == null)
{
    var playerVisual = GetComponentInChildren<PlayerVisual>();
    if (playerVisual != null)
        visualRoot = playerVisual.transform; // или Find("Visual")
}
```

### 5. Генератор — WeaponGenerator.cs / ArmorGenerator.cs

После добавления `equippedSprite` в EquipmentData, генераторы должны получать ссылку на equipped-спрайт:

```csharp
// В DTO генератора:
public string equippedSpritePath; // Путь к Equipped-спрайту

// При создании SO:
if (!string.IsNullOrEmpty(dto.equippedSpritePath))
    equipmentData.equippedSprite = LoadSprite(dto.equippedSpritePath);
```

---

## 📊 Полный перечень Equipped-спрайтов

### Приоритет 1 — Торс и Голова (самое заметное)

| # | Файл | Слот | Описание | Промпт для AI |
|---|------|------|----------|---------------|
| E01 | `eq_robe_cloth.png` | Torso | Тканевая роба | `character equipment overlay, cloth robe worn on torso, xianxia, transparent bg, centered` |
| E02 | `eq_robe_spirit.png` | Torso | Духовная роба | `character equipment overlay, glowing spirit robe, xianxia, transparent bg, centered` |
| E03 | `eq_vest_leather.png` | Torso | Кожаный жилет | `character equipment overlay, leather vest armor, xianxia, transparent bg, centered` |
| E04 | `eq_chainmail.png` | Torso | Кольчуга | `character equipment overlay, chainmail torso armor, xianxia, transparent bg, centered` |
| E05 | `eq_torso_iron.png` | Torso | Железный нагрудник | `character equipment overlay, iron plate torso armor, xianxia, transparent bg, centered` |
| E06 | `eq_full_plate.png` | Torso | Полные латы | `character equipment overlay, full plate armor torso, xianxia, transparent bg, centered` |
| E07 | `eq_helmet_iron.png` | Head | Железный шлем | `character equipment overlay, iron helmet on head, xianxia, transparent bg, centered` |
| E08 | `eq_helmet_medium.png` | Head | Кольчужный шлем | `character equipment overlay, medium chain helmet, xianxia, transparent bg, centered` |
| E09 | `eq_hood_cloth.png` | Head | Тканевый капюшон | `character equipment overlay, cloth hood on head, xianxia, transparent bg, centered` |

### Приоритет 2 — Оружие и Ноги

| # | Файл | Слот | Описание |
|---|------|------|----------|
| E10 | `eq_sword_iron.png` | WeaponMain | Железный меч в руке |
| E11 | `eq_axe_iron.png` | WeaponMain | Железный топор в руке |
| E12 | `eq_staff_wood.png` | WeaponMain | Деревянный посох в руке |
| E13 | `eq_bow_wood.png` | WeaponMain | Деревянный лук в руке |
| E14 | `eq_spear_iron.png` | WeaponMain | Железное копьё |
| E15 | `eq_boots_cloth.png` | Feet | Тканевые тапки |
| E16 | `eq_boots_leather.png` | Feet | Кожаные сапоги |
| E17 | `eq_boots_chain.png` | Feet | Кольчужные сапоги |
| E18 | `eq_sabatons_plate.png` | Feet | Латные сабатоны |

### Приоритет 3 — Ноги, Руки, Пояс

| # | Файл | Слот | Описание |
|---|------|------|----------|
| E19 | `eq_pants_cloth.png` | Legs | Тканевые штаны |
| E20 | `eq_pants_leather.png` | Legs | Кожаные штаны |
| E21 | `eq_greaves_iron.png` | Legs | Железные поножи |
| E22 | `eq_gloves_leather.png` | Hands | Кожаные перчатки |
| E23 | `eq_gloves_chain.png` | Hands | Кольчужные перчатки |
| E24 | `eq_gauntlets_plate.png` | Hands | Латные рукавицы |
| E25 | `eq_belt_leather.png` | Belt | Кожаный пояс |

### Приоритет 4 — Остальное оружие и аксессуары

| # | Файл | Слот | Описание |
|---|------|------|----------|
| E26 | `eq_sword_spirit.png` | WeaponMain | Духовный меч |
| E27 | `eq_dagger_iron.png` | WeaponMain/Off | Железный кинжал |
| E28 | `eq_greatsword_iron.png` | WeaponMain | Двуручный меч |
| E29 | `eq_hammer_iron.png` | WeaponMain | Боевой молот |
| E30 | `eq_mace_iron.png` | WeaponMain | Булава |
| E31 | `eq_wand_wood.png` | WeaponMain | Деревянный жезл |
| E32 | `eq_staff_jade.png` | WeaponMain | Нефритовый посох |
| E33 | `eq_crossbow_iron.png` | WeaponMain | Железный арбалет |
| E34 | `eq_claws.png` | WeaponMain | Когти |
| E35 | `eq_bracers_cloth.png` | Arms* | Тканевые наручи |
| E36 | `eq_bracers_chain.png` | Arms* | Кольчужные наручи |
| E37 | `eq_bracers_plate.png` | Arms* | Латные наручи |
| E38 | `eq_ring_bronze.png` | — | Бронзовое кольцо (не отображается на персонаже) |
| E39 | `eq_ring_silver.png` | — | Серебряное кольцо |
| E40 | `eq_ring_jade.png` | — | Нефритовое кольцо |
| E41 | `eq_amulet_jade.png` | — | Нефритовый амулет |
| E42 | `eq_vest_spirit.png` | Torso | Духовный жилет |
| E43 | `eq_full_robe.png` | Torso | Полная роба |
| E44 | `eq_full_chain.png` | Torso | Полная кольчуга |
| E45 | `eq_fists.png` | WeaponMain | Кулаки |

> *Слот Arms — скрытый в текущей модели EquipmentController (Hands). Наручи отображаются только если Arms станет видимым слотом, либо объединяются с Hands.

> **Кольца и амулеты** — маленькие аксессуары, плохо видимые на персонаже. Можно реализовать как эффект свечения (particle) вместо спрайта, либо пропустить на первом этапе.

---

## 🔄 Порядок реализации

### Этап 1: Подготовка данных (1-2 часа)

1. **Добавить `equippedSprite`** в `EquipmentData.cs`
2. **Создать `EquipmentVisualController.cs`** — компонент наложения слоёв
3. **Обновить `CharacterSpriteController.cs`** — интеграция flip с экипировкой
4. **Обновить `PlayerVisual.cs`** — передача ссылки на visualRoot
5. **Протестировать** с одним placeholder-спрайтом (программный красный квадрат)

### Этап 2: Генерация Equipped-спрайтов (2-3 часа)

1. **Приоритет 1** — 9 спрайтов (Torso + Head)
2. **Приоритет 2** — 9 спрайтов (Weapon + Feet)
3. **Приоритет 3** — 7 спрайтов (Legs + Hands + Belt)
4. **Приоритет 4** — 20 спрайтов (остальное)

### Этап 3: Импорт и назначение (1 час)

1. **Импорт** в Unity с настройками:
   - Texture Type: Sprite (2D and UI)
   - Sprite Mode: Single
   - PPU: 64
   - Filter Mode: Bilinear
   - Alpha Is Transparency: ✅ true
   - Compression: None (для чёткости)
2. **Назначить** `equippedSprite` в каждом EquipmentData SO
3. **Обновить генераторы** — автоназначение `equippedSprite` по пути

### Этап 4: Тестирование и полировка

1. Проверить наложение слоёв на персонаже
2. Проверить зеркалирование (flipX)
3. Проверить смену экипировки в реальном времени
4. Проверить корректность sorting group
5. Оптимизация: Sprite Atlas для equipped-спрайтов

---

## ⚠️ Риски и ограничения

### 1. Несовпадение размеров Equipped и базового спрайта

**Проблема:** Если базовый спрайт персонажа (128×128, PPU=64) и equipped-спрайт (256×256, PPU=64) имеют разный масштаб, слои не совпадут.

**Решение:** Оба спрайта должны использовать одинаковый PPU=64. Equipped-спрайт — 256×256, но прозрачные области снаружи. Видимая часть оборудования совпадает с позицией на персонаже.

### 2. Зеркалирование (flipX)

**Проблема:** При зеркалировании персонажа (scaleX = -1 или flipX = true), все equipment-слои должны зеркалиться одновременно.

**Решение:** 
- **Вариант A (рекомендуется):** EquipmentVisualController.SetFlipX() — вызывается из CharacterSpriteController
- **Вариант B:** EquipmentVisual как дочерний объект Visual — автоматически масштабируется с родителем

### 3. Скрытые слоты (Arms, Ring, Amulet)

**Проблема:** Наручи, кольца и амулеты — скрытые слоты в EquipmentController. Equipped-спрайты для них не будут отображаться, т.к. SpriteRenderer не создаётся для скрытых слотов.

**Решение:**
- **Наручи:** Добавить как подслот Hands (отображать вместе с перчатками) или сделать Arms видимым слотом
- **Кольца/Амулеты:** Пропустить на первом этапе (слишком малы для видимого отображения). Реализовать через свечение/эффект

### 6. Упрощённая модель — отказ от слоёв экипировки

**Решение принято:** 2026-04-29

Система слоёв экипировки (Матрёшка v1 — `layers: List<EquipmentLayer>`) была **упразднена** в EquipmentController v2.0. Принцип:

> **1 слот = 1 предмет = 1 equipped-спрайт**

Это означает:
- **Нельзя** надеть робу + броню одновременно на Torso — только что-то одно
- **Нельзя** надеть шапку + шлем на Head — только что-то одно
- Каждый видимый слот имеет **ровно один** SpriteRenderer для экипировки

**Причины отказа от слоёв:**
1. Упрощение логики EquipmentController (баг EQP-BUG-03: SwapSlots ломал currentLayer)
2. Слои не использовались реально — были заявлены, но не работали
3. Для xianxia-эстетики достаточно одной вещи на слот (роба или броня, не обе)
4. Overlay-подход проще в реализации и не требует восстановления `layers` в EquipmentData

> **Примечание:** Архитектура генерации «Матрёшка» (База × Грейд × Специализация) — **сохранена**, это отдельная концепция, не связанная со слоями экипировки.

### 4. Производительность

**Проблема:** 7+ дополнительных SpriteRenderer на персонажа + дополнительный draw call на каждый.

**Решение:**
- Sprite Atlas — объединить equipped-спрайты в атлас (уменьшает draw calls)
- Отключать SpriteRenderer.enabled когда спрайт null (уже в коде)
- SortingGroup — один проход рендеринга для всей группы

### 5. NPC с экипировкой

**Проблема:** NPC тоже могут иметь экипировку, но EquipmentVisualController привязан к Player.

**Решение:** Компонент не зависит от PlayerController — может быть добавлен к любому объекту с EquipmentController.

---

## 🧪 Промпты для AI-генерации

### Общий стиль (для всех equipped-спрайтов)

```
Fantasy RPG character equipment overlay sprite, xianxia cultivation game,
transparent background PNG, character silhouette overlay, 2D game asset,
no text, centered composition, equipment layer for 2D character,
dark outline, vibrant but muted colors, Chinese martial arts aesthetic
```

### Специфичные промпты

#### Torso — Роба (eq_robe_cloth.png)
```
Character equipment overlay, cloth robe worn on torso area only,
flowing fabric with wide sleeves, xianxia cultivation game style,
transparent background, centered on character body area,
2D game sprite overlay, no text, dark outline
```

#### Torso — Латы (eq_full_plate.png)
```
Character equipment overlay, full plate armor worn on torso,
ornate Chinese-style plate armor with shoulder guards,
xianxia cultivation game style, transparent background,
centered on character body area, 2D game sprite overlay, no text
```

#### Head — Шлем (eq_helmet_iron.png)
```
Character equipment overlay, iron helmet on head area only,
Chinese war helmet with nose guard and crest,
xianxia cultivation game style, transparent background,
centered on character head area, 2D game sprite overlay, no text
```

#### WeaponMain — Меч (eq_sword_iron.png)
```
Character equipment overlay, iron sword held in right hand,
single-handed jian sword, xianxia cultivation game style,
transparent background, weapon position on character,
2D game sprite overlay, no text, dark outline
```

#### Feet — Сапоги (eq_boots_leather.png)
```
Character equipment overlay, leather boots on feet area only,
Chinese martial arts boots, xianxia cultivation game style,
transparent background, centered on character feet area,
2D game sprite overlay, no text, dark outline
```

---

## 📎 Приложение А: Альтернативные подходы

### А1. Полная замена спрайта (Sprite Swap)

**Идея:** Вместо overlay-слоёв, полностью заменять спрайт персонажа на комбинированный (база + экипировка в одном PNG).

| Плюсы | Минусы |
|-------|--------|
| 1 draw call | Экспоненциальный рост комбинаций (7 слотов × N вариантов) |
| Нет проблем со слоями | Невозможно реализовать без генерации N! спрайтов |
| Простая реализация | Невозможно менять экипировку динамически |

**Вердикт:** ❌ Не подходит — слишком много комбинаций.

### А2. Анимация спрайтов (Sprite Sheet)

**Идея:** Каждый предмет экипировки имеет набор кадров анимации, которые комбинируются с анимацией персонажа.

| Плюсы | Минусы |
|-------|--------|
| Поддержка анимации | Требует анимации для каждого предмета × каждого кадра |
| Качественное отображение | Сложная синхронизация слоёв |

**Вердикт:** ❌ Слишком сложно для текущего этапа. Реализовать после базовой overlay-системы.

### А3. Матрёшка v2 (восстановление системы слоёв) — ❌ ОТКЛОНЕНО

**Идея:** Вернуть `List<EquipmentLayer>` в EquipmentData, как в легаси-версии.

| Плюсы | Минусы |
|-------|--------|
| Гибкость слоёв | Убрано по дизайну — «1 предмет на слот» |
| Поддержка перекрытия | Противоречит v2.0 EquipmentController |
| | Баг EQP-BUG-03: SwapSlots ломал currentLayer |
| | Не использовалось реально |

**Вердикт:** ❌ **ОТКЛОНЕНО** (решение 2026-04-29). Упрощённая модель принята как основная. Overlay-подход даёт визуальный результат без восстановления слоёв.

---

## 📎 Приложение Б: Техническая справка Unity 6.3

### SpriteRenderer — ключевые свойства (источник: Unity Docs)

| Свойство | Тип | Описание |
|----------|-----|----------|
| `sprite` | Sprite | Текущий спрайт для отображения |
| `sortingLayerName` | string | Имя sorting layer |
| `sortingOrder` | int | Порядок внутри sorting layer (выше = поверх) |
| `flipX` | bool | Зеркалирование по горизонтали |
| `color` | Color | Тонирование (Color.white = нет тонирования) |
| `material` | Material | Материал (Sprite-Lit-Default / Sprite-Unlit-Default) |
| `maskInteraction` | enum | Взаимодействие с SpriteMask |

### SortingGroup — назначение

> **Unity Docs:** «Sorting Groups prevent 2D GameObjects from mixing in sorting layers and sublayers.»

Используется для того, чтобы все дочерние SpriteRenderer (персонаж + экипировка) рендерились как единая группа, без интерливинга с другими объектами сцены.

### Sprite Import Settings для Equipped-спрайтов

```
Texture Type:        Sprite (2D and UI)
Sprite Mode:         Single
Pixels Per Unit:     64
Mesh Type:           Full Rect
Extrude Edges:       1
Pivot:               Center (0.5, 0.25) — совпадает с персонажем
Filter Mode:         Bilinear
Alpha Is Transparency: ✅ true  ← КРИТИЧНО! Без этого белый фон
Compression:         None       ← Для чёткости overlay
```

---

*Черновик создан: 2026-04-29*  
*Автор: AI-ассистент*  
*Ожидает ревью и согласования перед реализацией*
