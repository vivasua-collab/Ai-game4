# 🎯 План развития проекта Cultivation World Simulator

**Дата создания:** 2026-03-30  
**Дата изменения:** 2026-07-14  
**Версия:** 1.3  
**Статус:** ⚠️ ARCHIVED — legacy-эра (до модульной пересборки 2026-05-08)

---

> **⚠️ ВНИМАНИЕ:** Этот документ описывает **legacy-план** (Фазы 1–8, GameManager, SceneLoader, JSON-контент).
> Всё описанное здесь **завершено и заморожено** в `UnityProject/Legacy/`.
>
> **Актуальный план развития:**
> - [README.md §Этапы разработки](../../README.md) — Modular Rebuild → UI V3 → Fix Plan A–D → Этапы 5–9 → Контент
> - [START_PROMPT.md §Статус кодовой базы](../../START_PROMPT.md) — текущий фокус
> - [checkpoints/06_17_history_reconstruction.md](../checkpoints/06_17_history_reconstruction.md) — история разработки
> - [checkpoints/06_17_ui_fix_and_test_plan_v1.md](../checkpoints/06_17_ui_fix_and_test_plan_v1.md) — план UI исправлений (Фаза A–D)

---

> **📖 Глоссарий:** [GLOSSARY.md](./GLOSSARY.md) — единый справочник терминологии проекта

## 📊 ТЕКУЩЕЕ СОСТОЯНИЕ

### ✅ Завершённые фазы

| Фаза | Название | Статус | Результат |
|------|----------|--------|-----------|
| 1 | Foundation | ✅ COMPLETE | Структура, Enums, Constants, SO модели, JSON |
| 2 | Combat Core | ✅ COMPLETE | Body System, Damage Pipeline, Qi Buffer, Techniques |
| 3 | NPC & Interaction | ✅ COMPLETE | NPC AI, Dialogue, Relationships, Factions |
| 4 | World & Time | ✅ COMPLETE | Time System, Locations, Events |
| 5 | UI Enhancement | ✅ COMPLETE | Inventory UI, Character Panel, HUD |
| 6 | Testing & Balance | ✅ COMPLETE | Unit Tests, Integration Tests, Balance Verification |

### ✅ Добавлено 2026-04-01

