using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The public Órgãos Sociais (React track 008): which mandate is shown, how bodies and positions
/// are grouped and ordered, and what of a holder is public. Each test gets its own database.
/// </summary>
public class GovernanceServiceTests
{
    private static readonly int Current = FiscalYearHelper.GetCurrentFiscalYearStartYear();

    private readonly DatabaseFixture _db = new();
    private readonly GovernanceService _service;

    public GovernanceServiceTests()
    {
        _service = new GovernanceService(new Contexts(_db));
    }

    [Fact]
    public async Task NoPositionsRecorded_IsAnEmptyMandate()
    {
        await SeedFiscalYearAsync(Current);

        var result = await _service.GetPublicGovernanceAsync(null);

        result.FiscalYears.Should().BeEmpty("a fiscal year with nobody in it is not offered");
        result.FiscalYear.Should().BeNull();
        result.Bodies.Should().BeEmpty();
    }

    [Fact]
    public async Task EveryBodyAndPosition_IsListedInOrder_WithVacanciesEmpty()
    {
        await AssignAsync(Position.Secretario, Current, Member("sec"));

        var result = await _service.GetPublicGovernanceAsync(null);

        result.Bodies.Select(b => b.Name).Should().Equal(
            "Direção", "Mesa da Assembleia", "Conselho Fiscal", "Conselho de Veteranos", "Outros cargos");
        result.Bodies[0].Positions.Select(p => p.Title).Should().Equal(
            "Magister", "Vice-Magister", "Secretário", "1.º Tesoureiro", "2.º Tesoureiro");
        result.Bodies[2].Positions.Select(p => p.Title).Should().Equal("Presidente", "1.º Relator", "2.º Relator");
        result.Bodies.SelectMany(b => b.Positions).Should().HaveCount(13, "one entry per Position value");

        result.Bodies[0].Positions[2].Holders.Should().ContainSingle().Which.DisplayName.Should().Be("sec");
        result.Bodies[0].Positions[0].Holders.Should().BeEmpty("nobody is invented for a vacant position");
    }

    [Fact]
    public async Task SeveralHoldersOfOnePosition_AreAllShown_InTheOrderTheyWereRecorded()
    {
        await AssignAsync(Position.Ensaiador, Current, Member("first"));
        await AssignAsync(Position.Ensaiador, Current, Member("second"));

        var result = await _service.GetPublicGovernanceAsync(null);

        result.Bodies[^1].Positions.Single().Holders.Select(h => h.DisplayName).Should().Equal("first", "second");
    }

    [Fact]
    public async Task DefaultMandate_IsTheCurrentFiscalYear_WhenItHasHolders()
    {
        await AssignAsync(Position.Magister, Current - 1, Member("old"));
        await AssignAsync(Position.Magister, Current, Member("now"));

        var result = await _service.GetPublicGovernanceAsync(null);

        result.FiscalYear.Should().Be($"{Current}-{Current + 1}");
        result.FiscalYears.Should().Equal($"{Current}-{Current + 1}", $"{Current - 1}-{Current}");
    }

    [Fact]
    public async Task DefaultMandate_FallsBackToTheLatestRecorded_WhenTheCurrentYearIsEmpty()
    {
        // Every September the new fiscal year starts before anyone is assigned to it.
        await SeedFiscalYearAsync(Current);
        await AssignAsync(Position.Magister, Current - 3, Member("older"));
        await AssignAsync(Position.Magister, Current - 1, Member("latest"));

        var result = await _service.GetPublicGovernanceAsync(null);

        result.FiscalYear.Should().Be($"{Current - 1}-{Current}");
        result.Bodies[0].Positions[0].Holders.Single().DisplayName.Should().Be("latest");
    }

    [Fact]
    public async Task ARequestedMandate_IsHonoured_WhenItHasHolders()
    {
        await AssignAsync(Position.Magister, 2017, Member("then"));
        await AssignAsync(Position.Magister, Current, Member("now"));

        var result = await _service.GetPublicGovernanceAsync("2017-2018");

        result.FiscalYear.Should().Be("2017-2018");
        result.Bodies[0].Positions[0].Holders.Single().DisplayName.Should().Be("then");
    }

