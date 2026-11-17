namespace RTUB.Integration.Tests.Api;

internal static class OwnerHttpExtensions
{
    /// <summary>The body whatever the status (a refusal's problem JSON included).</summary>
    public static async Task<string> GetStringAsyncAllowingErrors(this HttpClient client, string path) =>
        await (await client.GetAsync(path)).Content.ReadAsStringAsync();
}
