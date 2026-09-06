# Полный аудит кода: архитектура · документация · лор · качество

**Дата:** 2026-09-06 08:49–11:20 UTC
**Сессия:** ручной аудит по прямому указанию пользователя (после завершения 6/6 автосеансов)
**Task ID:** AUDIT-FULL-2026-09-06
**Тип:** audit (+ точечные критические фиксы)
**HEAD на момент аудита:** `9de87e5` (= origin/main, рабочее дерево чистое)
**Метод:** 3 параллельных Explore-агента (архитектура / доки-код / качество) + личная верификация ключевых находок + чтение docs_v2 (ARCHITECTURE, MODULE_STRUCTURE, FILE_TREE, HOTKEYS, CHEAT_PANEL, LORE_SYSTEM, START_LORE, SAVE_SYSTEM, DIALOGUE_SYSTEM, SESSION_CONTEXT, START_PROMPT) + git-история S1–S6.

---

## ЭТАП A. Соответствие архитектуре (docs_v2/01_architecture)

### A.1. Слои — ✅ СООТВЕТСТВУЕТ (100%)

- `using Godot` в Core/Modules/Entry → **0 совпадений**. Все 47 наследников Node/Control — только в Adapter/Scene и Adapter/UI.
- Core имеет собственную математику (`Position2D`, `Vector2f`, `Rect2i` — readonly struct); NPC использует её через alias, а не Godot-типы.
- ⚠️ **Слабость (не нарушение):** весь код — один csproj (`Godot.NET.Sdk`). Изоляция «Core без Godot» — конвенционная, не enforced компилятором. Отдельный `CultivationGame.Core.csproj` без Godot-референса сделал бы правило несгибаемым (FILE_TREE.md это и предлагает — 4 проекта; фактическая структура = 1 проект, расхождение существовало с самого переноса).

### A.2. Hub-and-Spoke — ❌ НАРУШЕНИЯ (10 файлов, 6 направлений)

| Нарушение | Где | Серьёзность |
|---|---|---|
| **DI-каст к конкретному классу чужого модуля** | `Player/PlayerTechniqueCaster.cs:290` — `_formations is not Modules.Formation.FormationService` (ради formation-бонуса урона; fallback 1000‰) | Высокая. Прямо запрещён MODULE_STRUCTURE §0. Замена реализации молча «выключит» бонус. |
| **NPC-модуль — «магнит» зависимостей** | `NPC/NPCModuleServices.cs:37-38` регистрирует чужие конкретные классы `QiDataProvider` (Qi) и `EquipmentDataProvider` (Inventory); `NPCAssemblyService`, `SoulGenerator`, `AnimalService` тянут из Body конкретные `SpeciesRegistry`, `BodyEnhancementSystem`, интерфейс `IBodyFactory` (живёт в модуле, а не в Core) | Высокая. Системное нарушение звезды в 4 файлах. |
| **Player → Combat на конкретных классах** | `TechniqueSlotService.cs:44` `[Inject] TechniqueService`; `PlayerTechniqueCaster.cs:24-26` (TechniqueService, TechniqueChargeService, IFormationGeneratorService); `PlayerCombatAdapter.cs:8,222` (`CombatLos.HasLineOfSight`) | Средняя. В Core нет ITechniqueService — API-дыра Core. |
| **Generator → Formation** | `GeneratorModuleServices.cs:8`, `DeduplicationService.cs:22`, `FormationGeneratorService.cs:12` — `FormationRegistry` | Низкая (утилитарный модуль, но формальное нарушение). |
| Межмодульно видимые интерфейсы вне Core | `Body/IBodyFactory.cs`, `Generator/IFormationGeneratorService.cs` | Средняя — ARCHITECTURE §1.2 требует их в Core. |

**Положительное:** `NPC/NPCCombatAdapter.cs` — образцово («НЕ инжектит ICombatService», только контракты). NPC→Combat не существует.

### A.3. Запрещённые паттерны

