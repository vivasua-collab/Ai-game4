#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.World;

namespace CultivationGame.Entry;

/// <summary>
/// Main game-loop driver. Pure C#; called by the Godot Adapter bootstrap
/// node (<c>GameBoot</c>) once per process.
/// </summary>
/// <remarks>
/// <para>Implements <see cref="IStartable"/> and <see cref="ITickable"/>
/// so the adapter can drive both startup and the fixed-tick simulation
/// loop through a single resolved instance.</para>
/// <para><b>Start:</b> collects every <see cref="IStartable"/> and
/// <see cref="ITickable"/> from the container (excluding self to avoid
/// recursion) and calls <c>Start()</c> on each in registration order
/// (P2-12: ResolveAll iterates the ordered registration list; modules
/// first, then Entry services, then <c>GameEntryPoint</c> last via the
/// external <c>Start()</c> call from the adapter).</para>
/// <para><b>Startup contract — FAIL-CLOSED (P2-13, аудит 09.22 Фаза 4):</b>
/// все модули получают <c>Start()</c> (полная диагностика одной попытки),
/// затем при любом провале наружу летит <see cref="AggregateException"/>;
/// <c>_initialized</c> НЕ выставляется — тик-луп остаётся заглушён
/// (полуинициализированные модули не симулируют и не автосейвят).
/// Прежнее поведение (fail-open + <c>_initialized=true</c> до старта)
/// делало частично инициализированное состояние постоянным.</para>
/// <para><b>Retry contract — resume from failure (P1-10, аудит 09.22 Фазы 7/10):</b>
/// повторный <c>Start()</c> после проваленного бута ретраит ТОЛЬКО
/// провалившиеся и ещё не стартованные модули; успешно завершившие
/// <c>Start()</c> пропускаются (<c>_startedModules</c>) — повторный Start
/// модуля с EventBus-подписками = утечка токена + двойная обработка
/// событий. Транзакции «полный повтор бута» больше нет; откат (Stop/Dispose)
/// успешных модулей не делается — их инициализация валидна, тики до
/// успешного бута были заглушены.</para>
/// <para><b>Tick — fail-open (изоляция сбоя):</b> упавший в рантайме
/// <see cref="ITickable"/> логируется и пропускается, симуляция
/// продолжается. Контракт зеркален fail-closed startup: единичная
/// ошибка в установившемся рантайме не должна останавливать мир.
/// Re-entrancy-guarded: nested <c>Tick</c> (например, из
/// <c>Start()</c> модуля) — no-op; тики до успешного старта — no-op.</para>
/// </remarks>
public sealed class GameEntryPoint : IStartable, ITickable
{
    [Inject] private readonly IResolver _resolver = null!;
    [Inject] private readonly IGameSession _session = null!;

    private readonly List<IStartable> _startables = new();
    private readonly List<ITickable> _tickables = new();

    // P1-10 (аудит 09.22, Фазы 7/10): модули, успешно завершившие Start() в
    // ПРЕДЫДУЩИХ попытках бута. Живёт через ретраи (не чистится) — retry не
    // перезапускает их повторно: двойной Start = утечка/дубль EventBus-подписок
    // (NPC/Qi/Combat/Inventory паттерн «поле-присваивание без dispose-старого»).
    // Ссылочная идентичность: контейнер синглтонный — резолв тех же типов даёт
    // те же инстансы; новый набор появляется только у перепостроенного
    // контейнера = новый процесс/новый GameEntryPoint.
    private readonly HashSet<IStartable> _startedModules = new();

    private bool _initialized;
    private bool _ticking;

