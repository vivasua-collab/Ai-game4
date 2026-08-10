# 🗺️ Архитектура Карты Мира

> **Версия:** 1.2 | **Дата:** 2026-03-14
> **Аналог:** RimWorld (карта мира → карта поселения)
> **Статус:** 🔄 Частично реализовано

---

## 🎯 Обзор системы

Система карты реализует двухуровневую архитектуру:
1. **WorldScene** — карта мира с локациями (косметический вид)
2. **LocationScene** — локальная сцена с игроком, NPC и объектами

### Ключевые особенности

- **Координаты в метрах** — все расстояния в игровых метрах (1м = 32px)
- **Процедурная генерация** — NPC генерируются при входе в локацию
- **Временные NPC** — существуют только в памяти сессии
- **Статические объекты** — сохраняются в базе данных

---

## 📊 Текущее состояние реализации

### ✅ Реализовано (v0.6.x - v0.7.x)

| Компонент | Файл | Статус |
|-----------|------|--------|
| WorldScene | `src/game/scenes/WorldScene.ts` | ✅ Работает |
| LocationScene | `src/game/scenes/LocationScene.ts` | ✅ Работает |
| Map API | `src/app/api/map/route.ts` | ✅ Работает |
| Map Types | `src/types/map.ts` | ✅ Работает |
| MapService | `src/services/map.service.ts` | ✅ Работает |
| GameBridge | `src/services/game-bridge.service.ts` | ✅ Работает |
| BootScene | `src/game/scenes/BootScene.ts` | ✅ Работает |

### ❌ Не реализовано

| Компонент | Статус | Описание |
|-----------|--------|----------|
| Кнопка "Карта" | 🔴 Отключена | `ActionButtons.tsx` line 101-109 |
| Секторная система | ⬜ Планируется | См. ниже |
| Static/Dynamic объекты | ⬜ Планируется | См. ниже |

---

## 🏗️ Текущая архитектура

### Поток сцен Phaser

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PHASER SCENE FLOW                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                       │
│   BootScene                                                           │
│      │                                                                │
│      ├─ Создание текстур (программно)                                │
│      ├─ Загрузка пресетов                                            │
│      │                                                                │
│      └─────────────▶ WorldScene                                      │
│                           │                                           │
│                           ├─ Загрузка локаций (/api/map?action=all)  │
│                           ├─ Расстановка в круг                       │
│                           ├─ Клик на локацию                          │
│                           │                                           │
│                           └─────────────▶ LocationScene              │
│                                              │                        │
│                                              ├─ Игрок (WASD)         │
│                                              ├─ NPC                   │
│                                              ├─ Бой                   │
│                                              ├─ Окружение             │
│                                              │                        │
│                                              └── [ESC] / "← Карта"   │
│                                                        │              │
│                                                        ▼              │
│                                                   WorldScene          │
│                                                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

### WorldScene (Текущая реализация)

**Файл:** `src/game/scenes/WorldScene.ts`

```typescript
// Загрузка локаций
const response = await fetch(`/api/map?action=all&sessionId=${sessionId}`);

// Расстановка в круг (не использует реальные координаты)
const centerX = 450, centerY = 275, radius = 180;
const angle = (index / locations.length) * Math.PI * 2 - Math.PI / 2;
displayX = centerX + Math.cos(angle) * radius;
displayY = centerY + Math.sin(angle) * radius;

// Переход в локацию
this.goToScene('LocationScene', { locationId, locationName, sessionId });
```

**Особенности:**
- Локации отображаются в круге (косметический вид)
- Реальные координаты (x, y, z в метрах) не используются для отображения
- Интерактивные маркеры с tooltips
- Пульсирующий маркер игрока в центре

### LocationScene (Текущая реализация)

**Файл:** `src/game/scenes/LocationScene.ts`

**Функции:**
- Движение игрока (WASD/стрелки)
- NPC в локации
- Мишени для тренировки
- Окружение (деревья, камни)
- Переход обратно на карту (кнопка "← Карта")

