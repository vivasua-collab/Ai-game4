# NPC Artificial Intelligence - Теоретические разработки

**Версия:** 2.0 (Драфт)
**Дата:** 2026-03-24
**Статус:** 📋 Теоретическая разработка (активное развитие)
**Автор:** AI Assistant

---

## 📋 Обзор документа

Документ содержит теоретические разработки и варианты реализации искусственного интеллекта для NPC в мире культивации.

### Связанные документы
- [NPC_COMBAT_INTERACTIONS.md](./NPC_COMBAT_INTERACTIONS.md) - Боевые взаимодействия
- [random_npc.md](./random_npc.md) - Система временных NPC
- [faction-system.md](./faction-system.md) - Система фракций и государств
- [ARCHITECTURE.md](./ARCHITECTURE.md) - Архитектура проекта

---

## 🎯 Цели и требования

### Основные цели
1. **Живой мир** - NPC ведут себя естественно и предсказуемо
2. **Разнообразие** - Разные типы NPC имеют разное поведение
3. **Производительность** - AI не должен тормозить игру
4. **Масштабируемость** - Легко добавлять новые поведения

### Требования из мира культивации
- Уважение уровней культивации (подавление)
- Фракции и секты влияют на поведение
- Медитация и культивация меняют приоритеты
- Техники должны использоваться разумно

---

## 🧠 Варианты архитектуры AI

### Вариант A: Конечный автомат (State Machine)

**Принцип:**
NPC находится в одном из нескольких состояний. Переходы между состояниями происходят по условиям.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    STATE MACHINE ARCHITECTURE                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────┐     игрок рядом      ┌─────────┐                        │
│  │  IDLE   │ ───────────────────► │  ALERT  │                        │
│  └─────────┘                      └─────────┘                        │
│       │                                │                              │
│       │ патруль                        │ враг                         │
│       ▼                                ▼                              │
│  ┌─────────┐    цель в зоне     ┌─────────┐                          │
│  │ PATROL  │ ◄───────────────  │  CHASE  │                          │
│  └─────────┘                   └─────────┘                          │
│       │                             │                                 │
│       │                             │ в зоне атаки                    │
│       │                             ▼                                 │
│       │                        ┌─────────┐                           │
│       │                        │ ATTACK  │                           │
│       │                        └─────────┘                           │
│       │                             │                                 │
│       │                             │ HP < 20%                        │
│       │                             ▼                                 │
│       │                        ┌─────────┐                           │
│       └───────────────────────►│  FLEE   │                           │
│                                └─────────┘                           │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- Простота реализации
- Понятное поведение
- Легко отлаживать
- Низкие требования к ресурсам

**Недостатки:**
- Ограниченная сложность поведения
- Трудно создавать сложные комбинации
- Ригидность переходов

**Реализация:**
```typescript
// src/lib/game/npc-ai-state-machine.ts

type AIState = 'idle' | 'patrol' | 'alert' | 'chase' | 'attack' | 'flee' | 'meditate';

interface StateTransition {
  from: AIState;
  to: AIState;
  condition: (context: AIContext) => boolean;
  priority: number;
}

interface AIContext {
  npc: TempNPC;
  player: { level: number; position: { x: number; y: number } };
  nearbyNPCs: TempNPC[];
  time: GameTime;
  location: LocationConfig;
}

class StateMachineAI {
  private currentState: AIState = 'idle';
  private transitions: StateTransition[] = [];
  private lastStateChange: number = 0;
  
  update(context: AIContext): AIAction {
    // 1. Проверяем все возможные переходы
    const validTransitions = this.transitions
      .filter(t => t.from === this.currentState && t.condition(context))
      .sort((a, b) => b.priority - a.priority);
    
    // 2. Выполняем переход с наивысшим приоритетом
    if (validTransitions.length > 0) {
      this.changeState(validTransitions[0].to);
    }
    
    // 3. Выполняем действие текущего состояния
    return this.executeState(context);
  }
  
  private changeState(newState: AIState): void {
    console.log(`[AI] ${this.currentState} → ${newState}`);
    this.currentState = newState;
    this.lastStateChange = Date.now();
  }
  
  private executeState(context: AIContext): AIAction {
    switch (this.currentState) {
      case 'idle':
        return this.actionIdle(context);
      case 'patrol':
        return this.actionPatrol(context);
      case 'alert':
        return this.actionAlert(context);
      case 'chase':
        return this.actionChase(context);
      case 'attack':
        return this.actionAttack(context);
      case 'flee':
        return this.actionFlee(context);
      case 'meditate':
        return this.actionMeditate(context);
    }
  }
}
```

**Рекомендация:** ✅ Использовать как основу для MVP

---

### Вариант B: Utility AI (Взвешенный выбор)

**Принцип:**
Каждое возможное действие получает оценку (score). NPC выбирает действие с наивысшим score.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    UTILITY AI ARCHITECTURE                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Действия (Actions):                                                 │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │   Attack    │ │   Defend    │ │    Flee     │ │   Patrol    │   │
│  │   Score:    │ │   Score:    │ │   Score:    │ │   Score:    │   │
│  │   0.75      │ │   0.42      │ │   0.18      │ │   0.05      │   │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘   │
│         │                                                            │
│         ▼                                                            │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                    ACTION SELECTOR                          │    │
│  │  Выбирает действие с наивысшим score → ATTACK               │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  Формула Score:                                                      │
│  score = Σ(factor_i × weight_i)                                      │
│                                                                      │
│  Пример для Attack:                                                  │
│  - distanceToPlayer: 0.8 (близко) × 0.3 = 0.24                      │
│  - hpPercent: 0.9 (высокое) × 0.25 = 0.225                          │
│  - levelDifference: 0.9 (равные) × 0.2 = 0.18                       │
│  - hasTechnique: 1.0 (есть) × 0.15 = 0.15                            │
│  - aggressionLevel: 0.8 (агрессивен) × 0.1 = 0.08                    │
│  ────────────────────────────────────────────                       │
│  Total Attack Score = 0.875                                         │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- Гибкое и адаптивное поведение
- Легко настраивать веса
- Можно комбинировать много факторов
- Плавные переходы между действиями

**Недостатки:**
- Сложнее отлаживать
- Требует тщательной настройки весов
- Может давать неожиданные результаты

**Реализация:**
```typescript
// src/lib/game/npc-ai-utility.ts

interface AIAction {
  type: 'attack' | 'defend' | 'flee' | 'patrol' | 'meditate' | 'talk' | 'trade';
  score: number;
  factors: ScoreFactor[];
}

interface ScoreFactor {
  name: string;
  value: number;     // 0.0 - 1.0
  weight: number;    // Важность фактора
}

interface UtilityProfile {
  action: AIActionType;
  factors: FactorDefinition[];
}

interface FactorDefinition {
  name: string;
  weight: number;
  calculate: (context: AIContext) => number;
}

// Профили для разных типов NPC
const UTILITY_PROFILES: Record<string, UtilityProfile[]> = {
  // Агрессивный боец
  warrior: [
    {
      action: 'attack',
      factors: [
        { name: 'distanceToEnemy', weight: 0.3, calculate: (ctx) => 1 - (ctx.distanceToPlayer / 20) },
        { name: 'hpPercent', weight: 0.25, calculate: (ctx) => ctx.npc.bodyState.hp / ctx.npc.bodyState.maxHp },
        { name: 'qiPercent', weight: 0.2, calculate: (ctx) => ctx.npc.qi.current / ctx.npc.qi.max },
        { name: 'levelAdvantage', weight: 0.15, calculate: (ctx) => Math.min(1, ctx.npc.cultivation.level / ctx.player.level) },
        { name: 'aggression', weight: 0.1, calculate: (ctx) => ctx.npc.personality.aggressionLevel / 100 },
      ],
    },
    {
      action: 'flee',
      factors: [
        { name: 'lowHp', weight: 0.4, calculate: (ctx) => 1 - (ctx.npc.bodyState.hp / ctx.npc.bodyState.maxHp) },
        { name: 'outnumbered', weight: 0.3, calculate: (ctx) => ctx.nearbyEnemies.length > 1 ? 1 : 0 },
        { name: 'levelDisadvantage', weight: 0.3, calculate: (ctx) => ctx.player.level > ctx.npc.cultivation.level + 2 ? 1 : 0 },
      ],
    },
  ],
  
  // Осторожный практик
  cultivator: [
    {
      action: 'meditate',
      factors: [
        { name: 'lowQi', weight: 0.35, calculate: (ctx) => 1 - (ctx.npc.qi.current / ctx.npc.qi.max) },
        { name: 'safeLocation', weight: 0.3, calculate: (ctx) => ctx.nearbyEnemies.length === 0 ? 1 : 0 },
        { name: 'timeOfDay', weight: 0.2, calculate: (ctx) => ctx.time.hour >= 22 || ctx.time.hour < 5 ? 1 : 0.5 },
        { name: 'meditationAffinity', weight: 0.15, calculate: (ctx) => ctx.npc.personality.traits.includes('meditative') ? 1 : 0.3 },
      ],
    },
    {
      action: 'flee',
      factors: [
        { name: 'dangerNearby', weight: 0.5, calculate: (ctx) => ctx.nearbyEnemies.length > 0 ? 1 : 0 },
        { name: 'lowCombatAbility', weight: 0.3, calculate: (ctx) => ctx.npc.techniques.length < 2 ? 1 : 0 },
        { name: 'lowHp', weight: 0.2, calculate: (ctx) => 1 - (ctx.npc.bodyState.hp / ctx.npc.bodyState.maxHp) },
      ],
    },
  ],
  
  // Торговец
  merchant: [
    {
      action: 'trade',
      factors: [
        { name: 'playerNearby', weight: 0.4, calculate: (ctx) => ctx.distanceToPlayer < 5 ? 1 : 0 },
        { name: 'friendlyDisposition', weight: 0.3, calculate: (ctx) => Math.max(0, ctx.npc.personality.disposition / 100) },
        { name: 'hasGoods', weight: 0.3, calculate: (ctx) => ctx.npc.quickSlots.length > 0 ? 1 : 0 },
      ],
    },
    {
      action: 'talk',
      factors: [
        { name: 'playerNearby', weight: 0.35, calculate: (ctx) => ctx.distanceToPlayer < 10 ? 1 : 0.5 },
        { name: 'canTalk', weight: 0.3, calculate: (ctx) => ctx.npc.personality.canTalk ? 1 : 0 },
        { name: 'neutralDisposition', weight: 0.2, calculate: (ctx) => ctx.npc.personality.disposition > -50 ? 1 : 0 },
        { name: 'hasRumors', weight: 0.15, calculate: (ctx) => ctx.npc.knownRumors?.length > 0 ? 1 : 0.5 },
      ],
    },
    {
      action: 'flee',
      factors: [
        { name: 'hostileNearby', weight: 0.6, calculate: (ctx) => ctx.nearbyEnemies.length > 0 ? 1 : 0 },
        { name: 'lowCombat', weight: 0.4, calculate: (ctx) => ctx.npc.stats.strength < 12 ? 1 : 0 },
      ],
    },
  ],
};

class UtilityAI {
  private profile: UtilityProfile[];
  
  constructor(npcType: string) {
    this.profile = UTILITY_PROFILES[npcType] || UTILITY_PROFILES.warrior;
  }
  
  selectAction(context: AIContext): AIAction {
    const actions: AIAction[] = [];
    
    // 1. Вычисляем score для каждого действия
    for (const profile of this.profile) {
      const factors: ScoreFactor[] = [];
      let totalScore = 0;
      
      for (const factorDef of profile.factors) {
        const value = factorDef.calculate(context);
        const weightedValue = value * factorDef.weight;
        factors.push({ name: factorDef.name, value, weight: factorDef.weight });
        totalScore += weightedValue;
      }
      
      actions.push({
        type: profile.action,
        score: totalScore,
        factors,
      });
    }
    
    // 2. Сортируем по score
    actions.sort((a, b) => b.score - a.score);
    
    // 3. Возвращаем лучшее действие
    return actions[0];
  }
}
```

