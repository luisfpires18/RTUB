using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Loja rules (docs/react-shop.md): who adds products, manages reservations and deletes products.</summary>
public class ShopAuthorizationTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public void Members_ModAddsAndManagesReservations_AdminAndOwnerAlsoDelete(string role, bool create, bool manage)
    {
        var session = role.Length == 0 ? TestPrincipal.SignedIn("member") : TestPrincipal.SignedIn("member", role);

        ShopAuthorization.CanCreate(session).Should().Be(create);
        ShopAuthorization.CanManageReservations(session).Should().Be(create);
        ShopAuthorization.CanManage(session).Should().Be(manage);
    }

    [Fact]
    public void AVisitor_ManagesNothing()
    {
        var visitor = TestPrincipal.Visitor();

        ShopAuthorization.CanCreate(visitor).Should().BeFalse();
        ShopAuthorization.CanManageReservations(visitor).Should().BeFalse();
        ShopAuthorization.CanManage(visitor).Should().BeFalse();
    }

    [Theory]
    [InlineData("Mod")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public void ARoleClaimWithoutASignIn_ManagesNothing(string role)
    {
        var forged = TestPrincipal.NotAuthenticated("member", role);

        ShopAuthorization.CanCreate(forged).Should().BeFalse();
        ShopAuthorization.CanManage(forged).Should().BeFalse();
    }
}
