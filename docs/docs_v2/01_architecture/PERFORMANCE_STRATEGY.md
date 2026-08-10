# Стратегия производительности

> **Раздел:** 01_architecture
> **Статус:** Принятая стратегия.
> **Связанные документы:** `00_overview/TECHNOLOGY_DECISIONS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`.

---

## 0. Принципы

1. **Zero GC per frame** — все hot-path аллокации исключены. Сообщения между системами — `readonly struct` через шину событий. Никаких LINQ/lambda-captures в hot loops.
2. **Tick-based simulation, decoupled от frame rate.** Sim работает с фиксированным tick rate, рендер — независимо.
3. **Object pooling** для всех часто-создаваемых объектов: спрайты эффектов, projectiles, NPC entities, UI elements.
4. **Batch processing** — Qi-regen каждые 10 тиков, автосохранение каждые 60 тиков, AI по 3-уровневой каденции (Spinal 1–10 мс / Neural 10–50 мс / Brain 100–500 мс).
5. **C# для hot paths, GDScript/scene-glue — опционально.** Combat/AI/sim — C#. UI/scene-логика — на усмотрение.
6. **Многопоточность** — sim-расчёты (AI, pathfinding, Qi-regen) выносятся на worker threads; основной поток — рендер + ввод.

---

## 1. Performance budgets

### 1.1. CPU @ 100 NPC (на один тик)

| Операция | Модуль | Сложность | При 100 NPC |
|----------|--------|-----------|-------------|
| AI-тик | NPCAIService | O(N) | ~2 мс |
| Qi-реген | QiRegenCalculator | O(N) | ~0.5 мс |
| Buff-тик | BuffTickProcessor | O(N × B) | ~1 мс |
| Движение NPC | NPCMovementService | O(N) | ~1 мс |
| Обновление тайлов | TileMapService | O(changed) | ~0.1 мс |
| Pathfinding (A*) | TileMapService | O(n log n) | ~5–50 мс |
| Проверка столкновений | Physics2D | O(N²) worst | ~0.5 мс |
| Сериализация | SaveService | O(N) | ~5 мс |

### 1.2. CPU: шина событий — накладные расходы

| Событие | Частота | Подписчики | Накладные расходы |
|---------|---------|------------|-------------------|
| QiChangedEvent | При каждом изменении Ци | NPCService (кэш), UI | ~0.01 мс/event |
| TimeHourChanged | Раз в игровой час | WorldService, NPCService | ~0.01 мс/event |
| NPCAIStateChanged | При смене состояния | UI | ~0.01 мс/event |
| DamageDealtEvent | При каждом ударе | UI, Achievement (TBD) | ~0.02 мс/event |

> **Вывод:** Шина с `readonly struct` контрактами — минимальные накладные расходы. Zero GC allocation. Для 100 NPC с 10 событиями/сек = ~0.1 мс/сек — пренебрежимо мало.

### 1.3. GPU: рендеринг

| Параметр | Значение |
|----------|----------|
| Спрайты на экран | ~1000–5000 |
| Draw calls | ~50–200 |
| Видеопамять | ~100–500 MB |
| Sorting layers | 6 (Default, Background, Terrain, Objects, Player, UI) |

### 1.4. Memory: тайловые данные

| Тип локации | Тайлов | Несжатый | RLE | Sparse (10% заполн.) |
|-------------|--------|----------|-----|----------------------|
| Хутор (100×100 м) | 2 500 | 77 KB | ~10 KB | ~8 KB |
| Деревня (300×300 м) | 22 500 | 697 KB | ~80 KB | ~70 KB |
| Посёлок (500×500 м) | 62 500 | 1.9 MB | ~200 KB | ~195 KB |
| Средний город (1×1 км) | 250 000 | 7.7 MB | ~1.5 MB | ~800 KB |
| Большой город (3×3 км) | 2 250 000 | 69 MB | ~15 MB | ~7 MB |
| **Мегаполис (10×10 км)** | **25 000 000** | **775 MB** | **~150 MB** | **~77 MB** |
| Храм (200×200 м) | 10 000 | 310 KB | ~40 KB | ~31 KB |
| Подземелье | до 100 000 | 3.1 MB | ~500 KB | ~310 KB |

