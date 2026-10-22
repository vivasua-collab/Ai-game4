#nullable enable
// Создано: 2026-10-22 — Этап-7 аудита (квесты/диалоги/NPC): headless-верификация
// трёх фиксов и одного гейта по дословным регрессионным сценариям аудитора:
//
//   BUG-022 (P2) «покупка предмета засчитывается как сбор»:
//     — квест «Железная жила» (GatherItem material_iron_ore ×5) активен;
//     — РЕАЛЬНАЯ покупка руды у торговца (TryBuy через TradeService):
//       предметы в инвентаре, источник merchant_buy в ItemAddedEvent,
//       прогресс задания НЕ шевелится;
//     — реальная добыча (F-путь: TileService.TryHarvest по рудной жиле либо
//       канонический harvest-command из ResourceService) двигает прогресс
//       на фактическое количество; ItemAddedEvent на одну добычу — РОВНО ОДИН
//       (прежде InventoryModule добавлял дважды: command + факт-событие);
//     — батарея источников: merchant_buy/quest_reward/storage_transfer/
//       belt_transfer/equip_return/starting_gear не считаются; world/pickup/
//       loot/craft считаются. Батарея живёт в ОТДЕЛЬНОМ квесте с таргетом 50:
//       первая редакция сима гнала её по quest_gather_iron (таргет 5) и
//       упёрлась в потолок — world/pickup закрывали квест, loot/craft
//       оставались без запаса и «не зачитывались» (ошибка конструкции
//       сима, не игры). Побочная проверка: реальный квест закрывается
//       ровно на таргете, без перелива.
//
//   BUG-023 (P2) «завершение одного квеста пропускает прогресс другого»:
//     — два активных квеста с одинаковой целью (KillEnemy wolf ×1);
//     — ОДНО событие EnemyKilledEvent → ОБА квеста прогрессируют и завершаются
//       (прежде: первый удалялся из _activeQuestIds во время обхода, второй
//       прыгал через индекс и застревал 0/1);
//     — оба автоматически награждены (multi-complete в одном событии).
//
//   BUG-024 (P2) «неподдерживаемая награда теряется, квест помечается выданным»:
//     — квест с наградой Experience (не реализован) завершается → автовойда
//       ОТКАЗАНА, AreRewardsGranted == false (повторная выдача возможна);
//     — после починки контента (замена награды на поддержанный Qi) выдача
//       проходит ровно один раз; повтор — false (идемпотентность).
//
//   GAP-025 «цель "Поговори с NPC" не проверяет тип взаимодействия»:
//     — NPCInteractedEvent с типами trade/attack/gift НЕ двигает TalkToNPC;
//     — реальный E-путь (NPCService.OnNPCInteracted "talk") двигает и
//       завершает квест.
//
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE7_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация Этапа-7: семантика источников предметов, снимок
/// итерации активных квестов, превалидация наград, гейт типа взаимодействия.
/// Итог: [Stage7Sim] VERDICT: PASS/FAIL.
/// </summary>
public partial class Stage7QuestTruthSimDebug : Node
{
    [Inject] private IQuestService? _quests;
    [Inject] private IQuestRewardService? _questRewards;
    [Inject] private IInventoryService? _inventory;
    [Inject] private IItemDatabaseService? _itemDb;
    [Inject] private ITradeService? _trade;
    [Inject] private INPCSpawnerService? _npcSpawner;
    [Inject] private INPCService? _npcService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private ITileService? _tiles;

    [Inject] private IPublisher<EnemyKilledEvent>? _enemyKilledPub;
    [Inject] private IPublisher<NPCInteractedEvent>? _npcInteractedPub;
    [Inject] private IPublisher<ItemAddedEvent>? _itemAddedPub;
    [Inject] private IPublisher<ItemAddRequestEvent>? _itemAddRequestPub;
    [Inject] private ISubscriber<ItemAddedEvent>? _itemAddedSub;

