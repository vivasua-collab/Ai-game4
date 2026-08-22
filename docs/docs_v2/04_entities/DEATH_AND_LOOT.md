# Смерть и лут (Death and Loot)

> **Назначение:** Движко-независимая спецификация механики смерти NPC, выпадения лута, смерти игрока и респавна.
>
> **Связанные документы:** [COMBAT_SYSTEM.md](../02_systems/COMBAT_SYSTEM.md), [BODY_SYSTEM.md](../02_systems/BODY_SYSTEM.md), [GROUND_ITEM_SYSTEM.md](../06_player/GROUND_ITEM_SYSTEM.md), [EQUIPMENT_SYSTEM.md](../06_player/EQUIPMENT_SYSTEM.md), [NPC.md](./NPC.md), [ANIMALS.md](./ANIMALS.md).

---

## 1. Смерть NPC

### 1.1. Условие смерти
- NPC умирает, когда `CurrentHealth ≤ 0` (HP торса или головы падает до нуля, либо останавливается сердце — см. [BODY_SYSTEM.md §5](../02_systems/BODY_SYSTEM.md)).
- Флаг `IsAlive` устанавливается в `false`.

### 1.2. Событийный поток
```
HP ≤ 0
  → NPC.IsAlive = false
  → публикуется NPCDeathEvent (NPCId, позиция, уровень)
  → CombatLootService генерирует лут
  → GroundItemService.DropItem(loot, позиция NPC)
  → NPCSpriteRenderer удаляет спрайт NPC из сцены
```

### 1.3. Очистка
- Спрайт NPC удаляется из сцены.
- AI-компонент деактивируется.
- Если NPC был сюжетный/уникальный — помечается как «убит» в журнале (см. [JOURNAL_SYSTEM.md](../06_player/JOURNAL_SYSTEM.md)).

---

## 2. Лут

### 2.1. Генерация
- Лут генерируется `EquipmentGenerator.GenerateRandom(level)`, где `level` = уровень убитого NPC.
- Генератор использует «матрёшку» (см. [EQUIPMENT_SYSTEM.md §2](../06_player/EQUIPMENT_SYSTEM.md)): базовый класс → материал → грейд → зачарование.
- Количество предметов: 1–3 (по рандому, с весами редкости).

### 2.2. Детерминированность
- Генератор использует `ICombatRng` (seed=12345) — см. [COMBAT_SYSTEM.md §Детерминированность (Q5)](../02_systems/COMBAT_SYSTEM.md).
- Одинаковый seed + одинаковый уровень → одинаковый лут.

### 2.3. Выпадение
- Лут выпадает на землю через `GroundItemService.DropItem(item, position)`.
- Позиция = позиция трупа + random offset.
- Публикуется `ItemDroppedEvent`.

### 2.4. Подбор игроком
- Игрок подходит и нажимает **E** (радиус 1.5 тайла) — стандартный pickup.
- См. [GROUND_ITEM_SYSTEM.md §3](../06_player/GROUND_ITEM_SYSTEM.md).

---

## 3. Смерть игрока

### 3.1. Условие смерти
- Игрок умирает, когда:
  - **Сердце (Heart) Disabled** — RedHP сердца ≤ 0.
  - **Сердце Severed** — структурно отрублено (для частей с BlackHP; сердце имеет только RedHP, поэтому для него Severed = Disabled = смерть).
  - Голова (Head) Disabled/Severed.

### 3.2. Событийный поток
```
HP сердца/головы ≤ 0
  → PlayerService.Die()
  → публикуется PlayerDeathEvent (позиция, причина)
  → UI показывает экран смерти («Вы пали в бою»)
  → предлагается выбор: Revive / Load Save / Main Menu
```

### 3.3. Причина смерти
- В `PlayerDeathEvent` сохраняется причина (какая часть тела была уничтожена, какой атакующий).
- Используется в журнале и для возможных ачивок/последствий.

---

## 4. Respawn (возрождение игрока)

### 4.1. Принцип
- Игрок может возродиться (`Revive`) — HP восстанавливается до максимума, состояние тела возвращается к здоровому.

### 4.2. Что восстанавливается
- `CurrentHealth` → `MaxHealth`.
- Все части тела → `Healthy` (RedHP = max, BlackHP = max).
- Ци → максимум.
- Активные баффы/DoT — снимаются.

### 4.3. Что сохраняется
- **Позиция игрока** — остаётся той же (где игрок умер). Это сознательное решение: респавн происходит на месте смерти.
- Экипировка — остаётся на игроке (не выпадает).
- Инвентарь — остаётся.
- Уровень культивации, статы — без изменений.

### 4.4. Штраф
- Текущая реализация: штраф минимальный (только снятие активных баффов/DoT).
- В будущем: возможны штрафы к Ци/прочности экипировки.

### 4.5. Событие
- При респавне публикуется `PlayerReviveEvent`.
- Симуляция возобновляется (если была на паузе из-за экрана смерти).

> Контракты — `readonly struct`. См. [DI_AND_EVENTBUS.md §2.3](../01_architecture/DI_AND_EVENTBUS.md) (PlayerContracts: PlayerDeath, PlayerRevive).

---

## 5. Лут с животных

- Животные (wolf, deer, rabbit) при смерти тоже могут дать лут:
  - wolf → материал (шкура, клык).
  - deer → материал (рога, мясо).
  - rabbit → материал (мясо, шкурка).
- Лут выпадает через тот же `GroundItemService.DropItem` механизм.
- См. [ANIMALS.md §5](./ANIMALS.md) для retaliation-логики.

---

## 6. Сводная таблица событий

| Событие | Триггер | Реакция |
|---------|---------|---------|
| `NPCDeathEvent` | NPC HP ≤ 0 | LootService генерирует лут, GroundItemService выпадает |
| `PlayerDeathEvent` | Player Heart/Head ≤ 0 | UI экран смерти, пауза |
| `PlayerReviveEvent` | Игрок выбрал Revive | HP/body/Qi восстановлены, симуляция возобновлена |
| `ItemDroppedEvent` | Loot выпал на землю | Renderer рисует спрайт |
| `ItemPickedUpEvent` | Игрок подобрал E | → ItemAddRequestEvent → InventoryService |

---

## 7. Связанные документы

- [COMBAT_SYSTEM.md](../02_systems/COMBAT_SYSTEM.md) — формула урона, слой 10 (последствия), детерминированность.
- [BODY_SYSTEM.md](../02_systems/BODY_SYSTEM.md) — состояния частей, смерть по Heart/Head.
- [GROUND_ITEM_SYSTEM.md](../06_player/GROUND_ITEM_SYSTEM.md) — выпадение/подбор предметов.
- [EQUIPMENT_SYSTEM.md](../06_player/EQUIPMENT_SYSTEM.md) — генератор экипировки (матрёшка).
- [NPC.md](./NPC.md) — свойства NPC.
- [ANIMALS.md](./ANIMALS.md) — лут с животных.
- [JOURNAL_SYSTEM.md](../06_player/JOURNAL_SYSTEM.md) — журнал убийств.
