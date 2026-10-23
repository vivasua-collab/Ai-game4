#nullable enable
// Создано: 2026-10-23 — Этап-9 аудита владельца (инвентарь/экипировка/переносы).
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE9_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяет сценарии задания [STAGE 9 AUDIT] (checkpoints/
// 10_23_stage9_inventory_audit_request.md):
//   A. ST9-01 — крафт и атомарность: точные количества, отказ без списаний,
//      неизвестный результат без списаний, mid-loop отказ ингредиента
//      (A4a — текущая модель без мутаторов; A4b — враждебный подписчик
//      ItemRemovedEvent: воспроизведение потери и транзакционный откат),
//      переполнение инвентаря при выдаче результата (overflow на землю),
//      повторный крафт — ровно одно событие на применение.
//   B. ST9-02 — экипировка: замена в занятом слоте (старый возвращён один
//      раз), двуручное при занятом WeaponOff, отказы по гейтам (уровень
//      культивации / характеристики / заблокированный слот) без исчезновения
//      предмета, ампутация (возврат ровно один раз, слот блокируется,
//      повторное событие идемпотентно), save/load round-trip без дюпа,
//      синхронизация IEquipmentDataProvider. Прочность — GAP (строка DUR).
//   C. ST9-03 — переносы и переполнение: пояс (полный/частичный возврат,
//      остаток в слоте), зарядник (полный/частичный/пустой камень, stale
//      SlotId), переполненный инвентарь при подборе (дроп на землю, сумма
//      инвариантна), частичный стак + остаток на землю, двуручное при
//      полном инвентаре (снятое — на землю, не в никуда).
//
// Инвариант каждого сценария: сумма предметов по ВСЕМ хранилищам
// (инвентарь + пояс + зарядник + экипировка + земля) не теряется и не дюпается.
// Итог: [Stage9Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/NOT TESTED/GAP).
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Inventory;
using CultivationGame.Modules.Inventory.Data;
using CultivationGame.Modules.Charger;
using CultivationGame.Modules.Generator;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-9 аудита: крафт, экипировка, переносы.
    /// Каждый сценарий печатает ожидание/факт и статус; финал — VERDICT.
    /// </summary>
    public partial class Stage9InventoryTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private InventoryService? _inventory;
        [Inject] private CraftingService? _crafting;
        [Inject] private EquipmentService? _equipment;
        [Inject] private BeltService? _belt;
        [Inject] private ChargerItemBridge? _chargerBridge;
        [Inject] private ChargerService? _charger;
        [Inject] private IItemDatabaseService? _itemDb;
        [Inject] private IGroundItemService? _ground;
        [Inject] private IPlayerService? _player;
        [Inject] private IEquipmentDataProvider? _equipProvider;

        // === DI: события ===
        [Inject] private IPublisher<ItemAddRequestEvent>? _itemAddPub;
        [Inject] private IPublisher<BodyPartSeveredEvent>? _severedPub;
        [Inject] private ISubscriber<CraftCompletedEvent>? _craftDoneSub;
        [Inject] private ISubscriber<CraftFailedEvent>? _craftFailSub;
        [Inject] private ISubscriber<ItemRemovedEvent>? _itemRemovedSub;

        private IDisposable? _craftDoneToken;
        private IDisposable? _craftFailToken;
        private IDisposable? _itemRemovedToken;
        private IDisposable? _hostileToken;

        private int _craftDoneCount;
        private int _craftFailCount;

        // === Тестовые предметы ===
        private const string WoodId = "qa_st9_wood";
        private const string StoneId = "qa_st9_stone";
        private const string ArrowId = "qa_st9_arrow";
        private const string JunkId = "qa_st9_junk";
        private const string PotionId = "qa_st9_potion";
        private const string SwordId = "qa_st9_sword1h";
        private const string SwordBId = "qa_st9_sword1h_b";
        private const string ShieldId = "qa_st9_shield";
        private const string Axe2hId = "qa_st9_axe2h";
        private const string BeltArmorId = "qa_st9_belt";
        private const string ChargerBeltId = "qa_st9_charger";
        private const string GateHelmId = "qa_st9_gatehelm";
        private const string StatHelmId = "qa_st9_stathelm";

        private ItemData? _wood, _stone, _arrow, _junk, _potion;
        private EquipmentData? _sword, _swordB, _shield, _axe2h, _beltArmor, _chargerBelt;

        // Враждебный подписчик (A4b): моделирует будущий реактивный
        // сервис, мутирующий инвентарь синхронно на ItemRemovedEvent.
        private bool _hostileArmed;

        // === Результаты ===
        private readonly List<(string Id, string Status, string Detail)> _results = new();

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _craftDoneToken = _craftDoneSub?.Subscribe((in CraftCompletedEvent e) =>
            {
                _craftDoneCount++;
                GD.Print($"[Stage9Sim] CRAFT DONE: {e.RecipeId} → {e.ResultItemId}×{e.Count}");
            });
            _craftFailToken = _craftFailSub?.Subscribe((in CraftFailedEvent e) =>
            {
                _craftFailCount++;
                GD.Print($"[Stage9Sim] CRAFT FAIL: {e.RecipeId} — {e.Reason}");
            });
            _itemRemovedToken = _itemRemovedSub?.Subscribe((in ItemRemovedEvent e) =>
                GD.Print($"[Stage9Sim] ITEM REMOVED: {e.ItemId}×{e.Count}"));

            GD.Print("[Stage9Sim] Ready — Этап-9 (крафт/экипировка/переносы) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _craftDoneToken?.Dispose();
            _craftFailToken?.Dispose();
            _itemRemovedToken?.Dispose();
            _hostileToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            if (_inventory == null || _crafting == null || _equipment == null || _belt == null
                || _chargerBridge == null || _charger == null || _itemDb == null
                || _ground == null || _player == null || _equipProvider == null
                || _itemAddPub == null || _severedPub == null || _itemRemovedSub == null)
            {
                GD.Print("[Stage9Sim] FAIL — DI not wired");
                PrintVerdict(false);
                return;
            }

            SeedItems();

            // ─── A. Крафт и атомарность (ST9-01) ───
            await ScenarioA();

            // ─── B. Экипировка и боевые характеристики (ST9-02) ───
            await ScenarioB();

            // ─── C. Переносы и переполнение (ST9-03) ───
            await ScenarioC();

            // Прочность: отдельная строка-вердикт (GAP по коду: износа нет).
            RecordDurabilityGap();

            PrintSummary();
        }

        // ================================================================
        //  ПОДГОТОВКА
        // ================================================================

        private void SeedItems()
        {
            _wood = new ItemData
            {
                ItemId = WoodId, NameRu = "qa-дерево", Category = ItemCategory.Material,
                Stackable = true, MaxStack = 50, Volume = 0.25f, Weight = 0.1f,
            };
            _stone = new ItemData
            {
                ItemId = StoneId, NameRu = "qa-камень", Category = ItemCategory.Material,
                Stackable = true, MaxStack = 50, Volume = 0.25f, Weight = 0.1f,
            };
            _arrow = new ItemData
            {
                ItemId = ArrowId, NameRu = "qa-стрела", Category = ItemCategory.Material,
                Stackable = true, MaxStack = 99, Volume = 0.5f, Weight = 0.05f,
            };
            _junk = new ItemData
            {
                ItemId = JunkId, NameRu = "qa-хлам", Category = ItemCategory.Material,
                Stackable = true, MaxStack = 100, Volume = 0.5f, Weight = 0.1f,
            };
            _potion = new ItemData
            {
                ItemId = PotionId, NameRu = "qa-зелье", Category = ItemCategory.Consumable,
                Stackable = true, MaxStack = 20, Volume = 0.5f, Weight = 0.2f,
            };
            _itemDb.RegisterRange(new ItemData[] { _wood, _stone, _arrow, _junk, _potion });

            _sword = new EquipmentData
            {
                ItemId = SwordId, NameRu = "qa-меч A", Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponMain, HandType = WeaponHandType.OneHand,
                Stackable = false, MaxStack = 1, Volume = 1f, Damage = 10, ItemLevel = 1,
            };
            _swordB = new EquipmentData
            {
                ItemId = SwordBId, NameRu = "qa-меч B", Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponMain, HandType = WeaponHandType.OneHand,
                Stackable = false, MaxStack = 1, Volume = 1f, Damage = 12, ItemLevel = 1,
            };
            _shield = new EquipmentData
            {
                ItemId = ShieldId, NameRu = "qa-щит", Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponOff, HandType = WeaponHandType.OneHand,
                // C7: объём щита больше двуручного топора — при возврате в
                // ПОЛНОМ инвентаре освобождённого места не хватает → дроп.
                Stackable = false, MaxStack = 1, Volume = 4f, Defense = 5, ItemLevel = 1,
            };
            _axe2h = new EquipmentData
            {
                ItemId = Axe2hId, NameRu = "qa-двуручный топор", Category = ItemCategory.Weapon,
                Slot = EquipmentSlot.WeaponMain, HandType = WeaponHandType.TwoHand,
                Stackable = false, MaxStack = 1, Volume = 3f, Damage = 20, ItemLevel = 1,
            };
            _beltArmor = new EquipmentData
            {
                ItemId = BeltArmorId, NameRu = "qa-пояс", Category = ItemCategory.Armor,
                Slot = EquipmentSlot.Belt, ItemType = "Armor",
                Stackable = false, MaxStack = 1, Volume = 1f, Defense = 1, ItemLevel = 1,
            };
            _chargerBelt = new EquipmentData
            {
                ItemId = ChargerBeltId, NameRu = "qa-зарядник", Category = ItemCategory.Armor,
                Slot = EquipmentSlot.Belt, ItemType = "Charger",
                Stackable = false, MaxStack = 1, Volume = 2f, Defense = 0, ItemLevel = 1,
            };
            var gateHelm = new EquipmentData
            {
                ItemId = GateHelmId, NameRu = "qa-шлем-гейт L9", Category = ItemCategory.Armor,
                Slot = EquipmentSlot.Head, RequiredCultivationLevel = 9,
                Stackable = false, MaxStack = 1, Volume = 1f, Defense = 3, ItemLevel = 9,
            };
            var statHelm = new EquipmentData
            {
                ItemId = StatHelmId, NameRu = "qa-шлем-гейт STR", Category = ItemCategory.Armor,
                Slot = EquipmentSlot.Head,
                StatRequirements = new List<StatRequirement>
                {
                    new StatRequirement { StatName = "Strength", MinValue = 999 },
                },
                Stackable = false, MaxStack = 1, Volume = 1f, Defense = 3, ItemLevel = 1,
            };
            _itemDb.RegisterRange(new ItemData[]
            {
                _sword, _swordB, _shield, _axe2h, _beltArmor, _chargerBelt, gateHelm, statHelm,
            });

            // Камень Ци: собственный экземпляр сеятеля (itemId канонический).
            var stone = QiStoneSeeder.Create(Core.Data.QiStoneSize.Pebble, isChaotic: false);
            stone.Stackable = true;
            stone.MaxStack = 10;
            _itemDb.Register(stone);

            _crafting.RegisterRecipe(new CraftingRecipe
            {
                RecipeId = "qa_st9_arrows",
                Name = "qa-стрелы",
                ResultItemId = ArrowId,
                ResultCount = 5,
                Ingredients = new Dictionary<string, int> { { WoodId, 3 }, { StoneId, 2 } },
            });
            // Рецепт с неизвестным базе результатом (A3 — гейт P1-4).
            _crafting.RegisterRecipe(new CraftingRecipe
            {
                RecipeId = "qa_st9_bad_result",
                Name = "qa-битый рецепт",
                ResultItemId = "qa_st9_no_such_item",
                ResultCount = 1,
                Ingredients = new Dictionary<string, int> { { WoodId, 1 } },
            });

            GD.Print("[Stage9Sim] SeedItems: предметы и рецепты зарегистрированы");
        }

        /// <summary>Полный сброс хранилищ между секциями (земля — тоже).</summary>
        private void ResetStorages()
        {
            _belt!.ResetWorld();          // пояс пуст
            _equipment!.ResetWorld();     // кукла пуста, блокировки сняты, провайдер синхронизирован
            _inventory!.ResetWorld();     // инвентарь пуст
            if (_ground is Modules.Inventory.GroundItemService concreteGround)
                concreteGround.ResetWorld();
        }

        private int Inv(string itemId) => _inventory!.GetItemCount(itemId);
        private int Belt(string itemId) =>
            _belt!.GetSlots().Where(s => s.ItemId == itemId).Sum(s => s.Count);
        private int Ground(string itemId) =>
            _ground!.GetAllGroundItems().Where(g => g.ItemId == itemId).Sum(g => g.Count);
        private int EquippedCount(string itemId)
        {
            int n = 0;
            foreach (var kvp in _equipment!.GetAllEquipped())
                if (kvp.Value != null && kvp.Value.ItemId == itemId) n++;
            return n;
        }

        private bool Add(ItemData item, int count)
            => _inventory!.TryAddItem(item, count, ItemSources.Cheat);

        /// <summary>Заполнить объём инвентаря хламом до отказа.</summary>
        private void FillVolume()
        {
            int guard = 0;
            while (guard++ < 200)
            {
                int n = _inventory!.HowManyCanFit(_junk!);
                if (n <= 0) break;
                _inventory.TryAddItem(_junk!, n, ItemSources.Cheat);
            }
        }

        /// <summary>Освободить ровно volume единиц объёма (убрав хлам).</summary>
        private void FreeVolume(float volume)
        {
            int pieces = (int)Mathf.Ceil(volume / _junk!.Volume);
            _inventory!.TryRemoveItem(JunkId, pieces);
        }

        /// <summary>
        /// Контракт UI (CharacterDollPanel): предмет покидает инвентарь,
        /// TryEquip; при отказе — возврат в инвентарь (rollback).
        /// </summary>
        private bool EquipFromInventory(string itemId, EquipmentSlot slot)
        {
            if (!_itemDb!.TryGetItem(itemId, out var d) || d is not EquipmentData eq)
                return false;
            if (!_inventory!.TryRemoveItem(itemId, 1)) return false;
            if (_equipment!.TryEquip(slot, eq)) return true;
            _inventory.TryAddItem(eq, 1, ItemSources.EquipReturn);
            return false;
        }

        // ================================================================
        //  A. КРАФТ И АТОМАРНОСТЬ (ST9-01)
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage9Sim] ─── A. ST9-01: крафт и атомарность ───");
            await Task.CompletedTask;
            ResetStorages();

            // A1: успешный крафт — точные количества.
            _craftDoneCount = 0; _craftFailCount = 0;
            Add(_wood!, 10); Add(_stone!, 6);
            bool can = _crafting!.CanCraft("qa_st9_arrows");
            bool ok = _crafting.TryCraft("qa_st9_arrows");
            bool a1counts = ok && can
                && Inv(WoodId) == 7 && Inv(StoneId) == 4 && Inv(ArrowId) == 5
                && Ground(ArrowId) == 0;
            bool a1 = a1counts && _craftDoneCount == 1 && _craftFailCount == 0;
            GD.Print($"[Stage9Sim] A1: крафт 3 дерева + 2 камня → 5 стрел; факт: wood={Inv(WoodId)} stone={Inv(StoneId)} arrow={Inv(ArrowId)} events={_craftDoneCount}/{_craftFailCount} → {(a1 ? "PASS" : "FAIL")}");
            Record("A1", a1, $"wood 10→{Inv(WoodId)} (ожид 7), stone 6→{Inv(StoneId)} (ожид 4), arrow={Inv(ArrowId)} (ожид 5), events done/fail={_craftDoneCount}/{_craftFailCount}");

            // A2: недостаточно одного ингредиента — никаких списаний.
            _craftDoneCount = 0; _craftFailCount = 0;
            _inventory!.TryRemoveItem(WoodId, Inv(WoodId) - 2); // wood=2 (<3), stone=4
            int w2 = Inv(WoodId), s2 = Inv(StoneId);
            bool a2craft = _crafting.TryCraft("qa_st9_arrows");
            bool a2 = !a2craft && Inv(WoodId) == w2 && Inv(StoneId) == s2
                      && Inv(ArrowId) == 5 && _craftDoneCount == 0 && _craftFailCount == 1;
            GD.Print($"[Stage9Sim] A2: нехватка дерева — без списаний; факт craft={a2craft} wood={Inv(WoodId)} stone={Inv(StoneId)} fail-events={_craftFailCount} → {(a2 ? "PASS" : "FAIL")}");
            Record("A2", a2, $"wood {w2}→{Inv(WoodId)}, stone {s2}→{Inv(StoneId)} (ожид без изменений), fail={_craftFailCount}");

            // A3: неизвестный результат рецепта — никаких списаний (гейт P1-4).
            _craftDoneCount = 0; _craftFailCount = 0;
            Add(_wood!, 8); // wood=10 снова
            int w3 = Inv(WoodId);
            bool a3craft = _crafting.TryCraft("qa_st9_bad_result");
            bool a3 = !a3craft && Inv(WoodId) == w3 && _craftDoneCount == 0 && _craftFailCount == 1;
            GD.Print($"[Stage9Sim] A3: неизвестный результат — без списаний; факт craft={a3craft} wood={Inv(WoodId)}(ожид {w3}) → {(a3 ? "PASS" : "FAIL")}");
            Record("A3", a3, $"wood {w3}→{Inv(WoodId)}, отказ по гейту ItemDatabase, fail={_craftFailCount}");

            // A4a: текущая модель (нет реактивных мутаторов) — mid-loop отказ
            //      недостижим, крафт succeeds.
            _craftDoneCount = 0; _craftFailCount = 0;
            ResetStorages();
            Add(_wood!, 10); Add(_stone!, 6);
            bool a4aCraft = _crafting.TryCraft("qa_st9_arrows");
            bool a4a = a4aCraft && Inv(WoodId) == 7 && Inv(StoneId) == 4 && Inv(ArrowId) == 5;
            GD.Print($"[Stage9Sim] A4a: без мутаторов mid-loop отказ недостижим; craft={a4aCraft} → {(a4a ? "PASS" : "FAIL")}");
            Record("A4a", a4a, $"текущая модель: craft={a4aCraft}, wood={Inv(WoodId)}, stone={Inv(StoneId)}, arrow={Inv(ArrowId)} (латентный путь подтверждён как недостижимый сегодня)");

            // A4b: враждебный подписчик ItemRemovedEvent (модель будущего
            //      реактивного сервиса) — воспроизведение потери/отката.
            _craftDoneCount = 0; _craftFailCount = 0;
            ResetStorages();
            Add(_wood!, 10); Add(_stone!, 6);
            _hostileArmed = true;
            _hostileToken = _itemRemovedSub!.Subscribe((in ItemRemovedEvent e) =>
            {
                if (!_hostileArmed || e.ItemId != WoodId) return;
                _hostileArmed = false; // один выстрел
                int have = _inventory!.GetItemCount(StoneId);
                if (have > 0)
                {
                    GD.Print($"[Stage9Sim] hostile: реактивно изымает камень ×{have}");
                    _inventory.TryRemoveItem(StoneId, have);
                }
            });
            bool a4bCraft = _crafting.TryCraft("qa_st9_arrows");
            // Контракт: крафт ОТКАЗАЛ (реактивный мутатор съел ингредиент),
            // но сам крафт не должен терять НИЧЕГО: списанное откатывается.
            bool a4b = !a4bCraft && Inv(WoodId) == 10
                       && _craftFailCount == 1 && _craftDoneCount == 0
                       && Ground(WoodId) == 0;
            GD.Print($"[Stage9Sim] A4b: hostile-подписчик → отказ; факт craft={a4bCraft} wood={Inv(WoodId)} (ожид 10 — откат), stone={Inv(StoneId)} (0 — изъят враждебным актором), fail={_craftFailCount} → {(a4b ? "PASS" : "FAIL")}");
            Record("A4b", a4b, $"mid-loop отказ: wood={Inv(WoodId)} (ожид 10, транзакционный откат), craft={a4bCraft}, fail={_craftFailCount}; ДО фикса — wood=7 (потеря 3), воспроизведено префикс-прогоном");
            _hostileToken?.Dispose();
            _hostileToken = null;
            _hostileArmed = false;

            // A5: переполненный инвентарь при выдаче результата — overflow на землю.
            _craftDoneCount = 0; _craftFailCount = 0;
            ResetStorages();
            Add(_wood!, 3); Add(_stone!, 2);
            FillVolume(); // объём полон
            int groundBefore = Ground(ArrowId);
            bool a5Craft = _crafting.TryCraft("qa_st9_arrows");
            int invArr = Inv(ArrowId), grArr = Ground(ArrowId);
            bool a5 = a5Craft && (invArr + grArr - groundBefore) == 5
                      && Inv(WoodId) == 0 && Inv(StoneId) == 0 && _craftDoneCount == 1;
            GD.Print($"[Stage9Sim] A5: полный инвентарь → результат инвентарь+земля; craft={a5Craft} inv={invArr} ground={grArr} (сумма 5) → {(a5 ? "PASS" : "FAIL")}");
            Record("A5", a5, $"overflow-контракт: стрелы inv={invArr} + ground={grArr} = 5 (ничего не потеряно), событие={_craftDoneCount}");

            // A6: повторный крафт — ровно одно событие на применение, дублей нет.
            _craftDoneCount = 0; _craftFailCount = 0;
            ResetStorages();
            Add(_wood!, 10); Add(_stone!, 6);
            bool c1 = _crafting.TryCraft("qa_st9_arrows");
            bool c2r = _crafting.TryCraft("qa_st9_arrows");
            bool a6 = c1 && c2r && _craftDoneCount == 2 && Inv(ArrowId) == 10
                      && Inv(WoodId) == 4 && Inv(StoneId) == 2;
            GD.Print($"[Stage9Sim] A6: два крафта → 2 события, 10 стрел; факт events={_craftDoneCount} arrow={Inv(ArrowId)} → {(a6 ? "PASS" : "FAIL")}");
            Record("A6", a6, $"2 крафта: events={_craftDoneCount} (ожид 2), arrow={Inv(ArrowId)} (ожид 10), wood={Inv(WoodId)}, stone={Inv(StoneId)} — дублирования нет");
        }

        // ================================================================
        //  B. ЭКИПИРОВКА И БОЕВЫЕ ХАРАКТЕРИСТИКИ (ST9-02)
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage9Sim] ─── B. ST9-02: экипировка ───");
            await Task.CompletedTask;
            ResetStorages();

            // B1: замена предмета в занятом слоте — старый возвращён ровно один раз.
            Add(_sword!, 1); Add(_swordB!, 1);
            bool b1e1 = EquipFromInventory(SwordId, EquipmentSlot.WeaponMain);
            bool b1e2 = EquipFromInventory(SwordBId, EquipmentSlot.WeaponMain);
            bool b1 = b1e1 && b1e2
                      && Inv(SwordId) == 1 && Inv(SwordBId) == 0
                      && EquippedCount(SwordBId) == 1 && EquippedCount(SwordId) == 0
                      && _equipProvider!.GetEquipped("player_0", EquipmentSlot.WeaponMain)?.ItemId == SwordBId
                      && _equipProvider.GetEquipped("player", EquipmentSlot.WeaponMain)?.ItemId == SwordBId;
            GD.Print($"[Stage9Sim] B1: замена в занятом слоте; факт invA={Inv(SwordId)} (ожид 1 — возвращён), invB={Inv(SwordBId)} (ожид 0), слот={_equipment!.GetEquipped(EquipmentSlot.WeaponMain)?.ItemId} → {(b1 ? "PASS" : "FAIL")}");
            Record("B1", b1, $"swap: A inv={Inv(SwordId)}/eq={EquippedCount(SwordId)}, B inv={Inv(SwordBId)}/eq={EquippedCount(SwordBId)}; провайдер player_0+player синхронизирован");

            // B2: двуручное при занятом WeaponOff — щит снят и возвращён один раз;
            //     WeaponOff при двуручном main — отказ.
            Add(_shield!, 1);
            Add(_axe2h!, 1);
            bool b2e1 = EquipFromInventory(ShieldId, EquipmentSlot.WeaponOff);
            int shieldInvBefore = Inv(ShieldId);
            bool b2e2 = EquipFromInventory(Axe2hId, EquipmentSlot.WeaponMain);
            bool b2offRefused = EquipFromInventory(SwordId, EquipmentSlot.WeaponOff); // при двуручном — валидатор отказывает
            bool b2 = b2e1 && b2e2 && !b2offRefused
                      && Inv(ShieldId) == shieldInvBefore + 1
                      && _equipment!.GetEquipped(EquipmentSlot.WeaponOff) == null
                      && _equipment.GetEquipped(EquipmentSlot.WeaponMain)?.ItemId == Axe2hId
                      && Inv(SwordId) == 1; // отказ в WeaponOff не съел меч (rollback)
            GD.Print($"[Stage9Sim] B2: двуручное при занятой второй руке; щит inv {shieldInvBefore}→{Inv(ShieldId)}, WeaponOff={_equipment!.GetEquipped(EquipmentSlot.WeaponOff)} offRefused={b2offRefused} → {(b2 ? "PASS" : "FAIL")}");
            Record("B2", b2, $"двуручное: щит возвращён ({Inv(ShieldId)}), WeaponOff пуст, экип 2h={EquippedCount(Axe2hId)}, отказ WeaponOff при 2h не съел предмет (rollback контракта UI)");

            // B3a: гейт уровня культивации — отказ, предмет в инвентаре.
            var gateHelm = _itemDb!.TryGetItem(GateHelmId, out var gh) && gh is EquipmentData ghd ? ghd : null;
            bool b3a = false;
            if (gateHelm != null)
            {
                Add(gateHelm, 1);
                int before = Inv(GateHelmId);
                bool equipped = EquipFromInventory(GateHelmId, EquipmentSlot.Head);
                b3a = !equipped && Inv(GateHelmId) == before
                      && _equipment!.GetEquipped(EquipmentSlot.Head) == null;
            }
            GD.Print($"[Stage9Sim] B3a: гейт культивации L9; факт → {(b3a ? "PASS" : "FAIL")} (шлем в инвентаре={Inv(GateHelmId)}, Head={_equipment!.GetEquipped(EquipmentSlot.Head)?.ItemId})");
            Record("B3a", b3a, $"RequiredCultivationLevel=9 при L1: отказ, предмет не исчез (inv={Inv(GateHelmId)})");

            // B3b: гейт характеристик — отказ, предмет в инвентаре.
            bool b3b = false;
            if (_itemDb.TryGetItem(StatHelmId, out var sh) && sh is EquipmentData shd)
            {
                Add(shd, 1);
                int before = Inv(StatHelmId);
                bool equipped = EquipFromInventory(StatHelmId, EquipmentSlot.Head);
                b3b = !equipped && Inv(StatHelmId) == before
                      && _equipment!.GetEquipped(EquipmentSlot.Head) == null;
            }
            GD.Print($"[Stage9Sim] B3b: гейт Strength≥999; факт → {(b3b ? "PASS" : "FAIL")} (шлем в инвентаре={Inv(StatHelmId)})");
            Record("B3b", b3b, $"StatRequirements Strength≥999: отказ, предмет не исчез (inv={Inv(StatHelmId)})");

            // B4: ампутация — возврат ровно один раз, слот блокируется,
            //     повторное событие идемпотентно, провайдер синхронизирован.
            int axeInvBefore = Inv(Axe2hId);
            _severedPub!.Publish(new BodyPartSeveredEvent("player", BodyPartType.RightArm,
                new[] { EquipmentSlot.WeaponMain }));
            int axeAfterFirst = Inv(Axe2hId);
            _severedPub.Publish(new BodyPartSeveredEvent("player", BodyPartType.RightArm,
                new[] { EquipmentSlot.WeaponMain }));
            // Меч A сейчас в инвентаре (возвращён заменой B1): пробуем надеть
            // в заблокированный слот — валидатор обязан отказать, предмет не исчезает.
            bool b4blocked = EquipFromInventory(SwordId, EquipmentSlot.WeaponMain); // слот заблокирован
            bool b4 = axeAfterFirst == axeInvBefore + 1
                      && Inv(Axe2hId) == axeAfterFirst // второй sever ничего не вернул
                      && _equipment!.GetEquipped(EquipmentSlot.WeaponMain) == null
                      && !b4blocked
                      && Inv(SwordId) == 1 // rollback при отказе — не исчез
                      && _equipProvider!.GetEquipped("player_0", EquipmentSlot.WeaponMain) == null;
            GD.Print($"[Stage9Sim] B4: ампутация RightArm; топор inv {axeInvBefore}→{axeAfterFirst} (возврат 1×), повтор идемпотентен={Inv(Axe2hId) == axeAfterFirst}, слот заблокирован={!b4blocked} → {(b4 ? "PASS" : "FAIL")}");
            Record("B4", b4, $"ампутация: возврат ровно 1 ({axeInvBefore}→{axeAfterFirst}), повторное событие без дюпа, WeaponMain заблокирован, меч A не исчез (inv={Inv(SwordId)}), провайдер пуст");

            // B5: экипировка → capture → снятие → restore → нет дюпа и потери.
            ResetStorages();
            Add(_sword!, 1);
            bool b5e = EquipFromInventory(SwordId, EquipmentSlot.WeaponMain);
            object invState = _inventory!.CaptureState();
            object eqState = _equipment!.CaptureState();
            _equipment.TryUnequip(EquipmentSlot.WeaponMain, out _); // событие вернуло меч в инвентарь
            bool b5mid = Inv(SwordId) == 1 && EquippedCount(SwordId) == 0;
            _inventory.RestoreState(invState);
            _equipment.RestoreState(eqState);
            bool b5 = b5e && b5mid && Inv(SwordId) == 0 && EquippedCount(SwordId) == 1
                      && _equipProvider!.GetEquipped("player_0", EquipmentSlot.WeaponMain)?.ItemId == SwordId;
            int b5sum = Inv(SwordId) + EquippedCount(SwordId) + Ground(SwordId);
            GD.Print($"[Stage9Sim] B5: round-trip инвентарь+кукла; после restore inv={Inv(SwordId)} eq={EquippedCount(SwordId)} сумма={b5sum} → {(b5 ? "PASS" : "FAIL")}");
            Record("B5", b5, $"capture/restore: mid(после снятия) inv=1/eq=0; после restore inv={Inv(SwordId)}/eq={EquippedCount(SwordId)} — сумма {b5sum}==1, дюпа/потери нет");

            // B6: провайдер после ResetWorld (пересборка мира) — пуст и синхронизирован.
            ResetStorages();
            bool b6 = _equipProvider!.GetEquipped("player_0", EquipmentSlot.WeaponMain) == null
                      && _equipProvider.GetEquipped("player", EquipmentSlot.Head) == null;
            GD.Print($"[Stage9Sim] B6: провайдер после ResetWorld пуст → {(b6 ? "PASS" : "FAIL")}");
            Record("B6", b6, "IEquipmentDataProvider: после reset пересборки мира оба ключа (player/player_0) пустые — статы прошлого мира не протекают");
        }

        // ================================================================
        //  C. ПЕРЕНОСЫ И ПЕРЕПОЛНЕНИЕ (ST9-03)
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage9Sim] ─── C. ST9-03: переносы и переполнение ───");
            await Task.CompletedTask;
            ResetStorages();

            // C1: инвентарь → пояс → инвентарь при свободном месте.
            Add(_beltArmor!, 1);
            bool beltOn = EquipFromInventory(BeltArmorId, EquipmentSlot.Belt);
            Add(_potion!, 10);
            int moved = _belt!.TryAssign(0, PotionId);
            bool c1assign = moved == 10 && Inv(PotionId) == 0 && Belt(PotionId) == 10;
            bool c1back = _belt.TryTakeBack(0);
            bool c1 = beltOn && c1assign && c1back
                      && Inv(PotionId) == 10 && Belt(PotionId) == 0;
            GD.Print($"[Stage9Sim] C1: пояс round-trip; assign={moved}, takeback={c1back}, inv={Inv(PotionId)} belt={Belt(PotionId)} → {(c1 ? "PASS" : "FAIL")}");
            Record("C1", c1, $"inv→belt→inv: assign {moved}, takeback={c1back}, сумма {Inv(PotionId) + Belt(PotionId)}==10");

            // C2: частичный возврат из пояса при заполненном инвентаре —
            //     остаток живёт в слоте пояса (контракт INV-3).
            _belt.TryAssign(0, PotionId); // пояс снова держит 10
            FillVolume();
            FreeVolume(3.5f); // влезет ровно 7 зелий (0.5 объёма каждое)
            int invBefore = Inv(PotionId);
            bool c2back = _belt!.TryTakeBack(0);
            int addedP = Inv(PotionId) - invBefore;
            bool c2 = c2back && addedP == 7 && Belt(PotionId) == 3
                      && (invBefore + 10 - addedP) >= 0;
            GD.Print($"[Stage9Sim] C2: частичный возврат из пояса; добавлено {addedP} (ожид 7), остаток в поясе={Belt(PotionId)} (ожид 3) → {(c2 ? "PASS" : "FAIL")}");
            Record("C2", c2, $"частичный возврат: inv +{addedP}, пояс остаток {Belt(PotionId)}, сумма {Inv(PotionId) + Belt(PotionId)}==10 — без потерь (INV-3)");

            // C3: зарядник — полный / частично истощённый / пустой камень.
            ResetStorages();
            Add(_chargerBelt!, 1);
            bool chargerOn = EquipFromInventory(ChargerBeltId, EquipmentSlot.Belt);
            // Кастомный крошечный камень (QiAmount=100 < ёмкости буфера 500):
            // TryDischarge клампит по свободному месту буфера (AUDIT-0921 B2),
            // канонический Dust (1024) не опустошается без потребления буфера
            // техниками — для контрактного теста «пустой камень» берём объём,
            // гарантированно влезающий в буфер за один раз.
            var stoneData = QiStoneSeeder.Create(Core.Data.QiStoneSize.Dust, isChaotic: false);
            stoneData.ItemId = "qa_st9_stone_tiny";
            stoneData.NameRu = "qa-камень-крошка";
            stoneData.QiAmount = 100;
            stoneData.QiRemaining = 100;
            stoneData.Stackable = true;
            stoneData.MaxStack = 10;
            _itemDb.Register(stoneData);
            Add(stoneData, 2);
            string stoneId = stoneData.ItemId;
            // slot-адресная вставка (контракт R10 P1-SlotId).
            var invSlots = _inventory!.GetAllSlots();
            Guid stoneSlotId = Guid.Empty;
            foreach (var s in invSlots)
                if (s.ItemId == stoneId) { stoneSlotId = s.SlotId; break; }
            bool c3insert = _chargerBridge!.TryInsertStone(stoneSlotId, stoneId, out var insMsg);
            int stonesInCharger = CountChargerStones();
            bool c3a = chargerOn && stoneSlotId != Guid.Empty && c3insert
                       && Inv(stoneId) == 1 && stonesInCharger == 1;
            GD.Print($"[Stage9Sim] C3a: вставка полного камня; ok={c3insert} inv={Inv(stoneId)} (ожид 1) вЗаряднике={stonesInCharger} msg='{insMsg}' → {(c3a ? "PASS" : "FAIL")}");
            Record("C3a", c3a, $"insert slot-addressed: inv={Inv(stoneId)}, зарядник={stonesInCharger} ('{insMsg}')");

            // Извлечение полного камня — обратно в инвентарь.
            // Контракт моста: FromDomainStone возвращает КАНОНИЧЕСКИЙ предмет
            // размера (qistone_dust_calm) с сохранённым объёмом — не исходный
            // кастомный ItemId; считаем по фактическому возврату.
            const string DustCalmId = "qistone_dust_calm";
            int slotIdx = FirstOccupiedChargerSlot();
            bool c3extract = _chargerBridge.TryExtractStone(slotIdx, out var extMsg);
            bool c3b = c3extract && Inv(stoneId) == 1 && Inv(DustCalmId) == 1
                       && CountChargerStones() == 0;
            GD.Print($"[Stage9Sim] C3b: извлечение полного камня; ok={c3extract} inv(tiny)={Inv(stoneId)} (ожид 1), inv(dust)={Inv(DustCalmId)} (ожид 1 — канонический возврат) msg='{extMsg}' → {(c3b ? "PASS" : "FAIL")}");
            Record("C3b", c3b, $"extract полный: tiny={Inv(stoneId)}, канон dust={Inv(DustCalmId)}, зарядник пуст ('{extMsg}')");

            // Частично истощённый: вставка ПОСЛЕДНЕГО tiny-камня → TryDischarge →
            // отказ извлечения (анти-дюп).
            Guid sid2 = FindSlotIdOf(stoneId);
            bool ins2 = _chargerBridge.TryInsertStone(sid2, stoneId, out _);
            int slot2 = FirstOccupiedChargerSlot();
            bool discharged = _charger!.TryDischarge(slot2, 40f); // 40 из 100 — частичный расход
            var midSnap = _chargerBridge.GetSlot(slot2);
            bool c3partialRefused = _chargerBridge.TryExtractStone(slot2, out var partMsg);
            bool c3c = ins2 && sid2 != Guid.Empty && discharged && !c3partialRefused
                       && midSnap.HasStone && midSnap.CurrentQi == 60 && midSnap.CurrentQi < midSnap.MaxQi
                       && Inv(stoneId) == 0; // последний tiny теперь в заряднике
            GD.Print($"[Stage9Sim] C3c: частичный камень не извлекается; discharge={discharged} qi={midSnap.CurrentQi}/{midSnap.MaxQi} refused={c3partialRefused} msg='{partMsg}' → {(c3c ? "PASS" : "FAIL")}");
            Record("C3c", c3c, $"анти-дюп: частичный ({midSnap.CurrentQi}/{midSnap.MaxQi}) — отказ извлечения, камень в слоте ('{partMsg}')");

            // Пустой камень — рассыпается, в инвентарь не возвращается.
            // Оставшиеся 60 Ци влезают в буфер (100 ≤ 500) — камень пустеет
            // за одну операцию, без борьбы с семантикой потребления буфера.
            bool emptiedOp = _charger.TryDischarge(slot2, 60f);
            long curQi = _chargerBridge.GetSlot(slot2).CurrentQi;
            bool emptied = emptiedOp && curQi == 0;
            var emptySnap = _chargerBridge.GetSlot(slot2);
            bool c3dissolve = _chargerBridge.TryExtractStone(slot2, out var dustMsg);
            bool c3d = emptied && emptySnap.CurrentQi == 0 && c3dissolve
                       && Inv(stoneId) == 0 && Inv(DustCalmId) == 1
                       && CountChargerStones() == 0;
            GD.Print($"[Stage9Sim] C3d: пустой камень рассыпался; ok={c3dissolve} msg='{dustMsg}' tiny={Inv(stoneId)} dust={Inv(DustCalmId)} (без дюпа: 1 камень из 2 рaccыпался) → {(c3d ? "PASS" : "FAIL")}");
            Record("C3d", c3d, $"пустой камень: рассыпался (не вернулся), зарядник пуст; итог: tiny={Inv(stoneId)}, dust={Inv(DustCalmId)} — 2 камня → 1 предмет (второй отдал Ци буферу)");

            // C6: устаревший SlotId — безопасный отказ. Кучка канонического
            // камня израсходована ЦЕЛИКОМ (частичное изъятие сохраняет Guid —
            // именно полный расход делает адрес недействительным).
            Guid staleSlot = FindSlotIdOf(DustCalmId);
            _inventory.TryRemoveItem(DustCalmId, Inv(DustCalmId)); // кучка исчезла — Guid устарел
            bool c6insert = _chargerBridge.TryInsertStone(staleSlot, DustCalmId, out var staleMsg);
            bool c6remove = _inventory.TryRemoveFromSlot(staleSlot, DustCalmId, 1);
            bool c6 = staleSlot != Guid.Empty && !c6insert && !c6remove
                      && Inv(DustCalmId) == 0 && CountChargerStones() == 0;
            GD.Print($"[Stage9Sim] C6: устаревший SlotId; insert={c6insert} remove={c6remove} inv={Inv(DustCalmId)} msg='{staleMsg}' → {(c6 ? "PASS" : "FAIL")}");
            Record("C6", c6, $"stale Guid (кучка израсходована): обе операции безопасно отказали ('{staleMsg}'), предметы в корректных хранилищах");

            // C4: переполненный инвентарь при подборе дропа — частичный add +
            //     остаток на землю (command-событие, полный пайплайн).
            ResetStorages();
            FillVolume();
            FreeVolume(2.0f); // влезет 4 стрелы
            int groundBefore4 = Ground(ArrowId);
            _itemAddPub!.Publish(new ItemAddRequestEvent(ArrowId, 10, ItemSources.Pickup));
            int added4 = Inv(ArrowId);
            int onGround4 = Ground(ArrowId) - groundBefore4;
            bool c4 = added4 == 4 && onGround4 == 6 && (added4 + onGround4) == 10;
            GD.Print($"[Stage9Sim] C4: подбор при полном инвентаре; в инвентарь={added4} (ожид 4), на землю={onGround4} (ожид 6) → {(c4 ? "PASS" : "FAIL")}");
            Record("C4", c4, $"pickup overflow: inv +{added4}, ground +{onGround4}, сумма 10 инвариантна");

            // C5: частичное добавление стака + выброс остатка на землю.
            FreeVolume(1.5f); // ещё +3 стрелы
            int invBefore5 = Inv(ArrowId);
            int groundBefore5 = Ground(ArrowId);
            _itemAddPub.Publish(new ItemAddRequestEvent(ArrowId, 8, ItemSources.Pickup));
            int added5 = Inv(ArrowId) - invBefore5;
            int dropped5 = Ground(ArrowId) - groundBefore5;
            bool c5 = added5 == 3 && dropped5 == 5 && (added5 + dropped5) == 8;
            GD.Print($"[Stage9Sim] C5: частичный стак; в инвентарь +{added5} (ожид 3), на землю +{dropped5} (ожид 5) → {(c5 ? "PASS" : "FAIL")}");
            Record("C5", c5, $"частичный стак: inv +{added5} + ground +{dropped5} = 8 — сумма до/после неизменна");

            // C7: двуручное при ПОЛНОМ инвентаре — снятый щит не исчезает:
            //     переполнение возврата уходит на землю (EQ-A1/INV-2 путь).
            ResetStorages();
            Add(_shield!, 1);
            bool shieldEq = EquipFromInventory(ShieldId, EquipmentSlot.WeaponOff);
            Add(_axe2h!, 1); // топор в инвентаре ДО заполнения объёма
            FillVolume(); // инвентарь полон — возврату некуда
            int groundShieldBefore = Ground(ShieldId);
            bool axeEq = EquipFromInventory(Axe2hId, EquipmentSlot.WeaponMain);
            bool c7 = shieldEq && axeEq
                      && Inv(ShieldId) == 0 && EquippedCount(ShieldId) == 0
                      && Ground(ShieldId) - groundShieldBefore == 1
                      && _equipment!.GetEquipped(EquipmentSlot.WeaponMain)?.ItemId == Axe2hId;
            GD.Print($"[Stage9Sim] C7: возврат при полном инвентаре; щит inv={Inv(ShieldId)} eq={EquippedCount(ShieldId)} ground+{Ground(ShieldId) - groundShieldBefore} → {(c7 ? "PASS" : "FAIL")}");
            Record("C7", c7, $"двуручное при volume-full: щит снят → земля (+1), не потерян и не задублирован");
        }

        // ================================================================
        //  ПРОЧНОСТЬ — GAP
        // ================================================================

        private void RecordDurabilityGap()
        {
            // Код-факт (проверено чтением, EquipmentStatAggregator.cs:195-202):
            // GetDurabilityEfficiency — заглушка «Pristine 1.0» («В будущих
            // фазах: отслеживание текущей прочности через InstanceData»);
            // сервисов износа/ремонта в Modules/ нет; ItemData.HasDurability/
            // MaxDurability объявлены, но CurrentDurability не существует.
            // По заданию: отметить GAP, не имитировать PASS.
            GD.Print("[Stage9Sim] DUR: прочность экипировки НЕ реализована в игровом пути — GAP (см. EquipmentStatAggregator.GetDurabilityEfficiency, заглушка 1.0)");
            _results.Add(("DUR", "GAP",
                "прочность объявлена (HasDurability/MaxDurability/DurabilityCondition), но износ/ремонт не подключены к реальному пути: эффективность всегда 1.0 (Pristine); требуется решение владельца о вводе износа или фиксации статуса"));
        }

        // ================================================================
        //  ХЕЛПЕРЫ
        // ================================================================

        private int CountChargerStones()
        {
            int n = 0;
            for (int i = 0; i < _charger!.SlotCount; i++)
                if (_chargerBridge!.GetSlot(i).HasStone) n++;
            return n;
        }

        private int FirstOccupiedChargerSlot()
        {
            for (int i = 0; i < _charger!.SlotCount; i++)
                if (_chargerBridge!.GetSlot(i).HasStone) return i;
            return -1;
        }

        private Guid FindSlotIdOf(string itemId)
        {
            foreach (var s in _inventory!.GetAllSlots())
                if (s.ItemId == itemId) return s.SlotId;
            return Guid.Empty;
        }

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void PrintSummary()
        {
            GD.Print("[Stage9Sim] ═══════ СВОДКА ЭТАПА-9 ═══════");
            int pass = 0, fail = 0, notTested = 0, gap = 0;
            foreach (var (id, status, detail) in _results)
            {
                GD.Print($"[Stage9Sim] {id,-5} {status,-10} {detail}");
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else if (status == "GAP") gap++;
                else notTested++;
            }
            GD.Print($"[Stage9Sim] Итого: PASS={pass} FAIL={fail} NOT TESTED={notTested} GAP={gap}");
            GD.Print($"[Stage9Sim] GAP-прочность: система не подключена к игровому пути — помечена GAP, PASS не имитируется (см. строку DUR)");
            PrintVerdict(fail == 0 && notTested == 0 && pass > 0);
        }

        private static void PrintVerdict(bool pass)
        {
            GD.Print($"[Stage9Sim] VERDICT: {(pass ? "PASS — ST9-01..03 проверены (крафт/экипировка/переносы; прочность = GAP)" : "FAIL")}");
        }
    }
}
