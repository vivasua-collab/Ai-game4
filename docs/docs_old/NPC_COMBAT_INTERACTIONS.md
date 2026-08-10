# 🎮 NPC Combat Interactions - Design Document

**Версия:** 3.0  
**Дата:** 2026-03-22  
**Статус:** 📋 Добавлена система подавления уровнем

---

## 📋 Обзор

Документ описывает архитектуру боевых взаимодействий между игроком и NPC в мире культивации.

### Изменения в v3.0

| # | Изменение | Статус |
|---|-----------|--------|
| 0.1 | Бонусы и штрафы экипировки | ✅ Бонусы реализованы, штрафы продуманы |
| 1.3 | Хитбоксы по типу тела | ✅ Использована система из body.md |
| 2.1 | Урон от NPC "равноценный" игроку | ✅ Спроектировано |
| 2.3 | Агрессия NPC | 🔜 Подготовка вариантов |
| 2.4 | ⭐ Подавление уровнем для NPC | 📋 Спроектировано (NEW v3.0) |
| 3.x | Диалоги: 2 схемы | 📋 Спроектировано |
| 4.x | Квесты расширенные | 📋 Спроектировано |
| 5.x | Лут → Фаза 2 | 📋 Спроектировано |

---

## 🏗️ Архитектура системы

```
┌─────────────────────────────────────────────────────────────────────┐
│                    PHASER ENGINE (Client)                            │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  1. COLLISION DETECTION (Arcade Physics)                       │ │
│  │     ├─ player ↔ npc collision                                  │ │
│  │     ├─ technique hitbox ↔ npc collision                        │ │
│  │     └─ npc ↔ environment collision                             │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                              │                                       │
│                              ▼                                       │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  2. EVENT BUS (event-bus/client.ts)                            │ │
│  │     ├─ npc:collision { playerId, npcId, type }                 │ │
│  │     ├─ combat:damage { attackerId, targetId, damage, type }    │ │
│  │     ├─ npc:death { npcId, killerId }                           │ │
│  │     └─ npc:dialog { npcId, playerId, dialogId }                │ │
│  └────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ HTTP POST /api/game/event
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    SERVER (Next.js API)                              │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  3. TRUTH SYSTEM (truth-system.ts)                             │ │
│  │     ├─ Единое место расчётов                                   │ │
│  │     ├─ Память первична, БД вторична                            │ │
│  │     └─ Атомарные операции                                      │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                              │                                       │
│                              ▼                                       │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  4. DATABASE (Prisma)                                          │ │
│  │     ├─ NPC.update({ hp, state, equipment })                    │ │
│  │     ├─ Inventory.create({ items from loot })                   │ │
│  │     └─ Quest.update({ status, progress })                      │ │
│  └────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 0️⃣ Бонусы и штрафы экипировки

> **📋 Документация:** Полная система бонусов и штрафов описана в **[docs/bonuses.md](./bonuses.md)**

### 0.1 Принцип Единых Переменных

**Ключевая идея:** Бонусы и штрафы используют **одни и те же переменные**:
- Бонусы → положительные значения (`+10%`)
- Штрафы → отрицательные значения (`-15%`)

```typescript
// Примеры модификаторов
{ variableId: 'speed', value: +20 }  // Бонус скорости +20%
{ variableId: 'speed', value: -15 }  // Штраф скорости -15%
```

### 0.2 Реализовано — ✅

| Компонент | Файл | Статус |
|-----------|------|--------|
| Типы бонусов | `src/types/bonus-registry.ts` | ✅ |
| Runtime реестр | `src/lib/data/bonus-registry-runtime.ts` | ✅ |
| Генератор экипировки | `src/lib/generator/equipment-generator-v2.ts` | ✅ |

### 0.3 Требуется проектирование — 📋

| Компонент | Файл | Описание |
|-----------|------|----------|
| Калькулятор модификаторов | `src/lib/game/modifier-calculator.ts` | Расчёт итоговых значений |
| Штрафы от веса | `src/lib/game/equipment-penalties.ts` | Перегруз → -скорость |
| Штрафы от материала | `materials-registry.ts` | Тяжёлый металл → -скорость |
| Штрафы от durability | `equipment-penalties.ts` | Повреждения → -эффективность |

### 0.4 Переменные с односторонними модификаторами

Некоторые переменные **не могут** иметь противоположные значения:

| Только положительные | Только отрицательные | Не модифицируются |
|---------------------|---------------------|-------------------|
| `armor`, `qi_regen` | `damage_taken` | `strength`, `agility` |
| `life_steal`, `thorns` | `debuff_duration` | `qi_conductivity` |
| `luck`, `exp_bonus` | `fall_damage` | `core_capacity` |

**Подробности в [docs/bonuses.md](./bonuses.md)**, разделы 1.2-1.4

---

## 1️⃣ Система коллизий с NPC

### 1.1 Типы коллизий

| Тип | Описание | Действие |
|-----|----------|----------|
| `player_touch` | Игрок касается NPC | Открыть диалог / инициировать бой |
| `technique_hit` | Техника попадает по NPC | Нанести урон |
| `npc_attack` | NPC атакует игрока | Нанести урон игроку |
| `environment` | NPC сталкивается с препятствием | Изменить путь (pathfinding) |

### 1.2 Реализация в Phaser

```typescript
// src/game/objects/NPCSprite.ts
interface NPCSpriteConfig {
  id: string;
  name: string;
  x: number;
  y: number;
  hitboxRadius: number;
  faction: 'friendly' | 'neutral' | 'hostile';
  aggroRange: number;       // Радиус агрессии (пиксели)
  attackRange: number;      // Радиус атаки
}

