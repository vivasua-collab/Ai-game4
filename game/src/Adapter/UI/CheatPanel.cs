#if DEBUG
#nullable enable
// Создано: 2026-08-23 — Этап 7 внедрения ЦИ: чит-меню разработки (F1).
// DEBUG-ONLY: вся панель исключается из release-сборки директивой #if DEBUG.
//
// Содержит кнопки для тестирования системы ЦИ:
//   • Установка уровня культивации L1..L9
//   • Заполнение Ци / +10000 Ци
//   • Прорыв (TryBreakthrough)
//   • Выдача случайных техник / очистка техник
//   • Выдача случайных камней Ци
//   • Создание тест-формации (Gathering) в позиции игрока
//   • Тоггл быстрой утечки формации (×10)
//
// Управление: F1 — открыть/закрыть панель (вне зависимости от состояния UI).
// Панель находится в верхнем-левом углу, MouseFilter.Stop (ловит клики).
using System;
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI
{
    /// <summary>
    /// Чит-панель разработки (F1). Открывается в любой момент игры.
    /// #if DEBUG: в release-сборке класс не компилируется.
    /// </summary>
    public partial class CheatPanel : Panel
    {
        [Inject] private IQiService Qi = null!;
        [Inject] private IPlayerService Player = null!;
        [Inject] private IInventoryService Inventory = null!;
        [Inject] private IItemDatabaseService ItemDatabase = null!;
        [Inject] private ITechniqueGeneratorService TechniqueGenerator = null!;
        [Inject] private Modules.Combat.TechniqueService Techniques = null!;
        [Inject] private IFormationService Formations = null!;
        [Inject] private Modules.Generator.IFormationGeneratorService FormationGenerator = null!;
        [Inject] private Modules.Formation.FormationConfig FormationCfg = null!;
        [Inject] private IPublisher<ToastShownEvent> ToastPub = null!;
        // Phase F (2026-08-27): новые сервисы для генерации экипировки/расходников + верификация.
        [Inject] private IEquipmentGenerator EquipmentGenerator = null!;
        [Inject] private IItemGeneratorService ItemGenerator = null!;
        [Inject] private IVerificationService Verifier = null!;
        [Inject] private Modules.Generator.DeduplicationService Dedup = null!;
        [Inject] private Modules.Generator.TechniqueRegistry TechniqueRegistry = null!;

        private Label _statusLabel = null!;
        private Button _fastLeakButton = null!;
        private bool _fastLeakOn;
        private long _seedCounter = 70000;

        // Cycle-индексы для новых кнопок (по клику сдвигаются на 1).
        private int _weaponCycleIdx;
        private int _armorCycleIdx;
        private int _formationTypeIdx = 3; // Gathering по умолчанию
        private int _formationSizeIdx = (int)FormationSize.Small;
        private int _formationLevel = 1;

        // Cycle-массивы.
        private static readonly string[] WeaponIds =
            { "dagger", "sword", "axe", "spear", "greatsword", "bow", "staff" };
        private static readonly string[] ArmorIds =
            { "armor_head", "armor_torso", "armor_arms", "armor_legs", "armor_feet", "armor_belt" };
        private static readonly FormationType[] FormationTypeCycle =
        {
            FormationType.Barrier, FormationType.Trap, FormationType.Amplification,
            FormationType.Suppression, FormationType.Gathering, FormationType.Detection,
            FormationType.Teleportation, FormationType.Summoning
        };
        private static readonly FormationSize[] FormationSizeCycle =
        {
            FormationSize.Small, FormationSize.Medium, FormationSize.Large,
            FormationSize.Great, FormationSize.Heavy
        };

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            BuildUI();
            Visible = false;
            GD.Print("[CheatPanel] Ready (F1 to toggle)");
        }

        private void BuildUI()
        {
            // Top-left corner, dark panel, small font.
            SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            OffsetLeft = 12;
            OffsetTop = 130;  // под Qi-баром (60) + label (78) + запас
            CustomMinimumSize = new Vector2(280, 0);
            MouseFilter = MouseFilterEnum.Stop;
            Visible = false;
            ZIndex = 50;

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.04f, 0.03f, 0.96f),
            };
            style.SetBorderWidthAll(1);
            style.SetBorderColor(new Color(0.85f, 0.55f, 0.15f, 0.9f));
            style.SetCornerRadiusAll(4);
            AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.OffsetLeft = 8; vbox.OffsetRight = -8;
            vbox.OffsetTop = 6; vbox.OffsetBottom = -6;
            vbox.AddThemeConstantOverride("separation", 3);
            AddChild(vbox);

            // === Заголовок ===
            var header = MakeLabel("⚡ ЧИТ-МЕНЮ (F1)", 14, new Color(0.98f, 0.7f, 0.25f));
            vbox.AddChild(header);

            var hint = MakeLabel("dev-only, #if DEBUG", 10, new Color(0.6f, 0.5f, 0.4f));
            vbox.AddChild(hint);

            vbox.AddChild(MakeSeparator());

            // === Уровень культивации ===
            vbox.AddChild(MakeLabel("▸ Уровень культивации:", 12, new Color(0.94f, 0.83f, 0.66f)));
            var levelRow = new HBoxContainer();
            levelRow.AddThemeConstantOverride("separation", 2);
            for (int lvl = 1; lvl <= 9; lvl++)
            {
                int captured = lvl;
                var btn = MakeButton($"L{lvl}", 28, () => OnSetLevel(captured));
                levelRow.AddChild(btn);
            }
            vbox.AddChild(levelRow);

            // === Ци ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Ци:", 12, new Color(0.94f, 0.83f, 0.66f)));
            var qiRow1 = new HBoxContainer();
            qiRow1.AddThemeConstantOverride("separation", 4);
            qiRow1.AddChild(MakeButton("Заполнить Ци", 130, OnFillQi));
            qiRow1.AddChild(MakeButton("+10 000 Ци", 130, OnAddQi));
            vbox.AddChild(qiRow1);

            var qiRow2 = new HBoxContainer();
            qiRow2.AddThemeConstantOverride("separation", 4);
            qiRow2.AddChild(MakeButton("Прорыв ▲", 264, OnBreakthrough));
            vbox.AddChild(qiRow2);

            // === Техники ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Техники:", 12, new Color(0.94f, 0.83f, 0.66f)));
            var techRow = new HBoxContainer();
            techRow.AddThemeConstantOverride("separation", 4);
            techRow.AddChild(MakeButton("Выдать 3 рандом", 130, OnGrantTechniques));
            techRow.AddChild(MakeButton("Очистить", 130, OnClearTechniques));
            vbox.AddChild(techRow);

            // === Камни Ци ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Камни Ци:", 12, new Color(0.94f, 0.83f, 0.66f)));
            vbox.AddChild(MakeButton("Выдать 3 камня", 264, OnGrantQiStones));

            // === Формации ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Формации:", 12, new Color(0.94f, 0.83f, 0.66f)));
            vbox.AddChild(MakeButton("Создать формацию Сбора", 264, OnCreateFormation));
            vbox.AddChild(MakeButton("Формация (cycle: тип/размер/уровень)", 264, OnCreateCycledFormation));
            _fastLeakButton = MakeButton("Утечка ×1 (выкл)", 264, OnToggleFastLeak);
            vbox.AddChild(_fastLeakButton);

            // === Phase F: Экипировка (оружие/броня/рандом) ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Экипировка:", 12, new Color(0.94f, 0.83f, 0.66f)));
            var equipRow = new HBoxContainer();
            equipRow.AddThemeConstantOverride("separation", 4);
            equipRow.AddChild(MakeButton("Оружие cycle", 88, OnGenerateWeapon));
            equipRow.AddChild(MakeButton("Броня cycle", 84, OnGenerateArmor));
            equipRow.AddChild(MakeButton("Рандом", 80, OnGenerateRandomEquip));
            vbox.AddChild(equipRow);
            vbox.AddChild(MakeButton("Оружие + зачарование", 264, OnGenerateEnchantedWeapon));

            // === Phase F: Расходники + зарядники ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Расходники:", 12, new Color(0.94f, 0.83f, 0.66f)));
            var consRow = new HBoxContainer();
            consRow.AddThemeConstantOverride("separation", 4);
            consRow.AddChild(MakeButton("Расходник", 130, OnGenerateConsumable));
            consRow.AddChild(MakeButton("Зарядник Ци", 130, OnGenerateCharger));
            vbox.AddChild(consRow);

            // === Phase F: Техника с привязкой формации ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Техника + формация:", 12, new Color(0.94f, 0.83f, 0.66f)));
            vbox.AddChild(MakeButton("Создать Combat-Formation + старт", 264, OnGrantTechniqueWithFormation));

            // === Phase F: Верификация (dump) ===
            vbox.AddChild(MakeSeparator());
            vbox.AddChild(MakeLabel("▸ Верификация:", 12, new Color(0.94f, 0.83f, 0.66f)));
            vbox.AddChild(MakeButton("Dump LevelBoundaries", 264, OnDumpBoundaries));
            vbox.AddChild(MakeButton("Подсчёт дублей техник", 264, OnCountDuplicates));

            // === Статус ===
            vbox.AddChild(MakeSeparator());
            _statusLabel = MakeLabel("Готов", 11, new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(_statusLabel);
        }

        // === Кнопки ===

        private void OnSetLevel(int level)
        {
            Qi?.SetCultivationLevel(level, 0);
            long maxQi = Qi?.MaxQi ?? 0;
            // Заполнить Ци до нового максимума (для теста).
            Qi?.AddQi(maxQi);
            SetStatus($"Уровень L{level}, Ци {Qi?.CurrentQi ?? 0}/{maxQi}");
        }

        private void OnFillQi()
        {
            if (Qi == null) return;
            long maxQi = Qi.MaxQi;
            long missing = maxQi - Qi.CurrentQi;
            if (missing <= 0)
            {
                SetStatus("Ци уже полный");
                return;
            }
            Qi.AddQi(missing);
            SetStatus($"Заполнено +{missing} Ци ({Qi.CurrentQi}/{maxQi})");
        }

        private void OnAddQi()
        {
            Qi?.AddQi(10_000);
            SetStatus($"+10000 → {Qi?.CurrentQi ?? 0}/{Qi?.MaxQi ?? 0}");
        }

        private void OnBreakthrough()
        {
            if (Qi == null) return;
            bool ok = Qi.TryBreakthrough();
            SetStatus(ok
                ? $"Прорыв! Теперь L{(int)Qi.CultivationLevel}.{Qi.SubLevel}"
                : "Прорыв невозможен (недостаточно Ци/уровень)");
        }

        private void OnGrantTechniques()
        {
            if (TechniqueGenerator == null || Techniques == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            int granted = 0;
            // Цикл по пулу активных типов (как TechniqueGrantPhase).
            var types = new[]
            {
                TechniqueType.Combat, TechniqueType.Defense, TechniqueType.Healing,
                TechniqueType.Movement, TechniqueType.Support, TechniqueType.Sensory,
            };
            long seed = DateTime.UtcNow.Ticks;
            for (int i = 0; i < 3; i++)
            {
                var type = types[i % types.Length];
                var tech = TechniqueGenerator.GenerateSpecified(type, level, level, seed + i);
                if (tech != null && Techniques.LearnTechnique(tech))
                    granted++;
            }
            SetStatus($"Выдано {granted}/3 техник (L{level})");
        }

        private void OnClearTechniques()
        {
            Techniques?.ForgetAll();
            SetStatus("Все техники забыты");
        }

        private void OnGrantQiStones()
        {
            if (Inventory == null || ItemDatabase == null) return;
            // Случайно 3 камня из 10 канонических.
            var ids = QiStoneSeeder.AllItemIds();
            if (ids.Count == 0) return;
            // Убедимся, что камни зарегистрированы в БД.
            QiStoneSeeder.Seed(ItemDatabase);

            var rng = new System.Random((int)DateTime.UtcNow.Ticks);
            int granted = 0;
            for (int i = 0; i < 3; i++)
            {
                string id = ids[rng.Next(ids.Count)];
                if (ItemDatabase.TryGetItem(id, out var item))
                {
                    if (Inventory.TryAddItem(item, 1)) granted++;
                }
            }
            SetStatus($"Выдано {granted}/3 камней Ци");
        }

        private void OnCreateFormation()
        {
            if (FormationGenerator == null || Formations == null || Player == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            // Gathering-формация (сбор Ци — полезна для теста медитации).
            var formData = FormationGenerator.GenerateSpecified(
                FormationType.Gathering, FormationSize.Small, level, _seedCounter++);
            if (formData == null)
            {
                SetStatus("Не удалось сгенерировать формацию");
                return;
            }
            var pos = Player.Position;
            bool started = Formations.StartDrawing(formData.Id, Player.PlayerId, pos.X, pos.Y);
            if (!started)
            {
                SetStatus("StartDrawing неудачен (мало Ци / низкий уровень)");
                return;
            }
            // Мгновенно наполнить формацию для теста (как в TechniqueGrantPhase).
            long poolMax = Formations.QiPoolMax;
            if (poolMax > 0) Formations.ContributeQi(Player.PlayerId, poolMax);
            SetStatus($"Формация создана: {formData.DisplayName} (pool={Formations.QiPoolCurrent}/{poolMax})");
        }

        private void OnToggleFastLeak()
        {
            if (FormationCfg == null) return;
            _fastLeakOn = !_fastLeakOn;
            FormationCfg.DrainSpeedMultiplier = _fastLeakOn ? 10.0f : 1.0f;
            if (_fastLeakButton != null)
                _fastLeakButton.Text = _fastLeakOn ? "Утечка ×10 (вкл)" : "Утечка ×1 (выкл)";
            SetStatus($"Утечка формации ×{FormationCfg.DrainSpeedMultiplier}");
        }

        // === Phase F: Экипировка ===

        private void OnGenerateWeapon()
        {
            if (EquipmentGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            string subtype = WeaponIds[_weaponCycleIdx % WeaponIds.Length];
            _weaponCycleIdx++;
            var weapon = EquipmentGenerator.GenerateWeapon(level, subtype, _seedCounter++);
            if (weapon == null) { SetStatus("Оружие не сгенерировано"); return; }
            bool added = Inventory.TryAddItem(weapon, 1);
            SetStatus($"Оружие: {weapon.NameRu} ({weapon.Grade}, T{weapon.MaterialTier}, dmg={weapon.Damage}) — в инвентарь: {added}");
        }

        private void OnGenerateArmor()
        {
            if (EquipmentGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            string subtype = ArmorIds[_armorCycleIdx % ArmorIds.Length];
            _armorCycleIdx++;
            var armor = EquipmentGenerator.GenerateArmor(level, subtype, _seedCounter++);
            if (armor == null) { SetStatus("Броня не сгенерирована"); return; }
            bool added = Inventory.TryAddItem(armor, 1);
            SetStatus($"Броня: {armor.NameRu} ({armor.Grade}, def={armor.Defense}, cov={armor.Coverage:F0}%) — в инвентарь: {added}");
        }

        private void OnGenerateRandomEquip()
        {
            if (EquipmentGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            var item = EquipmentGenerator.GenerateRandom(level, _seedCounter++);
            if (item == null) { SetStatus("Рандом не сгенерирован"); return; }
            bool added = Inventory.TryAddItem(item, 1);
            SetStatus($"Рандом: {item.NameRu} ({item.Grade}, slot={item.Slot}) — в инвентарь: {added}");
        }

        private void OnGenerateEnchantedWeapon()
        {
            if (EquipmentGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            var weapon = EquipmentGenerator.GenerateWeapon(level, "sword", _seedCounter++);
            if (weapon == null) { SetStatus("Оружие не сгенерировано"); return; }
            bool enchanted = EquipmentGenerator.TryApplyEnchant(weapon, null, _seedCounter++);
            bool added = Inventory.TryAddItem(weapon, 1);
            SetStatus($"Оружие+зачар: {weapon.NameRu} | enchant={enchanted} | effects={weapon.SpecialEffects.Count} | в инвентарь: {added}");
        }

        // === Phase F: Расходники + зарядники ===

        private void OnGenerateConsumable()
        {
            if (ItemGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            var item = ItemGenerator.GenerateConsumableForLevel(level, _seedCounter++);
            if (item == null) { SetStatus("Расходник не сгенерирован"); return; }
            bool added = Inventory.TryAddItem(item, 1);
            SetStatus($"Расходник: {item.NameRu} (stack={item.MaxStack}, wt={item.Weight:F1}) — в инвентарь: {added}");
        }

        private void OnGenerateCharger()
        {
            if (ItemGenerator == null || Inventory == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            var charger = ItemGenerator.GenerateChargerForLevel(level, _seedCounter++);
            if (charger == null) { SetStatus("Зарядник не сгенерирован"); return; }
            bool added = Inventory.TryAddItem(charger, 1);
            SetStatus($"Зарядник: {charger.NameRu} (slot={charger.Slot}) — в инвентарь: {added}");
        }

        // === Phase F: Техника с привязкой формации ===

        private void OnGrantTechniqueWithFormation()
        {
            if (TechniqueGenerator == null || Techniques == null || FormationGenerator == null
                || Formations == null || Player == null) return;
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            // 1. Сгенерировать Formation-технику.
            var tech = TechniqueGenerator.GenerateSpecified(
                TechniqueType.Formation, level, level, _seedCounter++);
            if (tech == null) { SetStatus("Техника не сгенерирована"); return; }
            bool learned = Techniques.LearnTechnique(tech);
            // 2. Сгенерировать формацию.
            var formData = FormationGenerator.GenerateSpecified(
                FormationType.Gathering, FormationSize.Small, level, _seedCounter++);
            if (formData == null) { SetStatus("Формация не сгенерирована"); return; }
            var pos = Player.Position;
            bool started = Formations.StartDrawing(formData.Id, Player.PlayerId, pos.X, pos.Y);
            long poolMax = Formations.QiPoolMax;
            if (poolMax > 0 && started) Formations.ContributeQi(Player.PlayerId, poolMax);
            SetStatus($"Tech+Form: техн={tech.NameRu} ({(learned ? "изучена" : "слот полон")}) | формация={formData.DisplayName} | started={started} | stage={Formations.CurrentStage}");
        }

        // === Phase F: Cycle-формация ===

        private void OnCreateCycledFormation()
        {
            if (FormationGenerator == null || Formations == null || Player == null) return;
            var ftype = FormationTypeCycle[_formationTypeIdx % FormationTypeCycle.Length];
            var fsize = FormationSizeCycle[_formationSizeIdx % FormationSizeCycle.Length];
            int level = _formationLevel;
            // Сдвиг цикла: один клик = следующий тип; при полном обороте типов → следующий размер.
            _formationTypeIdx++;
            if (_formationTypeIdx % FormationTypeCycle.Length == 0)
            {
                _formationSizeIdx++;
                if (_formationSizeIdx % FormationSizeCycle.Length == 0)
                    _formationLevel = Math.Min(9, _formationLevel + 1);
            }
            var formData = FormationGenerator.GenerateSpecified(ftype, fsize, level, _seedCounter++);
            if (formData == null) { SetStatus("Формация не сгенерирована"); return; }
            var pos = Player.Position;
            bool started = Formations.StartDrawing(formData.Id, Player.PlayerId, pos.X, pos.Y);
            long poolMax = Formations.QiPoolMax;
            if (poolMax > 0 && started) Formations.ContributeQi(Player.PlayerId, poolMax);
            SetStatus($"Формация: {formData.DisplayName} | started={started} | stage={Formations.CurrentStage} | pool={Formations.QiPoolCurrent}/{poolMax}");
        }

        // === Phase F: Верификация ===

        private void OnDumpBoundaries()
        {
            int level = Qi == null ? 1 : Math.Max(1, (int)Qi.CultivationLevel);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"L{level} границы для Combat-Common:");
            var b = CultivationGame.Core.Data.LevelBoundaries.TechniqueBoundsFor(level, TechniqueType.Combat, TechniqueGrade.Common);
            sb.AppendLine($"  capacity[{b.MinCapacity}..{b.MaxCapacity}] qi[{b.MinQiCost}..{b.MaxQiCost}] dmg[{b.MinDamage}..{b.MaxDamage}] overshoot={b.Overshoot}");
            sb.AppendLine($"L{level} границы для Combat-Transcendent:");
            var bt = CultivationGame.Core.Data.LevelBoundaries.TechniqueBoundsFor(level, TechniqueType.Combat, TechniqueGrade.Transcendent);
            sb.AppendLine($"  capacity[{bt.MinCapacity}..{bt.MaxCapacity}] dmg[{bt.MinDamage}..{bt.MaxDamage}] overshoot={bt.Overshoot}");
            GD.Print($"[CheatPanel] {sb}");
            SetStatus($"Dump в лог (см. консоль). Overshoot Common={b.Overshoot}, Transcendent={bt.Overshoot}");
        }

        private void OnCountDuplicates()
        {
            if (Dedup == null || TechniqueRegistry == null) return;
            int dups = Dedup.CountDuplicates(
                TechniqueRegistry.GetAll(),
                t => Modules.Generator.DeduplicationService.Fingerprint(t));
            SetStatus($"Дублей в TechniqueRegistry: {dups} (total={TechniqueRegistry.Count})");
        }

        // === Helpers ===

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.Text = msg;
            GD.Print($"[CheatPanel] {msg}");
            ToastPub?.Publish(new ToastShownEvent(msg, 2.0f));
        }

        private static Label MakeLabel(string text, int fontSize, Color color)
        {
            var lbl = new Label { Text = text };
            lbl.AddThemeFontSizeOverride("font_size", fontSize);
            lbl.AddThemeColorOverride("font_color", color);
            return lbl;
        }

        private static Button MakeButton(string text, int minWidth, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(minWidth, 24),
            };
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => onClick();
            return btn;
        }

        private static HSeparator MakeSeparator()
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 2);
            return sep;
        }
    }
}
#endif  // DEBUG
