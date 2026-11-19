using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    private static async Task AddMembersPart2Async(
        UserManager<ApplicationUser> userManager, string password, List<ApplicationUser> members)
    {
        MemberBuilder Member() => new MemberBuilder(userManager).Password(password);
        Task Add(MemberBuilder builder) => AddMemberAsync(members, builder);

        await Add(Member().Nickname("dimitri").Name("Luis", "Martins")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Frutis").Name("João", "Patuleia")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Cestinho").Name("Daniel", "Ramos")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2006)
            .IsRetired(true));

        await Add(Member().Nickname("Pinta a Calça").Name("André", "Gomes")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true));

        await Add(Member().Nickname("Metralha").Name("Ruben", "Santos")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2005)
            .IsRetired(true));

        await Add(Member().Nickname("Brandao").Name("Jorge", "Brandão")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2005)
            .IsRetired(true));

        await Add(Member().Nickname("Exkill Bill").Name("Miguel", "Vicente")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2005)
            .IsRetired(true));

        await Add(Member().Nickname("Pirikito").Name("Jorge", "Ferreira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2012)
            .IsRetired(true));

        await Add(Member().Nickname("Autoscópio").Name("Sergio", "Silva")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Mata-cães").Name("Henrique", "Spiessens")
            .Role("Member")
            .Category(MemberCategory.Caloiro).YearCaloiro(2024));

        await Add(Member().Nickname("Borat").Name("Rui", "Almeida")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true));

        await Add(Member().Nickname("Castanholas").Name("Renato", "Alves")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Pardal").Name("Andre", "Fernandes")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Ambrósio").Name("Pedro", "Pereira")
            .Role("Admin")
            .Position(Position.PresidenteMesaAssembleia)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("TumTum").Name("Francisco", "Lima")
            .Role("Member")
            .Category(MemberCategory.Caloiro).YearCaloiro(2023));

        await Add(Member().Nickname("KimKana").Name("Joni", "Figueiredo")
            .Role("Member")
            .Category(MemberCategory.Caloiro).YearCaloiro(2024));

        await Add(Member().Nickname("Txaio").Name("Bruno", "Rafael")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Slimmy").Name("Nuno", "Oliveira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Zeca Diabo").Name("Marcos", "António")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true));

        await Add(Member().Nickname("Mija").Name("Vitor", "Teixeira")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Rolhas").Name("Afonso", "Martins")
            .Role("Mod")
            .Position(Position.SegundoTesoureiro)
            .Category(MemberCategory.Caloiro).YearCaloiro(2023));

        await Add(Member().Nickname("Pássaro Maluco").Name("Joel", "Gaspar")
            .Role("Admin")
            .Position(Position.PresidenteConselhoVeteranos)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true));

        await Add(Member().Nickname("Bronha").Name("Eduardo", "Cuevas")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Meia Grama").Name("João", "Pinheiro")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2020));

        await Add(Member().Nickname("Coma").Name("Vitor", "Silva")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));

        await Add(Member().Nickname("Frango").Name("Samuel", "Carneiro")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true));

        await Add(Member().Nickname("Croquetes").Name("Pedro", "Morais")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true));

        await Add(Member().Nickname("Conchita").Name("Manuel", "Esteves")
            .Role("Member")
            .Category(MemberCategory.Tuno).YearTuno(2019));
    }
}
