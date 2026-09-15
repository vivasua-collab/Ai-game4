# Система сохранений

> **Статус:** Концепция (engine-agnostic).
> **Связанные документы:** `WORLD_SAVE_SYSTEM.md`, `DATA_MODELS.md`, `CONFIGURATIONS.md`, `03_world/TIME_SYSTEM.md`.
>
> ⚠️ **Статус реализации (актуализировано 2026-09-06, аудит):** save/load **ОТКЛЮЧЁН** —
> решение пользователя Q8 (2026-08-22): «отложить save/load, отключить stubs» (причина:
> сейвы ломали загрузку после каждого изменения структур). F5/F9 дают честный тост
> «Сейвы отключены (этап разработки)» (2026-09-06), автосейва нет, пункт «Load Game»
> в меню всегда fallback в NewGame.
> **Временная замена:** `StartingGearPhase` (фаза сборки сцены) — детерминированный
> стартовый набор с сидом 1000 (замена рандомного состояния стартовой генерацией).
> **Решение пользователя (2026-09-06):** стартовый набор — временное решение для
> тестов в процессе разработки, остаётся как есть и будет неоднократно меняться;
> синхронизация со START_LORE §5 (меч+роба vs кинжал+лук) отложена до стабилизации.
> Ниже — целевая концепция на момент реактивации. Регистрация ISaveable-модулей
> сохранена (агрегатор собирает 6 ISaveable через ResolveAll), латентная проблема
> сериализации: System.Text.Json без IncludeFields не сериализует public-поля DTO
> (см. SESSION_CONTEXT 2026-08-28 №2) — учесть при реактивации.

---

## 1. Обзор

Система сохранений управляет сохранением и загрузкой игрового состояния. Это **JSON-based** система с опциональным бинарным форматом и GZIP-сжатием, работающая поверх чанковой модели мира.

**Ключевые характеристики:**
- Формат: **JSON** (текстовый, читаемый, легко отлаживается). Опционально — бинарный формат (×3–4 сжатие) + GZIP (×2–3 сжатие).
- Паттерн: **ISaveable** (SaveKey / CaptureState / RestoreState).
- Агрегатор: **SaveDataAggregator** собирает данные от всех систем.
- Триггеры: автосохранение (каждые 30 игровых минут — `SaveConfig.AutoSaveIntervalMinutes`, R17), ручное, событийное.
- Структура: `main.sav` + `chunks/` + `locations/` + `metadata.sav`.

> Реализация сохранений — pure C#, без движко-специфичных зависимостей. Сериализатор — `System.Text.Json` (или эквивалентный). Чтение/запись файлов — стандартный `File IO`. Концепция инвариантна относительно движка.

---

## 2. Формат сохранения

### 2.1 JSON (основной формат)

```json
{
  "version": "1.0.0",
  "sessionId": "session_001",
  "timestamp": 1864_05_12_14_30,
  "tickCount": 1450,
  "player": { ... },
  "world": { ... },
  "npcs": [ ... ],
  "formations": [ ... ],
  "buffs": [ ... ],
  "charger": { ... },
  "tiles": { ... }
}
```

### 2.2 Бинарный формат (опционально)

- Используется для production-сборок.
- Экономия ×3–4 по размеру.
- Те же данные, другая сериализация.

### 2.3 GZIP-сжатие (опционально)

- Применяется поверх JSON или бинарного формата.
- Экономия ×2–3 по размеру.
- Декпрессия при загрузке.

### 2.4 Сводная таблица сжатия

| Оптимизация | Уменьшение | Итог (100 ч игры) |
|-------------|------------|--------------------|
| Без оптимизации (чистый JSON) | — | ~100 KB |
| Бинарный формат | ×3–4 | ~30 KB |
| GZIP-сжатие | ×2–3 | ~15 KB |
| Дельта-сохранение | ×2–5 | ~5–8 KB |

---

## 3. Структура сохранений

### 3.1 Файловая структура

```
Saves/
├── slot1/
│   ├── main.sav              # Игрок, время, квесты (~10–50 KB)
│   │
│   ├── chunks/               # Папка чанков
│   │   ├── chunk_152_847.sav # Посещённые чанки
│   │   └── ...
│   │
│   ├── locations/            # Папка локаций
│   │   ├── loc_city_tumanny.sav
│   │   ├── loc_village_tihaya.sav
│   │   └── ...
│   │
│   └── metadata.sav          # Индекс и карта мира
│
├── autosave/                 # Автосохранение
├── quicksave/                # Быстрое сохранение
└── backups/                  # Резервные копии (rolling)
```

