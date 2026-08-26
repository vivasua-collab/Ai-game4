# Чекпоинт: Этап 1 — Выдача техник игроку + слоты + Ци HUD + медитация

**Дата:** 2026-08-23
**План:** `checkpoints/08_23_qi_impl_plan.md` (этап 1)
**Статус:** ✅ Завершён, сборка чистая, headless-проверка пройдена.

## Что сделано

### 1. Слотовая модель техник (TECHNIQUE_SYSTEM.md §12)
`Modules/Combat/TechniqueService.cs`:
- `SlotCategory(type)` — Cultivation/Combat/Curse/Formation; все активные типы (Defense/Support/Healing/Movement/Sensory/Poison) → пул Combat.
- `SlotCapacity(category, level)` — Cultivation 1, Combat 3+(L−1), Curse 1, Formation 1.
- `FreeSlots/UsedSlots` — учёт занятости.
- `LearnTechnique(TechniqueData)` — проверка слота + резонанса уровней (§8.1: max(1, L−4) ≤ L_техники ≤ L).
- `ForgetTechnique/ForgetAll` — для чит-панели (этап 7).
- `SelectTechnique/CycleSelection` — выбор активной техники для каста (этап 2).
- `UseTechnique` — рост мастерства +0.01 за использование (§5.1 шаг 5).
- `LearnedTechnique` расширен: Name, Level, Range, Mastery.
- Новые события: `TechniqueLearnedEvent`, `TechniqueForgottenEvent`, `TechniqueSelectionChangedEvent` (CombatContracts.cs).

### 2. Генератор: явный тип + фикс Cultivation cap=0
`Modules/Generator/TechniqueGeneratorService.cs`:
- `GenerateSpecified(type, level, cultivationLevel, seed)` — техника заданного типа (для тест-набора).
- Cultivation — пассивная (qiCost=0, BaseDamage=0, capacity=0, без Ultimate) — и в `Generate`, и в `GenerateSpecified` (баг из SESSION_CONTEXT §7 «Cultivator technique cap=0» закрыт).
- `ITechniqueGeneratorService` расширен.

### 3. Тест-набор техник при старте
`Entry/Phases/TechniqueGrantPhase.cs` (PhaseOrder=45, после PlayerSpawn):
- Cultivation-слот: 1 пассивная техника.
- Combat-пул: наполняется до 3+(L−1) случайными активными техниками (цикл типов Combat/Defense/Support/Healing/Movement/Sensory).
- Curse-слот: 1 проклятие. Formation-слот: 1 техника формаций (для этапа 5).
- Seed = Environment.TickCount, логируется для воспроизведения.
- Headless-проверка: `granted 6 techniques (cultivation 1, active 3/3, curse, formation) at L1`.

### 4. Ци HUD + медитация (QI_SYSTEM.md §5.2)
`Adapter/Scene/GameWorldController.cs`:
- Qi-бар под HP-баром (золотой, затухает при истощении) + подпись `Ци cur/max | L1.0 | пров. X.X/с`.
- Индикатор «☯ Медитация» при активной медитации.
- V — переключение медитации (публикация MeditationToggleRequestedEvent).
- Движение прерывает медитацию.

`Modules/Qi/QiModule.cs`:
- Владеет состоянием медитации: подписка на MeditationToggleRequestedEvent и CombatStartedEvent (бой прерывает).
- Tick: поглощение = conductivity × ENVIRONMENT_MULT_NORMAL (0.5, FORMATION_SYSTEM §10.2), double-аккумулятор для целочисленного AddQi (ЗАПРЕТ 2).
- Авто-завершение при полном ядре. Публикация MeditationStateChangedEvent.

`Core/Messaging/Contracts/QiContracts.cs`: MeditationToggleRequestedEvent, MeditationStateChangedEvent.
`Core/Data/Constants.cs`: ENVIRONMENT_MULT_NORMAL = 0.5f.

## Проверка
```
[TechniqueGrant] seed=30175187
[Phase 45] TechniqueGrant complete — granted 6 techniques (cultivation 1, active 3/3, curse, formation) at L1
```
Сборка: 0 ошибок. Регрессий физического прототипа нет (фазы 1-10 прошли).

## Горячие клавиши (новые)
- V — медитация вкл/выкл (движение/бой прерывает).
