# 📦 Установка и запуск Cultivation World Simulator

> Полное руководство по установке, настройке и запуску проекта.
> Последнее обновление: 2024-03-24

---

## 📋 Системные требования

| Компонент | Минимум | Рекомендуется |
|-----------|---------|---------------|
| **Bun** | 1.0+ | 1.1+ |
| **Node.js** | 18+ | 20+ (если не используется Bun) |
| **RAM** | 4 GB | 8 GB |
| **Диск** | 500 MB | 1 GB |
| **ОС** | Linux, macOS, Windows (WSL2) | Linux, macOS |

---

## 🚀 Быстрый старт

### 1. Клонирование проекта

```bash
git clone https://github.com/vivasua-collab/Ai-Game2.git
cd Ai-Game2
git checkout main2D
```

### 2. Установка зависимостей

```bash
bun install
```

### 3. Инициализация базы данных

```bash
bun run db:generate
bun run db:push
```

### 4. Запуск

```bash
bun run dev
```

Игра автоматически откроется в 2D режиме!

---

## 🎮 Первый запуск

При первом запуске:

1. **Автоматически создаётся персонаж** — имя "Путник", вариант старта "Секта"
2. **Сессия сохраняется** — ID записывается в localStorage
3. **LLM генерирует историю** — уникальное вступление

При последующих запусках:
- Сессия восстанавливается из localStorage автоматически

---

## 📁 Структура проекта

```
Ai-Game2/
├── src/
│   ├── app/
│   │   ├── api/              # API эндпоинты
│   │   │   ├── game/         # start, state, move
│   │   │   ├── rest/         # медитация, отдых, сон
│   │   │   ├── technique/    # использование техник
│   │   │   └── chat/         # чат с LLM
│   │   ├── page.tsx          # Главная страница (автозапуск)
│   │   └── layout.tsx        # Корневой layout
│   │
│   ├── components/
│   │   ├── game/             # Игровые компоненты
│   │   │   ├── PhaserGame.tsx      # 2D игра
│   │   │   ├── ChatPanel.tsx       # Чат + статус
│   │   │   ├── RestDialog.tsx      # Единый диалог отдыха
│   │   │   ├── StatusDialog.tsx    # Статус персонажа
│   │   │   ├── TechniquesDialog.tsx # Техники
│   │   │   └── ActionButtons.tsx   # Кнопки действий
│   │   └── ui/               # shadcn/ui компоненты
│   │
│   ├── data/
│   │   └── presets/          # Унифицированные пресеты
│   │       ├── base-preset.ts      # Базовый интерфейс
│   │       ├── technique-presets.ts # Техники (13 шт)
│   │       ├── skill-presets.ts    # Навыки (9 шт)
│   │       ├── formation-presets.ts # Формации (8 шт)
│   │       ├── item-presets.ts     # Предметы (15 шт)
│   │       ├── character-presets.ts # Персонажи (6 шт)
│   │       └── index.ts            # Единый экспорт
│   │
│   ├── lib/
│   │   ├── game/             # Игровая логика
│   │   │   ├── constants.ts        # Константы игры
│   │   │   ├── qi-system.ts        # Медитация, прорыв
│   │   │   ├── qi-shared.ts        # Расчёты Ци
│   │   │   ├── fatigue-system.ts   # Усталость
│   │   │   ├── time-system.ts      # Система времени
│   │   │   └── ...
│   │   ├── llm/              # LLM интеграция
│   │   └── db.ts             # Prisma клиент
│   │
│   ├── services/             # Сервисный слой
│   ├── stores/               # Zustand хранилища
│   │   └── game.store.ts     # Глобальное состояние
│   └── types/                # TypeScript типы
│
├── game/                     # Phaser 3 сцены
│   ├── config/               # Конфигурация игры
│   └── scenes/               # Сцены Phaser
│
├── prisma/
│   └── schema.prisma         # Схема БД
│
├── db/
│   └── custom.db             # SQLite база
│
├── docs/                     # Документация
├── .env                      # Переменные окружения
└── README.md
```

---

