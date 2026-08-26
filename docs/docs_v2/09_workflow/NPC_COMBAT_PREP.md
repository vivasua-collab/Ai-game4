# План: Внедрение NPC + Боевая система + Торговля + Чат

> **Дата:** 2026-08-19
> **Статус:** PLAN (подготовка к следующему этапу)
> **Источники:** Аудиты Task 5-a (NPC), 5-b (Combat), 5-c (Generators)

---

## 1. Текущее состояние (сводка)

### NPC модуль — ~3500 LOC, MOSTLY IMPLEMENTED
- ✅ Backend: NPCModule, NPCService, NPCSpawnerService, NPCAIService, NPCMovementService, NPCCombatAdapter, NPCRelationshipService, NPCAssemblyService (8-step pipeline)
- ✅ DialogueService (398 LOC) — linear + branching dialogue trees
- ✅ InteractionService — wired to E key
- ❌ NPCSpawnPhase — STUB (NPC не спавнятся)
- ❌ NPCVisualService — STUB (нет рендеринга в Godot)
- ❌ DialogueWindow/chat UI — ZERO
- ❌ Trade system — ZERO (no ITradeService, no docs)
- ❌ Faction system — ZERO в Ai-game4 (есть в Ai-game3-ref, 261 LOC, portable)

### Combat модуль — ~3527 LOC, MOSTLY IMPLEMENTED
- ✅ 11-layer damage pipeline (DamageService.CalculateDamage)
- ✅ TechniqueService (231 LOC), TechniqueChargeService (191 LOC)
- ✅ NPCCombatAdapter (227 LOC) — active
- ❌ PlayerCombatAdapter — STUB (74 LOC vs 241 в Ai-game3), NOT registered in DI
- ❌ Target selection — AttackIntentEvent.TargetId=string.Empty not resolved
- ❌ Equipment data NOT wired (5 TODOs: armorDodgePenalty, shieldBlock, weaponParryBonus, weapon.Penetration, techniqueCritBonus all =0)
- ❌ NO weapon variety (hardcoded "Sword" OneHand)
- ❌ NO ammo system, NO thrown weapons, NO dual wield
- ❌ Knockback + Chain lightning — STUBS in ElementalEffectService

### Generators — CODE-COMPLETE но DORMANT
- ✅ ItemGeneratorService (527 LOC, 7 methods) — работает, verified via GODOT_GEN_DEBUG
- ✅ TechniqueGeneratorService (555 LOC, 10-step pipeline) — работает
- ⚠ Все weapons = "Меч" (hardcoded Sword)
- ⚠ Все armor = Torso slot
- ⚠ Все consumables = "Лекарство" (Heal)
- ❌ Penetration=0, DodgeBonus=0, ArmorPenetration=0
- ❌ ItemId collision risk (modulo 1000)

