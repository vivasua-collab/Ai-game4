# 📜 Отчёт: Использование техник практиками

**Создано:** 2026-04-09 06:38:00 UTC
**Редактировано:** 2026-05-23 06:30:00 UTC — обновлён статус: теория актуальна для доработки системы техник
**Проект:** Cultivation World Simulator

> **⚠️ СТАТУС:** Теоретический документ. Формулы и пайплайн использования техник описывают
> целевую архитектуру. При доработке системы техник учитывать:
> - 10-слойный пайплайн урона — актуален (реализован в DamageService)
> - Формулы расчёта qiCost, capacity, damage — актуальны (реализованы в TechniqueCapacity, TechniqueChargeService)
> - Код примеров — legacy (Singleton/ServiceLocator), при реализации использовать VContainer+MessagePipe
> - Ссылки на TechniqueController → теперь TechniqueService (модульная архитектура)

---

## 📋 Обзор

Этот документ описывает порядок выполнения использования техник практиками — от проверки условий до нанесения урона.

---

## 🔄 Порядок выполнения использования техники

### Диаграмма потока

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ПОРЯДОК ИСПОЛЬЗОВАНИЯ ТЕХНИКИ                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. ПРОВЕРКА УСЛОВИЙ                                                         │
│  ├── Кулдаун = 0?                                                           │
│  ├── Уровень культивации ≥ minCultivationLevel?                             │
│  └── Текущее Ци ≥ qiCost?                                                   │
│      │                                                                       │
│      ├── FAIL → Возврат ошибки                                              │
│      └── PASS ↓                                                             │
│                                                                              │
│  2. РАСЧЁТ ПАРАМЕТРОВ                                                        │
│  ├── qiCost = baseCapacity × 2^(techniqueLevel - 1)                         │
│  ├── capacity = baseCapacity × 2^(level-1) × (1 + mastery × 0.5%)           │
│  └── damage = capacity × gradeMultiplier × ultimateMultiplier               │
│      │                                                                       │
│      ↓                                                                       │
│                                                                              │
│  3. ТРАТА ЦИ                                                                 │
│  └── qiController.SpendQi(qiCost)                                           │
│      │                                                                       │
│      ↓                                                                       │
│                                                                              │
│  4. УСТАНОВКА КУЛДАУНА                                                       │
│  └── cooldownRemaining = cooldown × 60 секунд                               │
│      │                                                                       │
│      ↓                                                                       │
│                                                                              │
│  5. ПОВЫШЕНИЕ МАСТЕРСТВА                                                     │
│  └── mastery = min(100, mastery + 0.01)                                     │
│      │                                                                       │
│      ↓                                                                       │
│                                                                              │
│  6. ВОЗВРАТ РЕЗУЛЬТАТА                                                       │
│  └── TechniqueUseResult { success, damage, qiCost, ... }                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Формулы расчёта

### 1. Стоимость Ци

```csharp
qiCost = floor(baseCapacity × 2^(techniqueLevel - 1))
```

| Тип техники | Base Capacity | L1 | L2 | L3 | L4 | L5 |
|-------------|---------------|----|----|----|----|----|
| Formation | 80 | 80 | 160 | 320 | 640 | 1280 |
| Defense | 72 | 72 | 144 | 288 | 576 | 1152 |
| Combat-Melee | 64 | 64 | 128 | 256 | 512 | 1024 |
| Support | 56 | 56 | 112 | 224 | 448 | 896 |
| Movement | 40 | 40 | 80 | 160 | 320 | 640 |

### 2. Ёмкость техники

```csharp
capacity = floor(baseCapacity × 2^(level-1) × (1 + mastery × 0.005))
```

**Пример: Combat-Melee L3, мастерство 50%**
```
capacity = 64 × 2^2 × (1 + 50 × 0.005)
         = 64 × 4 × 1.25
         = 320
```

### 3. Итоговый урон

```csharp
damage = capacity × gradeMultiplier × ultimateMultiplier
```

| Grade | Множитель |
|-------|-----------|
| Common | ×1.0 |
| Refined | ×1.2 |
| Perfect | ×1.4 |
| Transcendent | ×1.6 |
| Ultimate (флаг) | ×1.3 |

---

## ⏱️ Время каста

```csharp
castTime = qiCost / effectiveSpeed

effectiveSpeed = conductivity × (1 + cultivationBonus) × (1 + masteryBonus)
```

**Пример:**
- qiCost = 50
- conductivity = 2.0
- cultivationLevel = 3 (+10%)
- mastery = 50% (+50%)

```
effectiveSpeed = 2.0 × 1.10 × 1.50 = 3.3
castTime = 50 / 3.3 = 15.15 секунд
```

---

## 🎯 10-слойный пайплайн урона

После использования техники, урон проходит через 10 слоёв обработки:

### Слой 1: Исходный урон
```
rawDamage = capacity × gradeMultiplier × ultimateMultiplier
```

### Слой 2: Level Suppression
```
damage ×= suppressionMultiplier
```

| Разница уровней | Normal | Technique | Ultimate |
|-----------------|--------|-----------|----------|
| 0 | ×1.0 | ×1.0 | ×1.0 |
| 1 | ×0.5 | ×0.75 | ×1.0 |
| 2 | ×0.1 | ×0.25 | ×0.5 |
| 3 | ×0.0 | ×0.05 | ×0.25 |
| 4+ | ×0.0 | ×0.0 | ×0.1 |

### Слой 3: Часть тела
```
hitPart = RollBodyPart()
```

