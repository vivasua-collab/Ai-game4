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
| `PhaseOrder` | 13 | После UIInit (12), перед TechniqueGrant (14); аудит-1: 44 → 12, ревью-1: 12 → 13 |
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
                # 2026-09-08 (ревью-1 P1-1): BuildSpecified — ЯВНЫЙ грейд +
                # БЕЗ авто-регистрации (раньше GenerateSpecified выбирал грейд
                # случайно и регистрировал ДО валидации/дедупа — реестр засорялся)
                tech = _techniqueGenerator.BuildSpecified(GenTypes[t], GenGrades[g], level, level, seed)
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
            
            # Регистрация — ЕДИНСТВЕННАЯ точка записи: только валидные + уникальные
            для tech в unique:
                _registry.Register(tech)

# Контроль качества (ревью-1): gradeMismatches (грейд не тот, что просили —
#                          должен быть 0) и registryInvalid (статы вне границ
#                          СОБСТВЕННОГО уровня — должен быть 0)
```

### Логирование

После завершения фазы:

```
[PreGenTechnique] start — maxLevel=5 seed=1234567
[PreGenTechnique] done — generated=600 valid=540 duplicates=12 registered=528 (registry total=528)
[PreGenTechnique] grades registered: [Common=140, Refined=132, Perfect=140, Transcendent=16] gradeMismatches=0 registryInvalid=0
```

- `generated` — всего попыток генерации.
- `valid` — прошло VerificationService.FilterValid.
- `duplicates` — отброшено как дубли (по fingerprint).
- `registered` — зарегистрировано в реестре.
- `registry total` — итоговый размер реестра после фазы.
- `grades registered` — распределение зарегистрированных по грейдам (контроль,
  что грейд = запрошенному; ревью-1 P1-1: раньше грейд выбирался случайно).
- `gradeMismatches` — техники с НЕзапрошенным грейдом (должно быть 0).
- `registryInvalid` — техники в реестре со статами вне границ их уровня
  (должно быть 0; ревью-1 P1-1: раньше генератор регистрировал ДО валидации —
  отбракованные оставались в реестре навсегда).

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

Порядок выполнения (по PhaseOrder, нумерация 2026-09-08):

1. `CoreValidationPhase` (1)
2. `TileMapGenPhase` (2)
3. `WorldInitPhase` (3) — world-scoped сброс TechniqueRegistry (ревью-1)
4. `PlayerSpawnPhase` (4)
5. `StartingGearPhase` (5) / `AnimalSpawnPhase` (6) / `HumanNPCSpawnPhase` (7) / `GroupSpawnPhase` (8)
6. `FormationInitPhase` (9) / `ChargerInitPhase` (10) / `QuestInitPhase` (11) / `UIInitPhase` (12)
7. **`PreGenTechniquePhase` (13)** ← НАША фаза
8. `TechniqueGrantPhase` (14) — выдаёт игроку техники (может использовать
   зарегистрированные в реестре)
9. `FinalizePhase` (15)

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
