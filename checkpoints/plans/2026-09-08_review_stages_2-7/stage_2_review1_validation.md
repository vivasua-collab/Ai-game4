# Этап 2 — Результаты первого ревью (генерация техник, quicksave, pipeline, подписки)

> Статус: ✅ **ВЫПОЛНЕН и запушен** в сессии R4-REVIEW1 (2026-09-08, коммиты 3e41927 + ada5f74).
> Полный отчёт: `checkpoints/09_08_external_review_validation.md`.
> Этот файл фиксирует план и результат для полноты мастер-плана этапов 2–7.

## Находки ревью и вердикты

| # | Находка | Вердикт | Фикс |
|---|---|---|---|
| P1-1 | PreGenTechniquePhase не генерирует по грейдам, засоряет реестр невалидными/дубликатами | ✅ Подтверждено | `BuildSpecified(type, grade, ...)` без авто-регистрации; PreGen регистрирует только валидные+уникальные; WorldInit очищает реестр |
| P1-2 | quicksave загружается через SaveSlotType.Manual | ❌ ЛОЖНОЕ | SaveService маршрутизирует по `slot.Name` — quicksave.json в обоих случаях. Причина ошибки ревьюера — мёртвое поле `SaveSlot.FileName` (сбивает с толку) → удалено |
| P1-3 | Контракт pipeline фаз не работает: CanExecute/State/SkipOnLoad игнорируются, skipped всегда 0 | ✅ Подтверждено | Оркестратор `RunAssembly(ct, SceneAssemblyMode)`: Reset/CanExecute/MarkAs*/SkipOnLoad, честный SceneReadyEvent |
| P1-4 | Утечка подписок EventBus (12 создано, 2 освобождены) | ✅ Подтверждено | `_ExitTree` диспозит все 12 токенов подписок |
| P2-1 | Порядок фаз с Order=5 (AnimalSpawn/StartingGear) зависит от обхода Dictionary | ✅ Подтверждено | Фазы 1..15 уникальны |
| P2-2 | ComparePhaseOrder — мёртвый метод | ✅ Подтверждено | Удалён |
| P2-3 | ISceneAssemblyLogger — Unity-легаси без реализации | ✅ Подтверждено | Удалён |

## QA

- 12/12 PASS + новый хук `GODOT_REASSEMBLY_DEBUG` (повторная сборка мира: 15/15 Completed, реестр не дублируется).
- Хуки: 19 → 20 (TESTING_RULES §0.1 обновлён).

## Коммиты

- `3e41927` — валидация + фиксы.
- `ada5f74` — worklog.
