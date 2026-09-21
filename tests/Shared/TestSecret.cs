namespace RTUB.Tests;

/// <summary>
/// Generates test credentials at runtime.
///
/// A committed password literal is indistinguishable from a real one to a secret scanner: a
/// synthetic one added by unit 012 raised a GitGuardian "Generic Password" incident. Generating
/// the value instead keeps the repository free of credential-shaped strings without changing what
/// any test does — a test that needs the same value twice holds it in a local and passes it to
/// both calls. Do not quote a removed literal here; a comment is scanned like any other line.
///
/// Source-linked into every test project by <c>tests/Directory.Build.props</c>.
/// </summary>
internal static class TestSecret
{
    /// <summary>
    /// A fresh password, unique per call. The GUID supplies the length and the uniqueness; the
    /// suffix keeps the value valid if Identity's password complexity rules are ever turned on
    /// (they are all off today, with <c>RequiredLength = 4</c>).
    /// </summary>
    internal static string NewPassword() => Guid.NewGuid().ToString("N") + "Aa1!";
}
