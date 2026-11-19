using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Data.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    // Order matters: the owner first, then the members, then the data that points at them
    // (fiscal year, role assignments, mentors).
    public static async Task SeedMembersAsync(
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        bool isEmptyDb)
    {
        var memberPassword = configuration["SeedData:MemberPassword"];

        // The bulk member seed below runs only when the Owner-only bootstrap is switched off.
        // Every seeded member needs a password and there is deliberately no default, so it must
        // come from configuration. Validated here, before anything is written, so a missing
        // value cannot leave a half-seeded database behind.
        if (!isEmptyDb)
        {
            RequireSeedPassword(memberPassword, "SeedData:MemberPassword", "run the bulk member seed");
        }

        var ownerUser = await EnsureOwnerAsync(configuration, userManager);

        // An empty-database bootstrap seeds the Owner and nothing else. The guard
        // covers both cases (Owner just created or already present), so an empty database never
        // reaches the bulk member seeding below.
        if (isEmptyDb)
        {
            return;
        }

        var members = new List<ApplicationUser>();
        await AddMembersPart1Async(userManager, memberPassword!, members);
        await AddMembersPart2Async(userManager, memberPassword!, members);
        await AddMembersPart3Async(userManager, memberPassword!, members);

        await SeedFiscalYearAsync(dbContext);
        await SeedRoleAssignmentsAsync(dbContext, userManager);

        members.Add(ownerUser); // the owner is a mentee in the mentor map
        AssignMentors(members);
    }

    // CreateAsync returns null only when Identity rejects the user; the mentor map then fails fast.
    private static async Task AddMemberAsync(List<ApplicationUser> members, MemberBuilder builder) =>
        members.Add((await builder.CreateAsync())!);

    private static async Task AddMembersPart1Async(
        UserManager<ApplicationUser> userManager, string password, List<ApplicationUser> members)
    {
        MemberBuilder Member() => new MemberBuilder(userManager).Password(password);
        Task Add(MemberBuilder builder) => AddMemberAsync(members, builder);

        await Add(Member().Nickname("Nabo").Name("Rafael", "Magalhães")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Nharro").Name("Alexandre", "Caldeira")
            .Role("Mod")
            .Position(Position.SegundoSecretarioMesaAssembleia)
            .Category(MemberCategory.Caloiro).YearCaloiro(2020));

        await Add(Member().Nickname("Atchim").Name("Bruno", "Costa")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Arbusto").Name("Diogo", "Couto")
            .Role("Admin")
            .Position(Position.ViceMagister)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Matchero").Name("Ricardo", "Lameirão")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Erbalife").Name("Carlos", "Silva")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true));

        await Add(Member().Nickname("Badjoncas").Name("Rafael", "Gomes")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2021));

        await Add(Member().Nickname("Snoopy").Name("Diogo", "Morais")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Calimero").Name("Tiago", "Maia")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2022));

        await Add(Member().Nickname("Vinhas").Name("José", "Rebelo")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Malelo").Name("Bruno", "Neves")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2020));

        await Add(Member().Nickname("Prepúcio").Name("João", "Nunes")
            .Role("Admin")
            .Position(Position.PrimeiroTesoureiro)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Nininho").Name("Luís", "Prôta")
            .Role("Mod")
            .Position(Position.Secretario)
            .Category(MemberCategory.Caloiro).YearCaloiro(2023));

        await Add(Member().Nickname("Pilão").Name("Samuel", "Silva")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Tainada").Name("Daniel", "Afonso")
            .Role("Admin")
            .Position(Position.PresidenteConselhoFiscal)
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true));

        await Add(Member().Nickname("Cigano").Name("Ruben", "Freire")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Infra").Name("Alvaro", "Rosas")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2020));

        await Add(Member().Nickname("Drift").Name("João", "Cunha")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Saca Rabos").Name("Zé Tó", "")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2021));

        await Add(Member().Nickname("Tampas").Name("Helder", "Martins")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Indigesto").Name("André", "Batista")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Piroco").Name("Cláudio", "Espadanedo")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2013)
            .IsRetired(true));

        await Add(Member().Nickname("Mago De Quintanilha").Name("Bruno", "Miranda")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2005)
            .IsRetired(true));

        await Add(Member().Nickname("Foda Nice").Name("Bruno", "Berra")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Flor").Name("Marco", "Pinheiro")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Jornal").Name("Nuno", "Jornal")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Krika").Name("André", "Marçal")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Joy").Name("", "")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));
    }
}
