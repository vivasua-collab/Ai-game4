# Чекпоинт: Подготовка к NPC + Combat + Trade + Chat

**Дата:** 2026-08-19 11:45 UTC
**Сессия:** web-d86b1055
**Тип:** research + implementation (debug tool) + plan

---

## Контекст

Пользователь завершил пул задач на сегодня и запросил подготовку к следующему этапу:
1. Внедрение NPC (мирные + враждебные)
2. Взаимодействие с мирными NPC
3. Тестовый чат
4. Торговцы
5. Анализ боевой системы (холодное/дальнобойное/метательное + техники)
6. Проверка генераторов экипировки и техник
7. Изучение документации (новой + старой)
8. Onработки из Ai-game3

## Что сделано

### 1. Аудит (3 параллельных исследования)

#### Task 5-a: NPC система
- **Backend:** ~3500 LOC, 16 файлов — MOSTLY IMPLEMENTATED
- ✅ NPCModule, NPCService, NPCSpawnerService, NPCAIService, NPCMovementService, NPCCombatAdapter, NPCRelationshipService, NPCAssemblyService (8-step pipeline)
- ✅ DialogueService (398 LOC) — linear + branching dialogue trees
- ❌ NPCSpawnPhase — STUB (NPC не спавнятся)
- ❌ NPCVisualService — STUB (нет Godot рендеринга)
- ❌ DialogueWindow/chat UI — ZERO
- ❌ Trade system — ZERO
- ❌ Faction system — ZERO в Ai-game4 (261 LOC portable из Ai-game3-ref)
- **Docs:** 5 canonical docs (1795 LOC), MISSING Trade + Dialogue specs

#### Task 5-b: Боевая система
- **Backend:** ~3527 LOC, 18 файлов — MOSTLY IMPLEMENTED
- ✅ 11-layer damage pipeline (DamageService.CalculateDamage)
- ✅ TechniqueService (231 LOC), TechniqueGeneratorService (555 LOC)
- ✅ NPCCombatAdapter (227 LOC) — active
- ❌ PlayerCombatAdapter — STUB (74 LOC vs 241), NOT registered in DI
- ❌ Target selection — AttackIntentEvent.TargetId not resolved
- ❌ 5 TODOs (equipment data: pen, dodge, parry, etc. = 0)
- ❌ NO weapon variety (hardcoded "Sword" OneHand)
- ❌ NO ammo, NO thrown, NO dual wield
- ❌ Knockback + Chain lightning — STUBS
- **Docs:** 11 docs (~4300 LOC) — COMPREHENSIVE

#### Task 5-c: Генераторы
- **ItemGeneratorService** (527 LOC, 7 methods) — code-complete, DORMANT
- **TechniqueGeneratorService** (555 LOC, 10-step) — code-complete, DORMANT
- ⚠ Все weapons = "Меч" (hardcoded Sword)
- ⚠ Все armor = Torso slot
- ⚠ Все consumables = "Лекарство" (Heal)
- ❌ Penetration=0, DodgeBonus=0, ArmorPenetration=0
- ❌ ItemId collision risk (modulo 1000)
- ❌ Cultivator technique cap=0 (bug)

### 2. GODOT_GEN_DEBUG env flag (реализация)
- `GeneratorModule.cs` — added `RunGeneratorDebugDump()`
- Генерирует 5 items + 3 techniques + 6 loot items, выводит все поля в лог
- Headless verified: генераторы работают

### 3. Результаты проверки генераторов
```
[Weapon]  weapon_3_001 | Меч уровня 3 | dmg=14 | pen=0 (!) | OneHand | Metal:2
[Armor]   armor_3_002 | Броня уровня 3 | def=9 | dodge=0 (!) | Torso
[Consum]  consumable_3_003 | Лекарство уровня 3 | stack=20
[Charger] charger_5_004 | Зарядник Ци | Belt slot
[Tech1]   Cultivator → Cultivation/Neutral | cap=0 (!) | qiCost=0 (!)
[Tech2]   Guard → Defense/Void | cap=831 | qiCost=124
[Tech3]   Enemy → Curse/Void | cap=96 | qiCost=14
[Loot]    3 weapons/armor + 3 consumables
Database: 11 items registered
```

**Вывод:** Генераторы работают, но с ограничениями (no variety, pen=0, dodge=0, Cultivator bug).

### 4. План внедрения (NPC_COMBAT_PREP.md)
9 phases, ~5110 LOC total:

| Phase | Описание | LOC | Приоритет |
|-------|----------|-----|-----------|
| 1 | NPC Spawn + Render | ~480 | P0 BLOCKER |
| 2 | Test Chat (Dialogue UI) | ~450 | P0 |
| 3 | Faction Port | ~400 | P1 |
| 4 | Trade Foundation | ~520 | P1 |
| 5 | Trade UI | ~650 | P1 |
| 6 | Combat Activation | ~630 | P0 |
| 7 | Combat Visuals | ~330 | P2 |
| 8 | Weapon Variety + Ammo | ~1000 | P2 |
| 9 | Thrown + Dual Wield | ~650 | P2 |

## Решения

- **План поэтапный** — Phase 1+2 (NPC+Chat) и Phase 6 (Combat) — P0, остальное по приоритету
- **Generators debug flag** — `GODOT_GEN_DEBUG=1` для headless проверки, не влияет на normal flow
- **Ai-game3-ref onработки** — FactionService (261 LOC) + PlayerCombatAdapter (241 LOC) portable, DialoguePanelView НЕ portable
- **Документация** — NPC/Combat docs comprehensive, Trade/Dialogue docs MISSING (написать при реализации)

## Найденные проблемы

### Критические (Tier 1 BLOCKERS)
1. NPCSpawnPhase — stub, NPC не спавнятся
2. NPCVisualService — stub, нет рендеринга
3. PlayerCombatAdapter — dormant, не registered в DI
4. Target selection — не реализован
5. Cultivator technique cap=0 — баг генератора

### Средние (Tier 2 LIMITATIONS)
6. Penetration=0, DodgeBonus=0 — блокируют combat TODOs
7. Weapon variety = 0 (только "Sword")
8. Equipment data not wired (5 TODOs в CombatService)
9. Knockback + Chain lightning — stubs
10. ItemId collision risk

### Документация
11. NO Trade doc
12. NO Dialogue doc (format, conditions, variables)

## Следующие шаги (на следующую сессию)

1. **Phase 1: NPC Spawn + Render** — реализовать NPCSpawnPhase + NPCVisualService + wire InteractionService
2. **Phase 2: Test Chat** — DialogueWindow UI + тестовые диалоги
3. **Phase 6: Combat Activation** — PlayerCombatAdapter + target selection + equipment wiring
4. Fix generator bugs: Penetration, DodgeBonus, Cultivator cap=0
5. Port FactionService из Ai-game3-ref

## Файлы

**Созданные:**
- `docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md` — план (9 phases, ~5110 LOC)
- `checkpoints/08_19_npc_combat_prep.md` — этот чекпоинт

**Изменённые:**
- `game/src/Modules/Generator/GeneratorModule.cs` — +GODOT_GEN_DEBUG env flag + RunGeneratorDebugDump
- `worklog.md` — записи Tasks 5-a, 5-b, 5-c + запись 11:45

**Верификация:**
- `dotnet build`: 0 errors
- `GODOT_GEN_DEBUG=1` headless: генераторы работают, 11 items registered
- 3 исследования (NPC, Combat, Generators) — ~3000 строк отчётов в worklog
