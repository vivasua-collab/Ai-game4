#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CultivationGame.Core.DI;

/// <summary>
/// Internal registration record.
/// </summary>
internal sealed class Registration
{
    public Type ServiceType { get; }
    public Type? ImplementationType { get; }
    public Lifetime Lifetime { get; }
    public object? Instance { get; set; }
    public bool HasInstance => Instance is not null;

    public Registration(Type serviceType, Type? implementationType, Lifetime lifetime, object? instance)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        Instance = instance;
    }
}

/// <summary>
/// Minimal but functional DI container builder. Stores registrations in a
/// flat dictionary keyed by service type (resolution) and an ordered list
/// (iteration order for <c>ResolveAll</c> — the documented "registration
/// order" contract, P2-12). Build() returns an immutable
/// <see cref="Container"/>.
/// </summary>
public sealed class ContainerBuilder : IContainerBuilder
{
    private readonly Dictionary<Type, Registration> _registrations = new();
    private readonly List<Registration> _orderedRegistrations = new();

    public void Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
        where TImplementation : TInterface
    {
        var reg = new Registration(typeof(TInterface), typeof(TImplementation), lifetime, null);
        SetRegistration(typeof(TInterface), reg, pruneStaleForwarding: false);
        // Forwarding: also register the concrete implementation type so that
        // constructor injection requesting TImplementation (rather than
        // TInterface) resolves to the SAME singleton. Both keys share the
        // same Registration object — the singleton cache ensures only one
        // instance is ever constructed.
        if (typeof(TInterface) != typeof(TImplementation))
        {
            SetRegistration(typeof(TImplementation), reg, pruneStaleForwarding: false);
        }
        _orderedRegistrations.Add(reg);
    }

    public void Register<TImplementation>(Lifetime lifetime = Lifetime.Singleton)
        where TImplementation : class
    {
        var reg = new Registration(typeof(TImplementation), typeof(TImplementation), lifetime, null);
        SetRegistration(typeof(TImplementation), reg, pruneStaleForwarding: false);
        _orderedRegistrations.Add(reg);
    }

    public void RegisterInstance<T>(T instance) where T : class
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        var reg = new Registration(typeof(T), instance.GetType(), Lifetime.Singleton, instance);
        SetRegistration(typeof(T), reg, pruneStaleForwarding: true);
        _orderedRegistrations.Add(reg);
    }

    /// <summary>
    /// Заменить регистрацию по ключу <paramref name="key"/>.
    /// <para>
    /// P1-6 (аудит 09.22): pruning хвостов — ТОЛЬКО для инстанс-оверрайда
    /// (<see cref="RegisterInstance{T}"/> = адаптер подменяет single-provider
    /// сервис): если прежняя регистрация теряет по этому ключу свой ПЕРВИЧНЫЙ
    /// сервисный тип, её оставшиеся forwarding-ключи (concrete-тип) удаляются —
    /// иначе Adapter-override <c>RegisterInstance&lt;ISaveFileHandler&gt;(godotHandler)</c>
    /// поверх <c>Register&lt;ISaveFileHandler, Modules.SaveFileHandler&gt;</c> оставлял
    /// живой ключ Modules.SaveFileHandler, резолвившийся в мёртвый
    /// fallback-инстанс (и конструируемый впустую каждым ResolveAll-обходом).
    /// </para>
    /// <para>
    /// Обычные <c>Register&lt;&gt;</c>-перезаписи ключей НЕ прунят (ключи НЕЗАВИСИМЫ
    /// — префикс-семантика): мульти-интерфейсный паттерн (Register&lt;INPCService,X&gt;
    /// + Register&lt;ISaveable,X&gt;, затем Player-модуль перерегистрирует ISaveable)
    /// держится именно на выживании forwarding-ключей — по ним резолвятся
    /// concrete-параметры конструкторов (CorpseService→NPCService,
    /// CombatService→TechniqueService) и собирается ResolveAll&lt;ISaveable&gt;.
    /// Инстанс-оверрайд маркер-мульти-провайдерского ключа (напр. ISaveable)
    /// запрещён по построению — семантику «все провайдеры» он бы вычистил.
    /// </para>
    /// <para>
    /// P2-12 (аудит 09.22, Фаза 4): <see cref="_orderedRegistrations"/> — теперь
    /// источник итерации <c>ResolveAll</c> (контракт «registration order»).
    /// Согласованность со словарём ключей: сместившаяся регистрация (ключ
    /// перезаписан соседней регистрацией или хвосты сняты prune-ом P1-6),
    /// не владеющая больше НИ ОДНИМ ключом, МЕРТВА и удаляется из ordered —
    /// иначе ResolveAll, идущий по ordered, воскресил бы её (в т.ч. stale
    /// concrete из P1-6, чего страж C2 не допускает). Живая мульти-интерфейсная
    /// регистрация (владеет своим primary-ключом, форвард-ключ мог быть
    /// украден соседом — паттерн NPCService, страж C5) остаётся в списке.
    /// </para>
    /// </summary>
    private void SetRegistration(Type key, Registration reg, bool pruneStaleForwarding)
    {
        _registrations.TryGetValue(key, out var old);

        if (pruneStaleForwarding
            && old is not null
            && !ReferenceEquals(old, reg)
            && old.ServiceType == key)
        {
            var doomed = new List<Type>();
            foreach (var kv in _registrations)
                if (ReferenceEquals(kv.Value, old) && kv.Key != key) doomed.Add(kv.Key);
            foreach (var t in doomed) _registrations.Remove(t);
        }
        _registrations[key] = reg;

        // P2-12: ordered-список не должен содержать мёртвых регистраций.
        if (old is not null && !ReferenceEquals(old, reg) && !OwnsAnyKey(old))
            _orderedRegistrations.Remove(old);
    }

    /// <summary>Владеет ли регистрация хотя бы одним живым ключом словаря.</summary>
    private bool OwnsAnyKey(Registration candidate)
    {
        foreach (var kv in _registrations)
            if (ReferenceEquals(kv.Value, candidate)) return true;
        return false;
    }

    public Container Build() => new Container(_registrations, _orderedRegistrations);
}

