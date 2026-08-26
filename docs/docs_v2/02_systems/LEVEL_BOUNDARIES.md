# Система границ уровней (LevelBoundaries)

**Дата создания:** 2026-08-27 (Phase B)
**Исходник:** `game/src/Core/Data/LevelBoundaries.cs`

---

## Назначение

`LevelBoundaries` — статический калькулятор min/max границ характеристик
для техник, экипировки и формаций. Используется `VerificationService`
для проверки, что сгенерированный объект попал в границы своего уровня.

Принцип: генератор (TechniqueGeneratorService, EquipmentGenerator) уже
вычисляет конкретные значения через формулы. `LevelBoundaries` — это
зеркало тех же формул, но в виде диапазона `[min..max]`, который
используется верификатором для отбраковки невалидных объектов.

---

## Структура

```csharp
namespace CultivationGame.Core.Data;

public static class LevelBoundaries
{
    public static TechniqueBounds TechniqueBoundsFor(int level, TechniqueType type, TechniqueGrade grade);
    public static TechniqueBounds WithOvershootApplied(TechniqueBounds original, int level, TechniqueType type, TechniqueGrade grade);

    public static EquipmentBounds WeaponBoundsFor(int level, WeaponBaseClass wclass, EquipmentGrade grade, ItemRarity rarity);
    public static EquipmentBounds ArmorBoundsFor(int level, ArmorBaseClass aclass, EquipmentGrade grade, ItemRarity rarity);
    public static EquipmentBounds WithOvershootApplied(EquipmentBounds original, int level, EquipmentGrade grade, WeaponBaseClass? wclass = null, ArmorBaseClass? aclass = null);

    public static FormationBounds FormationBoundsFor(int level, FormationSize size);
}
```

> 2026-08-26: `WithOvershootApplied` принимает grade — next-границы считаются
> для ТОГО ЖЕ грейда (не Common): семантика «+1 уровень при том же грейде».
> Иначе Transcendent-легендарка с формулами L+1 (eff 2.0) выйдет за окно
> Common L+1 (eff 1.0).

### TechniqueBounds

| Поле | Описание |
|------|----------|
| MinCapacity, MaxCapacity | min/max для CapacityCost |
| MinQiCost, MaxQiCost | min/max для QiCost |
| MinDamage, MaxDamage | min/max для BaseDamage |
| Overshoot | Политика легендарного оверсама |

### EquipmentBounds

| Поле | Описание |
|------|----------|
| MinDamage, MaxDamage | min/max для Damage (оружие) |
| MinDefense, MaxDefense | min/max для Defense (броня) |
| MinDurability, MaxDurability | min/max для MaxDurability |
| MinCoverage, MaxCoverage | min/max для Coverage (броня) |
| MinWeight, MaxWeight | min/max для Weight |
| Overshoot | Политика легендарного оверсама |

### FormationBounds

| Поле | Описание |
|------|----------|
| MinContourQi, MaxContourQi | min/max для contourQi |
| MinPoolCapacity, MaxPoolCapacity | min/max для пула Ци |

---

## Формулы

### Техники (зеркало TechniqueGeneratorService)

- `baseCapacity = GameConstants.BaseCapacityByType[type]` (Combat=64, Defense=72, ...)
- `levelFactor = 2^(level-1)`
- `masteryFactor = 1.0 + mastery × 0.005` (mastery 0..100 → 1.0×..1.5×)
- `capacity = baseCapacity × levelFactor × masteryFactor`
  → **Min = при mastery=0, Max = при mastery=100**
- `qiCost = floor(baseCapacity × levelFactor)` — НЕ зависит от mastery → Min=Max
- `damage = capacity × gradeMultiplier` → Min и Max выводятся из Min/Max capacity
- `gradeMult`: Common ×1.0, Refined ×1.3, Perfect ×1.6, Transcendent ×2.0
- **Cultivation-техники**: пассивные, capacity=qiCost=damage=0.

### Экипировка (зеркало EquipmentGenerator)

#### Оружие