**Рекомендация:** ✅ Использовать для сложных NPC (лидеры, боссы)

---

### Вариант C: Behavior Tree (Дерево поведения)

**Принцип:**
Иерархическая структура узлов. Выполняются слева направо, сверху вниз.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    BEHAVIOR TREE ARCHITECTURE                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                    SELECTOR (Root)                          │    │
│  │  "Выполнить первое успешное действие"                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│         │                                                            │
│         ├───┬───────────────────────────────────────────────┐       │
│         │   │                                               │       │
│         ▼   ▼                                               ▼       │
│  ┌──────────────┐  ┌──────────────┐              ┌──────────────┐  │
│  │  SEQUENCE    │  │  SEQUENCE    │              │  SEQUENCE    │  │
│  │ "Все должны  │  │ "Все должны  │              │ "Все должны  │  │
│  │  быть true"  │  │  быть true"  │              │  быть true"  │  │
│  └──────────────┘  └──────────────┘              └──────────────┘  │
│         │                │                               │          │
│    ┌────┴────┐      ┌────┴────┐                    ┌────┴────┐    │
│    │         │      │         │                    │         │    │
│    ▼         ▼      ▼         ▼                    ▼         ▼    │
│ ┌─────┐  ┌─────┐ ┌─────┐  ┌─────┐            ┌─────┐  ┌─────┐   │
│ │Cond:│  │Act: │ │Cond:│  │Act: │            │Cond:│  │Act: │   │
│ │Enemy│  │Attack│ │LowHP│  │Flee │            │True │  │Patrol│  │
│ │Near│  │     │ │     │  │     │            │     │  │     │   │
│ └─────┘  └─────┘ └─────┘  └─────┘            └─────┘  └─────┘   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

**Типы узлов:**

| Тип | Описание | Возвращает |
|-----|----------|------------|
| **Selector** | Выполняет детей пока один не вернёт SUCCESS | SUCCESS / FAILURE |
| **Sequence** | Выполняет детей пока все не вернут SUCCESS | SUCCESS / FAILURE |
| **Condition** | Проверяет условие | SUCCESS / FAILURE |
| **Action** | Выполняет действие | SUCCESS / FAILURE / RUNNING |
| **Decorator** | Модифицирует результат ребёнка | Зависит от типа |
| **Inverter** | Инвертирует результат ребёнка | SUCCESS ↔ FAILURE |

**Преимущества:**
- Очень гибкое поведение
- Визуальное представление
- Легко добавлять новые поведения
- Поддержка сложных сценариев

**Недостатки:**
- Сложность реализации
- Требует больше ресурсов
- Кривая обучения

**Реализация:**
```typescript
// src/lib/game/npc-ai-behavior-tree.ts

type NodeStatus = 'success' | 'failure' | 'running';

interface BehaviorNode {
  type: 'selector' | 'sequence' | 'condition' | 'action' | 'decorator' | 'inverter';
  children?: BehaviorNode[];
  condition?: (ctx: AIContext) => boolean;
  action?: (ctx: AIContext) => NodeStatus;
  execute: (ctx: AIContext) => NodeStatus;
}

// Фабрика узлов
const BehaviorTree = {
  selector(...children: BehaviorNode[]): BehaviorNode {
    return {
      type: 'selector',
      children,
      execute: (ctx) => {
        for (const child of children) {
          const status = child.execute(ctx);
          if (status === 'success' || status === 'running') {
            return status;
          }
        }
        return 'failure';
      },
    };
  },
  
  sequence(...children: BehaviorNode[]): BehaviorNode {
    return {
      type: 'sequence',
      children,
      execute: (ctx) => {
        for (const child of children) {
          const status = child.execute(ctx);
          if (status === 'failure' || status === 'running') {
            return status;
          }
        }
        return 'success';
      },
    };
  },
  
  condition(check: (ctx: AIContext) => boolean): BehaviorNode {
    return {
      type: 'condition',
      condition: check,
      execute: (ctx) => check(ctx) ? 'success' : 'failure',
    };
  },
  
  action(execute: (ctx: AIContext) => NodeStatus): BehaviorNode {
    return {
      type: 'action',
      action: execute,
      execute,
    };
  },
  
  inverter(child: BehaviorNode): BehaviorNode {
    return {
      type: 'inverter',
      children: [child],
      execute: (ctx) => {
        const status = child.execute(ctx);
        if (status === 'success') return 'failure';
        if (status === 'failure') return 'success';
        return status;
      },
    };
  },
};

// Пример дерева для боевого NPC
const combatTree: BehaviorNode = BehaviorTree.selector(
  // 1. Бегство при низком HP
  BehaviorTree.sequence(
    BehaviorTree.condition(ctx => ctx.npc.bodyState.hp < ctx.npc.bodyState.maxHp * 0.2),
    BehaviorTree.action(ctx => {
      // Выполнить побег
      return ctx.executeFlee() ? 'success' : 'running';
    }),
  ),
  
  // 2. Атака при наличии врага рядом
  BehaviorTree.sequence(
    BehaviorTree.condition(ctx => ctx.distanceToPlayer < 10),
    BehaviorTree.selector(
      // 2a. Использовать технику если есть Qi
      BehaviorTree.sequence(
        BehaviorTree.condition(ctx => ctx.npc.qi.current > 20),
        BehaviorTree.condition(ctx => ctx.npc.techniques.length > 0),
        BehaviorTree.action(ctx => {
          return ctx.executeTechnique() ? 'success' : 'running';
        }),
      ),
      // 2b. Базовая атака
      BehaviorTree.action(ctx => {
        return ctx.executeBasicAttack() ? 'success' : 'running';
      }),
    ),
  ),
  
  // 3. Патрулирование по умолчанию
  BehaviorTree.action(ctx => {
    return ctx.executePatrol() ? 'success' : 'running';
  }),
);

class BehaviorTreeAI {
  private root: BehaviorNode;
  
  constructor(tree: BehaviorNode) {
    this.root = tree;
  }
  
  update(context: AIContext): NodeStatus {
    return this.root.execute(context);
  }
}
```

**Рекомендация:** ✅ Использовать для боссов и ключевых NPC

---

### Вариант D: GOAP (Goal-Oriented Action Planning)

**Принцип:**
NPC имеет цель и планирует последовательность действий для её достижения.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    GOAP ARCHITECTURE                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Текущее состояние:                Целевое состояние:               │
│  ┌─────────────────────┐          ┌─────────────────────┐          │
│  │ hp: 80              │          │ enemyDead: true     │          │
│  │ qi: 50              │   ───►   │ hp: > 0             │          │
│  │ distanceToEnemy: 15 │          │                     │          │
│  │ enemyVisible: true  │          │                     │          │
│  └─────────────────────┘          └─────────────────────┘          │
│                                                                      │
│  План действий:                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  1. MoveTo(enemy.position)     // Подойти к врагу          │    │
│  │  2. Attack(enemy)              // Атаковать                │    │
│  │  3. Repeat until enemyDead     // Повторять                │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  Доступные действия:                                                 │
│  ┌────────────┬────────────────────┬─────────────────────────────┐  │
│  │  Действие  │    Preconditions   │        Effects              │  │
│  ├────────────┼────────────────────┼─────────────────────────────┤  │
│  │ MoveTo     │ canMove: true      │ distance: X → 0            │  │
│  │ Attack     │ distance < 2       │ enemyHp: -damage           │  │
│  │ UseTech    │ qi > cost          │ enemyHp: -damage, qi: -cost│  │
│  │ Meditate   │ safe: true         │ qi: +regen                 │  │
│  │ Flee       │ canMove: true      │ distance: +20, safe: true  │  │
│  └────────────┴────────────────────┴─────────────────────────────┘  │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- Очень умное поведение
- Адаптация к ситуации
- Планирование наперёд