### 1.5. Memory: NPC

```
NPCState (один экземпляр):
├── Идентификация: ~200 bytes (strings, enums)
├── SoulData: ~100 bytes (long CoreCapacity, float Conductivity, etc.)
├── BodyParts: 11 × BodyPart ≈ 11 × 64 bytes = 704 bytes
├── EquipmentIds: 7 × KeyValuePair ≈ 200 bytes
├── TechniqueIds: 1-4 × string ≈ 200 bytes
├── InventorySlots: 2-5 × InventorySlot ≈ 300 bytes
├── Threats: Dictionary (обычно пустой) ≈ 50 bytes
├── Прочее: ~200 bytes
└── Итого: ~1.9 KB на NPC
```

| Локация | NPC | NPCState всего | Per-entity провайдеры |
|---------|-----|----------------|----------------------|
| Хутор | 5–10 | 10–19 KB | +2–4 KB (Body+Qi+Equip) |
| Деревня | 20–50 | 38–95 KB | +8–20 KB |
| Посёлок | 50–100 | 95–190 KB | +20–40 KB |
| Средний город | 100–300 | 190–570 KB | +40–120 KB |
| Большой город | 300–500 | 570–950 KB | +120–200 KB |
| Мегаполис | 500–2000 | 950 KB–3.8 MB | +200–800 KB |

### 1.6. Default MaxActiveNPCs

- **Default:** 100 NPC одновременно.
- **Мегаполис (с chunking):** до 2000 NPC.

---

## 2. Hardware tiers

| Уровень | CPU | RAM | GPU |
|---------|-----|-----|-----|
| **Minimum** | 4 cores, 2.5 GHz | 8 GB | GTX 1050 |
| **Recommended** | 6+ cores, 3.0 GHz | 16 GB | GTX 1660 |
| **Megapolis (2000 NPC)** | 8+ cores, 3.5 GHz | 32 GB | RTX 3060 |

### 2.1. Какие сценарии потянет

| Сценарий | Ответ | Примечание |
|----------|-------|------------|
| Малые локации (до 1 км) | ✅ Да | Любой современный ПК |
| Средние локации (1–3 км) | ✅ Да | С чанковой загрузкой |
| Большие локации (3–10 км) | ✅ Да | Требует оптимизаций |
| Мегаполис 10×10 км | ⚠️ С ограничениями | Только с чанковой загрузкой |

---

## 3. Архитектурные принципы производительности

### 3.1. Zero GC per frame

**Цель:** Никаких аллокаций GC в горячих циклах.

**Реализация:**
- Все сообщения между системами — `readonly struct` (zero allocation при передаче).
- Никаких LINQ в hot loops (`Where`, `Select`, `OrderBy` создают аллокации).
- Никаких lambda-captures в hot loops (создают closure-объекты).
- Пулы для всех часто-создаваемых объектов (см. §3.3).
- Pre-allocated массивы для batch-обработки.

**Контракт:**
```csharp
// ✅ Правильно: readonly struct, no allocations
public readonly struct QiChangedEvent
{
    public readonly long CurrentQi;
    public readonly long MaxQi;
    public QiChangedEvent(long current, long max) { ... }
}

// ❌ Плохо: class with allocations
public class QiChangedEventArgs : EventArgs { ... }
```

### 3.2. Tick-based simulation

**Цель:** Симуляция отвязана от frame rate.

**Реализация:**
- Фиксированный tick rate (60 Гц по умолчанию, configurable).
- 1 тик = 1 минута игрового времени.
- 4 скорости: Paused(0), Normal(1 тик/сек), Fast(5), VeryFast(15).
- Tick-батчинг: не все системы обновляются каждый тик.

**Tick-батчинг по системам:**

| Система | Период |
|---------|--------|
| Qi-регенерация | Каждые 10 тиков |
| Автосохранение | Каждые 60 тиков (плюс по триггерам) |
| Spinal AI | Каждый тик (1–10 мс) |
| Neural Router | Каждые ~3 тика (10–50 мс) |
| Brain Controller | Каждые ~10 тиков (100–500 мс) |
| Buff-тик | Каждый тик (но O(N×B) — дёшево) |
| Body-регенерация | Каждый тик |

### 3.3. Object pooling

