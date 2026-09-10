# Чекпоинт: R13 FULL-LOOT — верификация по документации + синхронизация docs_v2

**Дата:** 2026-09-10 07:30 UTC
**Сессия:** 2026-09-10 №2 (запрос пользователя: «прочитай START_PROMPT и связанные
документы, обнови SESSION_*, прочитай правила чекпоинтов, верифицируй задачу
согласно документации и планов docs_v2»)
**Тип:** verification + decision (docs sync)

---

## Контекст

Сессия R13 (09-10 №1) выполнила задачу пользователя (NPC спаун через генерацию,
доступ к инвентарю мёртвого NPC, full loot) и запушила (5f7e7e7, 7347bc1), но
старт сессии был неполным: не прочитаны START_PROMPT.md/SESSION_* перед работой
(указания пользователя были неполными). Данная сессия — корректный старт
(START_PROMPT + SESSION_CONTEXT + SESSION_SUMMARY + README) и верификация R13
по правилам docs_v2: «Документация — это спецификация. Любое расхождение кода
с документацией = баг» (AI_DEVELOPMENT_WORKFLOW §0).

## Что сделано

1. **Прочитаны входные документы:** START_PROMPT.md (правила + запреты),
   SESSION_CONTEXT.md, SESSION_SUMMARY.md (обнаружено отставание на 09-08…09-10),
   README.md, docs_v2: AI_DEVELOPMENT_WORKFLOW, TESTING_RULES, DEATH_AND_LOOT,
   NPC_ASSEMBLY_PIPELINE §8, MODULE_STRUCTURE §2.7, DI_AND_EVENTBUS §2.3.
2. **Функциональная верификация R13** (TESTING_RULES §0.2 конвейер):
   - `dotnet build` — 0 errors (348 warnings — базовый уровень R13);
   - GODOT_LOOT_DEBUG=1 → VERDICT: PASS (генерация состава 12 NPC/6 ролей/
     детерминизм, труп-контейнер, окно, SlotId-взятие, double-take отказ,
     full loot, TTL, ReinforcementTick);
   - Регрессии: COMBAT_SIM, SAVELOAD, CONTEXT, REASSEMBLY, KILLFEED,
     TRASHDROP, STORAGE, QUEST — все VERDICT: PASS (итого 9/9).
3. **Верификация соответствия спецификации** — найденные и устранённые
   расхождения (см. «Найденные проблемы»).
4. **SESSION_CONTEXT.md / SESSION_SUMMARY.md переписаны** под состояние
   2026-09-10 (R13, верификация, актуальные пути окружения Godot flat,
   правильные команды QA).
5. **Ответ на вопрос о правилах чекпоинтов** (см. «Решения»).

## Решения

- **Правила чекпоинтов живут в START_PROMPT.md §6, отдельного файла правил в
  checkpoints/ НЕ существует и никогда не существовал** — проверено полной
  git-историей имён файлов checkpoints/ (только ММ_ДД_описание.md, plans/,
  worklog-бэкапы). Это дизайн: START_PROMPT читается первым на каждой сессии,
  поэтому правила гарантированно в контексте до создания любого чекпоинта;
  файл правил внутри checkpoints/ был бы избыточен и мог бы быть пропущен.
- **docs_v2 синхронизированы с реализацией R13** (workflow §3.3: расхождение
  код↔доки = баг → обновить документацию с обоснованием):
  - DEATH_AND_LOOT.md — §1.2/§1.3/§2/§5/§6 переписаны под трупы-контейнеры
    (старая схема «CombatLootService → случайный дроп 1-3» удалена; причина
    R13 зафиксирована в шапке);
  - MODULE_STRUCTURE.md — таблица §1 (NPC: +ICorpseService, 8+ сервисов),
    §2.7 (NPCSpawnCompositionService, CorpseService), таблица событий
    (+CorpseCreated/Removed/Looted);
  - DI_AND_EVENTBUS.md — реестр контрактов: +CorpseContracts (3 события);
  - TESTING_RULES.md §0.1 — реестр env-хуков 25→30 (+LOOT, +SAVELOAD,
    +CONTEXT_HOLD, +SCREENSHOT_MENU, +SCREENSHOT_SPLIT).
- **Замороженное решение №5 («документация первична»)** уточнено в
  SESSION_CONTEXT §4-5: редактирование docs_v2 допускается при обнаруженном
  расхождении с обязательным обоснованием и фиксацией причины (прецедент —
  эта синхронизация).

## Найденные проблемы

| # | Проблема | Влияние | Статус |
|---|----------|---------|--------|
| 1 | DEATH_AND_LOOT.md описывал старый случайный дроп; конфликт с NPC_ASSEMBLY_PIPELINE §8.1 «полный лут» | Спецификация вводила в заблуждение при верификации | ✅ Исправлено (доки синхронизированы) |
| 2 | MODULE_STRUCTURE/DI_AND_EVENTBUS не знали CorpseService/CorpseContracts/NPCSpawnCompositionService | Реестр сервисов/контрактов неполный | ✅ Исправлено |
| 3 | TESTING_RULES §0.1 реестр отставал: 5 хуков не документированы (LOOT R13, SAVELOAD R11, CONTEXT_HOLD/SCREENSHOT_MENU/SPLIT R7) | Нарушение «новая фича = новый хук» | ✅ Исправлено (25→30) |
| 4 | SESSION_CONTEXT/SESSION_SUMMARY отставали на 09-08…09-10 (состояние 08-28) | Неверный контекст старта сессий | ✅ Переписаны |
| 5 | Баланс: камни в трупах по «таблице 4.1» щедрее, чем §8.2 NPC_ASSEMBLY_PIPELINE (0-1 @L3+ 10%) — это разные таблицы (death-drop vs инвентарь живого NPC), обе оставлены; возможное суммирование при обыске | Геймплейный баланс | ⚠ Наблюдение — живой QA на ПК (P0) |
| 6 | «[ahead 2]» локального git оказалось устаревшим tracking-ref (ls-remote: origin/main == 7347bc1 == HEAD) | Косметика | ✅ git fetch обновил |

## Следующие шаги

- P0: живой визуальный QA R13 на ПК (маркеры трупов/LootWindow) + баланс камней.
- Лут с животных (материалы по видам) — TODO помечено в DEATH_AND_LOOT §5.
- Обыск трупов ИИ-NPC; генерация состава для 500×500.
- Рекомендация: на старте каждой сессии читать START_PROMPT → SESSION_CONTEXT
  (теперь актуальны); при расхождении код↔доки — синхронизировать с обоснованием.

## Файлы

- Обновлены: docs/docs_v2/04_entities/DEATH_AND_LOOT.md,
  docs/docs_v2/01_architecture/MODULE_STRUCTURE.md,
  docs/docs_v2/01_architecture/DI_AND_EVENTBUS.md,
  docs/docs_v2/09_workflow/TESTING_RULES.md,
  SESSION_CONTEXT.md, SESSION_SUMMARY.md.
- Создан: checkpoints/09_10_r13_verification.md (этот файл).
