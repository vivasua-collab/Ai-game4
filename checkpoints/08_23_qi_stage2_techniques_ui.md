# Чекпоинт: Этап 2 — UI техник + каст + расход Ци + кулдауны

**Дата:** 2026-08-23
**План:** `checkpoints/08_23_qi_impl_plan.md` (этап 2)
**Статус:** ✅ Завершён, сборка чистая, headless-проверка пройдена.

## Что сделано

### 1. PlayerTechniqueCaster (`Modules/Player/PlayerTechniqueCaster.cs`)
Каст техник игрока по команде `TechniqueCastRequestedEvent` (Adapter: Z / двойной клик в панели):
- **Combat**: цель = ближайший живой NPC в Range техники (Range метры / 2, минимум 2 тайла) → `AttackIntentEvent(techniqueId)` → полный 11-слойный боевой конвейер. Проверка цели ДО расхода Ци.
- **Healing**: лечение раненых частей — BaseDamage распределяется по самым раненым (IBodyService.HealPart).
- **Defense**: активация Ци-буфера (QiBufferActivateRequestEvent, режим Shield, инвестиция max(50, qiCost)).
- **Movement**: dash на 3 тайла в сторону курсора (IPlayerService.SetPosition).
- **Sensory**: расход + событие (тост-схематика).
- **Support/Curse**: расход + тост + визуал (схематично; реальные баффы/дебаффы — через BuffService позже).
- **Formation**: отказ с сообщением (этап 5 подключит).
- **Cultivation**: пассивная — каст невозможен.
- Все пути публикуют `TechniqueCastResultEvent` (success/reason + точки origin/target + VisualKind для этапа 3).
- Расход Ци + кулдаун + мастерство: через `TechniqueService.UseTechnique` (QiConsumeRequestEvent — Hub-and-Spoke).

### 2. TechniquesPanel (`Adapter/UI/TechniquesPanel.cs`)
- Панель по T, **non-modal** (бой не прерывается), левый край экрана.
- Строки: [emoji стихии] Название L{уровень} [грейд] | ⚔урон Ци:x КД:yс М:z% [пассив].
- ЛКМ — выбрать активную (подсветка золотым), двойной клик — каст (с позицией курсора).
- Кулдауны live: ⏳Nс + Disabled; «⚠ мало Ци» при нехватке; Cultivation — Disabled.
- Заголовок: занятость слотов (боевые used/cap + проклятие + формация + культивация).
- Синхронизация: TechniqueLearned/Forget/SelectionChanged события.

### 3. Ввод
- Новый биндинг `cast_technique` (Z) — InputMapInitializer + InputAdapter sticky.
- **Фикс найденной дыры**: `meditate` (V) регистрировался в InputMap, но НЕ попадал в sticky-набор InputAdapter → IsMeditatePressed никогда не срабатывал. Теперь добавлен (этап 1 V-медитация реально работает).
- `special_action` (X) → цикл выбора техники (переиспользован свободный биндинг).
- IPlayerInputService: +IsCastTechniquePressed, +IsCycleTechniquePressed, +IsTechniquesPressed.

### 4. Интеграция GameWorldController
- Панель добавлена в HUD; TechniqueCaster.Start() в _Ready.
- T/X/Z обработка в _PhysicsProcess (до ResetFrameFlags).
- Подписка TechniqueCastResultEvent → тосты («✴ Техника применено» / «✖ причина»).
- Подсказка хоткеев дополнена (Z — каст, X — выбор).

## События (новые, CombatContracts.cs)
- `TechniqueCastRequestedEvent(techniqueId, mouseX, mouseY)`
- `TechniqueCastResultEvent(success, reason, origin, target, type, element, visualKind)`

## Проверка (headless)
```
[TechniqueGrant] seed=30933593
[Phase 45] TechniqueGrant complete — granted 6 techniques ...
[TechniquesPanel] Ready
```

## Клавиши (новые)
- T — панель техник (toggle, non-modal)
- X — следующая техника (цикл выбора)
- Z — каст выбранной техники (в направлении курсора)
