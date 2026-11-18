namespace RTUB.Tests;

/// <summary>
/// Where the repository is on disk, for the tests that read source files.
/// Source-linked into every test project by <c>tests/Directory.Build.props</c>.
/// </summary>
internal static class RepoPaths
{
    /// <summary>The repository root: the first folder at or above the test binaries that holds <c>RTUB.sln</c>.</summary>
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RTUB.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root (RTUB.sln).");
    }
}
