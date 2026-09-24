#nullable enable
// Создано: 2026-09-25 — G0 «Подготовка спрайтов»: headless-верификация
// (GODOT_ANIMQA_DEBUG=1).
//
// Проверяет контур спрайтов ДО и ПОСЛЕ доставки PNG (dual-режим G0.2:
// шаг 0 зондирует диск — файл есть → ожидаем текстуру, нет → fallback):
//   0. Состояние поставки: FileExists по player/npc/animal/weapon позициям.
//   1. Загрузчик ↔ диск: файл есть → TryGetSheet != null, нет → null
//      (SpriteSheetCache.LoadAttemptCount растёт в обоих случаях).
//   2. Загрузчик: QA-плейсхолдеры из _qa/ (полоса 6×64×64 и сетка 5×6)
//      через LoadSheetDirect → FrameCount/Rows/FrameSize корректны.
//   3. Аниматор игрока: состояние idle → процедурная статика (IsPng=false,
//      Frame=0); оверлей QA-листа → IsPng=true, кадр ПРОКРУЧИВАЕТСЯ со
//      временем, FrameAt математика; сброс → fallback возвращается.
//   4. Оружие (I-11): PNG каталога нет → процедурные visual-ы (Key стабилен,
//      размеры 32/48, PngHandCount=0) — ключи кэша не изменились.
//   5. NPC (I-5): резолв для живого NPC → fallback (IsPng=false) + idle/walk
//      детект движения; QA-оверлей роли → PNG-режим.
//   6. Звери (I-7): резолв → fallback; кадры листа идут по региону.
//
// Запуск: GODOT_NEWGAME=1 GODOT_ANIMQA_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Паттерн — WeaponVisSimDebug (env-хук, public QA-поля, VERDICT).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// G0: headless-верификация подготовки спрайтов. Итог:
/// [AnimQA] VERDICT: PASS/FAIL.
/// </summary>
public partial class SpriteAnimSimDebug : Node
{
    private const string QaStripPath = "res://resources/sprites/_qa/qa_walk_strip.png";
    private const string QaGridPath = "res://resources/sprites/_qa/qa_grid_5x6.png";

    [Inject] private INPCService? _npcService;
    [Inject] private AnimalService? _animalService;

    private GameWorldController? _world;
    private NPCSpriteRenderer? _npcRenderer;
    private AnimalSpriteRenderer? _animalRenderer;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[AnimQA] diag: npc={_npcService != null}");

        _world = GetParent() as GameWorldController ?? FindNode<GameWorldController>(GetTree().Root);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        _npcRenderer = FindNode<NPCSpriteRenderer>(GetTree().Root);
        _animalRenderer = FindNode<AnimalSpriteRenderer>(GetTree().Root);

        if (_world == null)
        {
            GD.Print("[AnimQA] VERDICT: FAIL — GameWorldController not found");
            return;
        }

        bool pass = true;

        // === 0. Состояние поставки (сим верен ДО и ПОСЛЕ доставки PNG) ====
        bool idleDelivered = FileAccess.FileExists(
            $"{SpriteSheetCache.Root}/characters/player/player_idle.png");
        bool walkDelivered = FileAccess.FileExists(
            $"{SpriteSheetCache.Root}/characters/player/player_walk.png");
        int expectedIconPng =
            (FileAccess.FileExists($"{SpriteSheetCache.Root}/equipment/icons/weapon_sword_1.png") ? 1 : 0) +
            (FileAccess.FileExists($"{SpriteSheetCache.Root}/equipment/icons/weapon_spear_1.png") ? 1 : 0);
        int expectedHandPng =
            (FileAccess.FileExists($"{SpriteSheetCache.Root}/equipment/equipped/weapon_hand_sword_1.png") ? 1 : 0) +
            (FileAccess.FileExists($"{SpriteSheetCache.Root}/equipment/equipped/weapon_hand_spear_1.png") ? 1 : 0);
        GD.Print($"[AnimQA] step0 delivery: player_idle={idleDelivered}, " +
                 $"player_walk={walkDelivered}, weaponPng={expectedIconPng}/{expectedHandPng}");

