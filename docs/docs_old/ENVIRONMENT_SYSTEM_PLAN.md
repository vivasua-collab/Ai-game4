# 🌳 План создания системы окружения для тестового полигона

**Дата:** 2026-03-04
**Статус:** 📝 Проектирование
**Цель:** Создать систему генерации окружения для тестового полигона

---

## 📊 Анализ текущего состояния

### ✅ Уже существует

| Компонент | Файл | Описание |
|-----------|------|----------|
| Prisma модель WorldObject | `prisma/schema.prisma` | objectType, resourceType, x/y/z, health |
| Prisma модель Building | `prisma/schema.prisma` | buildingType, width/length/height, locationId |
| EnvironmentSystem | `src/lib/game/environment-system.ts` | TerrainType, влияние на культивацию |
| TrainingTarget | `PhaserGame.tsx` | Соломенные чучела (HP, hitbox, damage) |
| SpriteLoader | `src/game/services/sprite-loader.ts` | Генерация текстур |

### ❌ Отсутствует

| Компонент | Описание |
|-----------|----------|
| Препятствия | Камни, овраги, стены |
| Деревья | Ресурс + препятствие |
| Рудные камни | Источники ресурсов |
| Строения | Стены, двери, окна (часть карты) |
| Генератор окружения | Процедурная генерация |
| Пресеты объектов | Конфигурации для генерации |

---

## 🏗️ Архитектура системы окружения

```
┌─────────────────────────────────────────────────────────────────────┐
│                    СИСТЕМА ОКРУЖЕНИЯ                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│   1. ТИПЫ ОБЪЕКТОВ (WorldObjectType)                                │
│      ├── obstacle     - Препятствия (камни, стены)                  │
│      ├── resource     - Ресурсы (руды, деревья)                     │
│      ├── building     - Строения (часть карты)                      │
│      ├── decoration   - Декорации (трава, цветы)                    │
│      └── interactive  - Интерактивные (сундуки, двери)              │
│                                                                      │
│   2. ПРЕСЕТЫ ОБЪЕКТОВ (EnvironmentPresets)                          │
│      ├── rock_presets.ts       - Камни разных размеров              │
│      ├── tree_presets.ts       - Деревья разных типов               │
│      ├── ore_presets.ts        - Рудные жилы                        │
│      ├── building_presets.ts   - Строения (деревянные)              │
│      └── index.ts              - Единый экспорт                     │
│                                                                      │
│   3. ГЕНЕРАТОР ОКРУЖЕНИЯ (EnvironmentGenerator)                     │
│      ├── generateEnvironment()    - Основная генерация              │
│      ├── placeObstacles()         - Размещение препятствий          │
│      ├── placeResources()         - Размещение ресурсов             │
│      ├── placeBuildings()         - Размещение строений             │
│      └── validatePlacement()      - Проверка коллизий               │
│                                                                      │
│   4. ИНТЕГРАЦИЯ В PHASER (PhaserEnvironment)                        │
│      ├── createEnvironmentSprite() - Создание спрайта               │
│      ├── createCollisionBody()    - Физика столкновений             │
│      ├── setupInteraction()       - Взаимодействие                  │
│      └── renderEnvironment()      - Отрисовка                       │
│                                                                      │
│   5. API ОКРУЖЕНИЯ                                                   │
│      ├── POST /api/environment/generate  - Генерация                │
│      ├── GET  /api/environment/state     - Загрузка                 │
│      └── POST /api/environment/interact  - Взаимодействие           │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ ТИПЫ ОБЪЕКТОВ ОКРУЖЕНИЯ

### 1.1 Препятствия (Obstacles)

```typescript
// Типы препятствий
type ObstacleType = 
  | 'rock_small'      // Малый камень (0.5м)
  | 'rock_medium'     // Средний камень (1м)
  | 'rock_large'      // Большой камень (2м)
  | 'boulder'         // Валун (3м)
  | 'ravine'          // Овраг (проходимый с штрафом)
  | 'cliff';          // Скала (непроходимая)

