# Правила тестирования (Testing Rules)

> **Назначение:** Движко-независимая спецификация правил тестирования: структура тестов, паттерны, naming, типы тестов, mock-объекты, верификация баланса, headless-запуск. Без привязки к конкретному test-фреймворку.
>
> **Связанные документы:** `AI_DEVELOPMENT_WORKFLOW.md`, `ALGORITHMS.md`, `01_architecture/DI_AND_EVENTBUS.md`, `01_architecture/MODULE_STRUCTURE.md`.

---

## 1. Принципы

1. **AAA Pattern** — все тесты следуют паттерну Arrange-Act-Assert.
2. **Изоляция** — каждый тест независим; глобальное состояние запрещено.
3. **Очистка** — после каждого теста все ресурсы освобождаются (TearDown).
4. **Именование** — `Method_Scenario_ExpectedResult`.
5. **Edge cases обязательны** — минимум 3 граничных случая на модуль.
6. **Mock через интерфейсы** — моки реализуют интерфейсы, не наследуют конкретные классы.
7. **Pure C# тестируется без движка** — все игровые системы (16 модулей core) тестируются через `dotnet test` без движка.
8. **Движко-зависимый код тестируется отдельно** — UI, сцены, рендеринг — через тест-фреймворк движка или скриншот-тесты.

---

## 2. Структура тестов

### 2.1. Дерево каталогов

```
tests/
├── Core/                           # Тесты чистого C# core
│   ├── ValidationTests             # Базовая проверка сборки
│   └── GameConstantsTests          # Тесты констант
├── Modules/
│   ├── Body/
│   │   ├── BodyPartTests
│   │   ├── BodyDamageCalculatorTests
│   │   └── BodyServiceTests
│   ├── Buff/
│   │   ├── BuffCalculatorTests
│   │   └── BuffServiceTests
│   ├── Combat/
│   │   ├── DamageCalculatorTests
│   │   ├── DefenseProcessorTests
│   │   ├── LevelSuppressionTests
│   │   └── TechniqueCapacityTests
│   ├── Qi/
│   │   ├── QiBufferServiceTests
│   │   ├── QiRegenCalculatorTests
│   │   ├── QiBreakthroughCalculatorTests
│   │   └── QiServiceTests
│   ├── Tile/
│   │   ├── TileMapServiceTests
│   │   └── DestructibleServiceTests
│   ├── Charger/                    # TBD
│   ├── Formation/                  # TBD
│   ├── Inventory/                  # TBD
│   ├── NPC/                        # TBD
│   ├── Player/                     # TBD
│   ├── Quest/                      # TBD
│   ├── Save/                       # TBD
│   ├── Interaction/                # TBD
│   └── World/                      # TBD
├── Integration/                    # Интеграционные тесты
├── Balance/                        # Верификация баланса
└── TestUtilities/                  # Хелперы и моки
    ├── TestContainerBuilder        # DI-конфигуратор для тестов
    ├── EventBusTestHelper          # Хелпер для тестов событий
    ├── MockQiService               # Мок IQiService
    ├── MockBodyService             # Мок IBodyService
    ├── MockBuffService             # Мок IBuffService
    ├── MockTimeService             # Мок ITimeService
    └── MockInventoryService        # Мок IInventoryService
```

> Структура повторяет структуру модулей (см. `01_architecture/MODULE_STRUCTURE.md`).

### 2.2. Покрытие по модулям (текущее состояние)

| Модуль | Статус | Тестов |
|--------|--------|--------|
| Core (Validation, Constants) | ✅ | 18 |
| Body | ✅ | 28 |
| Buff | ✅ | 13 |
| Combat | ✅ | 31 |
| Qi | ✅ | 31 |
| Tile | ✅ | 17 |
| Charger | 🔲 TBD | — |
| Formation | 🔲 TBD | — |
| Inventory | 🔲 TBD | — |
| NPC | 🔲 TBD | — |
| Player | 🔲 TBD | — |
| Quest | 🔲 TBD | — |
| Save | 🔲 TBD | — |
| Interaction | 🔲 TBD | — |
| World | 🔲 TBD | — |
| Integration | 🔲 TBD | — |
| Balance | 🔲 TBD | — |

---

## 3. Test Framework