- Singleton/ServiceLocator/поиск по имени в Core/Modules/Entry → **0**. ✅
- **GameBoot.cs:19-22** — `public static IResolver? Container` (ServiceLocator-стиль composition-root-мост). Используется 35+ файлами Adapter. Формально подпадает под §10, фактически — единственный законный способ дать Godot-нодам доступ к DI. Вердикт: принять как задокументированное исключение.
- **C# event для межмодульной коммуникации — 5 мёртвых рудиментов:** `SaveService.cs:40-41`, `WorldService.cs:55-56,116`, `IGameSession.cs:36` (реализация GameSession.cs:44). Подписчиков — 0. Дублируют живые контракты шины. Вердикт: удалить (мёртвый API-слой).

### A.4. Контракты readonly struct — ✅ 100%

Все ~140 контрактов в 25 файлах — `public readonly struct`. Единственная мелочь: `BodyContracts.cs:52` — массив `EquipmentSlot[] BlockedSlots` (reference-поле в struct; аллокация). Не нарушение, но против zero-GC духа.

### A.5. Шаблон модуля — 🟡 ЧАСТИЧНО

- 17/17 модулей имеют `XxxModule : IModule` ✅
- `XxxModuleServices` отдельным файлом — **11/17** (у Tile, Quest, Player, World, Save, UI Register живёт внутри Module.cs — паттерн соблюдён, структура нет).
- `XxxService` — 16/17 (Generator — россыпь генератор-сервисов без единого «лица»; `Core/Interfaces/IGeneratorService.cs` вообще без реализаций — мёртвый).
- **`Charger/ChargerData.cs:83` `ChargerSlotConfig` — struct с мутабельными полями** — прямое нарушение «Config — class, не struct» (MODULE_STRUCTURE §0).
- Конфигов отдельных — 14; BodyConfig внутри BodyModule.cs; у Charger/Generator нет.

### A.6. DI и фазы

- **GameLifetimeScope: порядок регистраций 1:1 совпадает с документированным** (World → Tile → Body → Qi → Buff → Inventory → Combat → Formation → NPC → Player → Quest → Interaction → **Trade** → UI → Charger → Save → Generator). ✅
- **Фазы: 15 зарегистрировано** (документировано 10; аудит 08-26 перенумеровал в 1–14; +StartingGearPhase). Фактический порядок: CoreValidation → TileMapGen → WorldInit → PlayerSpawn → **StartingGear(5) + AnimalSpawn(5) — коллизия PhaseOrder** → HumanNPC(6) → Group(7) → Formation(8) → Charger(9) → Quest(10) → UI(11) → PreGenTechnique(12) → TechniqueGrant(13) → Finalize(14). Спасает стабильная сортировка (OrderBy), но нумерация — долг. `NPCSpawnPhase.cs` существует, но **не зарегистрирован** (мёртвый файл, заменён Animal/Human/Group).

### A.7. Замороженные решения — статус

| Решение | Статус |
|---|---|
| Q8=A (отложить save/load) | ✅ Выполнен (F5/F9 потребители закомментированы, GameWorldController.cs:1437-1441) |
| Q9=B (разделить Spirit+Ring) | ✅ Выполнен (ISpiritStorageService + StorageRingService) **НО мёртвый legacy**: старый унифицированный `StorageService.cs` жив, создаётся в `InventoryModule.cs:68-72`, никем не потребляется |
| Q11=A (оставить _Draw) | ✅ TileMapLayer — 0 следов; кастомный _Draw + culling (SceneBuilder.cs:237-278) |
| Q13=A (queue re-entrant) | ✅ EventBus.cs:50-98 — очередь + дренаж, циклы ловятся |
| Q10=A (Element.Poison как маркер) | ✅ В коде; **лор-канон его исключает** — см. ЭТАП C |
| Qi = long | ✅ 100% (float только у множителей/коэффициентов) |

---

## ЭТАП B. Соответствие документации (docs_v2 ↔ код)

### B.1. Ключевые расхождения (полный реестр — в выводах агентов; здесь — важнейшее)

