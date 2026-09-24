#nullable enable
// Создано: 2026-09-10 — R15 «Оружие в руках»: каталог визуалов оружия.
//
// Назначение: единственная точка резолва спрайтов оружия для всех
// потребителей (композит игрока GameWorldController.MainHand, overlay NPC
// в NPCSpriteRenderer._Draw, иконки хотбара 1-2, кукла WeaponMain/Off).
//
// Модель (план R15 §3.1):
//   ключ кэша = (weaponClass, materialTier, rarity) → WeaponVisuals
//   (icon 32×32 + hand 48×48 + строковый TextureKey для QA);
//   текстуры генерируются процедурно ОДИН раз и кэшируются (Zero-GC per
//   frame — потребители только ссылаются на Texture2D).
//
// Fallback-цепочка WeaponClassOf (§3.2):
//   1. EquipmentData.WeaponClassId (генератор R15+);
//   2. парсинг префикса ItemId `eq_wep_{subtype}_…` (старые сейвы R11);
//   3. "sword" (generic — не краш, SPRITE_CATALOG §19).
//
// HandOffset (§1.3): позиция hand-спрайта относительно ЦЕНТРА тела.
// Кисти тела (CreatePlayerSprite/CreateNPCSprite 48×48): правая (cx+6,30),
// левая (cx-6,30) → локальные (+6,+6)/(-6,+6) от центра. Рукоять 1H в
// текстуре G=(16,36) → offset=(+14,-6) выравнивает её на правую кисть.
// Зеркалирование (facing left) — ТОЛЬКО по X: offset → (-X, +Y), см. §16.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R15: резолв и кэш визуалов оружия (7 классов × 5 тиров материала ×
/// редкость). Статик Godot-слоя (Adapter) — Core не трогаем, кроме поля
/// данных EquipmentData.WeaponClassId.
/// </summary>
public static class WeaponVisualCatalog
{
    /// <summary>Generic-класс, если класс оружия не распознан (не краш).</summary>
    public const string DefaultClass = "sword";

    /// <summary>Визуальная пара + QA-ключ (для headless-сверок).</summary>
    public sealed class WeaponVisuals
    {
        public required Texture2D Icon;  // 32×32 — хотбар/кукла
        public required Texture2D Hand;  // 48×48 — тело игрока/NPC
        public required string Key;      // "class|tier|rarity"
    }

    // Кэш: текстуры генерируются один раз на ключ.
    private static readonly Dictionary<string, WeaponVisuals> _cache = new();

    // G0 (I-11, 2026-09-25): PNG-миграция — счётчик загруженных PNG
    // (QA: GODOT_ANIMQA_DEBUG; ключи кэша НЕ меняются — PROCEDURAL_SPRITES §5).
    private static int _pngHandCount;
    private static int _pngIconCount;

    /// <summary>Ключ кэша/QA для параметров генерации.</summary>
    public static string TextureKey(string weaponClass, int materialTier, ItemRarity rarity) =>
        $"{weaponClass}|{materialTier}|{rarity}";

    /// <summary>
    /// Резолв визуалов для предмета оружия. null/не-оружие → null
    /// (слот пуст — вызывающий скрывает спрайт).
    /// </summary>
    public static WeaponVisuals? Resolve(EquipmentData? weapon)
    {
        if (weapon == null || weapon.Category != ItemCategory.Weapon) return null;
        string classId = WeaponClassOf(weapon);
        int tier = System.Math.Clamp(weapon.MaterialTier, 1, 5);
        return GetOrCreate(classId, tier, weapon.Rarity);
    }

    /// <summary>Получить (или сгенерировать) визуалы по ключу.
    /// G0 (I-11): PNG-миграция — сначала PNG из resources/sprites/equipment
    /// (по тем же ключам class|tier; редкость НЕ в имени — золотая обводка
    /// Legendary/Mythic остаётся кодом), отсутствует → процедурный рецепт
    /// R15 как сейчас. Механика крепления/офсетов/замаха не меняется.</summary>
    public static WeaponVisuals GetOrCreate(string weaponClass, int materialTier, ItemRarity rarity)
    {
        int tier = System.Math.Clamp(materialTier, 1, 5);
        string key = TextureKey(weaponClass, tier, rarity);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        // G0: PNG-первый (без editor-импорта — Image.LoadFromFile).
        ImageTexture? iconPng = LoadEquipmentPng($"icons/weapon_{weaponClass}_{tier}.png");
        ImageTexture? handPng = LoadEquipmentPng($"equipped/weapon_hand_{weaponClass}_{tier}.png");
        if (iconPng != null) _pngIconCount++;
        if (handPng != null) _pngHandCount++;

        var visuals = new WeaponVisuals
        {
            Icon = iconPng ?? ProceduralSpriteGenerator.CreateWeaponIcon(weaponClass, tier, rarity),
            Hand = handPng ?? ProceduralSpriteGenerator.CreateWeaponHandSprite(weaponClass, tier, rarity),
            Key = key,
        };
        _cache[key] = visuals;
        return visuals;
    }

