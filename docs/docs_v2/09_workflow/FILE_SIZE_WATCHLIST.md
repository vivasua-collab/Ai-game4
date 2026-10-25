# FILE_SIZE_WATCHLIST — вачлист файлов для ленивой корректировки размера

> **Дата среза:** 2026-10-25 · **Пороги:** делить при >600–800 строк или >25–30 КБ
> (обоснование — [AGENT_FILE_EDITING_NOTES.md](AGENT_FILE_EDITING_NOTES.md) §2–5).
> **Решение владельца (25.10):** корректировки **ленивые** — при очередном рабочем
> касании файла, НЕ отдельной задачей «рефакторинга ради размера».

## Политика ленивой корректировки

1. **Триггер — рабочее касание.** Файл из списков ниже попал в задачу → в том же
   эпизоде: (а) вычистить исторические комментарии / историю правок, не несущие
   смысловой нагрузки; (б) если файл >600–800 строк — разделить.
2. **Комментарии-контракты и инварианты НЕ трогать** (типы и судьба комментариев —
   AGENT_FILE_EDITING_NOTES.md §4). Удаляется только «история правок в теле»
   («исправлено тогда-то», построчные даты) и повествование, повторяющее код.
3. **Критические файлы — делить с осторожностью:** план деления в чекпоинт до
   правки, сохранение публичных контрактов 1-в-1, полная QA-регрессия после
   (`tools/qa_regression.sh`, ожидается 37/37), коммит отдельным эпизодом.
4. **Некритические** (QA-симы, UI-окна) — можно делить в рабочем порядке,
   проверкой служит сам прогон соответствующего сценария/окна.
5. **Список обслуживается лениво:** при касании файла из вачлиста обновить его
   строку (строки/КБ/статус `split`/`trimmed`). Раз в ~месяц — свежий срез
   (`wc -l` / `du -k`), устаревшие строки помечать `✅ done`.

## A. Игровое ядро (.cs) — критичность ВЫСОКАЯ (осторожно)

| Файл | Строк | КБ | Рекомендация |
|---|---|---|---|
| `Adapter/Scene/GameWorldController.cs` | 3589 | 208 | **Худший файл репо.** При следующем крупном касании — partial-классы по зонам (scene lifecycle / диагностика / скриншот-хуки). План в чекпоинт, регрессия 37/37 |
| `Modules/Combat/CombatService.cs` | 1867 | 124 | Делить по аспектам (резолв атаки / смерть-каскад / лут) при следующем касании |
| `Core/Data/Constants.cs` | 1630 | 76 | Partial-класс по доменам (`Data/Constants/<Domain>.cs`) — трогать только с регрессией: используется везде |
| `Modules/Body/BodyService.cs` | 1262 | 68 | Делить по подсистемам (HP/части/ампутации/дебаффы) при касании |
| `Modules/NPC/AnimalService.cs` | 1062 | 60 | Кандидат при следующем расширении зверей |
| `Modules/Combat/TechniqueService.cs` | 992 | — | При следующем серьёзном касании |
| `Modules/Formation/FormationService.cs` | 932 | 52 | При следующем серьёзном касании |
| `Modules/Player/PlayerTechniqueCaster.cs` | 906 | 56 | Часто меняется (PT0-3/4) — приоритетный кандидат |
| `Modules/NPC/NPCAIService.cs` | 892 | 48 | До AI-волн (GAP-008) желательно разделить |
| `Modules/Generator/TechniqueGeneratorService.cs` | 829 | 44 | Пограничный; делить при расширении генератора |
| `Modules/NPC/NPCService.cs` | 819 | 40 | Пограничный |
| `Modules/Combat/DamageService.cs` | 723 | 48 | Пограничный |
| `Modules/Charger/ChargerService.cs` | 714 | 36 | Пограничный; расширение GAP-003 — повод поделить |
| `Modules/Buff/BuffService.cs` | 700 | 36 | Пограничный |
| `Modules/Inventory/InventoryService.cs` | 680 | 32 | Пограничный |
| `Modules/NPC/NPCMovementService.cs` | 648 | 36 | Пограничный |
| `Modules/Inventory/StorageRingService.cs` | 619 | 32 | Пограничный |
| `Core/Data/Enums.cs` | 1088 | — | Partial по доменам, вместе с Constants (общий эпизод) |

## B. QA-симы (`Adapter/Scene/*SimDebug.cs`) — критичность НИЗКАЯ