| # | Документ | Заявлено | Фактически | Источник расхождения |
|---|---|---|---|---|
| 1 | **HOTKEYS.md** | Мёдитация R/M; хотбар 1=WeaponMain,2=WeaponOff,3-9 drag&drop; F5/F9 живые; карта M, мини-карта N, RMB-меню; InputLog backquote | Медитация **V**; хотбар 1/2=режимы боя, 3-9=техники, Shift+1-9=пояс; F5/F9 отключены (Q8); M/N/R/backquote/ПКМ-меню — мёртвые проводки; Z=каст, X=цикл техник, K=Культивация, F2=читы, F1=справка, PageUp/Down=скорость — в доке отсутствуют | Смешанное: сессии внедрения ЦИ (этапы 1-7), S4, решения 2026-08-28 |
| 2 | **CHEAT_PANEL.md** | F1, top-left панель | **F2**, центрированное модальное окно; +M2-секции (мишени/лук/стрелы/исцеление); +гейт CheatsEnabled в настройках главного меню | Решение пользователя 2026-08-28 (F1=справка Old School) + M2-сессия 09-03 + требование «отключаемое в настройках» (выполнено, но не в доке) |
| 3 | **MODULE_STRUCTURE.md** | 16 модулей | **17** — модуль Trade (TradeModule/TradeService/CurrencyService/TradeConfig/TradeContracts, ITradeService+ICurrencyService в Core) — полностью недокументирован | Сессия 2026-08-25 (Phase 4-5) — код обогнал доку |
| 4 | **SAVE_SYSTEM.md** | Живая система: F5/F9, автосейв каждые 60 тиков | Отключено (Q8), автосейва нет, «Load Game» всегда fallback в NewGame; вместо сейвов — StartingGearPhase (детерминированный сид 1000: кинжал, лук, 30 стрел, материалы, камни) | Решение пользователя Q8 + Phase 8 p2 |
| 5 | **DIALOGUE_SYSTEM.md** | Выбор 1-4; typewriter с CancellationToken; дистанция ≤1.5; отключаемый typewriter; 7 ролей (Student) | Выбор **1-9** (S6); typewriter — Tick-модель + **реальное время при паузе** (S6-патч — до него текст вообще не печатался); дистанция 2.5; отключаемость нет; Student-диалога нет (5 ролей) | S6 + постепенный дрейф |
| 6 | **UI_DESIGN.md** | 22 View | Реализовано 10 полностью + 3 частично + 9 отсутствуют; **не задокументированы новые**: HotkeysWindow(F1), CheatPanel(F2), CultivationWindow(K), DamageDirectionIndicator, LowHpVignette, KillFeed-механика, ToastStack-агрегация | Сессии S1-S6 (соблюдалось правило «доку не трогать») |
| 7 | **TESTING_RULES.md / AI_DEVELOPMENT_WORKFLOW.md** | dotnet test + godot --script тесты (которых нет) | **Документированы 2 из 19** GODOT_*-хуков; весь QA-конвейер (VERDICT: PASS-харнесс, 10 SimDebug-сцен) живёт только в worklog/checkpoints | S1-S6 |
| 8 | **START_LORE §5.2-5.4** | Старт: железный меч + роба + сапоги, 5×хлеб, 3×пилюли, осколок камня; техники «медитация» + «удар» | StartingGearPhase: **кинжал + лук + 30 стрел + 5×материалы + камни**; старт-баланс 50 камней (CurrencyService) | Phase 8 p2 (starter kit под ranged-бой) — **лор не синхронизирован** |
| 9 | FILE_TREE.md | 4 csproj-проекта + tests/ | 1 csproj, тестов-проекта нет (QA = headless env-хуки) | Перенос из Ai-game3 |
| 10 | LORE_SYSTEM §7.1 | Poison удалён из канона | `Element.Poison` в коде (Q10=A — маркер техник яда) | Принятое решение, не отражено |

