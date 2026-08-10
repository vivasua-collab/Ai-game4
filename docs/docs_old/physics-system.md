# 🎮 Система физики Phaser 3

**Версия Phaser:** 3.90
**Тип проекта:** Cultivation World Simulator

---

## 📋 Быстрый справочник

### Клавиши управления

| Клавиша | Действие | Примечание |
|---------|----------|------------|
| **WASD** / Стрелки | Движение | 8 направлений |
| **ЛКМ** | Атака | Снаряд по курсору |
| **P** или **`** | Debug режим | Показ хитбоксов |
| **E** / **F** | Взаимодействие | Радиус 60px до NPC |
| **1-4** | Выбор слота техники | Индексы 0-3 |
| **ESC** | Выход | Возврат в WorldScene |

### ⚠️ Клавиши F1-F12 перехватываются браузером!

| F1 | F3 | F5 | F11 | F12 |
|----|----|----|-----|-----|
| Справка | Поиск | Обновление | Fullscreen | DevTools |

---

## 🏗️ Архитектура

### Единый источник истины для позиции

```
┌────────────────────────────────────────────────────┐
│                                                    │
│   NPCSprite (Physics.Arcade.Sprite)               │
│   ├── sprite.x/y ← body.position                  │
│   └── ЕДИНСТВЕННЫЙ ИСТОЧНИК ПОЗИЦИИ               │
│                │                                   │
│                ▼                                   │
│   LocationNPC.x/y = sprite.x/y                    │
│   (только чтение, синхронизация в update())       │
│                                                    │
└────────────────────────────────────────────────────┘
```

### Способы перемещения объектов

| Метод | Физика | Коллизии | Использование |
|-------|--------|----------|---------------|
| `setPosition(x, y)` | ❌ Обходит | ❌ Игнорирует | Телепортация |
| `x += speed` | ❌ Обходит | ❌ Игнорирует | ❌ Не использовать |
| `setVelocity(vx, vy)` | ✅ Использует | ✅ Учитывает | Стандарт |
| `moveTo(x, y, speed)` | ✅ Использует | ✅ Учитывает | К точке |
| Tween анимация | ❌ Обходит | ❌ Игнорирует | UI/эффекты |

---

## 💥 Коллизии

### Настройка хитбокса

```typescript
// ВАЖНО: setCircle() требует offset для центрирования!
// __DEFAULT текстура = 32×32, origin = 0.5 → halfSize = 16

body.setCircle(
  radius,                    // Радиус
  SPRITE_HALF - radius,      // offsetX = 16 - radius
  SPRITE_HALF - radius       // offsetY = 16 - radius
);
```

### Порядок инициализации

```typescript
// 1. Создать объект (без physics.add.existing)
const npc = new NPCSprite(scene, config);

// 2. Добавить в Physics Group (создаёт тело)
group.add(npc);

// 3. Настроить тело ПОСЛЕ добавления
npc.configurePhysicsBody();
```

---

## ⛔ Антипаттерны

### ❌ setPosition вместо setVelocity

```typescript
// НЕПРАВИЛЬНО
npc.x += speed * delta;
sprite.setPosition(npc.x, npc.y);

// ПРАВИЛЬНО
sprite.setVelocity(nx * speed, ny * speed);
// или
sprite.moveTo(targetX, targetY, speed);
```

### ❌ Tween для игрового движения

```typescript
// НЕПРАВИЛЬНО — Tween обходит физику
this.tweens.add({ targets: sprite, x: targetX, y: targetY });

// ПРАВИЛЬНО — использовать физику
sprite.moveTo(targetX, targetY, speed);
```

---

## 🔧 Диагностика

### Включение debug режима

```typescript
// В игре: нажмите P или ` (backtick)
// Или в коде:
this.physics.world.debugGraphic.setVisible(true);
```

### Симптомы проблем

| Симптом | Причина | Решение |
|---------|---------|---------|
| NPC проходит сквозь игрока | setPosition() обходит физику | setVelocity() |
| Коллизии не срабатывают | Хитбокс смещён | Проверить setCircle() offset |
| Снаряды не попадают | Два источника позиции | Синхронизировать sprite.x/y |
| NPC "телепортируется" | Tween анимация | moveTo() |

---

*Документ создан: 2026-03-18*
*Источник: checkpoint_03_18_physics_usage.md, checkpoint_03_18_colision_fix.md*
