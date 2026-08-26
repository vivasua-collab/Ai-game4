# Аудит: Сложность добавления флагов для Духовного хранилища и Колец хранения

**Создано:** 2026-04-18 18:31:36 UTC
**Редактировано:** 2026-04-18 18:35:37 UTC
**Версия:** 2.0
**Статус:** ✅ Аудит завершён (v2.0 — с учётом пересоздания Assets)

**Ключевое условие:** На данном этапе папка `Assets/` удаляется и пересоздаётся генератором (Phase09). Нет существующих .asset файлов, нет save-данных, нет проблем совместимости. Это кардинально снижает риски.

**Связанные документы:**
- `docs_temp/INVENTORY_UI_DRAFT.md` — Драфт v2.0 инвентаря
- `docs/EQUIPMENT_SYSTEM.md` — Система экипировки
- `docs/INVENTORY_SYSTEM.md` — Устаревший драфт инвентаря

---

## 1. Цель аудита

Оценить сложность добавления новых полей (`volume`, `allowNesting`) в систему предметов для поддержки:
- **Духовного хранилища** (межмировая складка, каталогизатор, Qi×weight)
- **Колец хранения** (объём-ограниченные, Qi×volume)

---

## 2. Текущее состояние моделей данных

### 2.1 ItemData.cs (ScriptableObject)

| Поле | Тип | Описание |
|------|-----|----------|
| itemId | string | Уникальный ID |
| nameRu / nameEn | string | Названия |
| description | string | Описание |
| category | ItemCategory | Категория |
| itemType | string | Детальный тип |
| rarity | ItemRarity | Редкость |
| icon | Sprite | Иконка |
| stackable | bool | Можно стакать |
| maxStack | int | Максимум в стеке |
| sizeWidth | int (1-2) | Ширина в сетке |
| sizeHeight | int (1-3) | Высота в сетке |
| weight | float | Вес (кг) |
| value | int | Стоимость |
| hasDurability | bool | Имеет прочность |
| maxDurability | int | Макс. прочность |
| effects | List<ItemEffect> | Эффекты |
| requiredCultivationLevel | int | Требование культивации |
| statRequirements | List<StatRequirement> | Требования статов |

**❌ НЕТ**: `volume`, `allowNesting`

### 2.2 EquipmentData.cs (наследник ItemData)

Дополнительные поля: slot, layers, damage, defense, coverage, damageReduction, dodgeBonus, materialId, materialTier, grade, itemLevel, statBonuses, specialEffects

**❌ НЕТ**: BackpackData, StorageRingData как подклассы

### 2.3 MaterialData.cs (наследник ItemData)

Дополнительные поля: tier, materialCategory, hardness, durability, conductivity, damageBonus, defenseBonus, qiConductivityBonus, source, dropChance, requiredLevel

### 2.4 EquipmentSlot enum (Enums.cs)

```
None, WeaponMain, WeaponOff, Armor, Clothing, Charger, RingLeft, RingRight, Accessory, Backpack
```

**⚠️ НЕ СООТВЕТСТВУЕТ драфту v2.0**:
- Нет: head, torso, belt, legs, feet
- Только 2 кольца вместо 4
- Armor/Clothing — раздельные (матрёшка), а не один слот на зону

---

## 3. Что нужно добавить — поуровневая сложность

> **v2.0 NOTE**: Поскольку Assets пересоздаются генератором, риски совместимости = 0.
> EquipmentSlot enum можно переписать полностью, без боязни сломать сериализацию.

### 3.1 🟢 ПРОСТО (1-2 часа, нулевой риск)

| Изменение | Файл | Риск | Пояснение |
|-----------|------|------|-----------|
| Добавить `volume` (float, default=1.0) в ItemData | ItemData.cs | Нулевой | Assets пересоздаются — нечего ломать |
| Создать enum `NestingFlag` | Enums.cs | Нулевой | Чисто добавление нового enum |
| Добавить `allowNesting` (NestingFlag, default=Any) в ItemData | ItemData.cs | Нулевой | Новое поле с default |

**NestingFlag enum:**
```csharp
public enum NestingFlag
{
    None,       // Нельзя поместить ни в какое хранилище
    Spirit,     // Можно ТОЛЬКО в духовное хранилище
    Ring,       // Можно ТОЛЬКО в кольцо хранения
    Any         // Можно в любое хранилище
}
```