### B.2. Мёртвые проводки ввода (игрок жмёт — ничего не происходит)

`F5`, `F9` (Q8), `R` (rest), `M` (world map), `N` (minimap), `` ` `` (input log), ПКМ-контекст-меню (контракт есть, издателя/подписчика нет). **Хуже: окно-справка F1 рекламирует 4 из них как рабочие** (HotkeysWindow.cs:156,161,166,173) — систематический обман игрока. Исправлено в этой сессии (см. ЭТАП E).

### B.3. Статус UI-вью (сводка)

Реализованы: HUD, Hotbar(v2), Inventory(+кукла), TechniqueBook(T), CharacterSheet(C), Cultivation(K), QuestLog(Q), Trade, Dialogue, Toast(v2-стек), + новые DamageDir/Vignette/Hotkeys/Cheat/EventLog(J)/DamageNumbers.
Отсутствуют: MiniMap, WorldMap, Status, Rest, Formation-окно, Achievements, Crafting-окно, ContextMenu, InputLog, PauseMenu, LootWindow. Частично: Journal (J = лента событий, не журнал с категориями), Equipment (внутри инвентаря), Tooltip (только Trade).

---

## ЭТАП C. Реестр решений и оценка ИИ-изменений (доки + ЛОР)

### C.1. Решения ПОЛЬЗОВАТЕЛЯ (принятые — требуют отражения в доках; отражено в этой сессии)

| Решение | Дата | Отражение |
|---|---|---|
| Q1-Q14 = A (кроме Q7=B, Q9=B) | 08-22 | Выполнены все; docs-обновление не требовалось (архитектурные фиксы соответствовали доке) |
| Чит-меню: F2, отключаемое в настройках | 08-28 + пост. правило | CHEAT_PANEL.md обновлён (был F1) |
| F1 = окно-справка (Old School), инлайн-хинты из окон убрать | 08-28 | HOTKEYS.md обновлён |
| Сейвы отложены, вместо них стартовая генерация | Q8 + пост. правило | SAVE_SYSTEM.md помечен; START_LORE расхождение задокументировано (лор не редактировался без указания) |
| 6 автосеансов: «интерфейсы — информативность и отсутствие багов» | 09-04 | Все 6 выполнены; изменения в доках теперь отражены |
| Дока первична, не редактировать без указания | постоянно | Соблюдалось; сегодняшняя синхронизация — по прямому указанию («необходимо отразить») |

### C.2. Оценка ИИ-решений (в рамках мандата пользователя) относительно доков и ЛОРА

| Изменение | Сессия | Оценка | Комментарий |
|---|---|---|---|
| Тост-стек (5 строк, ×N, fade) | S2 | ✅ Корректно | Чистая презентация; UI_DESIGN ToastView совместим; лорно-нейтрально |
| Виньетка HP<35% | S2 | ✅ Корректно | Периферийный фидбек; UI_DESIGN не описывал, но и не запрещал; лорно-нейтрально (мета-фидбек игрока, не «способность» ГГ) |
| Kill-feed + атрибуция атакующего | S3 | ✅ Корректно | ЛОР §9.1 «убитый NPC = труп с инвентарём» — смерть озвучивается; атрибуция = честность информации. Формат тоста+журнала соответствует ToastView/EventLog |
| Hotbar v2 (3-9 техники, кулдауны, Qi-гейт, цвета стихий) | S4 | ✅ Корректно | Qi-гейт = «техника требует минимум Ци» (ЛОР §8.1); цвета стихий = ELEMENTS; ⚠️ семантика слотов разошлась с HOTKEYS §8 — теперь синхронизировано в доке |
| Стрелка направления атакующего | S5 | 🟡 Приемлемо с оговоркой | UI-мета-фидбек (как в Kenshi), НЕ внутримировое «чувство» ГГ — иначе конфликт с «планетарное восприятие = L8». В доке трактовать как HUD-афорданс |
| S6-диалог: 1-9/E/Esc, клик=Advance, ▼, печать при паузе | S6 | ✅ Корректно | Фикс «слепого окна» — критичный; DIALOGUE_SYSTEM §3.3 (E/Esc) соблюдён; расширение 1-4→1-9 разумно (HOTKEYS §4.2 и так обещал 1-9 в диалоге!) — противоречие было ВНУТРИ доков, код выбрал общую версию |
| S6-лавка: пометки «не хватает», тултипы, hover, Esc | S6 | ✅ Корректно | Валюта = духовные камни (ЛОР §9.2 «твёрдое Ци») ✅; тултипы с редкостью = UI_DESIGN Tooltip #20 (частичная реализация) |
| Trade-мост «Покажи товары» → TradeRequestedEvent | 08-25 | ✅ Корректно | Через шину, диалог завершается ДО открытия лавки (пауза-конфликт учтён) — Hub-and-Spoke соблюдён |
| Стартовый набор (кинжал+лук+стрелы) | Phase 8 p2 | 🟡 Дивергенция с START_LORE §5.2 | Прагматика ranged-тестов; **против лора** (меч+роба+сапоги, хлеб/пилюли). Требует решения: либо лор-апдейт, либо набор → лор-канон. Пока задокументировано как расхождение |
| Trade-сток детерминированный FNV-1a(npcId) | 08-25 | 🟡 Лор-натяжка | «Сохранение материи»: сток появляется из ниоткуда при каждом открытии? В v1-допуск, отметить при будущей экономике |
| StartingGearPhase вместо сейвов | Phase 8 p2 | ✅ Соответствует решению Q8 | Реализация чистая (Phase, детерминизм), но конфликт со START_LORE §5 (см. выше) |

**Итог по лору:** ни одна ИИ-фича не нарушает законы мира (Ци квантованная=long ✅, стихии ✅, экономика камней ✅, смерть=труп ✅). Единственная предметная дивергенция — **стартовый набор** (лор vs код) и микро-натяжка trade-стока. «Магических исключений» не введено (ЛОР §12) ✅.

### C.3. Нарушения ИИ-правил, найденные в коде (вне мандата — эрозия)

- 10 межмодульных импортов (см. A.2) — накапливались с этапов переноса/ЦИ/Phase8, не от S1-S6.
- Мёртвый C#-event API, мёртвый StorageService, мёртвый NPCSpawnPhase, мёртвый IGeneratorService.
- GameWorldController рос каждым сеансом (1967 строк) — «бог-класс».
- S1-S6 добавили ~30 warnings (271→301): в основном CS8618/CS8625 в новых QA-сценах.

---

## ЭТАП D. Качество кода

### D.1. 🚨 КРИТИЧЕСКИЙ БАГ (найден и ИСПРАВЛЕН в этой сессии)

**EventLogWindow.cs:330 — бесконечный цикл = полный фриз игры** после 61-го события (обычный бой даёт >60 событий за пару минут):
```csharp
while (_list.GetChildCount() > MaxEntries)
    _list.GetChild(0).QueueFree();   // QueueFree НЕ удаляет из дерева до конца кадра!
