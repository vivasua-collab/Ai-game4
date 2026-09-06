#nullable enable
// Создано: 2026-09-06 — S6: headless-верификация UX лавки
// (GODOT_TRADEUX_DEBUG=1).
//
// Проверяет S6-фиксы TradeWindow:
//   1. Лавка открывается (OpenTrade → событие → окно), ассортимент непуст.
//   2. Пометка «не хватает камней»: при бедном балансе строки недоступны,
//      после пополнения (Add → CurrencyChangedEvent) — доступны без
//      переоткрытия лавки.
//   3. Тултип первой строки: имя + описание + редкость + цена (не сырой itemId).
//   4. Hover-подсветка строки включается/выключается.
//   5. Футер упоминает Esc.
//   6. Close() закрывает лавку.
//
// GODOT_TRADEUX_HOLD=1 — не закрывать лавку после проверок (скриншоты).
// Запуск: GODOT_NEWGAME=1 GODOT_TRADEUX_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System.Linq;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация UX торгового окна. Итог: [TradeUXSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class TradeUXSimDebug : Node
{
    [Inject] private INPCService? _npcService;
    [Inject] private ITradeService? _trade;
    [Inject] private ICurrencyService? _currency;

    private GameWorldController? _world;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[TradeUXSim] diag: npc={_npcService != null}, trade={_trade != null}, currency={_currency != null}");

        if (_npcService == null || _trade == null || _currency == null)
        {
            GD.Print("[TradeUXSim] VERDICT: FAIL — services not injected");
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);

        _world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (_world == null)
        {
            GD.Print("[TradeUXSim] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        // === 1. Открыть лавку у торговца ===================================
        string? merchant = null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            var st = _npcService.GetNPC(id);
            if (st != null && st.IsAlive && st.Role == NPCRole.Merchant) { merchant = id; break; }
        }
        if (merchant == null)
        {
            GD.Print("[TradeUXSim] VERDICT: FAIL — Merchant-NPC не найден");
            return;
        }

        _trade.OpenTrade(merchant);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

        var win = _world.TradeWindowForQA;
        if (win == null)
        {
            GD.Print("[TradeUXSim] VERDICT: FAIL — TradeWindow не найдена");
            return;
        }

        bool pass = true;

        bool opened = win.IsOpen;
        int rows = win.StockRowCount;
        GD.Print($"[TradeUXSim] step1 open: merchant={merchant}, window={opened}, rows={rows} (expected True, rows>0)");
        if (!opened || rows < 1) pass = false;

        // === 2. Пометка «не хватает камней» и её снятие ====================
        int stones0 = _currency.SpiritStones;
        var rows0 = win.DebugStockRows;
        int maxPrice = rows0.Max(r => int.Parse(r.Split('|')[1]));
        bool poorCheck = stones0 < rows0.Min(r => int.Parse(r.Split('|')[1]));
        bool allPoor0 = rows0.All(r => r.Split('|')[2] == "0");
        GD.Print($"[TradeUXSim] step2 poor: stones={stones0}, maxPrice={maxPrice}, allUnaffordable={allPoor0} (poor={poorCheck})");
        if (poorCheck && !allPoor0) pass = false;

        // Пополнение → CurrencyChangedEvent → RefreshAll → строки перекрасились.
        _currency.Add(maxPrice + 10);
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        var rows1 = win.DebugStockRows;
        bool allRich1 = rows1.All(r => r.Split('|')[2] == "1");
        GD.Print($"[TradeUXSim] step2 rich: after Add({maxPrice + 10}) allAffordable={allRich1}");
        if (!allRich1) pass = false;

        // === 3. Тултип: описание вместо сырого itemId ======================
        string tooltip = win.DebugFirstStockRowTooltip();
        bool tooltipOk = tooltip.Contains("Редкость:") && tooltip.Contains("Цена за 1 шт.:") && tooltip.Contains('\n');
        GD.Print($"[TradeUXSim] step3 tooltip: len={tooltip.Length}, ok={tooltipOk}\n  '{tooltip.Replace("\n", " ⏎ ")}'");
        if (!tooltipOk) pass = false;

        // === 4. Hover-подсветка строки =====================================
        string firstItemId = rows1[0].Split('|')[0];
        var row = win.DebugFindRow(firstItemId, isBuy: true);
        if (row == null)
        {
            GD.Print("[TradeUXSim] step4 hover: ROW NOT FOUND");
            pass = false;
        }
        else
        {
            row.SetHovered(true);
            bool hovered = row.IsHoverHighlighted;
            row.SetHovered(false);
            bool unhovered = !row.IsHoverHighlighted;
            GD.Print($"[TradeUXSim] step4 hover: on={hovered}, off={unhovered}");
            if (!hovered || !unhovered) pass = false;
        }

        // === 5. Футер упоминает Esc ========================================
        string footer = win.DebugFooterText;
        GD.Print($"[TradeUXSim] step5 footer: '{footer}' (expected contains 'Esc')");
        if (!footer.Contains("Esc")) pass = false;

        // === 6. Закрытие (или удержание для скриншота) =====================
        if (System.Environment.GetEnvironmentVariable("GODOT_TRADEUX_HOLD") == "1")
        {
            // VLM-скриншот лавки.
            string? shot = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT");
            if (!string.IsNullOrEmpty(shot))
            {
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng(shot);
                GD.Print($"[TradeUXSim] screenshot: {shot}");
            }
            GD.Print("[TradeUXSim] HOLD: лавка остаётся открытой (скриншот)");
            GD.Print($"[TradeUXSim] VERDICT: {(pass ? "PASS — пометки/тултип/hover/футер" : "FAIL")}");
            return;
        }

        win.Close();
        bool openImmediate = win.IsOpen;
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        bool openAfter = win.IsOpen;
        GD.Print($"[TradeUXSim] step6 close: immediate={openImmediate}, after0.15s={openAfter} (expected False/False)");
        if (openImmediate || openAfter) pass = false;

        GD.Print($"[TradeUXSim] VERDICT: {(pass ? "PASS — доступность по балансу, тултипы, hover, футер, закрытие" : "FAIL")}");
    }

    private static GameWorldController? FindWorld(Node node)
    {
        if (node is GameWorldController world) return world;
        foreach (var child in node.GetChildren())
        {
            var found = FindWorld(child);
            if (found != null) return found;
        }
        return null;
    }
}