### 3.2 Размеры файлов

| Файл | Размер | Содержимое |
|------|--------|------------|
| `main.sav` | 10–50 KB | Игрок, время, квесты, активные сессии |
| `metadata.sav` | 5–20 KB | Индекс чанков, карта мира, флаги видимости |
| `chunks/chunk_X_Y.sav` | 0.5–5 KB | Метаданные секторов, дельта локаций |
| `locations/loc_*.sav` | 0.5–10 KB | Состояние конкретной локации (объекты, NPC, контейнеры) |

### 3.3 Размер всей папки сохранения

| Время игры | Посещено локаций | Размер сохранения |
|------------|------------------|-------------------|
| 10 часов | 10–20 | ~10–30 KB |
| 100 часов | 50–100 | ~50–150 KB |
| 1000 часов | 300–500 | ~200–500 KB |
| Экстремум | 2000 | ~1–2 MB |

Подробнее о размерах и оптимизациях — в `WORLD_SAVE_SYSTEM.md`.

---

## 4. Паттерн ISaveable

### 4.1 Контракт

```csharp
public interface ISaveable
{
    /// <summary>Уникальный ключ сохранения для этой системы.</summary>
    string SaveKey { get; }

    /// <summary>Снимок состояния для сохранения.</summary>
    object CaptureState();

    /// <summary>Восстановление состояния из снимка.</summary>
    void RestoreState(object savedState);
}
```

### 4.2 Пример реализации

```csharp
public class QiManager : ISaveable
{
    public string SaveKey => "qi";

    public object CaptureState()
    {
        return new QiSaveData
        {
            CurrentQi = player.CurrentQi,        // long
            CoreCapacity = player.CoreCapacity,  // long
            AccumulatedQi = player.AccumulatedQi // long
        };
    }

    public void RestoreState(object savedState)
    {
        var data = (QiSaveData)savedState;
        player.CurrentQi = data.CurrentQi;
        player.CoreCapacity = data.CoreCapacity;
        player.AccumulatedQi = data.AccumulatedQi;
    }
}
```

### 4.3 SaveDataAggregator

**SaveDataAggregator** — оркестратор сохранения. Собирает данные от всех `ISaveable` систем и записывает их в один `main.sav` (или распределяет по чанкам/локациям).

```csharp
public class SaveDataAggregator
{
    private readonly List<ISaveable> _saveables;

    public void Register(ISaveable saveable) => _saveables.Add(saveable);

    public MainSaveData CaptureAll()
    {
        var data = new MainSaveData();
        foreach (var s in _saveables)
        {
            data.Set(s.SaveKey, s.CaptureState());
        }
        return data;
    }

    public void RestoreAll(MainSaveData data)
    {
        foreach (var s in _saveables)
        {
            if (data.TryGet(s.SaveKey, out var state))
                s.RestoreState(state);
        }
    }
}
```

### 4.4 Зарегистрированные системы (SaveKey → система)

**R17 «Полнота сейва» (2026-09-15): фактический реестр — 19 блоков.**
Таблица ниже — из дизайн-документа Unity-итерации (историческая);
фактические блоки (жирным — новые R17):

| SaveKey | Система | Описание |
|---------|---------|---------|
| **`"world"`** | **WorldService** | **Идентичность активной локации (E-3)** |
| **`"world_time"`** | **TimeService** | **Календарь + тики (WT-5)** |
| **`"item_db"`** | **ItemDatabaseService** | **Каталог предметов + счётчики ID генераторов (G-2/G-3)** |
| **`"player"`** | **PlayerService** | **Позиция/сон/стойка/флаг смерти (E-3)** |
| **`"stats"`** | **StatService** | **Базовые статы/виртуальная дельта/пороги** |
| `"body"` | BodyService | Части тела игрока (HP/состояния) |
| **`"qi"`** | **QiService** | **Уровень/подуровень/качество ядра/Ци/ампутация сердца (QI-2)** |
| `"inventory"` | InventoryService | Слоты рюкзака |
| **`"equipment"`** | **EquipmentService** | **Кукла игрока: слот → itemId (INV-4)** |
| **`"belt"`** | **BeltService** | **Слоты пояса 0-6 (INV-4)** |
| `"techniques"` | TechniqueService | Изученные техники |
| `"technique_slots"` | TechniqueSlotService | Назначения слотов 1-9 |
| `"formation"` | FormationService | Активная формация (+позиция — R17 F-2) |
| `"npc"` | NPCService | NPC (статы/экипировка/тела) |
| **`"animals"`** | **AnimalService** | **Звери: вид/позиция/HP-тел/враждебность (NPC-1)** |
| **`"quests"`** | **QuestService** | **Статусы/прогресс/награды/день (QST-1)** |
| **`"currency"`** | **CurrencyService** | **Баланс духовных камней (TRD-1)** |
| `"charger"` | ChargerService | Слоты камней/буфер/тепло/режим |
| `"save_meta"` | SaveService | Версия формата + время сейва |

