# Чекпоинт: Перенос завершён, DI адаптация требуется

**Дата:** 2026-08-15 07:30 UTC
**Тип:** migration | progress

---

## Контекст

Перенос кода из Ai-game3 в Ai-game4 завершён. Build: 0 errors, 255 warnings. Но runtime падает с DI ошибкой.

## Что сделано

### Перенесено (253 .cs файла, было 120)
- **Core**: 85 файлов (Data 23, Interfaces 41, Messaging 21)
  - Constants.cs (1467 строк), Enums.cs (1086 строк)
  - 37 интерфейсов с полными контрактами
  - 118 readonly struct событий
- **Modules**: 141 файл (было 50)
  - Все 16 модулей с реальной логикой
  - Calculators (11-layer damage pipeline, Qi long math, body system)
  - Services (NPCService 35KB, CombatService 40KB, BodyService 44KB)
  - Configs, Data classes
- **Entry**: 17 файлов (адаптированы под новые интерфейсы)
- **Adapter**: 10 файлов (Godot-specific, обновлены)

### Build статус
- **0 errors** ✅
- 255 warnings (unused fields — косметика, не блокирует)

### Runtime статус
- MainMenu Ready ✅
- DI container build ✅
- Module resolution ❌ — `NPCAIService` constructor требует `NPCService`, но наш DI не поддерживает constructor injection

## Проблема

Ai-game3 Services используют **constructor injection** (VContainer pattern):
```csharp
public NPCAIService(INPCService npcService, ...) { ... }
```

Наш DI container поддерживает только **property injection**:
```csharp
[Inject] private INPCService NpcService { get; set; }
```

Container пытается resolver через constructor, но не может найти `NPCService` (registered as `INPCService`, не как concrete type).

## Решение (следующий шаг)

Вариант A: Адаптировать все Services — constructor → property injection (большая работа, 100+ файлов)

Вариант B: Расширить наш DI Container — поддержать constructor injection (как VContainer). Это правильнее — меньше изменений в перенесённом коде.

**Рекомендация: Вариант B.** Container уже умеет property injection. Добавить constructor injection:
1. При resolve, если класс не имеет `[Inject]` properties, использовать greediest constructor
2. Resolver constructor параметры через container
3. Это совместимо с Ai-game3 паттерном

## Файлы

- `/home/z/my-project/Ai-game4/game/src/Core/DI/Container.cs` — нужно расширить
- 141 Module Service файл — НЕ менять (работают с constructor injection)

## Следующие шаги

1. Расширить Container.cs — поддержать constructor injection
2. Headless верификация
3. Скриншоты
4. Git commit + push
