# NPC AI Нейротеория - Биомиметическая архитектура

**Версия:** 1.1 (Теоретическая разработка)
**Дата:** 2026-03-24
**Статус:** 📋 Исследование + Техническая проработка
**Автор:** AI Assistant

---

## 📋 Обзор документа

Документ описывает биомиметическую архитектуру искусственного интеллекта, вдохновлённую устройством нервной системы живых существ. Архитектура разделяет AI на три уровня: рефлекторный (позвоночник), связующий (нервная система) и когнитивный (мозг/LLM).

### Связанные документы
- [NPC_AI_THEORY.md](./NPC_AI_THEORY.md) - Базовая теория AI
- [faction-system.md](./faction-system.md) - Система фракций
- [ARCHITECTURE.md](./ARCHITECTURE.md) - Архитектура проекта

---

## 🧬 Концепция

### Биологический прототип

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    НЕРВНАЯ СИСТЕМА ЖИВОГО СУЩЕСТВА                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                        МОЗГ (CNS)                                │   │
│   │                                                                  │   │
│   │  • Сознательные решения                                         │   │
│   │  • Планирование                                                 │   │
│   │  • Сложные рассуждения                                          │   │
│   │  • Обучение                                                     │   │
│   │  • Эмоции и личность                                            │   │
│   │                                                                  │   │
│   │  Скорость: ~100-500мс (медленно)                                │   │
│   │  Энергия: Высокая                                               │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              ▲                                           │
│                              │ Нервные волокна                           │
│                              │ (асинхронная передача)                    │
│                              ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                    НЕРВНАЯ СИСТЕМА                               │   │
│   │                                                                  │   │
│   │  • Маршрутизация сигналов                                       │   │
│   │  • Асинхронная передача                                         │   │
│   │  • Буферизация                                                   │   │
│   │  • Приоритизация                                                │   │
│   │                                                                  │   │
│   │  Скорость: ~10-50мс                                             │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              ▲                                           │
│                              │ Спинной мозг                              │
│                              ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                      ПОЗВОНОЧНИК                                 │   │
│   │                                                                  │   │
│   │  • Рефлексы (автоматические реакции)                            │   │
│   │  • Двигательные паттерны                                        │   │
│   │  • Поддержание позы                                              │   │
│   │  • Базовые инстинкты                                            │   │
│   │                                                                  │   │
│   │  Скорость: ~1-10мс (очень быстро)                               │   │
│   │  Энергия: Минимальная                                           │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Аналогия для NPC AI

| Биология | NPC AI | Назначение |
|----------|--------|------------|
| **Позвоночник** | Spinal AI (RNS) | Рефлексы, движение, избегание |
| **Нервная система** | Neural Router | Маршрутизация, буферизация, асинхронность |
| **Мозг** | LLM Controller | Сложные решения, диалоги, планирование |

---

## 🦴 Уровень 1: Spinal AI (Позвоночник)

### Назначение

Spinal AI - это быстрая, лёгкая система автоматических реакций ("подсознание" NPC). Работает без участия LLM, обеспечивает мгновенный отклик на физические стимулы.

### Характеристики

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         SPINAL AI (ПОЗВОНОЧНИК)                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Входные сигналы:                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ • Столкновение с препятствием                                    │    │
│  │ • Получение урона                                                │    │
│  │ • Потеря равновесия                                              │    │
│  │ • Приближение края (обрыв, вода)                                 │    │
│  │ • Температура, свет, звук                                        │    │
│  │ • Голод, усталость (физиология)                                  │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Рефлексы:                                                               │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ • Отдёрнуть руку от горячего                                     │    │
│  │ • Пошатнуться при ударе                                          │    │
│  │ • Отпрыгнуть от опасности                                        │    │
│  │ • Восстановить равновесие                                        │    │
│  │ • Повернуться на громкий звук                                    │    │
│  │ • Уклониться от летящего объекта                                 │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Скорость реакции: 1-10мс (1-2 кадра)                                   │
│  Вычислительная сложность: O(1) - O(n)                                  │
│  Память: Минимальная (только текущее состояние)                         │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Реализация

```typescript
// src/lib/game/ai/spinal-ai.ts

/**
 * Spinal AI - быстрая рефлекторная система
 * "Подсознание" NPC - работает без LLM
 */

// ==================== ТИПЫ ====================

interface SpinalSignal {
  type: SpinalSignalType;
  intensity: number;      // 0.0 - 1.0
  direction?: Vector2D;   // Направление воздействия
  source?: string;        // Источник сигнала
  timestamp: number;
}

type SpinalSignalType =
  | 'collision'       // Столкновение
  | 'damage'          // Урон
  | 'danger_nearby'   // Опасность рядом
  | 'edge_detected'   // Обнаружен край
  | 'loud_sound'      // Громкий звук
  | 'temperature'     // Температура
  | 'balance_lost'    // Потеря равновесия
  | 'hunger'          // Голод
  | 'fatigue';        // Усталость

interface SpinalReflex {
  id: string;
  name: string;
  triggerCondition: (signal: SpinalSignal, state: SpinalState) => boolean;
  execute: (signal: SpinalSignal, state: SpinalState) => SpinalAction;
  priority: number;
  cooldown: number;      // Минимальный интервал между срабатываниями
  lastTriggered: number;
}

interface SpinalState {
  // Физическое состояние
  position: Vector2D;
  velocity: Vector2D;
  isGrounded: boolean;
  isMoving: boolean;
  
  // Физиология
  hp: number;
  hpPercent: number;
  qi: number;
  fatigue: number;
  hunger: number;
  
  // Восприятие
  nearbyThreats: Threat[];
  nearbyEdges: Edge[];
  lastSoundSource: Vector2D | null;
  
  // Текущее действие
  currentReflex: string | null;
  reflexTimer: number;
}

interface SpinalAction {
  type: SpinalActionType;
  params: Record<string, unknown>;
  duration: number;      // Длительность в мс
  canInterrupt: boolean; // Можно ли прервать другим рефлексом
}

type SpinalActionType =
  | 'dodge'           // Уклонение
  | 'stumble'         // Пошатнуться
  | 'freeze'          // Замереть
  | 'turn_to_sound'   // Повернуться на звук
  | 'step_back'       // Шаг назад
  | 'balance_recover' // Восстановить равновесие
  | 'flinch'          // Вздрогнуть
  | 'look_around'     // Осмотреться
  | 'retreat';        // Отступить

// ==================== БАЗОВЫЕ РЕФЛЕКСЫ ====================

const SPINAL_REFLEXES: SpinalReflex[] = [
  // Уклонение от опасности
  {
    id: 'danger_dodge',
    name: 'Уклонение от опасности',
    priority: 100,
    cooldown: 500,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      if (signal.type !== 'danger_nearby') return false;
      if (signal.intensity < 0.7) return false;
      return true;
    },
    
    execute: (signal, state) => ({
      type: 'dodge',
      params: {
        direction: signal.direction 
          ? { x: -signal.direction.x, y: -signal.direction.y }
          : { x: Math.random() - 0.5, y: Math.random() - 0.5 },
        distance: 2 + signal.intensity * 3,
        speed: 1.5,
      },
      duration: 300,
      canInterrupt: false,
    }),
  },
  
  // Рефлекс на урон
  {
    id: 'pain_flinch',
    name: 'Вздрогнуть от боли',
    priority: 90,
    cooldown: 300,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      return signal.type === 'damage' && signal.intensity > 0.3;
    },
    
    execute: (signal, state) => ({
      type: 'flinch',
      params: {
        direction: signal.direction,
        intensity: signal.intensity,
      },
      duration: 200 + signal.intensity * 200,
      canInterrupt: true,
    }),
  },
  
  // Пошатнуться при столкновении
  {
    id: 'collision_stumble',
    name: 'Пошатнуться при столкновении',
    priority: 80,
    cooldown: 200,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      return signal.type === 'collision';
    },
    
    execute: (signal, state) => ({
      type: 'stumble',
      params: {
        direction: signal.direction,
        intensity: signal.intensity,
      },
      duration: 150,
      canInterrupt: true,
    }),
  },
  
  // Отойти от края
  {
    id: 'edge_retreat',
    name: 'Отойти от края',
    priority: 85,
    cooldown: 500,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      return signal.type === 'edge_detected';
    },
    
    execute: (signal, state) => ({
      type: 'step_back',
      params: {
        direction: signal.direction, // Направление от края
        distance: 1,
      },
      duration: 300,
      canInterrupt: false,
    }),
  },
  
  // Повернуться на звук
  {
    id: 'sound_orient',
    name: 'Повернуться на звук',
    priority: 30,
    cooldown: 1000,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      return signal.type === 'loud_sound' && signal.intensity > 0.5;
    },
    
    execute: (signal, state) => ({
      type: 'turn_to_sound',
      params: {
        targetDirection: signal.direction,
        speed: 0.8,
      },
      duration: 400,
      canInterrupt: true,
    }),
  },
  
  // Восстановить равновесие
  {
    id: 'balance_recover',
    name: 'Восстановить равновесие',
    priority: 70,
    cooldown: 0,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      return signal.type === 'balance_lost';
    },
    
    execute: (signal, state) => ({
      type: 'balance_recover',
      params: {},
      duration: 500,
      canInterrupt: false,
    }),
  },
];

// ==================== SPINAL AI КОНТРОЛЛЕР ====================

class SpinalAIController {
  private reflexes: SpinalReflex[];
  private state: SpinalState;
  private signalQueue: SpinalSignal[] = [];
  private currentAction: SpinalAction | null = null;
  private actionStartTime: number = 0;
  
  constructor(initialState: Partial<SpinalState> = {}) {
    this.reflexes = [...SPINAL_REFLEXES].sort((a, b) => b.priority - a.priority);
    this.state = {
      position: { x: 0, y: 0 },
      velocity: { x: 0, y: 0 },
      isGrounded: true,
      isMoving: false,
      hp: 100,
      hpPercent: 1.0,
      qi: 100,
      fatigue: 0,
      hunger: 0,
      nearbyThreats: [],
      nearbyEdges: [],
      lastSoundSource: null,
      currentReflex: null,
      reflexTimer: 0,
      ...initialState,
    };
  }
  
  /**
   * Получить сигнал от сенсоров
   */
  receiveSignal(signal: SpinalSignal): void {
    this.signalQueue.push(signal);
  }
  
  /**
   * Обновление каждый кадр
   * Возвращает действие или null
   */
  update(deltaMs: number): SpinalAction | null {
    const now = Date.now();
    
    // 1. Если текущее действие активно - продолжаем его
    if (this.currentAction) {
      const elapsed = now - this.actionStartTime;
      if (elapsed < this.currentAction.duration) {
        return this.currentAction;
      }
      this.currentAction = null;
    }
    
    // 2. Обрабатываем сигналы
    while (this.signalQueue.length > 0) {
      const signal = this.signalQueue.shift()!;
      
      // Ищем подходящий рефлекс
      for (const reflex of this.reflexes) {
        // Проверяем кулдаун
        if (now - reflex.lastTriggered < reflex.cooldown) continue;
        
        // Проверяем условие
        if (reflex.triggerCondition(signal, this.state)) {
          const action = reflex.execute(signal, this.state);
          reflex.lastTriggered = now;
          this.currentAction = action;
          this.actionStartTime = now;
          this.state.currentReflex = reflex.id;
          
          console.log(`[SpinalAI] Reflex triggered: ${reflex.name}`);
          return action;
        }
      }
    }
    
    // 3. Нет рефлексов - возвращаем null
    return null;
  }
  
  /**
   * Обновить внутреннее состояние
   */
  updateState(newState: Partial<SpinalState>): void {
    this.state = { ...this.state, ...newState };
  }
  
  /**
   * Принудительно прервать текущий рефлекс
   */
  interrupt(): void {
    if (this.currentAction?.canInterrupt) {
      this.currentAction = null;
    }
  }
  
  getState(): SpinalState {
    return { ...this.state };
  }
}
```