    /// <summary>
    /// G0: загрузка PNG экипировки из res://resources/sprites/equipment/
    /// (null — файла нет; НЕ логируем — штатный fallback).
    /// </summary>
    private static ImageTexture? LoadEquipmentPng(string relative)
    {
        string path = $"{SpriteSheetCache.Root}/equipment/{relative}";
        if (!SpriteSheetCache.TryLoadTexture(path, out var tex)) return null;
        return tex;
    }

    /// <summary>
    /// Fallback-цепочка класса оружия: поле WeaponClassId → префикс
    /// ItemId (`eq_wep_{subtype}_…`) → DefaultClass.
    /// </summary>
    public static string WeaponClassOf(EquipmentData weapon)
    {
        if (!string.IsNullOrEmpty(weapon.WeaponClassId))
            return NormalizeClass(weapon.WeaponClassId);

        // Легаси/сейвы до R15: eq_wep_sword_L1_ab12_000001 → "sword".
        string? parsed = ParseClassFromItemId(weapon.ItemId);
        return parsed ?? DefaultClass;
    }

    /// <summary>
    /// Парсинг класса из ItemId генератора (NextId: eq_wep_{subtype}_…).
    /// null, если формат не распознан.
    /// </summary>
    public static string? ParseClassFromItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        // Устаревший формат TestItemSeeder'а: "weapon_sword_iron" и т.п.
        if (itemId.StartsWith("eq_wep_"))
        {
            string rest = itemId["eq_wep_".Length..];
            int cut = rest.IndexOf('_');
            if (cut > 0) return NormalizeClass(rest[..cut]);
            if (rest.Length > 0) return NormalizeClass(rest);
        }
        return null;
    }

    /// <summary>Нормализация к одному из 7 известных классов (иначе DefaultClass).</summary>
    public static string NormalizeClass(string classId)
    {
        return classId is "dagger" or "sword" or "axe" or "spear"
            or "greatsword" or "bow" or "staff"
            ? classId
            : DefaultClass;
    }

    /// <summary>2H классы (WeaponHandType из таблиц не доступны без предмета).</summary>
    public static bool IsTwoHanded(string weaponClass) =>
        weaponClass is "spear" or "greatsword" or "bow" or "staff";

    /// <summary>
    /// Offset hand-спрайта относительно ЦЕНТРА тела персонажа (48×48).
    /// Зеркалирование при facing-left — только X: (−X, Y).
    /// </summary>
    public static Vector2 HandOffset(string weaponClass)
    {
        return weaponClass switch
        {
            // 1H: рукоять G=(16,36) в текстуре → правая кисть (6,6) локально.
            "dagger" => new Vector2(14f, -6f),
            "sword"  => new Vector2(14f, -6f),
            "axe"    => new Vector2(14f, -6f),
            // 2H: диагональ уже через центр текстуры → лёгкий подъём.
            "spear"     => new Vector2(2f, -2f),
            "greatsword" => new Vector2(2f, -2f),
            "staff"     => new Vector2(2f, -2f),
            // Лук: вертикально у левой кисти (-6,6).
            "bow"    => new Vector2(-2f, 4f),
            _        => new Vector2(14f, -6f),
        };
    }

    /// <summary>QA: размер кэша (генерация ленивая, по требованию).</summary>
    public static int CachedEntryCount => _cache.Count;

    /// <summary>QA (GODOT_ANIMQA_DEBUG): число hand-PNG, подхваченных из каталога.</summary>
    public static int PngHandCount => _pngHandCount;

    /// <summary>QA (GODOT_ANIMQA_DEBUG): число icon-PNG, подхваченных из каталога.</summary>
    public static int PngIconCount => _pngIconCount;

    /// <summary>QA/тесты: сброс кэша (сценарии ReAssembly — новые миры).</summary>
    public static void ResetCache()
    {
        _cache.Clear();
        _pngHandCount = 0;
        _pngIconCount = 0;
    }
}
