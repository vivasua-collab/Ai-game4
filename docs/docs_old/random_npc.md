# Система временных NPC ("Статисты")

**Версия:** 2.1
**Обновлено:** 2026-03-22 15:40 UTC
**Статус:** ✅ Реализовано

## Концепция

### Два типа NPC

| Тип | Хранение | Примеры | Создание |
|-----|----------|---------|----------|
| **Персистентные** | База данных (Prisma NPC) | Руководство сект, квестовые NPC, именные персонажи | Через генератор с сохранением |
| **Статисты** | Память сессии (Runtime) | Обычное население, монстры, второстепенные враги | Динамически при входе в локацию |

### Жизненный цикл статиста

```
┌──────────────────────────────────────────────────────────────────┐
│                    ЖИЗНЕННЫЙ ЦИКЛ СТАТИСТА                        │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. ИНИЦИАЛИЗАЦИЯ ЛОКАЦИИ                                        │
│     ┌─────────────┐                                              │
│     │ Вход в      │──► Генерация N статистов по конфигу локации   │
│     │ локацию     │    (вид, роль, уровень = параметры локации)   │
│     └─────────────┘                                              │
│            │                                                      │
│            ▼                                                      │
│  2. СЕССИЯ В ЛОКАЦИИ                                             │
│     ┌─────────────┐                                              │
│     │ Взаимодей-  │──► Бой, диалог, торговля                      │
│     │ ствие       │    (экипировка/техники генерируются 1 раз)    │
│     └─────────────┘                                              │
│            │                                                      │
│            ▼                                                      │
│  3. ЗАВЕРШЕНИЕ                                                   │
│     ┌─────────────┐                                              │
│     │ Смерть ИЛИ  │──► Удаление из памяти + лут (если смерть)     │
│     │ Выход из    │    (без сохранения в базу)                    │
│     │ локации     │                                              │
│     └─────────────┘                                              │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## Архитектура

### Текущая структура

```
┌─────────────────────────────────────────────────────────────────┐
│                    СУЩЕСТВУЮЩАЯ АРХИТЕКТУРА                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Prisma NPC ─────► База данных (постоянные NPC)                 │
│       │                                                          │
│       └──► Поля: name, cultivationLevel, sectId, locationId...  │
│                                                                  │
│  SessionService ──► Управление сессией (БД)                     │
│                                                                  │
│  NPC Generator ───► Генерация + сохранение в presets/npcs/      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Новая структура

```
┌─────────────────────────────────────────────────────────────────┐
│                    НОВАЯ АРХИТЕКТУРА                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────┐    ┌──────────────────────┐          │
│  │ PersistentNPCService │    │  SessionNPCManager   │          │
│  │   (существующий)     │    │     (НОВЫЙ)          │          │
│  └──────────┬───────────┘    └──────────┬───────────┘          │
│             │                           │                       │
│             ▼                           ▼                       │
│  ┌──────────────────────┐    ┌──────────────────────┐          │
│  │   Prisma NPC (DB)    │    │  Runtime Memory Map  │          │
│  │  Постоянные NPC      │    │  sessionId → NPCs[]  │          │
│  └──────────────────────┘    └──────────────────────┘          │
│                                        │                        │
│                                        ▼                        │
│                             ┌──────────────────────┐           │
│                             │  TempNPC Interface   │           │
│                             │  + runtime data      │           │
│                             └──────────────────────┘           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

### ✅ Реализованные компоненты (2026-03-22)

| Компонент | Файл | Статус |
|-----------|------|--------|
| TempNPC interface | `src/types/temp-npc.ts` | ✅ Реализован |
| SessionNPCManager | `src/lib/game/session-npc-manager.ts` | ✅ Реализован |
| TempNPC Combat | `src/lib/game/skeleton/temp-npc-combat.ts` | ✅ Реализован |
| Event Bus Integration | `src/lib/game/event-bus/handlers/combat.ts` | ✅ Работает |
| NPC AI Controller | `src/lib/game/npc-ai.ts` | ✅ Реализован |
| V2 Generator Migration | `src/lib/generator/npc-full-generator.ts` | ✅ V2 Migrated |
| Material/Morphology | `src/types/temp-npc.ts` | ✅ Добавлены |

---

## Типы данных

### TempNPC Interface (v2.1)

```typescript
/**
 * Временный NPC (статист) - существует только в памяти
 * 
 * ✅ SoulEntity Compatibility (v2.0)
 * ✅ Material/Morphology Support (v2.1)
 */
