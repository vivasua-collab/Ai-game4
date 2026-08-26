# ARCHITECTURE_future.md - Будущая архитектура

**Версия:** 1.0
**Дата:** 2026-03-25
**Статус:** 📋 Планирование

---

## 🎯 Цель документа

Определить архитектуру для перехода от sandbox-ориентированной разработки к автономному постоянно живущему серверу с сессионными подключениями игроков.

---

## 1. Анализ текущей архитектуры

### 1.1 Текущее состояние (Sandbox)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ТЕКУЩАЯ АРХИТЕКТУРА (Sandbox)                              │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   BROWSER (Preview Panel)                                                    │
│   │                                                                          │
│   └── sandboxed iframe                                                       │
│       ├── ❌ localStorage ЗАБЛОКИРОВАН                                      │
│       ├── ❌ sessionStorage ЗАБЛОКИРОВАН                                    │
│       └── ✅ Только HTTP/HTTPS запросы                                      │
│                                                                              │
│   CADDY GATEWAY (Port 81)                                                    │
│   │                                                                          │
│   ├── Default → localhost:3000 (Next.js)                                    │
│   └── XTransformPort=3003 → localhost:3003 (WebSocket)                      │
│                                                                              │
│   NEXT.JS (Port 3000)                                                        │
│   └── HTTP API routes                                                        │
│                                                                              │
│   SOCKET.IO (Port 3003) - mini-service                                       │
│   └── WebSocket server                                                       │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Почему используется Caddy?

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ПРИЧИНЫ ИСПОЛЬЗОВАНИЯ CADDY                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. ОГРАНИЧЕНИЯ SANDBOX                                                    │
│      ├── Внешне доступен только один порт (81)                              │
│      ├── Sandbox iframe изолирован от прямых портов                         │
│      └── Нужен reverse proxy для маршрутизации                              │
│                                                                              │
│   2. МАРШРУТИЗАЦИЯ ПОРТОВ                                                   │
│      ├── Caddy слушает :81                                                  │
│      ├── По умолчанию → proxy на :3000 (Next.js)                            │
│      └── XTransformPort=N → proxy на :N                                     │
│                                                                              │
│   3. ПРЕИМУЩЕСТВА CADDY                                                     │
│      ├── Автоматический HTTPS (не используется в sandbox)                   │
│      ├── Простая конфигурация (Caddyfile)                                   │
│      ├── WebSocket proxy "из коробки"                                       │
│      └── Высокая производительность                                         │
│                                                                              │
│   4. НЕДОСТАТКИ CADDY (для нашего случая)                                   │
│      ├── Дополнительная зависимость                                         │
│      ├── Необходимость XTransformPort в query                               │
│      ├── Задержки 50-100мс                                                  │
│      └── Сложнее для production deployment                                  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Альтернативы Caddy

