# Сводный план исправлений — ПЕРЕРАБОТАННЫЙ

**Дата:** 2026-08-22 (переработан)
**Task ID:** AUDIT-FIX-PLAN

---

## Сводка аудитов

| Аудит | Scope | Critical | Major | Minor | Всего |
|-------|-------|----------|-------|-------|-------|
| AUDIT-1 | Core layer | 3 | 11 | 19 | 33 |
| AUDIT-2 | Modules layer | 4 | 14 | 30 | 48 |
| AUDIT-3 | Entry + Adapter | 5 | 14 | 28 | 47 |
| AUDIT-4 | Docs compliance | 0 (2 fixed) | 0 (4 fixed) | 4 conceptual | 4 |
| **Итого** | | **12** | **39** | **81** | **132** |

---

## ПРАВИЛА

1. **Документация первична** — НЕ редактировать без прямого указания пользователя
2. **"Не реализовано"** — будет реализовано позже (начальный этап, отладка ядра)
3. **Формулы** — док каноничен, код меняется (уже выполнено для qiCost, UltimateQiCost)
4. **Концептуальные расхождения** — пользователь принимает решение

---

## УЖЕ ИСПРАВЛЕНО ✅ (коммит 5d51a9f)

| # | Fix | Аудит | Файл |
|---|-----|-------|------|
| 1 | qiCost formula `capacity*0.15` → doc formula | AUDIT-4 F1 | TechniqueGeneratorService.cs |
| 2 | UltimateQiCostMultiplier 1.5f → 2.0f | AUDIT-4 F2 | Constants.cs + TechniqueData.cs |
| 3 | StorageRing float→long (ЗАПРЕТ 2) | AUDIT-4 F3 | StorageRingService.cs + IStorageRingService.cs |
| 4 | Charger position 6→14 | AUDIT-3 M9 + AUDIT-4 | GameLifetimeScope.cs |
| 5 | Esc-close inventory → Time.Resume() | AUDIT-3 C4 | GameWorldController.cs |
| 6 | HandlePickup distance fix | AUDIT-3 M3 | GameWorldController.cs |
| 7 | GroundItems ZIndex Objects+1→Objects | AUDIT-3 M1 | GroundItemRenderer.cs |
| 8 | PlayerInputService meditate bug | AUDIT-3 C5 | PlayerInputService.cs |
| 9 | "meditate" action added | AUDIT-3 M14 | InputMapInitializer.cs |
| 10 | Dead "i" check removed | AUDIT-3 | PlayerInputService.cs |
| 11 | TestItemSeeder #if DEBUG gate | AUDIT-3 M6 | InventoryWindow.cs |
| 12 | TechniqueData comments corrected | AUDIT-4 F1 | TechniqueData.cs |

---

## КОНЦЕПТУАЛЬНЫЕ ВОПРОСЫ для пользователя (14)

### Из AUDIT-1 (Core)

**Q1: NPCState — где должен жить?**
- A: Перенести в Core/Data (рекомендую — это DTO, 144 строки, нет логики)
- B: Интерфейс INPCStateView в Core
- C: Оставить в Modules (нарушение архитектуры)

**Q2: BodyPart — где должен жить?**
- A: Перенести в Core/Data (рекомендую — 272 строки, нет engine deps)
- B: Интерфейс IBodyPartView в Core
- C: Оставить в Modules (нарушение архитектуры)

### Из AUDIT-2 (Modules)

**Q3: SetConfig wiring — как внедрять config в 10 модулей?**
- A: [Inject] в модуле (рекомендую — стандартный DI)
- B: Resolve из Container
- C: Оставить (системы не инициализируются корректно)

**Q4: PlayerService HP — как связать с Body?**
- A: Делегировать BodyService (рекомендую — единая HP)
- B: Синхронизировать (дублирование)
- C: Оставить (игрок бессмертный)

**Q5: Random.Shared в combat — детерминированность?**
- A: Injectable SeededRandom (рекомендую — для тестов/save)
- B: Оставить Random.Shared (non-deterministic)

**Q6: NPC↔Generator cycle — как разорвать?**
- A: Перенести weight tables в Core (рекомендую)
- B: Отложить (работает, но нарушение архитектуры)