| Часть | Шанс |
|-------|------|
| Torso | 40% |
| Head | 5% |
| Heart | 2% |
| Arms | 20% |
| Legs | 24% |
| Hands | 8% |
| Feet | 1% |

### Слой 4: Активная защита
```csharp
// Уклонение
dodgeChance = 5% + (AGI-10) × 0.5% - armorDodgePenalty

// Парирование
parryChance = weaponParryBonus + (AGI-10) × 0.3%

// Блок
blockChance = shieldBlock + (STR-10) × 0.2%
```

### Слой 5: Qi Buffer

**Техники Ци:**
| Защита | Поглощение | Ratio | Пробитие |
|--------|------------|-------|----------|
| Raw Qi | 90% | 3:1 | 10% |
| Shield | 100% | 1:1 | 0% |

**Физический урон:**
| Защита | Поглощение | Ratio | Пробитие |
|--------|------------|-------|----------|
| Raw Qi | 80% | 5:1 | 20% |
| Shield | 100% | 2:1 | 0% |

### Слои 6-7: Броня
```csharp
if (random() < coverage) {
    damage *= (1 - damageReduction);  // Кап 80%
    damage = max(1, damage - armor × 0.5);
}
```

### Слой 8: Материал тела
```csharp
damage *= (1 - materialReduction)
```

| Материал | Снижение |
|----------|----------|
| Organic | 0% |
| Iron | 30% |
| Spirit | 50% |

### Слой 9: Распределение HP
```csharp
redHP -= damage × 0.7   // Функциональная
blackHP -= damage × 0.3  // Структурная
```

### Слой 10: Последствия
- redHP ≤ 0 → Паралич части тела
- blackHP ≤ 0 → Отрубание
- heart HP ≤ 0 → Смерть
- head HP ≤ 0 → Смерть

---

## 📝 Пример полного расчёта

**Сценарий:**
- Атакующий: L3, Combat-Melee L2, Refined, Mastery 50%
- Защищающийся: L4, Qi 5000, Raw Qi defence

**Расчёт:**

```
1. Base Capacity = 64 (Combat-Melee)
2. qiCost = 64 × 2^1 = 128

3. Capacity = 64 × 2^1 × (1 + 50 × 0.005)
            = 64 × 2 × 1.25
            = 160

4. Raw Damage = 160 × 1.2 (Refined)
              = 192

5. Level Suppression:
   diff = 4 - 3 = 1
   multiplier = 0.75 (Technique)
   damage = 192 × 0.75 = 144

6. Qi Buffer (Qi Technique vs Raw Qi):
   absorbed = 144 × 0.9 = 129.6
   qiNeeded = 129.6 × 3 = 388.8
   piercing = 144 × 0.1 = 14.4

7. Final Damage = 14.4
8. Red HP = 14.4 × 0.7 = 10.08
9. Black HP = 14.4 × 0.3 = 4.32
```

---

## 🔧 Код реализации

### TechniqueController.UseTechnique()

```csharp
public TechniqueUseResult UseTechnique(LearnedTechnique technique)
{
    // 1. Проверка условий
    if (!CanUseTechnique(technique))
        return new TechniqueUseResult { Success = false };

    // 2. Расчёт параметров
    int qiCost = CalculateQiCost(technique);
    int capacity = CalculateCapacity(technique);
    int damage = CalculateDamage(technique);

    // 3. Трата Ци
    qiController.SpendQi(qiCost);

    // 4. Установка кулдауна
    technique.CooldownRemaining = technique.Data.cooldown * 60f;

    // 5. Повышение мастерства
    IncreaseMastery(technique, 0.01f);

    // 6. Возврат результата
    return new TechniqueUseResult {
        Success = true,
        QiCost = qiCost,
        Capacity = capacity,
        Damage = damage
    };
}
```

### DamageCalculator.CalculateDamage()

```csharp
public static DamageResult CalculateDamage(
    int techniqueCapacity,
    AttackerParams attacker,
    DefenderParams defender)
{
    // СЛОЙ 1: Raw Damage
    float damage = TechniqueCapacity.CalculateDamage(...);

    // СЛОЙ 2: Level Suppression
    damage *= LevelSuppression.CalculateSuppression(...);

    // СЛОЙ 3: Body Part
    result.HitPart = DefenseProcessor.RollBodyPart();

    // СЛОЙ 4: Active Defense
    // dodge/parry/block checks...

    // СЛОЙ 5: Qi Buffer
    var qiResult = QiBuffer.ProcessQiTechniqueDamage(...);
    damage = qiResult.PiercingDamage;

    // СЛОИ 6-7: Armor
    // coverage and reduction...

    // СЛОЙ 8: Body Material
    damage *= (1 - materialReduction);

    // СЛОЙ 9: HP Distribution
    result.RedHPDamage = damage * 0.7f;
    result.BlackHPDamage = damage * 0.3f;

    // СЛОЙ 10: Consequences
    result.IsFatal = IsFatalHit(result);

    return result;
}
```

---

## 📚 Связанные документы

- [TECHNIQUE_SYSTEM.md](../docs/TECHNIQUE_SYSTEM.md) — Система техник
- [ALGORITHMS.md](../docs/ALGORITHMS.md) — Алгоритмы и формулы
- [COMBAT_SYSTEM.md](../docs/COMBAT_SYSTEM.md) — Боевая система
- [QI_SYSTEM.md](../docs/QI_SYSTEM.md) — Система Ци

---

*Документ создан: 2026-04-09 06:38:00 UTC*