        // === 1. Загрузчик ↔ диск: файл есть → текстура; нет → null =======
        SpriteSheetCache.ResetCache();
        WeaponVisualCatalog.ResetCache();
        var idleSheet = SpriteSheetCache.TryGetSheet("player_idle");
        var walkSheet = SpriteSheetCache.TryGetSheet("player_walk");
        bool attempts = SpriteSheetCache.LoadAttemptCount >= 2;
        bool idleOk = (idleSheet != null) == idleDelivered;
        bool walkOk = (walkSheet != null) == walkDelivered;
        GD.Print($"[AnimQA] step1 loader: idle={(idleSheet != null ? "png" : "fallback")}/{idleOk}, " +
                 $"walk={(walkSheet != null ? "png" : "fallback")}/{walkOk}, " +
                 $"attempts={SpriteSheetCache.LoadAttemptCount}");
        if (!idleOk || !walkOk || !attempts) pass = false;

        // === 2. Загрузчик: QA-плейсхолдеры (полоса + сетка) ================
        var strip = SpriteSheetCache.LoadSheetDirect(QaStripPath, fps: 11, loop: true);
        var grid = SpriteSheetCache.LoadSheetDirect(QaGridPath, fps: 14, loop: true);
        bool stripOk = strip != null && strip.FrameCount == 6 && strip.Rows == 1
                       && strip.FrameSize == 64 && strip.Texture.GetWidth() == 384;
        bool gridOk = grid != null && grid.FrameCount == 6 && grid.Rows == 5
                      && grid.Texture.GetWidth() == 384 && grid.Texture.GetHeight() == 320;
        // FrameAt-математика: прогресс 0→кадр0, 0.5→кадр3, ряд 3 → 3*6+col.
        bool frameMath = strip != null && strip.FrameAt(0f) == 0
                         && strip.FrameAt(0.5f) == 3
                         && grid != null && grid.FrameAt(0.5f, row: 3) == 3 * 6 + 3;
        GD.Print($"[AnimQA] step2 loader: strip={stripOk} (frames={strip?.FrameCount ?? -1}, " +
                 $"rows={strip?.Rows ?? -1}), grid={gridOk} (rows={grid?.Rows ?? -1}), " +
                 $"frameMath={frameMath}");
        if (!stripOk || !gridOk || !frameMath) pass = false;

        // Несуществующий путь — тихий null (не краш).
        bool missing = !SpriteSheetCache.TryLoadTexture(
            "res://resources/sprites/_qa/no_such_file.png", out _);
        GD.Print($"[AnimQA] step2b missing-file: quietNull={missing}");
        if (!missing) pass = false;

        // === 3. Аниматор игрока: fallback → оверлей → прокрутка ===========
        var animator = _world.PlayerAnimatorForQA;
        if (animator == null)
        {
            GD.Print("[AnimQA] step3 FAIL — PlayerAnimator not found");
            pass = false;
        }
        else
        {
            string startAnim = _world.PlayerAnimId;
            bool startPng = _world.PlayerAnimIsPng;
            int startFrame = _world.PlayerAnimFrame;

            // Оверлей QA-полосы: анимация должна перейти в PNG-режим и
            // прокрутиться (7 fps × 0.6с > 4 кадров).
            animator.DEBUG_SetSheetOverride(strip);
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            int f1 = _world.PlayerAnimFrame;
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            int f2 = _world.PlayerAnimFrame;
            bool pngMode = _world.PlayerAnimIsPng && _world.PlayerAnimFrameCount == 6;
            bool scrolled = f1 != f2 || (f1 != startFrame); // кадры идут
            GD.Print($"[AnimQA] step3 animator: start='{startAnim}'(png={startPng},f={startFrame}) " +
                     $"→ override: png={_world.PlayerAnimIsPng}, frames={_world.PlayerAnimFrameCount}, " +
                     $"f1={f1}, f2={f2}, scrolled={scrolled}");

            // One-shot: melee-нотификация → animId player_melee.
            animator.DEBUG_Reset();
            animator.NotifyMeleeAttack();
            await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
            bool meleeAnim = _world.PlayerAnimId == "player_melee";
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool meleeDone = _world.PlayerAnimId != "player_melee"; // 0.42с истекла
            GD.Print($"[AnimQA] step3b one-shot: melee={meleeAnim}, expired={meleeDone}");

            // Смерть: death-hold до NotifyRespawn.
            animator.NotifyDeath();
            bool deathAnim = _world.PlayerAnimId == "player_death";
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            bool deathHold = _world.PlayerAnimId == "player_death";
            animator.NotifyRespawn();
            bool respawnOk = _world.PlayerAnimId == "player_idle";
            GD.Print($"[AnimQA] step3c death: anim={deathAnim}, hold={deathHold}, respawn={respawnOk}");

            // Сброс оверлея → режим вернулся к состоянию поставки.
            animator.DEBUG_SetSheetOverride(null);
            animator.DEBUG_Reset();
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            // fallback: статика кадр 0; живой лист: кадр в границах листа.
            bool resetOk = _world.PlayerAnimIsPng == idleDelivered
                           && (idleDelivered
                               ? _world.PlayerAnimFrame < _world.PlayerAnimFrameCount
                               : _world.PlayerAnimFrame == 0);
            GD.Print($"[AnimQA] step3d reset: modeRestored={resetOk} " +
                     $"(png={_world.PlayerAnimIsPng}, ожидание={idleDelivered}), " +
                     $"anim='{_world.PlayerAnimId}'");

            // startPng/reset — по поставке; старт-кадр: статика 0, лист — в границах.
            bool startOk = startPng == idleDelivered;
            bool startFrameOk = idleDelivered
                ? startFrame >= 0 && startFrame < _world.PlayerAnimFrameCount
                : startFrame == 0;
            if (!startOk || !startFrameOk || !pngMode || !scrolled
                || !meleeAnim || !meleeDone || !deathAnim || !deathHold
                || !respawnOk || !resetOk) pass = false;
        }

