# 📝 Примеры кода Cultivation World Simulator

**Версия:** 2.0
**Дата:** 2026-03-24

> Этот файл содержит примеры кода для архитектурных решений.
> Основной документ: [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## ⏱️ Time Scaling (Масштабирование времени)

### Масштабирование скорости {#time-scaling}

```typescript
// src/lib/game/time-scaling.ts
import { scaleMovementSpeedInverse, scaleCooldown } from '@/lib/game/time-scaling';
import { useTimeStore } from '@/stores/time.store';

// Правильное масштабирование скорости движения
const timeStore = useTimeStore.getState();
const baseSpeed = 200; // px/sec при normal time

// ✅ ИСПОЛЬЗОВАТЬ scaleMovementSpeedInverse:
const scaledSpeed = scaleMovementSpeedInverse(baseSpeed, timeStore.speed);

// slow time (factor 5.0): 200 / 5.0 = 40 px/sec (медленно)
// normal time (factor 1.0): 200 / 1.0 = 200 px/sec (обычно)
// fast time (factor 0.333): 200 / 0.333 = 600 px/sec (быстро)

// ⚠️ НЕ ИСПОЛЬЗОВАТЬ scaleMovementSpeed (инверсия!):
// const wrongSpeed = scaleMovementSpeed(baseSpeed, timeStore.speed);
// slow time: 200 * 5.0 = 1000 px/sec (слишком быстро!)
```

### Activity Manager {#activity-manager}

```typescript
// src/lib/game/activity-manager.ts
import { activityManager, startCombat, endCombat } from '@/lib/game/activity-manager';

// Начало боя - автоматически переключает на superSuperSlow
startCombat(); // или activityManager.setActivity('combat');

// Окончание боя - восстанавливает предыдущую скорость
endCombat(); // или activityManager.endActivity();

// Начало медитации - переключает на ultra
activityManager.setActivity('meditation');

// Начало путешествия - переключает на fast
activityManager.setActivity('travel');
```

### Профили активностей {#activity-profiles}

```typescript
// src/lib/game/action-speeds.ts
import { ACTION_SPEED_PROFILES, getActivityProfile } from '@/lib/game/action-speeds';

const combatProfile = getActivityProfile('combat');
// {
//   activity: 'combat',
//   preferredSpeed: 'superSuperSlow',
//   autoSwitch: true,
//   rememberPrevious: true,
//   description: 'Замедленное время для тактического боя'
// }

const meditationProfile = getActivityProfile('meditation');
// {
//   activity: 'meditation',
//   preferredSpeed: 'ultra',
//   autoSwitch: true,
//   rememberPrevious: true,
//   description: 'Максимальное ускорение при культивации'
// }
```

---

## ⏰ Система времени

### Инициализация TickTimer {#time-init}

```typescript
// src/lib/tick-timer.ts
import { tickTimer } from '@/lib/tick-timer';
import { getQiTickProcessor } from '@/lib/game/qi-tick-processor';

// Инициализация
tickTimer.initDefaultQiProcessor();
tickTimer.start();
```

### React Hook {#time-hooks}

```typescript
// src/hooks/useTickTimer.ts
import { useTimeStore } from '@/stores/time.store';

export function useTickTimer() {
  const {
    isPaused,
    isRunning,
    tickCount,
    speed,
    gameTime,
    togglePause,
    setSpeed,
  } = useTimeStore();

  return {
    isPaused,
    isRunning,
    tickCount,
    speed,
    gameTime,
    togglePause,
    setSpeed,
    formatTime: () => formatGameDateTime(gameTime),
  };
}
```

### Phaser Scene Listener {#time-phaser}

```typescript
// src/game/scenes/LocationScene.ts
create() {
  // Слушатель тиков
  if (typeof window !== 'undefined') {
    window.addEventListener('game:tick', ((event: Event) => {
      const detail = (event as CustomEvent<TickEventDetail>).detail;
      this.onGameTick(detail);
    }) as EventListener);
  }
}

private onGameTick(detail: TickEventDetail): void {
  // Обновление NPC, анимации и т.д.
  this.updateNPCs(detail.minutesPerTick);
  this.updateDayNightCycle(detail.gameTime.hour);
}
```

### TruthSystem Sync {#time-truth}

```typescript
// После загрузки сессии
const truthSystem = TruthSystem.getInstance();
await truthSystem.loadSession(sessionId);

// Подключение к TickTimer
truthSystem.setupTickTimerSync(sessionId);

// При выгрузке
truthSystem.cleanupTickTimerSync();
await truthSystem.unloadSession(sessionId);
```

---

## 🔌 Event Bus

### Клиентский вызов {#event-bus-client}

```typescript
// src/lib/game/event-bus/client.ts
import { EventBusClient } from '@/lib/game/event-bus/client';

const client = EventBusClient.getInstance();

// Использование техники
const result = await client.useTechnique('technique_001', { x: 100, y: 200 });

if (result.canUse) {
  // Нанести урон с множителем
  const finalDamage = baseDamage * result.damageMultiplier;
}
```

### Серверный обработчик {#event-bus-server}

```typescript
// src/lib/game/event-bus/handlers/combat.ts
export async function handleTechniqueUse(
  sessionId: string,
  data: TechniqueUseData
): Promise<TechniqueUseResult> {
  const truthSystem = TruthSystem.getInstance();
  
  // Проверка и списание Qi
  const spendResult = truthSystem.spendQi(
    sessionId,
    technique.qiCost,
    'technique',
    `technique:${data.techniqueId}`
  );
  
  if (!spendResult.success) {
    return { canUse: false, reason: 'Недостаточно Ци' };
  }
  
  return {
    canUse: true,
    damageMultiplier: calculateDamageMultiplier(character, technique),
    currentQi: spendResult.data.currentQi,
  };
}
```

---

## 💾 TruthSystem

### Критические операции {#truth-critical}

```typescript
// Прорыв уровня - НЕМЕДЛЕННОЕ сохранение
await truthSystem.applyBreakthrough(sessionId, {
  newLevel: 3,
  newSubLevel: 0,
  newCoreCapacity: 1500,
  newConductivity: 0.85,
  qiConsumed: 500,
});

// Новая техника - НЕМЕДЛЕННОЕ сохранение
await truthSystem.addTechnique(sessionId, {
  techniqueId: 'technique_001',
  mastery: 0,
  quickSlot: 1,
  learningSource: 'insight',
});
```

### Обновление персонажа {#truth-character}

```typescript
// Обновление в памяти (isDirty = true)
truthSystem.updateCharacter(sessionId, {
  currentQi: 500,
  fatigue: 30,
  mentalFatigue: 20,
}, 'meditation');

// TruthSystem автоматически сохранит при:
// - batch save (каждые 60 тиков)
// - паузе игры
// - критическом событии
```

---

## ⚡ QiTickProcessor

### Использование {#qi-processor}

```typescript
// src/lib/game/qi-tick-processor.ts
import { initQiTickProcessor, getQiTickProcessor } from '@/lib/game/qi-tick-processor';

// Инициализация при старте игры
const processor = initQiTickProcessor(sessionId, characterId);

// Ручной flush перед паузой
await processor.flush();

// Очистка при завершении
processor.clearSession();
```

### Интеграция в TickTimer {#qi-timer}

```typescript
// src/lib/tick-timer.ts (внутри processTick)
private processTick(): void {
  // ... инкремент тика ...
  
  // Обработка Qi эффектов
  if (this.qiProcessor) {
    this.qiProcessor.processTick({
      tickCount,
      gameTime,
      speed,
      minutesPerTick,
      timestamp: Date.now(),
    });
  }
  
  // Отправка события
  this.emitEvent('game:tick', tickDetail);
}
```

---

## 📦 Zustand Store

### Time Store {#store-time}

```typescript
// src/stores/time.store.ts
import { create } from 'zustand';

interface TimeStoreState {
  isPaused: boolean;
  isRunning: boolean;
  tickCount: number;
  speed: TickSpeedId;
  gameTime: GameTime;
  
  togglePause: () => void;
  setSpeed: (speed: TickSpeedId) => void;
  _incrementTick: () => void;
  _calculateGameTime: (totalMinutes: number) => Omit<GameTime, 'totalMinutes'>;
}

export const useTimeStore = create<TimeStoreState>()((set, get) => ({
  isPaused: true,
  isRunning: false,
  tickCount: 0,
  speed: 'normal',
  gameTime: initialGameTime,
  
  togglePause: () => set(s => ({ isPaused: !s.isPaused })),
  setSpeed: (speed) => set({ speed }),
  
  _incrementTick: () => {
    const state = get();
    const minutesPerTick = state.speeds[state.speed].minutesPerTick;
    const newTotalMinutes = state.gameTime.totalMinutes + minutesPerTick;
    const newGameTime = get()._calculateGameTime(newTotalMinutes);
    
    set({
      tickCount: state.tickCount + 1,
      gameTime: { ...newGameTime, totalMinutes: newTotalMinutes },
    });
  },
}));
```

### Game Store {#store-game}

```typescript
// src/stores/game.store.ts
interface GameStoreState {
  sessionId: string | null;
  characterId: string | null;
  currentLocationId: string | null;
  isLoading: boolean;
  
  // Actions
  loadState: (sessionId: string) => Promise<void>;
  startGame: (variant: number) => Promise<void>;
  saveGame: () => Promise<void>;
}

// Селекторы
export const useGameCharacter = () => useGameStore(s => s.character);
export const useGameLocation = () => useGameStore(s => s.currentLocation);
export const useGameSessionId = () => useGameStore(s => s.sessionId);
```

---

## 🎮 Phaser 3

### Конфигурация {#phaser-config}

```typescript
const config: Phaser.Types.Core.GameConfig = {
  type: Phaser.AUTO,
  width: 900,
  height: 550,
  physics: {
    default: 'arcade',
    arcade: { gravity: { x: 0, y: 0 } }
  },
  scene: [LocationScene],
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH
  }
};
```

### Генерация текстур {#phaser-textures}

```typescript
// Создание текстуры игрока
const graphics = this.make.graphics();
graphics.fillStyle(0x4ade80); // Зелёный
graphics.fillCircle(24, 24, 24);
graphics.generateTexture('player', 48, 48);
graphics.destroy();

// Создание текстуры NPC
const npcGraphics = this.make.graphics();
npcGraphics.fillStyle(0xef4444); // Красный
npcGraphics.fillCircle(20, 20, 20);
npcGraphics.generateTexture('npc', 40, 40);
npcGraphics.destroy();
```

---

## 🔗 Связанные документы

- [ARCHITECTURE.md](./ARCHITECTURE.md) — Основная архитектура
- [TIME_SYSTEM.md](./TIME_SYSTEM.md) — Документация системы времени
- [FUNCTIONS.md](./FUNCTIONS.md) — Справочник функций

---

*Дата создания: 2026-03-24*