Вне сейва (осознанно, V1): предметы на земле (GROUND_ITEM_SYSTEM §6 —
исчезают при перезагрузке; сбрасываются IWorldResettable при пересборке),
трупы (R13-домен — реестр на сессию), баффы (пересчёт из источников).

### 4.5 Порядок восстановления (RestoreOrder) — контракт R17

`SaveDataAggregator` восстанавливает блоки в ЯВНОМ порядке
(SAV-5: не в порядке регистраций DI — рефакторинг регистраций больше
не меняет семантику загрузки):

```
world → world_time → item_db → player → stats → body → qi →
inventory → equipment → belt → techniques → technique_slots →
formation → npc → animals → quests → currency → charger → save_meta
```

Ключевые зависимости:
- `item_db` ДО `equipment`/`belt`/`npc` — они резолвят itemId через каталог;
- `world`/`world_time` первыми — GameSession читает локацию/время после Load;
- блоки вне списка (будущие модули) — после, в порядке регистраций;
- отсутствующие в сейве ключи пропускаются (форвард-совместимость).

Плюс сброс world-scoped доменов ДО RestoreState (IWorldResettable —
см. §4.6): GameSession.LoadGame резолвит все домены и сбрасывает —
сейв остаётся единственным источником истины (warm-load без «призраков»
прошлого мира; паттерн R14-P2-1, расширенный с NPC-домена на все домены).

### 4.6 IWorldResettable — сброс world-scoped доменов (R17 E-1)

Домены, чьё состояние привязано к миру (не к процессу), реализуют
`Core.Interfaces.IWorldResettable.ResetWorld()` (идемпотентен):

- NewGame: `WorldDomainResetPhase` (фаза 0) — `ResolveAll<IWorldResettable>`;
- LoadGame: `GameSession.LoadGame` — тот же сброс ДО RestoreState.

Реализаторы (16): NPCModule (реестр/трупы/группы), AnimalService,
FormationService, ChargerService, QiService+QiModule, PlayerService,
StatService, QuestService, CurrencyService, InventoryService,
EquipmentService, BeltService, GroundItemService, WorldService,
TimeService. Процесс-scoped контент (ItemDatabaseService — каталог,
реестр локаций) НЕ сбрасывается.

---

## 5. Данные сохранения

### 5.1 Данные персонажа

- Характеристики (STR, AGI, INT, VIT) — `float`
- Уровень культивации — `int`
- Текущее и максимальное Ци — **`long`**
- Проводимость — `float`
- Состояние тела (части тела, HP) — JSON

### 5.2 Данные техник

- Изученные техники (список ID + mastery)
- Назначение в слоты
- Прогресс изучения

### 5.3 Данные инвентаря

- Все предметы с характеристиками
- Экипировка в слотах
- Духовное хранилище

### 5.4 Данные формаций и баффов

```csharp
class FormationSaveData
{
    string FormationId;
    string TechniqueId;
    string CoreId;
    int Level;
    string Stage;
    long CurrentQi;        // long
    long MaxCapacity;      // long
    int EffectRadius;
    int DrainPerHour;
    (int X, int Y) Position;
    List<string> Participants;
}

class BuffSaveData
{
    string BuffId;
    string SourceId;       // что наложило бафф
    int RemainingTicks;    // сколько тиков осталось
    int Stacks;
    float Power;
}
```

### 5.5 Данные зарядника

```csharp
class ChargerSaveData
{
    string ChargerId;
    List<ChargerSlot> Slots;     // вставленные камни Ци
    long CurrentHeat;             // long
    long MaxHeat;
    long BufferQi;                // long
    long BufferMax;
}
```

