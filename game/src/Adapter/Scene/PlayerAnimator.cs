#nullable enable
// Создано: 2026-09-25 — G0 «Подготовка спрайтов»: I-1 плана
// SPRITE_ANIMATION_PLAN §11 — аниматор игрока (sprite-swap).
//
// Паттерн — PlayerSprite._Process эталонного 2.5D-демо: Sprite2D
// Hframes/Vframes/Frame, прокрутка в физ-тике (GWC._PhysicsProcess
// 60 Гц — все состояния уже под рукой: moveVec/facing/медитация/замах).
//
// Приоритет состояний (§5.1): death > melee > bow > hit > meditate >
// run/walk (движение) > idle. one-shot: melee/bow/hit/death (death —
// hold последнего кадра до NotifyRespawn).
//
// Fallback-контракт: листа нет → процедурная текстура 48×48 КАК СЕЙЧАС
// (нулевая визуальная регрессия до доставки PNG; Sprite2D.Texture
// переключается только когда лист реально резолвился).
//
// Скорость прокрутки: walk/run × speedMult (gameSpeed ×2/×3.5, Shift-бег,
// весовые штрафы — движение и анимация синхронны); one-shot — реальное
// время (замах кода MainHandSwingSec=0.42с тоже тикает в реальном времени).
using Godot;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// G0: покадровый аниматор игрока. Владелец — GameWorldController
/// (создаётся в SetupWorld после _playerSprite; НЕ Node — DI не нужен).
/// </summary>
public sealed class PlayerAnimator
{
    private readonly Sprite2D _sprite;
    private readonly Texture2D _proceduralTex;

    // Текущее состояние.
    private string _animId = "player_idle";
    private SpriteSheetCache.SpriteSheet? _sheet;
    private float _progress;          // 0..1 — позиция в цикле
    private float _phase;             // непрерывная фаза loop-анимаций

    // PT3-002 (плейтест-2-рестарт): последний ДОСТАВЛЕННЫЙ PNG-лист. Когда
    // целевой one-shot лист не доставлен (сейчас player_bow G4 — владелец
    // упёрся в лимит генерации), НЕ деградируем спрайт до процедурной
    // текстуры 48×48: PNG-кадры 64×64 с силуэтом 52px — подмена на 48px
    // выглядела как «персонаж усох на выстреле» (репорт владельца). Держим
    // последний хороший лист (idle/walk) — персонаж сохраняет рост, поза
    // просто не меняется до доставки bow-листа.
    private SpriteSheetCache.SpriteSheet? _lastGoodSheet;

    // one-shot-таймеры (реальное время; <0 — неактивны).
    private float _meleeAge = -1f;
    private float _rangedAge = -1f;
    private float _hitAge = -1f;
    private bool _deathActive;        // смерть: death-лист до NotifyRespawn

    // QA-оверлей: подменённый лист (GODOT_ANIMQA_DEBUG) — проверка цепочки
    // кадровой индексации без доставки реальных PNG.
    private SpriteSheetCache.SpriteSheet? _debugOverride;

    /// <param name="sprite">PlayerSprite (Sprite2D, ZIndex=Player).</param>
    /// <param name="proceduralTex">Процедурная текстура 48×48 (fallback).</param>
    public PlayerAnimator(Sprite2D sprite, Texture2D proceduralTex)
    {
        _sprite = sprite;
        _proceduralTex = proceduralTex;
        ApplyTexture();
    }

    // === Состояние для кадра (заполняет GWC._PhysicsProcess) ==============

    /// <summary>Вход аниматора: снимок состояния игрока на кадр.</summary>
    public readonly struct FrameState
    {
        public readonly bool Moving;       // moveVec≠0 или мышиная цель
        public readonly bool Running;      // Shift (×1.8)
        public readonly float SpeedMult;   // gameSpeed × бег × штрафы (walk/run)
        public readonly bool Meditation;   // MeditationStateChangedEvent.IsActive
        public static readonly FrameState Default = new(false, false, 1f, false);

        public FrameState(bool moving, bool running, float speedMult, bool meditation)
        {
            Moving = moving; Running = running; SpeedMult = speedMult; Meditation = meditation;
        }
    }

