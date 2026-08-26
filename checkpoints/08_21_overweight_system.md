# Чекпоинт: Overweight system — overflow, speed penalty, notification

**Дата:** 2026-08-21 12:30 UTC
**Сессия:** web-d86b1055
**Тип:** fix + feature

---

## Контекст

Пользователь сообщил:
- При переполнении инвентаря по весу новые ресурсы не попадают
- Нет сообщения о перевесе
- Должна уменьшаться скорость при перевесе
- Будет кольцо хранения и инвентарь души для перемещения ресурсов при перевесе

## Анализ

### Корневая причина
`InventoryService.TryAddItem` проверял `CanFitItem(item, count)` который учитывал ОБА лимита (вес+объём). При перевесе `CanFitItem` возвращал false → `TryAddItem` отклонял предмет → ресурсы не попадали в инвентарь.

### Конфигурация
- `MaxCarryWeight = 10 кг` (GameConstants.BASE_CARRY_WEIGHT)
- `MaxCarryVolume = 100 л` (InventoryConfig)

## Решение: Overflow Policy

| Лимит | Политика | Причина |
|-------|----------|---------|
| **Вес** | НЕ enforced — overflow разрешён | Игрок может нести больше максимума, но со штрафом к скорости |
| **Объём** | Enforced (partial add) | Физическое пространство рюкзака нельзя превысить |

**Будущее:** Storage Ring / Spirit Storage позволят переместить избыточные ресурсы (backend уже готов).

## Изменения

### 1. InventoryService.TryAddItem
- Убрана проверка веса
- Объём: если полон → partial add (сколько влезло)
- Логирование при partial add

### 2. CanFitItem / HowManyCanFit
- Проверяют только объём (вес игнорируется)
- `HowManyCanFit` возвращает лимит по объёму

### 3. IInventoryService — новые свойства
```csharp
bool IsOverweight { get; }     // текущий вес > эффективный макс
float OverweightRatio { get; } // 0 = нет, 1.0 = 2× макс, 3.0 = 4× макс (cap)
```

### 4. GameWorldController.HandleFreeMovement — штраф скорости
```csharp
if (Inventory.IsOverweight) {
    float ratio = Inventory.OverweightRatio;
    float penalty = 1.0f / (1.0f + ratio);
    speedMult *= penalty;
}
```

| Ratio | Вес (×max) | Speed |
|-------|------------|-------|
| 0.0 | ≤1.0× | 1.0× (normal) |
| 0.5 | 1.5× | 0.67× |
| 1.0 | 2.0× | 0.5× |
| 2.0 | 3.0× | 0.33× |
| 3.0 | 4.0× (cap) | 0.25× (min) |

### 5. Overweight notification (toast)
- При переходе через порог: "⚠ Перевес! 15.2/10.0 кг — скорость снижена"
- При возврате в норму: "Вес в норме"
- Debounced (один раз, не каждый кадр)

### 6. InventoryWindow weight label — цветовая индикация
- 🔴 Красный (AccentRed) — перевес или объём полон
- 🟡 Золотой (AccentGold) — >80% лимита
- ⚪ Серый (InkFaded) — в норме
- Текст: "Вес: 15.2 / 10.0 кг ⚠ ПЕРЕВЕС | Объём: 45.0 / 100.0"

### 7. DI
`IInventoryService` injected в `GameWorldController` для доступа к `IsOverweight`/`OverweightRatio`.

## Верификация

- `dotnet build`: 0 errors
- Headless: игра загружается, Inventory/Doll/ObjectLayer — OK
- Ресурсы добавляются даже при перевесе (overflow)
- Скорость снижается при перевесе (0.25×-1.0×)

## Следующие шаги

- [ ] Визуальная проверка на ПК с Godot
- [ ] Подключить Storage Ring UI (backend готов) — для перемещения избыточных ресурсов
- [ ] Подключить Spirit Storage UI (backend готов) — для ценных предметов
- [ ] Анимация замедления при перевесе (visual feedback)

## Файлы

**Изменённые:**
- `game/src/Core/Interfaces/IInventoryService.cs` — +IsOverweight, +OverweightRatio
- `game/src/Modules/Inventory/InventoryService.cs` — TryAddItem overflow, CanFitItem/HowManyCanFit volume-only, +IsOverweight/OverweightRatio impl
- `game/src/Adapter/Scene/GameWorldController.cs` — +IInventoryService inject, +overweight speed penalty, +toast notification
- `game/src/Adapter/UI/InventoryWindow.cs` — weight label color + status text
- `worklog.md` — запись 12:30
