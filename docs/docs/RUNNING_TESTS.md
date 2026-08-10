# 🧪 Инструкция по запуску тестов и проверке результатов

**Создано:** 2026-05-19 14:03:03 UTC
**Проект:** Cultivation World Simulator
**Тестовый фреймворк:** Unity Test Framework 1.4.0 + NUnit 3.5
**Архитектура:** Чистый C# + VContainer + MessagePipe + UniTask

---

## 📋 Сводка текущего состояния тестов

### Структура директории

```
UnityProject/Assets/Scripts/Tests/
├── Tests.asmdef                              — Assembly Definition (Editor-only)
├── Core/
│   ├── ValidationTests.cs                    — 1 тест (сборка загружается)
│   └── GameConstantsTests.cs                 — Тесты констант
├── Modules/
│   ├── Body/
│   │   ├── BodyPartTests.cs                  — 16 тестов
│   │   ├── BodyDamageCalculatorTests.cs      — 6 тестов
│   │   └── BodyServiceTests.cs               — 6 тестов
│   ├── Buff/
│   │   ├── BuffCalculatorTests.cs            — 5 тестов
│   │   └── BuffServiceTests.cs               — 8 тестов
│   ├── Combat/
│   │   ├── DamageCalculatorTests.cs          — 12 тестов
│   │   ├── DefenseProcessorTests.cs          — 5 тестов
│   │   ├── LevelSuppressionTests.cs          — 9 тестов
│   │   └── TechniqueCapacityTests.cs         — 5 тестов
│   ├── Qi/
│   │   ├── QiBufferServiceTests.cs           — 8 тестов
│   │   ├── QiRegenCalculatorTests.cs         — 5 тестов
│   │   ├── QiBreakthroughCalculatorTests.cs  — 5 тестов
│   │   └── QiServiceTests.cs                 — 13 тестов
│   ├── Tile/
│   │   ├── TileMapServiceTests.cs            — 10 тестов
│   │   └── DestructibleServiceTests.cs       — 7 тестов
│   ├── Charger/                              — (пусто, T4.1)
│   ├── Formation/                            — (пусто, T4.1)
│   ├── Inventory/                            — (пусто, T4.1)
│   ├── NPC/                                  — (пусто, T4.2)
│   ├── Player/                               — (пусто, T4.2)
│   ├── Quest/                                — (пусто, T4.1)
│   ├── Save/                                 — (пусто, T4.2)
│   ├── Interaction/                          — (пусто, T4.2)
│   └── World/                                — (пусто, T4.1)
├── Integration/                              — (пусто, T4.3)
├── Balance/                                  — (пусто, T4.3)
└── TestUtilities/
    ├── TestContainerBuilder.cs               — VContainer helper
    ├── MessagePipeTestHelper.cs              — MessagePipe helper
    ├── MockQiService.cs                      — Мок IQiService
    ├── MockBodyService.cs                     — Мок IBodyService
    ├── MockBuffService.cs                     — Мок IBuffService
    ├── MockTimeService.cs                     — Мок ITimeService
    └── MockInventoryService.cs                — Мок IInventoryService
```

---

## 🚀 Способ 1: Unity Test Runner (РЕКОМЕНДУЕТСЯ)

### Открытие Test Runner

1. Открой Unity Editor (проект `UnityProject/`)
2. Меню: **Window → General → Test Runner**
3. Или: **Window → Analysis → Test Runner** (в зависимости от версии Unity)

### Выбор типа тестов

Все тесты проекта — **EditMode** (не требуют Play Mode):
- Во вкладке **EditMode** — все unit-тесты
- Тесты с `[UnityTest]` и UniTask — тоже EditMode, но с async

### Запуск тестов

| Действие | Как |
|----------|-----|
| Запустить ВСЕ тесты | Нажми **Run All** |
| Запустить одну категорию | Выбери папку (например `Modules/Body`) → **Run Selected** |
| Запустить один тест | Выбери конкретный тест → **Run Selected** |
| Запустить с отладкой | Нажми **Debug** (вместо Run) — остановится на breakpoints |

### Результаты в Test Runner

| Индикатор | Значение |
|-----------|----------|
| 🟢 Зелёная галочка | Тест пройден (PASS) |
| 🔴 Красный крестик | Тест провален (FAIL) |
| ⚪ Серый | Тест пропущен (SKIP/Inconclusive) |
| 🟡 Жёлтый | Тест в процессе выполнения |

---

## 🖥️ Способ 2: Unity Command Line (Batch Mode)

Для CI/CD или запуска без GUI:

### Базовая команда