**Цель:** Переиспользование объектов вместо создания/удаления.

**Что пулируется:**
- Спрайты эффектов (VFX).
- Projectiles (снаряды техник).
- NPC entities (переиспользование NPCState при смерти/респавне).
- UI elements (панели, тосты).
- Временные массивы для batch-обработки.

**Паттерн:**
```
public class ObjectPool<T> where T : new()
{
    private readonly Stack<T> _pool = new();
    public T Rent() => _pool.Count > 0 ? _pool.Pop() : new T();
    public void Return(T item) { _pool.Push(item); }
}
```

### 3.4. Per-entity DataProvider Pattern

**Цель:** O(1) доступ к данным сущности без межмодульной зависимости.

**Реализация:**
- `QiDataProvider` — кэш Qi-состояний по EntityId.
- `BodyDataProvider` — кэш BodyParts по EntityId.
- `EquipmentDataProvider` — кэш Equipment по EntityId.

```
+ Преимущества:
  - Модули не знают друг о друге (Hub-and-Spoke).
  - O(1) доступ к данным.
  - Кэш обновляется через события шины.
- Затраты:
  - Дополнительная память: ~2-4 KB на NPC (3 провайдера).
  - Синхронизация кэша: ~0.05 ms на NPC при обновлении.
```

### 3.5. Чанковая загрузка локаций

**Цель:** Не держать всю мегаполис-локацию в памяти.

**Расчёт:**
```
Локация 10×10 км (25M тайлов):
Чанк = 200×200 м = 100×100 тайлов = 10 000 тайлов
Чанков в локации: 50×50 = 2 500 чанков

Радиус загрузки = 3 чанка (600×600 м):
Загружено чанков: 7×7 = 49 чанков
Загружено тайлов: 49 × 10 000 = 490 000 тайлов
Память: ~15 MB (сжатый) вместо 150 MB (полная локация)
```

### 3.6. Сжатие данных тайлов

| Метод | Коэффициент | Когда использовать |
|-------|-------------|-------------------|
| RLE | 10–20× | Дикая местность (большие области одинаковых тайлов) |
| Sparse Array | 3–10× | Города с пустыми зонами |
| Чанковая загрузка | 50× | Локации >1 км |
| LOD для тайлов | 2–5× | Дальние тайлы (только тип) |

### 3.7. AI-оптимизация

| Оптимизация | Описание | Экономия |
|-------------|----------|----------|
| AI-тик по расписанию | Не все NPC обновляются каждый кадр | 50–80% CPU |
| Радиус активации | NPC далеко от игрока не обновляют AI | 60–90% CPU |
| Кэширование решений | NPC повторяет решение N тиков | 30–50% CPU |
| Пул объектов | Переиспользование NPCState при смерти/респавне | GC reduction |

---

## 4. Многопоточность

### 4.1. Принцип

Sim-расчёты выносятся на worker threads. Основной поток — рендер + ввод.

### 4.2. Что выносится на worker threads

| Операция | Поток | Примечание |
|----------|-------|------------|
| AI-тик (Brain Controller) | Worker | 100–500 мс на NPC — параллелится хорошо |
| Pathfinding (A*) | Worker | До 50 мс на 100 NPC — критично |
| Qi-regen (batch) | Worker | 0.5 мс на 100 NPC — низкий приоритет |
| Save serialisation | Worker | 5 мс на 100 NPC — не блокирует UI |
| Chunk loading | Worker | I/O-bound |

### 4.3. Что ОСТАЁТСЯ на основном потоке

- Spinal AI (1–10 мс — слишком быстро для переключения потоков).
- Шина событий (publish/subscribe).
- Ввод.
- Рендеринг.
- UI обновление.

### 4.4. Синхронизация

- Изменение узлов сцены из worker thread — через `CallDeferred` / очередь задач на основном потоке.
- Доступ к разделяемым данным — через `lock` или lock-free структуры (ConcurrentQueue, ConcurrentDictionary).
- CancellationTokens для отмены долгих операций.

### 4.5. ThreadPool

Использовать системный `ThreadPool` или встроенный движковый `WorkerThreadPool`. Не создавать потоки вручную (кроме специализированных long-running workers).

---

## 5. C# hot paths