**Недостатки:**
- Высокая сложность
- Требует много ресурсов
- Сложно отлаживать

**Рекомендация:** ⚠️ Рассмотреть для будущего (боссы, стратегический AI)

---

## 🎭 Поведения для разных типов NPC

### Классификация NPC по поведению

| Тип NPC | Основное поведение | AI Сложность | Рекомендуемый подход |
|---------|-------------------|--------------|---------------------|
| **Монстры** | Агрессия, патруль | Низкая | State Machine |
| **Охраники** | Патруль, тревога | Средняя | State Machine + Utility |
| **Торговцы** | Торговля, разговоры | Низкая | State Machine |
| **Культиваторы** | Медитация, развитие | Средняя | Utility AI |
| **Лидеры сект** | Стратегия, управление | Высокая | Behavior Tree |
| **Боссы** | Сложные паттерны | Высокая | Behavior Tree + GOAP |

### Примеры поведений

#### 1. Монстр (Wolf, Tiger, etc.)
```typescript
const monsterBehavior: AIProfile = {
  type: 'monster',
  
  states: ['idle', 'patrol', 'chase', 'attack', 'flee'],
  
  initialState: 'patrol',
  
  transitions: [
    // Обнаружение врага
    { from: 'idle', to: 'patrol', condition: 'timePassed(30s)' },
    { from: 'patrol', to: 'chase', condition: 'enemyInRange(15m)' },
    { from: 'chase', to: 'attack', condition: 'enemyInRange(2m)' },
    
    // Потеря врага
    { from: 'chase', to: 'patrol', condition: 'enemyLost(30s)' },
    
    // Бегство
    { from: 'attack', to: 'flee', condition: 'hpBelow(20%)' },
    { from: 'chase', to: 'flee', condition: 'hpBelow(20%)' },
    
    // Восстановление
    { from: 'flee', to: 'idle', condition: 'safe(10s)' },
  ],
  
  actions: {
    patrol: { type: 'randomWalk', speed: 0.5 },
    chase: { type: 'moveToTarget', speed: 1.0 },
    attack: { type: 'meleeAttack', damage: 10 },
    flee: { type: 'moveAway', speed: 1.5 },
  },
};
```

#### 2. Охранник секты
```typescript
const guardBehavior: AIProfile = {
  type: 'guard',
  
  states: ['idle', 'patrol', 'alert', 'investigate', 'chase', 'attack', 'report'],
  
  specialRules: {
    // Уважение к старшим
    respectHierarchy: true,
    
    // Тревога при обнаружении врага
    alertAllies: true,
    alertRange: 50,
    
    // Доложить о нарушении
    reportTo: 'elder',
  },
  
  transitions: [
    // Патрулирование
    { from: 'idle', to: 'patrol', condition: 'shouldPatrol()' },
    { from: 'patrol', to: 'idle', condition: 'patrolComplete()' },
    
    // Обнаружение
    { from: 'patrol', to: 'alert', condition: 'suspiciousActivity()' },
    { from: 'alert', to: 'investigate', condition: 'hasTarget()' },
    { from: 'alert', to: 'patrol', condition: 'falseAlarm()' },
    
    // Преследование
    { from: 'investigate', to: 'chase', condition: 'enemyIdentified()' },
    { from: 'chase', to: 'attack', condition: 'enemyInRange(2m)' },
    
    // Атака
    { from: 'attack', to: 'chase', condition: 'enemyEscaped()' },
    { from: 'attack', to: 'report', condition: 'enemyDefeated()' },
    
    // Бегство (только если враг сильно выше уровнем)
    { from: 'any', to: 'flee', condition: 'enemyLevelDiff(>3)' },
  ],
  
  actions: {
    patrol: { type: 'waypointPatrol', route: 'sectorA' },
    alert: { type: 'alertAllies', range: 50 },
    investigate: { type: 'moveToLastKnownPosition' },
    chase: { type: 'chaseTarget' },
    attack: { type: 'combatStance', callForHelp: true },
    report: { type: 'reportToSuperior' },
  },
};
```

#### 3. Культиватор-практик
```typescript
const cultivatorBehavior: AIProfile = {
  type: 'cultivator',
  
  // Utility AI для культиватора
  utilityFactors: {
    meditate: [
      { factor: 'lowQi', weight: 0.4 },
      { factor: 'safeLocation', weight: 0.3 },
      { factor: 'nightTime', weight: 0.15 },
      { factor: 'meditationMood', weight: 0.15 },
    ],
    
    practice: [
      { factor: 'hasTechnique', weight: 0.3 },
      { factor: 'highQi', weight: 0.25 },
      { factor: 'trainingGround', weight: 0.25 },
      { factor: 'dayTime', weight: 0.2 },
    ],
    
    socialize: [
      { factor: 'friendlyNPCNearby', weight: 0.4 },
      { factor: 'socialMood', weight: 0.3 },
      { factor: 'restTime', weight: 0.2 },
      { factor: 'hasNews', weight: 0.1 },
    ],
    
    rest: [
      { factor: 'lowHp', weight: 0.35 },
      { factor: 'fatigue', weight: 0.35 },
      { factor: 'nightTime', weight: 0.2 },
      { factor: 'privateRoom', weight: 0.1 },
    ],
  },
  
  // Расписание дня
  schedule: {
    5: { action: 'wakeUp', priority: 10 },
    6: { action: 'meditate', priority: 8, duration: '2h' },
    8: { action: 'practice', priority: 7, duration: '4h' },
    12: { action: 'rest', priority: 6, duration: '1h' },
    13: { action: 'practice', priority: 7, duration: '3h' },
    16: { action: 'socialize', priority: 5, duration: '2h' },
    18: { action: 'meditate', priority: 8, duration: '2h' },
    20: { action: 'rest', priority: 6, duration: '1h' },
    21: { action: 'meditate', priority: 8, duration: '2h' },
    23: { action: 'sleep', priority: 10 },
  },
};
```

---

## 🎮 Специфика мира культивации

### Уровни культивации и поведение

| Разница уровней | Влияние на поведение |
|-----------------|---------------------|
| Игрок > NPC на 3+ | NPC проявляет уважение, страх, избегает конфликта |
| Игрок > NPC на 1-2 | NPC осторожен, но может атаковать при провокации |
| Игрок = NPC | Обычное поведение |
| Игрок < NPC на 1-2 | NPC уверен, может быть агрессивен |
| Игрок < NPC на 3+ | NPC высокомерен, не воспринимает всерьёз |

### Фракции и отношения

```typescript
interface FactionRelation {
  factionId: string;
  disposition: number;      // -100 до 100
  trustLevel: number;       // 0-100
  hostilityThreshold: number; // disposition при котором атакует
}

const FACTION_RELATIONS: Record<string, Record<string, FactionRelation>> = {
  azureDragonSect: {
    crimsonPhoenixSect: { disposition: -30, trustLevel: 20, hostilityThreshold: -50 },
    goldenLotusSect: { disposition: 10, trustLevel: 40, hostilityThreshold: -30 },
    shadowDemonSect: { disposition: -80, trustLevel: 0, hostilityThreshold: -20 },
  },
  // ... другие фракции
};
```

### Техники и AI

```typescript
// NPC выбирает технику на основе ситуации
interface TechniqueSelection {
  situation: 'melee' | 'ranged' | 'defensive' | 'finishing';
  preferredTypes: TechniqueType[];
  qiThreshold: number;  // Минимум Qi для использования
}

const TECHNIQUE_SELECTION: Record<string, TechniqueSelection> = {
  melee: {
    situation: 'melee',
    preferredTypes: ['strike', 'dash', 'aoe'],
    qiThreshold: 15,
  },
  ranged: {
    situation: 'ranged',
    preferredTypes: ['projectile', 'beam', 'summon'],
    qiThreshold: 25,
  },
  defensive: {
    situation: 'defensive',
    preferredTypes: ['shield', 'dodge', 'counter'],
    qiThreshold: 10,
  },
  finishing: {
    situation: 'finishing',
    preferredTypes: ['ultimate', 'combo'],
    qiThreshold: 50,
  },
};
```

---

## ⚡ Оптимизация производительности

### Проблемы производительности AI

1. **Слишком много NPC** - каждый кадр обновлять AI всех NPC дорого
2. **Сложные вычисления** - Utility AI и GOAP требуют много CPU
3. **Память** - Behavior Trees могут занимать много памяти

### Решения

#### 1. LoD (Level of Detail) для AI