interface ObstaclePreset {
  id: string;
  name: string;
  type: ObstacleType;
  
  // Размеры в метрах
  width: number;
  height: number;
  
  // Физика
  isBlocking: boolean;      // Блокирует движение
  movementPenalty: number;  // Штраф скорости (0-1)
  
  // Визуал
  color: number;            // Цвет для генерации текстуры
  shape: 'circle' | 'rectangle' | 'irregular';
  
  // Разрушаемость
  isDestructible: boolean;
  health: number;
  defense: number;
}
```

### 1.2 Деревья (Trees)

```typescript
// Типы деревьев
type TreeType =
  | 'pine'             // Сосна (иголки)
  | 'oak'              // Дуб (широкая крона)
  | 'bamboo'           // Бамбук (тонкий, высокий)
  | 'willow'           // Ива (плакучая)
  | 'spirit_tree';     // Духовное дерево (особое)

interface TreePreset {
  id: string;
  name: string;
  type: TreeType;
  
  // Размеры
  trunkWidth: number;   // Толщина ствола (метры)
  trunkHeight: number;  // Высота ствола
  canopyRadius: number; // Радиус кроны
  
  // Ресурс
  woodYield: number;    // Древесины при рубке
  isHarvestable: boolean;
  respawnTime: number;  // Минуты
  
  // Физика
  isBlocking: boolean;
  providesCover: boolean; // Укрытие от атак
  
  // Визуал
  trunkColor: number;
  canopyColor: number;
}
```

### 1.3 Рудные камни (Ore Deposits)

```typescript
// Типы руд
type OreType =
  | 'iron_ore'         // Железная руда
  | 'copper_ore'       // Медная руда
  | 'spirit_ore'       // Духовная руда (для артефактов)
  | 'jade_ore'         // Нефрит
  | 'crystal';         // Кристалл Ци

interface OrePreset {
  id: string;
  name: string;
  type: OreType;
  
  // Ресурс
  yieldMin: number;     // Минимум руды
  yieldMax: number;     // Максимум руды
  quality: 'low' | 'medium' | 'high';
  
  // Требования
  requiredTool: 'pickaxe' | 'hammer' | 'hands';
  requiredLevel: number; // Уровень культивации
  
  // Визуал
  baseColor: number;    // Цвет камня
  veinColor: number;    // Цвет прожилок
  glowColor?: number;   // Свечение (для особых руд)
  
  // Физика
  isBlocking: boolean;
  size: 'small' | 'medium' | 'large';
}
```

### 1.4 Строения (Buildings) — часть карты

```typescript
// Типы деревянных строений
type WoodenBuildingType =
  | 'wall_wooden'      // Деревянная стена
  | 'door_wooden'      // Деревянная дверь
  | 'window_wooden'    // Деревянное окно (ставни)
  | 'fence_wooden'     // Деревянный забор
  | 'gate_wooden'      // Деревянные ворота
  | 'floor_wooden'     // Деревянный пол
  | 'roof_thatch';     // Соломенная крыша

interface BuildingPartPreset {
  id: string;
  name: string;
  type: WoodenBuildingType;
  
  // Размеры
  width: number;        // Ширина (метры)
  height: number;       // Высота (метры)
  depth: number;        // Толщина (метры)
  
  // Свойства
  isPassable: boolean;  // Можно пройти
  isOpenable: boolean;  // Можно открыть/закрыть
  isTransparent: boolean; // Просвечивает
  
  // Прочность
  health: number;
  defense: number;
  isDestructible: boolean;
  
  // Визуал
  color: number;
  borderColor: number;
}
```

---

## 2️⃣ ПРЕСЕТЫ ОБЪЕКТОВ

### 2.1 Камни (Rocks)

```typescript
// src/data/presets/environment/rock-presets.ts