### 5.6 Данные тайлов (delta)

```csharp
class TileSaveData
{
    string LocationId;
    int Seed;                     // seed для процедурной генерации
    List<TileDelta> ModifiedTiles;
}

class TileDelta
{
    int X;
    int Y;
    Dictionary<string, object> Changes;  // только изменённые поля
}
```

> **Тайлы НЕ сохраняются индивидуально.** Сохраняется только seed + дельта изменений. Подробнее — в `WORLD_SAVE_SYSTEM.md`.

### 5.7 Данные NPC

```csharp
class NPCSaveData
{
    string NpcId;
    string PresetId;
    string Name;
    int CultivationLevel;
    long CurrentQi;               // long
    float Attitude;               // -100..+100
    PersonalityTrait Personality; // [Flags]
    (int X, int Y) Position;
    string FactionId;
    List<string> TechniqueIds;
    List<string> EquipmentIds;
    List<SkillLevelData> Skills;
}
```

### 5.8 Данные мира

- Текущее время (tickCount, gameTime)
- Текущая локация (locationId + entranceId)
- Состояние NPC в локации
- Выполненные квесты

---

## 6. Триггеры сохранения

### 6.1 Автосохранение

**R17 (SAV-2, 2026-09-15):** автосейв — ТОЛЬКО в активной игровой
сессии (`SessionState.Playing`): тик-луп загейчен в GameBoot (E-4),
плюс страховочный гейт в SaveModule.Tick — из главного меню мир не
симулирует и не автосейвит (раньше плодились слоты `autosave_NNNN` от
fallback-мира меню, без чистки).

| Параметр | Значение |
|---------|----------|
| Слот | Единый `autosave` (перезапись, не плодится) |
| Интервал | `SaveConfig.AutoSaveIntervalMinutes` = 30 игровых минут (1 тик = 1 игровая минута; ≤0 — выкл) |
| Гейт | `SessionState.Playing` (двойной: GameBoot-луп + SaveModule.Tick) |
| normal (1 тик/сек) | каждые 30 реальных секунд |
| fast (5 тик/сек) | каждые 6 реальных секунд |
| quick (15 тик/сек) | каждые 2 реальных секунды |

Событийные триггеры из таблицы ниже — план V2 (не реализованы):

| Событие | Действие (план) |
|---------|----------|
| Смена локации | Сохранить текущее состояние |
| Прорыв уровня | Сохранить персонажа |
| Завершение боя | Сохранить состояние боя |

### 6.2 Ручное сохранение

- **F5** — быстрое сохранение (quicksave).
- **F9** — быстрая загрузка (quickload).
- Меню → «Сохранить игру» → выбор слота.
- Меню → «Загрузить игру» → выбор слота.

### 6.3 Событийное сохранение

Критические события сохраняются немедленно (не дожидаясь каденции):
- прорыв уровня культивации;
- смерть NPC (особенно сюжетного);
- завершение квеста;
- получение уникального предмета;
- разрушение здания;
- активация Великой Формации.

### 6.4 Принудительное сохранение

- При паузе (опционально).
- При выходе из игры.
- При смене сцены / локации.

---

## 7. Жизненный цикл сохранения

### 7.1 Save flow

```
1. Триггер (60-й тик / смена локации / F5 / событие)
   │
   ▼
2. SaveDataAggregator.CaptureAll()
   ├── foreach (var s in _saveables) → s.CaptureState()
   ├── Сбор данных в MainSaveData
   └── Распределение по файлам:
       ├── main.sav — игрок, время, квесты, NPC сессии
       ├── chunks/chunk_X_Y.sav — дельта чанка (если изменён)
       ├── locations/loc_*.sav — дельта локации (если изменена)
       └── metadata.sav — индекс, карта мира, флаги
   │
   ▼
3. Сериализация (JSON / бинарный)
   │
   ▼
4. Сжатие (GZIP, опционально)
   │
   ▼
5. Запись на диск (атомарно: temp file → rename)
   │
   ▼
6. Backup (rolling, последние 3 сохранения)
```

### 7.2 Load flow