### 3.1. Нейтральность к фреймворку

Документ не предписывает конкретный test-фреймворк. Возможные варианты:

| Фреймворк | Назначение | Примечание |
|-----------|------------|------------|
| xUnit | .NET unit testing | Рекомендуется для Godot C# |
| NUnit | .NET unit testing | Альтернатива |
| Godot test framework (GUT) | Godot-зависимые тесты | Для сцен и UI |
| Custom | Собственный | Не рекомендуется |

> Все примеры ниже используют **нейтральный синтаксис** (NUnit-подобный), но легко переносятся в xUnit.

### 3.2. Атрибуты

| Атрибут | Назначение |
|---------|------------|
| `[TestFixture]` | Класс с тестами |
| `[Test]` | Один тест (синхронный) |
| `[Test]` async | Один тест (асинхронный, через `async Task`) |
| `[SetUp]` | Выполняется перед каждым тестом |
| `[TearDown]` | Выполняется после каждого теста |
| `[OneTimeSetUp]` | Выполняется один раз перед всеми тестами в классе |
| `[OneTimeTearDown]` | Выполняется один раз после всех тестов в классе |
| `[Ignore("reason")]` | Пропустить тест |
| `[Category("name")]` | Категория для группировки |

---

## 4. AAA Pattern

Все тесты следуют паттерну **Arrange-Act-Assert**:

```csharp
[Test]
public void Test_Example()
{
    // === Arrange ===
    int expected = 10;
    int value = 5;

    // === Act ===
    int result = value * 2;

    // === Assert ===
    Assert.AreEqual(expected, result);
}
```

---

## 5. Именование тестов

### 5.1. Формат

```
[Метод]_[Сценарий]_[ОжидаемыйРезультат]
```

### 5.2. Примеры

```csharp
[Test]
public void LevelSuppression_SameLevel_ReturnsFullDamage() { }

[Test]
public void QiBuffer_InsufficientQi_PartialAbsorption() { }

[Test]
public void TechniqueCapacity_Ultimate_HasMultiplier() { }

[Test]
public void TakeDamage_ReducesRedHP_70Percent() { }
```

### 5.3. Категории имён

| Категория | Префикс | Пример |
|-----------|---------|--------|
| Конструктор | `Constructor_` | `Constructor_SetsPartType` |
| Свойство | `Property_` | `Property_CurrentQi_ReturnsValue` |
| Метод | `[MethodName]_` | `TakeDamage_ReducesRedHP_70Percent` |
| Edge case | `[MethodName]_Edge_` | `TakeDamage_Edge_ZeroDamage_NoChange` |
| Интеграционный | `Integration_` | `Integration_QiService_TechniqueService_CombatFlow` |
| Баланс | `Balance_` | `Balance_QiRegen_10PercentPerDay` |

---

## 6. Setup и Teardown

```csharp
private SomeService _service;

[SetUp]
public void Setup()
{
    // Создаём сервис перед каждым тестом
    var mockDeps = new MockDeps();
    _service = new SomeService(mockDeps);
}

[TearDown]
public void TearDown()
{
    // Освобождаем ресурсы после каждого теста
    _service?.Dispose();
    _service = null;
}
```

### 6.1. Правила

- **Никакого глобального состояния.** Если нужно состояние — оно в поле класса, инициализируется в SetUp.
- **Каждый тест получает свежий сервис.** Не переиспользуем между тестами.
- **TearDown обязателен** для тестов, создающих ресурсы (файлы, сети, БД).

---

## 7. Типы тестов

### 7.1. Unit тесты

Тестируют один метод или класс в изоляции:

```csharp
[Test]
public void LevelSuppression_CalculatesCorrectly()
{
    // Тестируем только LevelSuppression.CalculateSuppression
    float result = LevelSuppression.CalculateSuppression(1, 3, AttackType.Normal);
    Assert.AreEqual(0.0f, result);
}
```

### 7.2. Интеграционные тесты

Тестируют взаимодействие между системами:

```csharp
[Test]
public void Integration_QiService_TechniqueService_CombatFlow()
{
    // Создаём обе системы с моками
    var qiService = new QiService(new MockTimeService());
    var techService = new TechniqueService(qiService);

    qiService.SetCultivationLevel(3, 5);
    qiService.RestoreFull();

    // Проверяем, что техника может быть использована
    bool canUse = techService.CanUseTechnique(learnedTech);
    Assert.IsTrue(canUse);
}
```

