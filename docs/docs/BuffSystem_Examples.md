# Примеры кода: Система баффов

**Источник:** [BUFF_MODIFIERS_SYSTEM.md](../docs/BUFF_MODIFIERS_SYSTEM.md)
**Создано:** 2026-04-04

---

## 1. ConductivityModifier

Модификатор проводимости с системой отката.

```csharp
/// <summary>
/// Модификатор проводимости с системой отката.
/// </summary>
public class ConductivityModifier
{
    // Бонус
    public float bonusValue;           // Величина бонуса (например, +0.2 = +20%)
    public float bonusRemainingTime;   // Оставшееся время бонуса
    
    // Откат (наступает после завершения бонуса)
    public float penaltyValue;         // Величина штрафа (bonusValue × 0.5)
    public float penaltyRemainingTime; // Оставшееся время штрафа
    
    // Состояние
    public bool isBonusActive;
    public bool isPenaltyActive;
    
    // Расчёт
    public float GetMultiplier()
    {
        if (isBonusActive) return 1f + bonusValue;
        if (isPenaltyActive) return 1f - penaltyValue;
        return 1f;
    }
}
```

---

## 2. BuffType (Enum)

Типы баффов и дебаффов.

```csharp
/// <summary>
/// Тип баффа/дебаффа.
/// ВАЖНО: Первичные характеристики (STR, AGI, INT, VIT) отсутствуют!
/// </summary>
public enum BuffType
{
    // === ВТОРИЧНЫЕ ХАРАКТЕРИСТИКИ ===
    AttackBoost,        // Увеличение урона
    DefenseBoost,       // Увеличение защиты
    SpeedBoost,         // Увеличение скорости
    CriticalChance,     // Шанс критического удара
    CriticalDamage,     // Урон критического удара
    Evasion,            // Уклонение
    
    // === РЕГЕНЕРАЦИЯ (текущие значения, НЕ базовые!) ===
    HealthRegen,        // Регенерация здоровья (HP)
    QiRestoration,      // Восстановление Ци (НЕ QiRegen!)
    StaminaRegen,       // Регенерация выносливости
    
    // === ПРОВОДИМОСТЬ (особые правила!) ===
    ConductivityBoost,  // Временное усиление с откатом
    ConductivityPenalty,// Штраф (дебафф или откат)
    
    // === ЗАЩИТА ===
    Shield,             // Щит (поглощение урона)
    DamageReduction,    // Снижение урона
    
    // === ИММУНИТЕТ ===
    Immunity_Poison,    // Иммунитет к яду
    Immunity_Stun,      // Иммунитет к оглушению
    Immunity_Slow,      // Иммунитет к замедлению
    
    // === УСКОРЕНИЕ ===
    AttackSpeed,        // Скорость атаки
    CastSpeed,          // Скорость каста
    
    // === ДЕБАФФЫ: Снижение характеристик ===
    AttackReduction,    // Снижение урона
    DefenseReduction,   // Снижение защиты
    SpeedReduction,     // Снижение скорости
    
    // === ДЕБАФФЫ: DoT ===
    Poison,             // Отравление
    Burn,               // Горение
    Bleed,              // Кровотечение
    Freeze,             // Заморозка
    
    // === ДЕБАФФЫ: Контроль ===
    Stun,               // Оглушение
    Slow,               // Замедление
    Blind,              // Ослепление
    Silence,            // Безмолвие (блок техник)
    
    // === ДЕБАФФЫ: Специальные ===
    Curse,              // Проклятие
    Vulnerability       // Уязвимость к элементу
}
```

---

## 3. BuffApplication и BuffStacking (Enums)

Способ применения и поведение при повторном применении.

```csharp
/// <summary>
/// Способ применения баффа.
/// </summary>
public enum BuffApplication
{
    Instant,            // Мгновенный эффект (лечение, урон)
    Duration,           // Длительный эффект
    Permanent,          // Постоянный (пока не снят)
    Stacking,           // Накапливающийся
    Refreshing          // Обновляет длительность
}

/// <summary>
/// Поведение при повторном применении.
/// </summary>
public enum BuffStacking
{
    Replace,            // Заменить
    Refresh,            // Обновить таймер
    Stack,              // Добавить стек
    Ignore              // Игнорировать
}
```

---

## 4. BuffData (ScriptableObject)

Данные баффа/дебаффа.