### Документация
- ✅ NPC: 5 docs (1795 LOC) — NPC.md, NPC_AI_SYSTEM.md, NPC_ASSEMBLY_PIPELINE.md, FACTION_SYSTEM.md, ENTITY_TYPES.md
- ✅ Combat: 11 docs (~4300 LOC) — COMBAT_SYSTEM.md, TECHNIQUE_SYSTEM.md, EQUIPMENT_SYSTEM.md, etc.
- ❌ NO Trade doc (mentioned in UI_DESIGN.md #18 only)
- ❌ NO Dialogue doc (mentioned in UI_DESIGN.md #19 only)

---

## 2. Результаты проверки генераторов (GODOT_GEN_DEBUG=1)

### Items — генерируются корректно:
```
[Weapon]  weapon_3_001 | Меч уровня 3 | dmg=14 | pen=0 (!) | OneHand | Metal:2
[Armor]   armor_3_002 | Броня уровня 3 | def=9 | dodge=0 (!) | Torso
[Consum]  consumable_3_003 | Лекарство уровня 3 | stack=20
[Charger] charger_5_004 | Зарядник Ци | Belt slot
[Random]  armor_4_005 | Броня уровня 4
```

### Techniques — генерируются, но Cultivator type cap=0:
```
[Tech1] Cultivator → Cultivation/Neutral | cap=0 (!) | qiCost=0 (!)
[Tech2] Guard → Defense/Void | cap=831 | qiCost=124
[Tech3] Enemy → Curse/Void | cap=96 | qiCost=14
```

### Loot — работает:
```
[Loot] 3 items (weapons + armor)
[CLoot] 3 consumables
```

### Database: 11 items registered after debug dump

---

## 3. Критические проблемы (Tier 1 BLOCKERS)

### NPC
1. **NPCSpawnPhase** — stub, не спавнит NPC
2. **NPCVisualService** — stub, нет Godot рендеринга
3. **InteractionService** — hardcoded test positions, не wired to NPC spawns
4. **DialogueWindow UI** — не существует
5. **IsInteractPressed** — не экспортирован в IPlayerInputService (как IsHarvestPressed раньше)

### Combat
6. **PlayerCombatAdapter** — dormant, не registered в DI
7. **Target selection** — AttackIntentEvent.TargetId не resolved
8. **Equipment data** — 5 TODOs (penetration, dodge, parry, etc. = 0)

### Generators
9. **Penetration=0** — блокирует CombatService.cs:444 TODO
10. **DodgeBonus=0** — блокирует CombatService.cs:428 TODO
11. **Cultivator technique cap=0** — баг в TechniqueGeneratorService
12. **Weapon variety** — все "Sword", нет bow/spear/axe/dagger

---

## 4. План внедрения (поэтапный)

### Phase 1: NPC Spawn + Render (BLOCKER)
**Цель:** NPC появляется в мире, виден, можно подойти.

| Задача | LOC | Файлы |
|--------|-----|-------|
| Реализовать NPCSpawnPhase | ~150 | Entry/Phases/NPCSpawnPhase.cs (new) |
| Реализовать NPCVisualService (Godot) | ~200 | Modules/Npc/NPCVisualService.cs + Adapter/Scene/NPCSpriteRenderer.cs (new) |
| Wire InteractionService to real NPC positions | ~80 | Modules/Npc/InteractionService.cs |
| Expose IsInteractPressed в IPlayerInputService | ~10 | Core/Interfaces + Player/PlayerInputService |
| Add E-key handler в GameWorldController | ~40 | Adapter/Scene/GameWorldController.cs |
| **Итого** | **~480** | |

**Результат:** NPC спавнятся (3-5 тестовых), видны как цветные кружки, E key → interaction event.

### Phase 2: Test Chat (Dialogue UI)
**Цель:** Простой чат с NPC — текст + варианты ответа.

| Задача | LOC | Файлы |
|--------|-----|-------|
| DialogueWindow (Control) | ~300 | Adapter/UI/DialogueWindow.cs (new) |
| Wire DialogueService → DialogueWindow | ~50 | Adapter/Scene/GameWorldController.cs |
| Тестовые диалоги (JSON or hardcoded) | ~100 | game/data/dialogues/*.json (new) |
| **Итого** | **~450** | |

**Результат:** Нажать E рядом с NPC → открывается чат, можно выбрать ответ.

### Phase 3: Faction System Port
**Цель:** Фракции влияют на отношение NPC.

| Задача | LOC | Файлы |
|--------|-----|-------|
| Port FactionService из Ai-game3-ref | ~250 | Modules/World/FactionService.cs (new) |
| Port FactionData | ~50 | Core/Data/FactionData.cs (new) |
| Register в WorldModule | ~30 | Modules/World/WorldModule.cs |
| Wire NPCRelationshipService → FactionService | ~70 | Modules/Npc/NPCRelationshipService.cs |
| **Итого** | **~400** | |

### Phase 4: Trade System Foundation
**Цель:** Торговец открывает окно торговли, можно купить/продать.

| Задача | LOC | Файлы |
|--------|-----|-------|
| ITradeService interface | ~30 | Core/Interfaces/ITradeService.cs (new) |
| TradeService impl | ~300 | Modules/Trade/TradeService.cs (new) |
| TradeEvent contracts | ~50 | Core/Messaging/Contracts/TradeContracts.cs (new) |
| MerchantInventory model | ~100 | Core/Data/MerchantInventory.cs (new) |
| TradeModule registration | ~40 | Modules/Trade/TradeModule.cs (new) |
| **Итого** | **~520** | |

### Phase 5: Trade UI
**Цель:** Окно торговли с列表ом товаров, корзиной, обменом.

| Задача | LOC | Файлы |
|--------|-----|-------|
| TradeWindow (Control) | ~400 | Adapter/UI/TradeWindow.cs (new) |
| Trade slot UI + drag&drop | ~200 | Adapter/UI/TradeSlotUI.cs (new) |
| Wire TradeService → TradeWindow | ~50 | GameWorldController.cs |
| **Итого** | **~650** | |

### Phase 6: Combat Activation
**Цель:** Игрок может атаковать NPC, получать урон.

| Задача | LOC | Файлы |
|--------|-----|-------|
| Реализовать PlayerCombatAdapter (full) | ~250 | Modules/Player/PlayerCombatAdapter.cs |
| Register в PlayerModule | ~30 | Modules/Player/PlayerModule.cs |
| Target selection service | ~150 | Modules/Combat/TargetSelectionService.cs (new) |
| Wire equipment data (5 TODOs) | ~150 | Modules/Combat/CombatService.cs |
| Add Space/LMB attack handler | ~50 | GameWorldController.cs |
| **Итого** | **~630** | |

### Phase 7: Combat Visuals
**Цель:** Видим урон, HP бары, анимации.

| Задача | LOC | Файлы |
|--------|-----|-------|
| DamageNumber floating text | ~150 | Adapter/UI/DamageNumber.cs (new) |
| HP bar над NPC | ~100 | Adapter/UI/NPCHealthBar.cs (new) |
| Combat stance/speed change | ~80 | Modules/Player/PlayerService.cs |
| **Итого** | **~330** | |

### Phase 8: Weapon Variety + Ammo
**Цель:** Разные типы оружия, луки со стрелами.

| Задача | LOC | Файлы |
|--------|-----|-------|
| WeaponSubtype enum + generator variety | ~300 | ItemGeneratorService.cs + Enums.cs |
| IAmmoService + AmmoService | ~200 | Modules/Inventory/AmmoService.cs (new) |
| Bow/crossbow attack logic | ~200 | Combat/WeaponDamageCalculator.cs |
| Arrow sprite + projectile travel | ~300 | Adapter/Scene/ProjectileRenderer.cs (new) |
| **Итого** | **~1000** | |

### Phase 9: Thrown Weapons + Dual Wield
**Цель:** Метательные ножи, двойное оружие.

| Задача | LOC | Файлы |
|--------|-----|-------|
| Thrown weapon mechanics | ~400 | Combat/ + Generator/ |
| Dual wield logic | ~250 | Combat/CombatService.cs |
| **Итого** | **~650** | |

---

## 5. Итоговая оценка

| Phase | LOC | Сложность | Приоритет |
|-------|-----|-----------|-----------|
| 1. NPC Spawn + Render | ~480 | 🟡 Medium | P0 (BLOCKER) |
| 2. Test Chat | ~450 | 🟢 Low | P0 |
| 3. Faction Port | ~400 | 🟢 Low | P1 |
| 4. Trade Foundation | ~520 | 🟡 Medium | P1 |
| 5. Trade UI | ~650 | 🟡 Medium | P1 |
| 6. Combat Activation | ~630 | 🔴 High | P0 |
| 7. Combat Visuals | ~330 | 🟡 Medium | P2 |
| 8. Weapon Variety + Ammo | ~1000 | 🔴 High | P2 |
| 9. Thrown + Dual Wield | ~650 | 🔴 High | P2 |
| **Итого** | **~5110** | | |

---

## 6. Рекомендации по порядку

1. **Phase 1 + 2** — NPC + Chat (можно тестировать взаимодействие)
2. **Phase 6** — Combat Activation (можно тестировать бой)
3. **Phase 3 + 4 + 5** — Faction + Trade (мир оживает)
4. **Phase 7** — Combat Visuals (полировка)
5. **Phase 8 + 9** — Weapon variety (глубина)

## 7. Что можно взять из Ai-game3-ref

| Компонент | Ai-game3-ref path | LOC | Статус |
|-----------|-------------------|-----|--------|
| FactionService | Modules/World/FactionService.cs | 227 | ✅ Portable |
| FactionData | Data/FactionData.cs | 34 | ✅ Portable |
| PlayerCombatAdapter (full) | Modules/Player/PlayerCombatAdapter.cs | 241 | ✅ Portable (stub в Ai-game4) |
| DialoguePanelView | Modules/UI/Dialogue/DialoguePanelView.cs | 42 | ❌ Not portable (Unity uGUI) |
| ItemDatabaseServiceTests | Tests/Modules/Generator/ | 155 | ✅ Portable (NUnit) |
| Combat tests (5 files) | Tests/Modules/Combat/ | 807 | ✅ Portable |

## 8. Генераторы — что починить

| Проблема | Файл | Фикс |
|----------|------|------|
| Penetration=0 | ItemGeneratorService.cs GenerateWeaponForLevel | Set based on weapon type |
| DodgeBonus=0 | ItemGeneratorService.cs GenerateArmorForLevel | Set based on armor type |
| Cultivator cap=0 | TechniqueGeneratorService.cs | Fix baseCapacity for Cultivation type |
| Weapon variety | ItemGeneratorService.cs | Add WeaponSubtype param, generate bow/spear/axe |
| ItemId collision | ItemGeneratorService.cs | Use full hash, not modulo 1000 |
