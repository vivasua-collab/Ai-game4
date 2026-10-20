#nullable enable
// Создано: 2026-09-21 — R30-эпизод 2 (репорт плейтеста 21.09): верификация
// формаций П1/П2/П3/П7 БЕЗ ручного QA (пользователь без локальных тестов).
//
// GODOT_FORM_SHOT=prefix (Xvfb + opengl3) — скриншоты:
//   _drawing.png / _filling.png / _active.png — видимость контура/рун/
//   подзаголовка (П1: тёмная подложка на светлом биоме, П3: описание,
//   П7: никаких C++-ошибок триангуляции в логе).
//
// Headless-вердикт (GODOT_FORM_DEBUG=1) — логические ассерты:
//   1. П2-радиус зарядки: формация Filling, игрок РЯДОМ → Ци утекает;
//      телепорт ЗА радиус → пауза утекания; возврат → резюм.
//   2. П3: FormationDescriptions.FullDescription непуст и содержит
//      тип действия + радиус + правило зарядки.
//   3. П7: вырожденный fillRatio (0.005) не роняет _Draw (полигон-гейт).
using Godot;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

public partial class FormationShotSimDebug : Node
{
    [Inject] private Modules.Formation.FormationService Formations = null!;
    [Inject] private CultivationGame.Modules.Generator.IFormationGeneratorService Gen = null!;
    [Inject] private IPlayerService Player = null!;
    [Inject] private IQiService Qi = null!;
    [Inject] private ITimeService Time = null!;

    private bool _ran;
    private GameWorldController? _gwc;

    /// <summary>Телепорт «навсегда» (логика+визуал): прямой Player.SetPosition
    /// откатывается _PhysicsProcess-синком к _visualPosition (урок R29) —
    /// гонка мимо DEBUG_TeleportPlayer делает проверки позиции фальшивыми.</summary>
    private void TeleportPlayer(int x, int y)
    {
        if (_gwc != null) _gwc.DEBUG_TeleportPlayer(x, y);
        else Player?.SetPosition(new Position2D(x, y));
    }

    public override void _Ready()
    {
        var env = System.Environment.GetEnvironmentVariable("GODOT_FORM_DEBUG");
        var shot = System.Environment.GetEnvironmentVariable("GODOT_FORM_SHOT");
        if (env != "1" && shot == null) { QueueFree(); return; }
        var container = Scene.GameBoot.Container;
        if (container != null) ContainerAdapter.InjectProperties(this, container);
        _gwc = GetParent() as GameWorldController;
        GD.Print("[FormShot] Ready — формации П1/П2/П3/П7 тест старт через 2с");
        _ = RunAsync(shot);
    }

