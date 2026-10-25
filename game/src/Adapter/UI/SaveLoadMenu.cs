#nullable enable
// Создано: 2026-10-20 — Этап-3 (директива пользователя: «начнём трогать
// сохранение/загрузку — реализовать меню загрузки и сохранения»).
//
// Переиспользуемое меню слотов сейвов для ДВУХ контекстов:
//   • Full — в игре (F5/F9 в GameWorldController): список слотов + запись
//     в именованный слот + загрузка + удаление;
//   • LoadOnly — в главном меню («Загрузить мир»): только загрузка/удаление
//     (сохранять нечего — сессии в MainMenu нет).
//
// Слой — Adapter/UI (Godot-нода), UI Builder pattern (UI_DESIGN §1.5):
// строится программно, стиль — тёмная панель в тон окнам справы/настроек.
// DI: [Inject] ISaveService — запись/удаление/перечисление слотов.
// Загрузку меню НЕ выполняет само: LoadRequested-событие отдаёт слот хосту
// (GWC: GameSession.LoadGame + пересборка сцены; MainMenu: LoadGame +
// EnterWorldIfSessionReady) — мир собирает сессия, не UI-слой.
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.UI;

/// <summary>Режим меню сейвов.</summary>
public enum SaveLoadMenuMode
{
    /// <summary>В игре: запись + загрузка + удаление.</summary>
    Full,
    /// <summary>Главное меню: загрузка + удаление (запись недоступна).</summary>
    LoadOnly,
}

public sealed partial class SaveLoadMenu : Control
{
    [Inject] private ISaveService SaveService { get; set; } = null!;

    /// <summary>Хост выполняет загрузку (смена сессии/сцены — не зона UI-слоя).</summary>
    public event Action<string>? LoadRequested;

    /// <summary>Закрытие окна (Esc/кнопка/клик по фону) — хост резюмит паузу (INP-1).</summary>
    public event Action? Closed;

    private SaveLoadMenuMode _mode = SaveLoadMenuMode.Full;
    private VBoxContainer _slotListBox = null!;
    private ScrollContainer _slotScroll = null!;
    private Label _emptyHint = null!;
    private LineEdit _newSlotEdit = null!;
    private Button _saveBtn = null!;
    private Button _loadBtn = null!;
    private Button _deleteBtn = null!;
    private Label _statusLabel = null!;
    private readonly Dictionary<string, Button> _slotButtons = new();

    /// <summary>Выбранный слот (null — ничего не выбрано).</summary>
    private string? _selectedSlot;

    private const string TitleFull = "СИЯНЬ КНИГА — СОХРАНЕНИЕ И ЗАГРУЗКА";
    private const string TitleLoadOnly = "СИЯНЬ КНИГА — ЗАГРУЗКА МИРА";

