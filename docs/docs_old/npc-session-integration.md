# NPC в игровой сессии - Схема интеграции

**Версия:** 2.1 | **Дата:** 2026-03-22 15:40 UTC | **Статус:** ✅ Реализовано

---

## 🆕 Обновления v2.1 (2026-03-22)

- ✅ **V2 Generator Migration** — npc-full-generator использует V2 техники
- ✅ **technique-compat.ts** — конвертер V2→V1 для TempNPC.techniqueData
- ✅ **Material/Morphology** — bodyMaterial и morphology в TempBodyState
- ✅ **Material Damage Reduction** — интегрировано в Event Bus combat handler
- ✅ **Level Suppression** — работает для TempNPC
- ✅ **Qi Buffer 90%** — работает для TempNPC
- ✅ **Ultimate-техники** — 5% шанс для transcendent, маркер ⚡

## 🆕 Обновления v2.0 (2026-03-13)

- ✅ **SoulEntity Compatibility** — TempNPC совместим с архитектурой SoulEntity
- ✅ **Combat Integration** — TempNPC участвуют в бою через Event Bus
- ✅ **AI Behavior** — NPCAIController для поведения (idle/patrol/chase/attack/flee)
- ✅ **Wave System** — WaveManager для Training Ground (опционально)
- ✅ **Validation** — Zod схемы для GeneratedNPC
- ✅ **Tests** — 28 unit тестов

---

## 📋 Обзор

Документ описывает схему добавления NPC в игровую сессию движка. Система поддерживает два типа NPC:

1. **Предварительно созданные NPC (Preset NPCs)** — уникальные персонажи с фиксированной историей
2. **Случайно сгенерированные NPC (Generated NPCs)** — процедурные "статисты" для наполнения мира

---

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ИГРОВАЯ СЕССИЯ                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                 NPC MANAGER (Singleton)                          │   │
│   │                                                                  │   │
│   │   ┌──────────────────┐      ┌──────────────────┐               │   │
│   │   │   Preset NPCs    │      │  Generated NPCs  │               │   │
│   │   │   (в базе)       │      │  (в памяти)      │               │   │
│   │   │                  │      │                  │               │   │
│   │   │  - Уникальные    │      │  - Статисты     │               │   │
│   │   │  - История       │      │  - Временные    │               │   │
│   │   │  - Квесты        │      │  - Без истории  │               │   │
│   │   │  - Персистентные │      │  - Удаляются    │               │   │
│   │   └────────┬─────────┘      └────────┬─────────┘               │   │
│   │            │                         │                          │   │
│   │            └────────────┬────────────┘                          │   │
│   │                         │                                       │   │
│   │                         ▼                                       │   │
│   │            ┌───────────────────────┐                           │   │
│   │            │   Session Location    │                           │   │
│   │            │   (активные NPC)      │                           │   │
│   │            └───────────────────────┘                           │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Типы NPC

### 1. Preset NPCs (Предустановленные)

**Характеристики:**
- Уникальное имя и предыстория
- Фиксированные характеристики
- Принадлежность к секте/фракции
- Квестовые цепочки
- Сохраняются в БД (Prisma NPC model)

**Источник:**
- Создаются вручную или через генератор и сохраняются как пресеты
- Хранятся в `presets/npcs/preset/` как JSON файлы
- Загружаются в БД при инициализации сессии

**ID формат:** `NPC_PRESET_XXXXX`

**Пример структуры:**
```typescript
interface PresetNPC {
  id: string;                      // NPC_PRESET_00001
  isPreset: true;
  name: string;                    // "Мастер Фэн"
  title?: string;                  // "Старейшина Секты Небесного Меча"
  
  // Биография
  backstory: string;
  personality: PersonalityPreset;
  
  // Характеристики (фиксированные)
  stats: {
    strength: number;
    agility: number;
    intelligence: number;
    conductivity: number;
  };
  
  // Культивация
  cultivation: {
    level: number;
    subLevel: number;
    coreCapacity: number;
    currentQi: number;
  };
  
  // Принадлежность
  sectId?: string;
  sectRole?: string;               // elder, sect_master, instructor
  factionId?: string;
  
  // Техники
  techniques: string[];            // ID техник
  
  // Экипировка
  equipment: {
    weapon?: string;
    armor?: string;
    accessories?: string[];
  };
  
  // Квесты
  quests?: QuestReference[];
  
  // Отношения
  relations: Record<string, number>;  // { targetId: disposition }
}
```

### 2. Generated NPCs (Сгенерированные статисты)

**Характеристики:**
- Процедурная генерация через `npc-generator.ts`
- Существуют только в памяти сессии
- Удаляются при выходе из локации или смерти
- Минимальная память (нет истории)

**Источник:**
- Генерируются на лету через `SessionNPCManager`
- Используют формулы из `lore-formulas.ts`