interface TempNPC {
  // === Идентификация ===
  id: string;                    // TEMP_XXXXXX (не в базе)
  isTemporary: true;             // Флаг временного NPC
  
  // === SoulEntity Compatibility (v2.0) ===
  soulType: 'character' | 'creature' | 'spirit' | 'construct';
  controller: 'ai';
  mind: 'full' | 'instinct' | 'simple';
  
  // === Пресеты (из базы пресетов) ===
  speciesId: string;             // human, elf, wolf, etc.
  speciesType: SpeciesType;      // humanoid, beast, spirit, hybrid, aberration
  roleId: string;                // outer_disciple, bandit, etc.
  
  // === Генерируемые данные ===
  name: string;                  // Случайное имя
  nameEn: string;                // Имя на английском
  gender: 'male' | 'female' | 'none';
  age: number;
  
  // === Характеристики (из формул Lore) ===
  stats: {
    strength: number;
    agility: number;
    intelligence: number;
    conductivity: number;
    vitality: number;
  };
  
  cultivation: {
    level: number;
    subLevel: number;
    coreCapacity: number;
    currentQi: number;
    qiDensity: number;
    meridianConductivity: number;
  };
  
  // === Тело (Kenshi-style) ===
  bodyState: TempBodyState;
  
  // === Материал и морфология (NEW! v2.1) ===
  bodyMaterial?: BodyMaterial;    // organic, scaled, chitin, ethereal, mineral, chaos
  morphology?: BodyMorphology;    // humanoid, quadruped, bird, serpentine, arthropod, amorphous
  
  // === Qi ===
  qi: {
    current: number;
    max: number;
  };
  
  // === Генерируемые слоты ===
  equipment: TempEquipment;
  quickSlots: (TempItem | null)[];       // Расходники в быстрых слотах
  techniques: string[];                   // ID техник из пула
  techniqueData?: TechniqueData[];        // Данные техник (V1 формат)
  
  // === Личность ===
  personality: {
    disposition: number;         // -100 до 100 (отношение к игроку)
    aggressionLevel: number;     // 0-100
    fleeThreshold: number;       // % HP при котором бежит
    canTalk: boolean;
    canTrade: boolean;
    traits: string[];
    motivation?: string;
    dominantEmotion?: string;
  };
  
  // === ИИ конфигурация ===
  position?: { x: number; y: number };
  aiConfig?: AIBehaviorConfig;
  
  // === Контекст ===
  locationId: string;            // Текущая локация
  resources: {
    spiritStones: number;
    contributionPoints: number;
  };
  generatedAt: number;
  seed: number;
}

/**
 * BodyMaterial - материал тела для снижения урона
 */
type BodyMaterial = 'organic' | 'scaled' | 'chitin' | 'ethereal' | 'mineral' | 'chaos';

/**
 * BodyMorphology - тип строения тела
 */
type BodyMorphology = 'humanoid' | 'quadruped' | 'bird' | 'serpentine' | 'arthropod' | 'amorphous';
```

### TempItem Interface

```typescript
/**
 * Временный предмет (существует только у статиста)
 */
interface TempItem {
  id: string;                    // TEMP_ITEM_XXXXXX
  name: string;
  type: 'weapon' | 'armor' | 'consumable' | 'accessory';
  rarity: 'common' | 'uncommon' | 'rare' | 'legendary';
  
  // Статы предмета
  stats: {
    damage?: number;
    defense?: number;
    qiBonus?: number;
  };
  
  // Эффекты (для расходников)
  effects?: {
    type: string;
    value: number;
  }[];
  
  // Заряды (для расходников)
  charges?: number;
  maxCharges?: number;
}
```

---

## Конфигурация локации

### LocationNPCConfig

```typescript
/**
 * Конфигурация NPC для локации
 * Хранится в Location.properties или отдельной таблице
 */
interface LocationNPCConfig {
  // Количество статистов
  population: {
    min: number;                 // Минимум в локации
    max: number;                 // Максимум в локации
    density?: number;            // На 1000 м²
  };
  
  // Ограничения по видам
  allowedSpecies: {
    type: 'humanoid' | 'beast' | 'spirit' | 'hybrid' | 'aberration';
    weight: number;              // Вероятность (сумма = 100)
  }[];
  
  // Ограничения по ролям
  allowedRoles: {
    type: 'sect' | 'profession' | 'social' | 'combat';
    weight: number;
  }[];
  
  // Диапазон уровней
  levelRange: {
    min: number;
    max: number;
    // Или привязка к игроку
    relativeToPlayer?: number;   // +-N уровней от игрока
  };
  