```typescript
enum AILevel {
  FULL = 'full',         // Полный AI - NPC рядом с игроком
  REDUCED = 'reduced',   // Упрощённый AI - NPC в той же локации
  MINIMAL = 'minimal',   // Минимальный AI - NPC далеко
  DORMANT = 'dormant',   // Спящий AI - NPC не активен
}

function getAILevel(npc: TempNPC, playerPosition: Point): AILevel {
  const distance = calculateDistance(npc.position, playerPosition);
  
  if (distance < 10) return AILevel.FULL;      // < 10 метров
  if (distance < 50) return AILevel.REDUCED;   // < 50 метров
  if (distance < 200) return AILevel.MINIMAL;  // < 200 метров
  return AILevel.DORMANT;                       // > 200 метров
}

function updateNPC(npc: TempNPC, level: AILevel): void {
  switch (level) {
    case AILevel.FULL:
      // Обновлять каждый кадр
      // Полный AI
      npc.ai.updateFull();
      break;
      
    case AILevel.REDUCED:
      // Обновлять каждые 5 кадров
      // Упрощённый State Machine
      if (frameCount % 5 === 0) {
        npc.ai.updateReduced();
      }
      break;
      
    case AILevel.MINIMAL:
      // Обновлять каждую секунду
      // Только базовые состояния
      if (frameCount % 60 === 0) {
        npc.ai.updateMinimal();
      }
      break;
      
    case AILevel.DORMANT:
      // Не обновлять
      // Сохранить состояние
      break;
  }
}
```

#### 2. Пулинг AI

```typescript
// Переиспользование AI объектов
class AIPool {
  private pool: Map<string, AIController> = new Map();
  private maxPoolSize: number = 100;
  
  acquire(npcId: string, type: string): AIController {
    const cached = this.pool.get(npcId);
    if (cached) return cached;
    
    const controller = this.createController(type);
    this.pool.set(npcId, controller);
    return controller;
  }
  
  release(npcId: string): void {
    const controller = this.pool.get(npcId);
    if (controller) {
      controller.reset();
      // Не удаляем, оставляем в пуле
    }
  }
}
```

#### 3. Батчинг обновлений

```typescript
// Обновлять AI группами на разных кадрах
class AIManager {
  private npcGroups: TempNPC[][] = [];
  private currentGroup: number = 0;
  
  update(): void {
    // Обновляем только одну группу за кадр
    const group = this.npcGroups[this.currentGroup];
    
    for (const npc of group) {
      npc.ai.update();
    }
    
    this.currentGroup = (this.currentGroup + 1) % this.npcGroups.length;
  }
}
```

---

## 📊 Сравнительная таблица подходов

| Критерий | State Machine | Utility AI | Behavior Tree | GOAP |
|----------|---------------|------------|---------------|------|
| **Сложность реализации** | 🟢 Низкая | 🟡 Средняя | 🟡 Средняя | 🔴 Высокая |
| **Производительность** | 🟢 Высокая | 🟡 Средняя | 🟡 Средняя | 🔴 Низкая |
| **Гибкость** | 🟡 Средняя | 🟢 Высокая | 🟢 Высокая | 🟢 Очень высокая |
| **Отладка** | 🟢 Легко | 🟡 Средне | 🟡 Средне | 🔴 Сложно |
| **Масштабируемость** | 🟡 Средняя | 🟢 Хорошая | 🟢 Хорошая | 🟢 Отличная |
| **Подходит для** | Простые NPC | Культиваторы, лидеры | Боссы, квестовые | Стратегические боссы |

---

## 🎯 Рекомендации

### Для MVP (Минимальный продукт)
1. **State Machine** для всех простых NPC (монстры, торговцы)
2. **Простые переходы** между состояниями
3. **LoD система** для оптимизации

### Для полной версии
1. **Utility AI** для культиваторов и ключевых NPC
2. **Behavior Tree** для боссов
3. **GOAP** для стратегических решений лидеров

### Приоритеты разработки
1. ✅ Базовая State Machine
2. ⏳ Система LoD
3. ⏳ Utility AI для культиваторов
4. ⏳ Behavior Tree для боссов
5. ⏳ GOAP для стратегических NPC

---

## 🚨 Текущая проблема

### Статус существующей реализации

**КРИТИЧЕСКАЯ ПРОБЛЕМА:** Текущая заплатка AI не работает должным образом:
- ❌ NPC неподвижны
- ❌ NPC не реагируют на игрока
- ❌ Отсутствует интеграция с системой фракций

### Причины

1. **Отсутствие активного AI-контроллера** - NPC создаются, но не имеют активного процесса принятия решений
2. **Нет связи с Phaser сценой** - AI не интегрирован в игровой цикл
3. **Отсутствует система событий** - NPC не реагируют на действия игрока

---

## 🔄 Два типа NPC и их требования

### Архитектура разделения ответственности

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ДВУХУРОВНЕВАЯ СИСТЕМА AI                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                 СТАТИЧНЫЕ NPC (Квестовые)                          │  │
│  │                                                                    │  │
│  │  Цель: Максимальная реалистичность                                │  │
│  │  Типы: Главы сект, торговцы, квестдатели, учителя                 │  │
│  │  AI: GOAP (Goal-Oriented Action Planning)                         │  │
│  │                                                                    │  │
│  │  Особенности:                                                      │  │
│  │  • Сложные диалоги и взаимодействия                               │  │
│  │  • Память о предыдущих встречах                                   │  │
│  │  • Отношения с игроком и фракциями                                │  │
│  │  • Расписание дня (сон, работа, медитация)                        │  │
│  │  • Реакция на репутацию игрока                                    │  │
│  │  • Возможность обучения игрока                                    │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                 ВРЕМЕННЫЕ NPC (Статисты)                           │  │
│  │                                                                    │  │
│  │  Цель: Условно простые действия, "фоновая жизнь"                  │  │
│  │  Типы: Прохожие, ученики, охранники, монстры                      │  │
│  │  AI: State Machine (Конечный автомат)                             │  │
│  │                                                                    │  │
│  │  Особенности:                                                      │  │
│  │  • Простые реакции (уйти, атаковать, убежать)                     │  │
│  │  • Базовые состояния (idle, patrol, chase, flee)                  │  │
│  │  • Минимальная память                                             │  │
│  │  • Генерация на лету                                              │  │
│  │  • Освобождение ресурсов при уходе                                │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                 LoD (Level of Detail)                              │  │
│  │                                                                    │  │
│  │  Позволяет подменять AI в зависимости от:                         │  │
│  │  • Расстояния до игрока                                           │  │
│  │  • Важности NPC                                                   │  │
│  │  • Текущей ситуации (бой, диалог, медитация)                      │  │
│  │                                                                    │  │
│  │  Пример: Статичный торговец при бое → State Machine (flee)        │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Сравнительная таблица типов NPC

| Характеристика | Статичные NPC | Временные NPC |
|----------------|---------------|---------------|
| **Жизненный цикл** | Постоянный (сохраняется между сессиями) | Временный (генерируется/удаляется) |
| **Память** | Полная (отношения, история) | Минимальная (текущее состояние) |
| **AI сложность** | Высокая (GOAP) | Низкая (State Machine) |
| **Ресурсы** | Высокие | Минимальные |
| **Примеры** | Глава секты, учитель, торговец | Прохожий, монстр, охранник |
| **Реакции** | Контекстные, персонализированные | Шаблонные |
| **Интеграция с фракциями** | Полная | Базовая |

---

## 🧠 GOAP для статичных NPC

### Почему GOAP?

GOAP (Goal-Oriented Action Planning) идеально подходит для статичных NPC, потому что:

1. **Целеполагание** - NPC имеет долгосрочные цели (продать товары, обучить учеников)
2. **Адаптивность** - План действий строится на основе текущей ситуации
3. **Реалистичность** - NPC "думает" перед действием
4. **Гибкость** - Легко добавлять новые действия и цели

### Архитектура GOAP для статичных NPC