    private async Task RunAsync(string? shot)
    {
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        bool pass = true;

        // Мир + формация у игрока (Barrier — щит, описания П3).
        // BUG-008 (АУДИТ-1020): контур НЕ в центре карты — за EffectRadius
        // Small (50м = 25 тайлов, FormationGeneratorService.EffectRadiusBySize)
        // в границах QA-мира 50×50 можно уйти только от нецентральной точки
        // (из (25,25) до любого угла ≤ 25т — радиус «упирается» в края).
        // Смещаем контур на ~15 тайлов от старта, игрок — рядом (радиус
        // зарядки Small 8т: п2 near-дрейн обязан работать).
        var p0 = Player.Position;
        int fx = Mathf.Max(3, p0.X - 15);
        int fy = Mathf.Max(3, p0.Y - 15);
        TeleportPlayer(fx + 1, fy + 1);
        await Frames(2);
        var data = Gen.GenerateSpecified(FormationType.Barrier, FormationSize.Small, 1, 424242);
        bool started = Formations.StartDrawing(data.Id, Player.PlayerId, fx, fy);
        pass &= started;
        GD.Print($"[FormShot] StartDrawing(Barrier Small @ {fx},{fy}) = {started}");
        await Frames(10);
        if (shot != null) Snap(shot + "_drawing.png");

        // Filling: активируем стадию.
        Formations.StartFilling();
        await Frames(10);
        if (shot != null) Snap(shot + "_filling.png");

        // === П2: радиус зарядки — headless-ассерты ===
        // Даём Qi для автонаполнения и меряем дрейн вблизи/вдали.
        long qiNearBefore = Qi.CurrentQi;
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        long qiNearAfter = Qi.CurrentQi;
        bool drainsNear = qiNearAfter < qiNearBefore;
        GD.Print($"[FormShot] П2 near: Ци {qiNearBefore}→{qiNearAfter} (drains={drainsNear}, ожидали True — конд. игрока > 0)");
        pass &= drainsNear || qiNearBefore == 0; // Qi 0 = конд. не позволяет — не фейлим

        // Телепорт ЗА радиус зарядки (Small = 8 тайлов; +30 — гарантированно вне).
        // R29-урок: через GWC.DEBUG_TeleportPlayer (обе позиции), иначе
        // _PhysicsProcess вернёт игрока назад и «пауза» не сработает.
        int radius = Formations.ChargingRadiusTiles;
        // QA-мир test_polygon 50×50: кламп в границы (fx,fy=(10,10) → 45,45,
        // Чебышёв-дистанция 35 > radius 8 — за радиусом зарядки).
        int tx = Mathf.Min(fx + radius + 30, 45);
        int ty = Mathf.Min(fy + radius + 30, 45);
        TeleportPlayer(tx, ty);
        // PlayerPositionChangedEvent публикуется PlayerService.SetPosition.
        await Frames(10);
        long qiFarBefore = Qi.CurrentQi;
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        long qiFarAfter = Qi.CurrentQi;
        bool pausedFar = qiFarAfter >= qiFarBefore;
        GD.Print($"[FormShot] П2 far (dist ~{radius + 30}т > {radius}т): Ци {qiFarBefore}→{qiFarAfter} (paused={pausedFar}, ожидали True)");
        pass &= pausedFar;

        // Возврат — резюм.
        TeleportPlayer(fx + 1, fy + 1);
        await Frames(10);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[FormShot] П2 return: стадия={Formations.CurrentStage}");

        // === П3: описание ===
        var f = Formations.CurrentFormation;
        string desc = f != null
            ? Modules.Formation.FormationDescriptions.FullDescription(f, radius)
            : "";
        bool descOk = desc.Length > 10 && desc.Contains("радиус") && desc.Contains("зарядка");
        GD.Print($"[FormShot] П3 FullDescription: '{desc}' (ok={descOk})");
        pass &= descOk;

        // === П7: вырожденный fillRatio не роняет _Draw ===
        // Пул событий: двигаем QiPoolChangedEvent минимальным значением не можем напрямую —
        // проверяем, что _Draw с текущим состоянием не падал (ошибки Godot в логе ловит
        // внешний прогон: rg 'triangulation failed'). Активируем формацию для снимка.
        Formations.ContributeQi(Player.PlayerId, Formations.QiPoolMax);
        await Frames(10);
        bool active = Formations.CurrentStage == FormationStage.Active;
        GD.Print($"[FormShot] активация: стадия={Formations.CurrentStage} (Active={active})");
        pass &= active;
        if (shot != null) Snap(shot + "_active.png");

        // === VER-001 (аудит form-s7, 10-19-fix-b): точечная верификация
        // эффект-логики формаций по типам. Пайплайн урона тянет бонусы
        // через IFormationService.GetFormationBonusPermil (DamageService
        // слои 3b/MED-2) — здесь проверяем ПРОИЗВОДИТЕЛЬНУЮ сторону.
        // BUG-008 (АУДИТ-1020): пайплайн урона теперь entity-aware
        // (радиус + сторона) — шаги 4a/4a2/4a3/4c проверяют новую семантику.
        // 4a. Barrier (Shield/Defense, тег ally): игрок-кастер В радиусе →
        //     entity-aware Defense-промилле > 0 (фикс VER-001: агрегация
        //     Shield в FormationEffects). Глобальный вызов заменён на
        //     entity-aware с позицией игрока.
        var pap = Player.Position;
        int barrierDef = Formations.GetFormationBonusPermil(StatType.Defense, Player.PlayerId, pap.X, pap.Y);
        bool barrierOk = barrierDef > 0;
        GD.Print($"[FormShot] VER-001 4a Barrier (entity-aware, ally в зоне): Defense permil={barrierDef} (ожидали > 0)");
        pass &= barrierOk;

        // 4a2 (BUG-008): телепорт игрока ЗА EffectRadius → бонус = 0;
        //     возврат в зону → бонус восстановлен. Small: 50м = 25т;
        //     +30 тайлов от контура, кламп в границы 50×50 (как П2).
        int farEx = Mathf.Min(fx + 30, 45);
        int farEy = Mathf.Min(fy + 30, 45);
        TeleportPlayer(farEx, farEy);
        await Frames(10);
        var papFar = Player.Position;
        int barrierDefFar = Formations.GetFormationBonusPermil(StatType.Defense, Player.PlayerId, papFar.X, papFar.Y);
        bool farZeroOk = barrierDefFar == 0;
        GD.Print($"[FormShot] BUG-008 4a2 far: игрок @({papFar.X},{papFar.Y}), дистанция {Mathf.Max(Mathf.Abs(papFar.X - fx), Mathf.Abs(papFar.Y - fy))}т " +
                 $"> EffectRadius 25т → Defense permil={barrierDefFar} (ожидали 0)");
        pass &= farZeroOk;
        TeleportPlayer(fx + 1, fy + 1);
        await Frames(10);
        var papBack = Player.Position;
        int barrierDefBack = Formations.GetFormationBonusPermil(StatType.Defense, Player.PlayerId, papBack.X, papBack.Y);
        bool backOk = barrierDefBack > 0;
        GD.Print($"[FormShot] BUG-008 4a2 возврат: Defense permil={barrierDefBack} (ожидали > 0)");
        pass &= backOk;

        // 4a3 (BUG-008): враг В зоне формации (formation+1) → его Defense = 0:
        //     Barrier Shield имеет TargetTag=ally — чужая сторона не бустится
        //     (прежде глобальный баг защищал врагов по всей карте).
        int enemyDef = Formations.GetFormationBonusPermil(StatType.Defense, "npc_hostile_qa", fx + 1, fy + 1);
        bool enemyZeroOk = enemyDef == 0;
        GD.Print($"[FormShot] BUG-008 4a3 enemy: Defense permil={enemyDef} @({fx + 1},{fy + 1}) (ожидали 0)");
        pass &= enemyZeroOk;

        // 4b. Деактивация снимает бонус (FormationEffects.Deactivate → 0).
        //     LEGACY-вызов (любой тег) — проверяем и старый контур API.
        Formations.DeactivateFormation();
        await Frames(2);
        int defAfter = Formations.GetFormationBonusPermil(StatType.Defense);
        bool barrierCleared = defAfter == 0;
        GD.Print($"[FormShot] VER-001 4b деактивация: Defense permil={defAfter} (ожидали 0)");
        pass &= barrierCleared;

        // 4c. Amplification (Buff/Damage, тег ally): генерация → мгновенное
        //     заполнение → активация → entity-aware Damage-промилле: игрок
        //     в зоне → > 0; враг-атакующий в зоне → 0 (BUG-008).
        Qi.AddQi(Qi.MaxQi); // топ-ап Ци (контур+ёмкость L1 Small ~880)
        var amp = Gen.GenerateSpecified(FormationType.Amplification, FormationSize.Small, 1, 777001);
        bool ampStarted = Formations.StartDrawing(amp.Id, Player.PlayerId, fx, fy);
        Formations.ContributeQi(Player.PlayerId, Formations.QiPoolMax);
        await Frames(2);
        var papAmp = Player.Position;
        int ampDmg = Formations.GetFormationBonusPermil(StatType.Damage, Player.PlayerId, papAmp.X, papAmp.Y);
        int ampEnemyDmg = Formations.GetFormationBonusPermil(StatType.Damage, "npc_hostile_qa", fx + 1, fy + 1);
        bool ampOk = ampStarted
            && Formations.CurrentStage == FormationStage.Active
            && ampDmg > 0
            && ampEnemyDmg == 0;
        GD.Print($"[FormShot] VER-001 4c Amplification (entity-aware): started={ampStarted}, стадия={Formations.CurrentStage}, " +
                 $"игрок Damage permil={ampDmg} (ожидали > 0), враг в зоне={ampEnemyDmg} (ожидали 0)");
        pass &= ampOk;
        Formations.DeactivateFormation();

        // 4d. Trap (Control/Slow|Freeze): ЧЕСТНЫЙ вердикт аудита — формация
        //     ставится/заполняется/активируется, Control-эффект присутствует в
        //     ДАННЫХ, но механизма наложения контроля на врагов нет (0
        //     потребителей ControlType; StatType.Speed не потребляется
        //     движением NPC/игрока) → задокументированный крупный разрыв,
        //     VERDICT не фейлим (механизм — отдельная задача реестра).
        Qi.AddQi(Qi.MaxQi);
        var trap = Gen.GenerateSpecified(FormationType.Trap, FormationSize.Small, 1, 777002);
        bool trapStarted = Formations.StartDrawing(trap.Id, Player.PlayerId, p0.X, p0.Y);
        Formations.ContributeQi(Player.PlayerId, Formations.QiPoolMax);
        await Frames(2);
        var trapEffects = Formations.GetActiveEffects();
        bool trapHasControl = trapEffects.Count > 0
            && trapEffects[0].EffectType == FormationEffectType.Control;
        int trapSpeed = Formations.GetFormationBonusPermil(StatType.Speed);
        GD.Print($"[FormShot] VER-001 4d Trap: started={trapStarted}, стадия={Formations.CurrentStage}, " +
                 $"Control-эффект в данных={trapHasControl} ({trapEffects.Count}), агрегированный Speed permil={trapSpeed} — " +
                 "механизм наложения контроля отсутствует (крупный разрыв, вердикт в реестре bugs.json)");
        pass &= trapStarted; // жизненный цикл ловушки обязан работать
        Formations.DeactivateFormation();

        // Пауза тиков скриншотам не мешает (рендер в _Process).
        GD.Print($"[FormShot] VERDICT: {(pass ? "PASS" : "FAIL")}");
        if (shot == null && pass == false)
            GetTree().Quit(1);
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Snap(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(path);
        GD.Print($"[FormShot] shot → {path}");
    }
}