### 2.1 Вариант A: Unified Port (HTTP + WebSocket на 3000)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ВАРИАНТ A: UNIFIED PORT                                    │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ПРИНЦИП:                                                                   │
│   Один HTTP сервер принимает и HTTP, и WebSocket соединения                 │
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    UNIFIED SERVER (Port 3000)                        │   │
│   │                                                                      │   │
│   │   const httpServer = createServer(nextApp.getRequestHandler());     │   │
│   │   const io = new Server(httpServer);                                │   │
│   │   httpServer.listen(3000);                                          │   │
│   │                                                                      │   │
│   │   // HTTP запросы → Next.js                                         │   │
│   │   // WebSocket → Socket.IO                                          │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   ПРЕИМУЩЕСТВА:                                                              │
│   ✅ Один порт                                                               │
│   ✅ Нет XTransformPort                                                      │
│   ✅ Работает в sandbox БЕЗ Caddy                                           │
│   ✅ Проще deployment                                                        │
│                                                                              │
│   НЕДОСТАТКИ:                                                                │
│   ⚠️ Нужен custom Next.js server                                            │
│   ⚠️ Сложнее debug (mixed traffic)                                          │
│   ⚠️ Потеря Vercel edge features                                            │
│                                                                              │
│   РЕАЛИЗАЦИЯ:                                                                │
│   См. раздел 4.1                                                             │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Вариант B: Native Bun WebSocket

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ВАРИАНТ B: NATIVE BUN WEBSOCKET                            │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ПРИНЦИП:                                                                   │
│   Использовать встроенный WebSocket API Bun без Socket.IO                   │
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    BUN SERVER                                         │   │
│   │                                                                      │   │
│   │   Bun.serve({                                                        │   │
│   │     port: 3000,                                                      │   │
│   │     fetch(req, server) {                                             │   │
│   │       // HTTP → Next.js                                              │   │
│   │       return nextApp.fetch(req);                                     │   │
│   │     },                                                               │   │
│   │     websocket: {                                                     │   │
│   │       open(ws) { ... },                                              │   │
│   │       message(ws, msg) { ... },                                      │   │
│   │       close(ws) { ... },                                             │   │
│   │     }                                                                │   │
│   │   });                                                                │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   ПРЕИМУЩЕСТВА:                                                              │
│   ✅ Максимальная производительность                                         │
│   ✅ Нет внешних зависимостей (Socket.IO)                                   │
│   ✅ Нативная интеграция с Bun                                              │
│   ✅ Меньше памяти                                                           │
│                                                                              │
│   НЕДОСТАТКИ:                                                                │
│   ⚠️ Нет fallback на polling                                                │
│   ⚠️ Нужен свой protocol                                                    │
│   ⚠️ Сложнее rooms/broadcast                                                │
│   ⚠️ Меньше ecosystem                                                       │
│                                                                              │
│   РЕАЛИЗАЦИЯ:                                                                │
│   См. раздел 4.2                                                             │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Вариант C: Next.js Custom Server + Socket.IO

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    ВАРИАНТ C: CUSTOM SERVER                                   │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ПРИНЦИП:                                                                   │
│   Создать custom server.js для Next.js с интегрированным Socket.IO          │
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    CUSTOM SERVER                                      │   │
│   │                                                                      │   │
│   │   // server.ts                                                       │   │
│   │   import { createServer } from 'http';                               │   │
│   │   import { parse } from 'url';                                       │   │
│   │   import next from 'next';                                           │   │
│   │   import { Server } from 'socket.io';                                │   │
│   │                                                                      │   │
│   │   const app = next({ dev: true });                                   │   │
│   │   const handle = app.getRequestHandler();                            │   │
│   │                                                                      │   │
│   │   const server = createServer((req, res) => {                        │   │
│   │     handle(req, res, parse(req.url, true));                          │   │
│   │   });                                                                │   │
│   │                                                                      │   │
│   │   const io = new Server(server, { path: '/socket.io' });             │   │
│   │                                                                      │   │
│   │   server.listen(3000);                                               │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   ПРЕИМУЩЕСТВА:                                                              │
│   ✅ Socket.IO ecosystem (rooms, broadcast, fallback)                       │
│   ✅ Один порт                                                               │
│   ✅ Знакомый API                                                            │
│                                                                              │
│   НЕДОСТАТКИ:                                                                │
│   ⚠️ Custom server = потеря Vercel features                                │
│   ⚠️ Дополнительный entry point                                             │
│   ⚠️ Сложнее в maintenance                                                  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 2.4 Сравнение вариантов

| Критерий | Caddy + Ports | Unified | Bun Native | Custom Server |
|----------|---------------|---------|------------|---------------|
| **Порты** | 81→3000/3003 | 3000 | 3000 | 3000 |
| **Sandbox** | ✅ Работает | ✅ Работает | ✅ Работает | ✅ Работает |
| **Задержка** | 50-100мс | 1-20мс | < 5мс | 1-20мс |
| **Сложность** | Средняя | Средняя | Высокая | Средняя |
| **Fallback polling** | ✅ Socket.IO | ✅ Socket.IO | ❌ Нет | ✅ Socket.IO |
| **Vercel deploy** | ✅ Да | ❌ Нет | ❌ Нет | ❌ Нет |
| **Производительность** | Хорошая | Хорошая | Отличная | Хорошая |

---

## 3. Рекомендуемое решение

