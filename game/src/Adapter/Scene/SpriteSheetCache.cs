#nullable enable
// Создано: 2026-09-25 — G0 «Подготовка спрайтов» (директива 25.09:
// «выполни все подготовительные задачи — генерация и доставка позже»).
//
// I-2 плана SPRITE_ANIMATION_PLAN §11: лист-загрузчик PNG + fallback на
// процедурный по контракту PROCEDURAL_SPRITES §5 (тот же ключ кэша,
// рендерер не переписывается).
//
// Модель:
//   animId ("player_walk" / "npc_guard_idle" / "animal_wolf_run" / …)
//     → файл game/resources/sprites/<категория>/<animId>.png
//     → SpriteSheet { Texture, FrameCount, Rows, FrameSize, Fps, Loop }
//   PNG отсутствует → TryGetSheet возвращает null → вызывающий рисует
//   процедурный спрайт КАК СЕЙЧАС (нулевая визуальная регрессия до
//   доставки ассетов).
//
// Загрузка — Image.LoadFromFile + ImageTexture (НЕ ResourceLoader):
//   - не требует editor-импорта (.import) — файл можно положить в каталог
//     и он сразу работает, в т.ч. в headless-режиме;
//   - фильтр — NEAREST (project.godot default_texture_filter=0; CanvasItem
//     игрока задаёт Nearest явно).
//
// Формат листа (SPRITE_ANIMATION_PLAN §4.1):
//   D1: полоса «1 анимация × 1 направление × N кадров» — 64N × 64;
//   D2: сетка 5 рядов-направлений × N кадров — 64N × 64×5 (Rows=5);
//   кадры индексируются Sprite2D.Hframes/Vframes/Frame (§17 каталога) либо
//   DrawTextureRegion (NPC/звери в _Draw).
//
// Реестр AnimSpec — fps/loop по канону §4.3/§5.1/§6 плана; счёт кадров
// АВТОРИТЕТНО берётся из размеров PNG (ширина/высота делятся на 64).
// Справочник доставки (манифест) — tools/sprites/sprites_manifest.json.
using Godot;
using System.Collections.Generic;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// G0: единственная точка загрузки PNG-листов спрайтов. Кэш позитивный и
/// негативный (отсутствие файла тоже кэшируется — PNG-набор статичен в
/// пределах сессии). ResetCache — QA/ReAssembly.
/// </summary>
public static class SpriteSheetCache
{
    /// <summary>Кадр рабочего формата (SPRITE_ANIMATION_PLAN §4.1).</summary>
    public const int FramePx = 64;

    /// <summary>Путь-корень спрайтов в res:// (§6.4 SPRITE_PROMPTS_CHARACTERS).</summary>
    public const string Root = "res://resources/sprites";

    /// <summary>Лист анимации: текстура + параметры прокрутки кадров.</summary>
    public sealed class SpriteSheet
    {
        public required Texture2D Texture;  // полоса (Rows=1) или сетка (Rows>1)
        public required int FrameCount;     // кадров в одном ряду-направлении
        public required int Rows;           // 1 = D1-полоса; 5 = D2-сетка направлений
        public required int FrameSize;      // 64 (или 32/24 для стикеров/иконок)
        public required int Fps;            // канон §17: 10–15; idle медленнее
        public required bool Loop;          // loop / one-shot (+hold на последнем)
        public required string AnimId;      // "player_walk"
        public required string Path;        // res://… (QA-трейс)

        /// <summary>Кадр по прогрессу [0..1) с учётом ряда (D2: row*N+col).</summary>
        public int FrameAt(float progress, int row = 0)
        {
            int col = Loop
                ? (int)(progress * FrameCount) % FrameCount
                : Mathf.Min((int)(progress * FrameCount), FrameCount - 1);
            return Mathf.Clamp(row, 0, Rows - 1) * FrameCount + col;
        }
    }

    /// <summary>Спецификация анимации из реестра (fps/loop — код, кадры — PNG).</summary>
    public sealed class AnimSpec
    {
        public required int Fps;
        public required bool Loop;
    }

    // === Реестр анимаций (D1; fps/loop — канон плана §5.1/§6/§6.2) =========
    // Триггеры состояний подключаются поэтапно (фазы G1–G5); реестр уже
    // содержит весь целевой набор, чтобы доставка PNG не требовала правок.
    private static readonly Dictionary<string, AnimSpec> Specs = new()
    {
        // — Игрок (§5.1: 13 листов / 52–56 кадров) —
        ["player_idle"]        = new() { Fps = 7,  Loop = true  },
        ["player_walk"]        = new() { Fps = 11, Loop = true  },
        ["player_run"]         = new() { Fps = 14, Loop = true  },
        ["player_melee"]       = new() { Fps = 15, Loop = false }, // 6к@15 = 0.4с ≈ замах 0.42
        ["player_bow"]         = new() { Fps = 12, Loop = false },
        ["player_cast"]        = new() { Fps = 10, Loop = true  },
        ["player_meditate"]    = new() { Fps = 5,  Loop = true  },
        ["player_guard"]       = new() { Fps = 8,  Loop = true  },
        ["player_hit"]         = new() { Fps = 12, Loop = false }, // 2к@12 ≈ 0.17с
        ["player_death"]       = new() { Fps = 12, Loop = false }, // hold последнего
        ["player_dash"]        = new() { Fps = 15, Loop = false },
        ["player_sleep"]       = new() { Fps = 2,  Loop = true  },
        ["player_breakthrough"] = new() { Fps = 15, Loop = false },
        // — NPC (§6.1: база + роли; melee/cast — в базовом цвете) —
        ["npc_idle"]           = new() { Fps = 6,  Loop = true  },
        ["npc_walk"]           = new() { Fps = 10, Loop = true  },
        ["npc_melee"]          = new() { Fps = 15, Loop = false },
        ["npc_cast"]           = new() { Fps = 10, Loop = true  },
        ["npc_hit"]            = new() { Fps = 12, Loop = false },
        ["npc_death"]          = new() { Fps = 12, Loop = false },
        // — Звери (§6.2) —
        ["animal_idle"]        = new() { Fps = 6,  Loop = true  },
        ["animal_walk"]        = new() { Fps = 10, Loop = true  },
        ["animal_run"]         = new() { Fps = 14, Loop = true  },
        ["animal_attack"]      = new() { Fps = 15, Loop = false },
        ["animal_death"]       = new() { Fps = 12, Loop = false },
    };