### 7.3. Edge Cases

Обязательные граничные случаи:

| Тип | Пример |
|-----|--------|
| Нулевые значения | `QiBuffer_ZeroDamage_NoConsumption` |
| Отрицательные значения | Проверка защиты от invalid input |
| Overflow | `Test_EdgeCase_QiOverflow` |
| Минимальные значения | Level 1 vs Level 10 |
| Максимальные значения | Проверка капов |
| Граничные условия | Точно на пороге (HP = 1, Qi = 0) |

> **Минимум 3 граничных случая на модуль.**

---

## 8. Mock объекты

### 8.1. Принцип

Моки реализуют **интерфейсы**, не наследуют конкретные классы. Это обеспечивает:
- Изоляцию от реальных зависимостей.
- Контроль над возвращаемыми значениями.
- Возможность проверки вызовов.

### 8.2. Пример мока

```csharp
public class MockQiService : IQiService
{
    private long _currentQi;
    private long _maxQi;
    private int _cultivationLevel;

    public long CurrentQi => _currentQi;
    public long MaxQi => _maxQi;
    public int CultivationLevel => _cultivationLevel;

    public void SetCurrentQi(long value) => _currentQi = value;
    public void SetMaxQi(long value) => _maxQi = value;
    public void SetCultivationLevel(int level, int subLevel) => _cultivationLevel = level;

    public bool TryConsume(long amount)
    {
        if (_currentQi < amount) return false;
        _currentQi -= amount;
        return true;
    }

    public void RestoreFull() => _currentQi = _maxQi;
}
```

### 8.3. Использование мока

```csharp
[Test]
public void TryConsume_SufficientQi_ReturnsTrue()
{
    // Arrange
    var mockQi = new MockQiService();
    mockQi.SetCurrentQi(100);
    mockQi.SetMaxQi(100);

    // Act
    bool result = mockQi.TryConsume(30);

    // Assert
    Assert.IsTrue(result);
    Assert.AreEqual(70, mockQi.CurrentQi);
}
```

### 8.4. Существующие моки

| Мок | Интерфейс | Назначение |
|-----|-----------|------------|
| `MockQiService` | `IQiService` | Управление Ци |
| `MockBodyService` | `IBodyService` | Тело и HP |
| `MockBuffService` | `IBuffService` | Баффы и модификаторы |
| `MockTimeService` | `ITimeService` | Тики и время |
| `MockInventoryService` | `IInventoryService` | Инвентарь |

---

## 9. Верификация баланса

### 9.1. Класс BalanceVerification

Специальный класс для проверки формул и таблиц баланса:

```csharp
public static class BalanceVerification
{
    public static void QuickVerify()
    {
        VerifyCoreCapacityFormula();
        VerifyLevelSuppressionTable();
        VerifyQiBufferEfficiency();
        VerifyMeditationTime();
    }

    public static void VerifyCoreCapacityFormula() { /* ... */ }
    public static void VerifyLevelSuppressionTable() { /* ... */ }
    public static void VerifyQiBufferEfficiency() { /* ... */ }
    public static void VerifyMeditationTime() { /* ... */ }
}
```

### 9.2. Проверяемые значения

| Категория | Метод | Что проверяет |
|-----------|-------|---------------|
| Плотность Ци | `VerifyCoreCapacityFormula()` | `qiDensity = 2^(level-1)` |
| Подавление уровнем | `VerifyLevelSuppressionTable()` | Таблица подавления |
| Qi Buffer | `VerifyQiBufferEfficiency()` | 90% поглощение, 10% пробитие |
| Время медитации | `VerifyMeditationTime()` | Формула времени медитации |
| Все сразу | `QuickVerify()` | Все вышеперечисленное |

> Формулы — в `09_workflow/ALGORITHMS.md`.

---

## 10. Обязательные модули для тестирования