```typescript
// src/lib/game/npc-ai-goap.ts

/**
 *GOAP система для статичных (квестовых) NPC
 */

// ==================== ТИПЫ ====================

interface GOAPWorldState {
  // Состояние мира
  playerNearby: boolean;
  playerReputation: number;          // -100 to 100
  playerLevel: number;
  timeOfDay: number;                 // 0-23
  
  // Состояние NPC
  hp: number;
  qi: number;
  isTalking: boolean;
  isMeditating: boolean;
  
  // Отношения
  disposition: number;               // -100 to 100
  factionRelation: number;           // -100 to 100
  
  // Контекст
  hasQuestsAvailable: boolean;
  canTeachTechnique: boolean;
  hasGoodsToSell: boolean;
  isInDanger: boolean;
}

interface GOAPAction {
  id: string;
  name: string;
  cost: number;
  
  // Условия для выполнения
  preconditions: Partial<GOAPWorldState>;
  
  // Эффекты после выполнения
  effects: Partial<GOAPWorldState>;
  
  // Функция проверки (динамические условия)
  checkPreconditions?: (state: GOAPWorldState, context: AIContext) => boolean;
  
  // Функция выполнения
  execute: (context: AIContext) => Promise<ActionResult>;
}

interface GOAPGoal {
  id: string;
  name: string;
  priority: number;
  
  // Желаемое состояние мира
  targetState: Partial<GOAPWorldState>;
  
  // Условие достижения цели
  isAchieved: (state: GOAPWorldState) => boolean;
  
  // Динамический приоритет
  calculatePriority?: (state: GOAPWorldState, context: AIContext) => number;
}

interface ActionResult {
  success: boolean;
  duration: number;  // Время выполнения в тиках
  newState?: Partial<GOAPWorldState>;
}

// ==================== ДЕЙСТВИЯ ДЛЯ ТОРГОВЦА ====================

const MERCHANT_ACTIONS: GOAPAction[] = [
  {
    id: 'greet_player',
    name: 'Приветствовать игрока',
    cost: 1,
    preconditions: { playerNearby: true, isTalking: false },
    effects: { isTalking: true },
    execute: async (ctx) => {
      // Показать приветствие
      return { success: true, duration: 1 };
    },
  },
  
  {
    id: 'offer_trade',
    name: 'Предложить торговлю',
    cost: 2,
    preconditions: { isTalking: true, hasGoodsToSell: true, disposition: 0 }, // disposition >= 0
    effects: { },
    checkPreconditions: (state) => state.disposition >= 0,
    execute: async (ctx) => {
      // Открыть интерфейс торговли
      return { success: true, duration: 1 };
    },
  },
  
  {
    id: 'refuse_trade',
    name: 'Отказать в торговле',
    cost: 3,
    preconditions: { isTalking: true },
    effects: { isTalking: false },
    checkPreconditions: (state) => state.disposition < -30,
    execute: async (ctx) => {
      // Показать отказ
      return { success: true, duration: 1 };
    },
  },
  
  {
    id: 'offer_quest',
    name: 'Предложить квест',
    cost: 5,
    preconditions: { isTalking: true, hasQuestsAvailable: true, disposition: 20 },
    effects: { },
    checkPreconditions: (state) => state.disposition >= 20,
    execute: async (ctx) => {
      // Показать квест
      return { success: true, duration: 1 };
    },
  },
  
  {
    id: 'call_guards',
    name: 'Позвать стражу',
    cost: 10,
    preconditions: { isInDanger: true },
    effects: { isInDanger: false },
    execute: async (ctx) => {
      // Вызвать охрану
      return { success: true, duration: 2 };
    },
  },
  
  {
    id: 'rest',
    name: 'Отдыхать',
    cost: 1,
    preconditions: { playerNearby: false, isTalking: false },
    effects: { },
    execute: async (ctx) => {
      // Перейти в режим ожидания
      return { success: true, duration: 60 };
    },
  },
];

// ==================== ДЕЙСТВИЯ ДЛЯ ГЛАВЫ СЕКТЫ ====================

const SECT_LEADER_ACTIONS: GOAPAction[] = [
  {
    id: 'teach_technique',
    name: 'Обучить технике',
    cost: 20,
    preconditions: { 
      isTalking: true, 
      canTeachTechnique: true, 
      playerReputation: 50,
      disposition: 50,
    },
    checkPreconditions: (state) => 
      state.disposition >= 50 && state.playerReputation >= 50,
    effects: { },
    execute: async (ctx) => {
      // Открыть интерфейс обучения
      return { success: true, duration: 100 };
    },
  },
  
  {
    id: 'assign_mission',
    name: 'Назначить миссию',
    cost: 10,
    preconditions: { 
      isTalking: true, 
      hasQuestsAvailable: true, 
      disposition: 30,
    },
    checkPreconditions: (state) => state.disposition >= 30,
    effects: { hasQuestsAvailable: false },
    execute: async (ctx) => {
      // Назначить миссию
      return { success: true, duration: 5 };
    },
  },
  
  {
    id: 'expel_from_sect',
    name: 'Изгнать из секты',
    cost: 50,
    preconditions: { isTalking: true, disposition: -80 },
    checkPreconditions: (state) => state.disposition <= -80,
    effects: { },
    execute: async (ctx) => {
      // Изгнать игрока
      return { success: true, duration: 5 };
    },
  },
  
  {
    id: 'meditate',
    name: 'Медитировать',
    cost: 5,
    preconditions: { playerNearby: false, isTalking: false, isInDanger: false },
    effects: { isMeditating: true },
    execute: async (ctx) => {
      // Начать медитацию
      return { success: true, duration: 300 };
    },
  },
  
  {
    id: 'stop_meditation',
    name: 'Прервать медитацию',
    cost: 3,
    preconditions: { isMeditating: true, playerNearby: true },
    effects: { isMeditating: false },
    execute: async (ctx) => {
      // Прервать медитацию
      return { success: true, duration: 1 };
    },
  },
];

// ==================== ЦЕЛИ ====================

const STATIC_NPC_GOALS: GOAPGoal[] = [
  {
    id: 'maintain_safety',
    name: 'Обеспечить безопасность',
    priority: 100,
    targetState: { isInDanger: false },
    isAchieved: (state) => !state.isInDanger,
  },
  
  {
    id: 'engage_player',
    name: 'Взаимодействовать с игроком',
    priority: 50,
    targetState: { isTalking: true },
    isAchieved: (state) => state.isTalking,
    calculatePriority: (state, ctx) => {
      // Выше приоритет если игрок рядом и disposition положительный
      if (!state.playerNearby) return 0;
      if (state.disposition >= 0) return 60;
      if (state.disposition < -50) return 20; // Нужно отказать
      return 40;
    },
  },
  
  {
    id: 'trade_with_player',
    name: 'Торговать с игроком',
    priority: 40,
    targetState: { },
    isAchieved: () => false, // Постоянная цель
    calculatePriority: (state, ctx) => {
      if (!state.isTalking || !state.hasGoodsToSell) return 0;
      return state.disposition >= 0 ? 45 : 0;
    },
  },
  
  {
    id: 'teach_player',
    name: 'Обучить игрока',
    priority: 30,
    targetState: { },
    isAchieved: () => false,
    calculatePriority: (state, ctx) => {
      if (!state.isTalking || !state.canTeachTechnique) return 0;
      if (state.disposition >= 50 && state.playerReputation >= 50) return 35;
      return 0;
    },
  },
  
  {
    id: 'cultivate',
    name: 'Культивировать',
    priority: 10,
    targetState: { isMeditating: true },
    isAchieved: (state) => state.isMeditating,
    calculatePriority: (state, ctx) => {
      // Медитация ночью или когда нет игрока
      if (state.playerNearby || state.isTalking) return 0;
      if (state.timeOfDay >= 22 || state.timeOfDay < 5) return 25;
      return 10;
    },
  },
];

// ==================== GOAP ПЛАНИРОВЩИК ====================

class GOAPPlanner {
  private actions: GOAPAction[];
  private goals: GOAPGoal[];
  
  constructor(actions: GOAPAction[], goals: GOAPGoal[]) {
    this.actions = actions;
    this.goals = goals;
  }
  
  /**
   * Создать план действий для достижения цели
   */
  plan(
    currentState: GOAPWorldState,
    goal: GOAPGoal,
    context: AIContext
  ): GOAPAction[] | null {
    // A* поиск пути в пространстве состояний
    const openSet: PlanNode[] = [{
      state: currentState,
      actions: [],
      gCost: 0,
      hCost: this.heuristic(currentState, goal.targetState),
    }];
    
    const closedSet = new Set<string>();
    
    while (openSet.length > 0) {
      // Сортируем по f = g + h
      openSet.sort((a, b) => (a.gCost + a.hCost) - (b.gCost + b.hCost));
      
      const current = openSet.shift()!;
      const stateKey = this.stateToKey(current.state);
      
      // Цель достигнута?
      if (goal.isAchieved(current.state)) {
        return current.actions;
      }
      
      if (closedSet.has(stateKey)) continue;
      closedSet.add(stateKey);
      
      // Пробуем все действия
      for (const action of this.actions) {
        if (this.canExecuteAction(action, current.state, context)) {
          const newState = this.applyEffects(action, current.state);
          const newKey = this.stateToKey(newState);
          
          if (!closedSet.has(newKey)) {
            openSet.push({
              state: newState,
              actions: [...current.actions, action],
              gCost: current.gCost + action.cost,
              hCost: this.heuristic(newState, goal.targetState),
            });
          }
        }
      }
      
      // Ограничение на глубину поиска
      if (current.actions.length >= 5) continue;
    }
    
    return null; // План не найден
  }
  
  private canExecuteAction(
    action: GOAPAction,
    state: GOAPWorldState,
    context: AIContext
  ): boolean {
    // Проверяем статические предусловия
    for (const [key, value] of Object.entries(action.preconditions)) {
      if (state[key as keyof GOAPWorldState] !== value) {
        return false;
      }
    }
    
    // Проверяем динамические предусловия
    if (action.checkPreconditions && !action.checkPreconditions(state, context)) {
      return false;
    }
    
    return true;
  }
  
  private applyEffects(action: GOAPAction, state: GOAPWorldState): GOAPWorldState {
    return { ...state, ...action.effects };
  }
  
  private heuristic(state: GOAPWorldState, target: Partial<GOAPWorldState>): number {
    let diff = 0;
    for (const [key, value] of Object.entries(target)) {
      if (state[key as keyof GOAPWorldState] !== value) {
        diff += 1;
      }
    }
    return diff;
  }
  
  private stateToKey(state: GOAPWorldState): string {
    return JSON.stringify(state);
  }
}

interface PlanNode {
  state: GOAPWorldState;
  actions: GOAPAction[];
  gCost: number;
  hCost: number;
}

// ==================== GOAP КОНТРОЛЛЕР ====================

class GOAPController {
  private planner: GOAPPlanner;
  private currentPlan: GOAPAction[] = [];
  private currentActionIndex: number = 0;
  private currentGoal: GOAPGoal | null = null;
  private worldState: GOAPWorldState;
  
  constructor(
    actions: GOAPAction[],
    private goals: GOAPGoal[],
    initialState: GOAPWorldState
  ) {
    this.planner = new GOAPPlanner(actions, goals);
    this.worldState = initialState;
  }
  
  /**
   * Обновление AI каждый тик
   */
  update(context: AIContext): AIAction | null {
    // 1. Обновляем состояние мира
    this.updateWorldState(context);
    
    // 2. Если есть план - выполняем
    if (this.currentPlan.length > 0 && this.currentActionIndex < this.currentPlan.length) {
      return this.executeCurrentAction(context);
    }
    
    // 3. Выбираем новую цель
    const goal = this.selectGoal(context);
    if (!goal) return null;
    
    // 4. Создаём план
    const plan = this.planner.plan(this.worldState, goal, context);
    
    if (plan) {
      this.currentPlan = plan;
      this.currentActionIndex = 0;
      this.currentGoal = goal;
      console.log(`[GOAP] New plan for goal "${goal.name}":`, plan.map(a => a.name));
      return this.executeCurrentAction(context);
    }
    
    return null;
  }
  
  private updateWorldState(context: AIContext): void {
    const distanceToPlayer = context.distanceToPlayer || 999;
    
    this.worldState = {
      ...this.worldState,
      playerNearby: distanceToPlayer < 10,
      playerReputation: context.playerReputation || 0,
      playerLevel: context.playerLevel || 1,
      timeOfDay: context.timeOfDay || 12,
      hp: context.npc?.bodyState?.hp || 100,
      qi: context.npc?.qi?.current || 100,
      isTalking: context.isTalking || false,
      isMeditating: context.isMeditating || false,
      disposition: context.disposition || 0,
      factionRelation: context.factionRelation || 0,
      isInDanger: context.isInDanger || false,
      hasQuestsAvailable: context.hasQuestsAvailable || false,
      canTeachTechnique: context.canTeachTechnique || false,
      hasGoodsToSell: context.hasGoodsToSell || false,
    };
  }
  
  private selectGoal(context: AIContext): GOAPGoal | null {
    // Вычисляем приоритеты для всех целей
    const goalsWithPriority = this.goals.map(goal => ({
      goal,
      priority: goal.calculatePriority 
        ? goal.calculatePriority(this.worldState, context)
        : goal.priority,
    }));
    
    // Сортируем по приоритету
    goalsWithPriority.sort((a, b) => b.priority - a.priority);
    
    // Возвращаем цель с наивысшим приоритетом
    if (goalsWithPriority[0].priority > 0) {
      return goalsWithPriority[0].goal;
    }
    
    return null;
  }
  
  private executeCurrentAction(context: AIContext): AIAction | null {
    const action = this.currentPlan[this.currentActionIndex];
    if (!action) return null;
    
    return {
      type: action.id,
      params: { action },
      execute: async () => {
        const result = await action.execute(context);
        if (result.success) {
          this.currentActionIndex++;
          this.worldState = { ...this.worldState, ...result.newState };
        }
        return result;
      },
    };
  }
}
```

