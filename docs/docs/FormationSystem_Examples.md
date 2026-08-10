# Примеры кода: Система формаций

**Источник:** [FORMATION_SYSTEM.md](../docs/FORMATION_SYSTEM.md)
**Создано:** 2026-04-04
**Редактировано:** 2026-05-23 06:30:00 UTC — оставлена теория паттернов формаций

> **СТАТУС:** 🟢 Теория актуальна. Основные паттерны для формаций (CoreImbuement, CONTOUR_QI_BY_LEVEL,
> FormationSize, FormationStage) используются в текущей реализации FormationService.
> Код примеров — legacy (без VContainer). При реализации использовать:
> - `CultivationGame.Modules.Formation.FormationService` — текущий сервис
> - `CultivationGame.Modules.Formation.FormationCalculator` — калькулятор
> - `CultivationGame.Modules.Formation.FormationQiPool` — пул Ци
> - MessagePipe контракты: `FormationContracts.cs`

---

## 1. Требования к созданию ядра (YAML)

```yaml
requirements:
  # Профессия
  skill: string              # ID профессии (masonry, jewelry, smithing, etc.)
  minLevel: number           # Мин. уровень профессии
  
  # Материалы
  materials: Array<{
    id: string
    quantity: number
  }>
  
# Время изготовления
craftTime: number            # минут
```

### Примеры профессий для создания ядер

| Профессия | ID | Создаваемые ядра |
|-----------|-----|------------------|
| Камнетёс | `masonry` | Каменные диски |
| Ювелир | `jewelry` | Нефритовые диски |
| Кузнец | `smithing` | Железные диски |
| Духовный кузнец | `spirit_smithing` | Духовно-железные диски |
| Инженер формаций | `formation_engineering` | Алтари |

---

## 2. Интерфейс CoreImbuement (C#)

Внедрение формации в ядро.

```csharp
interface CoreImbuement
{
  // Условия
  conditions: {
    knowsFormation: boolean;    // Практик знает формацию
    isCompatible: boolean;      // level формации ∈ core.levels
    isEmpty: boolean;           // Ядро не занято другой формацией
  };
  
  // Процесс
  process: {
    qiCost: number;             // contourQi формации
    time: number;               // contourQi / (conductivity × qiDensity)
  };
  
  // Результат
  result: {
    coreFormation: FormationInCore;
    isPermanent: true;          // Очистить ядро НЕЛЬЗЯ
  };
}
```

---

## 3. Таблица CONTOUR_QI_BY_LEVEL (TypeScript)

```typescript
const CONTOUR_QI_BY_LEVEL: Record<number, number> = {
  1: 80,      // 80 × 2^0
  2: 160,
  3: 320,
  4: 640,
  5: 1280,
  6: 2560,
  7: 5120,
  8: 10240,
  9: 20480,
};
```

---

## 4. Функция расчёта ёмкости формации (TypeScript)

```typescript
function calculateCapacity(
  level: number,
  size: FormationSize,
  isHeavy: boolean = false
): number {
  const contourQi = CONTOUR_QI_BY_LEVEL[level];
  
  const MULTIPLIERS = {
    small: 10,
    medium: 50,
    large: 200,
    great: 1000,
  };
  
  const HEAVY_MULTIPLIER = 10000;
  
  if (isHeavy && level >= 6) {
    return contourQi * HEAVY_MULTIPLIER;
  }
  
  return contourQi * MULTIPLIERS[size];
}
```

---

## 5. Enums для формаций (C#)

```csharp
public enum FormationCoreType
{
    Disk,           // Диск (портативный)
    Altar,          // Алтарь (стационарный)
    Array,          // Массив
    Totem,          // Тотем
    Seal            // Печать
}

public enum FormationCoreVariant
{
    Stone,          // Камень
    Jade,           // Нефрит
    Iron,           // Железо
    SpiritIron,     // Духовное железо
    Crystal,        // Кристалл
    StarMetal,      // Звёздный металл
    VoidMatter      // Пустотная материя
}

public enum FormationType
{
    Barrier,        // Барьер
    Trap,           // Ловушка
    Amplification,  // Усиление
    Suppression,    // Подавление
    Gathering,      // Сбор
    Detection,      // Обнаружение
    Teleportation,  // Телепортация
    Summoning       // Призыв
}

public enum FormationSize
{
    Small,          // 3x3 м
    Medium,         // 10x10 м
    Large,          // 30x30 м
    Great,          // 100x100 м
    Heavy           // 300x300 м
}
```