export const ROCK_PRESETS: ObstaclePreset[] = [
  {
    id: 'rock_small',
    name: 'Малый камень',
    type: 'rock_small',
    width: 0.5,
    height: 0.3,
    isBlocking: false,
    movementPenalty: 0.1,
    color: 0x6b7280,
    shape: 'circle',
    isDestructible: false,
    health: 100,
    defense: 50,
  },
  {
    id: 'rock_medium',
    name: 'Камень',
    type: 'rock_medium',
    width: 1.0,
    height: 0.6,
    isBlocking: true,
    movementPenalty: 0,
    color: 0x4b5563,
    shape: 'irregular',
    isDestructible: true,
    health: 500,
    defense: 80,
  },
  {
    id: 'rock_large',
    name: 'Большой камень',
    type: 'rock_large',
    width: 2.0,
    height: 1.5,
    isBlocking: true,
    movementPenalty: 0,
    color: 0x374151,
    shape: 'irregular',
    isDestructible: true,
    health: 2000,
    defense: 100,
  },
  {
    id: 'boulder',
    name: 'Валун',
    type: 'boulder',
    width: 3.0,
    height: 2.5,
    isBlocking: true,
    movementPenalty: 0,
    color: 0x1f2937,
    shape: 'circle',
    isDestructible: false,
    health: 10000,
    defense: 200,
  },
];
```

### 2.2 Деревья (Trees)

```typescript
// src/data/presets/environment/tree-presets.ts

export const TREE_PRESETS: TreePreset[] = [
  {
    id: 'pine',
    name: 'Сосна',
    type: 'pine',
    trunkWidth: 0.3,
    trunkHeight: 3.0,
    canopyRadius: 1.5,
    woodYield: 50,
    isHarvestable: true,
    respawnTime: 1440, // 1 игровой день
    isBlocking: true,
    providesCover: true,
    trunkColor: 0x5c4033,
    canopyColor: 0x228b22,
  },
  {
    id: 'oak',
    name: 'Дуб',
    type: 'oak',
    trunkWidth: 0.5,
    trunkHeight: 4.0,
    canopyRadius: 3.0,
    woodYield: 100,
    isHarvestable: true,
    respawnTime: 2880, // 2 дня
    isBlocking: true,
    providesCover: true,
    trunkColor: 0x4a3728,
    canopyColor: 0x2d5a27,
  },
  {
    id: 'bamboo',
    name: 'Бамбук',
    type: 'bamboo',
    trunkWidth: 0.1,
    trunkHeight: 5.0,
    canopyRadius: 0.5,
    woodYield: 20,
    isHarvestable: true,
    respawnTime: 480, // 8 часов
    isBlocking: false,
    providesCover: true,
    trunkColor: 0x90a955,
    canopyColor: 0x6b8e23,
  },
  {
    id: 'spirit_tree',
    name: 'Духовное дерево',
    type: 'spirit_tree',
    trunkWidth: 1.0,
    trunkHeight: 8.0,
    canopyRadius: 5.0,
    woodYield: 500,
    isHarvestable: false, // Нельзя рубить
    respawnTime: 0,
    isBlocking: true,
    providesCover: true,
    trunkColor: 0x8b5cf6,
    canopyColor: 0x4ade80,
  },
];
```

### 2.3 Рудные камни (Ore Deposits)

```typescript
// src/data/presets/environment/ore-presets.ts

