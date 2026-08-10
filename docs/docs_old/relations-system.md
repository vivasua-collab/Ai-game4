# Система отношений и взаимодействий

**Версия:** 1.0 | **Дата:** 2026-03-03

---

## 📋 Обзор

Документ описывает систему отношений между сущностями мира (персонажи, NPC, секты, фракции, государства) и механику взаимодействий (мирные и враждебные действия).

---

## 🎯 Уровни отношений

### Иерархия отношений

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        УРОВНИ ОТНОШЕНИЙ                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   1. ЛИЧНЫЕ (Character ↔ Character/NPC)                                 │
│      │                                                                   │
│      ├─ disposition: -100 до 100                                        │
│      ├─ Влияние: прямое взаимодействие                                  │
│      └─ Источник: действия игрока, диалоги, квесты                      │
│                                                                          │
│   2. СЕКТОВЫЕ (Sect ↔ Sect)                                             │
│      │                                                                   │
│      ├─ relations: -100 до 100                                          │
│      ├─ Влияние: члены сект к чужакам                                   │
│      └─ Источник: войны, союзы, политические действия                   │
│                                                                          │
│   3. ФРАКЦИОННЫЕ (Faction ↔ Faction)                                    │
│      │                                                                   │
│      ├─ relations: -100 до 100                                          │
│      ├─ Влияние: все секты фракции                                      │
│      └─ Источник: идеологические конфликты, альянсы                     │
│                                                                          │
│   4. ГОСУДАРСТВЕННЫЕ (Nation ↔ Nation)                                  │
│      │                                                                   │
│      ├─ relations: -100 до 100                                          │
│      ├─ Влияние: все секты государства                                  │
│      └─ Источник: дипломатия, войны, торговля                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Веса уровней

| Уровень | Вес в расчёте | Пример влияния |
|---------|---------------|----------------|
| Личный | 100% | NPC ненавидит игрока лично |
| Сектовый | 50% | Секта NPC враждует с сектой игрока |
| Фракционный | 30% | Фракции идеологически против |
| Государственный | 20% | Государства в холодной войне |

---

## 📊 Модель данных

### Расширение NPC

```typescript
interface NPCRelations {
  // Личные отношения
  personal: {
    disposition: number;           // -100 до 100 (к ГГ)
    memory: RelationMemory[];      // История взаимодействий
  };
  
  // Отношения к другим сущностям
  entities: Record<string, EntityRelation>;
}

interface EntityRelation {
  targetId: string;
  targetType: 'character' | 'npc' | 'sect' | 'faction' | 'nation';
  disposition: number;
  lastUpdated: number;             // Timestamp
  
  // Причины изменения
  modifiers: RelationModifier[];
}

interface RelationModifier {
  source: string;                  // Источник изменения
  value: number;                   // Изменение (+/-)
  reason: string;                  // Причина
  timestamp: number;
  expires?: number;                // Временный модификатор
}

interface RelationMemory {
  action: string;                  // Тип действия
  target: string;                  // К кому
  impact: number;                  // Влияние на отношение
  timestamp: number;
  description?: string;
}
```

### Prisma Schema

```prisma
// Расширение NPC модели
model NPC {
  // ... существующие поля ...
  
  // === Отношения ===
  relations String @default("{}") // JSON: NPCRelations
}

// Новая модель для истории отношений
model RelationEvent {
  id        String   @id @default(cuid())
  createdAt DateTime @default(now())
  
  sessionId String
  
  // Кто к кому
  sourceId   String
  sourceType String   // character, npc, sect, faction, nation
  targetId   String
  targetType String
  
  // Изменение
  dispositionChange Int
  reason    String
  action    String   // Тип действия
  
  // Временное?
  isTemporary Boolean @default(false)
  expiresAt   DateTime?
  
  @@index([sourceId])
  @@index([targetId])
  @@index([sessionId])
}
```

---

## 🔄 Расчёт итогового отношения

### Алгоритм