  // Спавн монстров (отдельно)
  monsters?: {
    types: string[];             // ['wolf', 'tiger', 'bear']
    spawnRate: number;           // Шанс спавна при входе
    levelVariance: number;       // Разброс уровней
  };
  
  // Параметры лута
  lootConfig: {
    dropRate: number;            // Базовый шанс дропа
    lootTable: string[];         // ID возможного лута
  };
}
```

### Примеры конфигураций

```typescript
// Пример 1: Города и поселения
const villageConfig: LocationNPCConfig = {
  population: { min: 5, max: 15, density: 10 },
  allowedSpecies: [
    { type: 'humanoid', weight: 90 },
    { type: 'beast', weight: 5 },
    { type: 'spirit', weight: 5 },
  ],
  allowedRoles: [
    { type: 'profession', weight: 40 },
    { type: 'social', weight: 30 },
    { type: 'sect', weight: 20 },
    { type: 'combat', weight: 10 },
  ],
  levelRange: { min: 1, max: 3 },
  lootConfig: { dropRate: 0.1, lootTable: ['common_consumable', 'spirit_stone_small'] },
};

// Пример 2: Опасная территория
const wildernessConfig: LocationNPCConfig = {
  population: { min: 3, max: 8, density: 2 },
  allowedSpecies: [
    { type: 'beast', weight: 70 },
    { type: 'humanoid', weight: 20 },
    { type: 'aberration', weight: 10 },
  ],
  allowedRoles: [
    { type: 'combat', weight: 80 },
    { type: 'social', weight: 20 },
  ],
  levelRange: { min: 3, max: 7, relativeToPlayer: 2 },
  monsters: {
    types: ['wolf', 'tiger', 'bear'],
    spawnRate: 0.8,
    levelVariance: 2,
  },
  lootConfig: { dropRate: 0.3, lootTable: ['beast_core', 'rare_material'] },
};

// Пример 3: Секта
const sectConfig: LocationNPCConfig = {
  population: { min: 20, max: 50, density: 15 },
  allowedSpecies: [
    { type: 'humanoid', weight: 95 },
    { type: 'spirit', weight: 5 },
  ],
  allowedRoles: [
    { type: 'sect', weight: 80 },
    { type: 'profession', weight: 15 },
    { type: 'combat', weight: 5 },
  ],
  levelRange: { min: 1, max: 8 },
  lootConfig: { dropRate: 0.05, lootTable: [] }, // В секте нет лута с NPC
};
```

---

## Сервисы

### SessionNPCManager (новый)

```typescript
/**
 * Менеджер временных NPC для сессии
 * Хранит статистов в оперативной памяти
 */
class SessionNPCManager {
  // Хранилище: sessionId -> locationId -> TempNPC[]
  private npcs: Map<string, Map<string, TempNPC[]>> = new Map();
  
  /**
   * Инициализация локации
   * Генерирует статистов при входе игрока
   */
  async initializeLocation(
    sessionId: string,
    locationId: string,
    config: LocationNPCConfig,
    playerLevel: number
  ): Promise<TempNPC[]> {
    // 1. Проверяем, уже инициализирована?
    if (this.getLocationNPCs(sessionId, locationId).length > 0) {
      return this.getLocationNPCs(sessionId, locationId);
    }
    
    // 2. Рассчитываем количество
    const count = this.calculatePopulation(config);
    
    // 3. Генерируем N статистов
    const npcs: TempNPC[] = [];
    for (let i = 0; i < count; i++) {
      const npc = await this.generateTempNPC(config, playerLevel);
      npcs.push(npc);
    }
    
    // 4. Сохраняем в память
    this.setLocationNPCs(sessionId, locationId, npcs);
    
    return npcs;
  }
  
