#nullable enable
// Создано: 2026-09-04 — S1 (директива пользователя: информативность UI).
// EventLogWindow — окно журнала событий (клавиша J, рекламировалась в легенде
// HUD и F1-справке с 2026-08-22, но не существовала — «мёртвая проводка»).
//
// Фиксируем ключевые события мира с точки зрения ИГРОКА (анти-спам:
// npc-vs-npc бой НЕ логируется):
//   • Бой: начало, удар игрока, урон игроку, победа/поражение, смерть врага
//   • Мир: добыча ресурса, подбор предмета, культивация (уровень)
//
// Паттерн: HotkeysWindow (модальное окно, пауза из GameWorldController).
// Ring-buffer на 60 записей; при открытии — прокрутка вниз.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Журнал событий (J): последние 60 событий боя/мира с метками игрового
/// времени. Информативность: игрок может пост-анализировать бой.
/// </summary>
public partial class EventLogWindow : Control
{
    private const int MaxEntries = 60;

    [Inject] private readonly INPCService? _npcService = null;
    [Inject] private readonly ITimeService? _timeService = null;
    [Inject] private readonly ISubscriber<CombatStartedEvent> _combatStartedSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageSub = null!;
    [Inject] private readonly ISubscriber<EnemyKilledEvent> _enemyKilledSub = null!;
    [Inject] private readonly ISubscriber<ResourceHarvestedEvent> _harvestSub = null!;
    [Inject] private readonly ISubscriber<ItemPickedUpEvent> _pickupSub = null!;
    [Inject] private readonly ISubscriber<CultivationLevelChangedEvent> _levelSub = null!;
    // 2026-09-04 S3: kill-feed физического боя (NPCCombatAdapter → NPCDeathEvent);
    // EnemyKilledEvent покрывает только stage-бой (CombatService поединки).
    [Inject] private readonly ISubscriber<NPCDeathEvent> _npcDeathSub = null!;

    private readonly List<(string Time, string Text, Godot.Color Colour)> _entries = new();
    private VBoxContainer? _list;
    private ScrollContainer? _scroll;
    private Label? _countLabel;
    private System.IDisposable? _t1, _t2, _t3, _t4, _t5, _t6, _t7, _t8;

    /// <summary>2026-09-04 S3: QA-доступ (GODOT_KILLFEED_DEBUG) — последний лог.</summary>
    public string? LastEntryText => _entries is { Count: > 0 } ? _entries[^1].Text : null;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();
        Visible = false;

        _t1 = _combatStartedSub?.Subscribe(OnCombatStarted);
        _t2 = _combatEndedSub?.Subscribe(OnCombatEnded);
        _t3 = _damageSub?.Subscribe(OnDamage);
        _t4 = _enemyKilledSub?.Subscribe(OnEnemyKilled);
        _t5 = _harvestSub?.Subscribe(OnHarvest);
        _t6 = _pickupSub?.Subscribe(OnPickup);
        _t7 = _levelSub?.Subscribe(OnLevelChanged);
        // 2026-09-04 S3: kill-feed физического боя.
        _t8 = _npcDeathSub?.Subscribe(OnNpcDeathFeed);

