# АУДИТ: Interaction (диалоги, E-взаимодействие, presenter-слой)

**Дата:** 2026-09-11. **HEAD:** `d0b065d` (READ-ONLY, код не менялся)
**Scope:** Modules/Interaction/ — все 9 файлов полностью (~1.4k строк):
DialogueService (533), DialoguePresenter (133), DialogueTypewriter (117),
InteractionService (195), InteractionConfig/Module/ModuleServices,
Data/DialogueNode, Data/DialogueChoice. Стыки: DialogueWindow (351, полн.),
GWC (E-путь 1676-1735, HandleNpcTalk 2195-2236, Esc 1537-1543,
OnDialogueEnded 1978-1984, SetOverUI 1711-1726), DialogueContracts/
UIContracts, NPCService.OnNPCInteracted, QuestService.OnQuestStartRequested,
GameBoot._PhysicsProcess, docs_v2/06_player/DIALOGUE_SYSTEM.md.

---

## Сводка

Живое ядро диалогов чисто: дерево (узлы/выборы/линейные переходы), квест-линк
(QuestStartRequestedEvent до перехода к узлу), sentinel «open_trade» с
корректным порядком EndDialogue→TradeRequestedEvent, модальность (пауза
тиков) с единой точкой резюма, typewriter без double-tick. Мёртв весь
«инфраструктурный» слой модуля: InteractionService (реестр интерактивных
объектов пуст навсегда), DialoguePresenter (не зарегистрирован в DI) и обе
их подписки в DialogueService. Находок: P2×2, P3×5, OK×1.

---

## Находки

### INT-1 [P2] InteractionService — мёртвый сервис: реестр интерактивных объектов пуст всегда

**Файл:** `InteractionService.cs:57, 146-158, 173-177, 111-131`.
**Сценарий:** `_interactablePositions` наполняется только через
RegisterInteractable/UnregisterInteractable — rg по game/src: **0
вызывателей** (фиктивный registry удалён Review-этапом 6, реальные владельцы
не появились). Цепочка: GetNearestInteractableId → null → OnUIInteractRequest
early return (175) → TryInteract false → `InteractionCompletedEvent` — 0
живых публикаций → подписка DialogueService.OnInteractionCompleted
(`DialogueService.cs:283-292`) мертва; `UIInteractRequestEvent` — 0
паблишеров (только UIContracts.cs:26). Рабочий E-путь целиком в GWC
(HandleCorpseSearchOrNpcTalk/HandleNpcTalk/HandlePickup). Подтверждает и
расширяет вывод Core-аудита «InteractRequestEvent мёртв»: мёртв ВЕСЬ сервис.
**Фикс:** удалить InteractionService+InteractionConfig из DI и подписку из
DialogueService, либо перенести туда реальный E-путь из GWC.

### INT-2 [P2] DialoguePresenter не зарегистрирован в DI — Q13-E02 FIX не работает

**Файл:** `DialoguePresenter.cs` (весь); `DialogueService.cs:41-42, 109-110`;
`InteractionModuleServices.cs:14-29`.
**Сценарий:** rg `DialoguePresenter` — упоминания только внутри самого файла:
нет регистрации ни в InteractionModuleServices, ни в UIModuleServices. Его
паблишеры `UIAdvanceDialogueRequestEvent`/`UISelectChoiceRequestEvent` —
единственные в репо — не срабатывают → подписки DialogueService (Q13-E02
FIX, 109-110) — мёртвый код. Факт-путь: DialogueWindow инжектит
`DialogueService` напрямую (`DialogueWindow.cs:33`) и зовёт
AdvanceDialogue()/SelectChoice() — EVT-01 («UI не знает IDialogueService»)
де-факто нарушен Adapter'ом. `DialogueChoiceSelectedEvent` тоже без живых
подписчиков (единственный — мёртвый презентер, `DialoguePresenter.cs:121`).
**Фикс:** зарегистрировать презентер и перевести окно на RequestAdvance/
RequestChoice, либо удалить презентер + мёртвые подписки + 2 контракта.

### INT-3 [P3] OnNPCInteracted («AI-инициированный talk») — недостижимая ветка

**Файл:** `DialogueService.cs:268-277`; `GameWorldController.cs:2217-2230`.
**Сценарий:** NPCInteractedEvent публикует только NPCService.OnNPCInteracted,
чей единственный игровой вызыватель — GWC ПОСЛЕ успешного
TryStartNpcDialogue → в обработчике `_isInDialogue == true` → early return.
«AI-инициированный talk» (комментарий 102) не существует. Балласт с ложной
документацией. **Фикс:** удалить подписку или сделать реальный AI-talk-паблишер.

### INT-4 [P3] DialogueChoice.ConditionId — мёртвое поле