```
`GetChildCount()` не уменьшается → вечный цикл в main thread. Подтверждено эмпирически (headless-скрипт Godot 4.7.1: после QueueFree count не изменился). QA-сессий не ловил только потому, что ни один сим не генерирует >60 событий. **Фикс: `RemoveChild` + `QueueFree`.**

### D.2. Прочие находки (по убыванию серьёзности)

1. **Атака/каст во время диалога не подавлялись** (Space/Z через _isOverUI: диалог, J, Q не входили в SetOverUI-условие) — вопреки HOTKEYS §9.2 «Dialogue: Атака ❌». **Исправлено в этой сессии.**
2. **Zero-GC нарушен повсеместно (~37 мест в 14 файлах):** HUD-Label.Text пересобирается каждый физ-кадр без dirty-check (GameWorldController:796-867); InputAdapter:137 — 9 string-интерполяций/кадр на опрос hotbar; HotbarPanel/NPCSpriteRenderer/ProjectileRenderer/DamageDirectionIndicator _Draw/_Process; 3 рендерера печатают GD.Print на каждый redraw (10 Гц спам); PlayerModule.Tick:105 — Console.WriteLine каждый тик при диагонали. ≈400–600 строк мусора/сек.
3. **172 null-warnings** (CS8618×91, CS8625×81, CS8603×28…) — nullable-контракт не ведётся; реальные возможные NPE в BodyService/ChargerService/BuffService.
4. **QueueFree-паттерн «очистка детей»** в 10+ UI-местах — stale-узлы до конца кадра (автоперенейм Godot, битые QA-поиски). Тосты делают правильно (RemoveChild+QueueFree) — остальное нет.
5. **GameWorldController — 1967 строк, 10+ ответственностей** (HUD/ввод/тосты/диалог/торговля/респавн/QA-хуки). Декомпозиция — 16-24 ч.
6. **RespawnAfterDeath — async void без try/catch** (GameWorldController:1634) — исключение = краш без лога.
7. **13 мёртвых DI-зависимостей (CS0414)** + 49 CS0649-шума от [Inject] (инъекция рефлексией) — реальные проблемы тонут в шуме; нужен suppression/анализатор.
8. **ParchmentTheme игнорируется:** 15 скопированных цветовых литералов, 30 StyleBoxFlat, 13 окон строят панель каждая по-своему — визуальный дрейф.
9. **Mutable public-поля на сервисах** (CombatService.IsInCombat, DialogueService.IsInDialogue, TechniqueService.*, TradeService.*) — инварианты боя/диалога не защищены. Контракты при этом образцовые.
10. **7 файлов >800 строк** (GameWorldController 1967, Constants 1551, Enums 1082, CombatService 946, TechniqueBookWindow 941, BodyService 939, TechniqueService 849).
11. **15 TODO**, из них функционально значимые: CombatService:352 (isRanged не влияет на CombatSubtype — ranged-урон может идти по melee-ветке!), ElementalEffectService:164,175 (knockback-событие не публикуется, chain-damage нет), BodyModuleServices:50 (EntityId NPC-тел захардкожен "player").
12. Дисциплина: 0 файлов не-PascalCase, 0 классов ≠ имени файла, 2 рассинхрона namespace↔путь (FormationRegistry, DialoguePresenter — ns Modules.UI при файле в Interaction!), CS0108 EventLogWindow.Name прячет Node.Name.

**Оценка техдолга: ~53–81 часов** (в деталях — у аудита-2c; приоритеты: 1) EventLog-фикс 1ч [сделано], 2) dirty-check HUD+InputAdapter ~4ч, 3) лог-гейты ~2ч, 4) QueueFree-хелпер 3-4ч).

---

## ЭТАП E. Что исправлено в этой сессии (код)

| # | Файл | Фикс |
|---|---|---|
| 1 | `Adapter/UI/EventLogWindow.cs` | **Anti-freeze:** `RemoveChild`+`QueueFree` вместо QueueFree-цикла (бесконечный цикл после 61-го события) |
| 2 | `Adapter/Scene/GameWorldController.cs` | **Модальность диалога/журналов:** SetOverUI теперь включает `_dialogueWindow.IsOpen`, `_questWindow.Visible`, `_eventLogWindow.Visible` — Space(атака)/Z(каст) подавляются, как требует HOTKEYS §9.2 |
| 3 | `Adapter/UI/HotkeysWindow.cs` | **Честная справка:** M/N/R/`/F5/F9 помечены «не реализовано» / «отключено (сейвы отложены)» вместо ложных обещаний |