### 5.1. Принцип

Вся игровая логика (16 модулей) — на C#. Это позволяет:
- Точную типизацию, статический анализ.
- Zero-GC через `readonly struct`.
- Многопоточность через `Task`/`ThreadPool`.
- Тестирование через `dotnet test`.

### 5.2. C# для hot paths

| Слой | Язык | Обоснование |
|------|------|-------------|
| Game logic core (16 модулей) | **C#** | Производительность, типизация, порт существующего кода |
| Hot paths (combat, AI, sim) | **C#** | Zero-GC, multithreading |
| Scene glue / signals | C# (или GDScript — на усмотрение) | Гибкость |
| UI logic | C# | Единообразие с core |
| Shaders | Движковый shader language | Нативно |
| Tooling / Editor scripts | C# (или GDScript) | Простой tooling |

### 5.3. Запрещённые паттерны в C# hot paths

```
// ❌ LINQ в hot loops — создаёт аллокации
foreach (var npc in npcs.Where(n => n.IsAlive).OrderBy(n => n.Position.X)) { ... }

// ❌ Lambda-captures — создают closure-объекты
npcs.ForEach(n => Process(n, someLocalVar));

// ❌ Box/unbox — аллокации
List<object> list = new() { 1, 2, 3 };  // int → object (box)

// ❌ string concatenation в hot loop — аллокации
string s = "";
for (int i = 0; i < 100; i++) s += i.ToString();

// ✅ Pre-allocated массивы
for (int i = 0; i < npcs.Length; i++) {
    if (npcs[i].IsAlive) Process(npcs[i]);
}

// ✅ StringBuilder для накопления строк
var sb = new StringBuilder(256);
for (int i = 0; i < 100; i++) sb.Append(i);
```

### 5.4. long vs float для Qi

**Решение:** Все Qi-значения — `long` (не `float`).

**Причина:**
- L8 ~789,750 Ци, L9 ~524,390,400 Ци — точность `float` (7 significant digits) потеряла бы детали.
- `long` имеет 18–19 значимых цифр — покрывает любые будущие значения.
- Целочисленная арифметика быстрее на modern CPU.

**Исключение:** Урон, проценты, множители — `float` или `double` (precision не критична).

---

## 6. Memory budgets

### 6.1. Общий бюджет памяти на сценарий

| Сценарий | RAM | Из чего |
|----------|-----|---------|
| Малая локация (хутор, 5–10 NPC) | <500 MB | Ядро + UI + tile data (RLE) + NPC states |
| Средняя локация (посёлок, 50–100 NPC) | 1–2 GB | + chunked tiles + per-entity providers |
| Большая локация (город, 300–500 NPC) | 3–5 GB | + active chunks + UI overlays |
| Мегаполис (2000 NPC) | 8–16 GB | + max chunks + max NPC + effects pool |

### 6.2. Категории памяти

| Категория | Размер (типичный) | Примеры |
|-----------|-------------------|---------|
| Game logic state | 100–500 MB | NPCState, BodyParts, Qi states, Inventory |
| Tile data | 100 MB – 1 GB | Loaded chunks (RLE/sparse) |
| Asset cache | 200–500 MB | Sprite atlas, animations, tilesets |
| UI state | 50–200 MB | Open panels, toast queue, theme resources |
| Save/scratch | 10–50 MB | Working JSON, aggregators |
| Renderer internal | 100–500 MB | GPU vertex/index buffers, textures |

### 6.3. GPU budgets

| Параметр | Min | Rec | Megapolis |
|----------|-----|-----|-----------|
| Спрайты на экран | 500 | 1000–5000 | 5000+ |
| Draw calls | 30 | 50–200 | 200+ |
| Видеопамять | 100 MB | 100–500 MB | 500 MB+ |
| Texture atlases | 1 | 2–3 | 4+ |

---

## 7. CPU budgets — подробный разбор

### 7.1. Бюджет тика (16 мс при 60 Гц)