    /// <summary>Тик аниматора (каждый физ-кадр). Смена анимации — редкое
    /// событие; кадр — дешёвый int-индекс (риск §12-7 закрыт).</summary>
    public void Update(double delta, in FrameState state)
    {
        // 1) one-shot-таймеры (синхронно с замахом оружия — реальное время).
        if (_meleeAge >= 0f)
        {
            _meleeAge += (float)delta;
            if (_meleeAge >= MeleeSec) _meleeAge = -1f;
        }
        if (_rangedAge >= 0f)
        {
            _rangedAge += (float)delta;
            if (_rangedAge >= BowSec) _rangedAge = -1f;
        }
        if (_hitAge >= 0f)
        {
            _hitAge += (float)delta;
            if (_hitAge >= HitSec) _hitAge = -1f;
        }

        // 2) Выбор анимации по приоритету состояний.
        string next = ResolveAnimId(state);
        if (next != _animId
            || (_sheet == null && SheetFor(next) != null)) // PNG «подвезли» в рантайме
        {
            SetAnim(next);
        }

        // 3) Прокрутка кадров.
        var sheet = ActiveSheet;
        if (sheet == null) return; // процедурная статика: Frame=0

        if (_animId is "player_walk" or "player_run")
        {
            // Синхронно с движением: ×gameSpeed/бег/штрафы, клэмп от
            // стробоскопа и слоу-мо (0.25..4).
            float fps = sheet.Fps * Godot.Mathf.Clamp(state.SpeedMult, 0.25f, 4f);
            _phase += fps * (float)delta;
            _progress = (_phase / sheet.FrameCount) % 1f;
        }
        else if (sheet.Loop)
        {
            _phase += sheet.Fps * (float)delta;
            _progress = (_phase / sheet.FrameCount) % 1f;
        }
        else
        {
            // one-shot: прогресс от возраста таймера; death — свободная
            // прокрутка до 1 с hold.
            if (_animId == "player_death")
            {
                _progress = Godot.Mathf.Min(
                    _progress + sheet.Fps * (float)delta / sheet.FrameCount, 1f);
            }
            else
            {
                _progress = _animId switch
                {
                    "player_melee" => _meleeAge >= 0 ? _meleeAge / MeleeSec : 1f,
                    "player_bow"   => _rangedAge >= 0 ? _rangedAge / BowSec : 1f,
                    "player_hit"   => _hitAge >= 0 ? _hitAge / HitSec : 1f,
                    _ => 1f,
                };
            }
        }

        int frame = sheet.FrameAt(_progress, Row);
        if (_sprite.Frame != frame) _sprite.Frame = frame;
    }

    /// <summary>
    /// Ряд D2-сетки (0..4: front/front-¾/side/back-¾/back). D1: бинарный
    /// facing → ряд «side»=2 (канон §4.2 — все D1-листы лягут рядом side;
    /// октантный facing — отдельная задача I-3 плана).
    /// </summary>
    public int Row => 2;

    // === Нотификации (GWC из обработчиков событий) ==========================

    /// <summary>AttackIntentEvent (melee, игрок) — one-shot ≈ замах 0.42с.</summary>
    public void NotifyMeleeAttack() => _meleeAge = 0f;

    /// <summary>AttackIntentEvent (ranged, игрок) — player_bow (G4).</summary>
    public void NotifyRangedAttack() => _rangedAge = 0f;

    /// <summary>DamageAppliedEvent (цель — игрок) — player_hit.</summary>
    public void NotifyHit() => _hitAge = 0f;

    /// <summary>PlayerDeathEvent — death до респавна (hold последнего кадра).</summary>
    public void NotifyDeath()
    {
        _deathActive = true;
        // ST16-001 (Этап-16 аудита): смерть прерывает ЛЮБОЙ активный one-shot —
        // после респавна старая атака/хит не доигрывается (таймеры не наследуют
        // жизнь). Раньше _meleeAge/_rangedAge/_hitAge выживали death→respawn и
        // ResolveAnimId доигрывал прежний замах на живом игроке (префикс:
        // melee→death→respawn → AnimId='player_melee').
        _meleeAge = -1f;
        _rangedAge = -1f;
        _hitAge = -1f;
        _progress = 0f;
        _phase = 0f;
        SetAnim("player_death");
    }