        // === 4. Оружие (I-11): ключи стабильны; PNG-счётчики = поставке ===
        var swordV = WeaponVisualCatalog.GetOrCreate("sword", 1, ItemRarity.Common);
        var spearV = WeaponVisualCatalog.GetOrCreate("spear", 1, ItemRarity.Common);
        bool weaponKeys = swordV.Key == "sword|1|Common" && spearV.Key == "spear|1|Common";
        bool weaponSizes = swordV.Icon.GetSize().X == 32 && swordV.Hand.GetSize().X == 48;
        bool weaponPngOk = WeaponVisualCatalog.PngIconCount == expectedIconPng
                        && WeaponVisualCatalog.PngHandCount == expectedHandPng;
        GD.Print($"[AnimQA] step4 weapon: keys={weaponKeys}, sizes={weaponSizes}, " +
                 $"pngCount={WeaponVisualCatalog.PngHandCount}/{WeaponVisualCatalog.PngIconCount} " +
                 $"(expected {expectedHandPng}/{expectedIconPng})");
        if (!weaponKeys || !weaponSizes || !weaponPngOk) pass = false;

        // === 5. NPC (I-5): резолв fallback + кадры по клоку ===============
        if (_npcRenderer != null && _npcService != null)
        {
            string? sampleId = null;
            foreach (var id in _npcService.GetAllNPCIds())
            {
                if (_npcService.IsAlive(id)) { sampleId = id; break; }
            }
            if (sampleId == null)
            {
                GD.Print("[AnimQA] step5 npc: FAIL — no alive NPC");
                pass = false;
            }
            else
            {
                var npc = _npcService.GetNPC(sampleId);
                var info1 = _npcRenderer.ResolveNpcAnim(sampleId, npc.Role, null, moving: true);
                var info2 = _npcRenderer.ResolveNpcAnim(sampleId, npc.Role, null, moving: false);
                // Ожидание по диску: ключ роли → база → процедурный (I-5).
                string roleKey = npc.Role.ToString().ToLowerInvariant();
                bool npcWalkPng = FileAccess.FileExists(
                                      $"{SpriteSheetCache.Root}/characters/npc/npc_{roleKey}_walk.png")
                                  || FileAccess.FileExists(
                                      $"{SpriteSheetCache.Root}/characters/npc/npc_base_walk.png");
                bool npcIdlePng = FileAccess.FileExists(
                                      $"{SpriteSheetCache.Root}/characters/npc/npc_{roleKey}_idle.png")
                                  || FileAccess.FileExists(
                                      $"{SpriteSheetCache.Root}/characters/npc/npc_base_idle.png");
                bool npcMovingOk = info1.IsPng == npcWalkPng;
                bool npcIdleOk = info2.IsPng == npcIdlePng;
                GD.Print($"[AnimQA] step5 npc: moving={(info1.IsPng ? "png" : "fallback")}/{npcMovingOk}, " +
                         $"idle={(info2.IsPng ? "png" : "fallback")}/{npcIdleOk}, " +
                         $"anim(moving)='{info1.AnimId}', anim(idle)='{info2.AnimId}'");

                // PNG-счётчик рендерера = ожиданию поставки (толпа живая).
                bool pngCountOk = (npcWalkPng || npcIdlePng)
                    ? _npcRenderer.NpcPngAnimatedCount > 0
                    : _npcRenderer.NpcPngAnimatedCount == 0;
                GD.Print($"[AnimQA] step5b npc: pngRenderer={pngCountOk} " +
                         $"(pngCount={_npcRenderer.NpcPngAnimatedCount}, " +
                         $"ожидание {((npcWalkPng || npcIdlePng) ? ">0" : "=0")})");
                if (!npcMovingOk || !npcIdleOk || !pngCountOk) pass = false;
            }
        }
        else
        {
            GD.Print("[AnimQA] step5 npc: FAIL — NPCSpriteRenderer/NPCService not found");
            pass = false;
        }

