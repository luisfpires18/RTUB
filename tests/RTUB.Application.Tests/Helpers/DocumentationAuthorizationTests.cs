using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Documentação rules (docs/react-documentation.md): who manages folders, who opens the page and who sees which
/// folder. "who" is a kind of member: a Veterano has 2 years as Tuno or more, a Tunossauro 6 or more.
/// </summary>
public class DocumentationAuthorizationTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, false)]
    [InlineData("Owner", true, true)]
    public void ModAdminAndOwnerManage_AndOnlyTheOwnerIsTheOwner(string role, bool manage, bool owner)
    {
        var session = TestPrincipal.WithRole(role);

        DocumentationAuthorization.CanManage(session).Should().Be(manage);
        DocumentationAuthorization.IsOwner(session).Should().Be(owner);
    }

    [Fact]
    public void WithoutASignIn_NobodyManages_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            DocumentationAuthorization.CanManage(session).Should().BeFalse();
            DocumentationAuthorization.IsOwner(session).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("leitao", false, false)]
    [InlineData("leitao", true, true)]
    [InlineData("caloiro", false, true)]
    [InlineData("tuno", false, true)]
    public void ThePage_IsClosedToLeitoes_UnlessTheyAreTheOwner(string who, bool isOwner, bool expected) =>
        DocumentationAuthorization.CanOpen(Member(who), isOwner).Should().Be(expected);

    [Theory]
    [InlineData("Atas CV 2024-2025", "veterano", true)]
    [InlineData("Atas CV 2024-2025", "tunossauro", true)]
    [InlineData("Atas CV 2024-2025", "magister", true)]
    [InlineData("Atas CV 2024-2025", "tuno", false)]
    [InlineData("Atas CV 2024-2025", "caloiro", false)]
    [InlineData("atas cv antigas", "tuno", false)]
    [InlineData("Atas AG 2024-2025", "tuno", true)]
    [InlineData("Atas AG 2024-2025", "caloiro", true)]
    [InlineData("Atas AG 2024-2025", "leitao", false)]
    [InlineData("atas ag antigas", "leitao", false)]
    [InlineData("Regulamentos", "caloiro", true)]
    [InlineData("Regulamentos", "leitao", true)] // the folder rule lets them in; the page rule above keeps them out
    public void AFolder_AtasCvIsForVeteranosAndTheMagister_AtasAgIsNotForLeitoes_TheRestIsForAll(string folder, string who, bool expected) =>
        DocumentationAuthorization.CanSeeFolder(Member(who), isOwner: false, folder).Should().Be(expected);

    [Theory]
    [InlineData("Atas CV 2024-2025")]
    [InlineData("Atas AG 2024-2025")]
    [InlineData("Regulamentos")]
    public void TheOwner_SeesEveryFolder(string folder) =>
        DocumentationAuthorization.CanSeeFolder(Member("leitao"), isOwner: true, folder).Should().BeTrue();

    private static ApplicationUser Member(string who)
    {
        var thisYear = PortugalTime.Today(TimeProvider.System).Year;
        return who switch
        {
            "leitao" => new ApplicationUser { Categories = [MemberCategory.Leitao] },
            "caloiro" => new ApplicationUser { Categories = [MemberCategory.Caloiro] },
            "tuno" => new ApplicationUser { Categories = [MemberCategory.Tuno], YearTuno = thisYear },
            "veterano" => new ApplicationUser { Categories = [MemberCategory.Tuno], YearTuno = thisYear - 3 },
            "tunossauro" => new ApplicationUser { Categories = [MemberCategory.Tuno], YearTuno = thisYear - 7 },
            "magister" => new ApplicationUser { Categories = [MemberCategory.Tuno], YearTuno = thisYear, Positions = [Position.Magister] },
            _ => throw new ArgumentOutOfRangeException(nameof(who), who, "not a kind of member this test knows"),
        };
    }
}
