#nullable enable
// Создано: 2026-09-08 — Review этап 7: headless-верификация квестов
// (GODOT_QUEST_DEBUG=1). Полный цикл каждого стартового квеста:
//   accept (через реальный диалог старейшины!) → runtime event → objective
//   completed → quest completed → reward выдана.
// + P2-5 (RequiredCultivationLevel) + P0-1.3 (travel-квест не регистрируется).
// Запуск: GODOT_NEWGAME=1 GODOT_QUEST_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class QuestSimDebug : Node
{
    [Inject] private IQuestService? _quests;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IQiService? _qi;
    [Inject] private INPCService? _npcs;
    [Inject] private Modules.Interaction.DialogueService? _dialogueSvc;
    [Inject] private IPublisher<EnemyKilledEvent>? _enemyKilledPub;
    [Inject] private IPublisher<ItemAddedEvent>? _itemAddedPub;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print("[QuestSim] Ready — quest cycle verification starts in 2.5s");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);

        if (_quests == null || _inventory == null || _qi == null || _npcs == null
            || _dialogueSvc == null || _enemyKilledPub == null || _itemAddedPub == null)
        {
            GD.Print("[QuestSim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        bool pass = true;

        // === 0. Старейшина спавнится; travel-квест не регистрируется =======
        string? elderId = null;
        foreach (var id in _npcs.GetAllNPCIds())
        {
            var st = _npcs.GetNPCState(id);
            if (st != null && st.IsAlive && st.Role == NPCRole.Elder) { elderId = id; break; }
        }
        GD.Print($"[QuestSim] elder spawned: {(elderId != null ? elderId : "НЕТ")} (ожидаем NPCRole.Elder)");
        pass &= elderId != null;

        bool forestRegistered = _quests.QuestExists("quest_reach_forest");
        GD.Print($"[QuestSim] quest_reach_forest registered: {forestRegistered} (ожидаем False — travel не реализован)");
        pass &= !forestRegistered;

        // === 1. P1-3: диалог старейшины принимает квесты ===================
        // Реальный путь: TryStartNpcDialogue → SelectChoice(1) «Мне нужны
        // задания» → SelectChoice(0) «Конечно, помогу» → QuestStartRequestedEvent.
        bool dialogueStarted = _dialogueSvc.TryStartNpcDialogue(elderId!);
        _dialogueSvc.SelectChoice(1); // «Мне нужны задания»
        _dialogueSvc.SelectChoice(0); // «Конечно, помогу»
        bool wolvesStarted = _quests.GetQuestStatus("quest_kill_wolves") == QuestStatus.Active;
        bool ironStarted = _quests.GetQuestStatus("quest_gather_iron") == QuestStatus.Active;
        GD.Print($"[QuestSim] dialogue→StartQuest: started={dialogueStarted}, wolves={wolvesStarted}, iron={ironStarted} (ожидаем True/True/True)");
        pass &= wolvesStarted && ironStarted;
        _dialogueSvc.EndDialogue();

        // === 2. TalkToNPC: разговор со старейшиной (роль Elder) ============
        _quests.StartQuest("quest_talk_elder");
        // Освобождаем место для награды Ци (игрок может быть на максимуме —
        // AddQi упирается в кап и дельта была бы не видна).
        _qi.TryConsumeQi(300);
        long qiBefore = _qi.CurrentQi;
        // Симулируем реальный E-путь: NPCService.OnNPCInteracted(npcId, player, "talk")
        if (_npcs is Modules.NPC.NPCService npcSvc)
            npcSvc.OnNPCInteracted(elderId!, "player_0", "talk");
        bool talkDone = _quests.GetQuestStatus("quest_talk_elder") == QuestStatus.Completed;
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool qiGranted = _qi.CurrentQi > qiBefore;
        GD.Print($"[QuestSim] talk_elder: completed={talkDone}, Qi reward granted={qiGranted} ({qiBefore}→{_qi.CurrentQi})");
        pass &= talkDone && qiGranted;

        // === 3. GatherItem: material_iron_ore ×5 → награда-слиток =========
        int steelBefore = _inventory.GetItemCount("material_steel_ingot");
        for (int i = 0; i < 5; i++)
            _itemAddedPub.Publish(new ItemAddedEvent("material_iron_ore", 1));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool ironDone = _quests.GetQuestStatus("quest_gather_iron") == QuestStatus.Completed;
        int steelAfter = _inventory.GetItemCount("material_steel_ingot");
        GD.Print($"[QuestSim] gather_iron: completed={ironDone}, steel ingots {steelBefore}→{steelAfter} (ожидаем +2, P1-2)");
        pass &= ironDone && steelAfter >= steelBefore + 2;

        // === 4. KillEnemy: волки (нормализация animal_wolf_N → wolf) ======
        _enemyKilledPub.Publish(new EnemyKilledEvent("animal_wolf_7"));
        _enemyKilledPub.Publish(new EnemyKilledEvent("animal_wolf_8"));
        _enemyKilledPub.Publish(new EnemyKilledEvent("animal_wolf_9"));
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        bool wolvesDone = _quests.GetQuestStatus("quest_kill_wolves") == QuestStatus.Completed;
        GD.Print($"[QuestSim] kill_wolves (×3 animal_wolf_N): completed={wolvesDone} (ожидаем True, P0-1 нормализация)");
        pass &= wolvesDone;

        // === 5. P2-5: RequiredCultivationLevel гейт =======================
        if (_quests is Modules.Quest.QuestService qs)
        {
            qs.RegisterQuest(new Modules.Quest.Data.QuestData
            {
                QuestId = "qa_gated_quest", DisplayName = "QA Gated", Description = "тест",
                Status = QuestStatus.NotStarted,
                RequiredCultivationLevel = 99,
                Objectives = { new Modules.Quest.Data.QuestObjective { ObjectiveId = "o1", Type = QuestObjectiveType.SurviveDays, TargetId = "1", Target = 1 } },
            });
        }
        bool gatedAccepted = _quests.StartQuest("qa_gated_quest");
        GD.Print($"[QuestSim] gated quest (req L99, player L1): accepted={gatedAccepted} (ожидаем False, P2-5)");
        pass &= !gatedAccepted;

        PrintVerdict(pass);
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[QuestSim] VERDICT: {(pass ? "PASS — все квесты: accept(диалог)→event→complete→reward; гейты работают" : "FAIL")}");
    }
}