| Модуль | Приоритет | Что тестировать |
|--------|-----------|-----------------|
| Combat/DamageCalculator | Высокий | Все формулы урона |
| Combat/LevelSuppression | Высокий | Таблица подавления |
| Combat/QiBuffer | Высокий | Поглощение, пробитие, частичное |
| Qi/QiService | Высокий | Накопление, расход, регенерация |
| Combat/TechniqueCapacity | Средний | Множители по типу техник |
| Formation/FormationQiPool | Средний | Пул Ци формации |
| Save/SaveManager | Средний | Сохранение/загрузка, версионирование |
| Body/BodyPart | Высокий | HP, статусы частей |
| Buff/BuffCalculator | Средний | Суммирование, капы |
| Tile/TileMapService | Средний | Размещение, удаление тайлов |

### Минимальные требования

- **Unit тесты:** Каждый публичный метод с логикой.
- **Edge cases:** Минимум 3 граничных случая на модуль.
- **Интеграция:** Каждая связь между системами.

---

## 11. Headless-запуск тестов

### 11.1. Командная строка

Для CI/CD или запуска без GUI:

```bash
# .NET (чистый C# core)
dotnet test --filter "Category=Unit" --logger "console;verbosity=detailed"

# С фильтром по неймспейсу
dotnet test --filter "FullyQualifiedName~CultivationGame.Tests.Modules.Body"

# С фильтром по классу
dotnet test --filter "FullyQualifiedName~BodyPartTests"

# С результатами в XML (NUnit/xUnit формат)
dotnet test --logger "nunit;LogFilePath=results.xml"
```

### 11.2. Движко-зависимые тесты (Godot example)

```bash
# Headless запуск Godot
godot --headless --path . --script res://tests/run_tests.gd

# Проверка компиляции C#
dotnet build
```

### 11.3. Формат результатов

Результаты сохраняются в XML (NUnit 3 Test Results XML или xUnit xml):

```xml
<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="116" result="Passed" total="116" passed="116" failed="0">
  <test-suite type="Assembly" name="CultivationGame.Tests" result="Passed" total="116" passed="116">
    <test-suite type="TestFixture" name="BodyPartTests" result="Passed">
      <test-case name="Constructor_SetsPartType" result="Passed" duration="0.001" />
      <test-case name="TakeDamage_ReducesRedHP_70Percent" result="Passed" duration="0.002" />
    </test-suite>
  </test-suite>
</test-run>
```

### 11.4. Анализ результатов

| Метрика | Критерий успеха |
|---------|-----------------|
| Total | Общее количество тестов |
| Passed | Должно быть = Total |
| Failed | Должно быть = 0 |
| Inconclusive | Должно быть = 0 |
| Duration | Общее время (< 5 сек для unit-тестов) |

---

## 12. Отладка проваленных тестов

### 12.1. Алгоритм

1. **Прочитать сообщение ошибки** — Expected vs Actual.
2. **Запустить с отладчиком** — поставить breakpoint в тесте.
3. **Добавить diagnostic output** — `Console.WriteLine` промежуточных значений.
4. **Проверить типичные причины** (см. таблицу ниже).

### 12.2. Типичные причины провалов

| Причина | Симптом | Решение |
|---------|---------|---------|
| Формула изменена | Expected ≠ Actual | Обновить тест под новую формулу |
| Integer truncation | `(int)(1 * 0.7) = 0` | Использовать `Math.Max(1, ...)` |
| Мок не настроен | NullReferenceException | Проверить мок в SetUp |
| Namespace mismatch | CS0246 | Проверить `using` |
| Reference missing | CS0246 | Добавить сборку в references |
| Race condition | Тест проходит по отдельности, но падает в группе | Изоляция: проверить глобальное состояние |

### 12.3. Пример с диагностикой

```csharp
[Test]
public void TakeDamage_ReducesRedHP_70Percent()
{
    // Arrange
    var part = new BodyPart(BodyPartType.Torso, 100);
    Console.WriteLine($"[TEST] Before: RedHP={part.CurrentRedHP}");

    // Act
    bool result = part.TakeDamage(7, 3);
    Console.WriteLine($"[TEST] After: RedHP={part.CurrentRedHP}, result={result}");

    // Assert
    Assert.AreEqual(93, part.CurrentRedHP);
}
```

---

## 13. Архитектурные принципы тестов

### 13.1. Новый подход (vs Legacy)

