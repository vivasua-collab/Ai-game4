#nullable enable
// Создано: 2026-09-11 — аудит боя с животными (D1–D7): боевой профиль
// животного для потребителей вне модуля NPC (таргетинг игрока, трупы,
// статы боя, рендеры). Данные снимаются AnimalService по требованию.
//
// Зачем отдельный struct: AnimalEntity (Modules/NPC) — внутренний POCO;
// Core-потребители (PlayerCombatAdapter, StatProviderAdapter) работают
// через Core-интерфейсы (IAnimalService) и Core-данные (AnimalInfo) —
// модули не тянут типы друг друга напрямую (архитектурный паттерн
// INPCService/NPCState).
using System;

namespace CultivationGame.Core.Data;

/// <summary>
/// Боевой профиль животного: ID, вид, позиция, морфология/материал,
/// статы вида (SpeciesRegistry: волк STR 8/AGI 14/VIT 10/INT 4).
/// Снимок на момент запроса — для таргетинга и пайплайна урона.
/// </summary>
public readonly struct AnimalInfo
{
    /// <summary>Уникальный ID сущности ("animal_wolf_3").</summary>
    public readonly string EntityId;

    /// <summary>Вид ("wolf" / "deer" / "rabbit").</summary>
    public readonly string SpeciesId;

    /// <summary>Тайловая позиция.</summary>
    public readonly Position2D Position;

    /// <summary>Морфология тела (Quadruped) — таблица попадания.</summary>
    public readonly Morphology Morphology;

    /// <summary>Материал тела (Organic).</summary>
    public readonly BodyMaterial Material;

    /// <summary>Живо ли животное.</summary>
    public readonly bool IsAlive;

    /// <summary>Сила вида (SpeciesData.BaseStrength).</summary>
    public readonly int Strength;

    /// <summary>Ловкость вида — уклонение в бою.</summary>
    public readonly int Agility;

    /// <summary>Живучесть вида.</summary>
    public readonly int Vitality;

    /// <summary>Интеллект вида.</summary>
    public readonly int Intelligence;

    public AnimalInfo(
        string entityId,
        string speciesId,
        Position2D position,
        Morphology morphology,
        BodyMaterial material,
        bool isAlive,
        int strength,
        int agility,
        int vitality,
        int intelligence)
    {
        EntityId = entityId ?? string.Empty;
        SpeciesId = speciesId ?? string.Empty;
        Position = position;
        Morphology = morphology;
        Material = material;
        IsAlive = isAlive;
        Strength = strength;
        Agility = agility;
        Vitality = vitality;
        Intelligence = intelligence;
    }

    /// <summary>Пустой профиль (нет такого животного) — для TryGet-семантики.</summary>
    public static AnimalInfo Empty => new(string.Empty, string.Empty, default, Morphology.Quadruped, BodyMaterial.Organic, false, 0, 0, 0, 0);

    /// <summary>Профиль заполнен (не Empty).</summary>
    public bool IsValid => !string.IsNullOrEmpty(EntityId);

    public override string ToString()
        => $"AnimalInfo({SpeciesId}#{EntityId} @ {Position}, {(IsAlive ? "alive" : "dead")}, STR={Strength} AGI={Agility})";
}