```typescript
interface DispositionContext {
  // Источник (кто оценивает)
  source: {
    id: string;
    type: 'npc' | 'character';
    sectId?: string;
    factionId?: string;
    nationId?: string;
    personalDisposition?: number;
  };
  
  // Цель (кого оценивают)
  target: {
    id: string;
    type: 'character' | 'npc' | 'sect' | 'faction' | 'nation';
    sectId?: string;
    factionId?: string;
    nationId?: string;
    
    // Экипировка
    attire?: {
      sectId: string;
      effects: AttireEffects;
    };
  };
  
  // Контекст
  context: {
    locationId?: string;
    isPrivateProperty?: boolean;
    recentActions?: string[];
  };
}

function calculateDisposition(ctx: DispositionContext): number {
  let totalDisposition = 0;
  let totalWeight = 0;
  
  // 1. Личное отношение (вес 1.0)
  if (ctx.source.personalDisposition !== undefined) {
    totalDisposition += ctx.source.personalDisposition * 1.0;
    totalWeight += 1.0;
  }
  
  // 2. Сектовые отношения (вес 0.5)
  if (ctx.source.sectId && ctx.target.sectId) {
    const sectRelation = getSectRelation(ctx.source.sectId, ctx.target.sectId);
    totalDisposition += sectRelation * 0.5;
    totalWeight += 0.5;
  }
  
  // 3. Фракционные отношения (вес 0.3)
  if (ctx.source.factionId && ctx.target.factionId) {
    const factionRelation = getFactionRelation(
      ctx.source.factionId, 
      ctx.target.factionId
    );
    totalDisposition += factionRelation * 0.3;
    totalWeight += 0.3;
  }
  
  // 4. Государственные отношения (вес 0.2)
  if (ctx.source.nationId && ctx.target.nationId) {
    const nationRelation = getNationRelation(
      ctx.source.nationId, 
      ctx.target.nationId
    );
    totalDisposition += nationRelation * 0.2;
    totalWeight += 0.2;
  }
  
  // 5. Эффекты экипировки (одеяние секты)
  if (ctx.target.attire) {
    const attireEffects = calculateAttireEffects(
      ctx.source.sectId,
      ctx.target.attire
    );
    totalDisposition += attireEffects;
    // Не добавляем вес, это модификатор
  }
  
  // Нормализация
  if (totalWeight > 0) {
    totalDisposition = totalDisposition / totalWeight;
  }
  
  // Ограничение диапазона
  return Math.max(-100, Math.min(100, Math.round(totalDisposition)));
}
```

### Примеры расчёта

```typescript
// Пример 1: Член враждебной секты
const context1: DispositionContext = {
  source: {
    id: 'npc_guard',
    type: 'npc',
    sectId: 'sect_heavenly_sword',
    personalDisposition: 0,  // Не знает лично
  },
  target: {
    id: 'player',
    type: 'character',
    sectId: 'sect_blood_moon',
    attire: {
      sectId: 'sect_blood_moon',
      effects: { dispositionPenaltyWithEnemies: -30 },
    },
  },
};

// Расчёт:
// personal: 0 * 1.0 = 0
// sect: -80 (враги) * 0.5 = -40
// attire: -30 (штраф за одеяние врага)
// Итого: 0 + (-40) + (-30) = -70 → Враждебный

// Пример 2: Односектовец
const context2: DispositionContext = {
  source: {
    id: 'npc_elder',
    type: 'npc',
    sectId: 'sect_heavenly_sword',
    personalDisposition: 20,  // Немного знает
  },
  target: {
    id: 'player',
    type: 'character',
    sectId: 'sect_heavenly_sword',
    attire: {
      sectId: 'sect_heavenly_sword',
      effects: { dispositionBonusWithSect: 20 },
    },
  },
};

// Расчёт:
// personal: 20 * 1.0 = 20
// sect: 50 (одна секта) * 0.5 = 25
// attire: +20 (бонус за одеяние секты)
// Итого: 20 + 25 + 20 = 65 → Благосклонный
```

---

## ⚔️ Типы взаимодействий

### Классификация действий

```typescript
type InteractionType =
  // === Мирные ===
  | 'greet'           // Приветствие
  | 'trade'           // Торговля
  | 'dialog'          // Разговор
  | 'train'           // Обучение
  | 'quest'           // Квест
  | 'gift'            // Подарок
  | 'share_technique' // Передача техники
  | 'heal'            // Лечение
  
  // === Нейтральные ===
  | 'observe'         // Наблюдение
  | 'ignore'          // Игнорирование
  | 'pass_by'         // Проход мимо
  
  // === Враждебные ===
  | 'steal'           // Кража
  | 'attack'          // Атака
  | 'insult'          // Оскорбление
  | 'threaten'        // Угроза
  | 'spy'             // Шпионаж
  | 'sabotage'        // Саботаж
  | 'assassinate';    // Убийство
```

### Влияние действий на отношения

