# Чекпоинт: внешнее ревью этапы 2–7 — ФИНАЛИЗАЦИЯ (2026-09-08)

> Завершение цикла исправлений по внешнему аудиту ChatGPT (файл `upload/запрос 2026_09_08_17_xx.txt`).
> База цикла: `ada5f74` → итоговый HEAD: см. git log (все коммиты ниже запушены в origin/main).
> Регламент: валидация → фикс → build 0 errors → QA → commit + push (после каждого этапа).

## 1. Сводная таблица всех находок (этап → вердикт → коммит)

### Этап 2 — ревью №1 (генерация/сейвы/pipeline/подписки) — `3e41927`
| Находка | Вердикт | Детали |
|---|---|---|
| P1: PreGen не генерирует по грейдам, засоряет реестр | ✅ исправлено | `BuildSpecified(type,grade)` без авто-регистрации; регистрация только валидных+уникальных; Clear реестра |
| P1: quicksave грузит не тот тип слота | ❌ отклонено (ложное) | SaveService маршрутизирует по slot.Name; мёртвый `SaveSlot.FileName` удалён |
| P1: pipeline игнорирует CanExecute/State/SkipOnLoad | ✅ исправлено | RunAssembly(ct, mode): Reset/CanExecute/MarkAs*/SkipOnLoad, честный SceneReadyEvent |
| P1: утечка подписок EventBus | ✅ исправлено | все 12 токенов освобождаются в `_ExitTree` |
| P2: Order 5/5 коллизия фаз | ✅ исправлено | фазы 1..15 уникальны |
| P2: ComparePhaseOrder мёртв | ✅ удалён | |
| P2: ISceneAssemblyLogger Unity-легаси | ✅ удалён | |

### Этап 3 — бой — `91dea48`
| Находка | Вердикт | Детали |
|---|---|---|
| P0: атаки вне своего хода (turn-gate не авторитетен) | ✅ исправлено | `_currentTurnOwnerId` — авторитетный гейт; инициатива у инициатора; тайм-аут 2.5с |
| P0: два NPC-AI механизма, фантомный «enemy» | ✅ исправлено | CombatAIService + AIPersonality УДАЛЕНЫ; NPC-атаки через NPCModule.ProcessNpcAttacks |
| P1: стрела списывается до валидации | ✅ исправлено | HasRangedAmmo до боя + TryConsumeRangedAmmo после Accepted |
| P1: чужой NPC переключает цель глобального боя | ✅ исправлено | целью управляет только игрок, 1v1 зафиксирован |
| P2: NPCCombatAdapter.StartAttack — misleading API | ✅ исправлено | → MarkNpcCombatStarted |

### Этап 4 — инвентарь — `aed371b`
| Находка | Вердикт | Детали |
|---|---|---|
| P0: TryRetrieve удаляет предмет не возвращая | ✅ исправлено | резолв ItemData ДО мутации; unknown → false, предмет не теряется |
| P1: нет stacking, портится кэш количества | ✅ исправлено | всегда ищем неполный стек; RecalcCountCache |
| P1: StorageRing не списывает Ци | ✅ исправлено | проверка баланса ДО мутации, списание ПОСЛЕ успеха |
| P1: крафт при полном инвентаре теряет результат | ✅ исправлено | overflow выбрасывается на землю; результат валидируется до расхода |
| P1: pickup unknown itemId теряет предмет | ✅ исправлено | валидация ДО удаления, остаётся на земле |
| P2: граница дистанции pickup `<` → `<=` | ✅ исправлено | |

### Этап 5 — Ци/формации/DoT — `31d53da`
| Находка | Вердикт | Детали |
|---|---|---|
| P0: NPC-вклад списывает Ци игрока | ✅ исправлено | EntityId явный во всех QiConsumeRequestEvent |
| P0: формацию всегда оплачивает игрок | ✅ исправлено | контур платит кастер (игрок — событие, NPC — provider) |
| P1: проверка/списание Ци не атомарны | ✅ исправлено | пул пополняется только по подтверждённому списанию |
| P1: DoT только логируется | ✅ исправлено | реальный урон через DamageAppliedEvent (тело/смерть/kill-feed) |
| P2: кэш уровня культивации не синхронизируется | ✅ исправлено | синхронизация из QiChangedEvent при инициализации |

### Этап 6 — мир/тайлы — `57a73fb`
| Находка | Вердикт | Детали |
|---|---|---|
| P0: ресурсы не восстанавливаются | ✅ исправлено | TileService подписан на ResourceRespawnedEvent, полная пересборка тайла |
| P1: TryTravel меняет ID без смены мира | ✅ исправлено | честный false без смены CurrentLocationId |
| P1: InteractionService — фиктивный registry | ✅ исправлено | статический registry и prefix-эвристики удалены; реальный E-путь |
| P2: TimeChangedEvent.Delta = 1/60 | ✅ исправлено | Delta = ITimeService.DeltaTime |
| P2: TryPickup out всегда null | ✅ исправлено | → честная команда RequestPickup(itemId) |