**Default-значения:**
- `volume = 1.0` — средний предмет (расходники/материалы уточним позже)
- `allowNesting = NestingFlag.Any` — по умолчанию можно куда угодно

**Таблица volume по категориям (из драфта §4):**

| Категория | Объём | Примечание |
|-----------|-------|------------|
| Расходники (пилюли, свитки) | 0.1 | Очень маленькие |
| Кольца, амулеты | 0.1-0.3 | Маленькие |
| Еда, травы | 0.5 | Средние |
| Ингредиенты, материалы | 0.5-1 | Зависит от типа |
| Лёгкое оружие (кинжалы) | 1 | Одноручное малое |
| Среднее оружие (мечи) | 2 | Одноручное |
| Броня (перчатки, сапоги) | 1-2 | Средняя |
| Тяжёлое оружие (двуручное) | 3-4 | Большое |
| Тяжёлая броня (нагрудник) | 3-4 | Большая |
| Рюкзак | 2-5 | Зависит от размера |
| Кольцо хранения (как предмет) | 0.3 | Само кольцо |

### 3.2 🟢 ПРОСТО — EquipmentSlot enum (ранее 🟡 СРЕДНЕ)

> **v2.0 CHANGE**: Поскольку Assets пересоздаются, EquipmentSlot можно переписать полностью. Риск снижен с «СРЕДНИЙ» до «НУЛЕВОЙ».

| Изменение | Файл | Риск | Пояснение |
|-----------|------|------|-----------|
| Переписать EquipmentSlot enum | Enums.cs | **Нулевой** | Assets пересоздаются, нечего ломать |
| Создать BackpackData : ItemData | BackpackData.cs (новый) | Нулевой | Новый файл + [CreateAssetMenu] |
| Создать StorageRingData : ItemData | StorageRingData.cs (новый) | Нулевой | Аналогично |
| Обновить AssetGeneratorExtended | AssetGeneratorExtended.cs | Низкий | Добавить генерацию новых типов |

**Новый EquipmentSlot enum (по драфту v2.0):**

```csharp
/// <summary>
/// Слот экипировки (переработан по INVENTORY_UI_DRAFT.md v2.0)
/// 
/// Видимые слоты куклы (7): Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
/// Скрытые слоты (заглушки на будущее): Amulet, RingLeft1, RingLeft2, 
///   RingRight1, RingRight2, Charger, Hands, Back
/// </summary>
public enum EquipmentSlot
{
    None,
    
    // === Видимые слоты куклы ===
    Head,           // Голова — шлем, шапка, корона
    Torso,          // Торс — нагрудник, рубашка, роба
    Belt,           // Пояс — ремень, пояс зелий, зарядник-пояс
    Legs,           // Ноги — поножи, штаны
    Feet,           // Ступни — сабатоны, сапоги
    WeaponMain,     // Основная рука — одноручное или щит
    WeaponOff,      // Вторичная рука — одноручное, щит или инструмент
    
    // === Скрытые слоты (заглушки) ===
    Amulet,         // Амулет — будет с ювелирной системой
    RingLeft1,      // Кольцо левое 1 — будет с системой хранения
    RingLeft2,      // Кольцо левое 2
    RingRight1,     // Кольцо правое 1
    RingRight2,     // Кольцо правое 2
    Charger,        // Зарядник Ци — будет с системой зарядников
    Hands,          // Перчатки — будет с расширением экипировки
    Back            // Плащ/спина — будет с расширением экипировки
}
```

**Почему полная замена безопасна:**
- Assets пересоздаются из JSON при каждой сборке (Phase09)
- Нет сохранённых .asset файлов с int-индексами старого enum
- EquipmentController.cs, EquipmentData.cs — будут переписаны в рамках полной переделки
- ItemCategory enum остаётся без изменений

**Удалены из старого enum:**
- `Armor` — заменён на `Head/Torso/Legs/Feet`
- `Clothing` — объединено с бронёй (один слот на зону)
- `Accessory` — заменён на `Amulet`
- `Backpack` — рюкзак НЕ слот экипировки (по драфту v2.0 — отдельная система)
- `RingLeft / RingRight` — заменены на 4 слота (RingLeft1/2, RingRight1/2)

