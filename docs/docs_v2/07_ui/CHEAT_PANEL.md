# Чит-меню разработки (CheatPanel, F1)

**Дата создания:** 2026-08-27 (Phase G)
**Исходник:** `game/src/Adapter/UI/CheatPanel.cs` (больше 480 строк, #if DEBUG)
**Управление:** клавиша `F1` — открыть/закрыть панель в любой момент игры.

---

## Назначение

Чит-меню — инструмент разработки для тестирования подсистем культивации:
- Установка уровня культивации L1..L9.
- Заполнение / добавление Ци, прорыв.
- Выдача случайных техник, очистка техник.
- Выдача камней Ци.
- Создание формаций (Gathering, cycle по типам/размерам/уровням).
- Тоггл быстрой утечки формации (×1 / ×10).
- Генерация экипировки (оружие/броня/рандом/зачарование).
- Генерация расходников + зарядников Ци.
- Связка: техника-формация + старт прорисовки.
- Dump границ уровней + подсчёт дублей в реестре техник.

Все кнопки работают в DEBUG-сборке. В release-сборке класс `CheatPanel`
не компилируется благодаря директиве `#if DEBUG ... #endif`.

---

## Структура UI

```
┌─ CheatPanel (Panel, top-left, 280×~700px) ───────────┐
│ ⚡ ЧИТ-МЕНЮ (F1)                                       │
│ dev-only, #if DEBUG                                   │
├───────────────────────────────────────────────────────┤
│ ▸ Уровень культивации: [L1][L2]...[L9]                │
│ ▸ Ци: [Заполнить Ци][+10000 Ци]                       │
│       [Прорыв ▲]                                       │
│ ▸ Техники: [Выдать 3 рандом][Очистить]                │
│ ▸ Камни Ци: [Выдать 3 камня]                          │
│ ▸ Формации: [Создать формацию Сбора]                  │
│             [Формация (cycle: тип/размер/уровень)]    │
│             [Утечка ×1 (выкл)]                         │
│ ▸ Экипировка: [Оружие cycle][Броня cycle][Рандом]    │
│               [Оружие + зачарование]                   │
│ ▸ Расходники: [Расходник][Зарядник Ци]                │
│ ▸ Техника + формация: [Создать Combat-Formation]     │
│ ▸ Верификация: [Dump LevelBoundaries]                 │
│                [Подсчёт дублей техник]                 │
├───────────────────────────────────────────────────────┤
│ [статус-строка]                                       │
└───────────────────────────────────────────────────────┘
```

---

## Injections (DI-зависимости)

Все инжектируются через `[Inject]` (property injection via ContainerAdapter):

| Сервис | Назначение |
|--------|------------|
| `IQiService Qi` | Уровень культивации, Ци, прорыв |
| `IPlayerService Player` | Позиция игрока (для формаций) |
| `IInventoryService Inventory` | Добавление предметов в инвентарь |
| `IItemDatabaseService ItemDatabase` | Поиск камней Ци |
| `ITechniqueGeneratorService TechniqueGenerator` | Генерация техник |
| `TechniqueService Techniques` | Изучение техник (LearnTechnique) |
| `IFormationService Formations` | StartDrawing / ContributeQi |
| `IFormationGeneratorService FormationGenerator` | GenerateSpecified |
| `FormationConfig FormationCfg` | DrainSpeedMultiplier |
| `IEquipmentGenerator EquipmentGenerator` | Оружие/броня + зачарование |
| `IItemGeneratorService ItemGenerator` | Расходники + зарядники |
| `IVerificationService Verifier` | (для будущих кнопок валидации) |
| `DeduplicationService Dedup` | Подсчёт дублей |
| `TechniqueRegistry TechniqueRegistry` | GetAll() для подсчёта |
| `IPublisher<ToastShownEvent> ToastPub` | Тосты |

---

## Кнопки: API и поведение

### Уровень культивации (L1..L9)

Вызывает `Qi.SetCultivationLevel(level, 0)` + `Qi.AddQi(maxQi)` (заполнить
до нового максимума).

### Ци

- **Заполнить Ци**: `Qi.AddQi(maxQi - currentQi)` — заполнить до максимума.
- **+10000 Ци**: `Qi.AddQi(10000)`.
- **Прорыв ▲**: `Qi.TryBreakthrough()` — попытка прорыва на следующий
  подуровень (зависит от прогресса, см. QI_SYSTEM.md).

### Техники

- **Выдать 3 рандом**: цикл по пулу типов (Combat/Defense/Healing/Movement/
  Support/Sensory), `TechniqueGenerator.GenerateSpecified(type, level, level, seed+i)`,
  затем `Techniques.LearnTechnique(tech)`.
- **Очистить**: `Techniques.ForgetAll()`.

### Камни Ци

- **Выдать 3 камня**: выбирает 3 камня Ци из `QiStoneSeeder.AllItemIds()`,
  добавляет через `Inventory.TryAddItem(item, 1)`.

### Формации

- **Создать формацию Сбора**: `FormationGenerator.GenerateSpecified(Gathering, Small, level, seed)`,
  `Formations.StartDrawing(id, playerId, x, y)`, мгновенное наполнение
  `ContributeQi(playerId, poolMax)`.
- **Формация (cycle)**: цикл по типам (8 шт) × размерам (5 шт) × уровням (1..9).
  Один клик = следующий тип. При полном обороте типов → следующий размер.
  При полном обороте размеров → следующий уровень.
- **Утечка ×1/×10**: `FormationCfg.DrainSpeedMultiplier` (10 — для быстрого
  теста истощения и автодеактивации).

### Экипировка (Phase F, 2026-08-27)

- **Оружие cycle**: cycle по `EquipmentGenerationTables.Weapons` (7 подтипов:
  dagger/sword/axe/spear/greatsword/bow/staff). Один клик = следующий
  подтип. Уровень = текущий cultivationLevel.
- **Броня cycle**: cycle по `EquipmentGenerationTables.Armors` (6 подтипов:
  head/torso/arms/legs/feet/belt).
- **Рандом**: 50/50 оружие или броня.
- **Оружие + зачарование**: генерирует sword (уровень cultivationLevel) +
  накладывает зачарование `EquipmentGenerator.TryApplyEnchant(weapon, null, seed)`.

### Расходники

- **Расходник**: `ItemGenerator.GenerateConsumableForLevel(level, seed)`.
- **Зарядник Ци**: `ItemGenerator.GenerateChargerForLevel(level, seed)`
  (требует L3+ для belt-слота).

### Техника + формация

- **Создать Combat-Formation + старт**: генерирует Formation-технику
  (`TechniqueGenerator.GenerateSpecified(Formation, level, level, seed)`)
  + изучает её, затем генерирует Gathering-формацию и стартует её в позиции
  игрока.

### Верификация

- **Dump LevelBoundaries**: печатает в лог границы для Combat-Common и
  Combat-Transcendent текущего уровня (демонстрация `LevelBoundaries.TechniqueBoundsFor`).
- **Подсчёт дублей техник**: `Dedup.CountDuplicates(registry.GetAll(), fingerprint)`.
  Показывает, есть ли дубли по характеристикам в реестре.

---

## Как добавить новую кнопку

1. В `BuildUI()` создать `Button` через `MakeButton(text, minWidth, action)`.
2. Добавить в `vbox` (или новый HBoxContainer).
3. Реализовать `OnXxx()` метод — инжекции уже есть.
4. (Опционально) Если нужны новые сервисы — добавить `[Inject]` поле.
5. `SetStatus(...)` в конце — для тоста и лога.

Пример:

```csharp
private void OnMyAction()
{
    if (SomeService == null) return;
    int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
    // ... вызов сервиса ...
    SetStatus($"Действие выполнено (level={level})");
}
```

В `BuildUI`:
```csharp
vbox.AddChild(MakeButton("Моя кнопка", 264, OnMyAction));
```

---

## Известные ограничения / TODO

- **Visual rendering формаций**: в headless sim-режиме lifecycle формации
  работает (Drawing→Filling→Active→Depleted), но визуальной отрисовки
  на canvas-слое нет. См. `FormationVisualizer` (если есть) — TBD.
- **Weapon switching system**: слоты 1/2 (ближнее/дальнее) сейчас только
  тосты. Полная система переключения оружия — TBD.
- **Сохранение TechniqueSlotService ISaveable**: SaveDataAggregator не
  собирает ISaveable автоматически — TBD.

---

## Связанные документы

- `docs/docs_v2/02_systems/LEVEL_BOUNDARIES.md` — границы уровней.
- `docs/docs_v2/02_systems/VERIFICATION_SYSTEM.md` — VerificationService.
- `docs/docs_v2/02_systems/PRE_GENERATION.md` — pred-generation pipeline.
- `docs/docs_v2/07_ui/HOTKEYS.md` — система горячих клавиш.
- `docs/docs_v2/02_systems/TECHNIQUE_SYSTEM.md` — система техник.
- `docs/docs_v2/02_systems/FORMATION_SYSTEM.md` — система формаций.