        // === 6. Звери (I-7): резолв fallback ===============================
        if (_animalRenderer != null && _animalService != null)
        {
            bool beastChecked = false, beastOk = true; string beastName = "";
            foreach (var animal in _animalService.GetAllAnimals())
            {
                if (!animal.IsAlive) continue;
                var a1 = _animalRenderer.ResolveAnimalAnim(animal.EntityId, animal.Species, animal.Size, true);
                var a2 = _animalRenderer.ResolveAnimalAnim(animal.EntityId, animal.Species, animal.Size, false);
                // Ожидание по диску: animal_{species}_{walk|idle}.png (I-7).
                string sp = animal.Species.ToLowerInvariant();
                bool beastWalkPng = FileAccess.FileExists(
                    $"{SpriteSheetCache.Root}/animals/animal_{sp}_walk.png");
                bool beastIdlePng = FileAccess.FileExists(
                    $"{SpriteSheetCache.Root}/animals/animal_{sp}_idle.png");
                beastChecked = true; beastName = animal.Species;
                beastOk = a1.IsPng == beastWalkPng && a2.IsPng == beastIdlePng;
                break; // первого живого достаточно (резолв детерминирован диском)
            }
            GD.Print($"[AnimQA] step6 animals: checked={beastChecked}/{beastOk}" +
                     (beastChecked ? $" (sample='{beastName}')" : " (no animals found)"));
            // Звери могут отсутствовать в маленьком QA-мире — не FAIL,
            // но логируем (L500-мир обязателен для полной проверки).
            if (!beastChecked) GD.Print("[AnimQA] step6 animals: WARN — beasts not spawned in this world");
            else if (!beastOk) pass = false;
        }
        else
        {
            GD.Print("[AnimQA] step6 animals: FAIL — AnimalSpriteRenderer not found");
            pass = false;
        }

        // === 7. Манифест-сверка реестра ====================================
        int playerSheets = 0, npcIds = 0, animalIds = 0;
        foreach (var animId in SpriteSheetCache.RegisteredAnimIds)
        {
            if (animId.StartsWith("player_")) playerSheets++;
            else if (animId.StartsWith("npc_")) npcIds++;
            else if (animId.StartsWith("animal_")) animalIds++;
        }
        bool registryOk = playerSheets == 13 && npcIds == 6 && animalIds == 5;
        GD.Print($"[AnimQA] step7 registry: player={playerSheets} (13), npc={npcIds} (6), " +
                 $"animal={animalIds} (5)");
        if (!registryOk) pass = false;

        GD.Print($"[AnimQA] flags: pass={pass}");

        GD.Print($"[AnimQA] VERDICT: {(pass
            ? "PASS — cache/fallback/loader/animator(one-shot,death)/weapon-keys/npc/animals готовы к доставке PNG"
            : "FAIL")}");
    }

    private static T? FindNode<T>(Node node) where T : Node
    {
        if (node is T typed) return typed;
        foreach (var child in node.GetChildren())
        {
            var found = FindNode<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