| Принцип | Реализация |
|---------|------------|
| Создание объектов | `new Service(mockDeps)` — чистый C# |
| DI | `TestContainerBuilder.Build()` или ручной конструктор |
| События | `EventBusTestHelper.CreatePair<T>()` |
| Async | `async Task` (нативный C#) |
| Mock | Реализация интерфейсов, не наследование |
| Структура | По модулям: `Tests/Modules/Combat/`, `Tests/Core/` |

### 13.2. Изоляция тестов

```csharp
// ❌ ПЛОХО — глобальное состояние
private static int counter = 0;

// ✅ ХОРОШО — изолированное состояние
private int counter;

[SetUp]
public void Setup()
{
    counter = 0;
}
```

### 13.3. Очистка ресурсов

```csharp
[TearDown]
public void TearDown()
{
    // Отписка от событий
    if (_service != null)
    {
        _service.OnQiChanged -= OnQiChangedHandler;
    }

    // Освобождение ресурсов
    _service?.Dispose();
    _service = null;
}
```

### 13.4. Использование try-finally

```csharp
[Test]
public void Test_WithCleanup()
{
    var resource = AllocateResource();

    try
    {
        // Тест
        Assert.IsNotNull(resource);
    }
    finally
    {
        // Гарантированная очистка
        ReleaseResource(resource);
    }
}
```

---

## 14. CI/CD интеграция

### 14.1. Минимальный pipeline

```bash
# 1. Сборка
dotnet build

# 2. Тесты
dotnet test --logger "nunit;LogFilePath=test-results.xml"

# 3. Проверка движка (если есть движко-зависимые тесты)
godot --headless --path . --script res://tests/run_tests.gd

# 4. Анализ покрытия (опционально)
dotnet test --collect:"XPlat Code Coverage"
```

### 14.2. Критерии успеха CI

- ✅ `dotnet build` без ошибок и предупреждений.
- ✅ Все тесты прошли (Failed = 0).
- ✅ Покрытие ≥ 80% для core-модулей (опционально, но желательно).

---

## 15. Покрытие тестами

### 15.1. Метрики

| Метрика | Цель |
|---------|------|
| Line coverage | ≥ 80% для core |
| Branch coverage | ≥ 70% для core |
| Method coverage | 100% для публичных методов с логикой |

### 15.2. Что НЕ нужно покрывать тестами

- Автосвойства (get-only, set-only без логики).
- Trivial методы (одна строка return).
- Движко-специфичный код (UI, сцены) — покрывается скриншот-тестами, не unit-тестами.

---

## 16. Текущий прогресс

| Этап | Статус | Файлов | Тестов |
|------|--------|--------|--------|
| T1.1 Инфраструктура | ✅ | 2 | 1 |
| T1.2 TestUtilities (моки) | ✅ | 7 | 0 |
| T2.1 GameConstants + LevelSuppression | ✅ | 2 | 18 |
| T2.2 BodyPart + BodyDamageCalculator | ✅ | 2 | 22 |
| T2.3 QiBuffer + QiRegen + QiBreakthrough | ✅ | 3 | 18 |
| T2.4 DamageCalculator + DefenseProcessor | ✅ | 2 | 20 |
| T3.1 QiService + TechniqueCapacity | ✅ | 2 | 18 |
| T3.2 BuffCalculator + BuffService | ✅ | 2 | 13 |
| T3.3 TileMapService + DestructibleService | ✅ | 2 | 17 |
| T3.4 BodyService | ✅ | 1 | 6 |
| T4.1 P2 вторичные модули | 🔲 | 7 | ~35 (план) |
| T4.2 P3 UI/Save | 🔲 | 4 | ~15 (план) |
| T4.3 Integration + Balance | 🔲 | 5 | ~27 (план) |
| **ИТОГО** | | | **~116 / 210** |

---

## 17. Связанные документы

- `AI_DEVELOPMENT_WORKFLOW.md` — workflow тестирования в AI-цикле.
- `ALGORITHMS.md` — формулы для верификации баланса.
- `01_architecture/DI_AND_EVENTBUS.md` — DI, моки, шина событий.
- `01_architecture/MODULE_STRUCTURE.md` — структура модулей (зеркалится в тестах).