---

## 🎮 State Machine для временных NPC

### Почему State Machine?

State Machine идеально подходит для временных NPC (статистов), потому что:

1. **Простота** - Минимальные вычислительные затраты
2. **Предсказуемость** - Чётко определённые переходы
3. **Масштабируемость** - Можно создать множество NPC без лагов
4. **Простота отладки** - Легко понять текущее состояние

### Архитектура State Machine для временных NPC

```typescript
// src/lib/game/npc-ai-state-machine-temp.ts

/**
 * State Machine для временных NPC (статистов)
 */

// ==================== ТИПЫ ====================

type TempNPCState = 
  | 'idle'          // Стоит на месте
  | 'patrol'        // Патрулирует
  | 'wander'        // Бродит
  | 'alert'         // Насторожен
  | 'chase'         // Преследует
  | 'attack'        // Атакует
  | 'flee'          // Убегает
  | 'talk'          // Разговаривает
  | 'meditate'      // Медитирует
  | 'dead';         // Мёртв

interface TempNPCStateTransition {
  from: TempNPCState | '*';  // '*' = любой состояние
  to: TempNPCState;
  condition: (context: TempNPCContext) => boolean;
  priority: number;
}

interface TempNPCContext {
  npc: TempNPC;
  playerDistance: number;
  playerLevel: number;
  playerHostile: boolean;
  nearbyAllies: number;
  nearbyEnemies: number;
  timeOfDay: number;
  locationType: string;
  hpPercent: number;
  qiPercent: number;
}

interface TempNPCAction {
  type: string;
  params?: Record<string, unknown>;
}

// ==================== КОНФИГУРАЦИИ ДЛЯ РАЗНЫХ ТИПОВ ====================

const TEMP_NPC_CONFIGS: Record<string, {
  initialState: TempNPCState;
  transitions: TempNPCStateTransition[];
  actions: Record<TempNPCState, TempNPCAction>;
}> = {
  // Монстр (агрессивный)
  monster: {
    initialState: 'wander',
    transitions: [
      // Обнаружение
      { from: 'idle', to: 'wander', condition: ctx => true, priority: 1 },
      { from: 'wander', to: 'alert', condition: ctx => ctx.playerDistance < 20, priority: 10 },
      { from: 'alert', to: 'chase', condition: ctx => ctx.playerDistance < 15, priority: 20 },
      { from: 'chase', to: 'attack', condition: ctx => ctx.playerDistance < 2, priority: 30 },
      
      // Потеря цели
      { from: 'alert', to: 'wander', condition: ctx => ctx.playerDistance > 25, priority: 5 },
      { from: 'chase', to: 'wander', condition: ctx => ctx.playerDistance > 30, priority: 5 },
      
      // Бегство
      { from: '*', to: 'flee', condition: ctx => ctx.hpPercent < 0.2, priority: 100 },
      { from: 'flee', to: 'wander', condition: ctx => ctx.playerDistance > 50, priority: 10 },
    ],
    actions: {
      idle: { type: 'wait', params: { duration: 60 } },
      wander: { type: 'randomWalk', params: { speed: 0.5, range: 30 } },
      alert: { type: 'lookAt', params: { target: 'player' } },
      chase: { type: 'moveTo', params: { target: 'player', speed: 1.0 } },
      attack: { type: 'meleeAttack', params: { damage: 10 } },
      flee: { type: 'moveAway', params: { from: 'player', speed: 1.5 } },
      talk: { type: 'wait', params: {} },
      meditate: { type: 'wait', params: {} },
      dead: { type: 'none', params: {} },
    },
  },
  
  // Охранник (нейтральный)
  guard: {
    initialState: 'patrol',
    transitions: [
      // Патруль
      { from: 'idle', to: 'patrol', condition: ctx => ctx.timeOfDay >= 6 && ctx.timeOfDay < 22, priority: 5 },
      { from: 'patrol', to: 'idle', condition: ctx => ctx.timeOfDay >= 22 || ctx.timeOfDay < 6, priority: 5 },
      
      // Обнаружение угрозы
      { from: 'patrol', to: 'alert', condition: ctx => ctx.playerHostile && ctx.playerDistance < 15, priority: 20 },
      { from: 'idle', to: 'alert', condition: ctx => ctx.playerHostile && ctx.playerDistance < 10, priority: 20 },
      { from: 'alert', to: 'chase', condition: ctx => ctx.playerHostile && ctx.playerDistance < 12, priority: 25 },
      { from: 'chase', to: 'attack', condition: ctx => ctx.playerDistance < 2, priority: 30 },
      
      // Потеря цели
      { from: 'alert', to: 'patrol', condition: ctx => !ctx.playerHostile || ctx.playerDistance > 20, priority: 10 },
      { from: 'chase', to: 'patrol', condition: ctx => ctx.playerDistance > 25, priority: 10 },
      
      // Бегство (только если сильно слабее)
      { from: '*', to: 'flee', condition: ctx => ctx.hpPercent < 0.15 && ctx.playerLevel > (ctx.npc as any).cultivationLevel + 3, priority: 100 },
    ],
    actions: {
      idle: { type: 'wait', params: { duration: 120 } },
      patrol: { type: 'waypointPatrol', params: { route: 'default', speed: 0.3 } },
      alert: { type: 'lookAt', params: { target: 'player' } },
      chase: { type: 'moveTo', params: { target: 'player', speed: 0.8 } },
      attack: { type: 'meleeAttack', params: { damage: 15 } },
      flee: { type: 'moveAway', params: { from: 'player', speed: 1.2 } },
      talk: { type: 'wait', params: {} },
      meditate: { type: 'wait', params: {} },
      dead: { type: 'none', params: {} },
    },
  },
  
  // Прохожий (мирный)
  passerby: {
    initialState: 'wander',
    transitions: [
      // Бродить
      { from: 'idle', to: 'wander', condition: ctx => true, priority: 1 },
      
      // Избегать конфликта
      { from: 'wander', to: 'alert', condition: ctx => ctx.playerHostile && ctx.playerDistance < 20, priority: 20 },
      { from: 'alert', to: 'flee', condition: ctx => ctx.playerHostile && ctx.playerDistance < 15, priority: 30 },
      
      // Разговор (если игрок дружелюбен)
      { from: 'wander', to: 'talk', condition: ctx => !ctx.playerHostile && ctx.playerDistance < 5, priority: 15 },
      { from: 'talk', to: 'wander', condition: ctx => ctx.playerDistance > 8, priority: 5 },
      
      // Бегство при опасности
      { from: '*', to: 'flee', condition: ctx => ctx.nearbyEnemies > 0 || ctx.hpPercent < 0.5, priority: 100 },
      { from: 'flee', to: 'wander', condition: ctx => ctx.playerDistance > 40 && ctx.nearbyEnemies === 0, priority: 5 },
    ],
    actions: {
      idle: { type: 'wait', params: { duration: 30 } },
      wander: { type: 'randomWalk', params: { speed: 0.3, range: 50 } },
      alert: { type: 'lookAt', params: { target: 'player' } },
      chase: { type: 'wait', params: {} },
      attack: { type: 'wait', params: {} },
      flee: { type: 'moveAway', params: { from: 'player', speed: 1.0 } },
      talk: { type: 'dialog', params: { type: 'greeting' } },
      meditate: { type: 'wait', params: {} },
      dead: { type: 'none', params: {} },
    },
  },
  
  // Ученик секты
  disciple: {
    initialState: 'meditate',
    transitions: [
      // Расписание дня
      { from: '*', to: 'meditate', condition: ctx => (ctx.timeOfDay >= 5 && ctx.timeOfDay < 8) || (ctx.timeOfDay >= 18 && ctx.timeOfDay < 21), priority: 5 },
      { from: '*', to: 'patrol', condition: ctx => ctx.timeOfDay >= 8 && ctx.timeOfDay < 12, priority: 5 },
      { from: '*', to: 'wander', condition: ctx => ctx.timeOfDay >= 12 && ctx.timeOfDay < 14, priority: 5 },
      { from: '*', to: 'patrol', condition: ctx => ctx.timeOfDay >= 14 && ctx.timeOfDay < 18, priority: 5 },
      { from: '*', to: 'idle', condition: ctx => ctx.timeOfDay >= 21 || ctx.timeOfDay < 5, priority: 5 },
      
      // Реакция на угрозу
      { from: 'meditate', to: 'alert', condition: ctx => ctx.playerHostile && ctx.playerDistance < 15, priority: 50 },
      { from: 'patrol', to: 'alert', condition: ctx => ctx.playerHostile && ctx.playerDistance < 15, priority: 50 },
      { from: 'alert', to: 'chase', condition: ctx => ctx.playerHostile && ctx.playerDistance < 10, priority: 60 },
      { from: 'chase', to: 'attack', condition: ctx => ctx.playerDistance < 2, priority: 70 },
      
      // Бегство
      { from: '*', to: 'flee', condition: ctx => ctx.hpPercent < 0.25, priority: 100 },
      
      // Возврат к расписанию
      { from: 'alert', to: 'patrol', condition: ctx => !ctx.playerHostile || ctx.playerDistance > 25, priority: 10 },
      { from: 'flee', to: 'patrol', condition: ctx => ctx.playerDistance > 40 && ctx.hpPercent > 0.5, priority: 10 },
    ],
    actions: {
      idle: { type: 'wait', params: { duration: 60 } },
      patrol: { type: 'waypointPatrol', params: { route: 'sect_grounds', speed: 0.4 } },
      wander: { type: 'randomWalk', params: { speed: 0.3, range: 20 } },
      alert: { type: 'lookAt', params: { target: 'player' } },
      chase: { type: 'moveTo', params: { target: 'player', speed: 0.9 } },
      attack: { type: 'meleeAttack', params: { damage: 12 } },
      flee: { type: 'moveAway', params: { from: 'player', speed: 1.0 } },
      talk: { type: 'dialog', params: { type: 'greeting' } },
      meditate: { type: 'meditate', params: { qiGain: 5 } },
      dead: { type: 'none', params: {} },
    },
  },
};

// ==================== STATE MACHINE КОНТРОЛЛЕР ====================

class TempNPCStateMachine {
  private currentState: TempNPCState;
  private transitions: TempNPCStateTransition[];
  private actions: Record<TempNPCState, TempNPCAction>;
  private lastStateChange: number = 0;
  private stateTimer: number = 0;
  
  constructor(configType: string) {
    const config = TEMP_NPC_CONFIGS[configType] || TEMP_NPC_CONFIGS.passerby;
    this.currentState = config.initialState;
    this.transitions = config.transitions;
    this.actions = config.actions;
  }
  
  /**
   * Обновление AI каждый тик
   */
  update(context: TempNPCContext): TempNPCAction {
    // 1. Проверяем переходы
    const validTransitions = this.transitions
      .filter(t => t.from === this.currentState || t.from === '*')
      .filter(t => t.condition(context))
      .sort((a, b) => b.priority - a.priority);
    
    // 2. Выполняем переход с наивысшим приоритетом
    if (validTransitions.length > 0) {
      const transition = validTransitions[0];
      this.changeState(transition.to);
    }
    
    // 3. Возвращаем действие текущего состояния
    return this.actions[this.currentState];
  }
  
  private changeState(newState: TempNPCState): void {
    if (this.currentState !== newState) {
      console.log(`[StateMachine] ${this.currentState} → ${newState}`);
      this.currentState = newState;
      this.lastStateChange = Date.now();
      this.stateTimer = 0;
    }
  }
  
  getState(): TempNPCState {
    return this.currentState;
  }
}
```