### Примеры рефлексов для мира культивации

```typescript
// Специфичные рефлексы для культиваторов

const CULTIVATOR_SPINAL_REFLEXES: SpinalReflex[] = [
  // Автоматическая защита при атаке Qi
  {
    id: 'qi_shield_reflex',
    name: 'Авто-щит Qi',
    priority: 95,
    cooldown: 3000,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      if (signal.type !== 'danger_nearby') return false;
      // Проверяем, является ли угроза Qi-атакой
      const threat = signal.source;
      return threat?.includes('qi_attack') && state.qi > 20;
    },
    
    execute: (signal, state) => ({
      type: 'qi_shield',
      params: {
        intensity: Math.min(state.qi * 0.3, 30),
      },
      duration: 500,
      canInterrupt: false,
    }),
  },
  
  // Реакция на подавление культивации
  {
    id: 'suppression_fear',
    name: 'Страх при подавлении',
    priority: 99,
    cooldown: 0,
    lastTriggered: 0,
    
    triggerCondition: (signal, state) => {
      // Если кто-то значительно выше уровнем рядом
      if (signal.type !== 'danger_nearby') return false;
      return signal.intensity > 0.9; // Сильное подавление
    },
    
    execute: (signal, state) => ({
      type: 'freeze',
      params: {
        reason: 'suppression',
      },
      duration: 1000,
      canInterrupt: false,
    }),
  },
];
```

---

## 🔗 Уровень 2: Neural Router (Нервная система)

### Назначение

Neural Router - это асинхронная система маршрутизации сигналов между Spinal AI и Brain (LLM). Обеспечивает буферизацию, приоритизацию и делегирование решений.

### Характеристики

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      NEURAL ROUTER (НЕРВНАЯ СИСТЕМА)                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Входные каналы:                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ 1. SPINAL SIGNALS → Рефлекторные сигналы (низкий приоритет)     │    │
│  │ 2. SENSORY INPUT → Сенсорные данные (средний приоритет)         │    │
│  │ 3. COGNITIVE REQUEST → Запросы к мозгу (высокий приоритет)      │    │
│  │ 4. BRAIN RESPONSE → Ответы от LLM (высокий приоритет)           │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Функции:                                                                │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ • Буферизация сигналов (очереди с приоритетами)                 │    │
│  │ • Маршрутизация (Spinal ↔ Brain)                                │    │
│  │ • Асинхронная передача (неблокирующий I/O)                      │    │
│  │ • Агрегация похожих сигналов (debounce/throttle)                │    │
│  │ • Фильтрация шума (пороговые значения)                          │    │
│  │ • Кэширование частых запросов                                   │    │
│  │ • Управление "вниманием" (attention)                            │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Скорость: 10-50мс                                                      │
│  Пропускная способность: 1000+ сигналов/сек                             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Реализация

```typescript
// src/lib/game/ai/neural-router.ts

/**
 * Neural Router - асинхронная нервная система NPC
 * Маршрутизатор между Spinal AI и Brain (LLM)
 */

// ==================== ТИПЫ ====================

type SignalPriority = 'low' | 'medium' | 'high' | 'critical';

interface NeuralSignal {
  id: string;
  source: 'spinal' | 'sensory' | 'cognitive' | 'brain' | 'external';
  type: string;
  priority: SignalPriority;
  payload: unknown;
  timestamp: number;
  requiresBrain: boolean;    // Требует ли обработки мозгом
  targetSystem?: 'spinal' | 'brain' | 'both';
}

interface NeuralQueue {
  signals: NeuralSignal[];
  maxSize: number;
  priority: SignalPriority;
}

interface RouterConfig {
  // Пороги для фильтрации
  signalThreshold: number;
  
  // Таймауты
  brainRequestTimeout: number;
  spinalResponseTimeout: number;
  
  // Размеры буферов
  maxQueueSize: number;
  
  // Частота обновления
  updateInterval: number;
}

// ==================== NEURAL ROUTER ====================

class NeuralRouter {
  private queues: Map<SignalPriority, NeuralQueue>;
  private spinalController: SpinalAIController;
  private brainController: BrainController | null = null;
  
  private config: RouterConfig = {
    signalThreshold: 0.1,
    brainRequestTimeout: 5000,
    spinalResponseTimeout: 100,
    maxQueueSize: 100,
    updateInterval: 16, // ~60fps
  };
  
  // Кэш частых запросов
  private responseCache: Map<string, CachedResponse> = new Map();
  
  // Активные запросы к мозгу
  private pendingBrainRequests: Map<string, PendingRequest> = new Map();
  
  // Система внимания
  private attentionFocus: AttentionFocus | null = null;
  
  constructor(spinalController: SpinalAIController) {
    this.spinalController = spinalController;
    
    // Инициализация очередей
    this.queues = new Map([
      ['low', { signals: [], maxSize: 50, priority: 'low' }],
      ['medium', { signals: [], maxSize: 30, priority: 'medium' }],
      ['high', { signals: [], maxSize: 20, priority: 'high' }],
      ['critical', { signals: [], maxSize: 10, priority: 'critical' }],
    ]);
  }
  
  /**
   * Установить контроллер мозга
   */
  setBrainController(brain: BrainController): void {
    this.brainController = brain;
  }
  
  /**
   * Отправить сигнал в систему
   */
  sendSignal(signal: NeuralSignal): void {
    const queue = this.queues.get(signal.priority);
    if (!queue) return;
    
    // Проверяем размер очереди
    if (queue.signals.length >= queue.maxSize) {
      // Удаляем старые сигналы
      queue.signals.shift();
    }
    
    queue.signals.push(signal);
    
    // Критические сигналы обрабатываем немедленно
    if (signal.priority === 'critical') {
      this.processSignal(signal);
    }
  }
  
  /**
   * Главный цикл обновления
   */
  async update(deltaMs: number): Promise<RouterResult> {
    const result: RouterResult = {
      spinalAction: null,
      brainRequest: null,
      brainResponse: null,
    };
    
    // 1. Обрабатываем сигналы по приоритетам (сверху вниз)
    for (const priority of ['critical', 'high', 'medium', 'low'] as SignalPriority[]) {
      const queue = this.queues.get(priority)!;
      
      while (queue.signals.length > 0) {
        const signal = queue.signals.shift()!;
        const processed = await this.processSignal(signal);
        
        if (processed.spinalAction) {
          result.spinalAction = processed.spinalAction;
        }
        if (processed.brainRequest) {
          result.brainRequest = processed.brainRequest;
        }
        if (processed.brainResponse) {
          result.brainResponse = processed.brainResponse;
        }
      }
    }
    
    // 2. Проверяем pending запросы к мозгу
    await this.checkPendingRequests();
    
    // 3. Обновляем фокус внимания
    this.updateAttention();
    
    return result;
  }
  
  /**
   * Обработать один сигнал
   */
  private async processSignal(signal: NeuralSignal): Promise<Partial<RouterResult>> {
    const result: Partial<RouterResult> = {};
    
    // Проверяем кэш
    const cacheKey = this.getCacheKey(signal);
    const cached = this.responseCache.get(cacheKey);
    if (cached && Date.now() - cached.timestamp < 5000) {
      return { brainResponse: cached.response };
    }
    
    // Маршрутизация
    if (signal.targetSystem === 'spinal' || !signal.requiresBrain) {
      // Отправляем в Spinal AI
      this.spinalController.receiveSignal(signal.payload as SpinalSignal);
      result.spinalAction = this.spinalController.update(16);
    }
    
    if (signal.targetSystem === 'brain' || signal.requiresBrain) {
      // Отправляем в Brain (LLM)
      if (this.brainController) {
        const requestId = `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        
        this.pendingBrainRequests.set(requestId, {
          signal,
          startTime: Date.now(),
          status: 'pending',
        });
        
        // Асинхронный запрос к мозгу
        this.brainController.requestDecision(signal)
          .then(response => {
            this.pendingBrainRequests.set(requestId, {
              ...this.pendingBrainRequests.get(requestId)!,
              status: 'completed',
              response,
            });
            
            // Кэшируем ответ
            this.responseCache.set(cacheKey, {
              response,
              timestamp: Date.now(),
            });
          })
          .catch(error => {
            console.error('[NeuralRouter] Brain request failed:', error);
            this.pendingBrainRequests.delete(requestId);
          });
        
        result.brainRequest = { requestId, signal };
      }
    }
    
    if (signal.targetSystem === 'both') {
      // Сначала спинальная реакция, потом запрос к мозгу
      this.spinalController.receiveSignal(signal.payload as SpinalSignal);
      result.spinalAction = this.spinalController.update(16);
      
      if (this.brainController) {
        // ... запрос к мозгу
      }
    }
    
    return result;
  }
  
  /**
   * Проверить pending запросы
   */
  private async checkPendingRequests(): Promise<void> {
    const now = Date.now();
    
    for (const [requestId, request] of this.pendingBrainRequests) {
      // Проверяем таймаут
      if (now - request.startTime > this.config.brainRequestTimeout) {
        console.warn(`[NeuralRouter] Brain request timeout: ${requestId}`);
        this.pendingBrainRequests.delete(requestId);
        continue;
      }
      
      // Если запрос завершён - обрабатываем
      if (request.status === 'completed' && request.response) {
        // Применяем ответ от мозга
        this.applyBrainResponse(request.response);
        this.pendingBrainRequests.delete(requestId);
      }
    }
  }
  
  /**
   * Применить ответ от мозга
   */
  private applyBrainResponse(response: BrainResponse): void {
    // Обновляем состояние
    if (response.stateUpdate) {
      this.spinalController.updateState(response.stateUpdate);
    }
  }
  
  /**
   * Обновить фокус внимания
   */
  private updateAttention(): void {
    // Определяем, на чём сфокусирован NPC
    // На основе последних сигналов и текущих действий
  }
  
  /**
   * Сгенерировать ключ кэша
   */
  private getCacheKey(signal: NeuralSignal): string {
    return `${signal.source}:${signal.type}:${JSON.stringify(signal.payload)}`;
  }
}

