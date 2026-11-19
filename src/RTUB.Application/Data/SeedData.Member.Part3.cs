using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    private static async Task AddMembersPart3Async(
        UserManager<ApplicationUser> userManager, string password, List<ApplicationUser> members)
    {
        MemberBuilder Member() => new MemberBuilder(userManager).Password(password);
        Task Add(MemberBuilder builder) => AddMemberAsync(members, builder);

        await Add(Member().Nickname("El Chapo").Name("Luis", "Pinto")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2020));

        await Add(Member().Nickname("Bombeiro").Name("Alexandre", "Figueiredo")
            .Role("Mod")
            .Position(Position.PrimeiroRelatorConselhoFiscal)
            .Category(MemberCategory.Caloiro).YearCaloiro(2021));

        await Add(Member().Nickname("Rufus").Name("Helder", "Vieira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Bate Sacas").Name("Bernardo", "Carvalho")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Calhau").Name("Leonardo", "Cardoso")
            .Role("Admin")
            .Position(Position.Magister)
            .Category(MemberCategory.Tuno).YearTuno(2022));

        await Add(Member().Nickname("Casilhas").Name("Gonçalo", "Borges")
            .Role("Mod")
            .Position(Position.SegundoRelatorConselhoFiscal)
            .Category(MemberCategory.Tuno).YearTuno(2020));

        await Add(Member().Nickname("Mealheiro").Name("Rui", "Guimarães")
            .Role("Admin")
            .Position(Position.PrimeiroSecretarioMesaAssembleia)
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Smeagol").Name("Claudio", "Moreira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Delay").Name("José", "Gonçalves")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Buceta").Name("David", "Ferreira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2021));

        await Add(Member().Nickname("Barbatov").Name("Luis", "Machado")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Xantrao").Name("Maciel", "Santos")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Matrix").Name("Bruno", "Teixeira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Alentejano").Name("Tiago", "Baroa")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Bean").Name("Pedro", "Pinto")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Mangas").Name("Tiago", "Henriques")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Meio Kg").Name("Sérgio", "Ferreira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true));

        await Add(Member().Nickname("Lacas").Name("Daniel", "Cunha")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        await Add(Member().Nickname("Parabento").Name("David", "Gomes")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        await Add(Member().Nickname("Caldas").Name("Vitor", "Caldas")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        await Add(Member().Nickname("Botox").Name("Nurio", "Fernandes")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        await Add(Member().Nickname("Chicaestrume").Name("Luis", "Salsas")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        await Add(Member().Nickname("Esbarrafat").Name("Bruno", "Costa")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true));

        // LEITÕES
        await Add(Member().Nickname("Merdas 1").Name("Porquinho", "Leitao1")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025));

        await Add(Member().Nickname("Merdas 2").Name("Porquinho", "Leitao2")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025));

        await Add(Member().Nickname("Merdas 3").Name("Porquinho", "Leitao3")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025));
    }
}
