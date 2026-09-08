#nullable enable
// Создано: 2026-08-27 — Phase E: пред-генерация техник при создании мира.
// Печёт пачку техник на каждый уровень 1..cultivationLevel по всем типам
// и грейдам, валидирует через VerificationService, дедуплицирует, и
// регистрирует в TechniqueRegistry.
//
// 2026-09-08 (ревью-1 P1-1) — ДВА ИСПРАВЛЕННЫХ ДЕФЕКТА:
//   1) ГРЕЙД ЗАФИКСИРОВАН: раньше цикл по GenGrades не передавал грейд в
//      генератор (GenerateSpecified выбирал его случайным DetermineGrade,
//      Cultivation всегда Common) — «генерация по грейдам» была фикцией.
//      Теперь используется BuildSpecified(type, grade, ...) с ЯВНЫМ грейдом.
//   2) РЕЕСТР ЧИСТ: раньше генератор САМ регистрировал каждую технику
//      ДО FilterValid/дедупа — невалидные и дубли оставались в реестре.
//      BuildSpecified НЕ регистрирует; фаза регистрирует только валидные
//      уникальные техники.
//
// Параметры (базовые):
//   - cultivationLevel = (int)_qi.CultivationLevel
//   - sessionSeed = Environment.TickCount (логируется)
//   - N per (type, level, grade) = 3 (configurable)
//   - Техники: Combat/Defense/Support/Healing/Movement/Sensory/Curse/
//     Poison/Cultivation/Formation
//   - Грейды: Common/Refined/Perfect (Transcendent — 1 образец, т.к. редкий)
//
// Поток:
//   1. Для каждого уровня L (1..cultivationLevel):
//      2. Для каждого типа T (10 типов):
//         3. Для каждого грейда G (4 грейда):
//            4. Build N техник с seed = sessionSeed + L*1000 + T*100 + G*10 + i
//               (БЕЗ регистрации — BuildSpecified)
//            5. Validate через _verifier.FilterValid(batch, L)
//            6. Deduplicate через _dedup.Deduplicate(validBatch)
//            7. Register в _registry (только валидные + уникальные)
//   8. После цикла: посчитать итог, логировать.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Generator;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Пред-генерация техник при создании мира. Phase E (2026-08-27).
/// Печёт набор техник на каждый уровень, валидирует, дедуплицирует,
/// регистрирует в TechniqueRegistry. Техники доступны для выдачи через
/// TechniqueGrantPhase и NPC AI (CombatService).
/// </summary>
public sealed class PreGenTechniquePhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "PreGenTechnique";
    // 2026-08-26 (аудит-1 A-1): 44 → 12 — уникальные порядки; перед TechniqueGrant (13),
    // после UIInit (11), до финализации (14).
    // 2026-09-08 (ревью-1 P2-1): 12 → 13 — сдвиг из-за уникализации порядков.
    public override int PhaseOrder => 13;

    [Inject] private readonly ITechniqueGeneratorService _techniqueGenerator = null!;
    [Inject] private readonly IVerificationService _verifier = null!;
    [Inject] private readonly DeduplicationService _dedup = null!;
    [Inject] private readonly TechniqueRegistry _registry = null!;
    [Inject] private readonly IQiService _qi = null!;

    /// <summary>Кол-во техник на (type, level, grade) перед валидацией.</summary>
    private const int PerBatch = 3;

    /// <summary>Типы для пред-генерации (включая Cultivation).</summary>
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

    /// <summary>Грейды для пред-генерации.</summary>
    private static readonly TechniqueGrade[] GenGrades =
    {
        TechniqueGrade.Common,
        TechniqueGrade.Refined,
        TechniqueGrade.Perfect,
        TechniqueGrade.Transcendent,
    };

    /// <summary>
    /// Роль для генератора. Elder — самый разнообразный (доступ ко всем
    /// типам, см. TechniqueGeneratorService.RoleTypeMap). Так пред-генерация
    /// покрывает максимум типов.
    /// </summary>
    private const NPCRole GenRole = NPCRole.Elder;

    public override Task ExecuteAsync()
    {
        int maxLevel = (int)_qi.CultivationLevel;
        long sessionSeed = Environment.TickCount;
        Console.WriteLine($"[PreGenTechnique] start — maxLevel={maxLevel} seed={sessionSeed}");

        int totalGenerated = 0;
        int totalValid = 0;
        int totalDuplicates = 0;
        int totalRegistered = 0;
        // 2026-09-08 (ревью-1 P1-1): контроль фактического распределения грейдов
        // (раньше грейд выбирался случайно — пачка «просили 3 Common» содержала
        // случайные грейды). После фикса — ровно запрошенный грейд.
        var byGrade = new Dictionary<TechniqueGrade, int>();
        var gradeMismatches = 0;

        for (int level = 1; level <= maxLevel; level++)
        {
            for (int t = 0; t < GenTypes.Length; t++)
            {
                var type = GenTypes[t];
                for (int g = 0; g < GenGrades.Length; g++)
                {
                    var grade = GenGrades[g];
                    // Transcendent — генерируем 1 (редкий), остальные — PerBatch.
                    int batch = grade == TechniqueGrade.Transcendent ? 1 : PerBatch;

                    var generated = new List<TechniqueData>(batch);
                    for (int i = 0; i < batch; i++)
                    {
                        long seed = sessionSeed + level * 1000 + t * 100 + g * 10 + i;
                        // 2026-09-08 (ревью-1 P1-1): ЯВНЫЙ грейд + БЕЗ авто-регистрации
                        // (раньше: GenerateSpecified выбирал грейд случайно и сам
                        // регистрировал каждую технику ДО валидации/дедупа).
                        var tech = _techniqueGenerator.BuildSpecified(type, grade, level, level, seed);
                        if (tech != null)
                        {
                            generated.Add(tech);
                            if (tech.Grade != grade)
                            {
                                gradeMismatches++;
                                Console.WriteLine($"[PreGenTechnique] ⚠ GRADE MISMATCH: просили {grade}, получено {tech.Grade} ({tech.TechniqueId})");
                            }
                        }
                    }
                    totalGenerated += generated.Count;

                    // Фильтр валидных.
                    var valid = _verifier.FilterValid(generated, level);
                    totalValid += valid.Count;

                    // Дедупликация (внутри пачки + не дублирует с реестром).
                    // Сначала проверяем, нет ли уже в реестре такого fingerprint.
                    var filtered = new List<TechniqueData>();
                    foreach (var v in valid)
                    {
                        // Дедуплицируем с уже зарегистрированными техниками.
                        bool isDup = false;
                        foreach (var existing in _registry.GetAll())
                        {
                            if (DeduplicationService.Fingerprint(existing) == DeduplicationService.Fingerprint(v))
                            {
                                isDup = true;
                                break;
                            }
                        }
                        if (isDup)
                            totalDuplicates++;
                        else
                            filtered.Add(v);
                    }
                    // Внутрипакетная дедупликация.
                    var unique = _dedup.Deduplicate(filtered);
                    totalDuplicates += (filtered.Count - unique.Count);

                    // Регистрация в реестре — ЕДИНСТВЕННАЯ точка записи этой фазы:
                    // только валидные + уникальные (BuildSpecified не регистрирует).
                    foreach (var tech in unique)
                    {
                        _registry.Register(tech);
                        totalRegistered++;
                        byGrade.TryGetValue(tech.Grade, out int c);
                        byGrade[tech.Grade] = c + 1;
                    }
                }
            }
        }

        // 2026-09-08 (ревью-1 P1-1): INTEGRITY-CHECK реестра — в реестре не должно
        // остаться невалидных техник (раньше генератор регистрировал ДО
        // фильтра — отбракованные оставались в реестре навсегда).
        // Валидация против СОБСТВЕННОГО уровня техники (статы в границах
        // LevelBoundaries для (level, type, grade)): резонанс «уровень ученика» —
        // отдельная забота TechniqueService.LearnTechnique; NPC-техники уровнем
        // выше игрока валидны для своих владельцев и обязаны жить в реестре.
        int registryInvalid = 0;
        foreach (var t in _registry.GetAll())
        {
            var check = _verifier.Validate(t, t.Level);
            if (!check.IsValid)
            {
                registryInvalid++;
                Console.WriteLine($"[PreGenTechnique] ⚠ invalid in registry: {t.TechniqueId} — {check.Message}");
            }
        }

        var gradeSummary = string.Join(", ", byGrade.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        Console.WriteLine($"[PreGenTechnique] done — generated={totalGenerated} " +
                          $"valid={totalValid} duplicates={totalDuplicates} " +
                          $"registered={totalRegistered} (registry total={_registry.Count})");
        Console.WriteLine($"[PreGenTechnique] grades registered: [{gradeSummary}] " +
                          $"gradeMismatches={gradeMismatches} registryInvalid={registryInvalid}");
        if (gradeMismatches > 0)
            Console.WriteLine($"[PreGenTechnique] ❌ GRADE GUARANTEE VIOLATED: {gradeMismatches} техник с незапрошенным грейдом");
        if (registryInvalid > 0)
            Console.WriteLine($"[PreGenTechnique] ❌ REGISTRY POLLUTION: {registryInvalid} невалидных техник в реестре");
        return Task.CompletedTask;
    }
}
