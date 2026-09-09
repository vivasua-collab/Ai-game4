# Чекпоинт R11 — Save/Load persistence (P0-P2 внешнего ревью №2)

**Дата:** 2026-09-09 (сессия R11, второе восстановление после обрыва)
**Коммит:** см. `git log -1` после пуша (HEAD == origin/main)

## Контекст

Внешнее ревью №2 (2026-09-09, блок Save/Load → World → Formation):
- 🔴 P0 `SaveFileHandler`: `Dictionary<string, object>` как контракт
  persistence — при чтении значения приходят как `JsonElement`, а
  `RestoreState(state)` всех ISaveable ждал свои DTO → паттерн
  `state is XxxSaveData` молча проваливался у ВСЕХ реализаторов,
  восстановление после Load не работало вообще. Требование: typed-контракт
  + round-trip-тесты.
- 🔴 P1 `SaveModule`: `SaveCompletedEvent(true, …)` издавался безусловно.
- 🔴 P1 `SaveDataAggregator`: исключения Capture/Restore глотались,
  ошибочные блоки писались в файл, Load возвращал true всегда.
- 🟠 P2 `WorldService.GetLocation()`: захардкожен Biome=Plains,
  DangerLevel=0.
- 🟠 P2 `TileModule`/`WorldModule`: две независимые модели карты.
- 🟠 P2 `FormationService`: корректен сам по себе, ломался общим P0-контрактом.

Обрывы сессий: (1) после VERDICT: PASS пользователь нашёл дефект САМОГО
теста — «камни 10→10» и «слот5 ''→''»: мутации `TryRemoveItem(int.MaxValue)`
(транзакционный отказ — доступно меньше запрошенного) и `AssignSlot(5,
"qa_probe_technique")` (валидация IsLearned отклоняет несуществующую
технику) НЕ изменяли состояние → проверки восстановления были тривиальны.
(2) Обрыв на начале «усиливаю мутации и добавляю integrity-контроль».
Код R11 был в рабочем дереве незакоммичен — восстановлен, тест усилен,
переверифицирован, закоммичен, запушен.

## Реализация

- `ISaveable`: новый `Type StateType { get; }` — конкретный тип state-блока
  для round-trip (все 8 реализаторов: body, inventory, techniques,
  formation, npc, technique_slots, charger, save_meta).
- `SaveJson` (новый): единые опции JSON всего конвейера — IncludeFields
  (XxxSaveData описаны ПОЛЯМИ — по умолчанию System.Text.Json их не
  пишет, блоки превращались в «{}»), CamelCase, case-insensitive чтение
  (совместимость с обоими историческими писателями), WhenWritingNull.
- `SaveDataAggregator`: `ConvertToStateType` (JsonElement → StateType),
  транзакционность Save (ошибка CaptureState → файл НЕ пишется) и Load
  (любой упавший блок → false), `LastErrors` для честных событий.
- `SaveModule`: `SaveCompletedEvent`/`LoadCompletedEvent` несут РЕАЛЬНЫЙ
  результат; автосейв логирует провал.
- `SaveService`: типизированная мета (SaveMetaState вместо анонимного
  типа), `LastError`.
- `Container.ResolveAll<T>`: инстанс-ориентированный обход — раньше
  мульти-интерфейсные форварды `Register<ISaveable, X>` коллапсировали в
  один словарный ключ, `ResolveAll<ISaveable>` возвращал SaveService ×7,
  в файл писался один блок save_meta.
- `WorldService.GetLocation()`: Biome из реального TerrainType (полный
  coverage-маппинг), DangerLevel из LocationData.
- `TileModule`/`GameWorldController`: единый источник размеров карты
  (WorldService), связь CurrentLocation↔CurrentGrid атомарна.
- `SaveLoadSimDebug` v2 (см. ниже).

## Тест SaveLoadSimDebug v2 (дефект от пользователя исправлен)

- Мутации РЕАЛЬНЫЕ: удаление ровно доступного числа камней (не
  int.MaxValue), НАСТОЯЩАЯ изученная техника из
  `TechniqueService.GetAllTechniques()` (не выдуманный id).
- Integrity-контроль (анти-тривиальность): каждая мутация ОБЯЗАНА
  изменить наблюдаемое состояние (камни 5+5→10, HP 1.00→0.66, слот5
  непустой, формация непустая, зарядник On→Off), иначе VERDICT: FAIL.
- Снимки CaptureState обязаны содержать МАРКЕРЫ мутаций (id камня,
  id техники, id формации, поля parts/mode) — сейв зависит от мутаций,
  равенство «до/после Load» нетривиально. Пустые блоки «{}» → FAIL.
- Post-load домен: камни 10→10 (после 10→0!), слот5 tech_…L1_41FE→(же),
  HP 0.66→0.66, зарядник On→On (после On→Off).

## QA (2026-09-09, все симы — свежий прогон после усиления)

- Build: 0 errors.
- GODOT_SAVELOAD_DEBUG: VERDICT: PASS — 8 блоков, снимки 7/7 OK,
  файл 8/8 блоков 34 КБ, поля-маркеры 4/4, домен 5/5, integrity OK.
- Регрессии: CONTEXT / TRASHDROP / STORAGE / QUEST — все VERDICT: PASS.