```bash
# macOS / Linux
/path/to/Unity -batchmode \
  -projectPath /path/to/UnityProject \
  -runTests \
  -testPlatform EditMode \
  -testResults /path/to/results.xml \
  -logfile /path/to/test-log.txt

# Windows
"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode ^
  -projectPath "C:\path\to\UnityProject" ^
  -runTests ^
  -testPlatform EditMode ^
  -testResults "C:\path\to\results.xml" ^
  -logfile "C:\path\to\test-log.txt"
```

### Запуск конкретной категории

```bash
# Только тесты Body
/path/to/Unity -batchmode \
  -projectPath /path/to/UnityProject \
  -runTests \
  -testPlatform EditMode \
  -testFilter "CultivationGame.Tests.Modules.Body" \
  -testResults results.xml

# Только один тестовый класс
/path/to/Unity -batchmode \
  -projectPath /path/to/UnityProject \
  -runTests \
  -testPlatform EditMode \
  -testFilter "CultivationGame.Tests.Modules.Body.BodyPartTests" \
  -testResults results.xml
```

### Ключевые параметры

| Параметр | Описание |
|----------|----------|
| `-batchmode` | Запуск без GUI |
| `-projectPath` | Путь к проекту Unity |
| `-runTests` | Режим запуска тестов |
| `-testPlatform EditMode` | Тип тестов (EditMode для unit-тестов) |
| `-testResults` | Путь к XML-файлу с результатами (NUnit format) |
| `-testFilter` | Фильтр по имени/неймспейсу |
| `-logfile` | Путь к лог-файлу |

### Формат результатов XML

Результаты сохраняются в формате **NUnit 3 Test Results XML**:

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

---

## 📊 Способ 3: Чтение результатов

### Анализ XML результатов

```bash
# Подсчёт пройденных/проваленных тестов
grep -c 'result="Passed"' results.xml
grep -c 'result="Failed"' results.xml

# Найти проваленные тесты
grep 'result="Failed"' results.xml -B2

# Детали ошибок (message внутри <failure>)
grep -A5 '<failure>' results.xml
```

### Показатели успеха

| Метрика | Критерий |
|---------|----------|
| **Total** | Общее количество тестов |
| **Passed** | Должно быть = Total |
| **Failed** | Должно быть = 0 |
| **Inconclusive** | Должно быть = 0 |
| **Duration** | Общее время (обычно < 5 сек для unit-тестов) |

---

## 🔍 Отладка проваленных тестов

### 1. Прочитать сообщение ошибки

В Test Runner → клик на проваленный тест → увидишь:

```
Expected: 100
But was:  50
at CultivationGame.Tests.Modules.Body.BodyPartTests.TakeDamage_ReducesRedHP_70Percent()
```

### 2. Запустить с Debug

1. В Test Runner нажми **Debug** вместо **Run**
2. Поставь breakpoint в тесте (или в проверяемом коде)
3. Unity остановится на breakpoint
4. Используй стандартные средства отладки (Watch, Locals, Call Stack)

### 3. Добавить Diagnostic Output

```csharp
[Test]
public void TakeDamage_ReducesRedHP_70Percent()
{
    // Arrange
    var part = new BodyPart(BodyPartType.Torso, 100);
    UnityEngine.Debug.Log($"[TEST] Before: RedHP={part.CurrentRedHP}");

    // Act
    bool result = part.TakeDamage(7, 3);
    UnityEngine.Debug.Log($"[TEST] After: RedHP={part.CurrentRedHP}, result={result}");

    // Assert
    Assert.AreEqual(93, part.CurrentRedHP);
}
```

### 4. Типичные причины провалов

| Причина | Симптом | Решение |
|---------|---------|---------|
| Формула изменена | Expected ≠ Actual | Обновить тест под новую формулу |
| P1-03 bug | `(int)(1 * 0.7) = 0` | Использовать `Math.Max(1, ...)` |
| Мок не настроен | NullReferenceException | Проверить мок в SetUp |
| asmdef не видит тип | CS0246 Compilation Error | Проверить `references` в Tests.asmdef |
| Namespace mismatch | CS0246 | Проверить `using` в тесте |

---

## ⚙️ Конфигурация Tests.asmdef

Текущая конфигурация (`Tests.asmdef`):

```json
{
    "name": "CultivationGame.Tests",
    "rootNamespace": "CultivationGame.Tests",
    "references": [
        "CultivationGame.New",       // Основная сборка игры
        "UnityEngine.TestRunner",     // Unity Test Runner
        "VContainer",                 // DI-контейнер
        "MessagePipe"                 // Событийная система
    ],
    "includePlatforms": ["Editor"],    // Только в редакторе
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"         // NUnit 3.5
    ]
}
```