    /// <summary>
    /// Drives startup. Resolves all <see cref="IStartable"/> /
    /// <see cref="ITickable"/> (self excluded) and starts them.
    /// Safe to call from the adapter after the container is built.
    /// </summary>
    public void Start()
    {
        if (_initialized)
        {
            Console.WriteLine("[GameEntryPoint] Start() ignored — already initialised");
            return;
        }

        // P2-13: повторный Start() (после проваленного бута или до успешного)
        // пересобирает списки С НАУЛЯ — дубликатов в списках не накапливаем.
        // P1-10 (аудит 09.22, Фазы 7/10): при этом сами МОДУЛИ, чей Start()
        // уже завершился успешно, при ретрае НЕ перезапускаются — стартовая
        // попытка = транзакция с точкой продолжения (resume from failure).
        // Прежде ретрай = полный повтор бута: успешно стартовавшие модули
        // получали второй Start() → у модулей с EventBus-подписками в Start()
        // (NPC/Qi/Combat/Inventory/… — поле-присваивание без dispose-старого)
        // каждая попытка ретрая УТЕЧКА прежний токен и ДУБЛИРОВАЛА обработку
        // событий (runtime-подтверждено симом J2: SubscriberCount 1→2).
        // Контракт ретрая: провалившийся модуль и не стартованные — ретраятся
        // с полной диагностикой; успешные — пропускаются (G3/J4).
        _startables.Clear();
        _tickables.Clear();

        // Collect startables (exclude self to prevent recursion).
        var startables = _resolver.ResolveAll<IStartable>();
        foreach (var s in startables)
        {
            if (!ReferenceEquals(s, this)) _startables.Add(s);
        }

        // Collect tickables (exclude self; GameEntryPoint.Tick is driven
        // externally by the adapter and forwards to the list).
        var tickables = _resolver.ResolveAll<ITickable>();
        foreach (var t in tickables)
        {
            if (!ReferenceEquals(t, this)) _tickables.Add(t);
        }

        // P2-13 (аудит 09.22, Фаза 4): контракт startup = FAIL-CLOSED.
        // Прежде: (1) _initialized выставлялся ДО запуска модулей; (2) исключение
        // любого Start() глоталось с логом — игра продолжала работать с частично
        // инициализированным состоянием (модуль без подписок EventBus/без
        // обязательной инициализации), повторный Start() уже игнорировался —
        // повреждение становилось постоянным. Теперь: все модули стартуют
        // (полная диагностика одной попытки), провалы агрегируются, бут
        // завершается AggregateException-ом; _initialized НЕ выставляется —
        // Tick остаётся заглушён. «Mark initialised BEFORE Start» удалено:
        // тики во время старта теперь отбрасываются гейтом !_initialized —
        // полуинициализированные модули тикать не должны.
        var failures = new List<Exception>();

        foreach (var s in _startables)
        {
            // P1-10: точка продолжения — успешный модуль НЕ получает второй
            // Start() при ретрае (его подписки/инициализация уже живут и
            // валидны; тики до успешного бута заглушены — он ничего не
            // пропустил). Идемпотентность самих Start() — дополнительный
            // слой (модульные гварды) — а не единственная защита транзакции.
            if (_startedModules.Contains(s))
            {
                Console.WriteLine($"[GameEntryPoint] Startable {s.GetType().Name} already started — skipped (P1-10 resume)");
                continue;
            }

            try
            {
                s.Start();
                _startedModules.Add(s);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameEntryPoint] Startable {s.GetType().Name} FAILED: {ex.GetType().Name}: {ex.Message}");
                failures.Add(new InvalidOperationException($"Startable {s.GetType().FullName} failed to start", ex));
            }
        }

        // R17 (аудит-0911 WT-3): полный каталог локаций — в реестр WorldService.
        // Entry-слой — законное место для LocationCatalog (Core ← Modules ← Entry);
        // WorldModule регистрирует только fallback test_polygon. Без этого
        // SetActiveLocation("large_world") тихо проваливался (NOT FOUND), а блок
        // "world" при LoadGame не мог восстановить локацию сейва (E-3).
        try
        {
            // Конкретный тип: RegisterLocation — на WorldService, не на
            // интерфейсе (реестр — процесс-scoped контент, IWorldService —
            // только потребительский контракт).
            var worldService = _resolver.Resolve<WorldService>();
            foreach (var loc in LocationCatalog.GetAll())
                worldService.RegisterLocation(loc);
            var ids = string.Join(", ", System.Linq.Enumerable.Select(LocationCatalog.GetAll(), l => l.Id));
            Console.WriteLine($"[GameEntryPoint] Location catalog registered: {LocationCatalog.GetAll().Count} locations [{ids}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameEntryPoint] Location catalog registration FAILED: {ex.GetType().Name}: {ex.Message}");
            // P2-13: каталог локаций — стартовый критический шаг (R17 E-3:
            // без него LoadGame не может восстановить локацию сейва) —
            // fail-closed вместе с остальным startup.
            failures.Add(new InvalidOperationException("Location catalog registration failed (R17 WT-3)", ex));
        }

        if (failures.Count > 0)
            throw new AggregateException(
                $"Game startup FAILED: {failures.Count} failure(s) — fail-closed contract (P2-13): " +
                "partial initialisation is not a runnable state",
                failures);

        _initialized = true;

        Console.WriteLine(
            $"[GameEntryPoint] Started. {_startables.Count} startables, {_tickables.Count} tickables, session={_session.GetType().Name}");
    }

    /// <summary>
    /// Forward a fixed tick to every collected <see cref="ITickable"/>.
    /// No-op until startup has fully succeeded (P2-13: полуинициализированные
    /// модули не тикают), and re-entrancy-guarded: if Tick is invoked while
    /// already ticking (e.g. a startable triggers a tick during Start), the
    /// nested call is a no-op. Per-tickable failures are isolated (fail-open:
    /// logged, skipped — см. контракт в class remarks).
    /// </summary>
    /// <param name="tickCount">Monotonic tick counter (1 tick = 1 game minute).</param>
    public void Tick(int tickCount)
    {
        if (!_initialized || _ticking) return;
        _ticking = true;
        try
        {
            for (int i = 0; i < _tickables.Count; i++)
            {
                try
                {
                    _tickables[i].Tick(tickCount);
                }
                catch (Exception ex)
                {
                    // R16: +StackTrace (первые 3 кадра) — диагностика источников
                    // (ArgumentNullException 'key' без стека не локализовался).
                    var frames = ex.StackTrace?.Split('\n');
                    string top = "";
                    if (frames != null)
                        for (int f = 0; f < System.Math.Min(6, frames.Length); f++)
                            top += "\n      " + frames[f].Trim();
                    Console.WriteLine($"[GameEntryPoint] Tickable {_tickables[i].GetType().Name} threw at tick {tickCount}: {ex.GetType().Name}: {ex.Message}{top}");
                }
            }
        }
        finally
        {
            _ticking = false;
        }
    }
}