### Из AUDIT-3 (Entry + Adapter)

**Q7: Movement — real-time или tick-based?**
- A: Real-time, убрать `*= Time.Speed` (рекомендую — простой фикс)
- B: Tick-based (перенести в PlayerModule.Tick)
- C: Оставить (баг на Fast/Quick speed)

**Q8: Save/load — когда реализовать?**
- A: Отложить (рекомендую — сейчас не критично)
- B: Реализовать сейчас (требует SceneOrchestrator fix + metadata)

### Из AUDIT-4 (Docs compliance)

**Q9: Spirit+Ring storage — unified или separate?**
- A: Оставить unified (рекомендую — DRY, работает)
- B: Разделить строго по доке
- C: Отложить

**Q10: Element.Poison — оставить или убрать?**
- A: Оставить (рекомендую — работает как маркер для техник яда)
- B: Убрать, использовать Neutral + status flag
- C: Отложить

**Q11: ЗАПРЕТ 8 TileMapLayer — мигрировать или оставить _Draw?**
- A: Отложить, оставить _Draw (рекомендую — работает, culling реализован)
- B: Мигрировать на TileMapLayer (~3-5 дней)
- C: Отложить

**Q12: ItemCategory — добавить QiStone?**
- A: Оставить как есть (рекомендую — QiStone добавим с экономикой)
- B: Добавить QiStone сейчас
- C: Отложить

### Дополнительные

**Q13: EventBus re-entrancy protection — как реализовать?**
- A: Queue re-entrant events (рекомендую — события не теряются)
- B: Throw на re-entrancy (краш с понятным сообщением)
- C: Отложить (риск StackOverflow)

**Q14: GameTile readonly struct — рефакторить?**
- A: Отложить (рекомендую — работает, рефакторинг рискованный)
- B: readonly struct + factory methods (~2 часа)
- C: Convert to class (GC pressure)

---

## ПЛАН ДЕЙСТВИЙ

### Этап 1: Ожидание решений (14 вопросов)
Пользователь должен ответить на Q1-Q14. Без ответов я не могу продолжать — каждый вопрос имеет несколько вариантов с разными последствиями.

### Этап 2: Применение исправлений (после ответов)
Для каждого вопроса с выбранным вариантом A/B — применить соответствующее исправление.

### Этап 3: Отложенные элементы (НЕ ТРЕБУЮТ РЕШЕНИЙ)
Все "не реализовано" — будет реализовано позже. Документация НЕ редактируется.

---

## Приоритеты исправлений (после решений)

### P0 — Критические (блокируют корректную работу)
1. Q3: SetConfig wiring (10 модулей) — без этого Body/Qi/Combat не инициализируются
2. Q4: PlayerService HP — без этого игрок бессмертный
3. Q13: EventBus re-entrancy — без этого риск краша

### P1 — Важные (влияют на gameplay)
4. Q1: NPCState в Core
5. Q2: BodyPart в Core
6. Q7: Movement speed scaling
7. EquipmentDataProvider fix (M4 → M3)
8. BodyModule DoT damage
9. TimeService.DeltaTime fix
10. PlayerModule.Start spawn removal (LargeWorld фикс)

### P2 — Архитектурные
11. Q5: SeededRandom в combat
12. Q6: NPC↔Generator cycle
13. DI improvements (M7, M8)
14. ObjectDefaults defensive default

### P3 — Отложить
- Q8: Save/load (позже)
- Q11: TileMapLayer migration (позже)
- Q14: GameTile readonly (позже)
- Все "не реализовано" (позже)

---

## Файлы

- `checkpoints/08_21_audit1_core.md` — Core layer (ПОДРОБНЫЙ)
- `checkpoints/08_21_audit2_modules.md` — Modules layer (ПОДРОБНЫЙ)
- `checkpoints/08_21_audit3_entry_adapter.md` — Entry + Adapter (ПОДРОБНЫЙ)
- `checkpoints/08_21_audit4_docs_compliance.md` — Docs compliance (ПОДРОБНЫЙ)
- `checkpoints/08_22_audit_fix_plan.md` — этот сводный план