```csharp
/// <summary>
/// Данные баффа/дебаффа.
/// </summary>
[CreateAssetMenu(fileName = "BuffData", menuName = "Cultivation/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    
    [Header("Type")]
    public BuffType type;
    public bool isDebuff;
    public Element element;         // Связанный элемент
    
    [Header("Effect")]
    public float value;             // Значение эффекта
    public bool isPercentage;       // true = %, false = абсолютное
    
    [Header("Duration")]
    public BuffApplication application;
    public float duration;          // Длительность в секундах
    public int maxStacks = 1;       // Максимум стеков
    public BuffStacking stackingBehavior;
    
    // === ОСОБОЕ ДЛЯ ПРОВОДИМОСТИ ===
    [Header("Conductivity Special (только для ConductivityBoost!)")]
    public bool hasConductivityPenalty = true;  // Всегда true для проводимости
    public float penaltyMultiplier = 3f;        // Длительность отката ×3
    public float penaltyValueMultiplier = 0.5f; // Величина отката ×0.5
    
    [Header("Tick Effect (для DoT/HoT)")]
    public bool hasTickEffect;
    public float tickInterval = 1f;
    public int tickDamage;
    public int tickHealing;
    
    [Header("Visual")]
    public GameObject effectPrefab;
    public Color buffColor = Color.green;
    public Color debuffColor = Color.red;
    
    [Header("Sound")]
    public AudioClip applySound;
    public AudioClip removeSound;
}
```

---

## 5. ActiveBuff

Активный бафф на цели.

```csharp
/// <summary>
/// Активный бафф на цели.
/// </summary>
public class ActiveBuff
{
    public BuffData data;
    public float remainingDuration;
    public int currentStacks;
    public float tickTimer;
    public GameObject source;
    public bool isActive;
    
    // Для проводимости
    public ConductivityModifier conductivityModifier;
    
    public float Progress => data.duration > 0 ? 1f - (remainingDuration / data.duration) : 0f;
    public float TotalValue => data.value * currentStacks;
}
```

---

## 6. Примеры JSON конфигураций баффов

### Правильные примеры

```json
{
  "buffs": [
    {
      "id": "attack_boost_20",
      "type": "AttackBoost",
      "displayName": "Ярость",
      "description": "+20% к урону атак на 30 секунд",
      "value": 0.20,
      "isPercentage": true,
      "duration": 30,
      "color": "#FFD700"
    },
    {
      "id": "conductivity_boost_temp",
      "type": "ConductivityBoost",
      "displayName": "Раскрытие меридиан",
      "description": "+25% проводимости на 20 сек, затем -12.5% на 60 сек",
      "value": 0.25,
      "isPercentage": true,
      "duration": 20,
      "hasConductivityPenalty": true,
      "penaltyMultiplier": 3.0,
      "penaltyValueMultiplier": 0.5,
      "color": "#00BFFF"
    },
    {
      "id": "qi_restoration",
      "type": "QiRestoration",
      "displayName": "Поток Ци",
      "description": "Восстанавливает 50 Ци каждые 5 секунд",
      "hasTickEffect": true,
      "tickInterval": 5,
      "tickHealing": 50,
      "duration": 60,
      "color": "#00FF88"
    },
    {
      "id": "shield_100",
      "type": "Shield",
      "displayName": "Ци-щит",
      "description": "Поглощает 100 урона",
      "value": 100,
      "isPercentage": false,
      "duration": 60,
      "color": "#FFFFFF"
    }
  ]
}
```

### ЗАПРЕЩЁННЫЕ примеры (НЕ использовать!)

```json
{
  "forbidden_examples": [
    {
      "id": "WRONG_strength_boost",
      "type": "StrengthBoost",
      "description": "НЕЛЬЗЯ! Первичная характеристика!",
      "reason": "STR развивается только физически!"
    },
    {
      "id": "WRONG_max_qi_boost",
      "type": "MaxQiBoost",
      "description": "НЕЛЬЗЯ! Ёмкость ядра!",
      "reason": "MaxQi определяется уровнем культивации!"
    },
    {
      "id": "WRONG_qi_regen_boost",
      "type": "QiRegenBoost",
      "description": "НЕЛЬЗЯ! Базовая регенерация!",
      "reason": "QiRegen = 10%/сутки, только от уровня!"
    }
  ]
}
```

---

## 7. Интеграция с техниками

```csharp
// В TechniqueController (теоретический пример)
private void ApplyTechniqueEffects(TechniqueData technique, GameObject target)
{
    var buffManager = target.GetComponent<BuffManager>();
    
    foreach (var buffId in technique.buffsToApply)
    {
        var buffData = BuffDatabase.GetBuff(buffId);
        
        // Проверка на запрещённые типы (должна быть в редакторе!)
        if (IsForbiddenBuffType(buffData.type))
        {
            Debug.LogError($"[BuffSystem] Запрещённый тип баффа: {buffData.type}");
            continue;
        }
        
        buffManager?.ApplyBuff(buffData, gameObject);
    }
}

private bool IsForbiddenBuffType(BuffType type)
{
    // Первичные характеристики
    // MaxQi, QiRegen, Density — не существуют в BuffType
    return false;
}
```

---

## 8. Интеграция с предметами

```csharp
// В ConsumableItem (теоретический пример)
public override void Use(GameObject user)
{
    var buffManager = user.GetComponent<BuffManager>();
    
    foreach (var buff in buffs)
    {
        buffManager?.ApplyBuff(buff);
    }
}
```

---

*Файл создан автоматически из BUFF_SYSTEM.md*
*Код предназначен для использования в Unity проекте*