| Операция | Время (100 NPC) | % от 16 мс |
|----------|-----------------|------------|
| Spinal AI | 2 мс | 12% |
| Qi-regen | 0.5 мс | 3% |
| Buff-тик | 1 мс | 6% |
| NPC движение | 1 мс | 6% |
| Tile обновление | 0.1 мс | 1% |
| Event bus | 0.1 мс | 1% |
| UI обновление | 1–2 мс | 6–12% |
| Save (если сработал) | 5 мс | 31% |
| Renderer prep | 1–2 мс | 6–12% |
| **Итого** | ~11–14 мс | ~70–88% |

> Pathfinding (A*) — переменный: 5–50 мс. Выносится на worker thread.

### 7.2. Что делать, если упёрлись в бюджет

1. **AI по расписанию:** не каждый тик, а каждые 3–10 тиков для далёких NPC.
2. **Радиус активации:** NPC вне радиуса 600 м не обновляются.
3. **Кэширование решений:** NPC повторяет решение N тиков.
4. **Пул объектов:** переиспользование NPCState, projectiles, VFX.
5. **Multi-thread:** вынос AI и pathfinding на worker threads.
6. **Уменьшить MaxActiveNPCs** с 100 → 50 для low-end.
7. **Native extension:** крайняя мера — критичные расчёты на C++/Rust через движковое расширение.

---

## 8. Скриншот/визуальные тесты

### 8.1. Принцип

AI-агент не видит рендер. Чтобы проверить визуальное состояние:
1. Рендеринг сцены в headless-режиме.
2. Сравнение с эталонным скриншотом.
3. Диф — если расхождение больше порога → баг.

### 8.2. Что тестируется

- Tile rendering (правильные тайлы на правильных позициях).
- UI layout (панели, кнопки, текст).
- Sprite composition (player, NPC, формации).
- Lighting (2D-свет не делает спрайты чёрными).
- Sorting layers (правильный z-order).

> Подробно — в `09_workflow/AI_DEVELOPMENT_WORKFLOW.md`.

---

## 9. Мониторинг в рантайме

### 9.1. Что логировать

| Категория | Что | Уровень |
|-----------|-----|---------|
| System | Boot, scene assembly, save/load | Info |
| Combat | Damage applied, kills, technique use | Debug (можно выключить) |
| Qi | Level changes, breakthroughs | Info |
| Body | Severed parts, critical | Warning |
| Performance | Frame time, GC allocs, memory | Info (в debug) |
| Save | Triggers, file size | Info |
| Errors | Exceptions, failed asserts | Error |

### 9.2. Performance counters

- Frame time (min/avg/max за последние 60 кадров).
- GC allocs per frame (должно быть 0 в hot path).
- Active NPC count.
- Loaded chunks count.
- Event bus: events/sec.
- Pool stats: rented/returned/created.

### 9.3. Профайлинг

- Встроенный профайлер движка.
- Custom performance counters через `EventBus` (например, `PerformanceReportEvent` раз в секунду).
- `dotnet-counters` для .NET-метрик.

---

## 10. Связанные документы

| Документ | Описание |
|----------|----------|
| `00_overview/TECHNOLOGY_DECISIONS.md` | Выбор движка и общая стратегия производительности |
| `ARCHITECTURE.md` | Архитектура (Hub-and-Spoke, ModuleServices) |
| `MODULE_STRUCTURE.md` | 16 модулей, tick-участие |
| `DI_AND_EVENTBUS.md` | readonly struct контракты (zero GC) |
| `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` | Headless-тестирование, скриншот-тесты |

---

## 11. Чеклист оптимизации (по приоритету)

1. **Чанковая загрузка** — обязательна для локаций >1 км.
2. **AI-тик по расписанию** — снижение CPU на 50–80%.
3. **Per-entity кэш** — O(1) доступ к данным.
4. **Сжатие RLE** — для дикой местности.
5. **Пул объектов** — для частых созданий/удалений NPC/projectiles/VFX.
6. **Tick-batching** — Qi-regen/Save/Brain AI не каждый тик.
7. **Zero GC проверки** — periodic profiling.
8. **Multi-threading** — AI, pathfinding, save на worker threads.
9. **Скриншот-тесты** — для регрессий рендеринга.

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источники: `docs_temp/COMPUTATIONAL_RESOURCES_CALCULATION.md` v2.0, `docs_temp/ENGINE_CHOICE_ANALYSIS.md` §3.4, `docs/ARCHITECTURE_CODE.md`.*