### Map API

**Файл:** `src/app/api/map/route.ts`

| Action | Метод | Описание |
|--------|------|----------|
| `all` | GET | Все локации сессии |
| `get` | GET | Конкретная локация по ID |
| `radius` | GET | Локации в радиусе от игрока |
| POST | POST | Создание локации/строения/объекта |

### Типы данных

**Файл:** `src/types/map.ts`

```typescript
// Тип локации
type LocationType = 'region' | 'area' | 'building' | 'room';

// Тип местности
type TerrainType = 'plains' | 'mountains' | 'forest' | 'sea' | 
                  'desert' | 'swamp' | 'tundra' | 'volcanic' | 
                  'holy' | 'cursed';

// Расширенная локация
interface MapLocation {
  id: LocationId;
  name: string;
  x: number; y: number; z: number;  // Реальные координаты в метрах
  distanceFromCenter: number;
  qiDensity: number;
  qiFlowRate: number;
  terrainType: TerrainType;
  locationType: LocationType;
  parentLocationId?: string;
  buildingParentId?: string;
}
```

---

## 🗺️ Детальное описание текущей системы

### WorldScene (Карта мира)

**Файл:** `src/game/scenes/WorldScene.ts`

**Назначение:**
- Отображает все локации сессии в виде интерактивных маркеров
- Позволяет перейти в выбранную локацию
- Косметическое представление (расстановка в круг)

**Ключевые методы:**
```typescript
// Загрузка локаций из API
loadLocations(): Promise<void>
  → fetch('/api/map?action=all&sessionId=...')
  → renderLocations()

// Создание маркера локации
createLocationMarker(location): Container
  → Circle (фон по типу местности)
  → Text (иконка локации)
  → Text (название)
  → Interactive handlers (hover, click)

// Переход в локацию
onLocationClick(location): void
  → goToScene('LocationScene', { locationId, locationName, sessionId })
```

**Визуальные элементы:**
- Градиентный фон с атмосферными частицами
- Маркеры локаций с пульсирующей анимацией
- Всплывающие подсказки (tooltip)
- Маркер игрока в центре

### LocationScene (Локальная сцена)

**Файл:** `src/game/scenes/LocationScene.ts`

**Назначение:**
- 2D арена с движением игрока (WASD)
- NPC с AI поведением (патруль, преследование, атака, бегство)
- Тренировочные мишени для тестирования боевой системы
- Интеграция с React UI (статус, отдых, техники, инвентарь)

**Параметры сцены:**
```typescript
const WORLD_WIDTH = 1600;   // 50 метров
const WORLD_HEIGHT = 1200;  // 37.5 метров
const PLAYER_SPEED = 200;   // пикселей/сек
const METERS_TO_PIXELS = 32;
```

**AI система NPC:**
```typescript
// Машина состояний
type NPCState = 'idle' | 'patrol' | 'chase' | 'attack' | 'flee';

// Обновление поведения
updateNPCBehavior(npc): void
  → Проверка дистанции до игрока
  → Определение агрессивности (disposition < 0)
  → Переход между состояниями
  → Движение к цели (moveNPCTowards)
```

**Система боя:**
- Конусная атака (60°, 150px дальность)
- Регистрация попаданий через хитбоксы
- Визуальные эффекты урона (числа, вспышки)
- Авто-регенерация мишеней

### Map API

**Файл:** `src/app/api/map/route.ts`

| Action | Метод | Параметры | Описание |
|--------|------|-----------|----------|
| `current` | GET | characterId | Текущая локация персонажа |
| `all` | GET | sessionId | Все локации сессии |
| `radius` | GET | characterId, radius | Локации в радиусе |
| POST | POST | type, data | Создание локации/строения/объекта |

