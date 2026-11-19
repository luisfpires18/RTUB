using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Data.Builders;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Pins what <see cref="SeedData.SeedMembersAsync"/> writes for the bulk member seed: who exists,
/// who mentors whom (by nickname, since two members share a display name) and the role
/// assignments. Captured from the code before it was split into partial files.
/// </summary>
public class SeedDataMemberSnapshotTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    private static readonly (string Mentee, string Mentor)[] ExpectedMentors =
    {
        ("Ambrósio", "Zeca Diabo"),
        ("Arbusto", "Bate Sacas"),
        ("Atchim", "Conchita"),
        ("Autoscópio", "Snoopy"),
        ("Badjoncas", "Erbalife"),
        ("Barbatov", "Esbarrafat"),
        ("Bate Sacas", "Conchita"),
        ("Bombeiro", "Arbusto"),
        ("Borat", "Autoscópio"),
        ("Botox", "Metralha"),
        ("Bronha", "Erbalife"),
        ("Buceta", "Txaio"),
        ("Caldas", "Alentejano"),
        ("Calhau", "Bate Sacas"),
        ("Calimero", "Conchita"),
        ("Castanholas", "Pinta a Calça"),
        ("Cestinho", "Joy"),
        ("Chicaestrume", "Matrix"),
        ("Cigano", "Pilão"),
        ("Conchita", "Cestinho"),
        ("Croquetes", "Conchita"),
        ("Delay", "Cestinho"),
        ("Drift", "Erbalife"),
        ("El Chapo", "Conchita"),
        ("Erbalife", "Foda Nice"),
        ("Esbarrafat", "Exkill Bill"),
        ("Flor", "Foda Nice"),
        ("Foda Nice", "Mago De Quintanilha"),
        ("Frango", "Conchita"),
        ("Frutis", "Flor"),
        ("Indigesto", "Piroco"),
        ("Jeans", "Erbalife"),
        ("Jornal", "Piroco"),
        ("Joy", "Flor"),
        ("KimKana", "Ambrósio"),
        ("Krika", "Piroco"),
        ("Lacas", "Bean"),
        ("Malelo", "Atchim"),
        ("Mangas", "Matrix"),
        ("Matchero", "Frutis"),
        ("Matrix", "Xantrao"),
        ("Mealheiro", "Infra"),
        ("Meia Grama", "Txaio"),
        ("Mija", "Pilão"),
        ("Nabo", "Zeca Diabo"),
        ("Nharro", "Txaio"),
        ("Nininho", "Malelo"),
        ("Parabento", "Lacas"),
        ("Pardal", "Zeca Diabo"),
        ("Pilão", "Coma"),
        ("Pinta a Calça", "Metralha"),
        ("Pirikito", "Lacas"),
        ("Piroco", "Foda Nice"),
        ("Prepúcio", "Frango"),
        ("Pássaro Maluco", "Pilão"),
        ("Rolhas", "Zeca Diabo"),
        ("Rufus", "Erbalife"),
        ("Saca Rabos", "Vinhas"),
        ("Slimmy", "Conchita"),
        ("Smeagol", "Frutis"),
        ("Snoopy", "Exkill Bill"),
        ("Tainada", "Cigano"),
        ("Tampas", "Matchero"),
        ("TumTum", "Erbalife"),
        ("Txaio", "Matrix"),
        ("Vinhas", "Erbalife"),
        ("Zeca Diabo", "Erbalife"),
        ("dimitri", "Flor")
    };

    private static readonly (string Nickname, Position Position)[] ExpectedAssignments =
    {
        ("Calhau", Position.Magister),
        ("Arbusto", Position.ViceMagister),
        ("Nininho", Position.Secretario),
        ("Prepúcio", Position.PrimeiroTesoureiro),
        ("Rolhas", Position.SegundoTesoureiro),
        ("Ambrósio", Position.PresidenteMesaAssembleia),
        ("Mealheiro", Position.PrimeiroSecretarioMesaAssembleia),
        ("Nharro", Position.SegundoSecretarioMesaAssembleia),
        ("Tainada", Position.PresidenteConselhoFiscal),
        ("Bombeiro", Position.PrimeiroRelatorConselhoFiscal),
        ("Casilhas", Position.SegundoRelatorConselhoFiscal),
        ("Pássaro Maluco", Position.PresidenteConselhoVeteranos),
    };

    public SeedDataMemberSnapshotTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(
            options, new Mock<IHttpContextAccessor>().Object, new AuditContext(), new AuditLogAppender());

        _userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(_context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        foreach (var role in new[] { "Owner", "Admin", "Mod", "Member" })
        {
            _context.Roles.Add(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() });
        }

        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private async Task SeedAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminUser:Username"] = "snapshotowner",
            ["AdminUser:Email"] = "snapshotowner@example.com",
            ["AdminUser:Password"] = "Test-Owner-Pw-1!",
            ["SeedData:MemberPassword"] = "Test-Member-Pw-1!",
        }).Build();

        await SeedData.SeedMembersAsync(configuration, _context, _userManager, isEmptyDb: false);
    }

    [Fact]
    public async Task SeedMembers_CreatesOwnerPlusEightyTwoMembers()
    {
        await SeedAsync();

        (await _userManager.Users.CountAsync()).Should().Be(83);
    }

    [Fact]
    public async Task SeedMembers_AssignsExactMentorPairs()
    {
        await SeedAsync();

        var users = await _userManager.Users.ToListAsync();
        var nicknameById = users.ToDictionary(u => u.Id, u => u.Nickname);
        var actual = users
            .Where(u => u.MentorId != null)
            .Select(u => (u.Nickname, nicknameById[u.MentorId!]))
            .ToList();

        actual.Should().BeEquivalentTo(ExpectedMentors);
    }

    [Fact]
    public async Task SeedMembers_CreatesRoleAssignmentsForCurrentYear()
    {
        await SeedAsync();

        var year = DateTime.Now.Year;
        var nicknameById = await _userManager.Users.ToDictionaryAsync(u => u.Id, u => u.Nickname);
        var actual = (await _context.RoleAssignments.ToListAsync())
            .Select(r => (nicknameById[r.UserId], r.Position))
            .ToList();

        actual.Should().BeEquivalentTo(ExpectedAssignments);
        (await _context.RoleAssignments.AllAsync(r => r.StartYear == year && r.EndYear == year + 1)).Should().BeTrue();
        (await _context.FiscalYears.SingleAsync()).Should().Match<FiscalYear>(f => f.StartYear == 2025 && f.EndYear == 2026);
    }

    [Fact]
    public async Task SeedMembers_MemberFieldsMatchSnapshot()
    {
        await SeedAsync();

        var users = await _userManager.Users.ToListAsync();
        var lines = new List<string>();
        foreach (var u in users.OrderBy(u => u.Nickname, StringComparer.Ordinal))
        {
            var roles = (await _userManager.GetRolesAsync(u)).OrderBy(r => r, StringComparer.Ordinal);
            lines.Add(string.Join("|",
                u.UserName, u.Nickname, u.FirstName, u.LastName,
                string.Join(",", u.Categories.OrderBy(c => c)),
                string.Join(",", u.Positions.OrderBy(p => p)),
                u.YearLeitao, u.YearCaloiro, u.YearTuno, u.IsRetired,
                string.Join(",", roles)));
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
        hash.Should().Be("65C762936A2B00447978C789B44471481E37409F94A8A0BA9B46D676859A7E13");
    }
}