| Компонент | Файл | Описание |
|-----------|------|----------|
| MortalStageData генерация | AssetGenerator.cs | Добавлена генерация 6 этапов смертных |
| GameManager | Managers/GameManager.cs | Главный менеджер игры |
| Scene Setup Tools | Editor/SceneSetupTools.cs | Полуавтоматическая настройка сцены |
| SemiAuto документация | asset_setup/*_SemiAuto.md | Инструкции для полуавтоматического режима |
| Enums расширение | Core/Enums.cs | GameState, GamePhase |
| GameEvents | Core/GameEvents.cs | Централизованная система событий (50+ событий) |
| SceneLoader | Managers/SceneLoader.cs | Асинхронная загрузка сцен с прогрессом |
| GameInitializer | Managers/GameInitializer.cs | Инициализация всех систем |
| techniques.json | Data/JSON/ | 34 техники культивации |
| enemies.json | Data/JSON/ | 27 типов врагов |
| equipment.json | Data/JSON/ | 39 предметов экипировки |
| npc_presets.json | Data/JSON/ | 15 пресетов NPC |
| quests.json | Data/JSON/ | 15 квестов |

### 📁 Созданный код (80+ файлов)

```
Scripts/
├── Core/           (7 файлов)   — Constants, Enums, Settings, StatDevelopment, GameEvents
├── Body/           (3 файла)    — BodyController, BodyPart, BodyDamage
├── Combat/         (9 файлов)   — CombatManager, DamageCalculator, QiBuffer, DefenseProcessor
├── Qi/             (1 файл)     — QiController
├── Player/         (3 файла)    — PlayerController, SleepSystem, PlayerVisual
├── NPC/            (4 файла)    — NPCController, NPCAI, NPCData, RelationshipController
├── Inventory/      (4 файла)    — InventoryController, EquipmentController, Crafting
├── World/          (4 файла)    — WorldController, TimeController, LocationController
├── Generators/     (7 файлов)   — NPCGenerator, TechniqueGenerator, WeaponGenerator, etc.
├── Save/           (3 файла)    — SaveManager, SaveFileHandler, DataTypes
├── UI/             (8 файлов)   — HUDController, InventoryUI, CharacterPanel, CombatUI
├── Interaction/    (2 файла)    — InteractionController, DialogueSystem
├── Data/SO/        (7 файлов)   — CultivationLevelData, ElementData, TechniqueData, etc.
├── Tests/          (3 файла)    — CombatTests, IntegrationTests, BalanceVerification
├── Managers/       (4 файла)    — GameManager, SceneLoader, GameInitializer
├── Editor/         (2 файла)    — AssetGenerator, SceneSetupTools
└── Examples/       (1 файл)     — NPCAssemblyExample
```

### 📊 JSON контент (11 файлов)

```
Data/JSON/
├── techniques.json       (34 техники)
├── enemies.json          (27 врагов)
├── equipment.json        (39 предметов)
├── npc_presets.json      (15 пресетов)
├── quests.json           (15 квестов)
├── items.json            (расходники, материалы)
├── cultivation_levels.json
├── elements.json
├── grades.json
├── materials.json
└── technique_types.json
```

---

## 🚀 СЛЕДУЮЩИЕ ЭТАПЫ

### ЭТАП 1: Создание ScriptableObject Assets (Приоритет: КРИТИЧЕСКИЙ)

**Статус:** ✅ ИНСТРУМЕНТЫ ГОТОВЫ (ожидает выполнения в Unity Editor)

**Инструменты:**
- `Window → Asset Generator` — генерация CultivationLevelData, MortalStageData, ElementData
- `Window → Scene Setup Tools` — настройка сцены и Player

**Задачи:**
```
□ CultivationLevelData — 9 уровней культивации (99 с подуровнями). При переходе на 10 уровень — игра заканчивается (Вознесение).
□ MortalStageData — 6 этапов смертных (Asset Generator)
□ ElementData — 8 элементов (Asset Generator)
□ SpeciesData — базовые виды (вручную или расширить AssetGenerator)
```

**Время:** 30 минут — 1 час (с инструментами автоматизации)

---

### ЭТАП 2: Создание игровых сцен (Приоритет: ВЫСОКИЙ)

**Статус:** ⏳ Требует Unity Editor

**Инструменты:**
- `Window → Scene Setup Tools` — автоматическое создание структуры сцены
- Инструкции: `docs/asset_setup/04_BasicScene_SemiAuto.md`

**Задачи:**
```
□ MainMenu.unity
  ├── Canvas с кнопками
  ├── EventSystem
  
□ GameWorld.unity
  ├── GameManager + Systems (Scene Setup Tools)
  ├── Player (Scene Setup Tools)
  ├── GameUI Canvas + HUD (Scene Setup Tools)
  └── Ручная настройка: теги, слои, префабы
```

**Время:** 1-2 часа (с инструментами автоматизации)

---

### ЭТАП 3: Интеграция GameManager (Приоритет: ВЫСОКИЙ)

**Статус:** 🔄 В ПРОЦЕССЕ (можно делать без Unity)

**Что можно сделать без Unity:**
```
✅ GameManager.cs — уже создан
□ GameEvents.cs — централизованные события
□ SceneLoader.cs — управление загрузкой сцен
□ GameInitializer.cs — инициализация при старте
```

**Время:** 2-3 часа

---

### ЭТАП 3.1: GameEvents — Система событий (Приоритет: ВЫСОКИЙ)

**Статус:** ✅ ЗАВЕРШЕНО

**Задача:** Создать централизованную систему событий для связи между системами.

**Файл:** `Scripts/Core/GameEvents.cs`

**Структура:**
```csharp
// События игры
public static class GameEvents
{
    // Game State
    public static event Action OnGameStart;
    public static event Action OnGamePause;
    public static event Action OnGameResume;
    public static event Action OnGameQuit;
    
    // Player
    public static event Action<int, int> OnPlayerHealthChanged;
    public static event Action<long, long> OnPlayerQiChanged;
    public static event Action<int> OnPlayerLevelChanged;
    public static event Action OnPlayerDeath;
    
    // Combat
    public static event Action<string> OnCombatStart;
    public static event Action<bool> OnCombatEnd;
    public static event Action<int> OnDamageDealt;
    public static event Action<int> OnDamageTaken;
    
    // World
    public static event Action<string> OnLocationChanged;
    public static event Action<int> OnTimeChanged;
    public static event Action<string> OnEventTriggered;
    
    // NPC
    public static event Action<string> OnNPCInteract;
    public static event Action<string, int> OnRelationChanged;
}
```

**Время:** 1 час

---

### ЭТАП 3.2: SceneLoader — Загрузка сцен (Приоритет: ВЫСОКИЙ)

**Статус:** ✅ ЗАВЕРШЕНО

**Файл:** `Scripts/Managers/SceneLoader.cs`

**Функции:**
- Загрузка сцен с индикатором
- Асинхронная загрузка
- Переходы между сценами

**Время:** 1 час

---

### ЭТАП 5.1: JSON данные для контента (Приоритет: СРЕДНИЙ)

**Статус:** ✅ ЗАВЕРШЕНО

**Задача:** Подготовить JSON файлы с данными для генерации контента.

**Файлы:**
```
Assets/Data/JSON/
├── techniques.json       — ✅ 34 техники
├── items.json           — ✅ 30+ предметов
├── equipment.json       — ✅ 39 предметов (оружие и броня)
├── enemies.json         — ✅ 27 врагов
├── npc_presets.json     — ✅ 15 пресетов NPC
└── quests.json          — ✅ 15 квестов
```

**Время:** 3-4 часа

---

## 📋 ЧТО МОЖНО ДЕЛАТЬ СЕЙЧАС (без Unity)

| Задача | Файл | Статус |
|--------|------|--------|
| GameEvents.cs | Core/GameEvents.cs | ✅ ЗАВЕРШЕНО |
| SceneLoader.cs | Managers/SceneLoader.cs | ✅ ЗАВЕРШЕНО |
| GameInitializer.cs | Managers/GameInitializer.cs | ✅ ЗАВЕРШЕНО |
| techniques.json | Data/JSON/techniques.json | ✅ ЗАВЕРШЕНО |
| items.json | Data/JSON/items.json | ✅ ЗАВЕРШЕНО |
| enemies.json | Data/JSON/enemies.json | ✅ ЗАВЕРШЕНО |
| equipment.json | Data/JSON/equipment.json | ✅ ЗАВЕРШЕНО |
| npc_presets.json | Data/JSON/npc_presets.json | ✅ ЗАВЕРШЕНО |
| quests.json | Data/JSON/quests.json | ✅ ЗАВЕРШЕНО |

**Все задачи без Unity завершены!**

---

## 📊 Метрики прогресса

### Код
| Категория | Файлов | Строк | Статус |
|-----------|--------|-------|--------|
| Core | 7 | ~1800 | ✅ |
| Combat | 9 | ~2500 | ✅ |
| NPC | 4 | ~1500 | ✅ |
| World | 4 | ~1200 | ✅ |
| Generators | 7 | ~2000 | ✅ |
| UI | 8 | ~2000 | ✅ |
| Save | 3 | ~800 | ✅ |
| Tests | 3 | ~750 | ✅ |
| Managers | 4 | ~800 | ✅ |
| Editor | 2 | ~800 | ✅ |
| **ИТОГО** | **80+** | **~15000** | ✅ |

### Документация
| Категория | Файлов | Статус |
|-----------|--------|--------|
| Игровые системы | 14 | ✅ |
| Техническая | 7 | ✅ |
| Архитектура | 5 | ✅ |
| Asset Setup | 7 | ✅ |
| Отчёты | 3 | ✅ |

### Ассеты
| Категория | Статус |
|-----------|--------|
| ScriptableObjects | ⏳ Инструменты готовы, ожидает Unity |
| Спрайты | ⏳ Требует создания |
| Сцены | ⏳ Инструменты готовы, ожидает Unity |
| Аудио | ⏳ Требует создания |

---

## 🎯 Критические пути

### Путь 1: Минимально играбельная версия (MVP)
```
ЭТАП 1 (Assets) → ЭТАП 2 (Scenes) → ЭТАП 3 (Integration)
Время: 2-4 часа с инструментами автоматизации
Результат: Можно запустить, создать персонажа, ходить, атаковать
```

### Путь 2: Демо-версия
```
MVP + ЭТАП 4 (минимум спрайтов) + ЭТАП 5 (контент из JSON)
Время: 3-5 дней
Результат: Можно играть 30-60 минут
```

---

## 📝 Рекомендации

### Для User (Unity Editor) — когда будет доступен

1. **ЭТАП 1:** `Window → Asset Generator` → Generate All
2. **ЭТАП 2:** Создать сцену → `Window → Scene Setup Tools` → SETUP ALL
3. **Project Settings:** Добавить теги/слои (см. *_SemiAuto.md)

### Для AI Agent (Код) — СЕЙЧАС

1. **GameEvents.cs** — централизованные события
2. **SceneLoader.cs** — загрузка сцен
3. **JSON контент** — данные для техник, предметов, врагов

---

*План создан: 2026-03-30*  
*План обновлён: 2026-04-01 13:03:39 UTC*