```typescript
const INTERACTION_IMPACT: Record<InteractionType, InteractionImpact> = {
  // Мирные
  greet: {
    dispositionChange: { min: 1, max: 5 },
    affectsPersonal: true,
    affectsSect: false,
    requiredDisposition: { min: -50, max: 100 },
  },
  
  trade: {
    dispositionChange: { min: 2, max: 10 },
    affectsPersonal: true,
    affectsSect: false,
    requiredDisposition: { min: 20, max: 100 },
    goldModifier: 1.0,  // Базовые цены
  },
  
  gift: {
    dispositionChange: { min: 5, max: 30 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.3,
    requiredDisposition: { min: -30, max: 100 },
  },
  
  share_technique: {
    dispositionChange: { min: 15, max: 50 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.5,
    requiredDisposition: { min: 50, max: 100 },
  },
  
  heal: {
    dispositionChange: { min: 10, max: 25 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.2,
  },
  
  // Враждебные
  steal: {
    dispositionChange: { min: -30, max: -10 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.5,
    isCrime: true,
    witnessRadius: 50,
  },
  
  attack: {
    dispositionChange: { min: -50, max: -20 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.7,
    isCrime: true,
    witnessRadius: 100,
  },
  
  insult: {
    dispositionChange: { min: -15, max: -5 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 0.3,
    requiredDisposition: { min: -100, max: 50 },
  },
  
  assassinate: {
    dispositionChange: { min: -100, max: -80 },
    affectsPersonal: true,
    affectsSect: true,
    sectImpactMultiplier: 1.0,
    isCrime: true,
    isMajorCrime: true,
  },
};

interface InteractionImpact {
  dispositionChange: { min: number; max: number };
  affectsPersonal: boolean;
  affectsSect: boolean;
  sectImpactMultiplier?: number;
  requiredDisposition?: { min: number; max: number };
  
  // Торговля
  goldModifier?: number;
  
  // Преступления
  isCrime?: boolean;
  isMajorCrime?: boolean;
  witnessRadius?: number;
  
  // Репутация
  reputationImpact?: number;
}
```

---

## 🛡️ Защитные механизмы

### Пороги агрессии

```typescript
interface AggressionThreshold {
  disposition: number;
  canInteract: InteractionType[];
  willAttack: boolean;
  willFlee: boolean;
  willCallGuards: boolean;
}

const AGGRESSION_THRESHOLDS: AggressionThreshold[] = [
  {
    disposition: 80,
    canInteract: ['greet', 'trade', 'dialog', 'train', 'quest', 'gift', 'share_technique', 'heal'],
    willAttack: false,
    willFlee: false,
    willCallGuards: false,
  },
  {
    disposition: 50,
    canInteract: ['greet', 'trade', 'dialog', 'quest'],
    willAttack: false,
    willFlee: false,
    willCallGuards: false,
  },
  {
    disposition: 20,
    canInteract: ['greet', 'dialog'],
    willAttack: false,
    willFlee: false,
    willCallGuards: false,
  },
  {
    disposition: -20,
    canInteract: ['greet'],
    willAttack: false,
    willFlee: false,
    willCallGuards: true,  // Если видит преступление
  },
  {
    disposition: -50,
    canInteract: [],
    willAttack: true,      // Атака при приближении
    willFlee: false,
    willCallGuards: true,
  },
  {
    disposition: -80,
    canInteract: [],
    willAttack: true,      // Атака без предупреждения
    willFlee: false,
    willCallGuards: true,
  },
];
```

### Расчёт реакции NPC

```typescript
function calculateNPCReaction(
  npc: NPC,
  player: Character,
  action: InteractionType,
  context: InteractionContext
): NPCReaction {
  const disposition = calculateDisposition({
    source: npc,
    target: player,
    context,
  });
  
  const threshold = findThreshold(disposition);
  const impact = INTERACTION_IMPACT[action];
  
  // Проверка доступности действия
  if (impact.requiredDisposition) {
    if (disposition < impact.requiredDisposition.min || 
        disposition > impact.requiredDisposition.max) {
      return {
        success: false,
        reason: 'Отказ в действии',
        message: generateRefusalMessage(npc, disposition),
      };
    }
  }
  
  // Преступление?
  if (impact.isCrime && context.witnesses.length > 0) {
    // Обновляем отношение свидетелей
    for (const witness of context.witnesses) {
      updateDisposition(witness, player.id, impact.dispositionChange.min);
    }
    
    // Звоним страже
    if (threshold.willCallGuards) {
      return {
        success: true,
        guardsAlerted: true,
        wantedLevel: impact.isMajorCrime ? 3 : 1,
      };
    }
  }
  
  // Агрессия
  if (threshold.willAttack) {
    return {
      success: false,
      combatInitiated: true,
      npcDisposition: disposition,
    };
  }
  
  // Успешное взаимодействие
  const dispositionChange = randomInRange(impact.dispositionChange);
  updateDisposition(npc, player.id, dispositionChange);
  
  // Сектовый эффект
  if (impact.affectsSect && impact.sectImpactMultiplier) {
    updateSectRelation(npc.sectId, player.sectId, 
      dispositionChange * impact.sectImpactMultiplier);
  }
  
  return {
    success: true,
    dispositionChange,
    newDisposition: disposition + dispositionChange,
    message: generateResponseMessage(npc, action, dispositionChange),
  };
}
```