// ==================== ТИПЫ ДАННЫХ ====================

interface RouterResult {
  spinalAction: SpinalAction | null;
  brainRequest: { requestId: string; signal: NeuralSignal } | null;
  brainResponse: BrainResponse | null;
}

interface PendingRequest {
  signal: NeuralSignal;
  startTime: number;
  status: 'pending' | 'completed' | 'failed';
  response?: BrainResponse;
}

interface CachedResponse {
  response: BrainResponse;
  timestamp: number;
}

interface AttentionFocus {
  targetId: string;
  targetType: 'player' | 'npc' | 'object' | 'location';
  intensity: number;
  startTime: number;
}

interface BrainResponse {
  action: string;
  params: Record<string, unknown>;
  reasoning?: string;
  stateUpdate?: Partial<SpinalState>;
}
```

---

## 🧠 Уровень 3: Brain Controller (Мозг/LLM)

### Назначение

Brain Controller - это когнитивный уровень AI, использующий LLM для сложных решений, диалогов и планирования. Работает асинхронно с задержкой 100-500мс.

### Характеристики

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      BRAIN CONTROLLER (МОЗГ/LLM)                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Входные запросы:                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ • Сложные решения (как ответить на провокацию?)                 │    │
│  │ • Диалоги (генерация реплик)                                    │    │
│  │ • Планирование (долгосрочные цели)                              │    │
│  │ • Анализ ситуации (оценка рисков)                               │    │
│  │ • Обучение (усвоение нового опыта)                              │    │
│  │ • Социальные взаимодействия                                     │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Возможности:                                                            │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ • Генерация диалогов с учётом контекста                         │    │
│  │ • Принятие нестандартных решений                                │    │
│  │ • Планирование многоходовых действий                            │    │
│  │ • Анализ намерений игрока                                       │    │
│  │ • Адаптация к стилю игры                                        │    │
│  │ • Эмоциональные реакции                                         │    │
│  │ • Творческие решения                                            │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Скорость: 100-500мс (асинхронно)                                       │
│  Энергия: Высокая (GPU/API запросы)                                     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Реализация

```typescript
// src/lib/game/ai/brain-controller.ts

/**
 * Brain Controller - когнитивный уровень AI на базе LLM
 * Использует z-ai-web-dev-sdk для генерации текста
 */

import { generateText } from '@/lib/llm';

// ==================== ТИПЫ ====================

interface BrainContext {
  // Идентичность NPC
  npc: {
    id: string;
    name: string;
    type: 'static' | 'temp';
    role: string;           // merchant, sect_leader, guard, etc.
    personality: string;    // Описание личности
    background: string;     // Предыстория
    
    // Принадлежность
    sectId?: string;
    factionId?: string;
    nationId?: string;
    
    // Состояние
    currentGoal: string;
    currentMood: string;
    knownFacts: string[];   // Факты, которые NPC знает
  };
  
  // Контекст ситуации
  situation: {
    location: string;
    timeOfDay: string;
    nearbyEntities: EntityInfo[];
    currentConversation?: ConversationInfo;
    recentEvents: EventInfo[];
  };
  
  // Игрок
  player: {
    name: string;
    reputation: number;
    sectId?: string;
    previousInteractions: string[];
    currentAction: string;
  };
  
  // Глобальный контекст
  world: {
    date: string;
    majorEvents: string[];
    factionRelations: Record<string, number>;
  };
}

interface BrainRequest {
  type: BrainRequestType;
  priority: 'low' | 'medium' | 'high';
  context: BrainContext;
  specificQuestion?: string;
  options?: string[];       // Варианты выбора (если применимо)
}

type BrainRequestType =
  | 'dialogue_generate'    // Генерация реплики
  | 'decision_make'        // Принятие решения
  | 'plan_create'          // Создание плана
  | 'situation_analyze'    // Анализ ситуации
  | 'emotion_determine'    // Определение эмоции
  | 'goal_evaluate';       // Оценка целей

interface BrainResponse {
  action: string;
  params: Record<string, unknown>;
  reasoning?: string;
  emotion?: string;
  dialogue?: DialogueResponse;
  plan?: PlanStep[];
  stateUpdate?: Partial<SpinalState>;
}

interface DialogueResponse {
  text: string;
  tone: string;
  emotion: string;
  followUpOptions?: string[];
}

interface PlanStep {
  action: string;
  params: Record<string, unknown>;
  duration: number;
  condition: string;
}

// ==================== BRAIN CONTROLLER ====================

class BrainController {
  private context: BrainContext;
  private conversationHistory: ConversationMessage[] = [];
  private maxHistoryLength: number = 20;
  
  // Системные промпты для разных ролей
  private rolePrompts: Record<string, string> = {
    merchant: `Ты торговец в мире культивации. Ты прагматичен, но честен.
              Твои приоритеты: прибыль, репутация, безопасность.
              Говори кратко и по делу. Используй торговый жаргон.`,
    
    sect_leader: `Ты глава секты культивации. Ты мудр, авторитетен, но справедлив.
                 Твои приоритеты: процветание секты, обучение учеников, сохранение традиций.
                 Говори важно и обдуманно. Используй философские обороты.`,
    
    guard: `Ты охранник секты. Ты дисциплинирован, бдителен, лоялен.
           Твои приоритеты: безопасность, порядок, выполнение приказов.
           Говори коротко и чётко. Используй военные термины.`,
    
    cultivator: `Ты практикующий культиватор. Ты стремишься к просветлению.
                Твои приоритеты: культивация, гармония, самосовершенствование.
                Говори спокойно и вдумчиво. Используй термины культивации.`,
    
    passerby: `Ты обычный человек в мире культивации. Ты осторожен и наблюдаешь.
              Твои приоритеты: выживание, семья, безопасность.
              Говори просто и эмоционально.`,
  };
  
  constructor(initialContext: Partial<BrainContext>) {
    this.context = {
      npc: {
        id: 'unknown',
        name: 'Unknown NPC',
        type: 'temp',
        role: 'passerby',
        personality: 'нейтральный',
        background: '',
        currentGoal: '',
        currentMood: 'neutral',
        knownFacts: [],
      },
      situation: {
        location: 'unknown',
        timeOfDay: 'day',
        nearbyEntities: [],
        recentEvents: [],
      },
      player: {
        name: 'Игрок',
        reputation: 0,
        previousInteractions: [],
        currentAction: '',
      },
      world: {
        date: 'День 1',
        majorEvents: [],
        factionRelations: {},
      },
      ...initialContext,
    };
  }
  