---

## 🔗 Интеграция с системой фракций

### Обзор интеграции

Система фракций ([faction-system.md](./faction-system.md)) предоставляет:

1. **Принадлежность** - Государство → Фракция → Секта → NPC
2. **Disposition** - Отношение от -100 до +100
3. **Одеяние секты** - Визуальная идентификация и бонусы

### Расчёт disposition для AI

```typescript
// src/lib/game/npc-disposition.ts

import { FactionRelation, SectRelation } from '@/types/faction';

interface DispositionContext {
  // NPC данные
  npc: {
    id: string;
    sectId?: string;
    factionId?: string;
    nationId?: string;
    personalityDispositions: Record<string, number>;
  };
  
  // Игрок данные
  player: {
    id: string;
    sectId?: string;
    factionId?: string;
    nationId?: string;
    reputation: number;
    equippedAttire?: {
      sectId: string;
      dispositionBonus: number;
      dispositionPenalty: number;
    };
  };
  
  // Глобальные отношения
  sectRelations: Record<string, Record<string, number>>;
  factionRelations: Record<string, Record<string, number>>;
  nationRelations: Record<string, Record<string, number>>;
}

/**
 * Рассчитать итоговое disposition NPC к игроку
 */
export function calculateDisposition(ctx: DispositionContext): number {
  let disposition = 0;
  
  // 1. Базовое личное отношение NPC (если было взаимодействие)
  const personalDisposition = ctx.npc.personalityDispositions[ctx.player.id] || 0;
  disposition += personalDisposition;
  
  // 2. Отношение к секте игрока (вес 50%)
  if (ctx.player.sectId && ctx.npc.sectId) {
    const sectRelation = ctx.sectRelations[ctx.npc.sectId]?.[ctx.player.sectId] || 0;
    disposition += sectRelation * 0.5;
  }
  
  // 3. Отношение к фракции игрока (вес 30%)
  if (ctx.player.factionId && ctx.npc.factionId) {
    const factionRelation = ctx.factionRelations[ctx.npc.factionId]?.[ctx.player.factionId] || 0;
    disposition += factionRelation * 0.3;
  }
  
  // 4. Отношение к государству игрока (вес 20%)
  if (ctx.player.nationId && ctx.npc.nationId) {
    const nationRelation = ctx.nationRelations[ctx.npc.nationId]?.[ctx.player.nationId] || 0;
    disposition += nationRelation * 0.2;
  }
  
  // 5. Модификаторы экипировки (одеяние секты)
  if (ctx.player.equippedAttire) {
    const attire = ctx.player.equippedAttire;
    if (ctx.npc.sectId === attire.sectId) {
      // Тот же сект - бонус
      disposition += attire.dispositionBonus;
    } else if (areSectsAtWar(ctx.npc.sectId, attire.sectId, ctx.sectRelations)) {
      // Враждебный сект - штраф
      disposition += attire.dispositionPenalty;
    }
  }
  
  // 6. Модификаторы репутации игрока
  disposition += ctx.player.reputation * 0.1;
  
  // Ограничение диапазона
  return Math.max(-100, Math.min(100, disposition));
}

/**
 * Проверить, находятся ли секты в состоянии войны
 */
function areSectsAtWar(
  sectA: string | undefined,
  sectB: string | undefined,
  relations: Record<string, Record<string, number>>
): boolean {
  if (!sectA || !sectB) return false;
  const relation = relations[sectA]?.[sectB] || 0;
  return relation < -50; // Порог вражды
}

/**
 * Определить поведение на основе disposition
 */
export function getDispositionBehavior(disposition: number): {
  type: 'friendly' | 'neutral' | 'suspicious' | 'hostile' | 'hateful';
  canTalk: boolean;
  canTrade: boolean;
  willAttack: boolean;
  dialogueModifier: number;
} {
  if (disposition >= 80) {
    return {
      type: 'friendly',
      canTalk: true,
      canTrade: true,
      willAttack: false,
      dialogueModifier: 1.5, // Бонус в диалогах
    };
  }
  
  if (disposition >= 50) {
    return {
      type: 'friendly',
      canTalk: true,
      canTrade: true,
      willAttack: false,
      dialogueModifier: 1.2,
    };
  }
  
  if (disposition >= 20) {
    return {
      type: 'neutral',
      canTalk: true,
      canTrade: true,
      willAttack: false,
      dialogueModifier: 1.0,
    };
  }
  
  if (disposition >= -20) {
    return {
      type: 'neutral',
      canTalk: true,
      canTrade: false,
      willAttack: false,
      dialogueModifier: 0.8,
    };
  }
  
  if (disposition >= -50) {
    return {
      type: 'suspicious',
      canTalk: false,
      canTrade: false,
      willAttack: false,
      dialogueModifier: 0.5,
    };
  }
  
  if (disposition >= -80) {
    return {
      type: 'hostile',
      canTalk: false,
      canTrade: false,
      willAttack: true, // Атака при приближении
      dialogueModifier: 0.0,
    };
  }
  
  return {
    type: 'hateful',
    canTalk: false,
    canTrade: false,
    willAttack: true, // Атака без предупреждения
    dialogueModifier: -1.0,
  };
}
```

### Пример интеграции в AI

```typescript
// В GOAP или State Machine

function updateContext(npc: NPC, player: Player): AIContext {
  const disposition = calculateDisposition({
    npc: {
      id: npc.id,
      sectId: npc.sectId,
      factionId: npc.factionId,
      nationId: npc.nationId,
      personalityDispositions: npc.dispositions,
    },
    player: {
      id: player.id,
      sectId: player.sectId,
      factionId: player.factionId,
      nationId: player.nationId,
      reputation: player.reputation,
      equippedAttire: player.equippedAttire,
    },
    sectRelations: GAME_DATA.sectRelations,
    factionRelations: GAME_DATA.factionRelations,
    nationRelations: GAME_DATA.nationRelations,
  });
  
  const behavior = getDispositionBehavior(disposition);
  
  return {
    disposition,
    canTalk: behavior.canTalk,
    canTrade: behavior.canTrade,
    playerHostile: behavior.willAttack,
    // ... остальной контекст
  };
}
```

