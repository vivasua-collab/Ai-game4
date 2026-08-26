# Система инвентаря (Inventory System)

**Версия:** 1.1  
**Создано:** 2025-02-28  
**Статус:** Активно

---

## 📋 Обзор

Система инвентаря обеспечивает хранение, управление и отображение предметов персонажа. Включает:

1. **Инвентарь (Inventory)** - 7x7 сетка для переносимых предметов
2. **Экипировка (Equipment)** - слоты на теле персонажа
3. **Духовное хранилище (Spirit Storage)** - вне-пространственное хранилище
4. **Кукла тела (Body Doll)** - визуальное отображение экипировки и состояния

---

## 🏗️ Архитектура

### Компоненты

```
┌─────────────────────────────────────────────────────────────────┐
│                    ИНВЕНТАРЬ (React + Phaser)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                  InventoryDialog (React)                  │   │
│  │                                                           │   │
│  │   ┌────────────┐   ┌────────────┐   ┌────────────────┐   │   │
│  │   │  BodyDoll  │   │  7x7 Grid  │   │ Storage Panel  │   │   │
│  │   │  (216x384) │   │  (49 слот) │   │   (список)     │   │   │
│  │   │   +20%     │   │            │   │   ↓ клик       │   │   │
│  │   └────────────┘   └────────────┘   └────────────────┘   │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              │ API                               │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │               InventoryService (Backend)                  │   │
│  │                                                           │   │
│  │   getInventoryState() → InventoryState                    │   │
│  │   getCharacterEquipment() → Map<SlotId, Item>             │   │
│  │   getSpiritStorage() → SpiritStorageState                 │   │
│  │   equipItem() / unequipItem() → void                      │   │
│  │   moveItem() → void                                       │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    Prisma + SQLite                        │   │
│  │                                                           │   │
│  │   InventoryItem │ Equipment │ SpiritStorage │ Character  │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📦 Модели данных

### InventoryItem

```prisma
model InventoryItem {
  id          String   @id @default(cuid())
  characterId String
  
  // Идентификация
  name        String
  nameId      String
  description String
  type        String   // weapon, armor, consumable, material_qi_stone, ...
  category    String   // weapon, armor, consumable, material, ...
  rarity      String   // common, uncommon, rare, epic, legendary, mythic
  icon        String   // Эмодзи или путь
  
  // Количество
  quantity    Int      @default(1)
  maxStack    Int      @default(1)
  stackable   Boolean  @default(false)
  
  // Размер
  sizeWidth   Int      @default(1)
  sizeHeight  Int      @default(1)
  weight      Float    @default(0.0)
  
  // Позиция
  posX        Int?
  posY        Int?
  location    String   @default("inventory") // inventory, equipment, storage
  
  // Экипировка
  equipmentSlot String?
  isEquipped    Boolean @default(false)
  
  // Дополнительно
  stats       String?  // JSON
  effects     String?  // JSON
  value       Int      @default(0)
  currency    String   @default("spirit_stones")
}
```

### Equipment

```prisma
model Equipment {
  id          String   @id @default(cuid())
  characterId String
  slotId      String   // head, torso, left_hand, right_hand, ...
  itemId      String   @unique
  equippedAt  DateTime @default(now())
  
  @@unique([characterId, slotId])
}
```

### SpiritStorage

```prisma
model SpiritStorage {
  id           String   @id @default(cuid())
  characterId  String   @unique
  capacity     Int      @default(20)
  unlocked     Boolean  @default(false)
  requiredLevel Int     @default(3)
  items        String   @default("[]") // JSON массив
}
```

---

## 🎮 Слоты экипировки

| Слот | ID | Описание |
|------|-----|----------|
| Голова | `head` | Шлемы, шапки, короны |
| Торс | `torso` | Броня, одежды, мантии |
| Левая рука | `left_hand` | Оружие, щиты, кольца |
| Правая рука | `right_hand` | Оружие, инструменты, кольца |
| Ноги | `legs` | Штаны, поножи |
| Ступни | `feet` | Сапоги, туфли |
| Аксессуар 1 | `accessory1` | Кольца, амулеты |
| Аксессуар 2 | `accessory2` | Кольца, амулеты |
| Спина | `back` | Плащи, крылья |
| Рюкзак | `backpack` | Сумки (+ слоты инвентаря) |

---

## 💼 Система пояса (Quick Access)

Пояс имеет до 4 слотов быстрого доступа для расходников. Это позволяет мгновенно использовать предметы в бою без открытия инвентаря.

### Горячие клавиши

| Комбинация | Действие |
|------------|----------|
| `CTRL+1` | Использовать предмет из слота 1 |
| `CTRL+2` | Использовать предмет из слота 2 |
| `CTRL+3` | Использовать предмет из слота 3 |
| `CTRL+4` | Использовать предмет из слота 4 |

### Ограничения

- В слоты быстрого доступа можно помещать только расходники (таблетки, эликсиры, еда, свитки)
- После использования предмет удаляется из слота
- Если в слоте нет предмета, горячая клавиша игнорируется

---

## 📊 Редкость предметов

| Редкость | Цвет | Множитель стоимости |
|----------|------|---------------------|
| Common | `#6b7280` (серый) | 1x |
| Uncommon | `#22c55e` (зелёный) | 2x |
| Rare | `#3b82f6` (синий) | 5x |
| Epic | `#a855f7` (фиолетовый) | 15x |
| Legendary | `#fbbf24` (золотой) | 50x |
| Mythic | `#ef4444` (красный) | 200x |

