#nullable enable
// R41-10 (репорт владельца 18.10 №7: «Спроектировать кнопку просмотра тайла
// под курсором мыши — для технического использования. Послойное описание
// начиная от типа тайла до плотности ци»).
//
// Тайл-инспектор — технический не-модальный оверлей под курсором:
//   • включение — клавиша F4 (свободна: InputMapInitializer «F3/F4/F6/F8
//     свободны»; F3 занят FPS-счётчиком, F6 — перф-оверлеем R41-2);
//   • послойное описание тайла из ЕДИНОЙ модели GameTile одним
//     GetTile(x,y) (OOB → Void-тайл, не бросает): координаты → биом →
//     поверхность/стоимость → проходимость/флаги → объект → ресурс →
//     прочность → сущности на тайле;
//   • плотность Ци — ЧЕСТНО на уровне ЛОКАЦИИ (IWorldService.GetLocation.
//     QiDensity): пер-тайл Ци в рантайме НЕТ (концепт TILE_SYSTEM §6);
//     высоты на тайле тоже НЕТ (elevation — транзиент генерации);
//   • окно перетаскивается за заголовок (паттерн R40-3 ItemContextMenu:
//     OnHeaderGuiInput + ClampPanelPosition) — не мешает обзору;
//   • обновление 0.2с троттлом + text-diff (паттерн FPS-лейбла GWC),
//     MouseFilter=Ignore на корне — клики проходят в мир (INP-1 не задет);
//   • собственная DI-инъекция в _Ready (паттерн PerfOverlay) — GWC не
//     трогает внутренности оверлея, только добавляет ноду в _hudCanvas.
//
// QA-нейтрален: в headless-симах не активен (Visible=false по умолчанию,
// F4 не жмётся, env-гейта нет — включение только клавишей).
using System;
using System.Text;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// R41-10: тайл-инспектор под курсором — послойное описание тайла
/// (тип → биом → объект → Ци локации) для технического использования.
/// F4 — тумбл видимости; панель перетаскивается за заголовок.
/// </summary>
public partial class TileInspectorOverlay : Control
{
    // === DI (паттерн PerfOverlay: своя инъекция, ZIndex=60, Ignore-мышь) ===
    [Inject] private ITileService? _tiles;
    [Inject] private IWorldService? _world;
    [Inject] private INPCService? _npcs;
    // IAnimalService не даёт перечня всех зверей (GetAllAnimals — конкретный
    // AnimalService, паттерн инъекции PerfOverlay._animalService).
    [Inject] private Modules.NPC.AnimalService? _animals;
    [Inject] private IQiService? _qi;

    // === UI ===
    private PanelContainer _panel = null!;
    private Label _headerLabel = null!;
    private Label _infoLabel = null!;

    // === Троттл обновления (0.2с, паттерн FPS-лейбла) ===
    private float _accum;

    // === Перетаскивание панели (паттерн R40-3 ItemContextMenu) ===
    private bool _dragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Не-модальный технический оверлей: не ловим мышь на корне (клики
        // проходят в мир), ZIndex=60 — над HUD-элементами своего слоя.
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 60;

        _panel = new PanelContainer
        {
            Name = "TileInspectorPanel",
            // Тело панели тоже прозрачно для мыши — ловит только заголовок
            // (drag) — техническое окно не должно перекрывать клики по миру.
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.05f, 0.92f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.45f, 0.55f, 0.45f, 0.8f));
        style.SetCornerRadiusAll(6);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 6;
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        _panel.AddChild(vbox);