### Этап 7 — квесты — `97cee42`
| Находка | Вердикт | Детали |
|---|---|---|
| P0: TargetId квестов не совпадают с реальными событиями | ✅ исправлено | волки: animal_{species}_{n} → species; руда: material_iron_ore; travel-квест НЕ регистрируется (переход не реализован — этап 6); старейшина: NPCRole.Elder + матч по роли |
| P1: награда «steel» не существует | ✅ исправлено | material_steel_ingot (регистрируется StartingGearPhase); валидация наград через IItemDatabaseService |
| P1: диалог не запускает квест | ✅ исправлено | DialogueChoice.QuestIdsToStart + QuestStartRequestedEvent — оба квеста через реальный диалог |
| P2: QuestConfig auto-generation — мёртвая | ✅ удалено | |
| P2: RequiredCultivationLevel не проверяется | ✅ исправлено | гейт в StartQuest (кэш уровня из QiChangedEvent) |

**Итого: 34 находки — 33 исправлено, 1 отклонена с доказательством (quicksave, этап 2).**

## 2. Полная QA-регрессия (финализация, все 17 прогонов PASS)

| # | Хук | Результат |
|---|---|---|
| 1 | GODOT_COMBAT_SIM (melee+ranged+LOS/ammo+turn-gate) | PASS |
| 2 | GODOT_CHARGE_SIM | PASS |
| 3 | GODOT_TOAST_DEBUG | PASS |
| 4 | GODOT_LOWHP_DEBUG | PASS |
| 5 | GODOT_KILLFEED_DEBUG | PASS |
| 6 | GODOT_HOTBAR_DEBUG | PASS |
| 7 | GODOT_DAMAGEDIR_DEBUG | PASS |
| 8 | GODOT_DIALOGUE_DEBUG | PASS |
| 9 | GODOT_TRADEUX_DEBUG | PASS |
| 10 | GODOT_TRADE_DEBUG (smoke buy/sell) | True/True |
| 11 | GODOT_REASSEMBLY_DEBUG | PASS (15/15 Completed, реестр не ×2) |
| 12 | GODOT_STORAGE_DEBUG (этап 4) | PASS |
| 13 | GODOT_DOT_DEBUG (этап 5) | PASS |
| 14 | GODOT_RESPAWN_DEBUG (этап 6) | PASS |
| 15 | GODOT_QUEST_DEBUG (этап 7) | PASS (accept→event→complete→reward) |
| 16 | GODOT_FORMATION_TEST | Active 800/800 |
| 17 | GODOT_GEN_DEBUG | 0 исключений |

`dotnet build`: **0 errors** (327 warnings — техдолг категории P2, известен по аудиту 09-06).

## 3. Синхронизация документации

- TESTING_RULES §0.1: **24 хука** (было 20; +STORAGE/DOT/RESPAWN/QUEST) — обновлено в коммитах этапов 4–7.
- FILE_TREE / MODULE_STRUCTURE / COMBAT_SYSTEM: удаление CombatAIService + AIPersonality отражено; добавлен раздел **COMBAT_SYSTEM §1.4 «Модель боя 1v1 и владение ходом (MVP)»** — фиксация модели до encounter-фазы.
- Квест-конфиги (semantic targets, реальный elder, отключение travel-квеста): документированы в планах/чекпоинтах этапа 7 и хуке GODOT_QUEST_DEBUG; отдельного QUEST_SYSTEM.md в docs_v2 нет (правило №6 — не расширять docs_v2 без указания).
- Заглушки будущих фаз явно помечены: encounter-модель боя, travel-pipeline, SceneAssemblyCompletedWithErrorsEvent (оставлена как контрактная заглушка).

## 4. Отложено / приоритеты следующего этапа

1. **Encounter-модель боя** (групповой бой) — сейчас строго 1v1 MVP.
2. **Travel pipeline** — физический переход между локациями (TryTravel честно возвращает false).
3. **Процедурная генерация квестов** — авто-конфиги удалены, квесты статические (3 активных: волки/руда/старейшина).
4. Warnings 327 (P2 техдолг: null-предупреждения, god-class GameWorldController, zero-GC нарушения) — санация по отдельному указанию.
5. DoT-эффекты применяются к игроку и NPC, но стихийные проки (chain/knockback) — заглушки будущих фаз.

## 5. Коммиты цикла (все в origin/main)

```
3e41927 fix(review-1)  — валидация ревью №1: 6/7 исправлено, 1 отклонено
ada5f74 worklog R4-REVIEW1
2223fca plans           — мастер-план этапов 2-7 + план финализации
91dea48 fix(review-3)  — combat authority
aed371b fix(review-4)  — inventory transactions
31d53da fix(review-5)  — qi transactions + DoT + cultivation sync
57a73fb fix(review-6)  — world/tile: respawn, honest travel, interaction
97cee42 fix(review-7)  — quests: semantic targets, rewards, dialogue link
(финализация — текущий коммит: доки 1v1 + чекпоинт + статусы планов)
```