Синхронизация документации (по прямому указанию пользователя отражены принятые решения):
HOTKEYS.md, CHEAT_PANEL.md, MODULE_STRUCTURE.md (+Trade, 17 модулей), SAVE_SYSTEM.md (статус Q8), DIALOGUE_SYSTEM.md (S6-реалии), TESTING_RULES.md (реестр 19 QA-хуков), **новый docs_v2/06_player/TRADE_SYSTEM.md**, docs_v2/README.md (индекс), UI_DESIGN.md (статусная сводка вью).

---

## Рекомендации (приоритеты следующего этапа)

**P0 (баги):**
1. ✅ EventLog-фриз — закрыто этой сессией.
2. TODO CombatService:352 — ranged-атаки по melee-ветке (проверить урон лука).
3. RespawnAfterDeath — обернуть try/catch.

**P1 (информативность — мандат пользователя):**
4. F5/F9 — добавить тост «Сейвы отключены (этап разработки)» вместо молчания (сейчас — тихий обман).
5. Мёртвые M/N/R/backquote — либо скрыть из InputMap, либо реализовать (минимально: N-миникарта уже есть в планах UI).

**P2 (архитектурная санация):**
6. Удалить мёртвый API: 5 C#-event'ов, StorageService-legacy, NPCSpawnPhase.cs, IGeneratorService.
7. IBodyFactory + IFormationGeneratorService → в Core; PlayerTechniqueCaster:290 — убрать даункаст (метод в IFormationService).
8. ChargerSlotConfig struct→class; развести PhaseOrder (AnimalSpawn→5b или перенумеровать).
9. QueueFree-хелпер; dirty-check HUD; лог-гейты (P2 качества).

