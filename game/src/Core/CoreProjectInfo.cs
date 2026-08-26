#nullable enable
using System.Runtime.CompilerServices;

// Exposes internal members to the test project so the DI container internals
// and event bus can be unit-tested without public exposure.
[assembly: InternalsVisibleTo("CultivationGame.Tests")]

namespace CultivationGame.Core;

/// <summary>Marker for the engine-agnostic Core layer.</summary>
internal static class CoreProjectInfo
{
    public const string Layer = "Core";
    public const string Version = "0.1.0";
}