class NPCSprite extends Phaser.Physics.Arcade.Sprite {
  public npcId: string;
  public faction: 'friendly' | 'neutral' | 'hostile';
  
  constructor(scene: Phaser.Scene, config: NPCSpriteConfig) {
    super(scene, config.x, config.y, 'npc_texture');
    
    // Физика
    scene.physics.add.existing(this);
    this.body.setCircle(config.hitboxRadius);
    this.body.setOffset(-config.hitboxRadius, -config.hitboxRadius);
    
    // Свойства
    this.npcId = config.id;
    this.faction = config.faction;
    
    // Настройка коллизий
    this.setupCollisions(scene, config);
  }
  
  private setupCollisions(scene: Phaser.Scene, config: NPCSpriteConfig) {
    // Коллизия с игроком
    scene.physics.add.overlap(
      scene.player,
      this,
      this.onPlayerCollision,
      undefined,
      this
    );
    
    // Коллизия с техниками
    scene.physics.add.overlap(
      scene.techniqueProjectiles,
      this,
      this.onTechniqueHit,
      undefined,
      this
    );
  }
  
  private onPlayerCollision(player: PlayerSprite, npc: NPCSprite) {
    eventBusClient.emit('npc:collision', {
      playerId: player.playerId,
      npcId: npc.npcId,
      type: npc.faction === 'hostile' ? 'combat_initiate' : 'dialog_initiate',
      position: { x: player.x, y: player.y }
    });
  }
  
  private onTechniqueHit(projectile: TechniqueProjectile, npc: NPCSprite) {
    eventBusClient.emit('combat:damage', {
      attackerId: projectile.ownerId,
      targetId: npc.npcId,
      techniqueId: projectile.techniqueId,
      damage: projectile.damage,
      type: projectile.damageType,
      position: { x: npc.x, y: npc.y }
    });
  }
}
```

### 1.3 Константы коллизий — по типу тела

**Используется система из `docs/body.md` и `src/types/body.ts`:**

```typescript
// src/game/constants/collision.ts

import type { BodyPartType, SizeClass } from '@/types/body';

/**
 * Множители размера для хитбоксов
 * 
 * Из docs/body.md:
 * | Класс | Высота | Множитель силы |
 * |-------|--------|----------------|
 * | tiny | < 30 см | 0.1x |
 * | small | 30-60 см | 0.3x |
 * | medium | 60-180 см | 1.0x |
 * | large | 1.8-3 м | 2.0x |
 * | huge | 3-10 м | 5.0x |
 * | gargantuan | 10-30 м | 15.0x |
 * | colossal | 30+ м | 50.0x |
 */
export const SIZE_CLASS_MULTIPLIERS: Record<SizeClass, number> = {
  tiny: 0.1,
  small: 0.3,
  medium: 1.0,
  large: 2.0,
  huge: 5.0,
  gargantuan: 15.0,
  colossal: 50.0,
};

/**
 * Радиусы хитбоксов по размеру (в метрах)
 * 
 * Из body-system.ts - hitboxRadius для частей тела:
 * - head: 0.15 м
 * - torso: 0.30 м
 * - arm: 0.08 м
 * - hand: 0.05 м
 * - leg: 0.10 м
 * - foot: 0.05 м
 */
export const HITBOX_RADIUS_BY_SIZE: Record<SizeClass, number> = {
  tiny: 0.1,        // 10 см
  small: 0.2,       // 20 см
  medium: 0.3,      // 30 см (базовый человек)
  large: 0.6,       // 60 см
  huge: 1.2,        // 1.2 м
  gargantuan: 3.0,  // 3 м
  colossal: 6.0,    // 6 м
};

/**
 * Расчёт хитбокса NPC по виду
 */
export function calculateNPCHitbox(
  sizeClass: SizeClass,
  bodyTemplate: BodyTemplate
): { mainRadius: number; partRadii: Record<string, number> } {
  const sizeMultiplier = SIZE_CLASS_MULTIPLIERS[sizeClass];
  const baseRadius = HITBOX_RADIUS_BY_SIZE[sizeClass];
  
  // Радиусы частей тела из body-system.ts
  const partRadii: Record<string, number> = {};
  
  const basePartRadii: Record<string, number> = {
    head: 0.15,
    torso: 0.30,
    heart: 0.10,
    arm: 0.08,
    hand: 0.05,
    leg: 0.10,
    foot: 0.05,
    wing: 0.12,
    tail: 0.08,
    core: 0.20,
    essence: 0.30,
  };
  
  for (const [part, radius] of Object.entries(basePartRadii)) {
    partRadii[part] = radius * sizeMultiplier;
  }
  
  return {
    mainRadius: baseRadius,
    partRadii,
  };
}

/**
 * Константы коллизий
 */