---

## 🎭 Система репутации

### Глобальная репутация персонажа

```typescript
interface CharacterReputation {
  // Общая репутация
  overall: number;                 // -100 до 100
  
  // По сектам
  bySect: Record<string, number>;
  
  // По фракциям
  byFaction: Record<string, number>;
  
  // По государствам
  byNation: Record<string, number>;
  
  // Титулы и прозвища
  titles: ReputationTitle[];
  
  // Известные преступления
  crimes: CrimeRecord[];
  
  // Известные подвиги
  deeds: DeedRecord[];
}

interface ReputationTitle {
  id: string;
  name: string;
  description: string;
  effects: {
    dispositionBonus?: number;
    tradeBonus?: number;
    questUnlock?: string[];
  };
  requiredReputation: { min: number; max?: number };
  requiredRegion?: string;         // Ограничение регионом
}

const REPUTATION_TITLES: ReputationTitle[] = [
  {
    id: 'outcast',
    name: 'Изгой',
    description: 'Отверженный культиваторского мира',
    effects: {
      dispositionBonus: -20,
    },
    requiredReputation: { max: -50 },
  },
  {
    id: 'demon_cultivator',
    name: 'Демонический культиватор',
    description: 'Известен запретными практиками',
    effects: {
      dispositionBonus: -30,
      questUnlock: ['demonic_sect_recruit'],
    },
    requiredReputation: { min: -80, max: -30 },
  },
  {
    id: 'righteous_hero',
    name: 'Праведный герой',
    description: 'Защитник слабых и борец со злом',
    effects: {
      dispositionBonus: 15,
      tradeBonus: 0.9,
    },
    requiredReputation: { min: 50 },
  },
  {
    id: 'legendary_master',
    name: 'Легендарный мастер',
    description: 'Величайший культиватор поколения',
    effects: {
      dispositionBonus: 30,
      tradeBonus: 0.7,
      questUnlock: ['master_challenges', 'sect_leadership'],
    },
    requiredReputation: { min: 80 },
  },
];
```

---

## 📡 API Endpoints

### Отношения

| Эндпоинт | Метод | Описание |
|----------|-------|----------|
| `/api/relations/check` | GET | Проверка отношения NPC к игроку |
| `/api/relations/update` | POST | Обновление отношения |
| `/api/relations/history` | GET | История отношений |
| `/api/relations/sect` | GET/POST | Отношения между сектами |
| `/api/relations/faction` | GET/POST | Отношения между фракциями |

### Взаимодействия

| Эндпоинт | Метод | Описание |
|----------|-------|----------|
| `/api/interaction/perform` | POST | Выполнить действие |
| `/api/interaction/available` | GET | Доступные действия с NPC |
| `/api/interaction/consequence` | GET | Последствия действия |

### Пример запроса

```typescript
// POST /api/interaction/perform
interface PerformInteractionRequest {
  sessionId: string;
  npcId: string;
  action: InteractionType;
  
  // Дополнительные параметры
  params?: {
    itemId?: string;        // Для gift, trade
    techniqueId?: string;   // Для share_technique
    amount?: number;        // Для trade
    message?: string;       // Для dialog
  };
}

interface PerformInteractionResponse {
  success: boolean;
  disposition: {
    before: number;
    change: number;
    after: number;
  };
  message: string;
  consequences: {
    guardsAlerted?: boolean;
    reputationChange?: number;
    sectRelationChange?: number;
    itemsLost?: string[];
    itemsGained?: string[];
  };
}
```

---

## 🚀 Следующие шаги

1. **Реализовать Prisma схему** — добавить поля для отношений
2. **Создать сервис RelationService** — расчёт и обновление отношений
3. **Создать API для взаимодействий** — endpoints для действий
4. **Интегрировать с движком** — Event Bus для боевых взаимодействий
5. **Добавить UI** — отображение отношения, доступные действия
