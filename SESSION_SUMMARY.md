# Сводка сессий (обновляется при завершении каждой сессии)

Обновлено: 2026-09-10 07:30 UTC (облачный агент Z.ai Code)

## Проект
Cultivation World Simulator (Ai-game4), Godot 4.7.1 .NET, C#
Репозиторий: https://github.com/vivasua-collab/Ai-game4 (публичный)
HEAD: 7347bc1 == origin/main

---

## Последние сессии

### 2026-09-10 (R13 full-loot + верификация)
- **R13 (5f7e7e7):** NPC спаун через генерацию (NPCSpawnCompositionService:
  состав от типа локации+DangerLevel+сид, кап 12, ReinforcementTick
  восполняет потери <60%); трупы-контейнеры (CorpseService: снапшот
  экипировки+инвентаря+камней 4.1, TTL 1 день, SlotId-адресно);
  full loot (LootWindow: E-обыск, «Забрать всё», Esc-авторитет);
  ClassicLootSeeder фикс фантомных ID.
- **Верификация (запрос пользователя):** QA 9/9 хуков PASS + build
  0 errors; **docs_v2 синхронизированы**: DEATH_AND_LOOT (трупы вместо
  случайного дропа), MODULE_STRUCTURE (NPC: +Corpse/+Composition),
  DI_AND_EVENTBUS (+CorpseContracts 3), TESTING_RULES §0.1 (реестр
  25→30 хуков). SESSION_CONTEXT/SESSION_SUMMARY обновлены.
- Ответ про чекпоинты: правила — START_PROMPT §6 (файла правил в
  checkpoints/ никогда не было, git-история подтверждена).

### 2026-09-09 (R10 SlotId + R11 Save/Load)
- R10 (756fd79): SlotId-адресность инвентаря (TOCTOU-защита кучек),
  ПКМ-меню (свойства/разделение стака/выброс).
- R11 (1fd5ba9): Save/Load — типизированный round-trip (IncludeFields),
  честные события, World biome/danger; анти-тривиальный QA.

### 2026-09-08 (внешнее ревью этапы 1-7)
- 34 находки исправлены: combat authority, inventory transactions,
  qi transactions + DoT, world/tile respawn, quests semantics.
  QA 17/17 PASS, доки 1v1 (3e41927…80f4f25).

### 2026-09-10 утро (R12: восстановление после ночного сброса)
- Клон без токена (репо публичный), dotnet 9.0.318, Godot
  /home/z/godot_flat/godot (flat-структура), restore_env.sh починен
  (имя ассета с подчёркиванием), QA PASS.

---

## Текущее состояние игры

### Что работает
- ✅ Мир/биомы/harvest/ground items; инвентарь+кукла+пояс+ПКМ-меню
- ✅ Бой: melee+ranged+LOS+ammo, turn-gate, визуал (цифры/HP/kill-feed)
- ✅ **Full loot R13:** генерация населения, трупы, обыск, респаун
- ✅ Save/Load; торговля; Qi/техники/формации/зарядники; квесты
- ✅ Генераторы + легендарки + верификация/дедуп

### Что НЕ работает (отложено)
- ❌ Визуал R13 живьём (Xvfb-скриншот/ПК); баланс камней в трупах
- ❌ Лут с животных (материалы TODO — только камни); faction port
- ❌ Per-attacker pending; обыск трупов ИИ; состав для 500×500

---

## Замороженные решения (НЕ нарушать)

- Godot 4.7.1 .NET; Qi = long; Permil integer math
- Документация первична: docs_v2 ≠ код = баг → синхронизировать
  с обоснованием (прецедент: R13 sync 2026-09-10)
- НЕ запускать Next.js DEV сервер (остановлен по запросу 09-10)

---

## Следующие шаги
1. P0: живой визуальный QA R13 (ПК/Xvfb); баланс камней
2. Лут с животных (материалы по видам)
3. Обыск трупов ИИ-NPC; faction port; 500×500 состав

---

## Предупреждения
- Godot: **/home/z/godot_flat/godot** (flat, не старый вложенный путь)
- QA-хуки поштучно, timeout ≥90с, вердикт grep 'VERDICT' (реестр —
  TESTING_RULES §0.1, 30 хуков)
- Токен: /home/z/my-project/.auth/github.token (не запрашивать, если есть)
- После сброса песочницы: recover_sandbox.sh; сверять git ls-remote