export const ORE_PRESETS: OrePreset[] = [
  {
    id: 'iron_ore',
    name: 'Железная руда',
    type: 'iron_ore',
    yieldMin: 5,
    yieldMax: 15,
    quality: 'low',
    requiredTool: 'pickaxe',
    requiredLevel: 0,
    baseColor: 0x4a4a4a,
    veinColor: 0x8b4513,
    isBlocking: true,
    size: 'medium',
  },
  {
    id: 'copper_ore',
    name: 'Медная руда',
    type: 'copper_ore',
    yieldMin: 3,
    yieldMax: 10,
    quality: 'low',
    requiredTool: 'pickaxe',
    requiredLevel: 0,
    baseColor: 0x4a4a4a,
    veinColor: 0xb87333,
    isBlocking: true,
    size: 'small',
  },
  {
    id: 'spirit_ore',
    name: 'Духовная руда',
    type: 'spirit_ore',
    yieldMin: 1,
    yieldMax: 5,
    quality: 'high',
    requiredTool: 'pickaxe',
    requiredLevel: 3,
    baseColor: 0x3b3b5c,
    veinColor: 0x8b5cf6,
    glowColor: 0xa78bfa,
    isBlocking: true,
    size: 'small',
  },
  {
    id: 'crystal',
    name: 'Кристалл Ци',
    type: 'crystal',
    yieldMin: 1,
    yieldMax: 3,
    quality: 'high',
    requiredTool: 'hands',
    requiredLevel: 5,
    baseColor: 0x1e293b,
    veinColor: 0x4ade80,
    glowColor: 0x22c55e,
    isBlocking: false,
    size: 'small',
  },
];
```

### 2.4 Деревянные строения (Wooden Buildings)

```typescript
// src/data/presets/environment/building-presets.ts

export const BUILDING_PART_PRESETS: BuildingPartPreset[] = [
  {
    id: 'wall_wooden',
    name: 'Деревянная стена',
    type: 'wall_wooden',
    width: 1.0,
    height: 2.5,
    depth: 0.2,
    isPassable: false,
    isOpenable: false,
    isTransparent: false,
    health: 500,
    defense: 30,
    isDestructible: true,
    color: 0x8b4513,
    borderColor: 0x5c4033,
  },
  {
    id: 'door_wooden',
    name: 'Деревянная дверь',
    type: 'door_wooden',
    width: 1.0,
    height: 2.2,
    depth: 0.1,
    isPassable: true,    // Открыта по умолчанию
    isOpenable: true,
    isTransparent: false,
    health: 200,
    defense: 20,
    isDestructible: true,
    color: 0xa0522d,
    borderColor: 0x5c4033,
  },
  {
    id: 'window_wooden',
    name: 'Окно со ставнями',
    type: 'window_wooden',
    width: 1.0,
    height: 1.0,
    depth: 0.1,
    isPassable: false,
    isOpenable: true,
    isTransparent: true, // Просвечивает
    health: 100,
    defense: 10,
    isDestructible: true,
    color: 0x8b4513,
    borderColor: 0x5c4033,
  },
  {
    id: 'fence_wooden',
    name: 'Деревянный забор',
    type: 'fence_wooden',
    width: 1.0,
    height: 1.2,
    depth: 0.1,
    isPassable: false,
    isOpenable: false,
    isTransparent: true, // Просвечивает
    health: 100,
    defense: 10,
    isDestructible: true,
    color: 0xdeb887,
    borderColor: 0x8b4513,
  },
  {
    id: 'gate_wooden',
    name: 'Деревянные ворота',
    type: 'gate_wooden',
    width: 3.0,
    height: 2.5,
    depth: 0.3,
    isPassable: true,
    isOpenable: true,
    isTransparent: false,
    health: 800,
    defense: 50,
    isDestructible: true,
    color: 0x8b4513,
    borderColor: 0x4a3728,
  },
];
```

---

## 3️⃣ ГЕНЕРАТОР ОКРУЖЕНИЯ

```typescript
// src/lib/generator/environment-generator.ts

interface EnvironmentGenerationConfig {
  width: number;          // Ширина области (метры)
  height: number;         // Высота области (метры)
  terrainType: TerrainType;
  
  // Плотность объектов
  rockDensity: number;    // 0-1
  treeDensity: number;    // 0-1
  oreDensity: number;     // 0-1
  
  // Строения (предустановленные)
  buildings?: BuildingPlacement[];
  
  // Seed для детерминированной генерации
  seed?: number;
}