### 3.1 Выбор: Вариант C (Custom Server + Socket.IO)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    РЕКОМЕНДАЦИЯ                                               │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ВЫБРАНО: Next.js Custom Server + Socket.IO                                │
│                                                                              │
│   ПРИЧИНЫ:                                                                   │
│   1. Socket.IO ecosystem - rooms, broadcast, auto-reconnect, polling       │
│   2. Один порт - упрощает deployment                                        │
│   3. Знакомый API - текущий код Socket.IO сохраняется                       │
│   4. Fallback polling - работает даже при проблемах с WebSocket             │
│                                                                              │
│   ПЛАН ПЕРЕХОДА:                                                             │
│   Фаза 1: Сохранить текущую архитектуру (Caddy + XTransformPort)            │
│   Фаза 2: Создать custom server для production                              │
│   Фаза 3: Унифицированный клиент с автоопределением режима                  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Унифицированная архитектура

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    УНИФИЦИРОВАННАЯ АРХИТЕКТУРА                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                        GAME SERVER                                   │   │
│   │                                                                      │   │
│   │   ┌─────────────────────────────────────────────────────────────┐   │   │
│   │   │                    PERSISTENT WORLD                          │   │   │
│   │   │                                                              │   │   │
│   │   │   TruthSystem (Singleton)                                    │   │   │
│   │   │      │                                                       │   │   │
│   │   │      ├── worldState: WorldState                              │   │   │
│   │   │      │      ├── time: WorldTime                              │   │   │
│   │   │      │      ├── npcs: Map<id, NPCState>                      │   │   │
│   │   │      │      ├── locations: Map<id, LocationState>            │   │   │
│   │   │      │      └── globalEvents: Event[]                        │   │   │
│   │   │      │                                                       │   │   │
│   │   │      └── sessions: Map<id, SessionState>                     │   │   │
│   │   │             └── player connections                           │   │   │
│   │   │                                                              │   │   │
│   │   │   NPCAIManager                                               │   │   │
│   │   │      └── Обновление NPC каждый tick                          │   │   │
│   │   │                                                              │   │   │
│   │   │   TickLoop (1 tick = 1 second real time)                     │   │   │
│   │   │      ├── updateWorld()                                       │   │   │
│   │   │      ├── updateNPCs()                                        │   │   │
│   │   │      ├── updatePlayers()                                     │   │   │
│   │   │      └── broadcastChanges()                                  │   │   │
│   │   │                                                              │   │   │
│   │   └─────────────────────────────────────────────────────────────┘   │   │
│   │                              │                                       │   │
│   │                              │ WebSocket / HTTP                      │   │
│   │                              ▼                                       │   │
│   │   ┌─────────────────────────────────────────────────────────────┐   │   │
│   │   │                    TRANSPORT LAYER                           │   │   │
│   │   │                                                              │   │   │
│   │   │   Unified Server (Port 3000)                                 │   │   │
│   │   │   ├── HTTP API → Next.js routes                              │   │   │
│   │   │   └── WebSocket → Socket.IO                                  │   │   │
│   │   │                                                              │   │   │
│   │   └─────────────────────────────────────────────────────────────┘   │   │
│   │                              │                                       │   │
│   └──────────────────────────────┼───────────────────────────────────────┘   │
│                                  │                                           │
│   ═════════════════════════════════════════════════════════════════════════  │
│                                  │                                           │
│          ┌───────────────────────┴───────────────────────┐                  │
│          │                                               │                  │
│          ▼                                               ▼                  │
│   ┌─────────────────────┐                    ┌─────────────────────┐         │
│   │   SANDBOX MODE      │                    │   PRODUCTION MODE   │         │
│   │   (Development)     │                    │   (Deployment)      │         │
│   │                     │                    │                     │         │
│   │   Caddy :81         │                    │   Direct :3000      │         │
│   │   XTransformPort    │                    │   Unified server    │         │
│   │                     │                    │                     │         │
│   │   Для: тестирование │                    │   Для: продакшн     │         │
│   │   в sandbox iframe  │                    │   localhost/deploy │         │
│   └─────────────────────┘                    └─────────────────────┘         │
│                                                                              │
│   ═════════════════════════════════════════════════════════════════════════  │
│                                                                              │
│   CLIENT (Browser) - ОДИНАКОВЫЙ КОД                                         │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                                                                      │   │
│   │   // Автоопределение режима                                          │   │
│   │   const socket = createGameSocket();                                │   │
│   │                                                                      │   │
│   │   // Внутри createGameSocket:                                        │   │
│   │   if (window.location.port === '') {                                 │   │
│   │     // Sandbox → через XTransformPort                               │   │
│   │     return io('/?XTransformPort=3003');                              │   │
│   │   } else {                                                           │   │
│   │     // Production → прямое подключение                              │   │
│   │     return io(window.location.origin);                               │   │
│   │   }                                                                  │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. План реализации

### 4.1 Фаза 1: Расширение World State

**Задача:** Добавить поддержку NPC в серверное состояние

