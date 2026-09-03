# Чекпоинт: Phase 8 ч.2 — дальний бой (луки) + стартовая генерация + чит-расширение

**Дата:** 2026-09-03 06:35 UTC
**Сессия:** продолжение 09-03 (раунд 2 по новым приоритетам пользователя)
**Тип:** implementation

---

## Контекст

Новые приоритеты пользователя (сессия 2026-09-03): физическая боевка —
основной приоритет; сейвы отложены — вместо них стартовая генерация
предметов/техник; чит-меню расширять под тесты, отключение в настройках.
Раунд 1 закрыл melee (M1 self-hit, M2 кулдаун/подтип/настройки, M2b range
NPC). Этот чекпоинт — раунд 2: дальний бой (Phase 8 ч.2, закрыт TODO из
CombatService), стартовый набор (замена сейвов), чит-кнопки для ranged-тестов.

## Что сделано

1. **Дальний бой end-to-end (коммит cf6e1fd):**
   - `PlayerCombatAdapter.WeaponMode` (Melee/Ranged), клавиши 1/2 —
     реальное переключение (GameWorldController, было «зарезервировано»)
   - Space в Ranged-режиме с луком: цель в радиусе `EquipmentData.AttackRange`
     (Chebyshev), `AttackIntentEvent.IsRanged=true`; без лука — fallback melee
   - `CombatService`: isRanged проводён через оба пути (мгновенный —
     `_lastAttackIsRanged`; pending-каст — `PendingTechnique.IsRanged`, паттерн M1)
   - Базовая ranged-атака: подтип `RangedProjectile` (стрела = piercing →
     кровотечение P2-7.3), `AttackType.Ranged` (INT scaling §4.2), урон Physical
   - `WeaponDamageCalculator.CalculateRangedWeaponDamage`: §4.2 AGI 2.5% + INT 5%
   - NPC-лучники: `NPCModule.ProcessNpcAttacks` — дальность из экипированного
     оружия (лук 18 тайлов) вместо жёсткого `dist > 2`
   - `CombatSimDebug` фаза 3c: лук, телепорт NPC на дистанцию 8, верификация
     урона + подтипа. VERDICT теперь покрывает melee + ranged

2. **Стартовая генерация предметов (коммит a6f6c51, замена сейвов):**
   - `StartingGearPhase` (Entry, PhaseOrder 5): правильный слой, работает в
     release (старый сид был `#if DEBUG` в InventoryWindow — UI-слой)
   - Канонические предметы в БД ДО спавна NPC (лавки сразу находят материалы)
   - Набор (детерминированный сид 1000): кинжал L1 **авто-надет** (MeleeWeapon
     с первого шага), лук L1 в инвентаре (ranged без читов), 2 оружия L1-3,
     торс L1 + броня L2, 1 random L2, материалы ×4 вида, расходники ×4 вида,
     камни Ци (3+2+1+1 chaotic)
   - `QiStoneSeeder` перенесён Adapter/UI → Modules/Generator (правильный слой)
   - `InventoryWindow.SeedGeneratedItems` удалён

3. **Чит-меню расширение (коммит 17a2df1):**
   - «🏹 Лук в руки»: генерация лука по уровню + TryEquip + SwitchToRangedMode
   - «Дальний + мишень»: бандит на дистанции 8 (вне melee 2.5, внутри лука 18)
   - Отключение чит-меню в настройках — уже было в M2 (GameSettings.CheatsEnabled:
     гейт F2 + тумблер в MainMenu), проверено — не дублировано

## Решения

- Стрела = Physical урон (материя), НЕ Qi — GetTechniqueDamageType(null)=Physical
  не тронут; подтип RangedProjectile задаёт только piercing-последствия —
  в Qi-маппинг (спринт 4 C6) попадают только техники (tech != null)
- Ammo (расход стрел) и ProjectileRenderer (визуал трассера) — отложены:
  COMBAT_SYSTEM.md §27 — бой формульный, снаряды не физические; MVP-приоритет —
  корректность урона/подтипов/дистанций
- LOS (стрельба через стены) не реализован — melee его тоже не имеет;
  зарегистрировано как TODO следующей итерации
- PhaseOrder 5 (вместе с NPC/Animal spawn): фазе нужен только игрок (4),
  регистрация предметов ДО HumanNPCSpawn (6) — лавки находят материалы

## Найденные проблемы

- ENV-флап: окружение песочницы упало между сессиями (репо/Godot удалены,
  токен и worklog выжили) — восстановлено cold_start.sh + re-clone. Все
  коммиты были запушены — потерь нет — процесс чекпоинтов сработал
- Fresh-clone требует `godot --headless --import` (кэш .godot/imported вне
  git) — учтено в процедурах восстановления

## Следующие шаги

1. Боевка техниками (приоритет 2): NPC-каст техник в бою (npc_strike →
   техники из репертуара NPC), визуал кастов
2. Ammo: стрелы как предмет + расход при выстреле (если пользователь подтвердит)
3. LOS для ranged (препятствия тайловой карты)
4. Документация: docs_v2 CHEAT_PANEL.md отстаёт (новые секции M2/Phase 8 ч.2) —
   обновить ТОЛЬКО по явному разрешению пользователя (доки заморожены)

## Файлы

- `game/src/Modules/Combat/CombatService.cs` — isRanged пайплайн + подтип + урон
- `game/src/Modules/Combat/WeaponDamageCalculator.cs` — ranged-формула §4.2
- `game/src/Modules/Player/PlayerCombatAdapter.cs` — WeaponMode + ranged-атаки
- `game/src/Modules/NPC/NPCModule.cs` — дальность оружия NPC
- `game/src/Adapter/Scene/GameWorldController.cs` — клавиши 1/2
- `game/src/Adapter/Scene/CombatSimDebug.cs` — фаза 3c ranged
- `game/src/Adapter/UI/CheatPanel.cs` — «Лук в руки» + «Дальний + мишень»
- `game/src/Entry/Phases/StartingGearPhase.cs` — стартовый набор (новый)
- `game/src/Modules/Generator/QiStoneSeeder.cs` — перенесён из Adapter/UI
- `game/src/Entry/SceneAssemblyRegistrar.cs` — регистрация фазы
- `game/src/Adapter/UI/InventoryWindow.cs` — dev-хак сида удалён

Коммиты: cf6e1fd (ranged), 17a2df1 (чит-кнопки), a6f6c51 (стартовый набор).
Все запушены в origin/main.