export const COLLISION_CONSTANTS = {
  // Масштаб
  METERS_TO_PIXELS: 32,
  PIXELS_TO_METERS: 1/32,
  
  // Радиусы агрессии (в метрах)
  AGGRO_RANGE_HOSTILE: 9,      // 9 м
  AGGRO_RANGE_NEUTRAL: 4.5,    // 4.5 м (при провокации)
  AGGRO_RANGE_FRIENDLY: 0,     // Не агрессивны
  
  // Радиусы атак (в метрах)
  ATTACK_RANGE_MELEE: 2,        // 2 м
  ATTACK_RANGE_RANGED: 12.5,   // 12.5 м
};
```

---

## 2️⃣ Система нанесения и получения урона

### 2.1 Урон от игрока к NPC

Использует существующую систему из `combat-system.ts`:

```typescript
// См. src/lib/game/combat-system.ts - calculateTechniqueDamageFull()
// Уже реализовано:
// - qiDensity = 2^(level-1)
// - statScaling по типу техники
// - masteryMultiplier до +30%
// - Бонус от проводимости
```

### 2.2 Урон от NPC к игроку — "Равноценный" с игроком

**Принцип:** NPC использует те же формулы, что и игрок.

```typescript
// src/lib/game/npc-damage-calculator.ts

import {
  calculateTechniqueDamageFull,
  calculateStatScalingByType,
  SCALING_COEFFICIENTS,
} from './combat-system';
import type { Technique, CombatTechniqueType } from '@/types/game';
import type { GeneratedNPC } from '@/lib/generator/npc-generator';

/**
 * Расчёт урона от NPC к игроку
 * 
 * NPC использует те же формулы, что и игрок:
 * - qiDensity = 2^(level-1)
 * - statScaling от характеристик NPC
 * - masteryMultiplier (если NPC владеет техникой)
 * 
 * @see combat-system.ts - calculateTechniqueDamageFull
 */
export function calculateDamageFromNPC(params: {
  npc: GeneratedNPC;
  technique: Technique | null;    // null = базовая атака
  qiSpent: number;
  target: { 
    armor: number; 
    conductivity: number; 
    vitality: number;
    meridianBuffer: number;
  };
}): {
  damage: number;
  qiSpent: number;
  effectiveQi: number;
  qiDensity: number;
  statMultiplier: number;
} {
  const { npc, technique, qiSpent, target } = params;
  
  // 1. Качество Ци NPC (геометрический рост ×2)
  const qiDensity = Math.pow(2, npc.cultivation.level - 1);
  
  // 2. Эффективное Ци
  const effectiveQi = qiSpent; // NPC не страдает от дестабилизации
  
  // 3. Базовый эффект = Ци × Качество
  let effect = effectiveQi * qiDensity;
  
  // 4. Масштабирование от характеристик NPC
  let statMultiplier = 1.0;
  if (technique?.effects?.combatType) {
    statMultiplier = calculateStatScalingByType(
      {
        strength: npc.stats.strength,
        agility: npc.stats.agility,
        intelligence: npc.stats.intelligence,
        conductivity: npc.cultivation.meridianConductivity,
        // ... остальные поля
      } as any,
      technique.effects.combatType as CombatTechniqueType
    );
  } else {
    // Базовая атака — масштабирование от силы
    statMultiplier = 1 + Math.max(0, npc.stats.strength - 10) * 0.05;
  }
  effect *= statMultiplier;
  
  // 5. Мастерство NPC (если есть техника)
  if (technique) {
    const mastery = 50; // Базовое мастерство NPC
    const masteryMultiplier = 1 + (mastery / 100) * 0.3;
    effect *= masteryMultiplier;
  }
  
  // 6. Проводимость NPC (бонус до +20%)
  const conductivityBonus = 1 + (npc.cultivation.meridianConductivity / 100) * 0.2;
  effect *= conductivityBonus;
  
  // 7. Поглощение бронёй игрока
  const armorReduction = target.armor * 0.5;
  let finalDamage = Math.max(1, effect - armorReduction);
  
  // 8. Буфер Ци игрока (до 30% урона)
  const buffered = Math.min(target.meridianBuffer, finalDamage * 0.3);
  finalDamage -= buffered;
  
  return {
    damage: Math.floor(finalDamage),
    qiSpent,
    effectiveQi,
    qiDensity,
    statMultiplier,
  };
}

/**
 * Выбор техники NPC для атаки
 */
export function selectNPCTechnique(
  npc: GeneratedNPC,
  situation: 'melee' | 'ranged' | 'defensive'
): Technique | null {
  const techniques = npc.techniques;
  
  if (techniques.length === 0) return null;
  
  // TODO: Интеграция с загрузчиком техник
  // return await loadTechnique(techniques[0]);
  return null;
}

/**
 * Расчёт Qi, которое NPC готов потратить
 */
export function calculateNPCQiSpent(
  npc: GeneratedNPC,
  technique: Technique | null
): number {
  if (!technique) {
    // Базовая атака — 5-10% от текущего Qi
    return Math.floor(npc.cultivation.currentQi * 0.05);
  }
  
  // Для техники — базовая стоимость
  return technique.qiCost ?? 10;
}
```

### 2.3 ⭐ Подавление уровнем для NPC

**Применяется к обеим сторонам: Игрок → NPC и NPC → Игрок.**

#### 2.3.1 Таблица подавления

```typescript
// src/lib/constants/level-suppression.ts