**BackpackData (новый ScriptableObject):**
```csharp
[CreateAssetMenu(fileName = "Backpack", menuName = "Cultivation/Backpack")]
public class BackpackData : ItemData
{
    [Header("Backpack")]
    public int gridWidth = 3;           // ширина сетки
    public int gridHeight = 4;          // высота сетки
    public float weightReduction = 0f;  // % снижения веса
    public float maxWeightBonus = 0f;   // бонус к максимальному весу (кг)
    public int beltSlots = 0;           // дополнительные слоты пояса (0-4)
}
```

**StorageRingData (новый ScriptableObject):**
```csharp
[CreateAssetMenu(fileName = "StorageRing", menuName = "Cultivation/Storage Ring")]
public class StorageRingData : ItemData
{
    [Header("Storage Ring")]
    public float maxVolume = 5f;        // максимальный объём хранения
    public int qiCostBase = 5;          // базовая стоимость Qi
    public float qiCostPerUnit = 2f;    // стоимость Qi за единицу объёма
    public float accessTime = 1.5f;     // время доступа (сек)
}
```

**Урок из прошлого**: EquipmentData и MaterialData были вынесены из ItemData.cs в отдельные файлы (commit 2026-04-13), потому что Unity требует совпадение имени файла и класса для ScriptableObject с `[CreateAssetMenu]`. Каждый новый подкласс — **свой файл**.

### 3.3 🔴 СЛОЖНО (8-15 часов, новые системы)

> Без изменений — контроллеры и UI — это новый код, риски от пересоздания Assets не влияют.

| Изменение | Файл | Риск | Пояснение |
|-----------|------|------|-----------|
| SpiritStorageController | Новый файл | Низкий | Чисто новый код |
| StorageRingController | Новый файл | Низкий | Чисто новый код |
| Каталогизатор SpiritStorage | UI + backend | Высокий | Новый UI-компонент |
| Валидация вложений (allowNesting) | В Spirit/StorageRing | Средний | Логика проверок |
| Стоимость Qi для хранилищ | Интеграция с QiController | Средний | Зависимость от Qi |

**SpiritStorageController API (из драфта §7.3):**
- StoreItem(itemId, count) — переместить в духовное хранилище (Qi × weight)
- RetrieveItem(entryId, count) — извлечь (Qi × weight)
- GetContents() — каталог
- FilterByCategory(), FilterByRarity(), Search()
- GetStorageCost(weight), GetRetrievalCost(weight)

**StorageRingController API (из драфта §7.4):**
- StoreItem(itemId, count) — в кольцо (Qi × volume)
- RetrieveItem(entryId, count) — из кольца (Qi × volume)
- GetContents()
- GetCurrentVolume() / GetMaxVolume()
- GetStorageCost(volume)
- CanStore(itemId) — проверка allowNesting + объём

**Ключевая зависимость — QiController:**
Оба контроллера списывают Qi. Нужно проверить API QiController:

```csharp
// Текущий API QiController (из EquipmentController.cs)
var qiCtrl = ServiceLocator.GetOrFind<QiController>();
qiCtrl.CultivationLevel  // уровень культивации
```

Нужны дополнительные методы:
- `qiCtrl.SpendQi(long amount)` — списать Qi
- `qiCtrl.CurrentQi` — текущее количество Qi

---

## 4. Обратная совместимость Save Data

> **v2.0 NOTE**: На данном этапе нет save-данных. Вся секция неактуальна до появления реальных сохранений.

| Формат | Влияние | Действие |
|--------|---------|----------|
| InventorySlotSaveData | ✅ Без изменений | volume/allowNesting берутся из ItemData, не из сейва |
| EquipmentSaveData | ⚠️ Изменится | Новые слоты enum — нужен апгрейд сейва |
| CraftingSaveData | ✅ Без изменений | |
| **Новый** SpiritStorageSaveData | 🆕 Новый формат | Нужно создать |
| **Новый** StorageRingSaveData | 🆕 Новый формат | Нужно создать |

**Вывод**: Пока нет реальных сохранений — нет проблем. К моменту появления save-системы формат устаканится.

---

## 5. Проблема EquipmentSlot — РЕШЕНА

> **v2.0 CHANGE**: Поскольку Assets пересоздаются, выбран **Вариант C — полная замена EquipmentSlot**.

### 5.1 Решение