    [Theory]
    [InlineData("1990-1991")]
    [InlineData("2017-2019")]
    [InlineData("2017")]
    [InlineData("abc-def")]
    [InlineData("")]
    public async Task AnUnknownOrMalformedMandate_FallsBackToTheDefault(string requested)
    {
        await AssignAsync(Position.Magister, 2017, Member("then"));
        await AssignAsync(Position.Magister, Current, Member("now"));

        var result = await _service.GetPublicGovernanceAsync(requested);

        result.FiscalYear.Should().Be($"{Current}-{Current + 1}");
    }

    [Fact]
    public async Task ANickname_IsTheDisplayName_AndTheFullNameFollows()
    {
        await AssignAsync(Position.Magister, Current, Member("Jeans", first: "Ana", last: "Sousa"));

        var holder = (await _service.GetPublicGovernanceAsync(null)).Bodies[0].Positions[0].Holders.Single();

        holder.DisplayName.Should().Be("Jeans");
        holder.FullName.Should().Be("Ana Sousa");
    }

    [Fact]
    public async Task WithABlankNickname_TheFullNameIsTheDisplayName_AndNotRepeated()
    {
        // Nickname is required by the schema, so "missing" means blank.
        await AssignAsync(Position.Magister, Current, Member("  ", first: "Ana", last: "Sousa"));

        var holder = (await _service.GetPublicGovernanceAsync(null)).Bodies[0].Positions[0].Holders.Single();

        holder.DisplayName.Should().Be("Ana Sousa");
        holder.FullName.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("https://pub-test.r2.dev/members/a.webp", "https://pub-test.r2.dev/members/a.webp")]
    [InlineData("/images/members/a.webp", "/images/members/a.webp")]
    [InlineData("http://insecure.example/a.webp", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:image/png;base64,AAAA", null)]
    [InlineData("//evil.example/a.webp", null)]
    public async Task OnlyAUsablePhoto_IsPublished_OtherwiseNone(string? imageUrl, string? expected)
    {
        await AssignAsync(Position.Magister, Current, Member("photo", imageUrl: imageUrl));

        var holder = (await _service.GetPublicGovernanceAsync(null)).Bodies[0].Positions[0].Holders.Single();

        holder.AvatarUrl.Should().Be(expected);
    }

    [Fact]
    public void PublicContracts_CarryNoPrivateMemberField()
    {
        // A new property on these records is a new public field: it must be added here on purpose.
        Properties<PublicGovernanceDto>().Should().BeEquivalentTo("FiscalYears", "FiscalYear", "Bodies");
        Properties<GovernanceBodyDto>().Should().BeEquivalentTo("Name", "Positions");
        Properties<GovernancePositionDto>().Should().BeEquivalentTo("Title", "Holders");
        Properties<GovernanceMemberDto>().Should().BeEquivalentTo("DisplayName", "FullName", "AvatarUrl");
    }

    // ---------- helpers ----------

    private static IEnumerable<string> Properties<T>() => typeof(T).GetProperties().Select(p => p.Name);

    private static ApplicationUser Member(string nickname, string first = "First", string last = "Last", string? imageUrl = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = "u" + Guid.NewGuid().ToString("N")[..10],
        Email = "private@test.com",
        PhoneNumber = "912345678",
        Nickname = nickname,
        FirstName = first,
        LastName = last,
        ImageUrl = imageUrl,
        DateOfBirth = new DateTime(2000, 1, 1)
    };

    private async Task AssignAsync(Position position, int startYear, ApplicationUser user)
    {
        await using var db = _db.CreateContext();
        db.Users.Add(user);
        db.RoleAssignments.Add(RoleAssignment.Create(user.Id, position, startYear, startYear + 1, notes: "internal note"));
        await db.SaveChangesAsync();
    }

    private async Task SeedFiscalYearAsync(int startYear)
    {
        await using var db = _db.CreateContext();
        db.FiscalYears.Add(FiscalYear.Create(startYear, startYear + 1));
        await db.SaveChangesAsync();
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