**Пример ответа `/api/map?action=all`:**
```json
{
  "success": true,
  "locations": [
    {
      "id": "loc_xxx",
      "name": "Тренировочный полигон",
      "terrainType": "plains",
      "x": 0,
      "y": 0,
      "z": 0,
      "qiDensity": 100,
      "locationType": "area"
    }
  ]
}
```

### MapService

**Файл:** `src/services/map.service.ts`

**Ключевые функции:**
- `getLocationsInRadius()` — поиск локаций в радиусе от точки
- `getLocationById()` — получение локации по ID
- `createLocation()` — создание новой локации
- `getBuildingsAtLocation()` — строения в локации
- `getObjectsAtLocation()` — объекты в локации
- `createWorldObject()` — создание объекта на карте

### Типы данных карты

**Файл:** `src/types/map.ts`

```typescript
// Тип локации
type LocationType = 'region' | 'area' | 'building' | 'room';

// Тип местности
type TerrainType = 'plains' | 'mountains' | 'forest' | 'sea' | 
                   'desert' | 'swamp' | 'tundra' | 'volcanic' | 
                   'holy' | 'cursed';

// Локация
interface MapLocation {
  id: LocationId;
  name: string;
  x: number; y: number; z: number;  // Координаты в метрах
  distanceFromCenter: number;
  qiDensity: number;
  qiFlowRate: number;
  terrainType: TerrainType;
  locationType: LocationType;
}

// Строение
interface Building {
  id: string;
  name: string;
  buildingType: BuildingType;
  locationId: LocationId;
  width: number; length: number; height: number;
  qiBonus: number;  // Бонус к медитации
  comfort: number;  // Восстановление
  defense: number;  // Защита
}

// Объект на карте
interface WorldObject {
  id: string;
  objectType: ObjectType;  // 'resource' | 'container' | 'interactable' | 'decoration'
  resourceType?: ResourceType;  // 'herb' | 'ore' | 'wood' | 'water' | 'crystal' | 'spirit'
  health: number; maxHealth: number;
  resourceCount: number;
  respawnTime: number;  // в минутах
}
```

---

## 🔮 План развития: Секторная система (v0.8.0+)

### Проблема текущей архитектуры

1. **Нет разделения на путешествие и локальную сцену**
   - WorldScene показывает только список локаций
   - Нет ощущения "мира" как пространства

2. **Нет статических/динамических объектов**
   - Все объекты в локации временные
   - Нет сохранения состояния окружения

3. **Кнопка "Карта" отключена**
   - Нет UI для доступа к WorldScene

### Решение: Двухуровневая система

```
┌─────────────────────────────────────────────────────────────────────┐
│                      WORLD MAP (Внешний уровень)                     │
│                                                                          │
│    ┌────┬────┬────┬────┬────┐                                          │
│    │ 🏔️ │ 🌲 │ 🏘️ │ 🌲 │ 🌊 │   Секторы (tiles)                        │
│    ├────┼────┼────┼────┼────┤   - Перемещение за игровое время         │
│    │ 🌲 │ 🏛️ │ 👤 │ 🌾 │ 🌲 │   - Вид "сверху" на мир                  │
│    ├────┼────┼────┼────┼────┤   - Фог войны                            │
│    │ 🏔️ │ 🌲 │ 🌲 │ 🐺 │ 🏔️ │                                          │
│    └────┴────┴────┴────┴────┘                                          │
│                              │                                          │
│                              │ Enter Sector                            │
│                              ▼                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                      SECTOR VIEW (Локальный уровень)                 │
│                                                                          │
│    ┌──────────────────────────────────────┐                            │
│    │  🌲 🪨     🏠        🌲 🌲           │                            │
│    │        👤                    🐺      │   Локальная сцена          │
│    │  🌲 🌲          🪨     🌲 🌲         │   - Phaser 2D арена        │
│    │        🌲 🌲                          │   - Статические объекты    │
│    │              🌲    🌲 🌲             │   - Динамические NPC       │
│    └──────────────────────────────────────┘                            │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 Новые типы данных

### 1. WorldSector (Сектор мира)

```typescript
interface WorldSector {
  id: string;                    // SECTOR_XXXX_YYYY
  worldId: string;
  