/// <summary>
/// Lightweight DI container supporting:
/// <list type="bullet">
///   <item>Singleton / Transient / Scoped lifetimes (Scoped ≡ Singleton for v1).</item>
///   <item>Constructor injection (greediest public ctor).</item>
///   <item>Property injection via <see cref="InjectAttribute"/>.</item>
///   <item>Pre-built instances via <c>RegisterInstance</c>.</item>
///   <item>ResolveAll iterates registrations in registration order (P2-12).</item>
///   <item>Circular dependency detection: construction path gives the exact
///   cycle («A → B → C → A», P2-14); depth &gt; 50 remains a safety net.</item>
/// </list>
/// </summary>
public sealed class Container : IResolver, IDisposable
{
    private readonly Dictionary<Type, Registration> _registrations;
    private readonly List<Registration> _orderedRegistrations;
    private readonly Dictionary<Type, object> _singletons;
    // P2-14 (аудит 09.22, Фаза 4): типы в ТЕКУЩЕЙ цепочке конструирования
    // (стек разрешения). Доступ — только под _lock, вложенные Resolve
    // рекурсивны на том же потоке (Monitor реентерабелен), потому обычный
    // List корректен без ThreadStatic.
    private readonly List<Type> _constructionPath = new();
    private readonly object _lock = new();
    private bool _disposed;

    internal Container(Dictionary<Type, Registration> registrations, List<Registration> ordered)
    {
        _registrations = registrations;
        _orderedRegistrations = ordered;
        _singletons = new Dictionary<Type, object>();
        // Self-register so IResolver can be injected.
        _singletons[typeof(IResolver)] = this;
    }

    public T Resolve<T>()
    {
        if (TryResolve<T>(out var result)) return result;
        throw new InvalidOperationException(
            $"No registration for service type '{typeof(T).FullName}'.");
    }