## 🛠️ Доступные скрипты

| Скрипт | Описание |
|--------|----------|
| `bun run dev` | Запуск сервера разработки (порт 3000) |
| `bun run build` | Сборка для продакшена |
| `bun start` | Запуск продакшен сервера |
| `bun run lint` | Проверка ESLint |
| `bun run db:push` | Применить схему Prisma к БД |
| `bun run db:generate` | Генерация Prisma клиента |

---

## ⚙️ Конфигурация

### Переменные окружения (.env)

```env
# База данных
DATABASE_URL="file:./db/custom.db"

# LLM (опционально)
LLM_PROVIDER="z-ai"
```

### Настройка LLM

Проект поддерживает провайдеры:
1. **z-ai** (по умолчанию) — через SDK
2. **local** — Ollama
3. **api** — кастомный API

---

## 🎮 Управление в игре

| Клавиша | Действие |
|---------|----------|
| `W` / `↑` | Движение вверх |
| `S` / `↓` | Движение вниз |
| `A` / `←` | Движение влево |
| `D` / `→` | Движение вправо |

### UI элементы

| Кнопка | Описание |
|--------|----------|
| 📊 Статус | Полный статус персонажа (4 вкладки) |
| ⏸️ Отдых/Медитация | Единый диалог с 3 вкладками |
| ⚔️ Техники | Просмотр и использование техник |
| 🎒 Инвентарь | В разработке |
| 🗺️ Карта | В разработке |

---

## 🗄️ База данных

### Схема (версия 6)

- **Character** — персонажи с культивацией
- **CharacterTechnique** — изученные техники с мастерством
- **Technique** — шаблоны техник
- **Location** — локации (3D координаты)
- **NPC** — неигровые персонажи
- **Sect** — секты
- **GameSession** — игровые сессии
- **InventoryItem** — предметы инвентаря
- **SystemLog** — системные логи

### Сброс БД

```bash
rm -f db/custom.db
bun run db:push
```

---

## 🐛 Устранение неполадок

### Ошибка: "Cannot find module"

```bash
rm -rf node_modules
bun install
```

### Ошибка: "Database is locked"

```bash
pkill -f "next"
bun run db:push
bun run dev
```

### Порт 3000 занят

```bash
lsof -i :3000
kill -9 <PID>
```

### Phaser не загружается

- Проверьте консоль браузера
- Phaser загружается динамически (требуется интернет для первого запуска)

---

## 🤖 Информация для ИИ агента

> **Важно:** Эта секция содержит критическую информацию для автоматического исправления ошибок окружения.

### Типичные ошибки при запуске и их решения

#### 1. Ошибка: "Cannot find module '/scripts/init-env.js'"

**Причина:** Отсутствует скрипт инициализации окружения.

**Решение:** Создать файл `scripts/init-env.js`:

```javascript
// scripts/init-env.js
const fs = require('fs');
const path = require('path');

const envPath = path.join(process.cwd(), '.env');
const envContent = 'DATABASE_URL=file:/home/z/my-project/db/custom.db\n';

if (!fs.existsSync(envPath)) {
  fs.writeFileSync(envPath, envContent);
  console.log('[init-env] Created .env file');
} else {
  console.log('[init-env] .env already exists');
}
```

**Команда создания:**
```bash
mkdir -p scripts
cat > scripts/init-env.js << 'EOF'
const fs = require('fs');
const path = require('path');
const envPath = path.join(process.cwd(), '.env');
const envContent = 'DATABASE_URL=file:/home/z/my-project/db/custom.db\n';
if (!fs.existsSync(envPath)) { fs.writeFileSync(envPath, envContent); console.log('[init-env] Created .env file'); } else { console.log('[init-env] .env already exists'); }
EOF
```

---

#### 2. Ошибка: "Module not found: Can't resolve 'radix-ui'"

**Причина:** Не установлены или устарели зависимости.

**Решение:**
```bash
bun install
```

Если не помогло:
```bash
rm -rf node_modules bun.lock
bun install
```

---

#### 3. Ошибка: "'sendEvent' is not exported from '@/lib/game/event-bus/client'"