interface GeneratedEnvironment {
  obstacles: WorldObject[];
  trees: WorldObject[];
  ores: WorldObject[];
  buildings: WorldObject[];
}

export function generateEnvironment(
  config: EnvironmentGenerationConfig
): GeneratedEnvironment {
  // 1. Инициализация RNG с seed
  // 2. Размещение препятствий (avoid overlaps)
  // 3. Размещение деревьев
  // 4. Размещение рудных камней
  // 5. Размещение строений
  // 6. Валидация путей (A* pathfinding)
}
```

---

## 4️⃣ ИНТЕГРАЦИЯ В PHASER

### 4.1 Создание текстур

```typescript
// src/game/services/environment-textures.ts

export function createRockTexture(
  scene: Phaser.Scene,
  preset: ObstaclePreset
): void {
  const graphics = scene.make.graphics();
  const size = preset.width * 32; // метры в пиксели
  
  if (preset.shape === 'circle') {
    graphics.fillStyle(preset.color);
    graphics.fillCircle(size / 2, size / 2, size / 2);
    
    // Добавляем текстуру
    graphics.fillStyle(Phaser.Display.Color.GetColor(
      Phaser.Display.Color.IntegerToRGB(preset.color).r * 0.8,
      Phaser.Display.Color.IntegerToRGB(preset.color).g * 0.8,
      Phaser.Display.Color.IntegerToRGB(preset.color).b * 0.8
    ));
    graphics.fillCircle(size / 3, size / 3, size / 6);
  }
  
  graphics.generateTexture(preset.id, size, size);
  graphics.destroy();
}

export function createTreeTexture(
  scene: Phaser.Scene,
  preset: TreePreset
): void {
  const graphics = scene.make.graphics();
  const trunkWidth = preset.trunkWidth * 32;
  const trunkHeight = preset.trunkHeight * 32;
  const canopyRadius = preset.canopyRadius * 32;
  
  // Ствол
  graphics.fillStyle(preset.trunkColor);
  graphics.fillRect(
    -trunkWidth / 2,
    -trunkHeight,
    trunkWidth,
    trunkHeight
  );
  
  // Крона
  graphics.fillStyle(preset.canopyColor);
  graphics.fillCircle(0, -trunkHeight, canopyRadius);
  
  graphics.generateTexture(preset.id, canopyRadius * 2, trunkHeight + canopyRadius);
  graphics.destroy();
}
```

### 4.2 Физика столкновений

```typescript
// В PhaserGame.tsx

interface EnvironmentObject {
  id: string;
  presetId: string;
  x: number;
  y: number;
  sprite: Phaser.GameObjects.Container;
  body: Phaser.Physics.Arcade.Body;
  preset: ObstaclePreset | TreePreset | OrePreset | BuildingPartPreset;
}

function createEnvironmentObject(
  scene: Phaser.Scene,
  preset: ObstaclePreset,
  x: number,
  y: number
): EnvironmentObject {
  const container = scene.add.container(x, y);
  
  // Спрайт
  const sprite = scene.add.image(0, 0, preset.id);
  container.add(sprite);
  
  // Физика
  scene.physics.add.existing(container, true); // static body
  
  const body = container.body as Phaser.Physics.Arcade.Body;
  if (preset.isBlocking) {
    const size = preset.width * 32;
    body.setSize(size, size);
    body.setOffset(-size / 2, -size / 2);
  }
  
  return {
    id: generateId(),
    presetId: preset.id,
    x, y,
    sprite: container,
    body,
    preset,
  };
}
```

---

## 5️⃣ API ENDPOINTS

### 5.1 Генерация окружения

```typescript
// POST /api/environment/generate

interface GenerateEnvironmentRequest {
  sessionId: string;
  locationId: string;
  config: EnvironmentGenerationConfig;
}