---

## 🔄 LoD (Level of Detail) для подмены AI

### Концепция

LoD позволяет динамически менять сложность AI в зависимости от ситуации:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    LOD ДЛЯ AI - УРОВНИ ДЕТАЛИЗАЦИИ                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  FULL (Полный)                                                    │  │
│  │  • Расстояние: < 10м                                              │  │
│  │  • AI: GOAP полный / State Machine полный                         │  │
│  │  • Частота обновления: каждый тик                                 │  │
│  │  • Пример: Диалог, бой, близкое взаимодействие                    │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  REDUCED (Упрощённый)                                             │  │
│  │  • Расстояние: 10-50м                                             │  │
│  │  • AI: GOAP упрощённый / State Machine базовый                    │  │
│  │  • Частота обновления: каждые 5 тиков                             │  │
│  │  • Пример: Патруль, медитация, ожидание                           │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  MINIMAL (Минимальный)                                            │  │
│  │  • Расстояние: 50-200м                                            │  │
│  │  • AI: Только движение (waypoints)                                │  │
│  │  • Частота обновления: каждую секунду                             │  │
│  │  • Пример: Фоновое движение                                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  DORMANT (Спящий)                                                 │  │
│  │  • Расстояние: > 200м                                             │  │
│  │  • AI: Отключён                                                   │  │
│  │  • Сохраняется только позиция                                     │  │
│  │  • Пример: NPC в другой локации                                   │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Ситуативная подмена AI

```typescript
// src/lib/game/npc-ai-lod.ts

enum AILevel {
  FULL = 'full',
  REDUCED = 'reduced',
  MINIMAL = 'minimal',
  DORMANT = 'dormant',
}

enum AISituation {
  NORMAL = 'normal',
  COMBAT = 'combat',
  DIALOG = 'dialog',
  MEDITATION = 'meditation',
}

interface LODConfig {
  distanceThresholds: {
    full: number;
    reduced: number;
    minimal: number;
  };
  situationOverrides: Record<AISituation, AILevel>;
}

const DEFAULT_LOD_CONFIG: LODConfig = {
  distanceThresholds: {
    full: 10,
    reduced: 50,
    minimal: 200,
  },
  situationOverrides: {
    normal: AILevel.FULL,
    combat: AILevel.FULL,
    dialog: AILevel.FULL,
    meditation: AILevel.REDUCED,
  },
};

class AILODManager {
  private config: LODConfig;
  private npcLevels: Map<string, AILevel> = new Map();
  
  constructor(config: LODConfig = DEFAULT_LOD_CONFIG) {
    this.config = config;
  }
  
  /**
   * Определить уровень AI для NPC
   */
  determineLevel(
    npcId: string,
    distanceToPlayer: number,
    situation: AISituation
  ): AILevel {
    // 1. Проверяем ситуативные переопределения
    const situationLevel = this.config.situationOverrides[situation];
    if (situationLevel === AILevel.FULL) {
      return AILevel.FULL; // Диалог и бой всегда FULL
    }
    
    // 2. Определяем по расстоянию
    let level: AILevel;
    if (distanceToPlayer < this.config.distanceThresholds.full) {
      level = AILevel.FULL;
    } else if (distanceToPlayer < this.config.distanceThresholds.reduced) {
      level = AILevel.REDUCED;
    } else if (distanceToPlayer < this.config.distanceThresholds.minimal) {
      level = AILevel.MINIMAL;
    } else {
      level = AILevel.DORMANT;
    }
    
    // 3. Если медитация - можно снизить уровень
    if (situation === AISituation.MEDITATION && level === AILevel.FULL) {
      level = AILevel.REDUCED;
    }
    
    // 4. Проверяем изменение уровня
    const previousLevel = this.npcLevels.get(npcId);
    if (previousLevel !== level) {
      this.onLevelChange(npcId, previousLevel, level);
    }
    
    this.npcLevels.set(npcId, level);
    return level;
  }
  
  private onLevelChange(npcId: string, from: AILevel | undefined, to: AILevel): void {
    console.log(`[LOD] NPC ${npcId}: ${from || 'none'} → ${to}`);
    
    // TODO: Подготовить AI для нового уровня
    // - FULL: Загрузить полный GOAP
    // - REDUCED: Упростить планирование
    // - MINIMAL: Только waypoints
    // - DORMANT: Сохранить состояние, выгрузить AI
  }
}
```

### Подмена AI для статичных NPC

```typescript
// Статичный торговец в бою → упрощённый AI

class StaticNPCAIController {
  private goapController: GOAPController;
  private stateMachine: TempNPCStateMachine;
  private currentAI: 'goap' | 'stateMachine' = 'goap';
  private lodLevel: AILevel = AILevel.FULL;
  
  update(context: AIContext): AIAction | null {
    // Определяем ситуацию
    const situation = this.determineSituation(context);
    
    // Подмена AI при бое
    if (situation === AISituation.COMBAT && this.currentAI === 'goap') {
      console.log('[AI] Switching to State Machine for combat');
      this.currentAI = 'stateMachine';
    }
    
    // Возврат к GOAP после боя
    if (situation === AISituation.NORMAL && this.currentAI === 'stateMachine') {
      console.log('[AI] Switching back to GOAP');
      this.currentAI = 'goap';
    }
    
    // Выполняем соответствующий AI
    if (this.currentAI === 'goap') {
      return this.goapController.update(context);
    } else {
      const action = this.stateMachine.update(this.convertContext(context));
      return this.convertAction(action);
    }
  }
  
  private determineSituation(context: AIContext): AISituation {
    if (context.isInDanger || context.nearbyEnemies > 0) {
      return AISituation.COMBAT;
    }
    if (context.isTalking) {
      return AISituation.DIALOG;
    }
    if (context.npc.isMeditating) {
      return AISituation.MEDITATION;
    }
    return AISituation.NORMAL;
  }
}
```

---

## 🏗️ Архитектура гибридной системы

### Общая структура

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ГИБРИДНАЯ AI СИСТЕМА                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    AI MANAGER (Singleton)                          │  │
│  │                                                                    │  │
│  │  • Управляет всеми AI контроллерами                               │  │
│  │  • Определяет LoD для каждого NPC                                 │  │
│  │  • Распределяет обновления по кадрам                              │  │
│  │  • Управляет пулом AI объектов                                    │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                              │                                           │
│                              ▼                                           │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    AI CONTROLLER (per NPC)                         │  │
│  │                                                                    │  │
│  │  • Выбирает тип AI (GOAP / State Machine)                         │  │
│  │  • Управляет текущим планом/состоянием                            │  │
│  │  • Интеграция с фракциями                                         │  │
│  │  • Обработка событий                                              │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                              │                                           │
│              ┌───────────────┴───────────────┐                          │
│              ▼                               ▼                           │
│  ┌─────────────────────┐         ┌─────────────────────┐               │
│  │  GOAP PLANNER       │         │  STATE MACHINE      │               │
│  │  (Статичные NPC)    │         │  (Временные NPC)    │               │
│  │                     │         │                     │               │
│  │  • Цели             │         │  • Состояния        │               │
│  │  • Действия         │         │  • Переходы         │               │
│  │  • Планировщик A*   │         │  • Приоритеты       │               │
│  └─────────────────────┘         └─────────────────────┘               │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    DISPOSITION SYSTEM                              │  │
│  │                                                                    │  │
│  │  • Расчёт отношений на основе фракций                             │  │
│  │  • Модификаторы от экипировки                                     │  │
│  │  • Определение поведения (friendly/neutral/hostile)               │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    PHASER INTEGRATION                              │  │
│  │                                                                    │  │
│  │  • update() вызывается в сцене                                    │  │
│  │  • Действия → анимации/движение                                   │  │
│  │  • События → AI (игрок вошёл, атака, диалог)                      │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📁 Следующие шаги

### Фаза 1: Базовая реализация (приоритет)
1. ✅ **Теоретическая разработка** (этот документ)
2. ⏳ **Создать базовый AI Manager** - единый контроллер для всех NPC
3. ⏳ **Реализовать State Machine** для временных NPC
4. ⏳ **Интегрировать с Phaser** - привязать к игровому циклу
5. ⏳ **Протестировать** на простых NPC (монстры, прохожие)

### Фаза 2: Расширенная функциональность
1. ⏳ **Реализовать GOAP** для статичных NPC
2. ⏳ **Интегрировать систему фракций** - расчёт disposition
3. ⏳ **Добавить LoD систему** - оптимизация производительности
4. ⏳ **Создать конфигурации** для разных типов NPC

### Фаза 3: Полировка
1. ⏳ **Utility AI** для культиваторов
2. ⏳ **Behavior Tree** для боссов
3. ⏳ **Оптимизация** - пулинг, батчинг
4. ⏳ **Документирование API**

---

## 📋 Чеклист перед реализацией

- [ ] Изучить существующий код NPC в проекте
- [ ] Определить точку интеграции в Phaser сцену
- [ ] Создать базовые типы и интерфейсы
- [ ] Реализовать простой State Machine для теста
- [ ] Проверить работу на одном NPC
- [ ] Масштабировать на все временные NPC
- [ ] Добавить GOAP для статичных NPC

---

**АВТОР**: AI Assistant  
**ВЕРСИЯ**: 2.0 (Драфт - активное развитие)
**ДАТА**: 2026-03-24
