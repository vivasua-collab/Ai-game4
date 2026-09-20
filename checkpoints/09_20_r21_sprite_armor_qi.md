# R21 — Эпизод 1 (2026-09-20): спрайт волка + пайплайн брони + пассивный щит Ци

> Репорт пользователя (5 пунктов, 20.09): №1 волк с телом человека,
> №2 ходы противника/парирование, №3 броня не влияет на урон, №4 пассивный
> щит Ци, №5 экономика золото/камни. Эпизод 1 закрывает №1, №3, №4.
> №2 (attack-speed миграция + clash-парирование) и №5 (две валюты) —
> следующие эпизоды; аудиты R21-A/B/C (worklog) содержат планы.

## Пункт 1: волк с телом человека — КОРЕНЬ

- **Симптом**: NPC «Молодой Волк · L1» (дикая локация: NPCSpawnCompositionService
  :188-190, `SpawnRequest(Monster, level, "wolf")`; Raid :297-300) рендерился
  как **человек в робе** (+меч до 67c0101). VLM-мисрид «Монгол Воин» = мелкий
  шрифт нейм-плейта.
- **Корень**: `NPCSpriteRenderer._Draw` выбирал спрайт ТОЛЬКО по роли
  (`CreateNPCSprite(npc.Role)`); Monster в switch-е рецептов нет → default
  серая роба. Морфология/вид не участвовали.
- **Фикс**: ветка по морфологии — `Quadruped` + `NPCState.SpeciesId` →
  `ProceduralSpriteGenerator.CreateAnimalSprite(species, size)` (кэш
  `_beastSpriteCache` по виду; BeastSizeClass: rabbit=Small,
  tiger/dragon=Large, прочие=Medium). Прочие морфологии — прежний
  гуманоидный fallback (SPRITE_CATALOG §19). Оружие — фильтр был (R15-аудит).
- **Файлы**: NPCSpriteRenderer.cs (+~30 строк).
- **Доки**: SPRITE_CATALOG §6-примечание (морфология важнее роли).

## Пункт 3: броня «визуально не влияет» — ДВА КОРНЯ

- **Корень-1 (масштаб)**: формула `armor/(armor+100)` давала ~5.6% после
  пробития на L1-сете (13 брони, pen 7) → на ударах 10-20 разница 0-2 ед.
  **Фикс**: плоское вычитание `damage −= effectiveArmor×0.5` ПОСЛЕ процентного
  (ALGORITHMS §5.2, COMBAT слой 7) в DefenseProcessor.ApplyDefense. Пример:
  броня 50/pen 10/DR 20%: 100 → 31 (69% суммарно).
- **Корень-2 (мёртвое поле)**: `EquipmentData.DamageReduction` писался
  генератором (min(80, 3+level×1.5)), показывался в тултипе («Снижение
  урона: X%»), но бой его НЕ читал — обман UI. **Фикс**: агрегат в
  EquipmentDataProvider (`_cachedDamageReduction`, ср. по броневым предметам,
  как coverage) + `GetDamageReductionPermil()` (интерфейс) → DamageService
  слой 6-7 → DefenseContext.DamageReductionPermil (поверх бафф/формаций).
  Тултип теперь честный.
- **Файлы**: IEquipmentDataProvider.cs, EquipmentDataProvider.cs,
  DamageService.cs, DefenseProcessor.cs.
- Проверка цепочки аудит-ом R21-B: проводка целая (TryEquip → SyncToProvider →
  SetEquipmentData → RecomputeAggregates → GetTotalArmor → coverage-roll →
  DefenseProcessor); обрыва не было — только масштаб + мёртвый DR.

## Пункт 4: пассивный щит Ци — НЕ РЕАЛИЗОВАН В ПРОВОДКЕ (было)

- **Ответ на вопрос «с какого уровня»**: в доках уровень НЕ задан; критерий —
  наличие Ци ≥ MIN_QI_FOR_BUFFER=10, т.е. практик с пробуждённым ядром L1+
  (MORTAL_DEVELOPMENT §6: у L0-смертных Ци нет). «Защищает даже во сне»
  (COMBAT §5, QI_SYSTEM §6).
- **Корни**: (а) RawQi-режим никто не активировал (только Shield через
  Defense-технику Z/Barrier); (б) QiDataProvider.SetQiBufferState — ноль
  вызывателей → NPC-щит всегда off; (в) G-стойка Shield требовала WeaponOff
  предмет, которых генератор не создаёт (мёртвый путь).
- **Фиксы**:
  1. DamageService, путь игрока: буфер не активен + Ци ≥ 10 → автоматически
     режим RawQi (80% физика/5:1/20% пробитие; гейт по Ци в формулах —
     опустошённый практик/смертный защиты не получают).
  2. QiDataProvider.SetQiState: авто-активация RawQi при Ци ≥ 10 (спаун/
     загрузка/реген; идемпотентно, формулы гейтят расход).
  3. PlayerCombatAdapter: стойка Shield доступна с Ци ≥ 10 без WeaponOff
     (Qi-щит ≠ физический щит; активацию гейтит CombatService по Ци 25%).
- **Файлы**: DamageService.cs, QiDataProvider.cs, PlayerCombatAdapter.cs.
- **Доки**: COMBAT_SYSTEM §5-примечание, QI_SYSTEM §6-примечание.

## QA

- COMBAT_SIM +фаза 3f (r21): (a) DefenseProcessor 100→31 при броне 50/DR
  200‰/pen 10; (b) DR-агрегат провайдера = 200‰; (c) авто-активация NPC
  RawQi (мортал Ци=5 → нет); (d) end-to-end: поглощено 80 / пробито 20 /
  Ци 500→100 (5:1) — NPC и игрок (пассивно, буфер не активен). Крит-устойчиво
  (инвариант absorbed+final=dmg(100|150)).
- Итог: **QA 18/18 PASS** (полная регрессия), build 0 errors.
- Тултип ItemContextMenu «Снижение урона» теперь соответствует бою.

## NEXT (из аудитов R21, см. worklog)

- **№2 attack-speed миграция**: чекпоинт 09_19_combat_attack_speed_analysis.md
  (3 эпизода) + clash-правило «одновременная атака → парирование» (~120-140
  строк поверх: парирование уже реализовано — DamageCalculator :202-209,
  parryChance = weaponParryBonus + (AGI−10)×3‰, кап 500‰; в ход-модели
  одновременной атаки не существует — гейт CombatService :471-476).
- **№5 экономика**: курс 1 камень Ци = 10 золота, спред 12/5 (зеркалит
  товарный 1200/500‰), меняют ТОЛЬКО культиваторы-торговцы
  (Role==Merchant && CultivationLevel != None); флаги есть
  (ItemData.RequiredCultivationLevel: 0=смертный; NPCState.CultivationLevel).
  План ~300 строк в аудите R21-C.
- Пользователю: пересобрать локальную сборку (меч за спиной волка уже погашен
  в HEAD 67c0101; скрин был со старой сборки pre-R19).
