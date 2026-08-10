# 🧪 Правила создания и запуска Unit тестов

**Версия:** 1.0
**Создано:** 2026-04-09 06:35:00 UTC
**Проект:** Cultivation World Simulator

---

## 📋 Общие принципы

### 1. Структура тестов

```
Assets/Scripts/Tests/
├── CombatTests.cs          — Тесты боевой системы
├── IntegrationTests.cs     — Интеграционные тесты
├── BalanceVerification.cs  — Верификация баланса
└── IntegrationTestScenarios.cs — Сценарии интеграции
```

### 2. Используемые фреймворки

- **NUnit** — Основной фреймворк для Unit тестов
- **Unity Test Framework** — Интеграция с Unity Editor

---

## 📝 Правила написания тестов

### AAA Pattern

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

### Именование тестов

```csharp
// Формат: [Метод]_[Сценарий]_[ОжидаемыйРезультат]
[Test]
public void LevelSuppression_SameLevel_ReturnsFullDamage() { }

[Test]
public void QiBuffer_InsufficientQi_PartialAbsorption() { }

[Test]
public void TechniqueCapacity_Ultimate_HasMultiplier() { }
```

### Setup и Teardown

```csharp
private GameObject testObject;

[SetUp]
public void Setup()
{
    // Создаём тестовый объект перед каждым тестом
    testObject = new GameObject("TestEntity");
    testObject.SetActive(false); // Отключаем до настройки
}

[TearDown]
public void TearDown()
{
    // Уничтожаем после каждого теста
    if (testObject != null)
    {
        UnityEngine.Object.DestroyImmediate(testObject);
    }
}
```

---

## 🎯 Типы тестов

### Unit тесты

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

### Интеграционные тесты

Тестируют взаимодействие между системами:

```csharp
[Test]
public void Test_QiController_TechniqueController_Integration()
{
    // Создаём обе системы
    var qiCtrl = testObject.AddComponent<QiController>();
    var techCtrl = testObject.AddComponent<TechniqueController>();
    
    // Настраиваем и проверяем взаимодействие
    qiCtrl.SetCultivationLevel(3, 5);
    qiCtrl.RestoreFull();
    
    // Проверяем, что техника может быть использована
    bool canUse = techCtrl.CanUseTechnique(learnedTech);
    Assert.IsTrue(canUse);
}
```

### Edge Cases

Обязательные граничные случаи:

| Тип | Пример |
|-----|--------|
| Нулевые значения | `QiBuffer_ZeroDamage_NoConsumption` |
| Отрицательные значения | Проверка защиты от invalid input |
| Overflow | `Test_EdgeCase_QiOverflow` |
| Минимальные значения | Level 1 vs Level 10 |
| Максимальные значения | Проверка капов |

---

## 🔧 Mock объекты

### Создание Mock

```csharp
public class MockCombatant : MonoBehaviour, ICombatant
{
    private int level;
    private QiController qiController;
    
    public void Setup(int level, QiController qi)
    {
        this.level = level;
        this.qiController = qi;
    }
    
    // Реализация ICombatant
    public int CultivationLevel => level;
    public long CurrentQi => qiController?.CurrentQi ?? 0;
    // ... остальные члены интерфейса
}
```

### Использование Mock

```csharp
[Test]
public void Test_WithMockCombatant()
{
    var attackerObj = new GameObject("Attacker");
    var attackerQi = attackerObj.AddComponent<QiController>();
    var attacker = attackerObj.AddComponent<MockCombatant>();
    
    attackerQi.SetCultivationLevel(3, 0);
    attacker.Setup(3, attackerQi);
    
    // Используем mock для тестирования
    var params = attacker.GetAttackerParams();
    
    DestroyImmediate(attackerObj);
}
```

---

## 📊 Верификация баланса

### BalanceVerification класс

Специальный класс для проверки формул и таблиц:

```csharp
// В Unity Editor:
// 1. Создай GameObject
// 2. Add Component → Balance Verification Component
// 3. Правый клик → Run Quick Verification

// Или через код:
BalanceVerification.QuickVerify();
BalanceVerification.VerifyCoreCapacityFormula();
BalanceVerification.VerifyLevelSuppressionTable();
```

### Проверяемые значения

| Категория | Метод |
|-----------|-------|
| Плотность Ци | `VerifyCoreCapacityFormula()` |
| Подавление уровнем | `VerifyLevelSuppressionTable()` |
| Qi Buffer | `VerifyQiBufferEfficiency()` |
| Время медитации | `VerifyMeditationTime()` |
| Все сразу | `QuickVerify()` |

---

## ⚠️ Важные правила

### 1. Изоляция тестов

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

### 2. Очистка ресурсов

```csharp
[TearDown]
public void TearDown()
{
    // Всегда уничтожай GameObject
    if (testObject != null)
    {
        UnityEngine.Object.DestroyImmediate(testObject);
    }
    
    // Отписывайся от событий
    if (qiController != null)
    {
        qiController.OnQiChanged -= OnQiChangedHandler;
    }
}
```

### 3. Использование try-finally

```csharp
[Test]
public void Test_WithCleanup()
{
    GameObject obj = new GameObject("Test");
    
    try
    {
        // Тест
        var component = obj.AddComponent<QiController>();
        Assert.IsNotNull(component);
    }
    finally
    {
        // Гарантированная очистка
        UnityEngine.Object.DestroyImmediate(obj);
    }
}
```

---

## 📈 Покрытие тестами

### Обязательные модули для тестирования

| Модуль | Приоритет |
|--------|-----------|
| Combat/DamageCalculator | Высокий |
| Combat/LevelSuppression | Высокий |
| Combat/QiBuffer | Высокий |
| Qi/QiController | Высокий |
| Combat/TechniqueCapacity | Средний |
| Formation/FormationQiPool | Средний |
| Save/SaveManager | Средний |

### Минимальные требования

- **Unit тесты:** Каждый публичный метод с логикой
- **Edge cases:** Минимум 3 граничных случая на модуль
- **Интеграция:** Каждая связь между системами

---

## 🔍 Отладка тестов

### Debug.Log в тестах

```csharp
[Test]
public void Test_WithDebug()
{
    var result = SomeCalculation();
    
    Debug.Log($"[TestName] Input: {input}");
    Debug.Log($"[TestName] Result: {result}");
    
    Assert.AreEqual(expected, result);
}
```

### Запуск отдельного теста

```csharp
// Помощник для запуска в редакторе
public static class TestRunnerHelper
{
    public static void RunAllCombatTests()
    {
        var tests = new CombatTests();
        // ... запуск через рефлексию
    }
}
```

---

## 📚 Связанные документы

- [ALGORITHMS.md](./ALGORITHMS.md) — Формулы для верификации
- [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md) — Логика боевой системы
- [docs_temp/RunningTests.md](../docs_temp/RunningTests.md) — Инструкция запуска

---

*Документ создан: 2026-04-09 06:35:00 UTC*