**Причина:** Изменён API модуля EventBusClient.

**Решение:** Использовать `eventBusClient.sendEvent()` вместо `sendEvent()`:

```typescript
// НЕПРАВИЛЬНО:
import { sendEvent } from '@/lib/game/event-bus/client';
const result = await sendEvent(data);

// ПРАВИЛЬНО:
import { eventBusClient } from '@/lib/game/event-bus/client';

// Инициализация (вызвать один раз при старте сессии)
eventBusClient.initialize(sessionId, characterId);

// Отправка события
const result = await eventBusClient.sendEvent('event:type', eventData);
```

---

#### 4. Dev сервер не запускается / порт 3000 не доступен

**Причина:** Сервер остановился или процесс "зомби".

**Решение - полный перезапуск:**
```bash
# 1. Убить старые процессы
pkill -f "next dev"
pkill -f "bun run dev"
pkill -f "node.*next"

# 2. Очистить кеш Next.js
rm -rf .next
rm -rf node_modules/.cache

# 3. Убедиться что .env существует
echo 'DATABASE_URL=file:/home/z/my-project/db/custom.db' > .env

# 4. Запустить dev сервер
bun run dev
```

---

#### 5. Ошибка: "Prisma Client could not be generated"

**Причина:** Не сгенерирован Prisma клиент.

**Решение:**
```bash
bun run db:generate
bun run db:push
```

---

#### 6. Ошибка базы данных: "Schema version mismatch"

**Причина:** Версия схемы в `migrations.ts` не совпадает с `schema.prisma`.

**Решение:** Проверить и обновить `SCHEMA_VERSION` в `src/lib/migrations.ts`:

```typescript
// Текущая версия схемы - должна совпадать с prisma/schema.prisma
export const SCHEMA_VERSION = 8;  // Обновить если не совпадает
```

Версии схемы:
- v1-v5: Базовые модели
- v6: Building, WorldObject для карты
- v7: bodyState (Kenshi-style повреждения)
- v8: Equipment, SpiritStorage (инвентарь)

---

#### 7. Чёрный экран с буквой "Z" вместо игры

**Причина:** Dev сервер не запущен, gateway показывает fallback страницу.

**Решение:**
```bash
# Проверить статус dev сервера
netstat -tlnp | grep 3000
# или
ss -tlnp | grep 3000

# Если порт не прослушивается - запустить сервер
bun run dev

# Дождаться сообщения "✓ Ready in XXXms"
```

---

#### 7a. Dev сервер падает сразу после запуска (контейнерная среда)

**Симптомы:**
- `bun run dev &` — процесс умирает через несколько секунд
- `nohup bun run dev &` — тоже умирает
- `ps aux` показывает процесс, но через секунду его нет
- Preview Panel показывает "Z" на чёрном фоне

**Причина:**

В контейнерной среде (sandbox, Docker, CI/CD) нет постоянного терминала.

```
┌─────────────────────────────────────────────────────────────┐
│                    КОНТЕЙНЕРНАЯ СРЕДА                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  bun run dev                                                │
│      │                                                      │
│      └─► node next dev (дочерний процесс)                   │
│               │                                             │
│               └─► next-server (внучатый процесс)            │
│                                                             │
│  Когда shell завершается → SIGHUP → убивает всю цепочку    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Обычные методы НЕ работают:**
```bash
# ❌ НЕ работает - процесс умирает при завершении shell
bun run dev &

# ❌ НЕ работает - nohup не всегда работает с bun
nohup bun run dev &

# ❌ НЕ работает - создаёт сессию, но связь сохраняется
setsid bun run dev
```

**Решение:**
```bash
# ✅ РАБОТАЕТ - полностью отсоединяет процесс
setsid -f bun run dev > dev.log 2>&1
```

**Объяснение флага `-f`:**
- `setsid` — создаёт новую сессию
- `-f` (fork) — форкает процесс и отсоединяет его **полностью** от родительской сессии
- Процесс становится независимым демоном

**Проверка:**
```bash
# Запустить
setsid -f bun run dev > dev.log 2>&1

