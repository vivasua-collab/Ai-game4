#nullable enable
// GAP-001 (P1, волна 1, 10-19-fix-b): экран смерти игрока.
//
// Прежнее поведение (аудит player-s5, bugs.json): смерть игрока → технический
// респавн через 3с в GameWorldController — игрок не понимает, что произошло
// и куда он возродился. Теперь: PlayerDeathEvent → ЭКРАН СМЕРТИ сразу
// (тёмная вуаль на весь экран + пергаментная панель с причиной гибели),
// респавн — ТОЛЬКО по кнопке «Возродиться» (старый 3с-таймер отключён).
//
// Механика:
//   • полный экран-вуаль (Control + ColorRect FullRect), MouseFilter=Stop —
//     клики в мир блокируются; ZIndex=50: над HUD и окнами (40), ниже
//     QA-панелей (60 — PerfOverlay/TileInspectorOverlay);
//   • GameWorldController.OnPlayerDeath вызывает Show(cause) — единая точка
//     смерти (GWC уже подписан на PlayerDeathEvent; дубль-подписки нет);
//   • причина — PlayerDeathEvent.Cause (часть тела: «Голова разрушена …»,
//     каскад R41 Head|Heart); источник последнего удара — кэш последнего
//     DamageAppliedEvent по игроку (имя NPC/зверя резолвится на показе);
//   • уровень культивации — IQiService.CultivationLevel (дёшево, из кэша);
//   • «Возродиться» → делегат _respawnAction, привязанный GWC к СУЩЕСТВУЮЩЕМУ
//     респавн-флоу (RequestPlayerRespawn: полное лечение частей + Revive +
//     телепорт в центр карты) — логика респавна НЕ дублируется в UI;
//   • PlayerReviveEvent (любой путь оживления) → защитный Hide()
//     (оверлей не может «залипнуть» над живым игроком).
//
// Паттерны: LootWindow (FullRect-вуаль + центральная Panel +
// ParchmentTheme, MouseFilter Stop/Ignore), TileInspectorOverlay
// (собственная DI-инъекция в _Ready, QA-акцессоры), PerfOverlay (DI).
// НЕ в стеке модальных окон INP-1: экран смерти не «окно планирования»,
// Esc его не закрывает (выход — только «Возродиться»).
using System;
using Godot;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using Contracts = CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// GAP-001: экран смерти игрока — вуаль + пергаментная панель с причиной
/// гибели и кнопкой «Возродиться». Показывает GameWorldController по
/// PlayerDeathEvent; кнопка запускает существующий респавн-флоу GWC.
/// </summary>
public partial class DeathScreenOverlay : Control
{
    // === DI (паттерн TileInspectorOverlay/LootWindow: своя инъекция) ===
    [Inject] private IPlayerService? _player;
    [Inject] private IQiService? _qi;
    [Inject] private INPCService? _npcs;
    [Inject] private Modules.NPC.AnimalService? _animals;
    [Inject] private ISubscriber<Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private ISubscriber<Contracts.PlayerReviveEvent>? _reviveSub;

    // === UI ===
    private ColorRect _veil = null!;
    private Label _causeLabel = null!;
    private Label _killerLabel = null!;
    private Label _metaLabel = null!;
    private Button _respawnButton = null!;

    // === Респавн-флоу GWC (привязка через делегат — без UI→Scene связи) ===
    private Action? _respawnAction;

    // === Кэш последнего удара по игроку (источник гибели) ===
    private string _lastAttackerId = string.Empty;

    private IDisposable? _damageToken;
    private IDisposable? _reviveToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Вуаль ловит мышь на весь экран, пока показана; ZIndex=50 — над
        // HUD и окнами (40), ниже QA-панелей (60).
        ZIndex = 50;
        BuildUI();

