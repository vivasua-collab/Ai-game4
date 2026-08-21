# Сводный план исправлений по итогам 4 аудитов

**Дата:** 2026-08-21 14:15 UTC
**Task ID:** AUDIT-FIX-PLAN

---

## Сводка аудитов

| Аудит | Scope | Critical | Major | Minor | Всего |
|-------|-------|----------|-------|-------|-------|
| AUDIT-1 | Core layer | 3 | 11 | 19 | 33 |
| AUDIT-2 | Modules layer | 4 | 14 | 30 | 48 |
| AUDIT-3 | Entry + Adapter | 5 | 14 | 28 | 47 |
| AUDIT-4 | Docs compliance | 2 (P0) | 8 auto-fix | 4 conceptual | 14 |
| **Итого** | | **14** | **47** | **81** | **142** |

---

## Концептуальные решения (требуют ответа пользователя)

### Из AUDIT-1
1. **UltimateQiCostMultiplier**: код 1.5f vs док ×2.0 → **док каноничен** (auto-fix)
2. **NPCState в Modules**: перенести в Core ИЛИ интерфейс INPCStateView?
3. **BodyPart в Modules**: перенести в Core ИЛИ интерфейс IBodyPartView?
4. **MorphologyHitTables**: 6/10 морфологий — добавить гибриды или оставить TBD?

### Из AUDIT-2
5. **SetConfig wiring**: inject через [Inject] или вызывать SetConfig из Start()?
6. **PlayerService HP**: делегировать BodyService или оставить параллельную систему?
7. **NPCAIService**: реализовать 3-tier сейчас или отложить?
8. **8 missing services**: реализовать все или удалить из docs?

### Из AUDIT-3
9. **Movement**: real-time (_PhysicsProcess) или tick-based?
10. **Time.Speed влияет на скорость ходьбы**: да или нет?
11. **GameLifetimeScope Charger позиция**: 6 (current) или 14 (doc)?
12. **RenderLayer.GroundItems**: добавить между Objects(3) и Player(4)?

### Из AUDIT-4
13. **Spirit+Ring storage**: unified или separate?
14. **Element.Poison**: убрать (док says "NOT an element") или оставить?
15. **ЗАПРЕТ 8 TileMapLayer**: мигрировать или оставить _Draw?
16. **ItemCategory**: раздуть Equipment или добавить QiStone?

---

## Auto-fixable (док каноничен, код меняется) — 8 items

Эти исправления выполняются без подтверждения пользователя (правило: док первичен для формул):

| # | Файл | Изменение | Аудит |
|---|------|-----------|-------|
| 1 | TechniqueGeneratorService.cs:446 | qiCost formula `capacity*0.15` → `floor(baseCapacity×2^(level-1))` | AUDIT-4 F1 |
| 2 | Constants.cs:452 | `ULTIMATE_QI_COST_MULTIPLIER = 1.5f` → `2.0f` | AUDIT-4 F2 |
| 3 | Constants.cs:442 | `ULTIMATE_QI_COST_MULTIPLIER_PERMIL = 1500` → `2000` | AUDIT-4 F2 |
| 4 | TechniqueData.cs:9,21,28 | Комментарии формул → правильные | AUDIT-4 F1 |
| 5 | StorageRingService.cs (4 sig) | `float qiCost` → `long qiCost` | AUDIT-4 ЗАПРЕТ 2 |
| 6 | IStorageRingService.cs:21,24 | `float qiCost` → `long qiCost` | AUDIT-4 ЗАПРЕТ 2 |
| 7 | GameLifetimeScope.cs:77 | Charger позиция 6 → 14 | AUDIT-3 M9 + AUDIT-4 |
| 8 | GameWorldController.cs:537-540 | Esc-close inventory → Time.Resume() | AUDIT-3 C4 |

---

## Критические исправления (P0, выполняются сейчас)

### Из AUDIT-3 (влияют на game playability)
1. **Esc-close inventory → Time.Resume()** (C4) — игрок застревает в паузе
2. **HandlePickup distance** (M3) — `1.5f * 96f` → `1.5f * GameConstants.TILE_PIXELS`
3. **GroundItems ZIndex** (M1) — `Objects+1` → `Objects` (ниже игрока)
4. **PlayerInputService meditate bug** (C5) — `"minimap"` → `"meditate"`

### Из AUDIT-4 (формулы)
5. **qiCost formula** (F1) — `capacity*0.15` → doc formula
6. **UltimateQiCostMultiplier** (F2) — 1.5f → 2.0f
7. **ЗАПРЕТ 2 StorageRing** — float → long

---

## План выполнения

### Этап 1: Auto-fixable (субагент, ~30 мин)
- 8 items из таблицы выше
- Файлы: Constants.cs, TechniqueData.cs, TechniqueGeneratorService.cs, StorageRingService.cs, IStorageRingService.cs, GameLifetimeScope.cs, GameWorldController.cs

### Этап 2: Critical fixes (субагент, ~1 час)
- Esc-close resume (GameWorldController.cs)
- HandlePickup distance (GameWorldController.cs)
- GroundItems ZIndex (GroundItemRenderer.cs)
- PlayerInputService meditate bug (PlayerInputService.cs + InputMapInitializer.cs)

### Этап 3: Ожидание решений пользователя
- 16 концептуальных вопросов (см. выше)
- Не выполняются до ответа пользователя

---

## Файлы

- `checkpoints/08_21_audit1_core.md` — Core layer audit
- `checkpoints/08_21_audit2_modules.md` — Modules layer audit
- `checkpoints/08_21_audit3_entry_adapter.md` — Entry + Adapter audit
- `checkpoints/08_21_audit4_docs_compliance.md` — Docs compliance audit
- `checkpoints/08_21_audit_fix_plan.md` — этот сводный план