    /// <summary>Респавн — возврат к живым состояниям.</summary>
    public void NotifyRespawn()
    {
        // ST16-001: новая жизнь — чистый лист one-shot'ов (симметрично смерти;
        // страховка от путей респавна, минующих NotifyDeath).
        _meleeAge = -1f;
        _rangedAge = -1f;
        _hitAge = -1f;
        _deathActive = false;
        _progress = 0f;
        _phase = 0f;
        SetAnim("player_idle");
    }

    // === QA-поверхность (GODOT_ANIMQA_DEBUG) ================================

    /// <summary>QA: подменить лист текущей анимации (проверка Frame-индексации).</summary>
    public void DEBUG_SetSheetOverride(SpriteSheetCache.SpriteSheet? sheet)
    {
        _debugOverride = sheet;
        SetAnim(_animId);
    }

    /// <summary>QA: сброс таймеров/состояния.</summary>
    public void DEBUG_Reset()
    {
        _meleeAge = _rangedAge = _hitAge = -1f;
        _deathActive = false;
        _progress = 0f;
        _phase = 0f;
        SetAnim("player_idle");
    }

    /// <summary>QA: текущий animId.</summary>
    public string AnimId => _animId;

    /// <summary>QA: кадр (Frame; без листа — 0).</summary>
    public int Frame => (_debugOverride ?? _sheet) != null ? _sprite.Frame : 0;

    /// <summary>QA: лист загружен из PNG (false — процедурный fallback).</summary>
    public bool IsPng => ActiveSheet != null;

    /// <summary>QA: кадров в активном листе (0 — fallback).</summary>
    public int FrameCount => ActiveSheet?.FrameCount ?? 0;

    // === Внутреннее ==========================================================

    // Длительности one-shot — фиксированы (синхрон с кодовыми таймерами
    // боя), НЕ зависят от числа кадров доставленного листа.
    private const float MeleeSec = 0.42f; // = GWC.MainHandSwingSec
    private const float BowSec = 0.25f;   // 3 кадра @12 (§5.1)
    private const float HitSec = 0.17f;   // 2 кадра @12

    private SpriteSheetCache.SpriteSheet? ActiveSheet => _debugOverride ?? _sheet;

    private string ResolveAnimId(in FrameState state)
    {
        if (_deathActive) return "player_death";
        if (_meleeAge >= 0f) return "player_melee";
        if (_rangedAge >= 0f) return "player_bow";
        if (_hitAge >= 0f) return "player_hit";
        if (state.Meditation) return "player_meditate";
        if (state.Moving) return state.Running ? "player_run" : "player_walk";
        return "player_idle";
    }

    /// <summary>Смена анимации: резолв листа (PNG → fallback) + Hframes.</summary>
    private void SetAnim(string animId)
    {
        _animId = animId;
        _sheet = SheetFor(animId);
        _progress = 0f;
        _phase = 0f;
        ApplyTexture();
    }

    private SpriteSheetCache.SpriteSheet? SheetFor(string animId) =>
        SpriteSheetCache.TryGetSheet(animId);

    /// <summary>Применить текстуру/Hframes/Vframes к Sprite2D.</summary>
    private void ApplyTexture()
    {
        var sheet = ActiveSheet;
        if (sheet != null)
        {
            _lastGoodSheet = sheet;
            _sprite.Texture = sheet.Texture;
            _sprite.Hframes = sheet.FrameCount;
            _sprite.Vframes = sheet.Rows;
        }
        else if (_lastGoodSheet != null)
        {
            // PT3-002: лист целевой анимации не доставлен — держим последний
            // PNG (без деградации до 48×48); кадр статичен (Frame=0).
            _sprite.Texture = _lastGoodSheet.Texture;
            _sprite.Hframes = _lastGoodSheet.FrameCount;
            _sprite.Vframes = _lastGoodSheet.Rows;
        }
        else
        {
            // Контракт G0 «нулевая регрессия до доставки PNG» нетронут:
            // если НИ ОДИН лист не доставлен — процедурная статика КАК СЕЙЧАС.
            _sprite.Texture = _proceduralTex;
            _sprite.Hframes = 1;
            _sprite.Vframes = 1;
        }
        _sprite.Frame = 0;
    }
}