export const LEVEL_SUPPRESSION_TABLE = {
  0: { normal: 1.0, technique: 1.0, ultimate: 1.0 },
  1: { normal: 0.5, technique: 0.75, ultimate: 1.0 },
  2: { normal: 0.1, technique: 0.25, ultimate: 0.5 },
  3: { normal: 0.0, technique: 0.05, ultimate: 0.25 },
  4: { normal: 0.0, technique: 0.0, ultimate: 0.1 },
  5: { normal: 0.0, technique: 0.0, ultimate: 0.0 },
};
```

#### 2.3.2 Примеры боя

| Сценарий | Уровень атакующего | Уровень защитника | Тип | Множитель |
|----------|-------------------|-------------------|-----|-----------|
| Игрок атакует слабого NPC | L7 | L3 | technique | 1.0 |
| Слабый NPC атакует игрока | L3 | L7 | normal | 0.0 |
| Игрок атакует равного NPC | L5 | L5 | technique | 1.0 |
| NPC босс атакует игрока | L9 | L7 | technique | 1.0 |
| Игрок атакует босса | L7 | L9 | ultimate | 0.1 |

#### 2.3.3 Интеграция в calculateDamageFromNPC

```typescript
export function calculateDamageFromNPC(params: {
  npc: GeneratedNPC;
  technique: Technique | null;
  // ... rest
}): { /* ... */ } {
  const { npc, technique, qiSpent, target } = params;
  
  // ... existing damage calculation ...
  
  // ⭐ НОВОЕ: Подавление уровнем
  const attackType = technique?.isUltimate ? 'ultimate' : 
                     technique ? 'technique' : 'normal';
  const suppression = calculateLevelSuppression(
    npc.cultivation.level,
    target.cultivationLevel,  // Уровень игрока
    attackType,
    technique?.level
  );
  finalDamage *= suppression;
  
  return { damage: Math.floor(finalDamage), /* ... */ };
}
```

### 2.4 Система агрессии NPC — 🔜 ПОДГОТОВКА ВАРИАНТОВ

**Внедрение после отработки основных механик боя.**

#### Вариант A: Конечный автомат (State Machine)

```typescript
// Простая машина состояний
type AIState = 'idle' | 'patrol' | 'alert' | 'chase' | 'attack' | 'flee';

interface SimpleAI {
  state: AIState;
  target: string | null;
  lastStateChange: number;
}
```

#### Вариант B: Utility AI (Взвешенный выбор)

```typescript
// Взвешенная оценка действий
interface AIAction {
  type: 'attack' | 'defend' | 'flee' | 'patrol';
  score: number;
}

function evaluateActions(npc: NPC, context: CombatContext): AIAction[] {
  // Оценка каждого действия по множеству факторов
}
```

#### Вариант C: Behaviour Tree

```typescript
// Дерево поведения
interface BehaviourNode {
  type: 'selector' | 'sequence' | 'action' | 'condition';
  children?: BehaviourNode[];
  action?: () => boolean;
}
```

**Рекомендация:** Начать с Варианта A (State Machine) для MVP.

---

## 3️⃣ Система диалогов с NPC

**Внедрение после реализации боевой системы.**

### 3.1 Две схемы диалогов

| Схема | Тип NPC | Хранение | Особенности |
|-------|---------|----------|-------------|
| **Статичная** | Quest NPC (preset) | JSON файлы | Глубокие деревья, условия, награды |
| **Динамичная** | Temp NPC | Генерация | Упрощённые, процедурные |

### 3.2 Статичные диалоги (Quest NPC)

```typescript
// src/data/dialogs/quest-dialogs.ts

/**
 * Статичный диалог для квестового NPC
 * 
 * Особенности:
 * - Глубокое дерево с ветвлениями
 * - Условия по уровню, квестам, характеристикам
 * - Награды: Qi, предметы, техники, репутация
 * - Действия: старт квеста, телепорт, торговля
 */
export interface QuestDialogTree {
  id: string;
  npcId: string;             // ID пресета NPC (NPC_elder_zhang)
  npcType: 'preset';        // Всегда preset
  
  trigger: 'approach' | 'interact' | 'quest_start' | 'quest_complete';
  rootNodeId: string;
  
  nodes: Record<string, QuestDialogNode>;
  
  // Метаданные
  author?: string;          // Кто написал диалог
  version: string;
  localizedVersions?: string[]; // ['en', 'ru']
}

export interface QuestDialogNode {
  id: string;
  text: string;
  speaker: 'npc' | 'player';
  
  options?: QuestDialogOption[];
  
  // Условия показа узла
  conditions?: DialogCondition[];
  
  // Награды при достижении узла
  rewards?: DialogReward[];
  
  // Действия при достижении узла
  actions?: DialogAction[];
}
```

### 3.3 Динамичные диалоги (Temp NPC)

```typescript
// src/lib/generator/temp-npc-dialog-generator.ts

/**
 * Динамичный диалог для временного NPC
 * 
 * Особенности:
 * - Генерируется на лету
 * - Упрощённая структура
 * - Зависит от роли и личности NPC
 * - Может содержать случайные элементы
 */
export interface TempDialogTree {
  id: string;               // TEMP_DIALOG_xxxxx
  npcId: string;            // TEMP_xxxxx
  npcType: 'temp';          // Всегда temp
  
  // Данные для генерации
  generatedAt: number;
  seed: number;
  
  // Упрощённая структура
  greeting: string;         // Приветствие (генерируется)
  topics: TempDialogTopic[];
}

export interface TempDialogTopic {
  id: string;
  name: string;             // "О секте", "О торговле", "Сплетни"
  
  // Линейные реплики (без ветвлений)
  lines: string[];
  
  // Возможные действия
  actions?: ('trade' | 'rumor' | 'direction')[];
}

/**
 * Генерация диалога для Temp NPC
 */
