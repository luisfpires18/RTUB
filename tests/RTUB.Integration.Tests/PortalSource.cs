namespace RTUB.Integration.Tests;

/// <summary>
/// Source-contract tests read portal text. Since task 113 a page or component is several files, so a test reads every
/// file of the feature it asserts on. A pattern that matches nothing throws, so a NotContain can never pass on no text.
/// </summary>
internal static class PortalSource
{
    /// <summary>Files under <paramref name="src"/> matching "dir/pattern" (the wildcard is in the file name only).</summary>
    public static string[] Files(string src, params string[] patterns) => patterns
        .SelectMany(p =>
        {
            var found = Directory.GetFiles(Path.Combine(src, Path.GetDirectoryName(p)!), Path.GetFileName(p));
            return found.Length == 0 ? throw new FileNotFoundException($"No portal file matches {p}") : found.OrderBy(f => f, StringComparer.Ordinal);
        })
        .ToArray();

    public static string Cat(string src, params string[] patterns) => string.Concat(Files(src, patterns).Select(File.ReadAllText));

    /// <summary>The home page: the page and the Home* parts it was split into.</summary>
    public static string Home(string src) => Cat(src, "pages/Home.tsx", "components/Home*.tsx");
}