**ID формат:** `TEMP_XXXXXX`

**Реализация:** `TempNPC` interface в `src/types/temp-npc.ts`

**✅ SoulEntity Compatibility (v2.0):**
```typescript
interface TempNPC {
  // SoulEntity fields
  soulType: 'character' | 'creature' | 'spirit' | 'construct';
  controller: 'ai';
  mind: 'full' | 'instinct' | 'simple';
  // ...остальные поля
}
```

**Species → Soul Mapping:**
| speciesType | soulType | mind |
|-------------|----------|------|
| humanoid | character | full |
| beast | creature | instinct |
| spirit | spirit | full |
| hybrid | character | full |
| aberration | construct | simple |

---

## ⚔️ Боевая интеграция (v2.0)

### Event Bus Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        EVENT BUS                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Phaser Scene                Truth System                       │
│       │                           │                              │
│       │  combat:attack            │                              │
│       │ ─────────────────────────► │                              │
│       │                           │                              │
│       │  isTempNPCId(targetId)?   │                              │
│       │                           │                              │
│       │  handleTempNPCCombat()    │                              │
│       │                           │                              │
│       │  combat:result            │                              │
│       │ ◄───────────────────────── │                              │
│       │                           │                              │
│       │  npc:move                 │                              │
│       │ ◄───────────────────────── │  (AI Controller)            │
│       │                           │                              │
│       │  temp_npc:death           │                              │
│       │ ─────────────────────────► │                              │
│       │                           │                              │
│       │  loot, xp                 │                              │
│       │ ◄───────────────────────── │                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Combat Handler Integration

```typescript
// src/lib/game/event-bus/handlers/combat.ts
import { isTempNPCId, handleTempNPCCombat } from '@/lib/game/skeleton/temp-npc-combat';

export async function handleDamageDealt(params: DamageParams) {
  const { targetId, sessionId, damage } = params;
  
  // Роутинг на TempNPC handler
  if (isTempNPCId(targetId)) {
    return await handleTempNPCCombat({
      sessionId,
      npcId: targetId,
      damage,
      techniqueId: params.techniqueId,
    });
  }
  
  // Обычная логика для персистентных NPC
  // ...
}
```

### AI Behavior

**Файл:** `src/lib/game/npc-ai.ts`

**Состояния ИИ:**
- `idle` — стоит на месте
- `patrol` — патрулирует точки
- `chase` — преследует игрока
- `attack` — атакует
- `flee` — бежит при низком HP

**Event Bus события:**
- `npc:move` — движение NPC
- `npc:attack` — атака NPC
- `npc:state_change` — смена состояния
- `npc_ai:tick` — тик обновления ИИ

### Wave Manager (опционально)

**Файл:** `src/lib/game/wave-manager.ts`

Для Training Ground — система волн с нарастающей сложностью.

---

## 🔄 Жизненный цикл NPC

### Preset NPC

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Создание  │────▶│  Сохранение │────▶│   Загрузка  │────▶│   Активный  │
│   (preset)  │     │   в БД      │     │   в сессию  │     │   в мире    │
└─────────────┘     └─────────────┘     └─────────────┘     └──────┬──────┘
                                                                   │
                                                                   ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Удаление  │◀────│  Выгрузка   │◀────│ Сохранение  │◀────│   Смена     │
│   из БД     │     │   из памяти │     │   в БД      │     │   локации   │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
```

### Generated NPC (TempNPC)

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Генерация │────▶│   Активный  │────▶│   Удаление  │
│  (on-demand)│     │   в памяти  │     │  (смерть/   │
└─────────────┘     └─────────────┘     │   выход)    │
                                        └─────────────┘
```

---

## 🎮 API для работы с NPC

### Эндпоинты

| Эндпоинт | Метод | Описание |
|----------|-------|----------|
| `/api/npc/spawn` | POST | Спавн NPC в локацию |
| `/api/npc/list` | GET | Список NPC в локации |
| `/api/npc/get` | GET | Данные конкретного NPC |
| `/api/npc/update` | POST | Обновление NPC |
| `/api/npc/remove` | POST | Удаление NPC (с лутом) |

### Спавн NPC

```typescript
// POST /api/npc/spawn
interface SpawnNPCRequest {
  sessionId: string;
  locationId: string;
  
  // Для preset NPC
  presetId?: string;               // ID пресета
  
  // Для generated NPC
  generateConfig?: {
    count?: number;                // Количество (1-10)
    speciesType?: SpeciesType;
    roleType?: string;
    levelRange?: { min: number; max: number };
  };
  
  // Общие параметры
  position?: { x: number; y: number };
  factionId?: string;
}

interface SpawnNPCResponse {
  success: boolean;
  npcs: (PresetNPC | TempNPC)[];
  error?: string;
}
```