```
1. Чтение metadata.sav (карта мира, индекс чанков)
   │
   ▼
2. Чтение main.sav (игрок, время, квесты, NPC)
   │
   ▼
3. Десериализация (GZIP decompress → JSON parse → объекты)
   │
   ▼
4. SaveDataAggregator.RestoreAll(data)
   ├── foreach (var s in _saveables) → s.RestoreState(data.Get(s.SaveKey))
   └── Восстановление состояния каждой системы
   │
   ▼
5. Загрузка текущей локации (по locationId)
   ├── Чтение locations/loc_*.sav (дельта)
   ├── Применение дельты к процедурно сгенерированной карте (seed)
   └── Спавн NPC, объектов, точек интереса
   │
   ▼
6. Resume симуляции
```

---

## 8. Версионирование

### 8.1 Версия сохранения

Каждое сохранение содержит поле `version` (семантическое версионирование).

```json
{
  "version": "1.2.0",
  ...
}
```

### 8.2 Миграции

При загрузке сохранения старой версии — запуск миграций:

```csharp
public class SaveMigrator
{
    private readonly List<ISaveMigration> _migrations;

    public MainSaveData Migrate(MainSaveData data)
    {
        var version = data.Version;
        foreach (var m in _migrations.Where(m => m.FromVersion == version))
        {
            data = m.Migrate(data);
            version = m.ToVersion;
        }
        return data;
    }
}
```

### 8.3 Стратегия обратной совместимости

- Сохранения向后 совместимы (старые сохранения грузятся в новых версиях).
- При breaking change — версия мажорная, старые сохранения не грузятся (с предупреждением).
- Миграции — отдельные классы, теструемые.

---

## 9. Безопасность и восстановление

### 9.1 Атомарная запись

**R17 (SAV-1, 2026-09-15): РЕАЛИЗОВАНО** в обоих хендлерах
(SaveFileHandler Module + Adapter):

```
1. Сериализация в память (JSON)
2. Запись во временный файл (main.json.tmp)
3. File.Move(tmp → main.json, overwrite: true) — атомарная операция ОС
```

Гарантия: обрыв записи (краш/потеря питания) оставляет либо старый
слот целиком, либо новый целиком — усечённый main.json невозможен.
(fsync-этап опущен — V1-допущение, окно риска < секунды.)
Удаление слота (Delete) — тоже безопасное: try/catch с честным
результатом вместо IOException наружу (SAV-7).

### 9.2 Rolling backups

- Последние 3 сохранения каждого слота.
- Имена: `slot1.bak.1`, `slot1.bak.2`, `slot1.bak.3`.
- При сохранении: `bak.3 → удалить`, `bak.2 → bak.3`, `bak.1 → bak.2`, `main.sav → bak.1`, новый → `main.sav`.

### 9.3 Восстановление при повреждении

- При ошибке чтения `main.sav` — попытка загрузить `bak.1`.
- Если и `bak.1` повреждён — `bak.2`, затем `bak.3`.
- Если все повреждены — предложение начать новую игру или восстановить из cloud backup (если есть).

### 9.4 Cloud backup (опционально, будущее)

- Игрок полностью однопользовательская, но cloud backup — опциональная фича для синхронизации между устройствами.
- Не является требованием; локальные сохранения — основной механизм.

---

## 10. Принципы системы сохранений

1. **JSON-формат** (с опциональным бинарным + GZIP).
2. **ISaveable pattern** — каждая система реализует SaveKey/CaptureState/RestoreState.
3. **SaveDataAggregator** — оркестратор, не система знает, как себя сохранить.
4. **Автосохранение каждые 30 игровых минут** (`SaveConfig.AutoSaveIntervalMinutes`, слот `autosave`, гейт SessionState.Playing — R17), плюс событийные триггеры.
5. **F5 / F9** — ручные quicksave / quickload.
6. **Чанковая структура:** `main.sav` + `chunks/` + `locations/` + `metadata.sav`.
7. **Атомарная запись** + rolling backups для безопасности.
8. **Версионирование + миграции** для обратной совместимости.

---

## 11. Связанные документы

| Документ | Связь |
|----------|-------|
| `WORLD_SAVE_SYSTEM.md` | Чанковое сохранение мира, seed+delta для тайлов |
| `DATA_MODELS.md` | Структуры данных для сериализации |
| `CONFIGURATIONS.md` | Пресеты (НЕ сохраняются, загружаются из data resources) |
| `03_world/TIME_SYSTEM.md` | Тики/время (автосейв-интервал — см. §6.1) |
| `01_architecture/PERFORMANCE_STRATEGY.md` | Производительность сериализации |