```typescript
// src/lib/game/types/world-state.ts

interface WorldState {
  // Время мира
  time: WorldTimeState;

  // NPC в мире
  npcs: Map<string, NPCState>;

  // Локации
  locations: Map<string, LocationState>;

  // Глобальные события
  globalEvents: WorldEvent[];

  // Метаданные
  lastTickTime: number;
  tickCount: number;
  isRunning: boolean;
}

interface NPCState {
  id: string;
  name: string;
  species: string;
  role: 'monster' | 'guard' | 'passerby' | 'cultivator';

  // Позиция
  locationId: string;
  x: number;
  y: number;
  z: number;

  // Состояние
  health: number;
  maxHealth: number;
  qi: number;
  maxQi: number;

  // AI состояние
  isActive: boolean;
  currentAction: NPCAction | null;
  actionQueue: NPCAction[];

  // Spinal AI
  spinalState: SpinalAIState;

  // Агрессия
  threatLevel: number;
  targetId: string | null;
}
```

### 4.2 Фаза 2: WebSocket Service

**Задача:** Создать мини-сервис для real-time

```
mini-services/game-ws/
├── index.ts           # Entry point
├── events/
│   ├── player.ts      # player:connect, player:move, player:attack
│   ├── npc.ts         # npc:spawn, npc:action, npc:despawn
│   └── world.ts       # world:sync, world:tick
├── managers/
│   ├── connection.ts  # Управление подключениями
│   ├── broadcast.ts   # Broadcast сообщений
│   └── tick-loop.ts   # Tick Loop
└── package.json
```

### 4.3 Фаза 3: Server AI Manager

**Задача:** Перенести AI на сервер

```
src/lib/game/ai/server/
├── npc-ai-manager.ts    # Главный менеджер
├── spinal-server.ts     # Адаптер для SpinalController
├── tick-loop.ts         # Tick Loop
├── broadcast-manager.ts # Отправка событий
└── types.ts             # Типы
```

### 4.4 Фаза 4: Custom Server (Production)

**Задача:** Создать unified server для production

```typescript
// server.ts

import { createServer } from 'http';
import { parse } from 'url';
import next from 'next';
import { Server } from 'socket.io';
import { TruthSystem } from './src/lib/game/truth-system';
import { NPCAIManager } from './src/lib/game/ai/server/npc-ai-manager';
import { setupGameSocketHandlers } from './src/lib/game/socket-handlers';

const dev = process.env.NODE_ENV !== 'production';
const app = next({ dev });
const handle = app.getRequestHandler();

app.prepare().then(() => {
  const server = createServer((req, res) => {
    const parsedUrl = parse(req.url!, true);
    handle(req, res, parsedUrl);
  });

  // Socket.IO
  const io = new Server(server, {
    path: '/socket.io',
    cors: { origin: '*' },
  });

  // Game socket handlers
  setupGameSocketHandlers(io);

  // Start Tick Loop
  TruthSystem.getInstance();
  NPCAIManager.getInstance().startTickLoop();

  server.listen(3000, () => {
    console.log('Game Server running on port 3000');
  });
});
```

### 4.5 Фаза 5: Унифицированный клиент

```typescript
// src/lib/game-socket.ts

import { io, Socket } from 'socket.io-client';

type GameSocketOptions = {
  reconnection?: boolean;
  reconnectionAttempts?: number;
};

/**
 * Создать WebSocket соединение
 * Автоопределение режима (sandbox/production)
 */
export function createGameSocket(options?: GameSocketOptions): Socket {
  const isSandbox = typeof window !== 'undefined' && window.location.port === '';

  let socketUrl: string;
  let socketPath: string;

  if (isSandbox) {
    // Sandbox mode: через Caddy с XTransformPort
    socketUrl = window.location.origin;
    socketPath = '/?XTransformPort=3003';
  } else {
    // Production mode: прямое подключение
    socketUrl = window.location.origin;
    socketPath = '/socket.io';
  }

  return io(socketUrl, {
    path: socketPath,
    transports: ['websocket', 'polling'],
    reconnection: options?.reconnection ?? true,
    reconnectionAttempts: options?.reconnectionAttempts ?? 10,
    reconnectionDelay: 1000,
    timeout: 10000,
  });
}

/**
 * Проверить, находимся ли в sandbox режиме
 */
export function isSandboxMode(): boolean {
  return typeof window !== 'undefined' && window.location.port === '';
}
```

---

## 5. WebSocket события

### 5.1 Клиент → Сервер

