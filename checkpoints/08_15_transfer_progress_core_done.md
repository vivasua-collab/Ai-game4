# Чекпоинт: Перенос Core + частичный Modules

**Дата:** 2026-08-15 07:15 UTC
**Тип:** migration | progress

---

## Контекст

Запущены 3 параллельных агента для переноса Core (Data, Interfaces, Messaging) + 2 агента для Modules (Calculators/Configs и Services). Core перенесён полностью, Modules — частично.

## Что сделано

### Core (полностью перенесён) ✅
- **Core/Data**: 14 файлов (Constants 1467 строк, Enums 1086 строк, все structs/data models)
- **Core/Interfaces**: 37 файлов (20 замен + 17 новых) — ICombatService с 11-layer pipeline, IQiService с long arithmetic, все per-entity providers
- **Core/Messaging/Contracts**: 20 файлов с 118 событиями (было 50)
- **Итого Core**: 85 файлов, 0 ошибок компиляции в Core

### Modules (частично перенесён) ⚠️
- **Calculators + Configs**: перенесены (Body 12, Combat 18, NPC 20 файлов)
- **Services**: частично (Player 3→6, Tile 3→4, Quest/UI/World не перенесены)
- **Итого Modules**: 134 файла (было 50)
- **Ошибки**: 204 (80 CS1503 type mismatch, 64 CS1061 missing member, 16 CS0117, 12 CS1501)

## Текущие ошибки

| Категория | Кол-во | Причина |
|-----------|--------|---------|
| CS1503 | 80 | Type mismatch — Services используют типы, которые не переносятся (Unity Vector2 vs наш Vector2f) |
| CS1061 | 64 | Missing member — интерфейсы из Ai-game3 богаче, stubs не реализуют новые методы |
| CS0117 | 16 | Static method not found — Calculators вызывают методы, которых нет в наших типах |
| CS1501 | 12 | Method signature mismatch — `in` keyword адаптация не завершена |
| CS1678/CS1661 | 12 | Delegate signature mismatch — handler `in` keyword |

## Решения

- **Core перенесён успешно** — 0 ошибок, можно считать завершённым
- **Modules Calculators/Configs перенесены** — чистая логика, компилируются
- **Modules Services требуют доработки** — нужно:
  1. Доперевести Quest/UI/World Services (3 модуля по 3 файла — stubs)
  2. Адаптировать handler signatures (`in` keyword)
  3. Заменить Unity типы (Vector2→Vector2f, Mathf→Math)
  4. Адаптировать Entry/Phases под новые интерфейсы

## Файлы

- Core: 85 файлов (Data 23, Interfaces 41, Messaging 21)
- Modules: 134 файла (Body 12, Buff 7, Charger 7, Combat 18, Formation 8, Generator 6, Interaction 9, Inventory 15, NPC 20, Player 6, Qi 8, Quest 3, Save 5, Tile 4, UI 3, World 3)
- Entry: 17 файлов (не адаптированы под новые интерфейсы)
- Adapter: 10 файлов (Godot-specific, не затронуты)

## Следующие шаги

1. Доперенести Quest/UI/World Module Services
2. Массовая адаптация: `void Handler(Event e)` → `void Handler(in Event e)`
3. Массовая замена: `Mathf.X` → `System.Math.X`, `Vector2` → `Vector2f`
4. Адаптировать Entry/Phases под новые ISceneAssemblyPhase
5. Верификация сборки