---

## 🔄 API Endpoints

### GET /api/inventory/state

Получить полное состояние инвентаря.

**Query:**
- `characterId` - ID персонажа

**Response:**
```json
{
  "success": true,
  "inventory": {
    "characterId": "xxx",
    "baseWidth": 7,
    "baseHeight": 7,
    "slots": [/* 49 элементов */],
    "currentWeight": 12.5,
    "maxWeight": 50,
    "usedSlots": 8,
    "totalSlots": 49
  },
  "equipment": {
    "head": {/* item */},
    "torso": null
  },
  "storage": {
    "unlocked": true,
    "capacity": 20,
    "slots": [/* предметы */]
  },
  "items": [/* все предметы */]
}
```

### POST /api/inventory/equip

Экипировать предмет.

**Body:**
```json
{
  "characterId": "xxx",
  "itemId": "yyy",
  "slotId": "head"
}
```

### POST /api/inventory/move

Переместить предмет.

**Body:**
```json
{
  "characterId": "xxx",
  "itemId": "yyy",
  "toX": 3,
  "toY": 2,
  "toLocation": "inventory"
}
```

### POST /api/inventory/add-qi-stone

Добавить камень Ци.

**Body:**
```json
{
  "characterId": "xxx",
  "quality": "stone",
  "quantity": 5
}
```

---

## 🧪 Типы предметов

### Камни Ци (Qi Stones)

См. [qi_stone.md](./qi_stone.md) для полной документации.

| Тип | Ци | Редкость |
|-----|-----|----------|
| Осколок | 1,000 | common |
| Фрагмент | 10,000 | uncommon |
| Камень | 100,000 | rare |
| Кристалл | 1,000,000 | epic |
| Сердце | 10,000,000 | legendary |
| Ядро | 100,000,000 | mythic |

---

## 🔧 Устранение неполадок

### "Failed to fetch inventory state"

**Причина:** NULL значения в обязательных полях БД (icon, category, rarity).

**Решение:**
```bash
bun run db:push -- --force-reset
```

**Предотвращение:**
- Всегда указывайте `icon`, `category`, `rarity` при создании предметов
- Используйте валидацию Zod в API

### Предметы не отображаются

**Проверить:**
1. `characterId` передан правильно
2. Персонаж существует в БД
3. API возвращает `success: true`

---

## 📝 Примеры использования

### Создание предмета через сервис

```typescript
import { inventoryService } from '@/services/inventory.service';

const item = await inventoryService.addItemToInventory(characterId, {
  name: 'Меч рассвета',
  nameId: 'sword_dawn_001',
  description: 'Древний меч...',
  type: 'weapon',
  category: 'weapon',
  rarity: 'rare',
  icon: '🗡️',
  quantity: 1,
  weight: 2.5,
  value: 500,
  stats: JSON.stringify({ damage: 45, speed: 1.2 }),
});
```

### Добавление камня Ци

```typescript
import { createQiStoneItem } from '@/types/qi-stones';

const qiStone = createQiStoneItem('crystal', 3);
// Создаёт 3 кристалла Ци (по 1,000,000 Ци каждый)
```

---

## 📚 Связанные документы

- [qi_stone.md](./qi_stone.md) - Камни Ци
- [equip.md](./equip.md) - Система экипировки
- [body.md](./body.md) - Концепция тела
- [../checkpoint28-inventar.md](../checkpoint28-inventar.md) - Чеклист разработки