  /**
   * Генерация временного NPC
   */
  private async generateTempNPC(
    config: LocationNPCConfig,
    playerLevel: number
  ): Promise<TempNPC> {
    // 1. Выбор вида и роли по весам
    const speciesType = this.weightedRandom(config.allowedSpecies);
    const roleType = this.weightedRandom(config.allowedRoles);
    
    // 2. Генерация уровня
    const level = this.generateLevel(config.levelRange, playerLevel);
    
    // 3. Генерация через существующий генератор
    const context: NPCGenerationContext = {
      speciesType,
      roleType,
      cultivationLevel: level,
      seed: Date.now() + Math.random() * 10000,
    };
    
    const baseNPC = generateNPC(context);
    
    // 4. Преобразование в TempNPC
    const tempNPC: TempNPC = {
      id: `TEMP_${Date.now().toString(36)}`,
      runtimeId: Symbol('temp-npc'),
      speciesId: baseNPC.speciesId,
      roleId: baseNPC.roleId,
      name: baseNPC.name,
      gender: baseNPC.gender,
      age: baseNPC.age,
      stats: baseNPC.stats,
      cultivation: baseNPC.cultivation,
      bodyState: baseNPC.bodyState,
      equipment: await this.generateEquipment(baseNPC),
      quickSlots: await this.generateQuickSlots(baseNPC),
      techniques: baseNPC.techniques,
      personality: {
        disposition: 50 + (Math.random() * 100 - 50),
        aggressionLevel: this.calculateAggression(baseNPC.roleId),
        fleeThreshold: 20 + Math.random() * 30,
      },
      locationId: '',
    };
    
    return tempNPC;
  }
  
  /**
   * Генерация экипировки для статиста
   */
  private async generateEquipment(npc: GeneratedNPC): Promise<Map<string, TempItem>> {
    const equipment = new Map<string, TempItem>();
    
    // Из role.presets.equipment берём категории
    // Генерируем случайные предметы соответствующего уровня
    // НЕ сохраняем в базу
    
    return equipment;
  }
  
  /**
   * Генерация быстрых слотов (расходники)
   */
  private async generateQuickSlots(npc: GeneratedNPC): Promise<(TempItem | null)[]> {
    // 1-3 слота с расходниками
    // Из пула consumables, но без сохранения
    return [];
  }
  
  /**
   * Очистка локации при выходе
   */
  clearLocation(sessionId: string, locationId: string): void {
    const sessionMap = this.npcs.get(sessionId);
    if (sessionMap) {
      sessionMap.delete(locationId);
    }
  }
  
  /**
   * Удаление мёртвого NPC
   */
  removeNPC(sessionId: string, npcId: string): { loot: TempItem[] } | null {
    // Находим и удаляем NPC
    // Возвращаем лут
    return null;
  }
  
  /**
   * Полная очистка сессии
   */
  clearSession(sessionId: string): void {
    this.npcs.delete(sessionId);
  }
}
```

---

## Интеграция с существующими системами

### 1. Система перемещения

```typescript
// В movement handler
async function handleLocationChange(
  sessionId: string,
  fromLocationId: string,
  toLocationId: string
) {
  // 1. Очистка статистов в старой локации
  sessionNPCManager.clearLocation(sessionId, fromLocationId);
  
  // 2. Инициализация новой локации
  const location = await getLocation(toLocationId);
  const playerLevel = await getPlayerLevel(sessionId);
  const npcs = await sessionNPCManager.initializeLocation(
    sessionId,
    toLocationId,
    location.npcConfig,
    playerLevel
  );
  
  // 3. Отправка клиенту
  return { location, npcs };
}
```

### 2. Боевая система

```typescript
// В combat handler
async function handleCombat(
  sessionId: string,
  targetId: string
) {
  // Определяем тип NPC
  if (targetId.startsWith('TEMP_')) {
    // Это статист
    const npc = sessionNPCManager.getNPC(sessionId, targetId);
    
    // Бой...
    
    if (npc.bodyState.isDead) {
      // Удаление + лут
      const result = sessionNPCManager.removeNPC(sessionId, targetId);
      return { victory: true, loot: result?.loot };
    }
  } else {
    // Это персистентный NPC - обычная логика
  }
}
```

### 3. Интеграция с генераторами

```typescript
// Использование существующих генераторов
import { generateWeapon, generateArmor, generateConsumable } from '@/lib/generator';
import { generatedObjectsLoader } from '@/lib/generator/generated-objects-loader';