    public IEnumerable<T> ResolveAll<T>()
    {
        // R11 P0-Save (внешнее ревью 2026-09-09): инстанс-ориентированный обход.
        //
        // Прежняя версия резолвила по reg.ServiceType. При мульти-интерфейсных
        // форвардах (Register<ISaveable, X> в N модулях — наш DI не умеет
        // .As<>().AsSelf()) все форварды делят ОДИН словарный ключ
        // typeof(ISaveable): выживала только ПОСЛЕДНЯЯ регистрация, а
        // уцелевшие impl-ключи (ServiceType=ISaveable, Impl=X) резолвили всех
        // через Resolve(ISaveable) в ОДИН И ТОТ ЖЕ инстанс последнего
        // победителя — ResolveAll<ISaveable> возвращал SaveService ×7, и
        // агрегатор сейвов записывал в файл один блок save_meta.
        //
        // Теперь: инстанс-регистрации отдаются напрямую, остальные резолвятся
        // по ImplementationType (impl-ключ всегда указывает на корректную
        // регистрацию своего типа), матчинг — по фактическому типу инстанса,
        // дедуп — по ссылке на инстанс (не по Registration-объекту).
        //
        // P2-12 (аудит 09.22, Фаза 4): итерация — по _orderedRegistrations
        // (порядок вызовов Register*/RegisterInstance), а не по Dictionary.Values:
        // словарь после удалений (prune P1-6) порядок НЕ гарантирует, а
        // GameEntryPoint документирует контракт «Start() … in registration
        // order». Мёртвые регистрации (не владеющие ни одним ключом)
        // ContainerBuilder из ordered уже удалил — см. SetRegistration.
        var seen = new HashSet<object>(
            System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var reg in _orderedRegistrations)
        {
            object? instance;
            if (reg.HasInstance)
            {
                instance = reg.Instance;
            }
            else
            {
                var resolveType = reg.ImplementationType ?? reg.ServiceType;
                instance = Resolve(resolveType, depth: 0);
            }
            if (instance is T typed && seen.Add(instance))
                yield return typed;
        }
    }

    public bool TryResolve<T>(out T result)
    {
        object? obj = Resolve(typeof(T), depth: 0, throwIfMissing: false);
        if (obj is T t)
        {
            result = t;
            return true;
        }
        result = default!;
        return false;
    }

