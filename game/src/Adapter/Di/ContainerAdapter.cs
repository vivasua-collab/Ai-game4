#nullable enable
using System;
using System.Linq;
using System.Reflection;
using CultivationGame.Core.DI;

namespace CultivationGame.Adapter.Di;

/// <summary>
/// Bridge between the engine-agnostic Core DI container and Godot nodes.
///
/// Godot instantiates Node-derived classes via the scene tree (not the DI
/// container), so [Inject]-marked fields on those nodes need to be wired
/// manually in their _Ready(). This helper does that via reflection.
///
/// Usage:
/// <code>
/// public override void _Ready()
/// {
///     var container = GameBoot.Container;
///     if (container != null)
///         ContainerAdapter.InjectProperties(this, container);
/// }
/// </code>
/// </summary>
public static class ContainerAdapter
{
    /// <summary>
    /// Resolve <typeparamref name="T"/> from the container. The container
    /// performs property injection on resolved instances per its own policy.
    /// </summary>
    public static T ResolveAndInject<T>(IResolver resolver) where T : class
    {
        return resolver.Resolve<T>();
    }

    /// <summary>
    /// Walk all public + non-public instance fields and properties on
    /// <paramref name="target"/>, and for any decorated with
    /// <see cref="InjectAttribute"/>, resolve the dependency type from
    /// <paramref name="resolver"/> and assign it via reflection.
    ///
    /// Works on <c>readonly</c> fields (reflection bypasses the
    /// compile-time check). Caches the open generic Resolve&lt;T&gt; method
    /// for performance.
    /// </summary>
    public static void InjectProperties(object target, IResolver resolver)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (resolver == null) throw new ArgumentNullException(nameof(resolver));

        var type = target.GetType();

        // Fields (including readonly private ones).
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.GetCustomAttribute<InjectAttribute>() == null) continue;
            var service = ResolveService(resolver, field.FieldType);
            if (service != null)
            {
                field.SetValue(target, service);
            }
            else
            {
                GDLogWarn($"[ContainerAdapter] Could not resolve {field.FieldType.Name} for {type.Name}.{field.Name}");
            }
        }

        // Properties.
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<InjectAttribute>() == null) continue;
            if (!prop.CanWrite) continue;
            var service = ResolveService(resolver, prop.PropertyType);
            if (service != null)
            {
                prop.SetValue(target, service);
            }
            else
            {
                GDLogWarn($"[ContainerAdapter] Could not resolve {prop.PropertyType.Name} for {type.Name}.{prop.Name}");
            }
        }
    }

    /// <summary>
    /// Try to resolve a service of <paramref name="serviceType"/> from the
    /// container. Uses the open-generic <c>IResolver.Resolve&lt;T&gt;()</c>
    /// method via reflection. Returns null on failure (rather than throwing)
    /// so partial injection is possible.
    /// </summary>
    private static object? ResolveService(IResolver resolver, Type serviceType)
    {
        try
        {
            var resolveMethod = typeof(IResolver)
                .GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance)
                ?? resolver.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethod && m.GetParameters().Length == 0);

            if (resolveMethod == null || !resolveMethod.IsGenericMethod)
            {
                GDLogWarn($"[ContainerAdapter] IResolver.Resolve<T> method not found.");
                return null;
            }

            var concrete = resolveMethod.MakeGenericMethod(serviceType);
            return concrete.Invoke(resolver, null);
        }
        catch (Exception ex)
        {
            GDLogWarn($"[ContainerAdapter] Resolve<{serviceType.Name}> threw: {ex.Message}");
            return null;
        }
    }

    // ---- Logging shim: avoids taking a hard Godot reference in case this
    //      file is ever reused in a non-Godot test context. ----
    private static void GDLogWarn(string message)
    {
        try
        {
            Godot.GD.PushWarning(message);
        }
        catch
        {
            // Godot not available — fall back silently.
            Console.WriteLine(message);
        }
    }
}