# Подождать 5 секунд
sleep 5

# Проверить процесс
ps aux | grep next-server | grep -v grep

# Должен показать:
# z  1234  ...  next-server (v16.1.3)

# Проверить лог
tail -20 dev.log
```

**Альтернативные решения:**

```bash
# Вариант 1: Запуск в screen/tmux (если установлены)
screen -dmS dev bun run dev
tmux new-session -d -s dev 'bun run dev'

# Вариант 2: Systemd service (для продакшена)
# Создать /etc/systemd/system/cultivation-game.service

# Вариант 3: Docker с CMD (для контейнеров)
# CMD ["bun", "run", "dev"]
```

**Для автоматизации** (добавить в скрипт):
```bash
#!/bin/bash
# start-dev.sh

cd /home/z/my-project

# Убить старые процессы
pkill -f "next dev" 2>/dev/null

# Очистить кеш если нужно
# rm -rf .next

# Запустить в фоне с полным отсоединением
setsid -f bun run dev > dev.log 2>&1

echo "Dev server starting... Check dev.log for status"
```

---

#### 8. Ошибка: "Attempted import error: 'X' is not exported"

**Причина:** Несоответствие импорта и экспорта модуля.

**Решение:** Проверить файл модуля и использовать правильный импорт:

```typescript
// Проверить что экспортируется в модуле
export const foo = ...;        // Именованный экспорт
export default bar;            // Дефолтный экспорт

// Соответствующие импорты
import { foo } from './module';  // Именованный
import bar from './module';      // Дефолтный
```

---

### Чек-лист для ИИ агента при проблемах с запуском

```
□ Проверить наличие .env файла
□ Проверить наличие scripts/init-env.js
□ Проверить установлены ли зависимости (node_modules/)
□ Проверить сгенерирован ли Prisma клиент
□ Проверить слушается ли порт 3000
□ Проверить логи dev.log на наличие ошибок
□ Проверить соответствие SCHEMA_VERSION в migrations.ts и schema.prisma
□ Проверить правильность импортов в TypeScript файлах
□ В контейнерной среде использовать: setsid -f bun run dev
```

### Команды для диагностики

```bash
# Статус порта
netstat -tlnp | grep 3000

# Последние логи
tail -50 dev.log

# Проверка TypeScript ошибок
bun run lint 2>&1 | head -50

# Проверка структуры проекта
ls -la src/app/api/game/start/
ls -la scripts/
ls -la db/
```

### Полный сброс и перезапуск

```bash
# Остановить всё
pkill -f "next dev"
pkill -f "bun run dev"

# Очистить
rm -rf .next node_modules/.cache

# Создать .env
echo 'DATABASE_URL=file:/home/z/my-project/db/custom.db' > .env

# Создать scripts/init-env.js если отсутствует
mkdir -p scripts
cat > scripts/init-env.js << 'EOF'
const fs = require('fs');
const path = require('path');
const envPath = path.join(process.cwd(), '.env');
const envContent = 'DATABASE_URL=file:/home/z/my-project/db/custom.db\n';
if (!fs.existsSync(envPath)) { fs.writeFileSync(envPath, envContent); console.log('[init-env] Created .env file'); } else { console.log('[init-env] .env already exists'); }
EOF

# Установить зависимости если нужно
bun install

# Синхронизировать БД
bun run db:push

# Запустить
bun run dev
```

---

## 📱 Режимы запуска

### Разработка

```bash
bun run dev
```
- Горячая перезагрузка
- Подробные ошибки
- DevTools

### Продакшен

```bash
bun run build
bun start
```
- Оптимизированная сборка
- Минимум логов

---

## 🔄 Обновление проекта

```bash
git fetch origin
git pull origin main2D
bun install
bun run db:push
```

---

## 📞 Поддержка

При проблемах:

1. Проверьте версию Bun: `bun --version`
2. Проверьте .env файл
3. Проверьте логи в консоли
4. Сбросьте БД и попробуйте снова

---

*Документация актуальна на 2024-03-24. Версия схемы БД: 8*
