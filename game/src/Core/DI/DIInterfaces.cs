#nullable enable
using System;
using System.Collections.Generic;

namespace CultivationGame.Core.DI;

/// <summary>Lifetime of a registered service.</summary>
public enum Lifetime
{
    /// <summary>Single instance shared across all resolvers.</summary>
    Singleton,
    /// <summary>A new instance is created on every resolve.</summary>
    Transient,
    /// <summary>Per-scope (treated like Singleton for v1 — single root scope).</summary>
    Scoped,
}

/// <summary>Registration DSL exposed to module <c>XxxModuleServices.Register(builder)</c>.</summary>
public interface IContainerBuilder
{
    /// <summary>Register an interface → implementation pair.</summary>
    void Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
        where TImplementation : TInterface;

    /// <summary>Register a concrete type (self-bound).</summary>
    void Register<TImplementation>(Lifetime lifetime = Lifetime.Singleton)
        where TImplementation : class;

    /// <summary>Register a pre-built instance (always singleton).</summary>
    void RegisterInstance<T>(T instance) where T : class;
}

/// <summary>Read-only resolver exposed to consumer code.</summary>
public interface IResolver
{
    T Resolve<T>();
    IEnumerable<T> ResolveAll<T>();
    bool TryResolve<T>(out T result);
}

/// <summary>
/// Marks a public property or field for injection. After the container
/// constructs an instance it scans for <c>[Inject]</c> members and
/// resolves them recursively. Property injection is the preferred form;
/// field injection is supported for adapter classes (e.g. Godot Node
/// subclasses) where properties are awkward.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
                AllowMultiple = false, Inherited = true)]
public sealed class InjectAttribute : Attribute
{
}
