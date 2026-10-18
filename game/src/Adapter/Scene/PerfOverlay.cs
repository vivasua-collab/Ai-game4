#nullable enable
// R41-2 (репорт владельца 18.10 №6: «периодические падения до 40+ FPS на
// 5070Ti — нужна система оптимизации и анализа быстродействия»).
//
// Перф-оверлей — технический HUD для анализа:
//   • FPS (Engine) + ms/кадр: process/physics (Performance-мониторы);
//   • draw calls / objects in frame / CPU frame-setup (RenderingServer);
//   • живые сущности: NPC + звери (инъекция сервисов);
//   • ФАКТИЧЕСКИЕ редравы/с по каждому рендереру (RenderDiagnostics —
//     счётчики _Draw, не QueueRedraw-запросы) + глобальные QueueRedrawAll;
//   • кольцевой буфер 600 кадров (10с @ 60 Гц): min / median / 1% low;
//   • лог медленных кадров (>33 мс) в godot.log под env GODOT_PERF_LOG=1.
//
// Включение: клавиша F6 (InputMapInitializer: «F3/F4/F6/F8 свободны» —
// F3 занят FPS-счётчиком, F4 — тайл-инспектор R41-10) либо env
// GODOT_PERF_OVERLAY=1. Обновление 4 Гц с text-diff-гайтом (паттерн
// FPS-лейбла GWC) — оверлей сам себе не грузит кадр. QA-нейтрален:
// в headless-симах не активен (env не задан, F6 не жмётся).
using System;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class PerfOverlay : Control
{
    [Inject] private INPCService? _npcService;
    [Inject] private Modules.NPC.AnimalService? _animalService;

    private Label _label = null!;
    private float _updateAccum;      // 4 Гц обновление текста
    private float _statsAccum;       // 1 Гц статистика (редравы/с, персентили)
    private readonly float[] _frameTimes = new float[600]; // мс, кольцо 10с
    private int _frameCount;
    private int _frameIndex;
    private float _worstFrameMs;     // с последнего отчёта (для SLOW-лога)

    // Дельты редравов (1 Гц)
    private long _lastBiome, _lastTrans, _lastObj, _lastNpc, _lastAnimal, _lastCorpse, _lastRedrawAll;
    private int _redrawsBiome, _redrawsTrans, _redrawsObj, _redrawsNpc, _redrawsAnimal, _redrawsCorpse, _redrawsAll;

    private static bool PerfLogEnabled =>
        System.Environment.GetEnvironmentVariable("GODOT_PERF_LOG") == "1";
    private float _slowLogCooldown;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Позиция: под FPS-счётчиком (левый верх), не-модальный, не ловит мышь.
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        Position = new Vector2(8, 40);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 60;

        _label = new Label
        {
            Name = "PerfOverlayText",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.AddThemeFontSizeOverride("font_size", 11);
        _label.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 0.85f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("outline_size", 3);
        AddChild(_label);

        // Env-гейт: автозапуск для отладочных сессий владельца.
        Visible = System.Environment.GetEnvironmentVariable("GODOT_PERF_OVERLAY") == "1";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // F6 — тумблер оверлея (клавиша свободна, см. InputMapInitializer).
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F6 })
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Кольцевой буфер frame times (ms).
        _frameTimes[_frameIndex] = dt * 1000f;
        _frameIndex = (_frameIndex + 1) % _frameTimes.Length;
        if (_frameCount < _frameTimes.Length) _frameCount++;
        if (dt * 1000f > _worstFrameMs) _worstFrameMs = dt * 1000f;

        // Медленный кадр → godot.log (env GODOT_PERF_LOG=1, анти-спам 0.5с).
        if (PerfLogEnabled && dt * 1000f > 33f && _slowLogCooldown <= 0f)
        {
            _slowLogCooldown = 0.5f;
            GD.Print($"[PERF] SLOW {dt * 1000f:F1}ms | fps={Engine.GetFramesPerSecond():F0} | " +
                     $"draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0} | " +
                     $"npc={_npcService?.GetAllNPCIds().Count ?? -1} animals={_animalService?.GetAllAnimals().Count ?? -1} | " +
                     $"redraw/s: biome={_redrawsBiome} trans={_redrawsTrans} obj={_redrawsObj} " +
                     $"npc={_redrawsNpc} animal={_redrawsAnimal} corpse={_redrawsCorpse} all={_redrawsAll}");
        }
        _slowLogCooldown -= dt;

        // 1 Гц: статистика редравов + персентили.
        _statsAccum += dt;
        if (_statsAccum >= 1f)
        {
            _statsAccum = 0f;
            _redrawsBiome = (int)(RenderDiagnostics.BiomeDraws - _lastBiome); _lastBiome = RenderDiagnostics.BiomeDraws;
            _redrawsTrans = (int)(RenderDiagnostics.TransitionDraws - _lastTrans); _lastTrans = RenderDiagnostics.TransitionDraws;
            _redrawsObj = (int)(RenderDiagnostics.ObjectDraws - _lastObj); _lastObj = RenderDiagnostics.ObjectDraws;
            _redrawsNpc = (int)(RenderDiagnostics.NpcDraws - _lastNpc); _lastNpc = RenderDiagnostics.NpcDraws;
            _redrawsAnimal = (int)(RenderDiagnostics.AnimalDraws - _lastAnimal); _lastAnimal = RenderDiagnostics.AnimalDraws;
            _redrawsCorpse = (int)(RenderDiagnostics.CorpseDraws - _lastCorpse); _lastCorpse = RenderDiagnostics.CorpseDraws;
            _redrawsAll = (int)(RenderDiagnostics.QueueRedrawAllCalls - _lastRedrawAll); _lastRedrawAll = RenderDiagnostics.QueueRedrawAllCalls;
            _worstFrameMs = 0f;
        }

        // 4 Гц: обновление текста (только когда видно; text-diff экономит
        // перерисовку лейбла — паттерн FPS-счётчика GWC).
        if (!Visible) return;
        _updateAccum += dt;
        if (_updateAccum < 0.25f) return;
        _updateAccum = 0f;

        float fps = (float)Engine.GetFramesPerSecond();
        float procMs = (float)(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0);
        float physMs = (float)(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0);
        float draws = (float)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        float objects = (float)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        float setupMs = (float)RenderingServer.GetFrameSetupTimeCpu();
        int npc = _npcService?.GetAllNPCIds().Count ?? -1;
        int animals = _animalService?.GetAllAnimals().Count ?? -1;

        ComputeFramePercentiles(out float minMs, out float medMs, out float p99Ms);

        string text =
            $"PERF (F6) fps={fps:F0} | кадр {medMs:F1}мс (min {minMs:F1}, 1%low {p99Ms:F1})\n" +
            $"proc={procMs:F1}мс phys={physMs:F1}мс gpu-setup={setupMs:F2}мс | draws={draws:F0} objects={objects:F0}\n" +
            $"сущности: npc={npc} звери={animals} | тайлов в кадре={RenderDiagnostics.LastBiomeTilesDrawn}\n" +
            $"redraw/с: биом={_redrawsBiome} транз={_redrawsTrans} объекты={_redrawsObj}\n" +
            $"          npc={_redrawsNpc} звери={_redrawsAnimal} трупы={_redrawsCorpse} глобальные={_redrawsAll}";
        if (_label.Text != text)
            _label.Text = text;
    }

    /// <summary>min/median/1%-low времени кадра за окно кольца (мс).</summary>
    private void ComputeFramePercentiles(out float minMs, out float medianMs, out float p99Ms)
    {
        if (_frameCount == 0) { minMs = medianMs = p99Ms = 0f; return; }
        // Копия окна для сортировки (1/с на 600 элементов — копейки).
        var span = _frameTimes.AsSpan(0, _frameCount).ToArray();
        Array.Sort(span);
        minMs = span[0];
        medianMs = span[span.Length / 2];
        p99Ms = span[Math.Min(span.Length - 1, Math.Max(0, (int)(span.Length * 0.01f)))];
    }
}