// В SessionNPCManager.generateEquipment()
async function generateEquipmentForTempNPC(
  npc: TempNPC,
  role: RolePreset
): Promise<Map<string, TempItem>> {
  const equipment = new Map<string, TempItem>();
  
  // 1. Загружаем сгенерированные предметы (если есть)
  const weapons = await generatedObjectsLoader.loadObjects('weapons');
  const armors = await generatedObjectsLoader.loadObjects('armor');
  
  // 2. Фильтруем по уровню
  const suitableWeapons = weapons.filter(w => 
    (w as any).level <= npc.cultivation.level
  );
  
  // 3. Или генерируем на лету
  if (suitableWeapons.length === 0) {
    const weapon = generateWeapon({
      level: npc.cultivation.level,
      rarity: this.randomRarity(),
    });
    equipment.set('weapon', this.toTempItem(weapon));
  } else {
    // Выбираем случайный
    const weapon = this.randomChoice(suitableWeapons);
    equipment.set('weapon', this.toTempItem(weapon));
  }
  
  return equipment;
}
```

---

## Оценка сложности

### Задачи и оценки времени

| # | Задача | Сложность | Время | Приоритет |
|---|--------|-----------|-------|-----------|
| 1 | SessionNPCManager класс | Средняя | 4-6ч | Высокий |
| 2 | TempNPC интерфейсы | Низкая | 1-2ч | Высокий |
| 3 | Интеграция с LocationConfig | Средняя | 3-4ч | Высокий |
| 4 | Генерация экипировки | Средняя | 3-4ч | Средний |
| 5 | Генерация быстрых слотов | Низкая | 2-3ч | Средний |
| 6 | Интеграция с боевой системой | Высокая | 6-8ч | Высокий |
| 7 | Интеграция с перемещением | Средняя | 3-4ч | Высокий |
| 8 | Система лута | Средняя | 4-6ч | Средний |
| 9 | UI отображение статистов | Средняя | 3-4ч | Низкий |
| 10 | Тестирование | Средняя | 4-6ч | Высокий |

**Итого: 33-47 часов работы**

### Зависимости

```
1. TempNPC интерфейсы ─────┐
                           │
2. SessionNPCManager ◄─────┤
   │                       │
   ├──► 3. LocationConfig  │
   │                       │
   ├──► 4. Генерация equip │
   │    │                  │
   │    └──► 5. QuickSlots │
   │                       │
   └──► 6. Боевая система ◄┘
        │
        └──► 8. Система лута
        │
7. Перемещение ─────────────┘
        │
        └──► 9. UI
        │
10. Тестирование ◄──────────┘
```

---

## Рекомендации по реализации

### Фаза 1: MVP (Минимальный жизнеспособный продукт)
1. TempNPC интерфейсы
2. Базовый SessionNPCManager
3. Простой конфиг локации
4. Интеграция с перемещением

### Фаза 2: Боевая интеграция
5. Боевая система + статисты
6. Система лута
7. Генерация экипировки

### Фаза 3: Полировка
8. Быстрые слоты
9. UI отображение
10. Оптимизация и тестирование

---

## Вопросы для уточнения

1. **Лут**: Какую систему лута вы хотите?
   - Дроп с расходников из быстрых слотов?
   - Отдельная таблица лута?
   - Генерация при смерти?

2. **Сохранение**: Нужно ли сохранять состояние статистов между сессиями игры?
   - Если да - в worldState как JSON?
   - Если нет - при перезаходе генерировать заново?

3. **Именные NPC**: Как отличать важных NPC от статистов?
   - Флаг isImportant в EncounteredEntity?
   - Отдельная таблица NamedNPC?
   - Через персистентные NPC (Prisma)?

4. **Агрессия**: Как работает система агрессии?
   - По умолчанию нейтральны?
   - Зависит от роли?
   - Зависит от фракции/секты?

5. **Боевые слоты**: Сколько слотов у статиста?
   - Как у игрока (3 + уровень)?
   - Фиксированное количество?
   - Зависит от роли?

---

## Пример использования

```typescript
// Клиентский код
const sessionManager = new SessionNPCManager();

// При входе в локацию
app.post('/api/game/enter-location', async (req, res) => {
  const { sessionId, locationId } = req.body;
  
  // 1. Получаем конфиг локации
  const location = await db.location.findUnique({
    where: { id: locationId },
  });
  
  const config = JSON.parse(location.properties || '{}').npcConfig || defaultConfig;
  
  // 2. Получаем уровень игрока
  const player = await getPlayer(sessionId);
  
  // 3. Инициализируем статистов
  const npcs = await sessionManager.initializeLocation(
    sessionId,
    locationId,
    config,
    player.cultivationLevel
  );
  
  // 4. Отправляем клиенту
  res.json({
    location,
    npcs: npcs.map(n => ({
      id: n.id,
      name: n.name,
      speciesId: n.speciesId,
      level: n.cultivation.level,
      disposition: n.personality.disposition,
    })),
  });
});

// При смерти NPC
app.post('/api/combat/npc-death', async (req, res) => {
  const { sessionId, npcId } = req.body;
  
  if (npcId.startsWith('TEMP_')) {
    const result = sessionManager.removeNPC(sessionId, npcId);
    res.json({ loot: result?.loot || [] });
  } else {
    // Персистентный NPC - обычная логика смерти
  }
});
```