| Событие | Данные | Описание |
|---------|--------|----------|
| `player:connect` | `{ sessionId }` | Подключение к миру |
| `player:move` | `{ x, y, z }` | Движение игрока |
| `player:attack` | `{ targetId, techniqueId }` | Атака |
| `player:action` | `{ type, data }` | Общее действие |

### 5.2 Сервер → Клиент

| Событие | Данные | Описание |
|---------|--------|----------|
| `world:sync` | `{ npcs, time }` | Полная синхронизация |
| `world:tick` | `{ tick, time }` | Тик времени |
| `npc:spawn` | `{ npc }` | Появление NPC |
| `npc:despawn` | `{ npcId }` | Исчезновение NPC |
| `npc:action` | `{ npcId, action }` | Действие NPC |
| `npc:update` | `{ npcId, changes }` | Обновление NPC |
| `combat:hit` | `{ attackerId, targetId, damage }` | Попадание |
| `combat:death` | `{ targetId, killerId }` | Смерть |
| `player:update` | `{ character }` | Обновление игрока |

---

## 6. Анализ задержек

### 6.1 При 1 TICK = 1 СЕКУНДА

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    АНАЛИЗ ЗАДЕРЖЕК                                            │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   SANDBOX (Caddy):                                                          │
│   ├── HTTP: 50-100мс                                                        │
│   └── WebSocket: 50-110мс                                                   │
│       └── ✅ 110мс << 1000мс (1 TICK) - ПРИЕМЛЕМО                           │
│                                                                              │
│   PRODUCTION (Direct):                                                      │
│   ├── HTTP: 1-10мс                                                          │
│   └── WebSocket: 10-20мс                                                    │
│       └── ✅ 20мс << 1000мс (1 TICK) - ОТЛИЧНО                              │
│                                                                              │
│   ВЫВОД: Оба режима работоспособны для AI                                   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 7. Принципы архитектуры

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         КЛЮЧЕВЫЕ ПРИНЦИПЫ                                     │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. СЕРВЕР - ИСТОЧНИК ИСТИНЫ                                               │
│      └── Вся логика, состояние, AI - на сервере                             │
│                                                                              │
│   2. КЛИЕНТ - ТОЛЬКО ОТОБРАЖЕНИЕ                                            │
│      └── Никакой бизнес-логики, только рендеринг                            │
│                                                                              │
│   3. ОДНА АРХИТЕКТУРА - ВСЕ РЕЖИМЫ                                          │
│      └── Sandbox, localhost, production - один код                          │
│                                                                              │
│   4. ПОСТОЯННО ЖИВУЩИЙ СЕРВЕР                                               │
│      └── Мир существует независимо от подключений                           │
│                                                                              │
│   5. СЕССИОННЫЕ ПОДКЛЮЧЕНИЯ                                                 │
│      └── Игроки подключаются/отключаются, мир продолжается                  │
│                                                                              │
│   6. DUAL PROTOCOL                                                          │
│      ├── HTTP: REST операции (медитация, инвентарь)                         │
│      └── WebSocket: Real-time (бои, NPC, тики)                              │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 8. Переход от sandbox к production

### 8.1 Текущее состояние

| Компонент | Sandbox | Production |
|-----------|---------|------------|
| Entry point | `next dev` | `next start` или `server.ts` |
| Порты | 3000 + 3003 (через Caddy) | 3000 (unified) |
| WebSocket | mini-service | Custom server |
| Caddy | Обязателен | Не нужен |

### 8.2 План миграции

```
ШАГ 1: Сохранить текущую архитектуру
       ├── Caddy + XTransformPort работает
       └── mini-service на порту 3003

ШАГ 2: Создать custom server
       ├── server.ts с Socket.IO
       └── Тестировать на localhost

ШАГ 3: Унифицировать клиент
       ├── createGameSocket() с автоопределением
       └── Работает в обоих режимах

ШАГ 4: Production deployment
       ├── Использовать server.ts
       └── Без Caddy
```

---

## 9. Связанные документы

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Текущая архитектура
- [checkpoint_03_25.md](./checkpoints/checkpoint_03_25.md) - Задача дня
- [checkpoint_03_25_websocket.md](./checkpoints/checkpoint_03_25_websocket.md) - WebSocket анализ
- [checkpoint_03_25_AI_server_implementation_plan.md](./checkpoints/checkpoint_03_25_AI_server_implementation_plan.md) - План AI

---

**АВТОР**: AI Assistant
**ДАТА**: 2026-03-25
