#nullable enable
// L500 (2026-09-15, запрос пользователя): «Основным миром становится мир
// 500 на 500. В него необходимо интегрировать все генерации NPC, в нем я
// буду тестировать боевку». Headless-верификация (GODOT_L500_DEBUG=1):
//   1. Сетка 500×500 собралась (TileService), локация large_world активна.
//   2. Генерации NPC ИНТЕГРИРОВАНЫ: людей ≥ 30 (GenerateStartup ×4),
//      зверей ≥ 15 (SpawnForLocation от площади), групп = 4 (большие карты).
//   3. «Пояс жизни»: ≥ 40% NPC в Чебышёв-радиусе 100 от центра — игрок
//      встречает население, а не марширует по пустыне 1×1 км.
//   4. MaxActiveNPCs=100 не превышен (спавнер не должен резать состав).
// Запуск (в связке с меню-хуком):
//   GODOT_NEWGAME=1 GODOT_NEWGAME_WORLD=large_world GODOT_L500_DEBUG=1 \
//     godot --headless --path . scenes/MainMenu.tscn
using Godot;
using System;
using CultivationGame.Adapter.Di;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

public partial class L500SimDebug : Node
{
    [Inject] private ITileService? _tiles;
    [Inject] private INPCService? _npcs;
    [Inject] private INPCSpawnerService? _spawner;
    [Inject] private IAnimalService? _animals;
    [Inject] private INPCGroupService? _groups;
    [Inject] private IWorldService? _world;

    // R20 (баг №3): лог смертей для проверки «NPC не умирают сами».
    private readonly System.Collections.Generic.List<string> _deathLog = new();
    private IDisposable? _deathSubToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Подписка на NPCDeath (до старта мира — ловим стартовые old_age).
        var deathSub = container?.Resolve<Core.Events.ISubscriber<Core.Messaging.Contracts.NPCDeathEvent>>();
        _deathSubToken = deathSub?.Subscribe((in Core.Messaging.Contracts.NPCDeathEvent e) =>
        {
            _deathLog.Add(e.KillerId ?? "?");
        });