export function generateTempNPCDialog(
  npc: TempNPC,
  context: { locationId: string; playerLevel: number }
): TempDialogTree {
  const seed = Date.now();
  
  // 1. Генерация приветствия
  const greeting = generateGreeting(npc, context);
  
  // 2. Выбор тем на основе роли NPC
  const topics = selectTopicsForRole(npc.roleId, npc.personality);
  
  // 3. Генерация реплик
  const populatedTopics = topics.map(topic => ({
    ...topic,
    lines: generateLinesForTopic(topic, npc, context),
  }));
  
  return {
    id: `TEMP_DIALOG_${seed}`,
    npcId: npc.id,
    npcType: 'temp',
    generatedAt: Date.now(),
    seed,
    greeting,
    topics: populatedTopics,
  };
}
```

### 3.4 Перевод Temp NPC → Static (Permanent)

```typescript
// src/lib/game/npc-promotion.ts

/**
 * Условия для перевода Temp → Static
 */
export interface PromotionConditions {
  // Игрок взаимодействовал с NPC N раз
  minInteractions: number;
  
  // NPC выполнил определённые действия
  requirements: Array<
    | { type: 'quest_complete'; questId: string }
    | { type: 'trade'; minAmount: number }
    | { type: 'save_player'; count: number }
    | { type: 'join_party'; duration: number } // Пробыл в группе N минут
  >;
  
  // Отношение NPC к игроку
  minDisposition: number;
  
  // Игрок имеет определённый статус
  playerRequirements?: {
    minLevel?: number;
    minReputation?: { factionId: string; amount: number };
    items?: string[];
  };
}

/**
 * Результат продвижения NPC
 */
export interface PromotionResult {
  success: boolean;
  newNpcId: string;       // ID нового пресета
  reason?: string;
}

/**
 * Перевод временного NPC в постоянного
 * 
 * Примеры триггеров:
 * - Принятие в группу → после квеста на лояльность
 * - Спасение NPC → после N спасений
 * - Торговый партнёр → после N сделок
 */
export async function promoteTempToPermanent(
  tempNpcId: string,
  conditions: PromotionConditions
): Promise<PromotionResult> {
  // 1. Проверяем условия
  const npc = await getTempNPC(tempNpcId);
  if (!npc) {
    return { success: false, reason: 'NPC not found' };
  }
  
  // 2. Проверяем историю взаимодействий
  const history = await getNPCInteractionHistory(tempNpcId);
  
  if (history.length < conditions.minInteractions) {
    return { 
      success: false, 
      reason: `Need ${conditions.minInteractions} interactions, have ${history.length}` 
    };
  }
  
  // 3. Проверяем disposition
  if (npc.personality.disposition < conditions.minDisposition) {
    return {
      success: false,
      reason: `Disposition too low: ${npc.personality.disposition} < ${conditions.minDisposition}`,
    };
  }
  
  // 4. Создаём пресет
  const preset = await createPresetFromTemp(npc);
  
  // 5. Сохраняем в БД
  await saveNPCPreset(preset);
  
  // 6. Удаляем из временного хранилища
  await removeTempNPC(tempNpcId);
  
  return {
    success: true,
    newNpcId: preset.id,
  };
}
```

---

## 4️⃣ Система квестов — расширенные варианты

### 4.1 Типы квестов

```typescript
// src/types/quest.ts

export type QuestType = 
  // === Базовые ===
  | 'kill'          // Убить N врагов
  | 'collect'       // Собрать N предметов
  | 'deliver'       // Доставить предмет
  | 'talk'          // Поговорить с NPC
  
  // === Расширенные ===
  | 'escort'        // Сопроводить NPC (с риском)
  | 'defend'        // Защитить локацию (волны)
  | 'explore'       // Исследовать область
  | 'craft'         // Создать предмет
  | 'cultivate'     // Достичь уровня культивации
  
  // === Глубокие ===
  | 'investigation' // Расследование (сбор улик)
  | 'choice'        // Квест с моральным выбором
  | 'chain'         // Цепочка квестов
  | 'timed'         // Квест с таймером
  | 'survival';     // Выжить N времени
```

### 4.2 Генератор квестов

```typescript
// src/lib/generator/quest-generator.ts

import { seededRandom } from './base-item-generator';

/**
 * Контекст генерации квеста
 */
export interface QuestGenerationContext {
  npcId: string;
  npcRole: string;
  locationId: string;
  playerLevel: number;
  difficulty?: 'easy' | 'medium' | 'hard';
  type?: QuestType;
  seed?: number;
}

/**
 * Сгенерированный квест
 */
export interface GeneratedQuest {
  id: string;
  name: string;
  description: string;
  
  type: QuestType;
  difficulty: 'easy' | 'medium' | 'hard' | 'legendary';
  
  objectives: QuestObjective[];
  rewards: QuestRewards;
  
  timeLimit?: number;
  repeatable: boolean;
  
  // Для сгенерированных квестов
  generatedAt: number;
  seed: number;
}

/**
 * Генерация квеста
 * 
 * Идеи для генерации:
 * - Контекст: NPC роль, локация, уровень игрока
 * - Типы: kill, collect, deliver, escort, defend
 * - Сложность: влияет на цели и награды
 */
