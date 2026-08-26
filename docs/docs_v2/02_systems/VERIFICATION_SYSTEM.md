# Система верификации (VerificationService)

**Дата создания:** 2026-08-27 (Phase C)
**Исходники:**
- `game/src/Core/Interfaces/IVerificationService.cs`
- `game/src/Modules/Generator/VerificationService.cs`

---

## Назначение

`VerificationService` проверяет, что сгенерированный объект (техника /
экипировка / формация) попадает в границы своего уровня. Используется:

1. `PreGenTechniquePhase` — постпроверка пачки техник после генерации.
2. `CheatPanel` — кнопка «Dump LevelBoundaries» (демонстрация).
3. (Будущее) NPC assembly pipeline — отбраковка техник перед выдачей NPC.

---

## API

```csharp
public interface IVerificationService
{
    ValidationResult Validate(TechniqueData tech, int cultivationLevel);
    ValidationResult Validate(EquipmentData item, int cultivationLevel);
    ValidationResult Validate(FormationData form, int cultivationLevel);

    List<TechniqueData> FilterValid(IEnumerable<TechniqueData> techniques, int cultivationLevel);
    List<EquipmentData> FilterValid(IEnumerable<EquipmentData> items, int cultivationLevel);
    List<FormationData> FilterValid(IEnumerable<FormationData> forms, int cultivationLevel);
}

public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> OutOfBoundsFields { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; }
}

public enum ValidationSeverity { None, Minor, Major, Critical }
```

### ValidationResult

- `IsValid` — true, если объект прошёл все проверки.
- `OutOfBoundsFields` — список полей, вышедших за границы (для логирования).
- `Severity` — серьёзность нарушения:
  - **None**: валиден.
  - **Minor**: мелкое отклонение (≤5% от max) — не используется пока.
  - **Major**: заметное отклонение (5-25%) — общий случай.
  - **Critical**: грубое (>25% или недопустимое поле, например Level вне [1..L]).

---

## Что проверяется

### Техника

| Поле | Условие | Severity при нарушении |
|------|---------|------------------------|
| `Level` | ∈ [1, cultivationLevel] | Critical |
| `CapacityCost` | ∈ [bounds.Min, effective.Max] (с оверсамом) | Major |
| `QiCost` | ∈ [bounds.MinQiCost, effective.MaxQiCost] (Ultimate ×2 учёт) | Major |
| `BaseDamage` | ∈ [bounds.MinDamage, effective.MaxDamage] (Ultimate ×2 учёт) | Major |
| Cultivation passive | capacity=qicost=damage=0 | Major |

**Ultimate-учёт**: если `tech.IsUltimate == true`, то qiCost и damage
умножаются на `GameConstants.ULTIMATE_QI_COST_MULTIPLIER` (×2.0) и
`ULTIMATE_DAMAGE_MULTIPLIER` (×2.0) перед сравнением с границами.

### Экипировка

| Поле | Условие | Severity |
|------|---------|----------|
| `ItemLevel` | ∈ [1, cultivationLevel] | Critical |
| `Damage` (оружие) | ∈ [min, max] (с оверсамом) | Major |
| `Defense` (броня) | ∈ [min, max] | Major |
| `MaxDurability` | ∈ [min, max] | Major |
| `Coverage` (броня) | ∈ [min, max] из базового класса | Major |
| `Weight` | ∈ [min, max] (по материалам тира) | Major |

Если базовый класс не найден (accessory, ring), статы не проверяются
(только ItemLevel).

### Формация

| Поле | Условие | Severity |
|------|---------|----------|
| `RequiredLevel` | ∈ [1, cultivationLevel] | Critical |
| `Size == Heavy` | `RequiredLevel >= HEAVY_FORMATION_MIN_LEVEL` (6) | Critical |

contourQi и poolCapacity — детерминированные формулы (FormationCalculator),
не варьируются → проверка не нужна.

---

## Легендарный оверсам (+1 уровень)

Подробнее в `LEVEL_BOUNDARIES.md` § "Политика легендарного оверсама".

Кратко: для техник `Transcendent`-грейд → +1 уровень по всем статам;
`Perfect` → +1 по damage и qiCost. Для экипировки `Legendary`-rarity →
+1 по damage/defense и durability; `Mythic` → +1 по всем.

VerificationService применяет `LevelBoundaries.WithOvershootApplied(bounds, level, ...)`
перед сравнением, если `Overshoot != None`.

---

## Использование

### Batch-фильтрация (PreGenTechniquePhase)

```csharp
var batch = new List<TechniqueData>();
for (int i = 0; i < N; i++)
    batch.Add(_techniqueGenerator.GenerateSpecified(type, level, level, seed + i));

var valid = _verifier.FilterValid(batch, level);
foreach (var tech in valid)
    _registry.Register(tech);
```

`FilterValid` логирует отбракованные: `[Verifier] Reject technique
tech_xxx_L5_xxxx: CapacityCost 100 out of [50..75]`.

### Single (CheatPanel)

```csharp
var tech = _techniqueGenerator.GenerateSpecified(type, level, level, seed);
var result = _verifier.Validate(tech, level);
if (!result.IsValid)
    GD.Print($"Invalid: {string.Join(", ", result.OutOfBoundsFields)}");
```

---

## DI-регистрация

```csharp
// GeneratorModuleServices.cs
builder.Register<Core.Interfaces.IVerificationService,
                 Modules.Generator.VerificationService>(Lifetime.Singleton);
```

---

## Примеры валидных и невалидных техник

### Валидная (Combat L5 Common, mastery=50)

- baseCapacity(Combat) = 64
- levelFactor = 2^4 = 16
- masteryFactor = 1 + 50×0.005 = 1.25
- capacity = 64 × 16 × 1.25 = 1280
- qiCost = floor(64 × 16) = 1024
- damage = 1280 × 1.0 = 1280
- Bound(L5, Combat, Common): cap[1024..1536], qi[1024..1024], dmg[1024..1536]
- ✅ все в границах.

### Невалидная (capacity > max)

Если генератор выдаст capacity=2000 при L5 Common — это будет отбраковано,
т.к. max=1536 (mastery=100). Скорее всего, ошибка в генераторе (например,
перепутан baseCapacity).

---

## Связанные документы

- `LEVEL_BOUNDARIES.md` — формулы границ.
- `PRE_GENERATION.md` — pred-generation pipeline (использует FilterValid).
- `CHEAT_PANEL.md` — кнопка «Dump LevelBoundaries».
- `TECHNIQUE_SYSTEM.md` — формулы техник.
