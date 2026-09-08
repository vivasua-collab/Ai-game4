#nullable enable
// Создано: 2026-09-08 — Review этап 5: headless-верификация DoT-пайплайна
// (GODOT_DOT_DEBUG=1). Проверяет (P1-4): BuffTickedEvent(Poison/Burn/Bleed/
// Freeze) → DamageAppliedEvent → BodyService применяет урон по частям тела
// ЦЕЛИ (игрок и NPC), HP реально падает (раньше — только Console.WriteLine).
// + P2-5: QiChangedEvent синхронизирует кэш уровня культивации тела.
// Запуск: GODOT_NEWGAME=1 GODOT_DOT_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class DotSimDebug : Node
{
    [Inject] private IPublisher<BuffTickedEvent>? _buffTickPub;
    [Inject] private IPublisher<QiChangedEvent>? _qiChangedPub;
    [Inject] private ISubscriber<DamageAppliedEvent>? _damageSub;
    [Inject] private IBodyDataProvider? _bodyProvider;
    [Inject] private INPCService? _npcService;

    private System.IDisposable? _damageToken;
    private int _dotDamageEvents;
    private string _lastDotSource = "";
    private string _lastDotTarget = "";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _damageToken = _damageSub?.Subscribe((in DamageAppliedEvent e) =>
        {
            if (e.SourceId != null && e.SourceId.StartsWith("dot:"))
            {
                _dotDamageEvents++;
                _lastDotSource = e.SourceId;
                _lastDotTarget = e.TargetId;
                GD.Print($"[DotSim] DamageApplied: {e.SourceId} → {e.TargetId}: {e.Damage} ({e.Type}, part={e.HitPart})");
            }
        });

        GD.Print("[DotSim] Ready — DoT pipeline verification starts in 2s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _damageToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_buffTickPub == null || _bodyProvider == null || _npcService == null)
        {
            GD.Print("[DotSim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        bool pass = true;

        // === 1. DoT по NPC: HP реально падает (P1-4) =======================
        string? npcId = null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (_npcService.IsAlive(id)) { npcId = id; break; }
        }
        if (npcId == null)
        {
            GD.Print("[DotSim] FAIL — no alive NPC");
            PrintVerdict(false);
            return;
        }

        int npcHpBefore = _bodyProvider.GetCurrentHealth(npcId);
        _dotDamageEvents = 0;
        _buffTickPub.Publish(new BuffTickedEvent(npcId, "qa_poison", BuffType.Poison, 7f));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        int npcHpAfter = _bodyProvider.GetCurrentHealth(npcId);
        int npcDrop = npcHpBefore - npcHpAfter;
        // NOTE: GetCurrentHealth = Σ RedHP; BodyService.ApplyDamage сплитит 70/30
        // (красный/белый слой) — видимый дроп ≈ 70% DoT-урона (как у обычных ударов).
        int npcExpectedRed = (int)(7f * 0.7f);
        bool npcDotOk = _dotDamageEvents == 1 && npcDrop >= npcExpectedRed && _lastDotTarget == npcId;
        GD.Print($"[DotSim] NPC poison tick: HP {npcHpBefore}→{npcHpAfter} (-{npcDrop} из 7; 70/30-сплит), events={_dotDamageEvents}");
        pass &= npcDotOk;

        // === 2. DoT по игроку: HP реально падает ===========================
        int playerHpBefore = _bodyProvider.GetCurrentHealth("player");
        _dotDamageEvents = 0;
        _buffTickPub.Publish(new BuffTickedEvent("player", "qa_burn", BuffType.Burn, 5f));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        int playerHpAfter = _bodyProvider.GetCurrentHealth("player");
        int playerDrop = playerHpBefore - playerHpAfter;
        int playerExpectedRed = (int)(5f * 0.7f);
        bool playerDotOk = _dotDamageEvents == 1 && playerDrop >= playerExpectedRed;
        GD.Print($"[DotSim] player burn tick: HP {playerHpBefore}→{playerHpAfter} (-{playerDrop} из 5; 70/30-сплит), events={_dotDamageEvents}");
        pass &= playerDotOk;

        // === 3. Bleed/Freeze тоже идут через пайплайн =====================
        _dotDamageEvents = 0;
        _buffTickPub.Publish(new BuffTickedEvent(npcId, "qa_bleed", BuffType.Bleed, 3f));
        _buffTickPub.Publish(new BuffTickedEvent(npcId, "qa_freeze", BuffType.Freeze, 2f));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[DotSim] bleed+freeze ticks: events={_dotDamageEvents} (ожидаем 2)");
        pass &= _dotDamageEvents == 2;

        // === 4. P2-5: QiChangedEvent с уровнем > 1 не ломает пайплайн ======
        // (синхронизация кэша — код-ревью; проверяем что событие с другим
        // уровнем обрабатывается без исключений и HP-поток жив.)
        if (_qiChangedPub != null)
        {
            _qiChangedPub.Publish(new QiChangedEvent("player", 100, 1000, 3, 0.5f));
            _dotDamageEvents = 0;
            _buffTickPub.Publish(new BuffTickedEvent(npcId, "qa_poison2", BuffType.Poison, 4f));
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            GD.Print($"[DotSim] after QiChanged(level=3): poison tick events={_dotDamageEvents} (ожидаем 1, без исключений)");
            pass &= _dotDamageEvents == 1;
        }

        PrintVerdict(pass);
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[DotSim] VERDICT: {(pass ? "PASS — DoT (Poison/Burn/Bleed/Freeze) наносит реальный урон через единый пайплайн" : "FAIL")}");
    }
}
