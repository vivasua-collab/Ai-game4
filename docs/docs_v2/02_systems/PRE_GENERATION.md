# Пред-генерация техник при создании мира (PreGenTechniquePhase)

**Дата создания:** 2026-08-27 (Phase E)
**Исходник:** `game/src/Entry/Phases/PreGenTechniquePhase.cs`

---

## Назначение

`PreGenTechniquePhase` — фаза сборки сцены (scene-assembly phase),
которая печёт набор техник на каждый уровень 1..cultivationLevel по всем
типам и грейдам, валидирует через `VerificationService`, дедуплицирует
через `DeduplicationService`, и регистрирует валидные уникальные техники
в `TechniqueRegistry`.

Цель: обеспечить, чтобы у боевого поиска (CombatService) был широкий
выбор техник на каждом уровне, без дублей и без невалидных объектов.

---

## Параметры фазы

| Параметр | Значение | Описание |
|----------|----------|----------|
| `PhaseName` | "PreGenTechnique" | Имя фазы для логов |
| `PhaseOrder` | 44 | Перед TechniqueGrantPhase (45) |
| `SkipOnLoad` | true (default) | При загрузке сейва фаза пропускается |
| `PerBatch` | 3 | Кол-во техник на (type, level, grade) перед валидацией |
| `GenRole` | NPCRole.Elder | Самый разнообразный пул типов |

### Типы для пред-генерации

```csharp
private static readonly TechniqueType[] GenTypes =
{
    TechniqueType.Combat,
    TechniqueType.Defense,
    TechniqueType.Support,
    TechniqueType.Healing,
    TechniqueType.Movement,
    TechniqueType.Sensory,
    TechniqueType.Curse,
    TechniqueType.Poison,
    TechniqueType.Cultivation,
    TechniqueType.Formation,
};
```

### Грейды для пред-генерации

```csharp
private static readonly TechniqueGrade[] GenGrades =
{
    TechniqueGrade.Common,
    TechniqueGrade.Refined,
    TechniqueGrade.Perfect,
    TechniqueGrade.Transcendent,  // 1 образец (редкий)
};
```

---

## Алгоритм

```
maxLevel = (int) _qi.CultivationLevel
sessionSeed = Environment.TickCount
для level = 1..maxLevel:
    для t = 0..GenTypes.Length-1:
        для g = 0..GenGrades.Length-1:
            batch = (g == Transcendent) ? 1 : PerBatch
            generated = []
            для i = 0..batch-1:
                seed = sessionSeed + level*1000 + t*100 + g*10 + i
                tech = _techniqueGenerator.GenerateSpecified(GenTypes[t], level, level, seed)
                generated.Add(tech)
            
            # Фильтр валидных
            valid = _verifier.FilterValid(generated, level)
            
            # Дедупликация с реестром (отбросить тех, что уже есть по fingerprint)
            filtered = []
            для v в valid:
                если нет в _registry.GetAll() с тем же fingerprint:
                    filtered.Add(v)
            
            # Внутрипакетная дедупликация
            unique = _dedup.Deduplicate(filtered)
            
            # Регистрация
            для tech в unique:
                _registry.Register(tech)
```

### Логирование

После завершения фазы:

```
[PreGenTechnique] start — maxLevel=5 seed=1234567
[PreGenTechnique] done — generated=600 valid=540 duplicates=12 registered=528 (registry total=528)
```

- `generated` — всего попыток генерации.
- `valid` — прошло VerificationService.FilterValid.
- `duplicates` — отброшено как дубли (по fingerprint).
- `registered` — зарегистрировано в реестре.
- `registry total` — итоговый размер реестра после фазы.

---

## Детерминизм

- `sessionSeed = Environment.TickCount` — НЕ детерминирован между запусками,
  но логируется, чтобы можно было воспроизвести конкретную генерацию.
- В будущем: seed должен быть частью save (seed мира), чтобы мир
  воспроизводился. Сейчас — каждый запуск новый seed.

---

## DI-зависимости

```csharp
[Inject] ITechniqueGeneratorService _techniqueGenerator
[Inject] IVerificationService _verifier
[Inject] DeduplicationService _dedup
[Inject] TechniqueRegistry _registry
[Inject] IQiService _qi  // для CultivationLevel
```

---

## Регистрация фазы

```csharp
// SceneAssemblyRegistrar.cs
builder.Register<PreGenTechniquePhase>(Lifetime.Singleton);
```

Порядок выполнения (по PhaseOrder):

1. `CoreValidationPhase` (10)
2. `TileMapGenPhase` (20)
3. `WorldInitPhase` (30)
4. `PlayerSpawnPhase` (40)
5. **`PreGenTechniquePhase` (44)** ← НАША фаза
6. `TechniqueGrantPhase` (45) — выдаёт игроку техники (может использовать
   зарегистрированные в реестре)
7. `AnimalSpawnPhase` (50)
8. `HumanNPCSpawnPhase` (55)
9. ... последующие фазы

---

## Влияние на производительность

- При maxLevel=5: 5 × 10 × 4 = 200 пачек × 3 = 600 генераций.
- Каждая генерация занимает < 1 мс (только формулы + SeededRandom).
- Дедупликация — O(N²) по fingerprint (по всем уже зарегистрированным),
  но N ≤ 600 → 600² = 360_000 операций сравнения строк.
  На практике меньше (раннее отбрасывание).
- Время на фазу: ~200-500 мс (один раз при новой игре).

---

## Связанные документы

- `VERIFICATION_SYSTEM.md` — Validation API.
- `LEVEL_BOUNDARIES.md` — формулы границ.
- `TECHNIQUE_SYSTEM.md` — формулы техник.
- `CHEAT_PANEL.md` — кнопка «Подсчёт дублей техник».