Не входят в игровой рантайм (харнессы `qa_regression.sh`). Дробить свободно
по группам сценариев (один файл = одна группа), при следующем расширении.

| Файл | Строк | КБ |
|---|---|---|
| `Audit0922SimDebug.cs` | 2155 | 140 |
| `CombatSimDebug.cs` | 2119 | 140 |
| `Stage8TechniqueTruthSimDebug.cs` | 1174 | 68 |
| `Stage18SubsystemIntegrationTruthSimDebug.cs` | 1077 | 76 |
| `Stage15GeneratorIntegrityTruthSimDebug.cs` | 989 | 64 |
| `Stage11BodyTruthSimDebug.cs` | 912 | 64 |
| `Stage17InitConfigStaminaTruthSimDebug.cs` | 875 | 60 |
| `Stage16RenderAnimationSyncTruthSimDebug.cs` | 863 | 56 |
| `Stage13NpcAiTruthSimDebug.cs` | 787 | — |
| `Stage9InventoryTruthSimDebug.cs` | 786 | — |
| `ContextMenuSimDebug.cs` | 774 | — |
| `SaveLoadSimDebug.cs` | 754 | — |
| `Stage12ProgressionTruthSimDebug.cs` | 718 | — |

## C. UI-окна (`Adapter/UI/`) — критичность СРЕДНЯЯ

Проверка после деления — ручной прогон окна + профильный UIShot-сценарий.

| Файл | Строк | КБ |
|---|---|---|
| `TechniqueBookWindow.cs` | 1143 | 52 |
| `HotbarPanel.cs` | 1094 | 52 |
| `InventoryWindow.cs` | 1021 | 44 |
| `ItemContextMenu.cs` | 887 | 44 |
| `CheatPanel.cs` | 868 | 52 |
| `TradeWindow.cs` | 631 | 28 |

## D. Живые доки (`docs_v2/`) — критичность канон-спеки

Делить при следующем **серьёзном расширении** (не переформатировать ради
красоты): вынести устоявшиеся разделы в отдельные файлы, обновить оглавление
и ссылки из START_PROMPT §11. Индексы `10_godot_reference/*_index.md` —
справочники для точечного grep-чтения, деление низкоприоритетно.

| Файл | Строк | Примечание |
|---|---|---|
| `02_systems/COMBAT_SYSTEM.md` | 972 | Крупнейшая спека; растёт с каждым боевым этапом |
| `09_workflow/TESTING_RULES.md` | 739 | **Растёт каждым этапом аудита** — кандидат №1 среди доков |
| `09_workflow/ALGORITHMS.md` | 830 | Стабильный канон — низкий приоритет |
| `01_architecture/MODULE_STRUCTURE.md` | 773 | Частично генерируется картой модулей |
| `01_architecture/FILE_TREE.md` | 788 | Обновляется вручную редко — кандидатом не считать, только актуализировать |
| `05_data/GENERATORS_SYSTEM.md` | 753 | Перед расширением форм-факторов (GAP-003) |
| `05_data/DATA_MODELS.md` | 721 | Стабильный |
| `05_data/SAVE_SYSTEM.md` | 694 | Растёт с новыми ISaveable-блоками (сейчас 21) |
| `06_player/INVENTORY_SYSTEM.md` | 674 | Стабильный |
| `04_entities/NPC_ASSEMBLY_PIPELINE.md` | 653 | Перед AI-волнами (GAP-008) |
| `02_systems/TECHNIQUE_SYSTEM.md` | 636 | Стабильный |
| `03_world/TILE_SYSTEM.md` | 629 | Стабильный |
| `01_architecture/DI_AND_EVENTBUS.md` | 608 | Стабильный |
| `07_ui/SPRITE_CATALOG.md` | 606 | Растёт с партиями спрайтов |

## Вне вачлиста (обслуживание другой политикой)

- `worklog.md` (648 строк) — append-only, свой лимит: архивация в `checkpoints/`
  при >1000 строк (START_PROMPT §8.1).
- `checkpoints/` — исторические записи, размер не правится.
- `portal/data/*.json` (bugs.json 192 КБ) — данные портала, не код; рост
  законен, ограничивать не нужно.
- `docs/docs/`, `docs/docs_old/`, `docs/docs_temp/` — архивы/исследования,
  не обслуживаются ленивой чисткой.
- `game/scenes/*.tscn` — генерируемые сцены.

---
*Срез снят автоматически (wc -l / du -k); следующий срез — при ~месячной
актуализации либо лениво по касаниям.*
