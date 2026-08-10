# Примеры кода: Система порогов развития

**Источник:** [STAT_THRESHOLD_SYSTEM.md](../docs/STAT_THRESHOLD_SYSTEM.md)
**Создано:** 2026-04-04

---

## 1. StatDevelopment (Класс)

Основной класс для управления развитием характеристик.

```csharp
public class StatDevelopment
{
    // Реальные характеристики
    public float strength;
    public float agility;
    public float intelligence;
    public float vitality;
    
    // Виртуальные дельты
    public float virtualStrengthDelta;
    public float virtualAgilityDelta;
    public float virtualIntelligenceDelta;
    public float virtualVitalityDelta;
    
    // Капы виртуальной дельты
    private const float MAX_VIRTUAL_DELTA = 10.0f;
    private const float MAX_INTELLIGENCE_DELTA = 15.0f;
    
    // Методы
    public void AddDelta(StatType type, float amount);
    public float GetThreshold(StatType type);
    public float ConsolidateSleep(float hours);
    public bool CanAdvance(StatType type);
}
```

---

## 2. Расчёт порога

```csharp
public float GetThreshold(StatType type)
{
    float currentStat = GetStat(type);
    return Mathf.Floor(currentStat / 10f);
}

public bool CanAdvance(StatType type)
{
    float delta = GetDelta(type);
    float threshold = GetThreshold(type);
    return delta >= threshold;
}
```

---

## 3. Добавление виртуальной дельты

```csharp
public void AddDelta(StatType type, float amount)
{
    float currentDelta = GetDelta(type);
    float maxDelta = GetMaxDelta(type);
    
    // Проверка капа
    float newDelta = Mathf.Min(currentDelta + amount, maxDelta);
    SetDelta(type, newDelta);
}

private float GetMaxDelta(StatType type)
{
    return type == StatType.Intelligence ? MAX_INTELLIGENCE_DELTA : MAX_VIRTUAL_DELTA;
}
```

---

## 4. Закрепление при сне

```csharp
public float ConsolidateSleep(float hours)
{
    // Минимум 4 часа для закрепления
    if (hours < 4f) return 0f;
    
    // Максимум закрепления за сон
    float maxConsolidation = 0.20f;  // При 8 часах
    float consolidationRate = Mathf.Min(hours * 0.025f, maxConsolidation);
    
    float totalConsolidated = 0f;
    
    foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
    {
        float delta = GetDelta(type);
        float threshold = GetThreshold(type);
        
        if (delta > 0)
        {
            // Закрепляем сколько можем
            float toConsolidate = Mathf.Min(delta, consolidationRate);
            float actualConsolidated = Mathf.Min(toConsolidate, threshold);
            
            // Проверка порога
            if (CanAdvance(type))
            {
                // Повышаем характеристику
                float currentStat = GetStat(type);
                SetStat(type, currentStat + 1f);
                SetDelta(type, delta - threshold);
                totalConsolidated += threshold;
            }
            else
            {
                // Частичное закрепление в дельте
                SetDelta(type, delta - actualConsolidated);
                totalConsolidated += actualConsolidated;
            }
        }
    }
    
    return totalConsolidated;
}
```

---

## 5. Enum StatType

```csharp
public enum StatType
{
    Strength,
    Agility,
    Intelligence,
    Vitality
}
```

---

## 6. Полная реализация StatDevelopment