export function generateQuest(context: QuestGenerationContext): GeneratedQuest {
  const rng = seededRandom(context.seed ?? Date.now());
  
  // 1. Выбор типа квеста
  const type = context.type ?? selectQuestType(context, rng);
  
  // 2. Генерация целей
  const objectives = generateObjectives(type, context, rng);
  
  // 3. Генерация наград
  const rewards = generateRewards(context, objectives, rng);
  
  // 4. Название и описание
  const { name, description } = generateQuestText(type, context, objectives, rng);
  
  return {
    id: `QUEST_gen_${Date.now()}`,
    name,
    description,
    type,
    difficulty: context.difficulty ?? 'medium',
    objectives,
    rewards,
    timeLimit: type === 'timed' ? 30 + Math.floor(rng() * 60) : undefined,
    repeatable: type === 'collect' || type === 'kill',
    generatedAt: Date.now(),
    seed: context.seed ?? Date.now(),
  };
}

/**
 * Шаблоны квестов по типам
 */
const QUEST_TEMPLATES: Record<QuestType, {
  nameTemplates: string[];
  objectiveGenerators: Array<(ctx: QuestGenerationContext, rng: () => number) => QuestObjective>;
}> = {
  kill: {
    nameTemplates: [
      'Охота на {target}',
      'Истребление {target}',
      'Зачистка территории',
    ],
    objectiveGenerators: [
      (ctx, rng) => ({
        id: 'obj_kill',
        type: 'kill',
        target: selectEnemyType(ctx.locationId, rng),
        count: 3 + Math.floor(rng() * 5),
        currentProgress: 0,
        optional: false,
      }),
    ],
  },
  
  // ... другие типы
};

/**
 * Выбор типа квеста по контексту
 */
function selectQuestType(context: QuestGenerationContext, rng: () => number): QuestType {
  // По роли NPC
  const roleWeights: Record<string, Record<QuestType, number>> = {
    elder: { kill: 30, talk: 40, collect: 20, escort: 10 },
    guard: { kill: 50, defend: 30, escort: 20 },
    merchant: { deliver: 50, collect: 30, escort: 20 },
    // ...
  };
  
  // ...
  return 'kill'; // Заглушка
}
```

---

## 5️⃣ Система лута (Full Loot) — Фаза 2

**Перемещено в Фазу 2 (после боевой системы).**

### 5.1 Принцип Full Loot

```
┌─────────────────────────────────────────────────────────────────────┐
│                    FULL LOOT SYSTEM                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  При убийстве NPC игрок получает:                                    │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  1. ВСЯ экипировка NPC (оружие, броня, аксессуары)          │    │
│  │  2. ВСЁ содержимое инвентаря NPC (расходники, материалы)    │    │
│  │  3. Ци-камни (если есть)                                     │    │
│  │  4. Духовные ядра (из существ)                               │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  Ограничения:                                                        │
│  - Вес лута (невозможно унести всё)                                  │
│  - Время сбора (риск быть атакованным)                               │
│  - Качество предметов (повреждены в бою)                             │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.2 Генерация лута при создании NPC

**Важно:** Экипировка генерируется при создании NPC, а не при смерти!

```typescript
// Уже реализовано в npc-generator.ts:

export function generateNPC(context: NPCGenerationContext): GeneratedNPC {
  // ...
  
  // 9. Экипировка — генерируется при создании!
  const equipment = generateEquipment(role, cultivation.level, rng);
  
  // 10. Инвентарь — из пула расходников
  // Будет заполнено через generateInventoryFromPool
  inventory: [],
  
  // ...
}

// Инвентарь — случайное наполнение
async function generateInventoryFromPool(npc: GeneratedNPC) {
  // Выбирает 1-3 расходника из пула
  // Количество: 1-3 штуки каждого
}
```

### 5.3 Перевод временного эквипа в постоянный

```typescript
// src/lib/game/loot-permanence.ts

/**
 * Перевод временного предмета в постоянный (сохранение в БД)
 * 
 * При сборе лута с убитого NPC:
 * 1. Проверяем, временный ли предмет (TEMP_ITEM_xxxxx)
 * 2. Если да — создаём постоянную запись в InventoryItem
 * 3. Присваиваем новый ID (без TEMP_)
 */
export async function permanentizeLootItem(
  tempItem: TempItem,
  characterId: string
): Promise<InventoryItem> {
  // 1. Генерируем постоянный ID
  const permanentId = tempItem.nameId ?? generatePermanentItemId(tempItem);
  
  // 2. Создаём запись в БД
  const permanent = await db.inventoryItem.create({
    data: {
      id: permanentId,
      name: tempItem.name,
      nameId: tempItem.nameId,
      type: tempItem.type,
      category: tempItem.category,
      rarity: tempItem.rarity,
      icon: tempItem.icon,
      
      // Статы
      stats: JSON.stringify(tempItem.stats),
      effects: JSON.stringify(tempItem.effects ?? []),
      
      // Прочность (для экипировки)
      durability: tempItem.stats.defense ? 100 : null,
      maxDurability: tempItem.stats.defense ? 100 : null,
      
      // Владелец
      characterId,
      
      // Стоимость
      value: tempItem.value ?? calculateItemValue(tempItem),
    },
  });
  
  return permanent;
}

/**
 * Сбор лута с NPC
 */
export async function collectLootFromNPC(
  npcId: string,
  characterId: string
): Promise<{ items: InventoryItem[]; totalWeight: number }> {
  const npc = await getNPC(npcId);
  if (!npc) throw new Error('NPC not found');
  
  const items: InventoryItem[] = [];
  let totalWeight = 0;
  
  // 1. Экипировка
  if (npc.equipment) {
    for (const [slot, tempItem] of Object.entries(npc.equipment)) {
      if (!tempItem) continue;
      
      // Применяем боевые повреждения
      const damagedItem = applyBattleDamage(tempItem, npc.battleDamage ?? 0);
      
      // Делаем постоянным
      const permanent = await permanentizeLootItem(damagedItem, characterId);
      items.push(permanent);
      totalWeight += damagedItem.stats.weight ?? 0;
    }
  }
  
  // 2. Инвентарь
  if (npc.inventory) {
    for (const invItem of npc.inventory) {
      const tempItem = await loadTempItem(invItem.id);
      if (!tempItem) continue;
      
      const permanent = await permanentizeLootItem(tempItem, characterId);
      
      // Устанавливаем количество
      permanent.quantity = invItem.quantity;
      await db.inventoryItem.update({
        where: { id: permanent.id },
        data: { quantity: invItem.quantity },
      });
      
      items.push(permanent);
    }
  }
  
  return { items, totalWeight };
}
```

