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
/// flat dictionary keyed by service type. Build() returns an immutable
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
        _registrations[typeof(TInterface)] = reg;
        // Forwarding: also register the concrete implementation type so that
        // constructor injection requesting TImplementation (rather than
        // TInterface) resolves to the SAME singleton. Both keys share the
        // same Registration object — the singleton cache ensures only one
        // instance is ever constructed.
        if (typeof(TInterface) != typeof(TImplementation))
        {
            _registrations[typeof(TImplementation)] = reg;
        }
        _orderedRegistrations.Add(reg);
    }

    public void Register<TImplementation>(Lifetime lifetime = Lifetime.Singleton)
        where TImplementation : class
    {
        var reg = new Registration(typeof(TImplementation), typeof(TImplementation), lifetime, null);
        _registrations[typeof(TImplementation)] = reg;
        _orderedRegistrations.Add(reg);
    }

    public void RegisterInstance<T>(T instance) where T : class
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        var reg = new Registration(typeof(T), instance.GetType(), Lifetime.Singleton, instance);
        _registrations[typeof(T)] = reg;
        _orderedRegistrations.Add(reg);
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
///   <item>Circular dependency detection (depth &gt; 50 → throws).</item>
/// </list>
/// </summary>
public sealed class Container : IResolver, IDisposable
{
    private readonly Dictionary<Type, Registration> _registrations;
    private readonly Dictionary<Type, object> _singletons;
    private readonly object _lock = new();
    private bool _disposed;

    internal Container(Dictionary<Type, Registration> registrations, List<Registration> ordered)
    {
        _registrations = registrations;
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
        // For v1 ResolveAll returns concrete instances assignable to T.
        // Dedupe by reference — forwarded registrations share the same
        // Registration object across interface and concrete-type keys, so
        // without dedup the same instance would be yielded twice.
        var seen = new HashSet<Registration>(ReferenceEqualityComparer.Instance);
        foreach (var reg in _registrations.Values)
        {
            if (!seen.Add(reg)) continue;
            if (typeof(T).IsAssignableFrom(reg.ServiceType))
            {
                var instance = Resolve(reg.ServiceType, depth: 0);
                if (instance is T typed) yield return typed;
            }
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

            object instance = Construct(reg.ImplementationType, depth);
            InjectProperties(instance, depth);

            if (reg.Lifetime != Lifetime.Transient)
            {
                _singletons[serviceType] = instance;
                // Cache under the implementation type too, so that subsequent
                // resolves via the forwarded concrete-type key hit the cache
                // and return the same singleton.
                if (reg.ImplementationType != serviceType)
                    _singletons[reg.ImplementationType] = instance;
            }

            return instance;
        }
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