- `damage = (base + perLevel×(L-1)) × speedScale × eff × (1 + matDamageBonus/100)`
- `speedScale = 1 + (1 - AttackSpeedFactor) / 2` (быстрое → легче, медленное → больнее)
- `eff = GradeProfiles.EfficiencyMult[grade]` (Damaged 0.5, Common 1.0, Refined 1.3, Perfect 1.6, Transcendent 2.0)
- `matDamageBonus`: min/max по материалам тира `clamp((L+1)/2, 1, 5)`
- `durability = MaterialDurabilityByTier[tier] × DurabilityMult[grade]`
- `weight = wclass.WeightKg × material.WeightMult` (min/max по материалам тира)

#### Броня

- `defense = (base + perLevel×(L-1)) × eff × (1 + matDefenseBonus/100)`
- `coverage = ArmorBaseClass.CoverageMin..CoverageMax` (рандом в генераторе)
- `durability`, `weight` — аналогично оружию

### Формации (зеркало FormationCalculator)

- `contourQi = FORMATION_BASE_CONTOUR_QI × 2^(level-1)` (80 × 2^(L-1))
- `poolCapacity = contourQi × FormationSizeMultipliers[size]`
  - Small ×10, Medium ×50, Large ×200, Great ×1000, Heavy ×10000
- Формулы детерминированы → Min=Max

---

## Политика легендарного оверсама (B3)

```csharp
public enum OvershootPolicy { None, DamageAndQi, All }
```

| Объект | Условие | Overshoot | Эффект |
|--------|---------|-----------|--------|
| Technique | Common/Refined | None | строго в границах L |
| Technique | Perfect | DamageAndQi | +1 уровень по damage и qiCost |
| Technique | Transcendent | All | +1 уровень по всем статам |
| Equipment | Common..Epic | None | строго в границах L |
| Equipment | Legendary | DamageAndQi | +1 по Damage/Defense и Durability |
| Equipment | Mythic | All | +1 по всем статам |

`WithOvershootApplied(bounds, level, grade, ...)` возвращает расширенные границы
(Max поднят до Bound(L+1).Max того же grade по выбранным полям).

### Пример (Combat, L5, Transcendent)

- `TechniqueBoundsFor(5, Combat, Transcendent)` → базовые границы L5
- `WithOvershootApplied(bounds, 5, Combat, Transcendent)` → MaxCapacity, MaxQiCost, MaxDamage
  взяты из `TechniqueBoundsFor(6, Combat, Transcendent)` (+1 уровень, тот же грейд)

---

## Epic→Legendary промоушен и оверкап (2026-08-26)

**Источники:** `GameConstants` (константы), `EquipmentGenerator`
(логика), `IEquipmentGenerator` (API).

### Двухступенчатая схема

```
Ролл грейда (EquipmentGradeWeightsByLevel)
  └─ Transcendent (Epic)? ──► ролл EPIC_TO_LEGENDARY_PROMOTE_CHANCE (20%)
         │                          │
         │ неудача                  │ успех → LEGENDARY
         ▼                          ▼
       Epic (как раньше)    ролл LEGENDARY_OVERCAP_CHANCE (18%)
                                   │            │
                                   ▼            ▼
                            ОВЕРКАП (18%)   БЕЗ оверкапа (82%)
                            статы по         статы по формулам L
                            формулам L+1     + перки легендарки
```

### Константы (GameConstants)

| Константа | Значение | Смысл |
|-----------|----------|--------|
| `EPIC_TO_LEGENDARY_PROMOTE_CHANCE` | 0.20 | 20% эпиков → легендарки |
| `LEGENDARY_OVERCAP_CHANCE` | 0.18 | 18% легендарок получают статы L+1 (диапазон ТЗ 10–25%) |
| `LEGENDARY_VALUE_MULTIPLIER` | 3.0 | множитель цены легендарки |

### Итоговые доли (по весам грейдов)

| Уровень | Вес Transcendent | Epic | Legendary |
|---------|------------------|------|-----------|
| L1–L6 | 0% | 0% | 0% |
| L7–L8 | 5% | 4.0% | 1.0% |
| L9+ | 20% | 16.0% | 4.0% |