### 5.4 Гарантированные дропы — Камни душ

**Для тёмного пути:** С NPC можно достать "камни душ".

```typescript
// src/types/soul-stone.ts

/**
 * Камень души
 * 
 * Тёмный путь: извлечение души из убитого NPC
 * 
 * Описание (теория):
 * - Содержит душу убитого практика
 * - Можно использовать для ритуалов
 * - Редкий и ценный ресурс
 */
export interface SoulStone {
  id: string;
  type: 'soul_stone';
  
  // Источник
  sourceNpcId: string;
  sourceNpcName: string;
  sourceCultivationLevel: number;
  
  // Содержимое
  soulQuality: 'faint' | 'dim' | 'bright' | 'radiant' | 'blazing';
  soulPower: number;        // Зависит от уровня культивации
  
  // Использование (будущее)
  uses: Array<
    | 'ritual_summon'      // Призыв духа
    | 'ritual_sacrifice'   // Жертвоприношение для силы
    | 'ritual_binding'     // Привязка к артефакту
    | 'consume_qi'         // Поглощение для получения Ци
  >;
  
  // Карма
  karmaPenalty: number;     // Штраф кармы за использование
}

/**
 * Шанс выпадения камня души
 */
export function calculateSoulStoneDropChance(
  npc: NPC,
  playerKarma: number
): number {
  // Базовый шанс зависит от уровня культивации NPC
  let baseChance = npc.cultivationLevel * 5; // 5-45%
  
  // Модификатор от кармы игрока (тёмный путь = больше шанс)
  const karmaModifier = Math.max(0, -playerKarma) * 0.1; // Негативная карма = бонус
  
  // Модификатор от способа убийства
  // TODO: Добавить определение способа убийства
  
  return Math.min(80, baseChance + karmaModifier);
}

/**
 * Генерация камня души
 */
export function generateSoulStone(npc: NPC): SoulStone | null {
  // Только для практиков уровня 3+
  if (npc.cultivationLevel < 3) return null;
  
  // Только для гуманоидов (с душой)
  if (npc.speciesType !== 'humanoid') return null;
  
  const qualityMap: Record<number, SoulStone['soulQuality']> = {
    3: 'faint',
    4: 'faint',
    5: 'dim',
    6: 'dim',
    7: 'bright',
    8: 'radiant',
    9: 'blazing',
  };
  
  return {
    id: `SOUL_STONE_${Date.now()}`,
    type: 'soul_stone',
    sourceNpcId: npc.id,
    sourceNpcName: npc.name,
    sourceCultivationLevel: npc.cultivationLevel,
    soulQuality: qualityMap[npc.cultivationLevel] ?? 'faint',
    soulPower: npc.cultivationLevel * 100 + npc.cultivation.currentQi,
    uses: ['ritual_summon', 'consume_qi'],
    karmaPenalty: npc.cultivationLevel * 5,
  };
}
```

---

## 📊 План реализации

### Фаза 1: Боевая система (2 недели)

| # | Задача | Приоритет | Дней |
|---|--------|-----------|------|
| 1.1 | NPCSprite с Arcade Physics | 🔴 | 1 |
| 1.2 | Коллизии по типу тела | 🔴 | 1 |
| 1.3 | Урон от игрока к NPC | 🔴 | 2 |
| 1.4 | Урон от NPC к игроку ("равноценный") | 🔴 | 2 |
| 1.5 | Штрафы экипировки | 🟠 | 2 |
| 1.6 | Система лута | 🟠 | 2 |

### Фаза 2: Диалоги и квесты (2 недели)

| # | Задача | Приоритет | Дней |
|---|--------|-----------|------|
| 2.1 | Статичные диалоги (JSON) | 🟠 | 2 |
| 2.2 | Динамичные диалоги (генерация) | 🟠 | 2 |
| 2.3 | Продвижение Temp → Static | 🟢 | 1 |
| 2.4 | Генератор квестов | 🟠 | 2 |
| 2.5 | Трекер прогресса квестов | 🟠 | 1 |

### Фаза 3: ИИ и агрессия (1 неделя)

| # | Задача | Приоритет | Дней |
|---|--------|-----------|------|
| 3.1 | State Machine AI | 🟠 | 2 |
| 3.2 | Система агрессии | 🟠 | 2 |
| 3.3 | Pathfinding | 🟢 | 1 |

---

## 🔗 Связанные документы