        // Заголовок — ЕДИНСТВЕННАЯ зона, ловящая мышь (перетаскивание).
        _headerLabel = new Label
        {
            Text = "◆ Тайл-инспектор (F4 — тумбл; тащи за заголовок)",
            MouseFilter = MouseFilterEnum.Stop,
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 12);
        _headerLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.95f, 0.75f));
        _headerLabel.GuiInput += OnHeaderGuiInput;
        vbox.AddChild(_headerLabel);

        _infoLabel = new Label
        {
            Text = "…",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        _infoLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.93f, 0.85f));
        _infoLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _infoLabel.AddThemeConstantOverride("outline_size", 3);
        vbox.AddChild(_infoLabel);

        // Стартовая позиция: под FPS-счётчиком/перф-оверлеем (левый верх).
        _panel.Position = new Vector2(12, 150);

        // По умолчанию скрыт; env GODOT_TILE_INSPECTOR=1 — автозапуск
        // (отладка/скриншоты; иначе — клавиша F4).
        Visible = System.Environment.GetEnvironmentVariable("GODOT_TILE_INSPECTOR") == "1";
        if (Visible) _accum = UpdateIntervalSec; // первый кадр — сразу
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // F4 — тумбл оверлея (клавиша свободна, см. InputMapInitializer).
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F4 })
        {
            Visible = !Visible;
            if (Visible) _accum = UpdateIntervalSec; // первый кадр — сразу
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _accum += (float)delta;
        if (_accum < UpdateIntervalSec) return;
        _accum = 0f;
        string text = BuildText();
        if (_infoLabel.Text != text)
            _infoLabel.Text = text;
    }

    /// <summary>Период обновления текста (сек) — 0.2с троттл.</summary>
    private const float UpdateIntervalSec = 0.2f;

    // === R41-10: QA-акцессор =============================================

    /// <summary>
    /// R41-10 QA: построить послойный текст инспектора прямо (без F4/GUI —
    /// мышь headless-прогона стоит в (0,0), тайл может быть Void/OOB).
    /// Проверяет, что расчёт слоёв не бросает исключений и возвращает
    /// полный текст (вызов — HotbarSimDebug шаг 8).
    /// </summary>
    public string BuildTextForQA() => BuildText();

    // === Построение текста: послойное описание тайла =========================

    /// <summary>
    /// Послойное описание тайла под курсором: координаты/локация → биом/
    /// поверхность/скорость → проходимость/флаги → объект → ресурс →
    /// прочность → сущности → уровень культивации игрока (штрафы биома).
    /// </summary>
    private string BuildText()
    {
        if (_tiles == null) return "Тайл-инспектор: ITileService не инжектирован";

        // Мышь → мировые координаты → тайл. Оверлей живёт в HUD CanvasLayer
        // (экранные координаты), а тайлы — в мировом канвасе с камерой:
        // обратный canvas-transform вьюпорта переводит экран → мир (паттерн
        // SceneBuilder.GetVisibleTileRange; прямой аналог GWC-кода —
        // GetGlobalMousePosition() у Node2D в мировом канвасе).
        Transform2D canvasXform = GetViewport().GetCanvasTransform();
        Vector2 worldPos = canvasXform.AffineInverse() * GetViewport().GetMousePosition();
        int tileX = (int)(worldPos.X / GameConstants.TILE_PIXELS);
        int tileY = (int)(worldPos.Y / GameConstants.TILE_PIXELS);

        GameTile tile = _tiles.GetTile(tileX, tileY); // OOB → Void-тайл

        // Локация + Ци локации (пер-тайл Ци в рантайме НЕТ — честный источник).
        string locName = "?";
        int locQi = 0;
        if (_world?.CurrentLocation is { } loc)
        {
            locName = loc.Name;
            locQi = loc.QiDensity;
        }
        else if (_world != null)
        {
            var info = _world.GetLocation(_world.CurrentLocationId);
            locName = info.Name;
            locQi = info.QiDensity;
        }

        // Уровень культивации игрока — для террейн-фактора и гейта L5+.
        int level = _qi != null ? Math.Max(1, (int)_qi.CultivationLevel) : 1;
        float speedFactor = GameConstants.GetTerrainSpeedFactor(tile, level);
        bool biomePenaltiesActive = level < 5;

        var sb = new StringBuilder(512);
        sb.Append($"Тайл ({tileX}, {tileY}) | локация: {locName} (Ци локации: {locQi})\n");
        sb.Append($"Биом: {tile.Biome} | Поверхность: {tile.Terrain} | " +
                  $"MoveCost {tile.EffectiveMoveCost:F2} | Скорость ×{speedFactor:F2}\n");
        sb.Append($"Проходимость: {tile.IsWalkable} | Флаги: {tile.Flags}\n");
        sb.Append($"Объект: {tile.Object} ({tile.ObjectCategory})\n");
        sb.Append($"Ресурс: {(string.IsNullOrEmpty(tile.ResourceId) ? "—" : tile.ResourceId)} " +
                  $"{tile.ResourceAmount:F0}/{tile.ResourceMax:F0} | добываем: {tile.IsHarvestable}\n");
        sb.Append($"Прочность: HP {tile.DestructibleHP:F0}/{tile.DestructibleMaxHP:F0} | тир {tile.HardnessTier}\n");
        sb.Append($"Сущности на тайле: {DescribeEntities(tileX, tileY)}\n");
        sb.Append($"Уровень культивации игрока: {level} " +
                  $"(штрафы биома: {(biomePenaltiesActive ? "действуют" : "игнорируются (L5+)")})\n");
        // Честная пометка: высот на тайле нет (транзиент генерации), пер-тайл
        // Ци — концепт доков; источник Ци — плотность локации (строка 1).
        sb.Append("Ци: пер-тайл Ци и высоты в рантайме нет — см. Ци локации выше");
        return sb.ToString();
    }

    /// <summary>
    /// Сущности, стоящие на тайле: живые NPC (имена) и звери (вид).
    /// Источники: INPCService.GetAllNPCIds()+GetNPCState().Position,
    /// AnimalService.GetAllAnimals() (паттерн перф-оверлея).
    /// </summary>
    private string DescribeEntities(int x, int y)
    {
        var sb = new StringBuilder();
        if (_npcs != null)
        {
            foreach (var id in _npcs.GetAllNPCIds())
            {
                if (!_npcs.IsAlive(id)) continue;
                if (_npcs.GetNPCState(id) is not { } st) continue;
                if (st.Position.X == x && st.Position.Y == y)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(st.DisplayName);
                }
            }
        }
        if (_animals != null)
        {
            foreach (var animal in _animals.GetAllAnimals())
            {
                if (!animal.IsAlive) continue;
                if (animal.Position.X != x || animal.Position.Y != y) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(_animals.GetDisplayName(animal.EntityId) ?? animal.Species);
            }
        }
        return sb.Length > 0 ? sb.ToString() : "—";
    }

    // === Перетаскивание панели (паттерн R40-3: ItemContextMenu) ==============

    /// <summary>R41-10: перетаскивание окна за заголовок (ЛКМ зажать + движение).</summary>
    private void OnHeaderGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            _dragging = mb.Pressed;
            if (_dragging)
                _dragOffset = _panel.Position - mb.GlobalPosition;
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            _panel.Position = ClampPanelPosition(motion.GlobalPosition + _dragOffset);
        }
    }

    /// <summary>
    /// R41-10: кламп позиции панели в границы вьюпорта (окно не уходит
    /// за экран; паттерн ItemContextMenu.ClampPanelPosition, но размер
    /// вьюпорта — корневой Control оверлея не FullRect, Size здесь = 0).
    /// </summary>
    private Vector2 ClampPanelPosition(Vector2 pos)
    {
        Vector2 vp = GetViewportRect().Size;
        Vector2 sz = _panel.Size;
        pos.X = Mathf.Clamp(pos.X, 0, MathF.Max(0, vp.X - sz.X));
        pos.Y = Mathf.Clamp(pos.Y, 0, MathF.Max(0, vp.Y - sz.Y));
        return pos;
    }
}
