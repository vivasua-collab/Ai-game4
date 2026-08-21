# Аудит 4: Code vs Documentation Compliance

**Дата:** 2026-08-21 14:00 UTC
**Task ID:** AUDIT-4

---

## Сводка

- **Документов проверено:** ~25 (docs_v2 + docs_temp)
- **Formula mismatches:** 2 (P0)
- **Architecture deviations:** 2
- **Rule violations:** 2 (ЗАПРЕТ 2, ЗАПРЕТ 8)
- **Feature gaps:** 5
- **Naming mismatches:** 2
- **Auto-fixable:** 8 items
- **Conceptual (user decision):** 4

---

## P0 — Formula mismatches (docs canonical, code must change)

### F1: Qi cost formula
- **Doc (TECHNIQUE_SYSTEM.md §5.2):** `qiCost = floor(baseCapacity × 2^(level-1))`
- **Code (TechniqueGeneratorService.cs:446):** `(long)(capacity * 0.15)`
- **Impact:** Techniques cost ~6.7× less Qi than designed
- **Fix:** Change formula to match doc

### F2: Ultimate qiCost multiplier
- **Doc (TECHNIQUE_SYSTEM.md §9.1):** ×2.0
- **Code (Constants.cs:452):** `ULTIMATE_QI_COST_MULTIPLIER = 1.5f`
- **Code (Constants.cs:442):** `ULTIMATE_QI_COST_MULTIPLIER_PERMIL = 1500`
- **Fix:** Change to 2.0f / 2000

---

## Auto-fixable items (8)

| # | File | Current | Should be (per doc) |
|---|------|---------|---------------------|
| 1 | TechniqueGeneratorService.cs:446 | `capacity * 0.15` | `floor(baseCapacity × 2^(level-1))` |
| 2 | Constants.cs:452 | `1.5f` | `2.0f` |
| 3 | Constants.cs:442 | `1500` | `2000` |
| 4 | TechniqueData.cs:9,21,28 | Wrong formula comments | Correct per doc |
| 5 | StorageRingService.cs:83,154,221,232 | `float qiCost` | `long qiCost` (ЗАПРЕТ 2) |
| 6 | IStorageRingService.cs:21,24 | `float qiCost` | `long qiCost` |
| 7 | Enums.cs:496-506 (ItemCategory) | Missing `QiStone` | Add per INVENTORY_SYSTEM.md §2.2 |
| 8 | GameLifetimeScope.cs:77 | Charger at position 6 | Position 14 per DI_AND_EVENTBUS §1.2 |

---

## Conceptual differences (USER DECISION REQUIRED)

| # | Code | Doc | Question |
|---|------|-----|----------|
| 1 | StorageService unified (Spirit+Ring) | INVENTORY_SYSTEM.md §5,§6 separate | Unified или separate services? |
| 2 | `Element.Poison` exists | ELEMENTS_SYSTEM.md: "Poison is NOT an element" | Убрать Poison из Element enum? |
| 3 | Custom `_Draw` rendering | ЗАПРЕТ 8: TileMapLayer required | Оставить _Draw или мигрировать на TileMapLayer? |
| 4 | ItemCategory: Weapon/Armor/Accessory/Quest/Misc | INVENTORY_SYSTEM.md §2.2: Equipment + QiStone | Раздуть Equipment или оставить как есть? |

---

## Feature gaps (5)

| # | Feature | Doc | Status |
|---|---------|-----|--------|
| 1 | Belt quick-access slots | INVENTORY_SYSTEM.md §7 | ❌ Not implemented |
| 2 | Body silhouette (procedural 64×64) | SPRITE_CATALOG.md | ❌ Not implemented |
| 3 | NPC 3-tier nervous system | NPC_AI_SYSTEM.md | ❌ Single-tier only |
| 4 | Faction system | FACTION_SYSTEM.md | ❌ Not implemented (portable from Ai-game3) |
| 5 | Full loot system | LOOT_SYSTEM_DRAFT.md | ⚠️ Basic only (CombatLootService) |

---

## Verified COMPLIANT ✅

- Capacity formula: `baseCapacity × 2^(level-1) × (1 + mastery × 0.005)` ✅
- Grade multipliers (technique 4, equipment 5) ✅
- Level suppression table (Permil exact match) ✅
- Dual HP split (Red 0.7 / Black 0.3, Heart exception) ✅
- Qi regeneration (10%/day microcore, accumulator) ✅
- Core capacity formula ✅
- Move cost table ✅
- Carry weight (`10 + (STR-10)×2`) ✅
- ЗАПРЕТ 3.9 (integer Permil math in combat) ✅
- 3-layer architecture (Core/Modules/Entry/Adapter) ✅
- BiomeType (9 values) ✅
- EquipmentSlot (15 slots) ✅
- NPCRole (8 values) ✅
- Storage Ring (str-volume model) ✅
- Damage pipeline 11 layers (1-5, 8-11 implemented; 6-7 stubbed per AUDIT-2) ✅

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-4)