- [docs/body.md](./body.md) — Система тела
- [docs/DAMAGE_FORMULAS_PROPOSAL.md](./DAMAGE_FORMULAS_PROPOSAL.md) — Формулы урона
- [docs/event-bus-system.md](./event-bus-system.md) — Event Bus
- [docs/random_npc.md](./random_npc.md) — Генерация NPC
- [src/lib/game/combat-system.ts](../src/lib/game/combat-system.ts) — Боевая система
- [src/types/body.ts](../src/types/body.ts) — Типы тела
- [docs/body_armor.md](./body_armor.md) — Порядок прохождения урона (v4.0)
- [docs/technique-system-v2.md](./technique-system-v2.md) — Система техник (v3.0)

---

## 2️⃣4️⃣ ⭐ ПОДАВЛЕНИЕ УРОВНЕМ ДЛЯ NPC (NEW v3.0)

### 24.1 Принцип

**NPC использует ту же систему подавления уровнем, что и игрок.**

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                    ПОДАВЛЕНИЕ УРОВНЕМ ДЛЯ NPC                                       │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  NPC L5 атакует игрока L8:                                                          │
│  levelDiff = 8 - 5 = 3                                                              │
│  breakthrough = npc.technique.level (если есть техника)                            │
│  effectiveDiff = max(0, 3 - breakthrough)                                           │
│                                                                                      │
│  NPC БЕЗ техники: breakthrough = 0                                                  │
│  → effectiveDiff = 3                                                                │
│  → multiplier = ×0 (normal attack, полный иммунитет игрока!)                        │
│                                                                                      │
│  NPC с техникой L5: breakthrough = 5                                                │
│  → effectiveDiff = 0                                                                │
│  → multiplier = ×1.0 (полный урон)                                                  │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

### 24.2 Интеграция в npc-damage-calculator.ts

```typescript
// src/lib/game/npc-damage-calculator.ts

import { calculateLevelSuppression } from './level-suppression';

export function calculateDamageFromNPC(params: NPCAttackParams): DamageResult {
  const { npc, technique, target, isCritical = false } = params;
  
  // ... существующий расчёт урона ...
  
  let effect = effectiveQi * qiDensity * statMultiplier * masteryMultiplier;
  
  // ⭐ ПОДАВЛЕНИЕ УРОВНЕМ (NEW v3.0)
  const suppression = calculateLevelSuppression(
    npc.cultivationLevel,           // attackerLevel
    target.cultivationLevel,        // defenderLevel
    technique?.level ?? 0,          // techniqueLevel (0 если нет техники)
    technique?.isUltimate ?? false  // isUltimate
  );
  
  effect *= suppression.multiplier;
  
  // Если иммунитет — возвращаем 0 урона
  if (suppression.multiplier === 0) {
    return {
      damage: 0,
      qiSpent,
      effectiveQi,
      qiDensity,
      statMultiplier,
      masteryMultiplier,
      conductivityBonus,
      armorReduction: 0,
      bufferAbsorbed: 0,
      isCritical,
      damageType: 'immune',
    };
  }
  
  // ... остальной расчёт ...
}
```

### 24.3 Влияние на поведение NPC

**NPC осознают подавление уровнем!**

```typescript
// NPC AI учитывает подавление при выборе цели

function selectTarget(npc: NPC, potentialTargets: Entity[]): Entity | null {
  const viableTargets = potentialTargets.filter(target => {
    const suppression = calculateLevelSuppression(
      npc.cultivationLevel,
      target.cultivationLevel,
      npc.bestTechnique?.level ?? 0,
      npc.bestTechnique?.isUltimate ?? false
    );
    
    // Не атаковать цели с иммунитетом
    return suppression.multiplier > 0;
  });
  
  // Если нет viable целей — искать другую стратегию
  if (viableTargets.length === 0) {
    return null; // Бежать / прятаться / звать помощь
  }
  
  // Выбрать ближайшую viable цель
  return findClosest(npc, viableTargets);
}
```

### 24.4 Примеры сценариев

**Сценарий 1: Слабый NPC без техники**
```
NPC L3 (волк) атакует игрока L8
  technique = null → breakthrough = 0
  levelDiff = 8 - 3 = 5
  effectiveDiff = 5
  multiplier = ×0 (ИММУНИТЕТ)
  
Результат: Волк L3 не может нанести урон игроку L8!
```

**Сценарий 2: NPC с техникой**
```
NPC L5 (бандит) с техникой L5 атакует игрока L8
  technique = L5 → breakthrough = 5
  levelDiff = 3
  effectiveDiff = max(0, 3 - 5) = 0
  multiplier = ×1.0 (technique)
  
Результат: Полный урон! NPC L5 с техникой L5 опасен для L8.
```

**Сценарий 3: NPC босс**
```
NPC L9 (дракон) с ultimate-техникой L9 атакует игрока L8
  isUltimate = true
  levelDiff = 8 - 9 = -1 (атакующий выше)
  multiplier = ×1.0 (нет подавления)
  
Результат: Полный урон от босса.
```

### 24.5 Баланс для NPC

| Тип NPC | Уровень | Техника | Угроза для L8 игрока |
|---------|---------|---------|---------------------|
| Слабый моб | L1-L3 | Нет | ИММУНИТЕТ |
| Обычный моб | L4-L5 | Нет | ИММУНИТЕТ |
| Элитный моб | L5-L6 | L4-L5 | ×0.75 - ×1.0 |
| Мини-босс | L7-L8 | L6-L8 | ×0.75 - ×1.0 |
| Босс | L8-L9 | L8-L9 + ultimate | ×1.0 |

---

*Документ создан: 2026-03-16*  
*Обновлён: 2026-03-22 (v3.0 — добавлено подавление уровнем)*  
*Автор: ИИ-агент*