```csharp
using UnityEngine;
using System;

[System.Serializable]
public class StatDevelopment
{
    // Реальные характеристики
    [SerializeField] private float strength = 10f;
    [SerializeField] private float agility = 10f;
    [SerializeField] private float intelligence = 10f;
    [SerializeField] private float vitality = 10f;
    
    // Виртуальные дельты
    [SerializeField] private float virtualStrengthDelta;
    [SerializeField] private float virtualAgilityDelta;
    [SerializeField] private float virtualIntelligenceDelta;
    [SerializeField] private float virtualVitalityDelta;
    
    // Капы
    private const float MAX_VIRTUAL_DELTA = 10.0f;
    private const float MAX_INTELLIGENCE_DELTA = 15.0f;
    private const float MIN_SLEEP_HOURS = 4f;
    private const float MAX_CONSOLIDATION_PER_SLEEP = 0.20f;
    
    // События
    public event Action<StatType, float> OnStatChanged;
    public event Action<StatType, float> OnDeltaChanged;
    public event Action<StatType> OnStatAdvanced;
    
    // Получение текущего значения
    public float GetStat(StatType type) => type switch
    {
        StatType.Strength => strength,
        StatType.Agility => agility,
        StatType.Intelligence => intelligence,
        StatType.Vitality => vitality,
        _ => 0f
    };
    
    // Установка значения
    private void SetStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.Strength:
                strength = value;
                break;
            case StatType.Agility:
                agility = value;
                break;
            case StatType.Intelligence:
                intelligence = value;
                break;
            case StatType.Vitality:
                vitality = value;
                break;
        }
        OnStatChanged?.Invoke(type, value);
    }
    
    // Получение дельты
    public float GetDelta(StatType type) => type switch
    {
        StatType.Strength => virtualStrengthDelta,
        StatType.Agility => virtualAgilityDelta,
        StatType.Intelligence => virtualIntelligenceDelta,
        StatType.Vitality => virtualVitalityDelta,
        _ => 0f
    };
    
    // Установка дельты
    private void SetDelta(StatType type, float value)
    {
        switch (type)
        {
            case StatType.Strength:
                virtualStrengthDelta = value;
                break;
            case StatType.Agility:
                virtualAgilityDelta = value;
                break;
            case StatType.Intelligence:
                virtualIntelligenceDelta = value;
                break;
            case StatType.Vitality:
                virtualVitalityDelta = value;
                break;
        }
        OnDeltaChanged?.Invoke(type, value);
    }
    
    // Максимальная дельта для типа
    private float GetMaxDelta(StatType type)
    {
        return type == StatType.Intelligence ? MAX_INTELLIGENCE_DELTA : MAX_VIRTUAL_DELTA;
    }
    
    // Расчёт порога
    public float GetThreshold(StatType type)
    {
        float currentStat = GetStat(type);
        return Mathf.Floor(currentStat / 10f);
    }
    
    // Проверка возможности повышения
    public bool CanAdvance(StatType type)
    {
        float delta = GetDelta(type);
        float threshold = GetThreshold(type);
        return delta >= threshold;
    }
    
    // Добавление прогресса
    public void AddDelta(StatType type, float amount)
    {
        float currentDelta = GetDelta(type);
        float maxDelta = GetMaxDelta(type);
        float newDelta = Mathf.Min(currentDelta + amount, maxDelta);
        SetDelta(type, newDelta);
    }
    
    // Закрепление при сне
    public float ConsolidateSleep(float hours)
    {
        if (hours < MIN_SLEEP_HOURS) return 0f;
        
        float consolidationRate = Mathf.Min(hours * 0.025f, MAX_CONSOLIDATION_PER_SLEEP);
        float totalConsolidated = 0f;
        
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            float delta = GetDelta(type);
            float threshold = GetThreshold(type);
            
            if (delta <= 0) continue;
            
            // Пытаемся повысить характеристику
            while (delta >= threshold && threshold > 0)
            {
                float currentStat = GetStat(type);
                SetStat(type, currentStat + 1f);
                delta -= threshold;
                totalConsolidated += threshold;
                OnStatAdvanced?.Invoke(type);
                threshold = GetThreshold(type);  // Пересчёт порога
            }
            
            // Оставшаяся дельта
            SetDelta(type, delta);
        }
        
        return totalConsolidated;
    }
    
    // Сброс виртуальной дельты
    public void ResetDelta(StatType type)
    {
        SetDelta(type, 0f);
    }
    
    // Сброс всех дельт
    public void ResetAllDeltas()
    {
        virtualStrengthDelta = 0f;
        virtualAgilityDelta = 0f;
        virtualIntelligenceDelta = 0f;
        virtualVitalityDelta = 0f;
    }
}
```

---

## 7. Пример использования

```csharp
public class PlayerController : MonoBehaviour
{
    [SerializeField] private StatDevelopment stats;
    
    private void OnAttackHit()
    {
        stats.AddDelta(StatType.Strength, 0.001f);
    }
    
    private void OnDodge()
    {
        stats.AddDelta(StatType.Agility, 0.001f);
    }
    
    private void OnTechniqueUse()
    {
        stats.AddDelta(StatType.Intelligence, 0.0005f);
    }
    
    private void OnDamageTaken()
    {
        stats.AddDelta(StatType.Vitality, 0.001f);
    }
    
    private void OnSleep(float hours)
    {
        float consolidated = stats.ConsolidateSleep(hours);
        Debug.Log($"Закреплено прогресса: {consolidated:F3}");
    }
}
```

---

## 8. Модификаторы развития

```csharp
public static class StatDevelopmentModifiers
{
    // Множители обучения
    public static float GetLearningMultiplier(int age)
    {
        if (age < 20) return 1.2f;   // Молодой
        if (age > 50) return 0.8f;   // Пожилой
        return 1.0f;                  // Взрослый
    }
    
    // Множители от техник
    public static float GetTechniqueMultiplier(string techniqueId)
    {
        return techniqueId switch
        {
            "body_refining_art" => 1.5f,
            "qi_gathering_meditation" => 1.3f,
            "iron_body_technique" => 1.4f,
            _ => 1.0f
        };
    }
    
    // Множители от артефактов
    public static float GetArtifactMultiplier(string artifactId)
    {
        return artifactId switch
        {
            "pendant_of_wisdom" => 1.3f,
            "ring_of_learning" => 1.2f,
            _ => 1.0f
        };
    }
}
```

---

*Файл создан автоматически из STAT_THRESHOLD_SYSTEM.md*
*Код предназначен для использования в Unity проекте*