### Легендарка ВСЕГДА получает (независимо от оверкапа)

- `Rarity = Legendary`, суффикс имени «(легендар.)»;
- гарантированное зачарование (из eligible по MinGrade ≤ Transcendent);
- максимум стат-бонусов грейда (`BonusCountMax` = 6 для Transcendent)
  с максимальной силой (5 × BonusPowerMult);
- `Value × 3`.

### Оверкап (только 18% легендарок)

- **Оружие:** `Damage = (base + perLevel×((L+1)-1)) × speedScale × eff × (1+matBonus/100)`
  — базовая линия считается для L+1, материал и грейд прежние.
- **Броня:** `Defense` — аналогично для L+1.
- **Оба:** `MaxDurability = MaterialDurabilityByTier[tier(L+1)] × DurabilityMult`.
- Пометка «ОВЕРКАП: …» в Description.
- `RequiredCultivationLevel` остаётся L — носится на своём уровне,
  «заход по характеристикам на +1 уровень» (политика DamageAndQi:
  только Damage/Defense + Durability, НЕ Weight/Coverage).
- **Edge case:** L = MAX_CULTIVATION_LEVEL (10) — формулы L+1 упираются
  в потолок, легендарка получает только перки.

### Верификация

- Легендарка БЕЗ оверкапа — в базовых границах L → валидна.
- Легендарка С оверкапом — в расширенных границах
  [min_L .. max_{L+1, тот же grade}] → валидна.
- Правило 10–25% — ГЕНЕРАЦИОННОЕ (распределение), а не
  валидационное: верификатор принимает оба случая.

### API (IEquipmentGenerator)

```csharp
// Обычная генерация с встроенным промоушеном (20% от эпиков):
EquipmentData GenerateWeapon(int level, string? subtype = null, long seed = 0);

// Принудительная легендарка (forceOvercap: null=ролл 18%, true/false=детерминированно):
EquipmentData GenerateLegendaryWeapon(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null);
EquipmentData GenerateLegendaryArmor(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null);
```

### Детерминизм

Роллы промоушена/оверкапа потребляются из общего SeededRandom и ТОЛЬКО
для Transcendent-предметов — последовательность rng для обычных
предметов не меняется. Зачарование легендарки — с явным производным
сидом (seed×31+7) для стабильности headless-дампов.

### Замер фактических шансов

`GODOT_GEN_DEBUG=1` (секция «Epic→Legendary промоушен + оверкап»):
400 генераций L9 + 40 принудительных легендарок с верификацией.
Эталонный прогон 2026-08-26: Epic 16.8%, Legendary 4.0%, оверкап 12.5%
(n=16, биномиальный шум), верификация 40/40.

---

## Использование

### Из VerificationService

```csharp
var bounds = LevelBoundaries.TechniqueBoundsFor(tech.Level, tech.Type, tech.Grade);
var effective = LevelBoundaries.WithOvershootApplied(bounds, tech.Level, tech.Type, tech.Grade);
if (tech.CapacityCost < effective.MinCapacity || tech.CapacityCost > effective.MaxCapacity)
    result.AddOutOfBounds("CapacityCost");
```

### Из CheatPanel (dump)

```csharp
var b = LevelBoundaries.TechniqueBoundsFor(level, TechniqueType.Combat, TechniqueGrade.Common);
GD.Print($"L{level}: capacity[{b.MinCapacity}..{b.MaxCapacity}] dmg[{b.MinDamage}..{b.MaxDamage}]");
```

---

## Связанные документы

- `VERIFICATION_SYSTEM.md` — VerificationService использует границы.
- `PRE_GENERATION.md` — pred-generation pipeline валидирует каждую технику.
- `TECHNIQUE_SYSTEM.md` — формулы генерации техник.
- `EQUIPMENT_SYSTEM.md` — формулы генерации экипировки.
- `FORMATION_SYSTEM.md` — формулы формаций.
