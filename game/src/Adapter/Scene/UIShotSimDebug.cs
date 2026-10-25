#nullable enable
// Создано: 2026-10-02 — R38 (баг-репорт 02.10 №3/№5/№6): визуальные пруфы
// UI-окон после фиксов раунда.
//
// GODOT_UI_SHOT=prefix (Xvfb + opengl3) — скриншоты:
//   prefix_inventory.png  — инвентарь с доспавненными ЛЕГЕНДАРКАМИ
//                           (баг №3: контраст rarity-цветов на пергаменте);
//   prefix_technique.png  — книга техник (баг №6: блоки по типам без
//                           наложения, скролл);
//   prefix_character.png  — лист персонажа, вкладка «Характеристики»
//                           (баг №5: полные статы).
//
// Паттерн FormationShotSimDebug (R30): инъекция контейнера, доступ к окнам
// через *ForQA-геттеры GWC, Snap() через viewport, завершение Quit().
using Godot;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class UIShotSimDebug : Node
{
    [Inject] private IInventoryService Inventory = null!;
    [Inject] private IEquipmentGenerator EquipmentGenerator = null!;
    [Inject] private IQiService Qi = null!;
    // PT0-2: гранты в открытое окно книги (проверка вкладок после фикса).
    [Inject] private ITechniqueGeneratorService? TechniqueGenerator;
    [Inject] private Modules.Combat.TechniqueService? Techniques;
    // PT1 (плейтест-1, 25.10): пруфы фиксов — медитация×клик-движение,
    // walkability-гейт, меню сейвов с легаси-мусором.
    [Inject] private Core.Events.IPublisher<Core.Messaging.Contracts.MeditationToggleRequestedEvent>? MeditationTogglePub;
    [Inject] private ITileService? TilesForQA;
    [Inject] private ISaveService? SaveSvc;

    private GameWorldController? _gwc;

    public override void _Ready()
    {
        var shot = System.Environment.GetEnvironmentVariable("GODOT_UI_SHOT");
        if (string.IsNullOrEmpty(shot)) { QueueFree(); return; }
        var container = Scene.GameBoot.Container;
        if (container != null) ContainerAdapter.InjectProperties(this, container);
        _gwc = GetParent() as GameWorldController;
        GD.Print("[UIShot] Ready — скриншоты UI-окон (инвентарь/книга/лист) через 2с");
        _ = RunAsync(shot);
    }

    private async Task RunAsync(string prefix)
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        // ── Подготовка инвентаря: легендарки (rarity-палитра видна в полный рост).
        int level = Qi == null ? 1 : System.Math.Max(1, (int)Qi.CultivationLevel);
        long seed = 777;
        int added = 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryWeapon(level, "sword", seed++), 1) ? 1 : 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryArmor(level, "armor_torso", seed++), 1) ? 1 : 0;
        added += Inventory.TryAddItem(EquipmentGenerator.GenerateLegendaryArmor(level, "armor_feet", seed++), 1) ? 1 : 0;
        GD.Print($"[UIShot] доспавн легендарок для кадра: {added}/3");

        // ── №3: инвентарь (контраст золота на пергаменте).
        if (_gwc?.InventoryWindowForQA is { } inv)
        {
            inv.Toggle();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_inventory.png");
            inv.Toggle();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }

        // ── №6: книга техник (layout: блоки по типам друг под другом).
        // PT0-2 (плейтест-0, 24.10): +3 гранта В ОТКРЫТОЕ окно — OnLearned ×3
        // в одном кадре → RebuildTabs ×3; прежний QueueFree-паттерн на таком
        // повторе порождал вкладки «@VBoxContainer@N» (жалоба владельца).
        if (_gwc?.TechniqueBookWindowForQA is { } book)
        {
            book.Toggle();
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            if (TechniqueGenerator != null && Techniques != null)
            {
                int pt0Level = Qi == null ? 1 : System.Math.Max(1, (int)Qi.CultivationLevel);
                long pt0Seed = 424242;
                int pt0Granted = 0;
                var pt0Types = new[]
                {
                    CultivationGame.Core.Data.TechniqueType.Combat,
                    CultivationGame.Core.Data.TechniqueType.Defense,
                    CultivationGame.Core.Data.TechniqueType.Healing,
                };
                foreach (var t in pt0Types)
                {
                    var tech = TechniqueGenerator.GenerateSpecified(t, pt0Level, pt0Level, pt0Seed++);
                    if (tech != null && Techniques.LearnTechnique(tech)) pt0Granted++;
                }
                GD.Print($"[UIShot] PT0-2: грантов в открытое окно {pt0Granted}/3 — " +
                         $"RebuildTabs ×{pt0Granted} в одном кадре (проверка вкладок)");
            }
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_technique.png");
            book.Toggle();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        }

        // ── №5: лист персонажа, вкладка «Характеристики».
        if (_gwc?.CharacterSheetWindowForQA is { } sheet)
        {
            sheet.Toggle();
            sheet.SelectStatsTabForQA();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_character.png");
            sheet.Toggle();
        }

        // ── PT1-001 (плейтест-1 №2): медитация + КЛИК-движение → прерывание.
        // Прежний гейт ловил только клавиатурный moveVec — клик-цель шла мимо;
        // владелец ходил кликами при формации и медитация «не сбрасывалась».
        // План: включить медитацию → убедиться в активном кэше → поставить
        // клик-цель на +20px (в пределах текущего тайла — без влияния гейта
        // проходимости) → после кадров движения кэш обязан погаснуть.
        if (_gwc != null && MeditationTogglePub != null)
        {
            MeditationTogglePub.Publish(new Core.Messaging.Contracts.MeditationToggleRequestedEvent(true));
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout); // событие → кэш
            bool wasActive = _gwc.MeditationActiveForQA;
            var pos = _gwc.VisualPositionForQA;
            _gwc.SetMouseTargetForQA(new Vector2(pos.X + 20f, pos.Y)); // клик-движение, без клавиатуры
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout); // кадры движения
            bool interrupted = !_gwc.MeditationActiveForQA;
            GD.Print($"[UIShot] PT1-001: медитация включена={wasActive}; после клик-движения прервана={interrupted} " +
                     $"(ожидание: True/True; до фикса прерывание ловило только WASD — клик шёл мимо)");
        }

        // ── PT1-002 (плейтест-1 №7): walkability-гейт — глубокая вода/лава
        // непроходимы (владелец ходил по океану со «скоростью травы»).
        // План: найти в полигоне непроходимый тайл → гейт обязан не пустить
        // в его центр (осевое скольжение или стоп); проходимый сосед — пускать.
        if (_gwc != null && TilesForQA != null && TilesForQA.MapWidth > 0)
        {
            int blockedX = -1, blockedY = -1;
            for (int y = 0; y < TilesForQA.MapHeight && blockedX < 0; y++)
                for (int x = 0; x < TilesForQA.MapWidth; x++)
                    if (!TilesForQA.GetTile(x, y).IsWalkable) { blockedX = x; blockedY = y; break; }
            if (blockedX >= 0)
            {
                float t = GameConstants.TILE_PIXELS;
                var player = _gwc.VisualPositionForQA;
                // Игрок стоит на проходимом тайле — центр непроходимого далековат:
                // проверяем семантику гейта напрямую (candidate=центр воды, current=игрок).
                var waterCenter = new Vector2(blockedX * t + t / 2f, blockedY * t + t / 2f);
                var gated = _gwc.ApplyWalkabilityGateForQA(waterCenter, player);
                bool blocked = gated != waterCenter;
                // Контроль-ветвь: шаг на +20px в своём тайле — гейт обязан пропустить.
                var step = new Vector2(player.X + 20f, player.Y);
                bool passable = _gwc.ApplyWalkabilityGateForQA(step, player) == step;
                GD.Print($"[UIShot] PT1-002: непроходимый тайл ({blockedX},{blockedY}); гейт блокирует вход={blocked}; " +
                         $"шаг по проходимому пропускает={passable} (ожидание: True/True; до фикса — свободное плавание по океану)");
            }
            else
            {
                GD.Print("[UIShot] PT1-002: SKIP — в полигоне нет непроходимых тайлов (гейт проверен регрессией/ревью)");
            }
        }

        // ── PT1-003 (плейтест-1, PT-S02): меню сейвов — свежий ручной слот
        // СВЕРХУ, легаси-мусор (автосейвы старых билдов без save_meta) — на
        // дне; валидация при выборе ещё в UI-слое (скриншот фиксирует порядок).
        if (_gwc?.SaveLoadMenuForQA is { } saveMenu && SaveSvc != null)
        {
            // 1) Настоящий ручной слот (текущее состояние — валиден).
            bool savedSlot = SaveSvc.Save(new CultivationGame.Core.Data.SaveSlot("slot_pt1_proof", CultivationGame.Core.Data.SaveSlotType.Manual));
            // 2) Легаси-фантомы: файлы БЕЗ save_meta — как ротация autosave_000N
            //    прошлых билдов на диске владельца (PRE-REJECT на загрузке).
            var saveRoot = Godot.ProjectSettings.GlobalizePath("user://saves");
            var legacyNames = new[] { "autosave_0001", "autosave_0002", "autosave_0003", "autosave_0004" };
            foreach (var ln in legacyNames)
            {
                var dir = System.IO.Path.Combine(saveRoot, ln);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "main.json"), "{\"legacy\":\"no_meta\"}");
            }
            GD.Print($"[UIShot] PT1-003: ручной слот сохранён={savedSlot}; легаси-фантомов={legacyNames.Length}");

            saveMenu.Open(CultivationGame.Adapter.UI.SaveLoadMenuMode.Full);
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            Snap(prefix + "_savemenu.png");
            saveMenu.Close();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            // Чистим за собой: фантомы + proof-слот (не мусорим диску владельца/CI).
            foreach (var ln in legacyNames)
                SaveSvc.DeleteSave(new CultivationGame.Core.Data.SaveSlot(ln, CultivationGame.Core.Data.SaveSlotType.Manual));
            SaveSvc.DeleteSave(new CultivationGame.Core.Data.SaveSlot("slot_pt1_proof", CultivationGame.Core.Data.SaveSlotType.Manual));
            GD.Print("[UIShot] PT1-003: QA-слоты вычищены");
        }

        // ── PT2-000 (плейтест-2 №0): формация в книге — «Действие»-блок.
        // Репорт: «мне выпала формация молнии… в описании нет ничего по ней».
        // Грант-фаза старта УЖЕ выдала формацию (лимит 1) — берём ЕЁ
        // (та же, что видел владелец); свежесгенерированная — fallback.
        if (_gwc?.TechniqueBookWindowForQA is { } book2 && Techniques != null)
        {
            // Существующая формация игрока (грант при сборке мира) или новая.
            CultivationGame.Modules.Combat.LearnedTechnique? form = null;
            foreach (var kvp in Techniques.GetAllTechniques())
                if (kvp.Value.Type == CultivationGame.Core.Data.TechniqueType.Formation) { form = kvp.Value; break; }
            if (form == null && TechniqueGenerator != null)
            {
                int pt2Level = Qi == null ? 1 : System.Math.Max(1, (int)Qi.CultivationLevel);
                var fresh = TechniqueGenerator.GenerateSpecified(
                    CultivationGame.Core.Data.TechniqueType.Formation, pt2Level, pt2Level, 909090);
                if (fresh != null && Techniques.LearnTechnique(fresh))
                    form = Techniques.GetTechnique(fresh.TechniqueId);
            }
            GD.Print($"[UIShot] PT2-000: формация id={form?.TechniqueId ?? "—"}, kind={form?.FormationKind ?? -1}, " +
                     $"desc=\"{form?.Description ?? "?"}\"");
            if (form != null)
            {
                // Книга ЗАКРЫТА сценарием №6. Порядок: открыть (refresh со
                // старым выбором) → SelectTechnique (OnSelectionChanged при
                // видимом окне обновит _selectedTechId) → закрыть-открыть
                // (Open → полный refresh деталей УЖЕ с формацией) → Snap.
                book2.Toggle(); // открыть
                await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
                Techniques.SelectTechnique(form.TechniqueId);
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                book2.Toggle(); // закрыть
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                book2.Toggle(); // открыть → refresh деталей с формацией
                await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
                Snap(prefix + "_formation.png");
                book2.Toggle();
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            }
        }

        GD.Print("[UIShot] done");
        GetTree().Quit();
    }

    private void Snap(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(path);
        GD.Print($"[UIShot] shot → {path} ({img.GetWidth()}x{img.GetHeight()})");
    }
}