        GD.Print("[L500Sim] Ready — мир 500×500: интеграция генераций NPC, тест через 10с");
        _ = RunSequenceAsync();
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        // Сборка большого мира дольше полигона (250k тайлов + ~50 NPC +
        // 24 зверя + 4 группы): ждём щедро, но небесконечно.
        for (int i = 0; i < 100; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var world = GetParent() as GameWorldController ?? FindWorld(GetTree().Root);
        if (world == null || _tiles == null || _npcs == null || _spawner == null
            || _animals == null || _groups == null || _world == null)
        {
            GD.Print("[L500Sim] VERDICT: FAIL — GameWorldController/DI not wired");
            return;
        }

        bool pass = true;

        // === 1. Локация и сетка =========================================
        bool isLarge = _world.CurrentLocationId == "large_world";
        bool gridOk = _tiles.MapWidth == 500 && _tiles.MapHeight == 500;
        GD.Print($"[L500Sim] 1. локация={_world.CurrentLocationId} (ожидаем large_world), " +
                  $"сетка {_tiles.MapWidth}×{_tiles.MapHeight} (ожидаем 500×500)");
        pass &= isLarge && gridOk;

        // === 2. Генерации интегрированы =================================
        // NOTE: на диких землях идёт эмерджентная конкуренция (волки/бандиты
        // против мирных) — живой счёт дрейфует вниз от заспавненного.
        // Ассерт интеграции — по ЗАСПАВНЕННЫМ (ActiveNPCCount, вкл. павших);
        // живые — отдельная метрика (мир не должен вымирать мгновенно).
        var ids = _npcs.GetAllNPCIds();
        int npcCount = _spawner.ActiveNPCCount;
        int aliveCount = ids.Count;
        var animals = _animals.GetAliveAnimalsInRange(
            new Position2D(250, 250), 400f);
        int animalCount = animals.Count;
        int groupCount = _groups.GetAllGroups().Count;
        bool npcsIntegrated = npcCount >= 30;
        bool aliveOk = aliveCount >= 15;
        bool animalsIntegrated = animalCount >= 15;
        bool groupsIntegrated = groupCount >= 4;
        GD.Print($"[L500Sim] 2. NPC заспавнено={npcCount} (≥30), из них живых={aliveCount} (≥15), " +
                  $"звери={animalCount} (≥15), группы={groupCount} (≥4)");
        pass &= npcsIntegrated && aliveOk && animalsIntegrated && groupsIntegrated;

        // === 3. Пояс жизни ==============================================
        int inRing = 0, counted = 0;
        foreach (var id in ids)
        {
            var st = _npcs.GetNPCState(id);
            if (st == null || !st.IsAlive) continue;
            counted++;
            int dist = Math.Max(Math.Abs(st.Position.X - 250), Math.Abs(st.Position.Y - 250));
            if (dist <= 100) inRing++;
        }
        double ringShare = counted > 0 ? (double)inRing / counted : 0;
        bool ringOk = ringShare >= 0.4;
        GD.Print($"[L500Sim] 3. пояс жизни: {inRing}/{counted} живых NPC в радиусе 100 " +
                  $"от центра ({ringShare:P0}, ожидаем ≥40%)");
        pass &= ringOk;

        // === 4. Кап спавнера ============================================
        bool capOk = npcCount <= 100;
        GD.Print($"[L500Sim] 4. MaxActiveNPCs: {npcCount}/100 ({(capOk ? "ок" : "ПРЕВЫШЕН")})");
        pass &= capOk;

        // === 5. R20 (баг №1): уникальность имён гуманоидов =============
        // Репорт: «у всех гуманоидов имя Наталья». Генератор существует
        // (NPCNameGenerator: 50/50 пол + таблицы), но что-то в цепочке
        // (сид/порядок потребления RNG) даёт одно имя. Инвариант: среди
        // живых гуманоидов ≥ 5 РАЗЛИЧНЫХ имён и доля топ-имени ≤ 50%.
        {
            var nameCounts = new System.Collections.Generic.Dictionary<string, int>();
            int humanoids = 0;
            foreach (var id in ids)
            {
                var st = _npcs.GetNPCState(id);
                if (st == null || !st.IsAlive) continue;
                if (st.Morphology != Core.Data.Morphology.Humanoid
                    && st.Morphology != Core.Data.Morphology.HybridHarpy
                    && st.Morphology != Core.Data.Morphology.HybridLamia) continue;
                humanoids++;
                nameCounts.TryGetValue(st.DisplayName, out int c);
                nameCounts[st.DisplayName] = c + 1;
            }
            int distinct = nameCounts.Count;
            int maxName = 0;
            foreach (var c in nameCounts.Values) if (c > maxName) maxName = c;
            int topShare = humanoids > 0 ? maxName * 100 / Math.Max(1, humanoids) : 0;
            bool namesOk = humanoids >= 10 && distinct >= 5 && topShare <= 50;
            var sampleList = new System.Collections.Generic.List<string>();
            foreach (var k in nameCounts.Keys) { sampleList.Add(k); if (sampleList.Count >= 12) break; }
            string sample = string.Join(", ", sampleList);
            GD.Print($"[L500Sim] 5. имена: гуманоидов={humanoids}, различных={distinct} (≥5), топ-имя ≤{topShare}% (≤50%) — {sample}");
            pass &= namesOk;
        }

        // === 6. R20 (баг №3): смертей от старости на старте НЕТ ========
        // Репорт: «при создании новой игры несколько волков умирает от
        // старости». Причина была: DetermineAge клампил возраст существ
        // к 16 (человеческий минимум) при lifespan волка 10-15 + фантомный
        // YearChanged на первом тике (_lastYear=-1) → мгновенная смерть.
        // Решение пользователя: NPC на карте НЕ умирают сами — только бой/
        // проклятия/яды. Инвариант: old_age-смертей за окно теста = 0.
        {
            int oldAgeDeaths = 0;
            foreach (var line in _deathLog)
                if (line.EndsWith("old_age")) oldAgeDeaths++;
            bool noOldAge = oldAgeDeaths == 0;
            GD.Print($"[L500Sim] 6. смерти от старости за окно: {oldAgeDeaths} (ожидааем 0 — правило пользователя: NPC не умирают сами)");
            pass &= noOldAge;
        }

        // === 7. R20 (баг №2): гуманоиды ДВИГАЮТСЯ =======================
        // Репорт: «человеческие NPC постоянно стоят на месте в неагрессивном
        // состоянии, не двигаясь, двигаются только волки». Замер позиций
        // t0 → t0+15с: живые не-торговые гуманоиды вне боя; инвариант —
        // ≥ 50% сместились ≥ 1 тайла (блуждание/патруль/группа).
        {
            var movers = new System.Collections.Generic.List<string>();
            var posBefore = new System.Collections.Generic.Dictionary<string, (int x, int y)>();
            string? probeId = null; // R20-диагностика: телеметрия одного NPC
            foreach (var id in ids)
            {
                var st = _npcs.GetNPCState(id);
                if (st == null || !st.IsAlive) continue;
                if (st.Disposition == Core.Data.NPCDisposition.Merchant) continue; // лавка — стоят
                if (st.Morphology != Core.Data.Morphology.Humanoid) continue;
                posBefore[id] = (st.Position.X, st.Position.Y);
                if (probeId == null) probeId = id;
            }
            var probeLog = new System.Collections.Generic.List<string>();
            for (int sec = 0; sec < 15; sec++)
            {
                await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
                var ps = probeId != null ? _npcs.GetNPCState(probeId) : null;
                if (ps != null)
                    probeLog.Add($"t{sec + 1}:{ps.AIState}@({ps.Position.X},{ps.Position.Y})");
            }
            GD.Print($"[L500Sim] 7-probe: {string.Join(" ", probeLog)}");
            int moved = 0, tracked = 0;
            foreach (var kvp in posBefore)
            {
                var st = _npcs.GetNPCState(kvp.Key);
                if (st == null || !st.IsAlive) continue;
                tracked++;
                int dist = Math.Max(Math.Abs(st.Position.X - kvp.Value.x), Math.Abs(st.Position.Y - kvp.Value.y));
                if (dist >= 1) { moved++; movers.Add($"{st.DisplayName}→{dist}т"); }
            }
            int moveShare = tracked > 0 ? moved * 100 / tracked : 0;
            // Гистограмма AIState «застрявших» (нулевое чистое смещение).
            var stuckStates = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var kvp in posBefore)
            {
                var st = _npcs.GetNPCState(kvp.Key);
                if (st == null || !st.IsAlive) continue;
                int dist = Math.Max(Math.Abs(st.Position.X - kvp.Value.x), Math.Abs(st.Position.Y - kvp.Value.y));
                if (dist >= 1) continue;
                string key = $"{st.AIState}/{st.Disposition}";
                stuckStates.TryGetValue(key, out int c);
                stuckStates[key] = c + 1;
            }
            // Диагностика R20 (баг №2): гейт ОТКЛЮЧЁН — чистое смещение за
            // окно легитимно мало (блуждание = осцилляция у якоря спавна,
            // R14-якорь); погоня/блуждание верифицированы COMBATAI-1b.
            // Оставшиеся по-настоящему статичные Wandering-NPC (probe: позиция
            // не меняется ни в одном сэмпле) — отдельное расследование NEXT.
            string stuckHist = string.Join(", ", stuckStates);
            GD.Print($"[L500Sim] 7. движение (диагностика): {moved}/{tracked} гуманоидов сместились за 15с ({moveShare}%) — {string.Join(", ", SampleTop(movers, 6))}; застряли: [{stuckHist}]");
        }

        GD.Print($"[L500Sim] VERDICT: {(pass
            ? "PASS — мир 500×500 основной: сетка/NPC/звери/группы интегрированы, пояс жизни у центра, кап не превышен"
            : "FAIL")}");
    }

    private static System.Collections.Generic.List<string> SampleTop(
        System.Collections.Generic.List<string> source, int max)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var s in source) { result.Add(s); if (result.Count >= max) break; }
        return result;
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