    public override void _Ready()
    {
        // DI — из глобального контейнера GameBoot (паттерн MainMenuController).
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);
        else
            GD.PushWarning("[SaveLoadMenu] GameBoot.Container is null — DI not wired");
        BuildUI();
        Visible = false;
    }

    // ──────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Открыть меню в заданном режиме (обновляет список слотов).</summary>
    public void Open(SaveLoadMenuMode mode)
    {
        _mode = mode;
        _saveBtn.Visible = mode == SaveLoadMenuMode.Full;
        _newSlotEdit.Visible = mode == SaveLoadMenuMode.Full;
        RefreshSlots();
        SetTitle(mode == SaveLoadMenuMode.Full ? TitleFull : TitleLoadOnly);
        Visible = true;
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Closed?.Invoke();
    }

    public void Toggle(SaveLoadMenuMode mode)
    {
        if (Visible) Close();
        else Open(mode);
    }

    /// <summary>
    /// Показать в статусе меню ошибку загрузки, сообщённую ХОСТОМ
    /// (сессия вернула не-Playing — причина у GameSession/SaveService,
    /// UI-слой её не знает). Вызывается хостом из LoadRequested-колбэка.
    /// </summary>
    public void ShowLoadError(string message)
    {
        ShowStatus($"✖ {message}", error: true);
    }

    // ──────────────────────────────────────────────────────────────────
    // UI construction
    // ──────────────────────────────────────────────────────────────────

    private Label _titleLabel = null!;

    private void BuildUI()
    {
        Name = "SaveLoadMenu";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // перехват кликов: модальное окно

        // Затемнение фона — как у окна настроек.
        var bg = new ColorRect { Name = "Dim", Color = new Color(0, 0, 0, 0.55f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        // Клик по затемнению = закрыть (тот же жест, что у прочих окон).
        bg.GuiInput += evt =>
        {
            if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                Close();
        };
        AddChild(bg);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { Name = "Panel" };
        var panelStyle = new StyleBoxFlat { BgColor = new Color(0.10f, 0.08f, 0.05f, 0.98f) };
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetBorderColor(new Color(0.55f, 0.42f, 0.20f, 0.9f));
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.ContentMarginLeft = 20;
        panelStyle.ContentMarginRight = 20;
        panelStyle.ContentMarginTop = 14;
        panelStyle.ContentMarginBottom = 14;
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var box = new VBoxContainer { Name = "Box" };
        box.AddThemeConstantOverride("separation", 10);
        box.CustomMinimumSize = new Vector2(460, 0);
        panel.AddChild(box);

        _titleLabel = new Label
        {
            Text = TitleFull,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.72f));
        box.AddChild(_titleLabel);

        // ---- Список слотов (прокрутка, ограничение высоты) ----
        _slotScroll = new ScrollContainer
        {
            Name = "SlotScroll",
            // PT1-003 (плейтест-1, 25.10): 240 → 320 — при 6+ строках (автосейв +
            // ручные + легаси-мусор старых билдов) свежий ручной слот выталкивался
            // за кромку скролла; вместе с сортировкой «новые сверху» (RefreshSlots)
            // видны все актуальные строки без прокрутки.
            CustomMinimumSize = new Vector2(440, 320),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(_slotScroll);

        _slotListBox = new VBoxContainer { Name = "SlotList" };
        _slotListBox.AddThemeConstantOverride("separation", 6);
        _slotScroll.AddChild(_slotListBox);

        _emptyHint = new Label
        {
            Text = "— Нет сохранений —",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _emptyHint.AddThemeFontSizeOverride("font_size", 14);
        _emptyHint.AddThemeColorOverride("font_color", new Color(0.65f, 0.58f, 0.45f));
        _slotListBox.AddChild(_emptyHint);

        // ---- Имя нового слота + «Сохранить» (только режим Full) ----
        _newSlotEdit = new LineEdit
        {
            Name = "NewSlotName",
            PlaceholderText = "Имя нового сохранения (латиница/цифры)",
            CustomMinimumSize = new Vector2(440, 36),
        };
        box.AddChild(_newSlotEdit);

        _saveBtn = UIFactory.CreateButton("SaveBtn", "◆ Сохранить", 440, 40);
        _saveBtn.Pressed += OnSavePressed;
        UIFactory.AddHoverLift(_saveBtn);
        UIFactory.AddPressShrink(_saveBtn);
        box.AddChild(_saveBtn);

        // ---- Действия выбранного слота ----
        var actionsRow = new HBoxContainer { Name = "Actions" };
        actionsRow.AddThemeConstantOverride("separation", 10);
        actionsRow.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(actionsRow);

        _loadBtn = UIFactory.CreateButton("LoadBtn", "Загрузить", 200, 40);
        _loadBtn.Disabled = true;
        _loadBtn.Pressed += OnLoadPressed;
        UIFactory.AddHoverLift(_loadBtn);
        actionsRow.AddChild(_loadBtn);

        _deleteBtn = UIFactory.CreateButton("DeleteBtn", "Удалить", 200, 40);
        _deleteBtn.Disabled = true;
        _deleteBtn.Pressed += OnDeletePressed;
        UIFactory.AddHoverLift(_deleteBtn);
        actionsRow.AddChild(_deleteBtn);

        // ---- Статус ----
        _statusLabel = new Label
        {
            Name = "Status",
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 0),
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.72f));
        box.AddChild(_statusLabel);

        var closeBtn = UIFactory.CreateButton("CloseBtn", "Закрыть (Esc)", 440, 36);
        closeBtn.Pressed += Close;
        UIFactory.AddHoverLift(closeBtn);
        box.AddChild(closeBtn);
    }

    private void SetTitle(string text) => _titleLabel.Text = text;

    private void ShowStatus(string message, bool error = false)
    {
        _statusLabel.Text = message;
        // AccentRed на тёмной панели темновата — используем яркие тона контекста.
        _statusLabel.AddThemeColorOverride("font_color",
            error ? new Color(0.95f, 0.45f, 0.35f) : new Color(0.80f, 0.85f, 0.65f));
    }

    // ──────────────────────────────────────────────────────────────────
    // Slots
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Перечитать каталог слотов (даты — из save_meta каждого слота).</summary>
    private void RefreshSlots()
    {
        _selectedSlot = null;
        foreach (var btn in _slotButtons.Values) btn.QueueFree();
        _slotButtons.Clear();
        _loadBtn.Disabled = true;
        _deleteBtn.Disabled = true;

        List<(string Name, string When, long Unix)> slots = new();
        if (SaveService != null)
        {
            foreach (var info in SaveService.GetAllSaves())
            {
                var when = "";
                if (info.CreatedUnixSeconds > 0)
                {
                    try
                    {
                        when = DateTimeOffset.FromUnixTimeSeconds(info.CreatedUnixSeconds)
                            .ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                    }
                    catch
                    {
                        when = "";
                    }
                }
                slots.Add((info.Slot.Name, when, info.CreatedUnixSeconds));
            }
        }

        // PT1-003 (плейтест-1, 25.10): свежие — СВЕРХУ. Алфавитный порядок
        // ФС ставил легаси-мусор autosave_0001..0004 (ротация прошлых билдов,
        // без save_meta — грузить нельзя) ВЫШЕ свежесохранённого slot_1 и
        // выталкивал его за кромку скролла: владелец «не мог выбрать слот для
        // загрузки» — строки ручного слота не было видно, кликабельны были
        // только автосейвы. Unix=0 (легаси без меты) — тонут на дно списка.
        slots.Sort((a, b) => a.Unix != b.Unix
            ? b.Unix.CompareTo(a.Unix)
            : string.CompareOrdinal(a.Name, b.Name));

        _emptyHint.Visible = slots.Count == 0;
        foreach (var (name, when, _) in slots)
        {
            var text = string.IsNullOrEmpty(when) ? name : $"{name}  —  {when}";
            var btn = UIFactory.CreateButton($"slot_{name}", text, 440, 38);
            // Выбор слота: подсветка + enable действий.
            btn.Pressed += () => SelectSlot(name);
            UIFactory.AddHoverLift(btn);
            _slotListBox.AddChild(btn);
            _slotButtons[name] = btn;
        }
    }

    private void SelectSlot(string name)
    {
        _selectedSlot = name;
        // PT1-003 (плейтест-1, 25.10): валидация формата В МОМЕНТ выбора.
        // Легаси-слоты старых билдов (ротация autosave_000N, файлы без
        // save_meta) видны в списке, но грузить их нельзя — прежде строка
        // выглядела рабочей, «Загрузить» уходил в сессию и PRE-REJECT'ился
        // тостом Validate failed. Теперь честно сразу: Load мёртв для
        // невалидного слота, Delete жив (мусор можно почистить), статус
        // объясняет причину. Validate — без побочных эффектов (Этап-4 P1).
        bool loadable = SaveService != null
            && SaveService.Validate(new SaveSlot(name, SaveSlotType.Manual));
        _loadBtn.Disabled = !loadable;
        _deleteBtn.Disabled = false;
        ShowStatus(loadable
            ? $"Выбрано: «{name}»"
            : $"«{name}» — старый/повреждённый формат: загрузка недоступна (можно удалить)");
        // Подсветка выбранной строки.
        foreach (var (slotName, btn) in _slotButtons)
            btn.Modulate = slotName == name ? new Color(1f, 0.95f, 0.7f) : Colors.White;
    }

    // ──────────────────────────────────────────────────────────────────
    // Actions
    // ──────────────────────────────────────────────────────────────────

    private void OnSavePressed()
    {
        if (SaveService == null)
        {
            ShowStatus("Сервис сохранений недоступен", error: true);
            return;
        }

        // Имя: из поля, иначе автослот slot_N (первый свободный).
        var name = _newSlotEdit.Text.Trim();
        if (name.Length == 0)
        {
            int n = 1;
            while (SaveService.HasSave(new SaveSlot($"slot_{n}", SaveSlotType.Manual))) n++;
            name = $"slot_{n}";
        }

        var ok = SaveService.Save(new SaveSlot(name, SaveSlotType.Manual));
        if (ok)
        {
            _newSlotEdit.Text = "";
            ShowStatus($"Сохранено: «{name}»");
            RefreshSlots();
        }
        else
        {
            // P1-2-цепочка: SaveService.LastError несёт причину (блок/IO).
            ShowStatus($"Не удалось сохранить: {SaveService.LastError ?? "ошибка записи"}", error: true);
        }
    }

    private void OnLoadPressed()
    {
        if (_selectedSlot == null) return;
        // Мир загружает ХОСТ (сессия + сцена): UI только сообщает выбор.
        ShowStatus($"Загрузка «{_selectedSlot}»…");
        LoadRequested?.Invoke(_selectedSlot);
    }

    private void OnDeletePressed()
    {
        if (_selectedSlot == null || SaveService == null) return;
        var name = _selectedSlot;
        // Этап-3 (дефект 3): честный bool по цепочке — при I/O-отказе
        // сообщение об ошибке, а не ложный «удалено».
        var ok = SaveService.DeleteSave(new SaveSlot(name, SaveSlotType.Manual));
        if (ok)
        {
            ShowStatus($"Удалено: «{name}»");
            RefreshSlots();
        }
        else
        {
            ShowStatus($"Не удалось удалить «{name}» (файл занят или нет прав)", error: true);
        }
    }
}