interface GenerateEnvironmentResponse {
  success: boolean;
  environment?: {
    obstacles: WorldObjectData[];
    trees: WorldObjectData[];
    ores: WorldObjectData[];
    buildings: WorldObjectData[];
  };
}
```

### 5.2 Загрузка окружения

```typescript
// GET /api/environment/state?locationId=xxx

interface GetEnvironmentResponse {
  success: boolean;
  objects: WorldObjectData[];
}
```

### 5.3 Взаимодействие

```typescript
// POST /api/environment/interact

interface InteractRequest {
  objectId: string;
  action: 'harvest' | 'destroy' | 'open' | 'close';
  characterId: string;
}

interface InteractResponse {
  success: boolean;
  result?: {
    resources?: { itemId: string; quantity: number }[];
    newState?: Partial<WorldObjectData>;
  };
}
```

---

## 📋 План реализации

### Фаза 1: Базовая инфраструктура (1 день)

```
□ Создать src/types/environment.ts (типы)
□ Создать src/data/presets/environment/ (папка)
□ Создать rock-presets.ts
□ Создать tree-presets.ts
□ Создать ore-presets.ts
□ Создать building-presets.ts
□ Создать index.ts (экспорт)
```

### Фаза 2: Генератор текстур (1 день)

```
□ Создать src/game/services/environment-textures.ts
□ createRockTexture()
□ createTreeTexture()
□ createOreTexture()
□ createBuildingTexture()
□ Интегрировать в PhaserGame.tsx
```

### Фаза 3: Генератор окружения (2 дня)

```
□ Создать src/lib/generator/environment-generator.ts
□ generateEnvironment()
□ placeObstacles() с проверкой коллизий
□ placeResources()
□ placeBuildings()
□ validatePlacement()
□ createDeterministicRNG()
```

### Фаза 4: API (1 день)

```
□ Создать src/app/api/environment/generate/route.ts
□ Создать src/app/api/environment/state/route.ts
□ Создать src/app/api/environment/interact/route.ts
□ Интеграция с WorldObject моделью
```

### Фаза 5: Интеграция в Phaser (2 дня)

```
□ Добавить environmentObjects[] в PhaserGame
□ createEnvironmentObject() с физикой
□ Коллизии игрока с препятствиями
□ Взаимодействие с ресурсами
□ Визуализация в тестовом полигоне
```

### Фаза 6: Тестирование (1 день)

```
□ Генерация тестового полигона
□ Проверка коллизий
□ Проверка сбора ресурсов
□ Проверка разрушаемости
□ Проверка дверей/окон
```

---

## 🎨 Визуальные примеры

### Малый камень
```
   ⬤
  ██▀
  ▄█
```

### Средний камень
```
  ▄███▄
 ███████
  ▀███▀
```

### Сосна
```
    🌲
   /│\
  /││\
 /█████\
    │
    │
```

### Железная руда
```
  ▄███▄
 ██░░░██  ← прожилки
  ▀███▀
```

### Деревянная стена
```
 ┌─────────┐
 │ ═══════ │
 │ ═══════ │
 │ ═══════ │
 └─────────┘
```

---

## 📊 Оценка времени

| Фаза | Время | Сложность |
|------|-------|-----------|
| 1. Базовая инфраструктура | 1 день | 🟢 Низкая |
| 2. Генератор текстур | 1 день | 🟡 Средняя |
| 3. Генератор окружения | 2 дня | 🟡 Средняя |
| 4. API | 1 день | 🟢 Низкая |
| 5. Интеграция в Phaser | 2 дня | 🔴 Высокая |
| 6. Тестирование | 1 день | 🟡 Средняя |
| **Итого** | **8 дней** | |

---

## 🔄 Связанные документы

- `prisma/schema.prisma` — модели WorldObject, Building
- `src/lib/game/environment-system.ts` — система местности
- `docs/TRAINING_GROUND_ROADMAP.md` — план полигона
- `docs/equip.md` — система экипировки (для инструментов)

---

*Документ создан: 2026-03-04*