### Если тесты не компилируются

1. **CS0246** — тип не найден → добавить сборку в `references`
2. **CS0234** — namespace не найден → проверить `using`
3. **CS0103** — имя не существует → проверить, что класс `public`

---

## 📈 Текущий прогресс по плану тестирования

По плану `UnityProject/checkpoints/05_19_plan_testing.md`:

| Этап | Статус | Файлы | Тестов |
|------|--------|-------|--------|
| T1.1 Инфраструктура + asmdef | ✅ Готов | Tests.asmdef, ValidationTests.cs | 1 |
| T1.2 TestUtilities (моки) | ✅ Готов | 7 файлов моков + хелперов | 0 |
| T2.1 GameConstants + LevelSuppression | ✅ Готов | 2 файла | 18 |
| T2.2 BodyPart + BodyDamageCalculator | ✅ Готов | 2 файла | 22 |
| T2.3 QiBuffer + QiRegen + QiBreakthrough | ✅ Готов | 3 файла | 18 |
| T2.4 DamageCalculator + DefenseProcessor | ✅ Готов | 2 файла | 20 |
| T3.1 QiService + TechniqueCapacity | ✅ Готов | 2 файла | 18 |
| T3.2 BuffCalculator + BuffService | ✅ Готов | 2 файла | 13 |
| T3.3 TileMapService + DestructibleService | ✅ Готов | 2 файла | 17 |
| T3.4 BodyService | ✅ Готов | 1 файл | 6 |
| **T4.1 P2 вторичные** | 🔲 Не начат | 7 файлов | 35 |
| **T4.2 P3 UI/Save** | 🔲 Не начат | 4 файла | 15 |
| **T4.3 Integration + Balance** | 🔲 Не начат | 5 файлов | 27 |
| **ИТОГО** | | | **~116 / 210** |

---

## 🏗️ Архитектурные принципы тестов

### Новый подход (vs Legacy)

| Принцип | Реализация |
|---------|------------|
| Создание объектов | `new Service(mockDeps)` — чистый C# |
| DI | `TestContainerBuilder.Build()` или ручной конструктор |
| События | `MessagePipeTestHelper.CreatePair<T>()` |
| Async | `[UnityTest]` + `UniTask.ToCoroutine()` |
| Mock | `MockQiService : IQiService` — интерфейсы, НЕ MonoBehaviour |
| Структура | По модулям: `Tests/Modules/Combat/`, `Tests/Core/` |

### Пример теста с моком

```csharp
[TestFixture]
public class QiServiceTests
{
    private MockQiService _mockQi;

    [SetUp]
    public void Setup()
    {
        _mockQi = new MockQiService();
        _mockQi.SetCurrentQi(100);
        _mockQi.SetMaxQi(100);
    }

    [Test]
    public void TryConsume_SufficientQi_ReturnsTrue()
    {
        // Arrange
        _mockQi.SetCurrentQi(50);

        // Act
        bool result = _mockQi.TryConsume(30);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(20, _mockQi.CurrentQi);
    }
}
```

---

## ⚠️ Ограничения данной среды

В текущей облачной среде **нет доступа к Unity Editor**. Это означает:

- ❌ Нельзя запустить Unity Test Runner
- ❌ Нельзя запустить Unity batch mode
- ✅ Можно писать и проверять код тестов (синтаксис, компилируемость через чтение)
- ✅ Можно анализировать структуру тестов и план

### Рекомендуемый workflow

1. **В данной среде:** писать тесты → проверять код ревью → коммитить
2. **На локальной машине с Unity:** открыть проект → запустить Test Runner → проверить результаты
3. **При провале:** исправить код → закоммитить → повторить на локальной машине

---

## 📝 Шпаргалка

| Действие | Путь/команда |
|----------|-------------|
| Открыть Test Runner | `Window → General → Test Runner` |
| Запустить все тесты | **Run All** во вкладке EditMode |
| Запустить один тест | Выбрать → **Run Selected** |
| Отладить тест | **Debug** (вместо Run) |
| CLI: все тесты | `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml` |
| CLI: фильтр по классу | `-testFilter "CultivationGame.Tests.Modules.Body.BodyPartTests"` |
| Посмотреть ошибки | Клик на проваленный тест в Test Runner |
| Проверить компиляцию | Если тесты не видны в Test Runner → проверить Tests.asmdef |

---

*Документ создан: 2026-05-19 14:03:03 UTC*
*Основание: UnityProject/checkpoints/05_19_plan_testing.md*
