using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Inventário rule that reads only the session (docs/react-inventory.md). Leitões also manage instruments, by
/// category: <c>InstrumentInventoryService</c> checks that against the member record.
/// </summary>
public class InventoryAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", true)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void ModAdminAndOwner_ManageInstruments(string role, bool expected) =>
        InventoryAuthorization.CanManage(TestPrincipal.WithRole(role)).Should().Be(expected);

    [Fact]
    public void WithoutASignIn_NobodyManages_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            InventoryAuthorization.CanManage(session).Should().BeFalse();
        }
    }
}