  /**
   * Запрос решения от мозга
   */
  async requestDecision(signal: NeuralSignal): Promise<BrainResponse> {
    const request = signal.payload as BrainRequest;
    
    switch (request.type) {
      case 'dialogue_generate':
        return this.generateDialogue(request);
      case 'decision_make':
        return this.makeDecision(request);
      case 'plan_create':
        return this.createPlan(request);
      case 'situation_analyze':
        return this.analyzeSituation(request);
      case 'emotion_determine':
        return this.determineEmotion(request);
      default:
        return this.defaultResponse(request);
    }
  }
  
  /**
   * Генерация диалоговой реплики
   */
  private async generateDialogue(request: BrainRequest): Promise<BrainResponse> {
    const systemPrompt = this.buildSystemPrompt();
    const contextPrompt = this.buildContextPrompt(request.context);
    
    const prompt = `${contextPrompt}

Игрок: ${request.specificQuestion || request.player.currentAction}

Что ты ответишь? Ответь кратко (1-3 предложения), учитывая:
1. Твою личность и роль
2. Отношение к игроку (disposition)
3. Текущую ситуацию
4. Твои текущие цели`;

    const response = await generateText({
      prompt,
      systemPrompt,
      maxTokens: 150,
      temperature: 0.7,
    });
    
    return {
      action: 'say',
      params: { text: response.text },
      dialogue: {
        text: response.text,
        tone: this.detectTone(response.text),
        emotion: this.detectEmotion(response.text),
      },
    };
  }
  
  /**
   * Принятие решения
   */
  private async makeDecision(request: BrainRequest): Promise<BrainResponse> {
    const systemPrompt = this.buildSystemPrompt();
    const contextPrompt = this.buildContextPrompt(request.context);
    
    const optionsText = request.options 
      ? `\n\nВарианты:\n${request.options.map((o, i) => `${i + 1}. ${o}`).join('\n')}`
      : '';
    
    const prompt = `${contextPrompt}

Ситуация требует решения: ${request.specificQuestion}
${optionsText}

Проанализируй и выбери оптимальное действие.
Ответь в формате JSON:
{
  "decision": "выбранный вариант или описание действия",
  "reasoning": "краткое обоснование",
  "emotion": "текущая эмоция"
}`;

    const response = await generateText({
      prompt,
      systemPrompt,
      maxTokens: 200,
      temperature: 0.5,
    });
    
    try {
      const parsed = JSON.parse(response.text);
      return {
        action: 'decide',
        params: { decision: parsed.decision },
        reasoning: parsed.reasoning,
        emotion: parsed.emotion,
      };
    } catch {
      return {
        action: 'decide',
        params: { decision: response.text },
      };
    }
  }
  
  /**
   * Создание плана действий
   */
  private async createPlan(request: BrainRequest): Promise<BrainResponse> {
    const systemPrompt = this.buildSystemPrompt();
    const contextPrompt = this.buildContextPrompt(request.context);
    
    const prompt = `${contextPrompt}

Текущая цель: ${request.context.npc.currentGoal}
Текущая ситуация: ${request.specificQuestion}

Создай план из 3-5 шагов для достижения цели.
Ответь в формате JSON массива:
[
  {
    "action": "название действия",
    "params": {"key": "value"},
    "duration": 10,
    "condition": "условие выполнения"
  }
]`;

    const response = await generateText({
      prompt,
      systemPrompt,
      maxTokens: 300,
      temperature: 0.6,
    });
    
    try {
      const plan = JSON.parse(response.text);
      return {
        action: 'plan',
        params: {},
        plan,
      };
    } catch {
      return {
        action: 'plan',
        params: {},
        plan: [{
          action: 'wait',
          params: {},
          duration: 60,
          condition: 'default',
        }],
      };
    }
  }
  
  /**
   * Анализ ситуации
   */
  private async analyzeSituation(request: BrainRequest): Promise<BrainResponse> {
    const systemPrompt = this.buildSystemPrompt();
    const contextPrompt = this.buildContextPrompt(request.context);
    
    const prompt = `${contextPrompt}

Проанализируй текущую ситуацию и оцени:
1. Уровень опасности (0-100)
2. Возможности для достижения целей
3. Рекомендуемое поведение

Ответь кратко в JSON формате.`;

    const response = await generateText({
      prompt,
      systemPrompt,
      maxTokens: 150,
      temperature: 0.4,
    });
    
    try {
      const analysis = JSON.parse(response.text);
      return {
        action: 'analyze',
        params: analysis,
      };
    } catch {
      return {
        action: 'analyze',
        params: { dangerLevel: 50 },
      };
    }
  }
  
  /**
   * Определение эмоции
   */
  private async determineEmotion(request: BrainRequest): Promise<BrainResponse> {
    const emotions = ['neutral', 'happy', 'angry', 'fearful', 'surprised', 'disgusted', 'sad', 'curious'];
    
    // Простая эвристика без LLM для быстрых реакций
    const disposition = this.calculateDisposition(request.context);
    
    let emotion = 'neutral';
    if (disposition >= 50) emotion = 'happy';
    else if (disposition >= 20) emotion = 'curious';
    else if (disposition >= -20) emotion = 'neutral';
    else if (disposition >= -50) emotion = 'suspicious';
    else emotion = 'angry';
    
    return {
      action: 'emotion',
      params: { emotion },
      emotion,
    };
  }
  
  /**
   * Дефолтный ответ
   */
  private defaultResponse(request: BrainRequest): BrainResponse {
    return {
      action: 'wait',
      params: {},
    };
  }
  
  // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================
  
  private buildSystemPrompt(): string {
    const rolePrompt = this.rolePrompts[this.context.npc.role] || this.rolePrompts.passerby;
    
    return `${rolePrompt}

Твои характеристики:
- Имя: ${this.context.npc.name}
- Личность: ${this.context.npc.personality}
- Предыстория: ${this.context.npc.background}
- Текущая цель: ${this.context.npc.currentGoal}

ВАЖНО: Ты NPC в игре. Отвечай кратко и по делу. Не выходи из роли.`;
  }
  
  private buildContextPrompt(context: BrainContext): string {
    const playerInfo = `Игрок: ${context.player.name} (репутация: ${context.player.reputation})`;
    const locationInfo = `Локация: ${context.situation.location}, ${context.situation.timeOfDay}`;
    const moodInfo = `Твоё настроение: ${context.npc.currentMood}`;
    
    let additionalInfo = '';
    
    if (context.player.sectId) {
      additionalInfo += `\nИгрок принадлежит к секте: ${context.player.sectId}`;
    }
    
    if (context.situation.nearbyEntities.length > 0) {
      additionalInfo += `\nРядом: ${context.situation.nearbyEntities.map(e => e.name).join(', ')}`;
    }
    
    return `${playerInfo}
${locationInfo}
${moodInfo}
${additionalInfo}`;
  }
  
  private detectTone(text: string): string {
    if (text.includes('!')) return 'emphatic';
    if (text.includes('?')) return 'questioning';
    return 'neutral';
  }
  
  private detectEmotion(text: string): string {
    const emotionKeywords: Record<string, string[]> = {
      happy: ['рад', 'счастлив', 'прекрасно', 'отлично', 'хорошо'],
      angry: ['зол', 'раздражён', 'невыносимо', 'хватит'],
      sad: ['грустно', 'жаль', 'печально', 'к сожалению'],
      fearful: ['боюсь', 'опасаюсь', 'страшно', 'опасность'],
      surprised: ['удивительно', 'неожиданно', 'невероятно'],
    };
    
    for (const [emotion, keywords] of Object.entries(emotionKeywords)) {
      if (keywords.some(kw => text.toLowerCase().includes(kw))) {
        return emotion;
      }
    }
    
    return 'neutral';
  }
  
  private calculateDisposition(context: BrainContext): number {
    // Базовый расчёт на основе репутации
    let disposition = context.player.reputation;
    
    // Модификаторы фракций
    // TODO: Интегрировать с системой фракций
    
    return Math.max(-100, Math.min(100, disposition));
  }
  
  /**
   * Обновить контекст
   */
  updateContext(newContext: Partial<BrainContext>): void {
    this.context = {
      ...this.context,
      ...newContext,
      npc: { ...this.context.npc, ...newContext.npc },
      situation: { ...this.context.situation, ...newContext.situation },
      player: { ...this.context.player, ...newContext.player },
      world: { ...this.context.world, ...newContext.world },
    };
  }
  