---

## 📁 Структура хранения

### Preset NPCs

```
presets/
└── npcs/
    ├── preset/
    │   ├── story_characters.json    # Сюжетные персонажи
    │   ├── sect_elders.json         # Старейшины сект
    │   ├── merchants.json           # Торговцы
    │   └── quest_givers.json        # Квестодатели
    └── generated/
        └── (временные файлы после генерации)
```

### Session NPCs (память)

```typescript
// SessionNPCManager singleton
{
  npcs: Map<sessionId, Map<locationId, TempNPC[]>>
}
```

---

## 🔧 Интеграция с TruthSystem

### Добавление Preset NPC в сессию

```typescript
// 1. Загрузка пресета
const preset = await presetStorage.loadNPCPreset(presetId);

// 2. Создание в БД
const dbNPC = await db.nPC.create({
  data: {
    sessionId,
    name: preset.name,
    title: preset.title,
    age: preset.age || 30,
    cultivationLevel: preset.cultivation.level,
    cultivationSubLevel: preset.cultivation.subLevel,
    coreCapacity: preset.cultivation.coreCapacity,
    currentQi: preset.cultivation.currentQi,
    strength: preset.stats.strength,
    agility: preset.stats.agility,
    intelligence: preset.stats.intelligence,
    conductivity: preset.stats.conductivity,
    personality: JSON.stringify(preset.personality),
    sectId: preset.sectId,
    role: preset.sectRole,
    locationId,
  }
});

// 3. Добавление в TruthSystem (если нужно)
TruthSystem.registerNPC(sessionId, dbNPC);
```

### Генерация Temporary NPC

```typescript
// Через SessionNPCManager
const manager = getSessionNPCManager();

const npcs = await manager.initializeLocation(
  sessionId,
  locationId,
  'village',  // пресет локации
  playerLevel
);
```

---

## 🎯 Алгоритм выбора типа NPC

```
┌─────────────────────────────────────────────────────────────┐
│                    ЗАПРОС НА СПАВН NPC                       │
└─────────────────────────────┬───────────────────────────────┘
                              │
                              ▼
                  ┌───────────────────────┐
                  │  Указан presetId?     │
                  └───────────┬───────────┘
                              │
              ┌───────────────┴───────────────┐
              │ YES                           │ NO
              ▼                               ▼
    ┌─────────────────┐            ┌─────────────────┐
    │ Загрузить       │            │ Генерировать    │
    │ Preset NPC      │            │ TempNPC         │
    │ из пресета      │            │ через генератор │
    └────────┬────────┘            └────────┬────────┘
             │                              │
             ▼                              ▼
    ┌─────────────────┐            ┌─────────────────┐
    │ Сохранить в БД  │            │ Хранить только  │
    │ (Prisma NPC)    │            │ в памяти сессии │
    └────────┬────────┘            └────────┬────────┘
             │                              │
             └──────────────┬───────────────┘
                            │
                            ▼
              ┌─────────────────────────┐
              │ Добавить в активную     │
              │ локацию сессии          │
              └─────────────────────────┘
```

---

## 📝 Рекомендации по использованию

### Когда использовать Preset NPC:
- Сюжетные персонажи (старейшины, мастера, антагонисты)
- Торговцы с уникальным ассортиментом
- Квестодатели
- Учителя техник
- Лидеры сект/фракций

### Когда использовать Generated NPC:
- Наполнение локаций (жители деревни, горожане)
- Случайные путники
- Монстры в дикой местности
- Враги в подземельях
- Фоновые персонажи

---

## 🔄 Конвертация TempNPC → Preset NPC

Если временный NPC стал важным для сюжета:

```typescript
async function promoteToPreset(
  tempNPC: TempNPC,
  sessionId: string
): Promise<string> {
  // 1. Создаём пресет
  const preset: PresetNPC = {
    id: generatePresetId(),
    isPreset: true,
    name: tempNPC.name,
    stats: tempNPC.stats,
    cultivation: tempNPC.cultivation,
    personality: tempNPC.personality,
    // ... остальные поля
  };
  
  // 2. Сохраняем пресет
  await presetStorage.saveNPCPreset(preset);
  
  // 3. Создаём в БД
  const dbNPC = await db.nPC.create({ ... });
  
  // 4. Удаляем из временных
  sessionNPCManager.removeNPC(sessionId, tempNPC.id);
  
  return preset.id;
}
```

---

## 🚀 Следующие шаги

1. **Создать API `/api/npc/spawn`** — точка входа для спавна NPC
2. **Создать пресеты сюжетных NPC** — master_feng, sect_elder, etc.
3. **Интегрировать с TruthSystem** — кэширование активных NPC
4. **Добавить UI для просмотра NPC** — расширить NPCViewerDialog