    private System.IDisposable? _itemAddedToken;

    private readonly List<(string itemId, int count, string source)> _itemEvents = new();

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _itemAddedToken = _itemAddedSub?.Subscribe((in ItemAddedEvent e) =>
        {
            _itemEvents.Add((e.ItemId, e.Count, e.Source));
        });

        GD.Print("[Stage7Sim] Ready — Этап-7: источники предметов / снимок квест-итерации / превалидация наград / talk-гейт, старт через 2с");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _itemAddedToken?.Dispose(); _itemAddedToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_quests == null || _questRewards == null || _inventory == null || _itemDb == null
            || _trade == null || _npcSpawner == null || _npcService == null || _playerService == null
            || _tiles == null || _enemyKilledPub == null || _npcInteractedPub == null
            || _itemAddedPub == null || _itemAddRequestPub == null)
        {
            GD.Print("[Stage7Sim] VERDICT: FAIL — DI not wired");
            return;
        }
        var questService = _quests as Modules.Quest.QuestService;
        if (questService == null)
        {
            GD.Print("[Stage7Sim] VERDICT: FAIL — QuestService не резолвится конкретным типом");
            return;
        }

        bool pass = true;
        var pp = _playerService.Position;
        const string oreId = "material_iron_ore";

        // ════════════════════════════════════════════════════════════════
        // 1. BUG-022: покупка ≠ сбор; добыча = сбор (ровно один зачёт)
        // ════════════════════════════════════════════════════════════════
        if (!_quests.QuestExists("quest_gather_iron") || !_quests.StartQuest("quest_gather_iron"))
        {
            GD.Print("[Stage7Sim] VERDICT: FAIL — quest_gather_iron не зарегистрирован/не стартует");
            return;
        }
        int progress0 = QuestProgress(_quests, "quest_gather_iron");

        // 1a. РЕАЛЬНАЯ покупка руды в лавке.
        var ore = EnsureOre(_itemDb);
        string merchantId = _npcSpawner.SpawnNPC("human", NPCRole.Merchant, 1,
            new Position2D(pp.X + 3, pp.Y + 3), 77460);
        _trade.OpenTrade(merchantId);
        int invBefore = _inventory.GetItemCount(oreId);
        _itemEvents.Clear();
        bool bought = _trade.TryBuy(merchantId, oreId, 5);
        int invGained = _inventory.GetItemCount(oreId) - invBefore;
        bool buySourceMarked = _itemEvents.Any(e => e.itemId == oreId && e.source == ItemSources.MerchantBuy);
        bool buyProgressStuck = QuestProgress(_quests, "quest_gather_iron") == progress0;
        bool b022BuyOk = bought && invGained >= 1 && buySourceMarked && buyProgressStuck;
        GD.Print($"[Stage7Sim] 1a-TryBuy(руда): куплено {invGained}, источник merchant_buy " +
                 $"→ {buySourceMarked}, прогресс {progress0}→{QuestProgress(_quests, "quest_gather_iron")} " +
                 $"(не шевелится) → {(b022BuyOk ? "OK" : "FAIL")}");
        pass &= b022BuyOk;

        // 1b. РЕАЛЬНАЯ добыча: ищем рудную жилу на карте.
        int harvestX = -1, harvestY = -1;
        for (int x = 0; x < _tiles.MapWidth && harvestX < 0; x++)
            for (int y = 0; y < _tiles.MapHeight; y++)
            {
                var t = _tiles.GetTile(x, y);
                if (t.Object == ObjectType.OreVein && t.IsHarvestable && t.ResourceAmount > 0f)
                {
                    harvestX = x; harvestY = y;
                    break;
                }
            }

        int progressAfterBuy = QuestProgress(_quests, "quest_gather_iron");
        int harvestProgressDelta;
        bool singleAddEvent;
        if (harvestX >= 0)
        {
            _itemEvents.Clear();
            bool harvested = _tiles.TryHarvest(harvestX, harvestY, out var result);
            singleAddEvent = _itemEvents.Count(e => e.itemId == result.ItemId) == 1;
            harvestProgressDelta = QuestProgress(_quests, "quest_gather_iron") - progressAfterBuy;
            bool harvestOk = harvested && singleAddEvent
                && result.ItemId == oreId
                && harvestProgressDelta == result.Amount;
            GD.Print($"[Stage7Sim] 1b-TryHarvest(жила {harvestX},{harvestY}): +{result.Amount} " +
                     $"{result.ItemId}, ItemAddedEvent ×{_itemEvents.Count(e => e.itemId == result.ItemId)} " +
                     $"(ровно 1), прогресс +{harvestProgressDelta} → {(harvestOk ? "OK" : "FAIL")}");
            pass &= harvestOk;
        }
        else
        {
            // Fallback: в QA-мире нет жилы — канонический harvest-command
            // (тот же ItemAddRequestEvent, что публикует ResourceService.Harvest).
            GD.Print("[Stage7Sim] 1b-FALLBACK: рудной жилы нет — имитируем harvest-command ResourceService");
            _itemEvents.Clear();
            _itemAddRequestPub.Publish(new ItemAddRequestEvent(oreId, 3, ItemSources.Harvest));
            await Wait(0.2);
            singleAddEvent = _itemEvents.Count(e => e.itemId == oreId) == 1;
            harvestProgressDelta = QuestProgress(_quests, "quest_gather_iron") - progressAfterBuy;
            bool harvestOk = singleAddEvent && harvestProgressDelta == 3;
            GD.Print($"[Stage7Sim] 1b-harvest-command: +3 {oreId}, ItemAddedEvent " +
                     $"×{_itemEvents.Count(e => e.itemId == oreId)} (ровно 1 — дабл-адд добычи вылечен), " +
                     $"прогресс +{harvestProgressDelta} → {(harvestOk ? "OK" : "FAIL")}");
            pass &= harvestOk;
        }

        // 1c. Батарея источников — в ОТДЕЛЬНОМ квесте с таргетом 50.
        //     Ошибка конструкции первой редакции сима: батарея шла по
        //     quest_gather_iron (таргет 5) и упиралась в потолок — после
        //     добычи (1b) world/pickup закрывали квест, loot/craft не имели
        //     запаса. Чистая проверка семантики источников требует головного
        //     пространства: отдельный квест на тот же предмет, таргет 50.
        RegisterGatherQuest(questService, "qa_battery_sources", "QA Батарея источников",
            oreId, 50);
        if (!_quests.StartQuest("qa_battery_sources"))
        {
            GD.Print("[Stage7Sim] VERDICT: FAIL — qa_battery_sources не стартует");
            return;
        }
        int batteryBase = QuestProgress(_quests, "qa_battery_sources");
        if (batteryBase != 0)
            GD.Print($"[Stage7Sim] 1c-БАЗА: qa_battery_sources стартовал с прогрессом " +
                     $"{batteryBase} (ожидался 0) — ассерты ниже считаются от базы");
        int ironBefore = QuestProgress(_quests, "quest_gather_iron");

        // 1c-исключения: не-сбор не двигает.
        foreach (var src in new[]
                 {
                     ItemSources.MerchantBuy, ItemSources.QuestReward, ItemSources.StorageTransfer,
                     ItemSources.BeltTransfer, ItemSources.EquipReturn, ItemSources.StartingGear,
                 })
        {
            _itemAddedPub.Publish(new ItemAddedEvent(oreId, 2, src));
            await Wait(0.1);
            if (QuestProgress(_quests, "qa_battery_sources") != batteryBase)
            {
                GD.Print($"[Stage7Sim] 1c-FAIL: источник {src} засчитан как сбор (прогресс {batteryBase}→" +
                         $"{QuestProgress(_quests, "qa_battery_sources")})");
                pass = false;
            }
        }
        bool batteryExcludedOk = QuestProgress(_quests, "qa_battery_sources") == batteryBase;
        GD.Print($"[Stage7Sim] 1c-батарея-исключений: 6 не-сборов (merchant_buy/quest_reward/" +
                 $"storage/belt/equip/starting) — прогресс {batteryBase} → {(batteryExcludedOk ? "не тронут, OK" : "FAIL")}");
        pass &= batteryExcludedOk;

        // 1c-сбор: world/pickup/loot/craft двигают (+1 каждый), квест жив.
        foreach (var src in new[]
                 {
                     ItemSources.World, ItemSources.Pickup, ItemSources.Loot, ItemSources.Craft,
                 })
        {
            int before = QuestProgress(_quests, "qa_battery_sources");
            _itemAddedPub.Publish(new ItemAddedEvent(oreId, 1, src));
            await Wait(0.1);
            bool moved = QuestProgress(_quests, "qa_battery_sources") == before + 1;
            if (!moved)
            {
                GD.Print($"[Stage7Sim] 1c-FAIL: источник {src} НЕ засчитан как сбор");
                pass = false;
            }
        }
        bool batteryIncludedOk = QuestProgress(_quests, "qa_battery_sources") == batteryBase + 4
                              && _quests.GetQuestStatus("qa_battery_sources") == QuestStatus.Active;
        GD.Print($"[Stage7Sim] 1c-батарея-сбора: world/pickup/loot/craft → " +
                 $"{batteryBase}→{QuestProgress(_quests, "qa_battery_sources")} из 50, квест жив " +
                 $"→ {(batteryIncludedOk ? "OK" : "FAIL")}");
        pass &= batteryIncludedOk;

        // 1c-таргет-5 (документация причины переезда батареи): те же события
        // world/pickup довели реальный quest_gather_iron до таргета —
        // Completed РОВНО на 5; loot/craft после закрытия не переливаются
        // через таргет (AddProgress капает в Min, завершённый не итерируется).
        bool ironCappedOk = _quests.GetQuestStatus("quest_gather_iron") == QuestStatus.Completed
                         && QuestProgress(_quests, "quest_gather_iron") == 5;
        GD.Print($"[Stage7Sim] 1c-таргет-5: quest_gather_iron {ironBefore}→" +
                 $"{QuestProgress(_quests, "quest_gather_iron")}/5, статус " +
                 $"{_quests.GetQuestStatus("quest_gather_iron")} — закрылся на таргете, " +
                 $"без перелива → {(ironCappedOk ? "OK" : "FAIL")}");
        pass &= ironCappedOk;

        // ════════════════════════════════════════════════════════════════
        // 2. BUG-023: одно событие продвигает ОБА квеста одной цели
        // ════════════════════════════════════════════════════════════════
        RegisterKillQuest(questService, "qa_wolves_a", "QA Волки A", 1);
        RegisterKillQuest(questService, "qa_wolves_b", "QA Волки B", 1);
        if (!_quests.StartQuest("qa_wolves_a") || !_quests.StartQuest("qa_wolves_b"))
        {
            GD.Print("[Stage7Sim] VERDICT: FAIL — qa_wolves_a/b не стартуют");
            return;
        }
        _enemyKilledPub.Publish(new EnemyKilledEvent("animal_wolf_77"));
        await Wait(0.2);
        bool aDone = _quests.GetQuestStatus("qa_wolves_a") == QuestStatus.Completed;
        bool bDone = _quests.GetQuestStatus("qa_wolves_b") == QuestStatus.Completed;
        bool bothRewarded = _questRewards.AreRewardsGranted("qa_wolves_a")
                         && _questRewards.AreRewardsGranted("qa_wolves_b");
        bool b023Ok = aDone && bDone && bothRewarded;
        GD.Print($"[Stage7Sim] 2-двойной-зачёт: A={_quests.GetQuestStatus("qa_wolves_a")}, " +
                 $"B={_quests.GetQuestStatus("qa_wolves_b")}, награды A/B={bothRewarded} " +
                 $"(одно событие закрыло оба) → {(b023Ok ? "OK" : "FAIL")}");
        pass &= b023Ok;

        // ════════════════════════════════════════════════════════════════
        // 3. BUG-024: неподдержанная награда ≠ «выдано»
        // ════════════════════════════════════════════════════════════════
        var unsupported = new Modules.Quest.Data.QuestData
        {
            QuestId = "qa_reward_unsupported", DisplayName = "QA Награда-фантом",
            Type = QuestType.Side, Status = QuestStatus.NotStarted,
        };
        unsupported.Objectives.Add(new Modules.Quest.Data.QuestObjective
        {
            ObjectiveId = "obj_kill", Type = QuestObjectiveType.KillEnemy,
            TargetId = "wolf", Target = 1,
        });
        unsupported.Rewards.Add(new Modules.Quest.Data.QuestReward
        {
            RewardId = "qa_reward_exp", Type = QuestRewardType.Experience, Amount = 100,
        });
        questService.RegisterQuest(unsupported);
        _quests.StartQuest("qa_reward_unsupported");
        _enemyKilledPub.Publish(new EnemyKilledEvent("animal_wolf_78"));
        await Wait(0.2);
        bool unsupportedCompleted = _quests.GetQuestStatus("qa_reward_unsupported") == QuestStatus.Completed;
        bool notMarked = !_questRewards.AreRewardsGranted("qa_reward_unsupported");
        bool b024RejectOk = unsupportedCompleted && notMarked;
        GD.Print($"[Stage7Sim] 3-неподдержанная-награда: квест Completed={unsupportedCompleted}, " +
                 $"награда помечена={_questRewards.AreRewardsGranted("qa_reward_unsupported")} " +
                 $"(ожидание false) → {(b024RejectOk ? "OK" : "FAIL")}");
        pass &= b024RejectOk;

        // «Починка контента»: заменяем награду на поддержанную — теперь выдаётся
        // ровно один раз, повтор блокируется.
        var questData = questService.GetQuestData("qa_reward_unsupported");
        questData!.Rewards.Clear();
        questData.Rewards.Add(new Modules.Quest.Data.QuestReward
        {
            RewardId = "qa_reward_qi", Type = QuestRewardType.Qi, Amount = 50,
        });
        bool grantedOnce = _questRewards.GrantRewards("qa_reward_unsupported")
                        && _questRewards.AreRewardsGranted("qa_reward_unsupported");
        bool grantIdempotent = !_questRewards.GrantRewards("qa_reward_unsupported");
        bool b024FixOk = grantedOnce && grantIdempotent;
        GD.Print($"[Stage7Sim] 3-починка-контента: повторная выдача после замены → {grantedOnce}, " +
                 $"повтор заблокирован → {grantIdempotent} → {(b024FixOk ? "OK" : "FAIL")}");
        pass &= b024FixOk;

        // ════════════════════════════════════════════════════════════════
        // 4. GAP-025: TalkToNPC — только «talk»
        // ════════════════════════════════════════════════════════════════
        var talkQuest = new Modules.Quest.Data.QuestData
        {
            QuestId = "qa_talk_gate", DisplayName = "QA Talk-гейт",
            Type = QuestType.Side, Status = QuestStatus.NotStarted,
        };
        talkQuest.Objectives.Add(new Modules.Quest.Data.QuestObjective
        {
            ObjectiveId = "obj_talk", Type = QuestObjectiveType.TalkToNPC,
            TargetId = "Elder", Target = 1,
        });
        questService.RegisterQuest(talkQuest);
        _quests.StartQuest("qa_talk_gate");
        foreach (var badType in new[] { "trade", "attack", "gift" })
        {
            _npcInteractedPub.Publish(new NPCInteractedEvent("npc_elder_qa", "player_0", badType, "Elder"));
            await Wait(0.1);
        }
        bool gateHeld = _quests.GetQuestStatus("qa_talk_gate") == QuestStatus.Active
                     && QuestProgress(_quests, "qa_talk_gate") == 0;
        GD.Print($"[Stage7Sim] 4-гейт: trade/attack/gift не двигают TalkToNPC → {gateHeld}");
        pass &= gateHeld;

        // Реальный E-путь: NPCService.OnNPCInteracted("talk") с живым старейшиной.
        string elderId = _npcSpawner.SpawnNPC("human", NPCRole.Elder, 2,
            new Position2D(pp.X + 4, pp.Y + 4), 77560);
        if (_npcService is Modules.NPC.NPCService npcSvc)
            npcSvc.OnNPCInteracted(elderId, "player_0", "talk");
        else
            _npcInteractedPub.Publish(new NPCInteractedEvent(elderId, "player_0", "talk", "Elder"));
        await Wait(0.2);
        bool talkCompletes = _quests.GetQuestStatus("qa_talk_gate") == QuestStatus.Completed;
        GD.Print($"[Stage7Sim] 4-talk-зачёт: статус qa_talk_gate = " +
                 $"{_quests.GetQuestStatus("qa_talk_gate")} → {(talkCompletes ? "OK" : "FAIL")}");
        pass &= talkCompletes;

        // ════════════════════════════════════════════════════════════════
        // Итог
        // ════════════════════════════════════════════════════════════════
        GD.Print($"[Stage7Sim] VERDICT: {(pass
            ? "PASS — BUG-022/023/024 + GAP-025: источник предмета решает сбор, снимок итерации, честные награды, talk-гейт"
            : "FAIL")}");
    }

    // === Helpers ===

    private Godot.SignalAwaiter Wait(double seconds) =>
        ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static int QuestProgress(IQuestService quests, string questId)
    {
        var summary = quests.GetQuestSummaries().FirstOrDefault(q => q.QuestId == questId);
        return summary?.Objectives.FirstOrDefault().Progress ?? -1;
    }

    private static void RegisterKillQuest(Modules.Quest.QuestService service, string id, string name, int target)
    {
        var quest = new Modules.Quest.Data.QuestData
        {
            QuestId = id, DisplayName = name, Type = QuestType.Side,
            Status = QuestStatus.NotStarted,
        };
        quest.Objectives.Add(new Modules.Quest.Data.QuestObjective
        {
            ObjectiveId = $"obj_{id}", Type = QuestObjectiveType.KillEnemy,
            TargetId = "wolf", Target = target,
        });
        quest.Rewards.Add(new Modules.Quest.Data.QuestReward
        {
            RewardId = $"reward_{id}", Type = QuestRewardType.Qi, Amount = 10,
        });
        service.RegisterQuest(quest);
    }

    private static void RegisterGatherQuest(Modules.Quest.QuestService service, string id,
        string name, string itemId, int target)
    {
        var quest = new Modules.Quest.Data.QuestData
        {
            QuestId = id, DisplayName = name, Type = QuestType.Side,
            Status = QuestStatus.NotStarted,
        };
        quest.Objectives.Add(new Modules.Quest.Data.QuestObjective
        {
            ObjectiveId = $"obj_{id}", Type = QuestObjectiveType.GatherItem,
            TargetId = itemId, Target = target,
        });
        service.RegisterQuest(quest);
    }

    private static ItemData EnsureOre(IItemDatabaseService db)
    {
        if (db.TryGetItem("material_iron_ore", out var existing))
            return existing!;
        var ore = new ItemData
        {
            ItemId = "material_iron_ore", NameRu = "Железная руда",
            Category = ItemCategory.Material, Stackable = true, MaxStack = 100,
            Weight = 1.5f, Volume = 1.0f, Value = 5,
        };
        db.Register(ore);
        return ore;
    }
}