  /**
   * Добавить факт в память
   */
  addFact(fact: string): void {
    this.context.npc.knownFacts.push(fact);
    // Ограничиваем размер памяти
    if (this.context.npc.knownFacts.length > 50) {
      this.context.npc.knownFacts.shift();
    }
  }
}
```

---

## 🔄 Интеграция трёхуровневой системы

### Общая архитектура

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ИНТЕГРИРОВАННАЯ AI СИСТЕМА                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ИГРОВОЙ ЦИКЛ                                                          │
│       │                                                                  │
│       ▼                                                                  │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                      NEURAL ROUTER                               │   │
│   │                                                                  │   │
│   │  update(deltaMs) → RouterResult                                 │   │
│   │                                                                  │   │
│   │  • Маршрутизация сигналов                                       │   │
│   │  • Управление очередями                                         │   │
│   │  • Координация Spinal ↔ Brain                                   │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│       │                       │                       │                  │
│       ▼                       ▼                       ▼                  │
│   ┌──────────┐         ┌──────────┐         ┌──────────┐                │
│   │ SENSORS  │         │ SPINAL   │         │  BRAIN   │                │
│   │          │         │   AI     │         │ (LLM)    │                │
│   │ Вход:    │────────►│          │◄───────►│          │                │
│   │ • Зрение │         │ Рефлексы │         │ Диалоги  │                │
│   │ • Слух   │         │ 1-10мс   │         │ Планы    │                │
│   │ • Урон   │         │ O(1)     │         │ 100-500мс│                │
│   │ • Qi     │         │          │         │          │                │
│   └──────────┘         └──────────┘         └──────────┘                │
│                                                                          │
│   РЕЗУЛЬТАТ:                                                            │
│   • SpinalAction (немедленное)                                          │
│   • BrainResponse (отложенное)                                          │
│   • State Update                                                        │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Главный контроллер

```typescript
// src/lib/game/ai/neuro-ai-controller.ts

/**
 * Главный контроллер нейротеории AI
 * Объединяет все три уровня
 */

class NeuroAIController {
  private router: NeuralRouter;
  private spinal: SpinalAIController;
  private brain: BrainController | null;
  
  private npcId: string;
  private npcType: 'static' | 'temp';
  
  // Конфигурация
  private config: {
    useBrain: boolean;         // Использовать ли LLM
    brainMinInterval: number;  // Минимальный интервал запросов к мозгу
    lastBrainRequest: number;
  } = {
    useBrain: true,
    brainMinInterval: 1000, // Не чаще раза в секунду
    lastBrainRequest: 0,
  };
  
  constructor(npcId: string, npcType: 'static' | 'temp', config?: Partial<BrainContext>) {
    this.npcId = npcId;
    this.npcType = npcType;
    
    // Инициализация Spinal AI
    this.spinal = new SpinalAIController();
    
    // Инициализация Router
    this.router = new NeuralRouter(this.spinal);
    
    // Инициализация Brain (только для статичных NPC)
    if (npcType === 'static' && this.config.useBrain) {
      this.brain = new BrainController(config);
      this.router.setBrainController(this.brain);
    } else {
      this.brain = null;
    }
  }
  
  /**
   * Получить сенсорный ввод
   */
  receiveSensoryInput(input: SensoryInput): void {
    const signal: NeuralSignal = {
      id: `sense_${Date.now()}`,
      source: 'sensory',
      type: input.type,
      priority: this.determinePriority(input),
      payload: input,
      timestamp: Date.now(),
      requiresBrain: input.requiresBrain || false,
      targetSystem: input.targetSystem || 'spinal',
    };
    
    this.router.sendSignal(signal);
  }
  
  /**
   * Обновление каждый кадр
   */
  async update(deltaMs: number): Promise<AIAction | null> {
    const result = await this.router.update(deltaMs);
    
    // Приоритет: Spinal > Brain
    if (result.spinalAction) {
      return this.convertSpinalAction(result.spinalAction);
    }
    
    if (result.brainResponse) {
      return this.convertBrainResponse(result.brainResponse);
    }
    
    return null;
  }
  
  /**
   * Определить приоритет сигнала
   */
  private determinePriority(input: SensoryInput): SignalPriority {
    // Урон и опасность - критический приоритет
    if (input.type === 'damage' || input.type === 'danger') {
      return 'critical';
    }
    
    // Диалог и социальные - высокий приоритет
    if (input.type === 'dialogue' || input.type === 'social') {
      return 'high';
    }
    
    // Движение и локация - средний
    if (input.type === 'movement' || input.type === 'location') {
      return 'medium';
    }
    
    return 'low';
  }
  
  /**
   * Конвертировать спинальное действие в игровое
   */
  private convertSpinalAction(action: SpinalAction): AIAction {
    return {
      type: action.type,
      params: action.params,
      priority: 100, // Рефлексы всегда высокий приоритет
      source: 'spinal',
    };
  }
  
  /**
   * Конвертировать ответ мозга в игровое действие
   */
  private convertBrainResponse(response: BrainResponse): AIAction {
    return {
      type: response.action,
      params: response.params,
      priority: 50,
      source: 'brain',
      reasoning: response.reasoning,
      emotion: response.emotion,
    };
  }
  
  /**
   * Принудительный запрос к мозгу
   */
  async requestBrainDecision(question: string): Promise<BrainResponse | null> {
    if (!this.brain) return null;
    
    const now = Date.now();
    if (now - this.config.lastBrainRequest < this.config.brainMinInterval) {
      return null; // Слишком часто
    }
    
    this.config.lastBrainRequest = now;
    
    return this.brain.requestDecision({
      type: 'decision_make',
      priority: 'high',
      context: this.brain['context'],
      specificQuestion: question,
    });
  }
}

// ==================== ТИПЫ ====================

interface SensoryInput {
  type: string;
  data: unknown;
  intensity?: number;
  direction?: Vector2D;
  source?: string;
  requiresBrain?: boolean;
  targetSystem?: 'spinal' | 'brain' | 'both';
}

interface AIAction {
  type: string;
  params: Record<string, unknown>;
  priority: number;
  source: 'spinal' | 'brain' | 'state_machine' | 'goap';
  reasoning?: string;
  emotion?: string;
}
```

---

## 🧠 Spinal Neural Network (SNN) - Нейросетевая архитектура

### Концепция

Spinal Neural Network (SNN) - это быстрая, лёгкая нейросеть для рефлекторных реакций NPC. В отличие от основанной на правилах системы, SNN способна дообучаться в процессе игры.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SPINAL NEURAL NETWORK ARCHITECTURE                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                        ВХОДНОЙ СЛОЙ                              │   │
│   │                                                                  │   │
│   │  Сенсорные входы (нормализованные 0.0-1.0):                     │   │
│   │  • dangerLevel     - уровень опасности                          │   │
│   │  • damageIntensity - интенсивность урона                        │   │
│   │  • threatDirection - направление угрозы (x, y)                  │   │
│   │  • distanceToEdge  - расстояние до края                         │   │
│   │  • soundIntensity  - громкость звука                            │   │
│   │  • soundDirection  - направление звука (x, y)                   │   │
│   │  • hpPercent       - процент HP                                 │   │
│   │  • qiPercent       - процент Qi                                 │   │
│   │  • fatigue         - усталость                                  │   │
│   │  • isMoving        - движется ли                                │   │
│   │                                                                  │   │
│   │  Размер: 12 нейронов                                            │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                    СКРЫТЫЕ СЛОИ (CORE)                           │   │
│   │                                                                  │   │
│   │  Предобученные веса - не изменяются                             │   │
│   │  • Hidden Layer 1: 24 нейрона (ReLU)                            │   │
│   │  • Hidden Layer 2: 16 нейронов (ReLU)                           │   │
│   │                                                                  │   │
│   │  Базовые рефлексы: уклонение, боль, равновесие                  │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                    LoRA АДАПТАЦИЯ                                │   │
│   │                                                                  │   │
│   │  Малые обучаемые слои - адаптация под NPC                       │   │
│   │  • LoRA A: 12 × 4 = 48 параметров                               │   │
│   │  • LoRA B: 4 × 16 = 64 параметра                                │   │
│   │  • Итого: ~112 параметров на адаптацию                          │   │
│   │                                                                  │   │
│   │  Персонализация: страх, агрессия, осторожность                  │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              ▼                                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                       ВЫХОДНОЙ СЛОЙ                              │   │
│   │                                                                  │   │
│   │  Действия (softmax):                                            │   │
│   │  • dodge         - уклонение                                    │   │
│   │  • flinch        - вздрогнуть                                   │   │
│   │  • freeze        - замереть                                     │   │
│   │  • retreat       - отступить                                    │   │
│   │  • turn_to_sound - повернуться на звук                          │   │
│   │  • step_back     - шаг назад                                    │   │
│   │  • balance       - восстановить равновесие                      │   │
│   │  • none          - нет действия                                 │   │
│   │                                                                  │   │
│   │  Размер: 8 нейронов                                             │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  Общее количество параметров:                                           │
│  • Core (заморожен): ~600 параметров                                    │
│  • LoRA (обучаем): ~112 параметров                                      │
│  • Итого: ~712 параметров (очень лёгкая!)                               │
│                                                                          │
│  Скорость: < 1мс на CPU                                                 │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Архитектура LoRA (Low-Rank Adaptation)

LoRA позволяет дообучать модель без изменения базовых весов:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         LoRA PRINCIPLE                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Обычный слой:                                                           │
│  Y = W × X                                                               │
│  где W - матрица весов (заморожена)                                      │
│                                                                          │
│  LoRA адаптация:                                                         │
│  Y = W × X + (B × A) × X                                                 │
│      ↑         ↑                                                         │
│    Core    LoRA адаптация                                               │
│   (заморожен)  (обучаем)                                                │
│                                                                          │
│  Размерности:                                                            │
│  • W: [output_dim × input_dim] - например, [16 × 12] = 192             │
│  • A: [rank × input_dim] - например, [4 × 12] = 48 (обучаем)           │
│  • B: [output_dim × rank] - например, [16 × 4] = 64 (обучаем)          │
│                                                                          │
│  Экономия: 192 замороженных vs 112 обучаемых параметров                 │
│  Сокращение обучаемых параметров: ~94%                                  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Реализация

```typescript
// src/lib/game/ai/spinal-neural-network.ts

/**
 * Spinal Neural Network - быстрая нейросеть для рефлексов
 * с возможностью дообучения через LoRA
 */

