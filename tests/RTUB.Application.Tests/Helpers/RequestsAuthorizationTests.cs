using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Pedidos rules (docs/react-requests-questions.md): every member but a Leitão reads the requests; Admin and Owner
/// answer and delete them, and read them even as a Leitão.
/// </summary>
public class RequestsAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void AdminAndOwner_Manage_AModDoesNot(string role, bool expected) =>
        RequestsAuthorization.CanManage(TestPrincipal.WithRole(role)).Should().Be(expected);

    [Theory]
    [InlineData("", MemberCategory.Tuno, true)]
    [InlineData("", MemberCategory.Caloiro, true)]
    [InlineData("", MemberCategory.Leitao, false)]
    [InlineData("Mod", MemberCategory.Leitao, false)]
    [InlineData("Admin", MemberCategory.Leitao, true)]
    [InlineData("Owner", MemberCategory.Leitao, true)]
    public void ThePage_IsClosedToLeitoes_UnlessAdminOrOwner(string role, MemberCategory category, bool expected) =>
        RequestsAuthorization.CanOpen(new ApplicationUser { Categories = [category] }, TestPrincipal.WithRole(role)).Should().Be(expected);

    [Fact]
    public void WithoutASignIn_NobodyManages_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            RequestsAuthorization.CanManage(session).Should().BeFalse();
        }
    }
}