**Файл:** `Data/DialogueChoice.cs:22`; `DialogueService.cs:164-209`.
SelectChoice не читает ConditionId, UI не фильтрует недоступные варианты.
Задокументировано «будущее расширение» — заготовка-ловушка при чтении
контракта. **Фикс:** пометить UNIMPLEMENTED или удалить.

### INT-5 [P3] Sentinel «open_trade» — magic string без константы

**Файл:** `DialogueService.cs:180, 422`. Литерал в двух местах (обработчик +
дефолтный диалог торговца); опечатка в одном = молча неработающий вход в
лавку (GoToNode лишь логирует «узел не найден»). InteractionType имеет
GameConstants — sentinel аналогично просится туда. **Фикс:** константа.

### INT-6 [P3] Выбор клавишами 1-9 доступен до завершения печати текста

**Файл:** `DialogueWindow.cs:320-349`. `_Input` не гейтован
IsTypewriterComplete — можно выбрать ответ, не видя полной реплики; дока
§5.2 обещает «после полного текста — варианты активны». Мягкий UX-дефект.
**Фикс:** гейт или CompleteImmediately при нажатии цифры.

### INT-7 [P3] Doc-drift DIALOGUE_SYSTEM.md §1.1/§6

**Файл:** `docs_v2/06_player/DIALOGUE_SYSTEM.md:19-20, 123-131`.
§1.1 «InteractionRequestEvent → InteractionService открывает диалог» — путь
мёртв (INT-1); реальный — GWC→GetNearbyNPCIds→TryStartNpcDialogue. §6:
InteractionCompletedEvent «слушают QuestModule, NPCModule» — 0 живых;
DialogueStartedEvent «→ TimeService (pause)» — паузу ставит GWC напрямую
`Time.Pause()` (GWC:2232-2233), GamePausedEvent в диалоге не публикуется.
Шапка дока (2026-09-06) верна по дистанции 2.5/клавишам 1-9/S6, но §1.1/§6
неактуальны. **Фикс:** переписать под GWC-путь.

### INT-8 [OK-BY-DESIGN] Типизация целей — string NodeId + sentinel

TargetNodeId — строка; null/"" = конец; GoToNode при несуществующем узле
мягко логирует и остаётся на месте (`DialogueService.cs:312-323`, не краш).
Для хардкод-диалогов V1 приемлемо.

---

## Проверено чисто

- **Пауза/резюм:** единая авторитетная точка DialogueEndedEvent →
  GWC.OnDialogueEnded (1978-1984) закрывает окно и резюмит тики; все пути
  (E-advance, Esc 1537-1543, кнопка/клик по панели, выбор-выход) идут через
  Close()/EndDialogue; двойной Resume идемпотентен.
- **Typewriter без double-tick:** InteractionModule.Tick — только при
  !IsPaused (GameBoot.cs:100-101), DialogueWindow._Process — только при
  IsPaused (DialogueWindow.cs:304-305); S6-BUGFIX корректен;
  CompleteImmediately-иерархия верна.
- **Повторные входы:** StartDialogue гейт `_isInDialogue` (119);
  Initialize с dispose-паттерном подписок (B01-fix, 97-100); Dispose чист.
- **Квест-линк:** QuestIdsToStart → QuestStartRequestedEvent ДО перехода
  (191-198); гейты принятия — на стороне QuestService (аудит Quest чист);
  повторное принятие невозможно.
- **open_trade-порядок:** EndDialogue (резюм) СТРОГО до TradeRequestedEvent
  (пауза лавки) — комментарий 175-179 верен.
- **E-путь:** TalkRangeTiles=2.5f Чебышёв (GWC:288, 2200-2214), только
  живые NPC (2209); труп ближе — обыск (2170-2177); «Нечего сказать» для
  NPC без диалога (2220).

---

## Соответствие docs_v2

| Пункт | Код | Статус |
|---|---|---|
| §1.1 InteractionRequestEvent-путь | мёртв (INT-1) | DRIFT |
| Шапка: 2.5 тайла, 1-9, S6-печать | GWC:288, DialogueWindow:326 | OK |
| §5.2 «активны после полного текста» | нет гейта (INT-6) | DRIFT minor |
| §6 слушатели InteractionCompleted/DialogueStarted | 0 живых / Time.Pause напрямую | DRIFT |
| §2 7 ролей (Student нет) | 6 диалогов в RegisterDefaultDialogues | OK (шапка признаёт) |

*Аудит Interaction завершён. Функциональное ядро чисто; инфраструктурный
слой (InteractionService/DialoguePresenter) — мёртвый балласт, сбивающий и
доку, и читателя кода. Приоритет: санация INT-1/INT-2/INT-3.*