---

## 6. FormationData (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "FormationData", menuName = "Cultivation/Formation Data")]
public class FormationData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    
    [Header("Classification")]
    public FormationType formationType;
    public FormationSize defaultSize;
    public int level;                    // Уровень формации (1-9)
    
    [Header("Qi Parameters")]
    public int baseContourQi;            // Базовая стоимость прорисовки
    public float sizeCapacityMultiplier; // Множитель ёмкости от размера
    
    [Header("Effects")]
    public FormationEffect[] effects;
    public float effectRadius;
    public float effectDuration;
    
    [Header("Drain")]
    public int drainInterval;            // Интервал утечки (в тиках)
    public int drainAmount;              // Ци за раз
    
    [Header("Requirements")]
    public int minCultivationLevel;
    public SkillRequirement[] skillRequirements;
}

[System.Serializable]
public class FormationEffect
{
    public string effectId;
    public float value;
    public bool isPercentage;
    public EffectTarget target;  // Self, Allies, Enemies, All
}

public enum EffectTarget
{
    Self,
    Allies,
    Enemies,
    All
}
```

---

## 7. FormationCoreData (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "FormationCoreData", menuName = "Cultivation/Formation Core Data")]
public class FormationCoreData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    
    [Header("Type")]
    public FormationCoreType coreType;
    public FormationCoreVariant variant;
    
    [Header("Level Range")]
    public int minFormationLevel;        // Мин. уровень формации
    public int maxFormationLevel;        // Макс. уровень формации
    
    [Header("Capacity")]
    public int baseCapacity;             // Базовая ёмкость
    public int maxCapacity;              // Максимальная ёмкость
    
    [Header("Qi Flow")]
    public int baseConductivity;         // Проводимость (ед/сек)
    public int qiStoneSlots;             // Слоты для камней Ци
    
    [Header("State")]
    public bool isImbued;                // Внедрена ли формация
    public string imbuedFormationId;     // ID внедрённой формации
    
    [Header("Creation Requirements")]
    public SkillRequirement craftingSkill;
    public MaterialRequirement[] materials;
    public int craftTimeMinutes;
}

[System.Serializable]
public class SkillRequirement
{
    public string skillId;
    public int minLevel;
}

[System.Serializable]
public class MaterialRequirement
{
    public string materialId;
    public int quantity;
}
```

---

## 8. ActiveFormation (Runtime)

```csharp
public class ActiveFormation
{
    // Идентификация
    public string id;
    public FormationData formationData;
    public FormationCoreData coreData;
    
    // Состояние
    public FormationStage stage;
    public int level;
    
    // Ци
    public int currentQi;
    public int maxCapacity;
    
    // Геометрия
    public Vector3 position;
    public float creationRadius;
    public float effectRadius;
    
    // Утечка
    public int drainCounter;
    public int drainInterval;
    public int drainAmount;
    
    // Участники
    public List<FormationParticipant> participants;
    
    // Таймеры
    public float stageTimer;
}

public enum FormationStage
{
    None,           // Не создана
    Drawing,        // Прорисовка контура
    Imbuing,        // Внедрение в ядро (если есть)
    Mounting,       // Монтаж (для алтарей)
    Filling,        // Наполнение Ци
    Active,         // Активна
    Depleted        // Истощена
}

public class FormationParticipant
{
    public GameObject entity;
    public float contributionRate;   // ед/сек
    public bool isCreator;
}
```

---

*Файл создан автоматически из FORMATION_SYSTEM.md*
*Код предназначен для использования в Unity проекте*
