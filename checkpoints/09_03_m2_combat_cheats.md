# Чекпоинт: M2 — физическая боевка (кулдаун атак, подтипы) + чит-меню (настройки + расширение)

**Дата:** 2026-09-03 02:35 UTC
**Сессия:** web-6be67126 (основная сессия)
**Тип:** implementation + fix

---

## Контекст

Новые указания пользователя (2026-09-03): основной приоритет MVP — физическая
боевка, дополнительный — техники; сейвы отложены (ломаются от изменений,
вместо них — стартовая генерация); чит-меню изучить/расширить и сделать
отключаемым в настройках; регулярные пуши и чекпоинты (окружение может упасть).

Аудит физбойки по COMBAT_SYSTEM.md выявил:
1. **P0: у игрока НЕТ кулдауна базовой атаки** — PlayerCombatAdapter.Tick
   публиковал AttackIntentEvent каждый физ-кадр при удержании Space (~60/сек):
   спам AttackRejectedEvent, спам тостов «⚔ Атака!», атака быстрее спеки §8.1
   (базовая атака = 1 игроминута = 1 сек на Normal). У животных кулдаун был
   (3 тика), у human NPC — тоже. Только игрок — без.
2. **P1: подтип basic_attack всегда MeleeStrike** — при экипированном оружии
   подтип врал в последствиях (кровотечение slashing/piercing не триггерилось).
3. **Чит-меню**: без рантайм-выключателя (только #if DEBUG при сборке).

## Что сделано

### Физическая боевка (основной приоритет)
- **PlayerCombatAdapter.cs**: кулдаун базовой атаки по §8.1-8.2:
  - `BaseAttackCooldownSec = 1.0` (1 игроминута на Normal);
  - `AttackCooldownSeconds() = 1.0 / (1 + AGI × 0.01)` — ловкость ускоряет
    базовые атаки (§8.2, только базовые — касты техник по-прежнему от
    проводимости);
  - кулдаун ставится только на УСПЕШНЫЙ интент (цель найдена) — удар
    «вхолостую» не блокирует следующий;
  - AGI — через IStatProvider (StatProviderAdapter, per-entity).
- **CombatService.cs**: подтип basic_attack при оружии в главной руке →
  `MeleeWeapon` (было всегда `MeleeStrike`). Влияет на последствия
  (кровотечение slashing/piercing) и консистентно с useWeaponDamage.
  TODO isRanged (Phase 8 ч.2: луки → RangedProjectile) — оставлен, требует
  ammo + ProjectileRenderer.
- **GameWorldController.cs**: убран polling-тост «⚔ Атака!» (спам 60/сек);
  вместо — подписка на AttackRejectedEvent (C-5 аудита-3) → тост причины
  («Каст уже идёт: …») только для атак ИГРОКА (NPC-отклонения не шумят).

### Чит-меню: отключаемость в настройках (требование пользователя)
- **Adapter/Persistence/GameSettings.cs (новый)**: рантайм-настройки,
  user://settings.json (вне git, переживает перезапуски), минимальный JSON
  без зависимостей. `CheatsEnabled` (default true — dev-сборка).
- **MainMenuController.cs**: OnSettings (был stub «Settings (stub)») →
  модальное окно настроек: CheckButton «Чит-меню разработки (F2)»,
  мгновенное сохранение, стиль в тон HotkeysWindow (тёмный Old School).
- **GameWorldController.cs (F2-гейт)**: при CheatsEnabled=false — тост
  «Читы отключены», панель не открывается. Двойная защита: #if DEBUG
  (release не содержит класс) + рантайм-гейт (dev-сборка «без читов»).

### Чит-меню: расширение (секция «Физическая боевка (M2)»)
- **«Полное исцеление»** — все части тела до Max (Red+Black) через
  IBodyService.HealPart: повторные боевые тесты без ожидания регенерации.
- **«Мишень-бандит»** — спавн human NPCRole.Enemy в 2 тайлах от игрока
  (NPCSpawnerService.SpawnNPC): мишень для тестов рядом, не искать по карте.

## Решения

- Кулдаун только на успешный интент — не наказывать игрока за удары в пустоту.
- Подтип MeleeWeapon для basic_attack с оружием — согласовано с уже
  существующим useWeaponDamage (урон оружия уже считался, подтип врал).
- Настройки: user:// вместо data/ — персистентность вне git-репо; минимальный
  ручной JSON-парс (один bool) — без System.Text.Json (латентный баг IncludeFields
  из worklog 2026-08-28 не касается user-файлов, но лишняя зависимость не нужна).
- CheatPanel инжектит IBodyService и NPCSpawnerService напрямую (не через
  интерфейс INPCSpawnerService) — паттерн уже используется в панели
  (FormationGenerator, Dedup, TechniqueRegistry — конкретные классы).

## Найденные проблемы

- **Расхождение с докой**: CHEAT_PANEL.md не описывает новую секцию «Физическая
  боевка» и окно настроек. docs_v2 заморожены без указания пользователя —
  обновить CHEAT_PANEL.md отдельной правкой после разрешения (расхождение
  зафиксировано здесь).
- Аномалия ФС жива: `timeout`-execve Godot периодически ENOENT (обход:
  literal path + env, повтор через паузу). Headless прогоны не завершаются
  сами — критерий PASS = ключевые строки лога (exit 124 — норма).

## Следующие шаги (по приоритетам пользователя)

1. Физбойка дальше: TODO isRanged → CombatSubtype (лук = RangedProjectile,
   Phase 8 ч.2: IAmmoService + ProjectileRenderer); knockback/chain lightning
   stubs в ElementalEffectService (ближе к техникам).
2. Боёвка техниками (доп. приоритет): каст Z/X, зарядка, стабильность pending.
3. Стартовая генерация предметов/техник (вместо сейвов, решение пользователя):
   расширить PreGen/TestItemSeeder — стартовое снаряжение новичка.
4. По разрешению: обновить CHEAT_PANEL.md (новая секция + настройки).

## Файлы

- `game/src/Modules/Player/PlayerCombatAdapter.cs` — кулдаун §8.1-8.2 + AGI
- `game/src/Modules/Combat/CombatService.cs` — подтип basic_attack
- `game/src/Adapter/Scene/GameWorldController.cs` — тост-фикс + AttackRejected + F2-гейт
- `game/src/Adapter/Persistence/GameSettings.cs` — НОВЫЙ: настройки
- `game/src/Adapter/UI/MainMenuController.cs` — окно настроек
- `game/src/Adapter/UI/CheatPanel.cs` — секция «Физическая боевка» (2 кнопки)
- `checkpoints/09_03_m2_combat_cheats.md` — этот чекпоинт