~~Проблема: Текущий EquipmentSlot основан на «матрёшке» (Armor + Clothing — раздельные слоты). Драфт v2.0 упрощает до 7 видимых слотов.~~

**Решение**: EquipmentSlot переписывается полностью по драфту v2.0. Старый enum удаляется. Все файлы, ссылающиеся на EquipmentSlot, обновляются одновременно.

**Почему это безопасно сейчас:**
1. Assets пересоздаются из JSON — нет .asset файлов со старыми int-значениями
2. InventoryController + EquipmentController — и так подлежат полной переделке
3. CraftingController ссылается на EquipmentGrade (не меняется), не на EquipmentSlot напрямую
4. Phase09 (AssetGeneratorExtended) генерирует EquipmentData с slot-полем — обновляется вместе с enum

### 5.2 Влияние на существующие файлы

| Файл | Что менять | Сложность |
|------|-----------|-----------|
| Enums.cs | Переписать EquipmentSlot | 🟢 Просто |
| EquipmentData.cs | slot-поле автоматически подхватит новый enum | 🟢 Ничего менять |
| EquipmentController.cs | Полная переделка (уже запланирована) | 🔴 Но не из-за enum |
| AssetGeneratorExtended.cs | Обновить генерацию EquipmentData | 🟡 Средне |
| InventorySlotSaveData | Нет ссылки на EquipmentSlot | 🟢 Ничего |
| EquipmentSaveData | Ссылка на EquipmentSlot — обновится | 🟢 Автоматически |

---

## 6. Итоговая оценка сложности (v2.0 — с пересозданием Assets)

| Этап | Сложность | Время | Зависимости |
|------|-----------|-------|-------------|
| Добавить volume + allowNesting в ItemData | 🟢 Простая | 1-2 ч | Нет |
| Переписать EquipmentSlot enum | 🟢 Простая | 30 мин | Нет (Assets пересоздаются) |
| Создать BackpackData + StorageRingData | 🟢 Простая | 2-3 ч | ItemData с volume |
| Обновить AssetGeneratorExtended | 🟡 Средняя | 2-3 ч | Новые SO + новый enum |
| SpiritStorageController | 🔴 Сложная | 5-8 ч | QiController |
| StorageRingController | 🔴 Сложная | 5-8 ч | QiController + volume |
| Интеграция allowNesting в UI | 🟡 Средняя | 3-4 ч | Оба контроллера |

**Итого**: 18-28 часов на полный цикл.

### Минимальный набор для «флаги работают»:
- volume + allowNesting в ItemData: **1-2 часа**
- NestingFlag enum в Enums.cs: **15 минут**
- Переписать EquipmentSlot: **30 минут**
- BackpackData + StorageRingData ScriptableObject: **2-3 часа**

**Итого минимум**: 4-6 часов, чтобы данные были готовы для контроллеров.

---

## 7. Рекомендуемый порядок внедрения

1. ✅ Добавить `NestingFlag` enum в Enums.cs
2. ✅ Переписать `EquipmentSlot` enum по драфту v2.0
3. ✅ Добавить `volume` + `allowNesting` в ItemData.cs (с defaults)
4. ✅ Создать BackpackData.cs (наследник ItemData)
5. ✅ Создать StorageRingData.cs (наследник ItemData)
6. 🔜 Обновить AssetGeneratorExtended для новых SO и enum
7. 🔜 Написать SpiritStorageController
8. 🔜 Написать StorageRingController
9. 🔜 UI-интеграция: контекстное меню, валидация, стоимость Qi

---

## 8. Изменения v2.0 относительно v1.0

| Пункт | v1.0 | v2.0 | Причина |
|-------|------|------|---------|
| EquipmentSlot | 🟡 СРЕДНЕ (риск сериализации) | 🟢 НУЛЕВОЙ | Assets пересоздаются |
| Рекомендация EquipmentSlot | Вариант B (DollSlot) | Вариант C (полная замена) | Нет .asset для ломания |
| Обратная совместимость Save | Актуальна | Не актуальна | Нет save-данных |
| Итого минимум | 3-5 ч | 4-6 ч | Включена переработка enum |
| Итого полный цикл | 19-30 ч | 18-28 ч | Упрощение enum |

---

*Документ создан: 2026-04-18 18:31:36 UTC*
*Редактировано: 2026-04-18 18:35:37 UTC*
*Статус: Аудит завершён (v2.0)*