        // Скрыт по умолчанию — QA-нейтрален (симы без смерти его не видят).
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        // Последний удар по игроку → источник гибели (имя на экране смерти).
        _damageToken = _damageSub?.Subscribe(OnDamageApplied);
        // Любой путь оживления (не только кнопка) → защитное скрытие.
        _reviveToken = _reviveSub?.Subscribe(OnPlayerRevived);
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _reviveToken?.Dispose();
        _damageToken = null;
        _reviveToken = null;
    }

    // === Layout (паттерн LootWindow: Theme пергамента + центральная панель) ===

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();

        // Полноэкранная тёмная вуаль — блокирует клики в мир.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _veil = new ColorRect
        {
            Name = "Veil",
            Color = new Color(0.03f, 0.02f, 0.02f, 0.8f),
        };
        _veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _veil.MouseFilter = MouseFilterEnum.Stop; // клик по вуали НЕ закрывает экран
        AddChild(_veil);

        // Центральная пергаментная панель 560×340.
        var panel = new Panel { Name = "DeathPanel" };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -280;
        panel.OffsetRight = 280;
        panel.OffsetTop = -170;
        panel.OffsetBottom = 170;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 20;
        outer.OffsetRight = -20;
        outer.OffsetTop = 16;
        outer.OffsetBottom = -16;
        outer.AddThemeConstantOverride("separation", 8);
        panel.AddChild(outer);

        var title = new Label
        {
            Name = "Title",
            Text = "☠ Вы пали…",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", ParchmentTheme.AccentRed);
        outer.AddChild(title);

        var subtitle = new Label
        {
            Name = "Subtitle",
            Text = "Тело разрушено — путь прерван",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        subtitle.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(subtitle);

        outer.AddChild(new HSeparator());

        _causeLabel = new Label
        {
            Name = "Cause",
            Text = "Причина неизвестна",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _causeLabel.AddThemeFontSizeOverride("font_size", 17);
        _causeLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_causeLabel);

        _killerLabel = new Label
        {
            Name = "Killer",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _killerLabel.AddThemeFontSizeOverride("font_size", 14);
        _killerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_killerLabel);

        _metaLabel = new Label
        {
            Name = "Meta",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _metaLabel.AddThemeFontSizeOverride("font_size", 13);
        _metaLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_metaLabel);

        // Кнопка «Возродиться» — единственный выход с экрана.
        var buttons = new HBoxContainer
        {
            Name = "ButtonsRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        buttons.AddThemeConstantOverride("separation", 12);
        outer.AddChild(buttons);

        _respawnButton = new Button
        {
            Name = "RespawnButton",
            Text = "Возродиться",
        };
        _respawnButton.AddThemeFontSizeOverride("font_size", 18);
        _respawnButton.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);
        _respawnButton.Pressed += OnRespawnPressed;
        buttons.AddChild(_respawnButton);

        var footer = new Label
        {
            Name = "Footer",
            Text = "«Возродиться» восстановит тело и вернёт вас в центр земель",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(footer);
    }

    // === Показ/скрытие ===

    /// <summary>
    /// Показать экран смерти. Вызывает GameWorldController.OnPlayerDeath
    /// (единый подписчик PlayerDeathEvent) с причиной из события.
    /// </summary>
    public void Show(string cause)
    {
        _causeLabel.Text = string.IsNullOrEmpty(cause) ? "Причина неизвестна" : cause;
        _killerLabel.Text = BuildKillerLine();
        _metaLabel.Text = BuildMetaLine();

        Visible = true;
        MouseFilter = MouseFilterEnum.Stop; // вуаль ест клики в мир
        _respawnButton.GrabFocus();          // Enter = «Возродиться»
        GD.Print($"[DeathScreen] Показ: причина='{_causeLabel.Text}', убийца='{_killerLabel.Text}'");
    }

    private void HideScreen()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    // === Кнопка «Возродиться»: СУЩЕСТВУЮЩИЙ респавн-флоу GameWorldController ===

    /// <summary>
    /// GWC привязывает сюда свой RequestPlayerRespawn (полное лечение +
    /// Revive + телепорт в центр) — логика респавна живёт в одном месте.
    /// </summary>
    public void BindRespawnAction(Action respawnAction)
    {
        _respawnAction = respawnAction;
    }

    private void OnRespawnPressed()
    {
        if (_respawnAction == null)
        {
            GD.PrintErr("[DeathScreen] Респавн-делегат не привязан (GameWorldController не вызвал BindRespawnAction)");
        }
        else
        {
            _respawnAction.Invoke();
        }
        HideScreen();
        _lastAttackerId = string.Empty;
        GD.Print("[DeathScreen] «Возродиться» нажата — оверлей скрыт");
    }

    // === Подписки ===

    /// <summary>Кэш последнего удара по игроку → источник гибели.</summary>
    private void OnDamageApplied(in Contracts.DamageAppliedEvent e)
    {
        if (_player == null) return;
        if (PlayerIdResolver.AreSameEntity(e.TargetId, _player.PlayerId))
            _lastAttackerId = e.SourceId ?? string.Empty;
    }

    /// <summary>Защита: игрок оживлён любым путём → экран обязан скрыться.</summary>
    private void OnPlayerRevived(in Contracts.PlayerReviveEvent e)
    {
        if (Visible) HideScreen();
    }

    // === Строки панели ===

    /// <summary>«Последний удар: Имя» — имя NPC/зверя по кэшу атакующего.</summary>
    private string BuildKillerLine()
    {
        if (string.IsNullOrEmpty(_lastAttackerId))
            return "Последний удар: неведомая сила";

        if (PlayerIdResolver.IsPlayer(_lastAttackerId))
            return "Последний удар: собственное рассеяние";

        string? name = _npcs?.GetNPCState(_lastAttackerId)?.DisplayName
                      ?? _animals?.GetDisplayName(_lastAttackerId);
        return $"Последний удар: {name ?? "неведомая сила"}";
    }

    /// <summary>Уровень культивации (дёшево — кэш IQiService). Время выживания
    /// не пишем: метрики жизни/смерти в домене нет (честное умолчание).</summary>
    private string BuildMetaLine()
    {
        int level = _qi != null ? Math.Max(1, (int)_qi.CultivationLevel) : 1;
        return $"Уровень культивации: L{level}";
    }

    // === QA-акцессоры (RespawnSimDebug шаг 4, паттерн TileInspectorOverlay) ===

    /// <summary>Причина гибели, как она показана (QA: текст из события).</summary>
    internal string DebugCauseText => _causeLabel?.Text ?? "";

    /// <summary>Строка об источнике удара (QA).</summary>
    internal string DebugKillerText => _killerLabel?.Text ?? "";

    /// <summary>Экран блокирует мышь (QA: MouseFilter=Stop на вуали).</summary>
    internal bool BlocksInput => Visible && MouseFilter == MouseFilterEnum.Stop;

    /// <summary>
    /// QA: симуляция нажатия «Возродиться» без GUI (headless) — честный
    /// сигнал кнопки (тот же Pressed-handler, что и у реального клика).
    /// </summary>
    internal void SimulateRespawnPressForQa()
    {
        _respawnButton.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
