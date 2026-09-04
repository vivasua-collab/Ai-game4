# S3 — Kill-feed + атрибуция атакующего (2026-09-04)

**Сеанс:** 3/6 автоматических сеансов кодинга (директива: интерфейсы
взаимодействия пользователя — информативность и отсутствие багов).

**Время сеанса:** начало 2026-09-04 12:26:23 UTC, окончание см. git log
(ручной старт — cron не отработал; окружение было снова сброшено и
восстановлено в первые ~40 минут сеанса).

## Проблема (информативность боя — критично)

1. **Убийства NPC были невидимы**: NPCCombatAdapter публикует
   NPCDeathEvent при смерти NPC, но UI на него НЕ подписывался. Игрок
   убивал противника — ноль отклика (только лут падал). EventLogWindow
   слушал только EnemyKilledEvent из stage-боя (поединки CombatService),
   который в физическом бою не публикуется.
2. **Урон игроку без источника**: тост «💥 −12 HP» не говорил, КТО бьёт —
   в замесе непонятно, кого атаковать в ответ.

## Решение

**Kill-feed (GameWorldController + EventLogWindow):**
- Тост при смерти NPC от игрока: «☠ {Имя} повержен» (3с).
- Естественная смерть: «✝ {Имя} ушёл из мира (старость)».
- Смерть от NPC — БЕЗ тоста (анти-спам npc-npc), но пишется в журнал.
- EventLogWindow: подписка на NPCDeathEvent с дедупом (< 2с, защита
  от двойного срабатывания физического+stage путей):
  «☠ {Имя} повержен (руками)» / «✝ … (старость)» / «☠ {Имя} погиб ({убийца})».
- Тосты стека (S2) показывают несколько смертей одновременно.

**Атрибуция атакующего:**
- OnPlayerDamaged: «💥 −12 HP — {Имя атакующего}» (SourceId → DisplayName).

## QA (GODOT_KILLFEED_DEBUG=1, новый) — PASS

```
[KillFeedSim] test npc: npc_cbb2b5d134d445d2 'Екатерина'
[KillFeedSim] step1 kill toast: '☠ Екатерина повержен'
[KillFeedSim] step2 log entry: '☠ Екатерина повержен (руками)'
[KillFeedSim] step3 dedup: last='☠ Екатерина повержен (руками)'
[KillFeedSim] step4 old-age toast: '✝ Николай ушёл из мира (старость)'
[KillFeedSim] VERDICT: PASS
```

Регрессия (после сброса окружения, всё пересобрано): COMBAT PASS,
CHARGE PASS, TOAST PASS, LOWHP PASS, TRADE PASS (buy/sell True). Build 0 errors.

## Инфраструктура: восстановление после 2-го сброса песочницы

- Ai-game4/, .auth/, godot/, .dotnet — снова удалены; счётчик (2/6) выжил.
- .NET 8.0.424 + 9.0.317 переустановлены; репо переклонирован (HEAD 460cce4).
- **Godot теперь живёт в /home/z/godot_flat/godot** (плоская копия):
  распакованная папка из zip имеет имя `Godot_v4.7.1-stable_mono_linux_x86_64`
  (подчёркивание в linux_x86_64!), литеральные пути с точкой ломались;
  глубокие пути флапали ENOENT. Извлечение — напрямую из zip по
  динамическому префиксу + бинарник под плоским именем.
- `automation/restore_env.sh` переписан под плоский Godot (python-инсталлятор).

## Файлы

- `game/src/Adapter/Scene/GameWorldController.cs` — NpcDeathSub, OnNpcDied, атрибуция
- `game/src/Adapter/UI/EventLogWindow.cs` — OnNpcDeathFeed, дедуп, LastEntryText (QA)
- `game/src/Adapter/Scene/KillFeedSimDebug.cs` — новый QA (GODOT_KILLFEED_DEBUG=1)
- `automation/restore_env.sh` — плоский Godot (вне репо, локально)