  // Позиция на карте мира
  gridX: number;                 // 0..WORLD_WIDTH-1
  gridY: number;                 // 0..WORLD_HEIGHT-1
  
  // Тип сектора
  terrainType: TerrainType;
  biomeType: BiomeType;
  
  // Характеристики
  dangerLevel: number;           // 1-10
  qiDensity: number;
  resourceRichness: number;
  
  // Статические данные (сохраняются)
  staticObjects: StaticObject[];
  structures: Structure[];
  
  // Исследование
  explored: boolean;
  lastVisited?: number;
}
```

### 2. StaticObject vs DynamicEntity

```typescript
// Статические - сохраняются при уходе
interface StaticObject {
  id: string;
  type: 'tree' | 'rock' | 'ore' | 'ruin' | 'shrine';
  localX: number;
  localY: number;
  health: number;
  harvested: boolean;
}

// Динамические - генерируются при входе
interface DynamicEntity {
  id: string;
  entityType: 'npc' | 'animal' | 'spirit';
  localX: number;
  localY: number;
  spawnTime: number;
  lifespan?: number;
}
```

---

## 📐 Размерности

### Текущие

```typescript
// Сцена Phaser
const GAME_WIDTH = 900;
const GAME_HEIGHT = 550;

// Координаты мира (метры)
const WORLD_BOUNDS = {
  minX: -100000, maxX: 100000,  // 200km
  minY: -100000, maxY: 100000,  // 200km
  minZ: -1000, maxZ: 10000,
};
```

### Планируемые

```typescript
// Размер мира в секторах
const WORLD_SIZE = { width: 50, height: 50 };

// Размер сектора (локальный)
const SECTOR_SIZE = {
  tiles: 32,      // 32x32 тайлов
  pixels: 1024,   // 1024x1024 px
  meters: 320,    // 320x320 m
};
```

---

## 🔧 План реализации

### Фаза 1: Интеграция (v0.7.x)

| Задача | Описание | Статус |
|--------|----------|--------|
| Включить кнопку "Карта" | Добавить onClick в ActionButtons.tsx | 🔴 Todo |
| Связать с GameBridge | Переход в WorldScene через bridge | 🔴 Todo |
| Передать sessionId | GameBridge должен хранить sessionId | ✅ Done |

### Фаза 2: Секторная система (v0.8.0)

| Задача | Описание | Статус |
|--------|----------|--------|
| Prisma Schema | World, WorldSector, StaticObject | ⬜ Plan |
| World Generator | Генерация 50x50 секторов | ⬜ Plan |
| World Map Scene | Новая сцена карты мира | ⬜ Plan |
| Travel System | Переходы между секторами | ⬜ Plan |

### Фаза 3: Локальный режим (v0.9.0)

| Задача | Описание | Статус |
|--------|----------|--------|
| Sector Scene | Сцена отдельного сектора | ⬜ Plan |
| Static Objects | Сохранение объектов | ⬜ Plan |
| Dynamic Entities | Генерация NPC | ⬜ Plan |
| NPC by Location | Фильтрация по сектору | ✅ Done |

---

## 🔗 Связанные файлы

| Файл | Назначение |
|------|------------|
| `src/game/scenes/WorldScene.ts` | Сцена карты мира |
| `src/game/scenes/LocationScene.ts` | Сцена локации |
| `src/game/scenes/BootScene.ts` | Загрузка сцен |
| `src/types/map.ts` | Типы для карты |
| `src/services/map.service.ts` | Сервис карты |
| `src/services/game-bridge.service.ts` | Мост Phaser ↔ React |
| `src/app/api/map/route.ts` | API карты |
| `src/components/game/ActionButtons.tsx` | UI кнопки "Карта" |

---

*Документ обновлён: 2026-03-14*