**P3 (стратегия):**
10. Решить стартовый набор: START_LORE §5 ↔ StartingGearPhase (сейчас расходятся).
11. Подготовить split на 4 csproj (Core без Godot-референса) — когда появится окно стабильности.

---

*Аудит выполнен без изменения игровой логики (кроме 3 точечных фиксов UI-безопасности). Сборка: 0 errors. QA-регрессия: см. worklog.*

---

# ДОПОЛНЕНИЕ: санация по решениям пользователя (2026-09-06, сеанс 2)

Пользователь рассмотрел аудит и принял три решения:

1. **Стартовый набор** — временное решение для тестов в процессе разработки.
   **Оставляем как есть**, ещё неоднократно будет меняться.
   → Синхронизация START_LORE §5 ↔ StartingGearPhase отложена до стабилизации.
   Отражено: SAVE_SYSTEM.md (шапка-статус), P3-пункт 10 закрыт решением.

2. **Баги исправляем** (активный процесс разработки):
   - ✅ **P0-2 (CombatService:352)** — при верификации выяснилось: TODO был
     **устаревшим комментарием**, а не живым багом. Реализация isRanged→
     CombatSubtype.RangedProjectile существует с Phase 8 p2 (строки 480, 633-635),
     отдельная ranged-формула AGI/INT §4.2, QA CombatSim покрывает
     (ranged-фаза: RangedProjectile-subtyped dmg > 0 — PASS).
     Исправление = удалён ложный TODO, комментарий приведён к реальности.
   - ✅ **P0-3 RespawnAfterDeath** — async void обёрнут в try/catch +
     GD.PrintErr + тост (GameWorldController). Исключение в респавне больше
     не роняет игру молча.
   - ✅ **P1-4 F5/F9** — вместо тихого «ничего» честный тост
     «⏳ Сейвы отключены (этап разработки)» (Q8). Паттерн соседних тостов.
   - ✅ **P2-8 (частично) ChargerSlotConfig struct→class** — MODULE_STRUCTURE
     §0 «Config — class». ChargerSlot копирует поля в конструкторе —
     семантика не изменилась.