        GD.Print("[EventLogWindow] Ready");
    }

    public override void _ExitTree()
    {
        _t1?.Dispose(); _t2?.Dispose(); _t3?.Dispose(); _t4?.Dispose();
        _t5?.Dispose(); _t6?.Dispose(); _t7?.Dispose(); _t8?.Dispose();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible) ScrollToBottom();
    }

    // === Event handlers (анти-спам: только события с игроком) ===

    private void OnCombatStarted(in CombatStartedEvent e)
    {
        if (!InvolvesPlayer(e.InstigatorId, e.TargetId)) return;
        Add($"⚔ Бой начался: {Name(e.InstigatorId)} против {Name(e.TargetId)}",
            new Godot.Color(0.9f, 0.55f, 0.3f));
    }

    private void OnCombatEnded(in CombatEndedEvent e)
    {
        if (!InvolvesPlayer(e.WinnerId, e.LoserId)) return;
        bool playerWon = IsPlayer(e.WinnerId);
        Add(playerWon
                ? $"☠ Победа над {Name(e.LoserId)}"
                : $"☠ Поражение от {Name(e.WinnerId)}",
            playerWon
                ? new Godot.Color(0.55f, 0.85f, 0.4f)
                : new Godot.Color(0.9f, 0.3f, 0.25f));
    }

    private void OnDamage(in DamageAppliedEvent e)
    {
        if (!InvolvesPlayer(e.SourceId, e.TargetId)) return;
        if (IsPlayer(e.SourceId))
            Add($"🗡 Вы нанесли {e.Damage} урона → {Name(e.TargetId)}" +
                (e.Result != CombatAttackResult.Hit ? $" ({RuResult(e.Result)})" : ""),
                new Godot.Color(0.85f, 0.75f, 0.45f));
        else
            Add($"💥 {Name(e.SourceId)} → вам {e.Damage} урона ({RuPart(e.HitPart)})",
                new Godot.Color(0.9f, 0.4f, 0.3f));
    }

    private void OnEnemyKilled(in EnemyKilledEvent e)
    {
        Add($"☠ {Name(e.EnemyId)} повержен", new Godot.Color(0.6f, 0.8f, 0.35f));
    }

    /// <summary>
    /// 2026-09-04 S3: kill-feed физического боя — NPCDeathEvent.
    /// Дедуп: то же событие может прийти и через stage-бой (EnemyKilledEvent)
    /// в тот же момент — пропускаем, если этот NPC уже залогирован < 2с назад.
    /// </summary>
    private void OnNpcDeathFeed(in NPCDeathEvent e)
    {
        string name = Name(e.NpcId);
        // Дедуп по последней записи (тот же NPC + слово «повержен»).
        var now = System.DateTime.UtcNow;
        if (_lastKillLog is { } last && last.Name == name &&
            (now - last.At).TotalSeconds < 2.0)
            return;
        _lastKillLog = (name, now);

        if (e.KillerId is ("player_0" or "player"))
            Add($"☠ {name} повержен (руками)", new Godot.Color(0.55f, 0.85f, 0.4f));
        else if (e.KillerId == "old_age")
            Add($"✝ {name} ушёл из мира (старость)", new Godot.Color(0.7f, 0.65f, 0.55f));
        else
        {
            string killer = Name(e.KillerId);
            Add($"☠ {name} погиб ({(killer != "???" ? killer : "причина неизвестна")})",
                new Godot.Color(0.7f, 0.5f, 0.3f));
        }
    }

    private (string Name, System.DateTime At)? _lastKillLog;

    private void OnHarvest(in ResourceHarvestedEvent e)
    {
        Add($"🌿 Добыто: {RuItem(e.ItemId)} ×{e.Amount}", new Godot.Color(0.5f, 0.75f, 0.5f));
    }

    private void OnPickup(in ItemPickedUpEvent e)
    {
        Add($"🎁 Подобрано: {RuItem(e.ItemId)} ×{e.Count}", new Godot.Color(0.55f, 0.65f, 0.85f));
    }

    private void OnLevelChanged(in CultivationLevelChangedEvent e)
    {
        Add($"✦ Прорыв: новый уровень культивации L{(int)e.NewLevel}",
            new Godot.Color(0.95f, 0.8f, 0.3f));
    }

    // === Helpers ===

    private static bool IsPlayer(string id) => id == "player" || id == "player_0";

    private bool InvolvesPlayer(string a, string b) => IsPlayer(a) || IsPlayer(b);

    private string Name(string entityId)
    {
        if (IsPlayer(entityId)) return "Вы";
        var st = _npcService?.GetNPCState(entityId);
        return st?.DisplayName ?? "???";
    }

    private static string RuItem(string itemId)
    {
        // item id вида "consumable_1_887" → «Лекарство L1» не резолвим без БД —
        // показываем компактный id без префиксов.
        return itemId;
    }

    private static string RuResult(CombatAttackResult r) => r switch
    {
        CombatAttackResult.Miss   => "промах",
        CombatAttackResult.Dodge  => "уклонение",
        CombatAttackResult.Parry  => "парирование",
        CombatAttackResult.Block  => "блок",
        CombatAttackResult.CriticalHit => "КРИТ",
        CombatAttackResult.Kill => "добивание",
        _ => r.ToString(),
    };

    private static string RuPart(BodyPartType p) => p switch
    {
        BodyPartType.Head     => "голова",
        BodyPartType.Torso    => "торс",
        BodyPartType.LeftArm  => "лев. рука",
        BodyPartType.RightArm => "прав. рука",
        BodyPartType.LeftLeg  => "лев. нога",
        BodyPartType.RightLeg => "прав. нога",
        _ => p.ToString(),
    };

    private void Add(string text, Godot.Color colour)
    {
        string time = "";
        if (_timeService?.CurrentTime is { } t)
            time = $"{t.Hour:D2}:{t.Minute:D2}";

        _entries.Add((time, text, colour));
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);

        if (Visible || _list != null)
            AppendRow(_entries[^1]);

        if (_countLabel != null)
            _countLabel.Text = $"{_entries.Count}/{MaxEntries}";
    }

    // === Construction ===

    private void BuildUI()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Name = "Background", Color = new Godot.Color(0.05f, 0.03f, 0.02f, 0.78f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var panel = new Panel { Name = "EventLogPanel" };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -360; panel.OffsetRight = 360;
        panel.OffsetTop = -280; panel.OffsetBottom = 280;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var style = new StyleBoxFlat { BgColor = new Godot.Color(0.10f, 0.08f, 0.05f, 0.98f) };
        style.SetBorderWidthAll(2);
        style.SetBorderColor(new Godot.Color(0.55f, 0.42f, 0.20f, 0.9f));
        style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 12; root.OffsetRight = -12;
        root.OffsetTop = 10; root.OffsetBottom = -10;
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        // Header.
        var header = new HBoxContainer();
        var title = new Label { Text = "◆  Журнал событий  ◆" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Godot.Color(0.95f, 0.80f, 0.45f));
        header.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        _countLabel = new Label { Text = $"0/{MaxEntries}" };
        _countLabel.AddThemeFontSizeOverride("font_size", 12);
        _countLabel.AddThemeColorOverride("font_color", new Godot.Color(0.7f, 0.6f, 0.45f));
        _countLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_countLabel);

        var closeBtn = new Button { Text = "×" };
        closeBtn.Pressed += () => Visible = false;
        header.AddChild(closeBtn);
        root.AddChild(header);

        // Scrollable list.
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(_scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 2);
        _scroll.AddChild(_list);

        var hint = new Label { Text = "J / Esc — закрыть · фиксы: бой, добыча, подбор, прорывы" };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Godot.Color(0.6f, 0.55f, 0.45f));
        root.AddChild(hint);
    }

    private void AppendRow((string Time, string Text, Godot.Color Colour) entry)
    {
        if (_list == null) return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var time = new Label
        {
            Text = entry.Time,
            CustomMinimumSize = new Vector2(44, 18),
        };
        time.AddThemeFontSizeOverride("font_size", 11);
        time.AddThemeColorOverride("font_color", new Godot.Color(0.55f, 0.5f, 0.4f));
        row.AddChild(time);

        var text = new Label
        {
            Text = entry.Text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        text.AddThemeFontSizeOverride("font_size", 13);
        text.AddThemeColorOverride("font_color", entry.Colour);
        row.AddChild(text);

        _list.AddChild(row);

        // Ring-buffer GUI: убираем старейшие строки (детей списка).
        while (_list.GetChildCount() > MaxEntries)
            _list.GetChild(0).QueueFree();

        if (Visible)
            ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        // Deferred: layout должен пересчитаться до прокрутки.
        CallDeferred(nameof(DeferredScroll));
    }

    private void DeferredScroll()
    {
        if (_scroll != null && _list != null)
            _scroll.ScrollVertical = (int)_list.GetMinimumSize().Y;
    }
}