// ==================== ТИПЫ ====================

interface SpinalInput {
  // Опасность
  dangerLevel: number;      // 0.0 - 1.0
  damageIntensity: number;  // 0.0 - 1.0
  threatDirectionX: number; // -1.0 - 1.0
  threatDirectionY: number; // -1.0 - 1.0
  
  // Окружение
  distanceToEdge: number;   // 0.0 - 1.0 (нормализованное)
  soundIntensity: number;   // 0.0 - 1.0
  soundDirectionX: number;  // -1.0 - 1.0
  soundDirectionY: number;  // -1.0 - 1.0
  
  // Состояние тела
  hpPercent: number;        // 0.0 - 1.0
  qiPercent: number;        // 0.0 - 1.0
  fatigue: number;          // 0.0 - 1.0
  isMoving: number;         // 0 или 1
}

type SpinalOutputAction =
  | 'dodge'
  | 'flinch'
  | 'freeze'
  | 'retreat'
  | 'turn_to_sound'
  | 'step_back'
  | 'balance'
  | 'none';

interface SpinalOutput {
  action: SpinalOutputAction;
  confidence: number;       // 0.0 - 1.0
  params: {
    direction?: { x: number; y: number };
    intensity?: number;
    speed?: number;
  };
}

interface SpinalActionParams {
  direction?: { x: number; y: number };
  intensity?: number;
  speed?: number;
}

// ==================== МАТРИЧНЫЕ ОПЕРАЦИИ ====================

class Matrix {
  static multiply(A: number[][], B: number[][]): number[][] {
    const rowsA = A.length;
    const colsA = A[0].length;
    const colsB = B[0].length;
    const result: number[][] = [];
    
    for (let i = 0; i < rowsA; i++) {
      result[i] = [];
      for (let j = 0; j < colsB; j++) {
        let sum = 0;
        for (let k = 0; k < colsA; k++) {
          sum += A[i][k] * B[k][j];
        }
        result[i][j] = sum;
      }
    }
    
    return result;
  }
  
  static add(A: number[][], B: number[][]): number[][] {
    return A.map((row, i) => row.map((val, j) => val + B[i][j]));
  }
  
  static vectorMultiply(M: number[][], v: number[]): number[] {
    return M.map(row => row.reduce((sum, val, i) => sum + val * v[i], 0));
  }
  
  static relu(x: number): number {
    return Math.max(0, x);
  }
  
  static softmax(arr: number[]): number[] {
    const max = Math.max(...arr);
    const exps = arr.map(x => Math.exp(x - max));
    const sum = exps.reduce((a, b) => a + b, 0);
    return exps.map(x => x / sum);
  }
}

// ==================== LoRA СЛОЙ ====================

class LoRALayer {
  // Матрицы LoRA: output = B @ A @ input
  private A: number[][];  // [rank × input_dim]
  private B: number[][];  // [output_dim × rank]
  private rank: number;
  private learningRate: number;
  
  constructor(inputDim: number, outputDim: number, rank: number = 4) {
    this.rank = rank;
    this.learningRate = 0.01;
    
    // Инициализация A случайными малыми значениями
    this.A = Array(rank).fill(null).map(() =>
      Array(inputDim).fill(null).map(() => (Math.random() - 0.5) * 0.1)
    );
    
    // Инициализация B нулями (в начале адаптация не влияет)
    this.B = Array(outputDim).fill(null).map(() =>
      Array(rank).fill(0)
    );
  }
  
  /**
   * Прямой проход через LoRA
   */
  forward(input: number[]): number[] {
    // intermediate = A @ input  [rank]
    const intermediate: number[] = [];
    for (let i = 0; i < this.rank; i++) {
      let sum = 0;
      for (let j = 0; j < input.length; j++) {
        sum += this.A[i][j] * input[j];
      }
      intermediate.push(sum);
    }
    
    // output = B @ intermediate  [output_dim]
    const output: number[] = [];
    for (let i = 0; i < this.B.length; i++) {
      let sum = 0;
      for (let j = 0; j < this.rank; j++) {
        sum += this.B[i][j] * intermediate[j];
      }
      output.push(sum);
    }
    
    return output;
  }
  
  /**
   * Обратное распространение ошибки
   */
  backward(input: number[], outputGradient: number[]): void {
    // Градиент для B
    const intermediate = this.forwardIntermediate(input);
    
    // Обновляем B
    for (let i = 0; i < this.B.length; i++) {
      for (let j = 0; j < this.rank; j++) {
        this.B[i][j] -= this.learningRate * outputGradient[i] * intermediate[j];
      }
    }
    
    // Градиент для A (упрощённый)
    const aGradient = this.computeAGradient(outputGradient, intermediate);
    
    // Обновляем A
    for (let i = 0; i < this.rank; i++) {
      for (let j = 0; j < input.length; j++) {
        this.A[i][j] -= this.learningRate * aGradient[i] * input[j];
      }
    }
  }
  
  private forwardIntermediate(input: number[]): number[] {
    const intermediate: number[] = [];
    for (let i = 0; i < this.rank; i++) {
      let sum = 0;
      for (let j = 0; j < input.length; j++) {
        sum += this.A[i][j] * input[j];
      }
      intermediate.push(sum);
    }
    return intermediate;
  }
  
  private computeAGradient(outputGradient: number[], intermediate: number[]): number[] {
    const gradient: number[] = [];
    for (let i = 0; i < this.rank; i++) {
      let sum = 0;
      for (let j = 0; j < outputGradient.length; j++) {
        sum += outputGradient[j] * this.B[j][i];
      }
      gradient.push(sum);
    }
    return gradient;
  }
  
  /**
   * Экспорт весов LoRA
   */
  exportWeights(): { A: number[][]; B: number[][] } {
    return {
      A: this.A.map(row => [...row]),
      B: this.B.map(row => [...row]),
    };
  }
  
  /**
   * Импорт весов LoRA
   */
  importWeights(weights: { A: number[][]; B: number[][] }): void {
    this.A = weights.A.map(row => [...row]);
    this.B = weights.B.map(row => [...row]);
  }
  
  /**
   * Сброс LoRA к начальным значениям
   */
  reset(): void {
    const inputDim = this.A[0].length;
    const outputDim = this.B.length;
    
    this.A = Array(this.rank).fill(null).map(() =>
      Array(inputDim).fill(null).map(() => (Math.random() - 0.5) * 0.1)
    );
    
    this.B = Array(outputDim).fill(null).map(() =>
      Array(this.rank).fill(0)
    );
  }
}

// ==================== SPINAL NEURAL NETWORK ====================

class SpinalNeuralNetwork {
  // Размерности
  private readonly INPUT_DIM = 12;
  private readonly HIDDEN1_DIM = 24;
  private readonly HIDDEN2_DIM = 16;
  private readonly OUTPUT_DIM = 8;
  private readonly LORA_RANK = 4;
  
  // Core веса (замороженные, предобученные)
  private W1: number[][];  // [24 × 12]
  private b1: number[];    // [24]
  private W2: number[][];  // [16 × 24]
  private b2: number[];    // [16]
  private W3: number[][];  // [8 × 16]
  private b3: number[];    // [8]
  
  // LoRA адаптации
  private lora1: LoRALayer;  // Для скрытого слоя 1
  private lora2: LoRALayer;  // Для скрытого слоя 2
  
  // Конфигурация обучения
  private learningRate: number = 0.001;
  private trainingEnabled: boolean = true;
  private maxTrainingSteps: number = 1000;
  private currentStep: number = 0;
  
  // Действия
  private readonly ACTIONS: SpinalOutputAction[] = [
    'dodge', 'flinch', 'freeze', 'retreat',
    'turn_to_sound', 'step_back', 'balance', 'none'
  ];
  
  constructor() {
    // Инициализация весов Core
    this.W1 = this.initWeights(this.HIDDEN1_DIM, this.INPUT_DIM, 'xavier');
    this.b1 = Array(this.HIDDEN1_DIM).fill(0);
    this.W2 = this.initWeights(this.HIDDEN2_DIM, this.HIDDEN1_DIM, 'xavier');
    this.b2 = Array(this.HIDDEN2_DIM).fill(0);
    this.W3 = this.initWeights(this.OUTPUT_DIM, this.HIDDEN2_DIM, 'xavier');
    this.b3 = Array(this.OUTPUT_DIM).fill(0);
    
    // Инициализация LoRA
    this.lora1 = new LoRALayer(this.INPUT_DIM, this.HIDDEN1_DIM, this.LORA_RANK);
    this.lora2 = new LoRALayer(this.HIDDEN1_DIM, this.HIDDEN2_DIM, this.LORA_RANK);
    
    // Загружаем предобученные веса (если есть)
    this.loadPretrainedWeights();
  }
  
