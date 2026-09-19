# Чекпоинт: Godot 4.7.1 → 4.7.2 — оценка необходимости обновления движка

**Дата:** 2026-09-19 17:30 UTC (20:30 МСК)
**Сессия:** web-2bba32a1 (эпизод 1 дня)
**Тип:** research — восстановление потерянного исследования + оценка (код НЕ менялся)

---

## Контекст

Запрос пользователя: «Пока я выполнял локальные тесты, у нас был этап
исследования, Godot 4.7.2 есть ли смысл обновляться, я не могу найти
документы по результатам этого исследования, посмотри, возможно они
сохранились, если нет, оцени ещё раз необходимость обновления версии
движка».

### 0. Результат поиска исходных документов: НЕ НАЙДЕНЫ

Поиск по всему репо (grep `4.7.2|4.7.1`), по platform-worklog, по кэшу
результатов инструментов прошлых сессий (`tool-results/`, ~40 файлов),
по git-истории (--grep, reflog, stash):

- Единственный след — NEXT-метка `worklog.md:2258` (от 19.09): *«Godot
  4.7.2 changelog/оценка — обе задачи из 16.09 ещё не начаты»*.
- Хиты «4.7» в tool-results — это заголовки C#-файлов (`/// Godot 4.7
  notes:`) и номера задач аудита «4.3+4.7», НЕ исследования версии.
- Вывод: исследование обсуждалось в сессии 16.09, прерванной обрывом
  окружения, но НИ чекпоинта, ни коммита создано не было — результаты
  потеряны безвозвратно вместе с контекстом. Настоящий документ —
  повторное исследование «с нуля», по живым данным (§0 правил
  достоверности: не по памяти, а из первоисточников).

---

## 1. Источники (все получены живыми запросами 19.09.2026)

| # | Источник | Что взято |
|---|----------|-----------|
| 1 | godotengine.org — статья «Maintenance release: Godot 4.7.2» (Thaddeus Crews, 18.08.2026) | Полный текст: хайлайты, заявление о совместимости, число фиксов |
| 2 | godotengine.org — «Release candidate: Godot 4.7.2 RC 1» (03.08.2026) | Хайлайты RC-стадии (43 правки), коммит 36a04fe52 |
| 3 | Веб-поиск (godotengine.org, github.com, gamedev.net, godotlearning.com) | Даты релизов, сводка «57 фиксов / 39 контрибьюторов», статус 4.8-dev |
| 4 | GitHub Releases API | ❌ rate-limited с IP песочницы — полный список 57 фиксов не вытащен (см. §5 caveat) |

---

## 2. Факты о релизе

- **Godot 4.7.2 stable вышел 18.08.2026** — maintenance-релиз (только
  багфиксы по политике ветки 4.7).
- **57 правок от 39 контрибьюторов**; собран из коммита RC1 36a04fe52
  (+добавки между RC и stable: Editor favorite nodes, GDExtension
  parents-check, Windows DispatcherQueueOptions).
- 4.7.1 вышел ~месяцем ранее (июль 2026) — мы на нём сейчас.
- Официальная позиция (цитата из статьи): *«As of now, there are no
  known incompatibilities with the previous Godot 4.7.1 release.
  **We encourage all users to upgrade to 4.7.2.**»*
- .NET-сборка («mono») в 4.7.x остаётся на **.NET 8** — наш SDK-стек
  (dotnet 8.0.425 в песочнице) не меняется.
- В разработке **4.8** (1164 коммита в master) — feature-релиз. НАМ НЕ
  НУЖЕН до feature-complete вехи (procgen-планы требуют стабильной
  линии 4.7.x).

### Курируемые хайлайты 4.7.2 (полный список из статьи)

```
3D:          Fix 3D ruler tool tooltip (GH-120890)
Assetlib:    Asset Store: Fix image width on different Editor Scales (GH-121470)
Core:        Fix crash when failing to open log file for writing (GH-121926)
Core:        Forbid negative weights in RandomPCG::rand_weighted (GH-120004)
Core:        Make it impossible to have more than one main thread (GH-121161)  ← critical
Core:        Update DirAccess::create_temp to not fail on empty prefix (GH-121315)
Editor:      Don't auto-translate favorite nodes (GH-122260)
Editor:      Fix Visual Profiler cursor (GH-118294)
GDExtension: Fix register_extension_class never iterating parents (GH-120985)
GUI:         Fix BaseButton input when enable_long_press_as_right_click (GH-120962)
Input:       Fix performance issues with high mouse polling rate on Windows (GH-109639)  ← critical
Input:       Fix simultaneous shift release (GH-120327)
Input:       Wayland: IME popup position under fractional scaling (GH-121571)
Multiplayer: Fix peers stopping replication on MultiplayerSpawner deletion (GH-109864)
Navigation:  Fix debug NavigationRegion3D colors (GH-120939)
Network:     mbedTLS: always use Godot's OS as entropy source (GH-121759)
Platforms:   Windows: exception handling in DispatcherQueueOptions init (GH-122261)
Rendering:   Fix PCSS shadows using shadow range begin in wrong space (GH-120774)
Thirdparty:  Update AccessKit to 0.22.3 (GH-121393)
```

---

## 3. Релевантность к нашему проекту

Стек: C#/.NET 8 (mono), 2D-тайлы 500×500 (TileMapLayer), UI целиком из
кода (Control/AddThemeStylebox), мышь+клавиатура, headless-QA,
OpenGL3, свой JSON-сейв.

### 3.1. Прямо релевантные правки

| Правка 4.7.2 | Наше использование | Эффект |
|--------------|--------------------|--------|
| **Input: high polling rate mouse perf на Windows (GH-109639, critical)** | Пользователь локально тестирует на **Windows**; мышь активно используется (GWC клики, ПКМ-контекстные меню, trade/loot-окна) | Прямая польза для локальных тестов пользователя: убирает фризы/фризы-производительности при движении мыши 1000Гц+ (типично для игровых мышей). Гипотеза-бонус (НЕ подтверждена): часть «ощущений сломанного движения» при INP-1 могла идти отсюда — но модальная пауза уже починена и диагностируется индикатором, смешивать нельзя |
| Input: simultaneous shift release (GH-120327) | Shift задействован (InputMapInitializer, InputAdapter — модификаторы движения) | Корректность комбо-релизов клавиш |
| Core: crash при неудаче открыть лог (GH-121926) | GameBoot пишет логи | Устраняет редкий crash-путь |
| Core: single main thread (GH-121161, critical) | Всё приложение | Общая стабильность |
| Platforms: Windows DispatcherQueueOptions (GH-122261) | Локальная машина пользователя | Robustness Windows-инициализации |

### 3.2. НЕ затронутые нами подсистемы

В хайлайтах **ноль** правок по: C#/.NET-биндингам, TileMap/2D-рендеру,
FileAccess/сейвам, Control/UI-темам. Также не используем: Multiplayer,
Navigation, PCSS/3D-рендер, GDExtension, Asset Store, Wayland.

---

## 4. Цена обновления (в песочнице + у пользователя)

### 4.1. Песочница (один короткий эпизод, ~30 мин)

| Файл | Что менять |
|------|-----------|
| `cold_start.sh` | `GODOT_URL` → `.../download/4.7.2-stable/Godot_v4.7.2-stable_mono_linux_x86_64.zip` (:42); `GODOT_BIN` пути (:74); **size-check 145073296 → фактический размер нового бинарника** (:75); dir-имена в python-извлечении (:89-90) |
| `tools/run_godot.sh` | `GODOT_BIN` путь (:11) |
| `tools/qa_regression.sh` | `GODOT` путь (:7) |
| `START_PROMPT.md` | 8 упоминаний 4.7.1 (шапка, таблица движка, тулинг, правило #1/#3, запускалки) |
| `README.md`, `docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md`, `docs/docs_v2/09_workflow/COLD_START.md`, `SESSION_SUMMARY.md` | Упоминания версии/путей |

Плюс: подмена бинарника в `my-project/godot` (симлинк `/home/z/godot`
указывает туда), пересборка C#, QA-регрессия 18/18 (~2.5 мин), коммит+пуш.

### 4.2. Машина пользователя

Скачать .NET-сборку 4.7.2, открыть проект — кэш `.godot/` перегенерируется
автоматически. **Сейвы не затронуты** (собственный JSON-формат, движок в
сериализации не участвует). project.godot `config_version=5` неизменен.

### 4.3. Координация с INP-1 (важно!)

Решающий локальный тест пользователя (сборка с HUD-индикатором паузы от
19.09) лучше выполнять **на той же версии движка, что и песочница** — иначе
в одном тесте меняются два переменных (сборка кода + версия движка).
Оптимальный порядок: обновить песочницу → пользователь обновляется до
4.7.2 → один решающий тест INP-1.

---

## 5. Вердикт

**ОБНОВЛЯТЬСЯ: ДА, рекомендовано — но без срочности.**

Аргументы «за»:
1. Официальное «safe upgrade, no known incompatibilities, encourage all
   users to upgrade» — политически и технически безопасный патч-прыжок.
2. Две input-правки прямо касаются Windows-локальных-тестов пользователя
   (мышь high-polling — critical-flag фиксы).
3. Цена — один короткий эпизод (§4.1), полностью покрывается QA-регрессией.
4. Стратегически: оставаться на свежей точке maintenance-линии 4.7.x;
   чем дальше отстаёшь, тем больше eventual-прыжок (4.7.3, 4.8...).

Честные caveats (§0):
- Для нашего стека (C#/.NET8, 2D, TileMap, UI-из-кода) в 4.7.2 **нет ни
  одной точечной правки** — эффект «общая стабильность + Windows-ввод»,
  не «фичи». Не будь Windows-тестов пользователя — можно было бы спокойно
  ждать 4.7.3.
- Полный список 57 фиксов получен только в курируемом виде (~19
  хайлайтов): GitHub API rate-limited. На решение не влияет (политика
  maintenance-релизов = только багфиксы), но при исполнении обновления
  стоит глазами пробежать полный interactive changelog.
- Проверка «работает после обновления» = QA 18/18 + build 0 errors;
  отдельных 2D/TileMap-регрессий в списке нет — риск ниже порога
  обнаружимости нашего QA-харнесса.

---

## 6. План исполнения (когда пользователь подтвердит решение)

1. cold_start.sh: URL/пути/size-check (размер узнать фактическим
   скачиванием, 4.7.1 = 145073296 байт — у 4.7.2 будет отличаться).
2. run_godot.sh / qa_regression.sh — пути.
3. Доки: START_PROMPT (8 мест), README, ENVIRONMENT_LINKING, COLD_START,
   SESSION_SUMMARY.
4. Скачать 4.7.2 mono → подмена в my-project/godot → `--version` check →
   build 0 errors → QA 18/18.
5. Коммит + пуш + инструкция пользователю для его Windows-машины
   (§4.2), увязка с решающим тестом INP-1 (§4.3).

NEXT: решение за пользователем; параллельно живёт задача «тестовая задача
переноса UI в сцену» (из 16.09) и ждём результат локального теста INP-1.
