using System.Reflection;

namespace SmsTwin.Health;

/// <summary>The twin's version, from <c>Version</c> in <c>SmsTwin.csproj</c>.</summary>
internal static class TwinVersion
{
    public static string Current { get; } =
        typeof(TwinVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("the assembly has no informational version");
}