    /// <summary>Реестр: спецификация по animId (null — неизвестный id).</summary>
    public static AnimSpec? SpecOf(string animId) =>
        Specs.TryGetValue(animId, out var s) ? s : null;

    /// <summary>Все зарегистрированные animId (QA/манифест-сверка).</summary>
    public static IReadOnlyCollection<string> RegisteredAnimIds => Specs.Keys;

    // === Кэш ================================================================
    private static readonly Dictionary<string, SpriteSheet?> _sheets = new();
    private static int _loadAttempts; // QA: попыток загрузки (позитив+негатив)

    /// <summary>
    /// Резолв листа по animId: PNG из каталога категорий (player_* →
    /// characters/player, npc_* → characters/npc, animal_* → animals,
    /// corpse_* → corpses). Отсутствие (негативный кэш) → null → fallback.
    /// </summary>
    public static SpriteSheet? TryGetSheet(string animId)
    {
        if (string.IsNullOrEmpty(animId)) return null;
        if (_sheets.TryGetValue(animId, out var cached)) return cached;

        string path = $"{Root}/{CategoryOf(animId)}/{animId}.png";
        var sheet = LoadSheet(path, animId);
        _sheets[animId] = sheet; // негативный результат тоже кэшируем
        return sheet;
    }

    /// <summary>
    /// Прямая загрузка листа по пути (QA-стенд _qa/, будущий спрайт-вьюер):
    /// тот же контракт формата (кадры по 64, Rows = h/64).
    /// </summary>
    public static SpriteSheet? LoadSheetDirect(string resPath, int fps, bool loop)
    {
        return LoadSheet(resPath, System.IO.Path.GetFileNameWithoutExtension(resPath), fps, loop);
    }

    /// <summary>Загрузка произвольного PNG-спрайта (иконки/стикеры 32/24/48).</summary>
    public static bool TryLoadTexture(string resPath, out ImageTexture texture)
    {
        texture = LoadPngTexture(resPath);
        return texture != null;
    }

    /// <summary>QA: число попыток загрузки с момента старта (или ResetCache).</summary>
    public static int LoadAttemptCount => _loadAttempts;

    /// <summary>QA/ReAssembly: сброс кэшей (PNG-набор поменялся на диске).</summary>
    public static void ResetCache()
    {
        _sheets.Clear();
        _loadAttempts = 0;
    }

    // === Внутреннее ==========================================================

    private static string CategoryOf(string animId) => animId switch
    {
        var a when a.StartsWith("player_") => "characters/player",
        var a when a.StartsWith("npc_")    => "characters/npc",
        var a when a.StartsWith("animal_") => "animals",
        var a when a.StartsWith("corpse_") => "corpses",
        _ => "items", // qistone_*/pill_*/material_* — иконки предметов (G1)
    };

    private static SpriteSheet? LoadSheet(string resPath, string animId, int? fpsOverride = null, bool? loopOverride = null)
    {
        _loadAttempts++;
        var tex = LoadPngTexture(resPath);
        if (tex == null) return null;

        int w = tex.GetWidth(), h = tex.GetHeight();
        int rows = Mathf.Max(1, h / FramePx);
        if (h % FramePx != 0 || w % FramePx != 0 || w < FramePx)
        {
            GD.PrintErr($"[SpriteSheetCache] НЕКОРРЕКТНАЯ геометрия листа '{animId}' " +
                        $"({w}×{h}): размеры должны быть кратны {FramePx} (полоса 64N×64 / сетка 64N×64×R). " +
                        "Файл проигнорирован — рендер остаётся на процедурном fallback.");
            return null;
        }

        var spec = SpecOf(animId);
        int fps = fpsOverride ?? spec?.Fps ?? 10;
        bool loop = loopOverride ?? spec?.Loop ?? true;

        return new SpriteSheet
        {
            Texture = tex,
            FrameCount = w / FramePx,
            Rows = rows,
            FrameSize = FramePx,
            Fps = fps,
            Loop = loop,
            AnimId = animId,
            Path = resPath,
        };
    }

    /// <summary>
    /// PNG → ImageTexture без editor-импорта (FileAccess/ResourceLoader
    /// независим). Отсутствие файла → null (тихо — это штатный fallback).
    /// </summary>
    private static ImageTexture? LoadPngTexture(string resPath)
    {
        if (!FileAccess.FileExists(resPath)) return null;
        var image = Image.LoadFromFile(resPath);
        if (image == null || image.IsEmpty())
        {
            GD.PrintErr($"[SpriteSheetCache] PNG прочитан, но пуст/бит: {resPath}");
            return null;
        }
        return ImageTexture.CreateFromImage(image);
    }
}