  /**
   * Прямой проход нейросети
   */
  forward(input: SpinalInput): SpinalOutput {
    // Конвертируем вход в вектор
    const x = this.inputToVector(input);
    
    // Скрытый слой 1: h1 = ReLU(W1 @ x + b1 + LoRA1(x))
    const h1PreAct = Matrix.vectorMultiply(this.W1, x);
    const lora1Out = this.lora1.forward(x);
    for (let i = 0; i < h1PreAct.length; i++) {
      h1PreAct[i] += this.b1[i] + lora1Out[i];
    }
    const h1 = h1PreAct.map(Matrix.relu);
    
    // Скрытый слой 2: h2 = ReLU(W2 @ h1 + b2 + LoRA2(h1))
    const h2PreAct = Matrix.vectorMultiply(this.W2, h1);
    const lora2Out = this.lora2.forward(h1);
    for (let i = 0; i < h2PreAct.length; i++) {
      h2PreAct[i] += this.b2[i] + lora2Out[i];
    }
    const h2 = h2PreAct.map(Matrix.relu);
    
    // Выходной слой: out = softmax(W3 @ h2 + b3)
    const logits = Matrix.vectorMultiply(this.W3, h2);
    for (let i = 0; i < logits.length; i++) {
      logits[i] += this.b3[i];
    }
    const probs = Matrix.softmax(logits);
    
    // Выбираем действие
    const maxIdx = probs.indexOf(Math.max(...probs));
    const action = this.ACTIONS[maxIdx];
    const confidence = probs[maxIdx];
    
    // Формируем параметры действия
    const params = this.computeActionParams(action, input, h2);
    
    return { action, confidence, params };
  }
  
  /**
   * Обучение на одном примере
   */
  train(input: SpinalInput, targetAction: SpinalOutputAction, reward: number = 1.0): void {
    if (!this.trainingEnabled || this.currentStep >= this.maxTrainingSteps) return;
    
    // Получаем текущий выход
    const output = this.forward(input);
    
    // Вычисляем ошибку
    const targetIdx = this.ACTIONS.indexOf(targetAction);
    const outputIdx = this.ACTIONS.indexOf(output.action);
    
    // Если действие неправильное - обучаем
    if (targetIdx !== outputIdx) {
      // Простое обучение с подкреплением
      // Увеличиваем вес правильного действия
      
      // Градиент для выходного слоя (упрощённый)
      const outputGradient = Array(this.OUTPUT_DIM).fill(0);
      outputGradient[targetIdx] = reward * 0.1;
      outputGradient[outputIdx] = -reward * 0.05;
      
      // Обновляем LoRA слои
      const x = this.inputToVector(input);
      this.lora1.backward(x, outputGradient);
      
      this.currentStep++;
    }
  }
  
  /**
   * Обучение на основе исхода ситуации
   */
  trainFromOutcome(
    input: SpinalInput,
    actionTaken: SpinalOutputAction,
    outcome: 'success' | 'failure' | 'neutral'
  ): void {
    let reward = 0;
    
    switch (outcome) {
      case 'success':
        reward = 1.0;  // Положительное подкрепление
        break;
      case 'failure':
        reward = -1.0; // Отрицательное подкрепление
        break;
      case 'neutral':
        reward = 0.1;  // Слабое положительное
        break;
    }
    
    this.train(input, actionTaken, reward);
  }
  
  /**
   * Конвертация входных данных в вектор
   */
  private inputToVector(input: SpinalInput): number[] {
    return [
      input.dangerLevel,
      input.damageIntensity,
      input.threatDirectionX,
      input.threatDirectionY,
      input.distanceToEdge,
      input.soundIntensity,
      input.soundDirectionX,
      input.soundDirectionY,
      input.hpPercent,
      input.qiPercent,
      input.fatigue,
      input.isMoving,
    ];
  }
  
  /**
   * Вычисление параметров действия
   */
  private computeActionParams(
    action: SpinalOutputAction,
    input: SpinalInput,
    hidden: number[]
  ): SpinalActionParams {
    switch (action) {
      case 'dodge':
        return {
          direction: {
            x: -input.threatDirectionX + (Math.random() - 0.5) * 0.3,
            y: -input.threatDirectionY + (Math.random() - 0.5) * 0.3,
          },
          intensity: input.dangerLevel,
          speed: 1.0 + input.dangerLevel * 0.5,
        };
        
      case 'flinch':
        return {
          direction: {
            x: input.threatDirectionX,
            y: input.threatDirectionY,
          },
          intensity: input.damageIntensity,
        };
        
      case 'retreat':
        return {
          direction: {
            x: -input.threatDirectionX,
            y: -input.threatDirectionY,
          },
          speed: 0.8,
        };
        
      case 'turn_to_sound':
        return {
          direction: {
            x: input.soundDirectionX,
            y: input.soundDirectionY,
          },
        };
        
      case 'step_back':
        return {
          direction: {
            x: -input.threatDirectionX,
            y: -input.threatDirectionY,
          },
        };
        
      default:
        return {};
    }
  }
  
  /**
   * Инициализация весов (Xavier)
   */
  private initWeights(rows: number, cols: number, method: 'xavier' | 'he'): number[][] {
    const scale = method === 'xavier'
      ? Math.sqrt(2 / (rows + cols))
      : Math.sqrt(2 / rows);
    
    return Array(rows).fill(null).map(() =>
      Array(cols).fill(null).map(() => (Math.random() - 0.5) * 2 * scale)
    );
  }
  
  /**
   * Загрузка предобученных весов
   */
  private loadPretrainedWeights(): void {
    // В реальной реализации здесь загружаются веса из файла
    // или вычисляются на основе калибровки
    
    // Симуляция предобученных весов для базовых рефлексов
    // (веса настроены так, чтобы сеть выдавала разумные рефлексы)
    
    console.log('[SpinalNN] Loaded pretrained core weights');
  }
  
  /**
   * Экспорт состояния сети
   */
  exportState(): SpinalNNState {
    return {
      lora1: this.lora1.exportWeights(),
      lora2: this.lora2.exportWeights(),
      trainingStep: this.currentStep,
    };
  }
  
  /**
   * Импорт состояния сети
   */
  importState(state: SpinalNNState): void {
    this.lora1.importWeights(state.lora1);
    this.lora2.importWeights(state.lora2);
    this.currentStep = state.trainingStep;
  }
  
  /**
   * Сброс обучения
   */
  resetTraining(): void {
    this.lora1.reset();
    this.lora2.reset();
    this.currentStep = 0;
  }
  
  /**
   * Включить/выключить обучение
   */
  setTrainingEnabled(enabled: boolean): void {
    this.trainingEnabled = enabled;
  }
}

interface SpinalNNState {
  lora1: { A: number[][]; B: number[][] };
  lora2: { A: number[][]; B: number[][] };
  trainingStep: number;
}

// ==================== ФАСАД ДЛЯ ИСПОЛЬЗОВАНИЯ ====================

class SpinalNNController {
  private nn: SpinalNeuralNetwork;
  private lastInput: SpinalInput | null = null;
  private lastAction: SpinalOutputAction | null = null;
  
  // Пороги для обработки
  private readonly DANGER_THRESHOLD = 0.3;
  private readonly SOUND_THRESHOLD = 0.4;
  
  constructor() {
    this.nn = new SpinalNeuralNetwork();
  }
  
  /**
   * Обработка сенсорного ввода
   */
  process(signal: SpinalSignal, state: SpinalState): SpinalOutput | null {
    // Конвертируем сигнал и состояние во вход нейросети
    const input = this.signalToInput(signal, state);
    this.lastInput = input;
    
    // Прямой проход
    const output = this.nn.forward(input);
    this.lastAction = output.action;
    
    // Если уверенность низкая - нет действия
    if (output.confidence < 0.3) {
      return null;
    }
    
    return output;
  }
  
  /**
   * Сообщить об исходе действия
   */
  reportOutcome(outcome: 'success' | 'failure' | 'neutral'): void {
    if (this.lastInput && this.lastAction) {
      this.nn.trainFromOutcome(this.lastInput, this.lastAction, outcome);
    }
  }
  
  /**
   * Конвертация сигнала во вход нейросети
   */
  private signalToInput(signal: SpinalSignal, state: SpinalState): SpinalInput {
    // Вычисляем направление угрозы
    const threatDir = signal.direction || { x: 0, y: 0 };
    
    // Определяем уровень опасности
    const dangerLevel = this.calculateDangerLevel(signal, state);
    
    // Расстояние до края
    const edgeDistance = this.calculateEdgeDistance(state);
    
    return {
      dangerLevel,
      damageIntensity: signal.type === 'damage' ? signal.intensity : 0,
      threatDirectionX: threatDir.x,
      threatDirectionY: threatDir.y,
      distanceToEdge: edgeDistance,
      soundIntensity: signal.type === 'loud_sound' ? signal.intensity : 0,
      soundDirectionX: signal.type === 'loud_sound' ? (signal.direction?.x || 0) : 0,
      soundDirectionY: signal.type === 'loud_sound' ? (signal.direction?.y || 0) : 0,
      hpPercent: state.hpPercent,
      qiPercent: state.qi / 100,
      fatigue: state.fatigue / 100,
      isMoving: state.isMoving ? 1 : 0,
    };
  }
  
  private calculateDangerLevel(signal: SpinalSignal, state: SpinalState): number {
    let danger = 0;
    
    if (signal.type === 'danger_nearby') {
      danger = signal.intensity;
    } else if (signal.type === 'damage') {
      danger = 0.8;
    } else if (state.nearbyThreats.length > 0) {
      danger = 0.5;
    }
    
    return danger;
  }
  
  private calculateEdgeDistance(state: SpinalState): number {
    if (state.nearbyEdges.length === 0) return 1.0;
    
    const minDist = Math.min(...state.nearbyEdges.map(e => e.distance));
    return Math.min(1.0, minDist / 5); // Нормализуем
  }
  
  /**
   * Экспорт состояния для сохранения
   */
  exportState(): SpinalNNState {
    return this.nn.exportState();
  }
  