    private object? Resolve(Type serviceType, int depth, bool throwIfMissing = true)
    {
        if (depth > 50)
            throw new InvalidOperationException(
                $"Circular dependency or excessive resolution depth (>50) while resolving '{serviceType.FullName}'.");

        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Container));

            // IResolver → self.
            if (serviceType == typeof(IResolver)) return this;

            // Special-case: IPublisher<T> and ISubscriber<T> → wrap EventBus.
            if (serviceType.IsGenericType)
            {
                var genericDef = serviceType.GetGenericTypeDefinition();
                if (genericDef == typeof(Core.Events.IPublisher<>))
                {
                    var bus = Resolve(typeof(Core.Events.EventBus), depth + 1, throwIfMissing: false);
                    if (bus is Core.Events.EventBus eb)
                    {
                        var wrapperType = typeof(Core.Events.EventBusPublisher<>).MakeGenericType(serviceType.GetGenericArguments());
                        return Activator.CreateInstance(wrapperType, eb)!;
                    }
                    return null!;
                }
                if (genericDef == typeof(Core.Events.ISubscriber<>))
                {
                    var bus = Resolve(typeof(Core.Events.EventBus), depth + 1, throwIfMissing: false);
                    if (bus is Core.Events.EventBus eb)
                    {
                        var wrapperType = typeof(Core.Events.EventBusSubscriber<>).MakeGenericType(serviceType.GetGenericArguments());
                        return Activator.CreateInstance(wrapperType, eb)!;
                    }
                    return null!;
                }
            }

            if (!_registrations.TryGetValue(serviceType, out var reg))
            {
                if (throwIfMissing) return null;
                return null;
            }

            // Pre-built instance.
            if (reg.HasInstance) return reg.Instance;

            // Singleton cache. Check both the requested service type AND the
            // implementation type — forwarded registrations (interface +
            // concrete-type keys pointing to the same Registration) must share
            // a single instance regardless of which key was used to resolve.
            if (reg.Lifetime != Lifetime.Transient)
            {
                if (_singletons.TryGetValue(serviceType, out var cached))
                    return cached;
                if (reg.ImplementationType is not null
                    && reg.ImplementationType != serviceType
                    && _singletons.TryGetValue(reg.ImplementationType, out var cachedImpl))
                    return cachedImpl;
            }

            // Need to construct.
            if (reg.ImplementationType is null)
                throw new InvalidOperationException(
                    $"Registration for '{serviceType.FullName}' has no implementation type and no instance.");

            // P2-14 (аудит 09.22, Фаза 4): настоящий cycle detection — по
            // construction path (типы в текущей цепочке конструирования), а
            // не только depth-limit. Цикл ловится на ПЕРВОМ повторном входе
            // с точным путём «A → B → C → A», без 51-го лишнего конструирования.
            // Кэш синглтонов НЕ мешает: инстанс попадает в _singletons ПОСЛЕ
            // завершения Construct — повторный вход в недостроенный тип это
            // по определению цикл. Depth>50 остаётся страховкой (циклы через
            // фабрики/Activator path не оставляют).
            var implType = reg.ImplementationType;
            int cycleAt = _constructionPath.IndexOf(implType);
            if (cycleAt >= 0)
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving '{serviceType.FullName}': " +
                    FormatConstructionPath(cycleAt, implType));

            _constructionPath.Add(implType);
            try
            {
                object instance = Construct(implType, depth);
                InjectProperties(instance, depth);

                if (reg.Lifetime != Lifetime.Transient)
                {
                    _singletons[serviceType] = instance;
                    // Cache under the implementation type too, so that subsequent
                    // resolves via the forwarded concrete-type key hit the cache
                    // and return the same singleton.
                    if (implType != serviceType)
                        _singletons[implType] = instance;
                }

                return instance;
            }
            finally
            {
                _constructionPath.RemoveAt(_constructionPath.Count - 1);
            }
        }
    }

    /// <summary>
    /// P2-14: путь цикла от первого вхождения повторного типа до текущего
    /// конца цепочки + сам повтор: «A → B → C → A».
    /// </summary>
    private string FormatConstructionPath(int cycleAt, Type repeated)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = cycleAt; i < _constructionPath.Count; i++)
        {
            sb.Append(_constructionPath[i].Name);
            sb.Append(" → ");
        }
        sb.Append(repeated.Name);
        return sb.ToString();
    }

    private object Construct(Type implType, int depth)
    {
        var ctors = implType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0)
            return Activator.CreateInstance(implType)!;

        // Pick greediest ctor we can satisfy.
        var ctor = ctors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters()
            .Select(p =>
            {
                var resolved = Resolve(p.ParameterType, depth + 1, throwIfMissing: false);
                if (resolved is null && !p.HasDefaultValue)
                    throw new InvalidOperationException(
                        $"Cannot resolve parameter '{p.Name}' ({p.ParameterType.FullName}) " +
                        $"for constructor of '{implType.FullName}'.");
                return resolved ?? p.DefaultValue;
            })
            .ToArray();

        return ctor.Invoke(args)!;
    }

    private void InjectProperties(object instance, int depth)
    {
        var type = instance.GetType();

        // Property injection (preferred).
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;
            if (prop.GetCustomAttribute<InjectAttribute>() is null) continue;
            var resolved = Resolve(prop.PropertyType, depth + 1, throwIfMissing: false);
            if (resolved is null) continue;
            prop.SetValue(instance, resolved);
        }

        // Field injection (for adapter classes where properties are awkward).
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.GetCustomAttribute<InjectAttribute>() is null) continue;
            var resolved = Resolve(field.FieldType, depth + 1, throwIfMissing: false);
            if (resolved is null) continue;
            field.SetValue(instance, resolved);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            // Forwarded registrations cache the same instance under multiple
            // keys (interface + concrete type) — dedupe by reference before
            // disposing to avoid calling Dispose() twice on the same object.
            var disposed = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var singleton in _singletons.Values)
            {
                if (ReferenceEquals(singleton, this)) continue;
                if (!disposed.Add(singleton)) continue;
                if (singleton is IDisposable d) d.Dispose();
            }
            _singletons.Clear();
            _registrations.Clear();
        }
    }
}
