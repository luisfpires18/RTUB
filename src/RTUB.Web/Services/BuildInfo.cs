using System.Reflection;

namespace RTUB.Web.Services;

/// <summary>
/// The running build's identity, as served by <c>GET /api/version</c>. The version comes from the
/// repository's <c>VERSION</c> file (Directory.Build.props); the SDK appends the full commit SHA to
/// the informational version as <c>+&lt;sha&gt;</c>. Nothing environment-specific and nothing secret.
/// </summary>
public sealed record BuildInfo(string Version, string? Commit)
{
    public static BuildInfo Current { get; } = Parse(
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static BuildInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return new BuildInfo("unknown", null);

        var plus = informationalVersion.IndexOf('+');
        return plus < 0
            ? new BuildInfo(informationalVersion, null)
            : new BuildInfo(informationalVersion[..plus], informationalVersion[(plus + 1)..]);
    }
}