3. **Мёртвый API** — каждый пункт оценён: «заглушка под будущее» vs «ошибка
   реализации». Legasy-кода быть не должно.

   **УДАЛЕНО (ошибки реализации / легаси):**
   | Что | Вердикт |
   |---|---|
   | `NPCSpawnPhase.cs` (+.uid) | Мёртвый файл с Phase 5: не регистрируется (заменён Animal/Human/Group-фазами), только упоминания в комментариях. Легаси v1-stub. |
   | `Core/Interfaces/IGeneratorService.cs` (+.uid) | Core-контракт, 0 реализаций, 0 потребителей. Дизайн-рудимент, вытеснен конкретными сервисами генератора (ItemGeneratorService и др.). |
   | `Modules/Inventory/StorageService.cs` + `Core/Interfaces/IStorageService.cs` (+.uid) + `StorageType` enum + `InventoryModule._ringStorage/GetRingStorage()` | Легаси-цепочка Q9=B: dead store (создаётся, никем не потребляется), в DI не регистрируется, заменён живыми SpiritStorageService + StorageRingService. |
   | `SaveService.OnSaveCompleted/OnLoadCompleted` (C#-event'ы) | 0 подписчиков; дублируют живой контракт шины SaveCompletedEvent (публикует SaveModule). Межмодульные уведомления — только через шину (ARCHITECTURE). |
   | `TimeService.OnTick/OnTimeChanged` | 0 подписчиков; живой паттерн — опрос ITimeService; шина — TimeSpeedChangedEvent. «V1 stubs»-рудимент до-шинной эпохи. |
   | `WorldService.OnLocationChanged` | 0 подписчиков; чистое дублирование живого LocationChangedEvent (публикуется в той же строке кода). |

   **ОСТАВЛЕНО (заглушки под будущие реализации):**
   | Что | Обоснование |
   |---|---|
   | `IGameSession.OnStateChanged` | Entry-слой (не межмодульное — правило шины не нарушает). Живой стейт-машины наблюдатель для будущего UI (Loading-экран/меню). State-машина активна (гарды NewGame-реентри). |
   | PlayerInput: rest (R) / world_map (M) / minimap (N) / save (F5) / load (F9) | Ввод для запланированных систем (карты, отдых, сейвы). F1-справка честно помечает «не реализовано/отключено». |
   | `ContextMenuRequestedEvent` (контракт) | Будущее ПКМ-меню (UI_DESIGN). |
   | ElementalEffectService: ApplyKnockback / ApplyChain | Явные заглушки будущих фаз (Air-knockback, Lightning-chain, требуют IPositionService). |
   | BodyModuleServices TODO EntityId="player" | Живой конфиг + честная пометка параметризации (P2-03). |

   **Документация синхронизирована** (8 файлов): FILE_TREE (минус 3 файла,
   фазы 15), ARCHITECTURE (таблица фаз 10→актуальная, 14 строк + примечание),
   MODULE_STRUCTURE (Inventory-интерфейсы Q9=B), DI_AND_EVENTBUS (регистрации
   + INV-02 «суперседед»), GLOSSARY (−StorageType), ENVIRONMENT_CONCEPT
   (ISpiritStorageService), SAVE_SYSTEM (решение по стартовому набору + тост
   F5/F9), HOTKEYS (F5/F9 честный тост; заглушки сохранены решением
   2026-09-06), NPC_COMBAT_PREP (помечен историческим — план выполнен).

**Проверка:** build --no-incremental 0 errors (300 warn, −1);
QA-регрессия 11/11 PASS: COMBAT (melee+ranged+LOS/ammo), CHARGE, TOAST,
LOWHP, KILLFEED, HOTBAR, DAMAGEDIR, DIALOGUE, TRADEUX, TRADE smoke
(buy True/sell True), GEN (0 исключений C#; движковые shutdown-шумы не
считаются). Сборка сцены: «15 phases, 84 ms».

**Осталось из рекомендаций аудита (следующие окна):**
- P2-7: IBodyFactory + IFormationGeneratorService → в Core; убрать
  даункаст PlayerTechniqueCaster:290 (живые интерфейсы — перенос, не удаление).
- P2-9: QueueFree-хелпер (10+ UI-мест), dirty-check HUD/InputAdapter,
  лог-гейты рендереров (zero-GC, ~37 мест).
- P2-8 (остаток): развести PhaseOrder StartingGear/AnimalSpawn (5/5).
- Декомпозиция GameWorldController (1967→ строк).

