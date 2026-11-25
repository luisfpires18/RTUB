using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Task 164: <c>/api/account/me</c> gives signed-in members <c>shareOrigin</c> = the configured <c>App:BaseUrl</c>, so a
/// "Copiar link" made on rtub.pt, the Azure host or an installed app opens www.rtub.pt. The request's own host is never used.
/// </summary>
public class ShareOriginTests : IClassFixture<AccountRecoveryFactory>
{
    private readonly AccountRecoveryFactory _factory;

    public ShareOriginTests(AccountRecoveryFactory factory) => _factory = factory;

    [Fact]
    public async Task Me_GivesTheConfiguredBaseUrl_AsShareOrigin()
    {
        var (client, _) = await CookieTestSession.SignInAsync(_factory, $"so{Guid.NewGuid():N}"[..20], "10.96.0.1", null);

        var me = await AreaHttp.Json(client, "/api/account/me");

        me.GetProperty("shareOrigin").GetString().Should().Be(AccountRecoveryFactory.BaseUrl);
    }
}