  /**
   * Импорт состояния при загрузке
   */
  importState(state: SpinalNNState): void {
    this.nn.importState(state);
  }
}
```

### Обучение в процессе игры

```typescript
// src/lib/game/ai/spinal-training.ts

/**
 * Система обучения Spinal NN в процессе игры
 */

interface TrainingEvent {
  timestamp: number;
  input: SpinalInput;
  action: SpinalOutputAction;
  outcome: 'success' | 'failure' | 'neutral';
  context: string;
}

class SpinalTrainer {
  private nn: SpinalNeuralNetwork;
  private eventHistory: TrainingEvent[] = [];
  private maxHistorySize: number = 100;
  
  // Параметры обучения
  private batchLearningEnabled: boolean = true;
  private batchSize: number = 10;
  
  constructor(nn: SpinalNeuralNetwork) {
    this.nn = nn;
  }
  
  /**
   * Записать событие для обучения
   */
  recordEvent(
    input: SpinalInput,
    action: SpinalOutputAction,
    outcome: 'success' | 'failure' | 'neutral',
    context: string
  ): void {
    const event: TrainingEvent = {
      timestamp: Date.now(),
      input,
      action,
      outcome,
      context,
    };
    
    this.eventHistory.push(event);
    
    // Ограничиваем историю
    if (this.eventHistory.length > this.maxHistorySize) {
      this.eventHistory.shift();
    }
    
    // Обучаем на одном примере
    this.nn.trainFromOutcome(input, action, outcome);
    
    // Периодическое батчевое обучение
    if (this.batchLearningEnabled && this.eventHistory.length % this.batchSize === 0) {
      this.batchTrain();
    }
  }
  
  /**
   * Батчевое обучение
   */
  private batchTrain(): void {
    // Берём последние события
    const batch = this.eventHistory.slice(-this.batchSize);
    
    // Вычисляем среднее вознаграждение по типам действий
    const actionStats = new Map<SpinalOutputAction, { success: number; total: number }>();
    
    for (const event of batch) {
      const stats = actionStats.get(event.action) || { success: 0, total: 0 };
      stats.total++;
      if (event.outcome === 'success') stats.success++;
      actionStats.set(event.action, stats);
    }
    
    console.log('[SpinalTrainer] Batch training stats:', Object.fromEntries(actionStats));
  }
  
  /**
   * Анализ эффективности обучения
   */
  getTrainingStats(): {
    totalEvents: number;
    successRate: number;
    actionStats: Record<string, { success: number; total: number }>;
  } {
    let successes = 0;
    const actionStats: Record<string, { success: number; total: number }> = {};
    
    for (const event of this.eventHistory) {
      if (event.outcome === 'success') successes++;
      
      if (!actionStats[event.action]) {
        actionStats[event.action] = { success: 0, total: 0 };
      }
      actionStats[event.action].total++;
      if (event.outcome === 'success') {
        actionStats[event.action].success++;
      }
    }
    
    return {
      totalEvents: this.eventHistory.length,
      successRate: successes / Math.max(1, this.eventHistory.length),
      actionStats,
    };
  }
}

// ==================== ПРИМЕР ИСПОЛЬЗОВАНИЯ ====================

/**
 * Пример интеграции Spinal NN в игрового NPC
 */
class NPCSpinalAI {
  private nnController: SpinalNNController;
  private trainer: SpinalTrainer;
  
  constructor() {
    const nn = new SpinalNeuralNetwork();
    this.nnController = new SpinalNNController();
    this.trainer = new SpinalTrainer(nn);
  }
  
  /**
   * Обработка сигнала
   */
  handleSignal(signal: SpinalSignal, state: SpinalState): SpinalAction | null {
    const output = this.nnController.process(signal, state);
    
    if (!output) return null;
    
    return {
      type: output.action,
      params: output.params,
      duration: this.getActionDuration(output.action),
      canInterrupt: this.canInterrupt(output.action),
    };
  }
  
  /**
   * Обратная связь после выполнения действия
   */
  feedback(outcome: 'success' | 'failure' | 'neutral', context: string = ''): void {
    this.nnController.reportOutcome(outcome);
    // Дополнительное логирование для аналитики
  }
  
  private getActionDuration(action: SpinalOutputAction): number {
    const durations: Record<SpinalOutputAction, number> = {
      dodge: 300,
      flinch: 200,
      freeze: 500,
      retreat: 400,
      turn_to_sound: 300,
      step_back: 250,
      balance: 400,
      none: 0,
    };
    return durations[action] || 100;
  }
  
  private canInterrupt(action: SpinalOutputAction): boolean {
    return !['freeze', 'balance'].includes(action);
  }
  
  /**
   * Сохранение состояния для персистентности
   */
  save(): SpinalNNState {
    return this.nnController.exportState();
  }
  
  /**
   * Загрузка состояния
   */
  load(state: SpinalNNState): void {
    this.nnController.importState(state);
  }
}
```

### Преимущества архитектуры

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ПРЕИМУЩЕСТВА SNN + LoRA                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. МГНОВЕННАЯ СКОРОСТЬ                                                  │
│     • < 1мс на CPU (в 100-1000 раз быстрее LLM)                         │
│     • Нет сетевых запросов                                              │
│     • Работа в реальном времени                                         │
│                                                                          │
│  2. ВОЗМОЖНОСТЬ ОБУЧЕНИЯ                                                 │
│     • Адаптация под стиль игры                                          │
│     • Персонализация NPC                                                │
│     • Улучшение со временем                                             │
│                                                                          │
│  3. ЭФФЕКТИВНОСТЬ ПАМЯТИ                                                 │
│     • ~712 параметров (килобайты)                                       │
│     • LoRA: ~112 обучаемых параметров                                   │
│     • Легко сохранять/загружать                                         │
│                                                                          │
│  4. СТАБИЛЬНОСТЬ                                                         │
│     • Core веса заморожены                                              │
│     • Базовые рефлексы всегда работают                                  │
│     • LoRA не может "сломать" базовое поведение                         │
│                                                                          │
│  5. ПЕРСОНАЛИЗАЦИЯ                                                       │
│     • Каждый NPC может иметь свой LoRA                                  │
│     • Разные "характеры" реакций                                        │
│     • Передача опыта между NPC                                          │
│                                                                          │
│  6. ОБЪЯСНИМОСТЬ                                                         │
│     • Понятные входы и выходы                                           │
│     • Логичные рефлексы                                                 │
│     • Легко отлаживать                                                  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Примеры персонализации через LoRA

```typescript
// Разные "характеры" для разных типов NPC

const LORA_PRESETS: Record<string, LoRAWeights> = {
  // Трусливый NPC - быстрее убегает
  cowardly: {
    // Увеличиваем вес retreat и freeze
    // Уменьшаем вес dodge и flinch
  },
  
  // Агрессивный NPC - контратакует вместо бегства
  aggressive: {
    // Увеличиваем вес dodge в сторону угрозы
    // Уменьшаем вес retreat
  },
  
  // Осторожный NPC - заранее отступает
  cautious: {
    // Увеличиваем чувствительность к dangerLevel
    // Раньше реагирует на угрозы
  },
  
  // Опытный культиватор - использует Qi для защиты
  cultivator: {
    // Интеграция с qiPercent для приоритизации defence
    // Специфичные реакции на Qi-атаки
  },
  
  // Монстр - простые агрессивные реакции
  monster: {
    // Минимальный freeze
    // Агрессивный dodge
  },
};
```

---

## 📊 Сравнение с классическими подходами

| Характеристика | State Machine | GOAP | Нейротеория |
|----------------|---------------|------|-------------|
| **Скорость реакции** | Средняя | Медленная | Мгновенная (Spinal) |
| **Сложность решений** | Простые | Средние | Любые (LLM) |
| **Гибкость** | Низкая | Высокая | Очень высокая |
| **Ресурсы CPU** | Низкие | Средние | Низкие (Spinal) / Высокие (Brain) |
| **Когнитивные способности** | Нет | Ограниченные | Полные (LLM) |
| **Диалоги** | Скриптовые | Скриптовые | Динамические |
| **Обучение** | Нет | Нет | Возможное |

---

## 🎯 Рекомендации по использованию

### Когда использовать Spinal AI
- Любые NPC (базовые рефлексы)
- Бой, уклонение, движение
- Реакция на опасность
- Физиологические потребности

### Когда использовать Neural Router
- Все NPC с комплексным поведением
- Координация нескольких систем
- Управление вниманием
- Асинхронные операции

### Когда использовать Brain (LLM)
- Статичные NPC (торговцы, главы сект)
- Сложные диалоги
- Нестандартные ситуации
- Планирование и анализ
- Обучение игрока

---

## 📁 Структура файлов

```
src/lib/game/ai/
├── neuro/
│   ├── spinal-ai.ts           # Рефлекторная система (правила)
│   ├── spinal-neural-network.ts # Нейросетевой Spinal AI + LoRA
│   ├── neural-router.ts       # Маршрутизатор
│   ├── brain-controller.ts    # Когнитивный уровень (LLM)
│   ├── neuro-ai-controller.ts # Главный контроллер
│   ├── spinal-training.ts     # Система обучения
│   └── types.ts               # Типы
```

---

## 🔗 Связанные документы

- [NPC_AI_THEORY.md](./NPC_AI_THEORY.md) - Базовая теория AI
- [checkpoint_03_24_AI.md](./checkpoints/checkpoint_03_24_AI.md) - Чекпоинт разработки

---

**АВТОР**: AI Assistant
**ВЕРСИЯ**: 1.1
**ДАТА**: 2026-03-24
