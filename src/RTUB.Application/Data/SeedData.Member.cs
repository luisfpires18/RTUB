using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Data.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Globalization;
using System.Text;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    private static MemberBuilder Member(UserManager<ApplicationUser> userManager) => new(userManager);

    // ---- public entrypoint ----
    public static async Task SeedMembersAsync(
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        // ===== 1. OWNER =====
        var defaultUsername = configuration["AdminUser:Username"] ?? "rtub";
        var defaultEmail = configuration["AdminUser:Email"] ?? "admin@rtub.pt";
        var adminPassword = configuration["AdminUser:Password"] ?? "Admin123!";

        var ownerUser = await userManager.FindByNameAsync(defaultUsername);
        if (ownerUser == null)
        {
            ownerUser = new ApplicationUser
            {
                UserName = defaultUsername,
                Email = defaultEmail,
                EmailConfirmed = true,
                FirstName = "Luís",
                LastName = "Pires",
                Nickname = "Jeans",
                PhoneContact = "936854524",
                Positions = new List<Position>(),
                Categories = new List<MemberCategory> { MemberCategory.Tuno },
                YearLeitao = 2013,
                YearCaloiro = 2017,
                YearTuno = 2019,
                Degree = "Engenharia Informática",
                MainInstrument = InstrumentType.Guitarra,
                City = "Bragança",
                Subscribed = true,
            };

            var result = await userManager.CreateAsync(ownerUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(ownerUser, "Owner");
                await userManager.AddToRoleAsync(ownerUser, "Admin");
            }
            else
            {
                throw new Exception($"Unable to create owner user: {string.Join(", ", result.Errors)}");
            }
        }

        // ===== 2. MEMBERS =====
        // GUITARRA
        var nabo = await Member(userManager).Nickname("Nabo").Name("Rafael", "Magalhães")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var nharro = await Member(userManager).Nickname("Nharro").Name("Alexandre", "Caldeira")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.SegundoSecretarioMesaAssembleia)
            .Category(MemberCategory.Caloiro)
            .CreateAsync();

        var atchim = await Member(userManager).Nickname("Atchim").Name("Bruno", "Costa")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var arbusto = await Member(userManager).Nickname("Arbusto").Name("Diogo", "Couto")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.ViceMagister)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var matchero = await Member(userManager).Nickname("Matchero").Name("Ricardo", "Lameirão")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        // example “speaker”
        var speaker = await Member(userManager).Nickname("Erbalife").Name("Carlos", "Silva")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var badjoncas = await Member(userManager).Nickname("Badjoncas").Name("Rafael", "Gomes")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var snoopy = await Member(userManager).Nickname("Snoopy").Name("Diogo", "Morais")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var calimero = await Member(userManager).Nickname("Calimero").Name("Tiago", "Maia")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var vinhas = await Member(userManager).Nickname("Vinhas").Name("José", "Rebelo")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var malelo = await Member(userManager).Nickname("Malelo").Name("Bruno", "Neves")
            .Role("Member").Instrument(InstrumentType.Guitarra).Category(MemberCategory.Tuno).CreateAsync();

        var prepucio = await Member(userManager).Nickname("Prepúcio").Name("João", "Nunes")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.PrimeiroTesoureiro)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var nininho = await Member(userManager).Nickname("Nininho").Name("Luís", "Prôta")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.Secretario)
            .Category(MemberCategory.Caloiro)
            .CreateAsync();

        // BANDOLIM
        var pilao = await Member(userManager).Nickname("Pilão").Name("Samuel", "Silva")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var tainada = await Member(userManager).Nickname("Tainada").Name("Daniel", "Afonso")
            .Role("Admin").Instrument(InstrumentType.Bandolim)
            .Position(Position.PresidenteConselhoFiscal)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var cigano = await Member(userManager).Nickname("Cigano").Name("Ruben", "Freire")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var infra = await Member(userManager).Nickname("Infra").Name("Alvaro", "Rosas")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var drift = await Member(userManager).Nickname("Drift").Name("João", "Cunha")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var sacarabos = await Member(userManager).Nickname("Saca Rabos").Name("Zé Tó", "")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var tampas = await Member(userManager).Nickname("Tampas").Name("Helder", "Martins")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var indigesto = await Member(userManager).Nickname("Indigesto").Name("André", "Batista")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var autoscopio = await Member(userManager).Nickname("Autoscópio").Name("Sergio", "Silva")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Tuno).CreateAsync();

        var matacaes = await Member(userManager).Nickname("Mata-cães").Name("Henrique", "Spiessens")
            .Role("Member").Instrument(InstrumentType.Bandolim).Category(MemberCategory.Caloiro).CreateAsync();

        // CAVAQUINHO
        var borat = await Member(userManager).Nickname("Borat").Name("Rui", "Almeida")
            .Role("Member").Instrument(InstrumentType.Cavaquinho).Category(MemberCategory.Tuno).CreateAsync();

        var castanholas = await Member(userManager).Nickname("Castanholas").Name("Renato", "Alves")
            .Role("Member").Instrument(InstrumentType.Cavaquinho).Category(MemberCategory.Tuno).CreateAsync();

        var pardal = await Member(userManager).Nickname("Pardal").Name("Andre", "Fernandes")
            .Role("Member").Instrument(InstrumentType.Cavaquinho).Category(MemberCategory.Tuno).CreateAsync();

        // ACORDEÃO
        var ambrosio = await Member(userManager).Nickname("Ambrósio").Name("Pedro", "Pereira")
            .Role("Admin").Instrument(InstrumentType.Acordeao)
            .Position(Position.PresidenteMesaAssembleia)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var tumtum = await Member(userManager).Nickname("TumTum").Name("Francisco", "Lima")
            .Role("Member").Instrument(InstrumentType.Acordeao).Category(MemberCategory.Caloiro).CreateAsync();

        // FAGOTE
        var kimkana = await Member(userManager).Nickname("KimKana").Name("Joni", "Figueiredo")
            .Role("Member").Instrument(InstrumentType.Fagote).Category(MemberCategory.Caloiro).CreateAsync();

        // FLAUTA
        var txaio = await Member(userManager).Nickname("Txaio").Name("Bruno", "Rafael")
            .Role("Member").Instrument(InstrumentType.Flauta).Category(MemberCategory.Tuno).CreateAsync();

        var slimmy = await Member(userManager).Nickname("Slimmy").Name("Nuno", "Oliveira")
            .Role("Member").Instrument(InstrumentType.Flauta).Category(MemberCategory.Tuno).CreateAsync();

        // BAIXO
        var zecadiabo = await Member(userManager).Nickname("Zeca Diabo").Name("Marcos", "António")
            .Role("Member").Instrument(InstrumentType.Baixo).Category(MemberCategory.Tuno).CreateAsync();

        var mija = await Member(userManager).Nickname("Mija").Name("Vitor", "Teixeira")
            .Role("Member").Instrument(InstrumentType.Baixo).Category(MemberCategory.Tuno).CreateAsync();

        var rolhas = await Member(userManager).Nickname("Rolhas").Name("Afonso", "Martins")
            .Role("Admin").Instrument(InstrumentType.Baixo)
            .Position(Position.SegundoTesoureiro)
            .Category(MemberCategory.Caloiro)
            .CreateAsync();

        // PERCUSSÃO
        var passaromal = await Member(userManager).Nickname("Pássaro Maluco").Name("Joel", "Gaspar")
            .Role("Admin").Instrument(InstrumentType.Percussao)
            .Position(Position.PresidenteConselhoVeteranos)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var bronha = await Member(userManager).Nickname("Bronha").Name("Eduardo", "Cuevas")
            .Role("Member").Instrument(InstrumentType.Percussao).Category(MemberCategory.Tuno).CreateAsync();

        var meiagrama = await Member(userManager).Nickname("Meia Grama").Name("João", "Pinheiro")
            .Role("Member").Instrument(InstrumentType.Percussao).Category(MemberCategory.Tuno).CreateAsync();

        var coma = await Member(userManager).Nickname("Coma").Name("Vitor", "Silva")
            .Role("Member").Instrument(InstrumentType.Percussao).Category(MemberCategory.Tuno).CreateAsync();

        // PANDEIRETA
        var frango = await Member(userManager).Nickname("Frango").Name("Samuel", "Carneiro")
            .Role("Member").Instrument(InstrumentType.Pandeireta).Category(MemberCategory.Tuno).CreateAsync();

        var croquetes = await Member(userManager).Nickname("Croquetes").Name("Pedro", "Morais")
            .Role("Member").Instrument(InstrumentType.Pandeireta).Category(MemberCategory.Tuno).CreateAsync();

        var conchita = await Member(userManager).Nickname("Conchita").Name("Manuel", "Esteves")
            .Role("Member").Instrument(InstrumentType.Pandeireta).Category(MemberCategory.Tuno).CreateAsync();

        var elchapo = await Member(userManager).Nickname("El Chapo").Name("Luis", "Pinto")
            .Role("Member").Instrument(InstrumentType.Pandeireta).Category(MemberCategory.Tuno).CreateAsync();

        // ESTANDARTE
        var bombeiro = await Member(userManager).Nickname("Bombeiro").Name("Alexandre", "Figueiredo")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.PrimeiroRelatorConselhoFiscal)
            .Category(MemberCategory.Caloiro)
            .CreateAsync();

        var rufus = await Member(userManager).Nickname("Rufus").Name("Helder", "Vieira")
            .Role("Member").Instrument(InstrumentType.Estandarte).Category(MemberCategory.Tuno).CreateAsync();

        var batesacas = await Member(userManager).Nickname("Bate Sacas").Name("Bernardo", "Carvalho")
            .Role("Member").Instrument(InstrumentType.Estandarte).Category(MemberCategory.Tuno).CreateAsync();

        var calhau = await Member(userManager).Nickname("Calhau").Name("Leonardo", "Cardoso")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.Magister)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var casilhas = await Member(userManager).Nickname("Casilhas").Name("Gonçalo", "Borges")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.SegundoRelatorConselhoFiscal)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var mealheiro = await Member(userManager).Nickname("Mealheiro").Name("Rui", "Guimarães")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.PrimeiroSecretarioMesaAssembleia)
            .Category(MemberCategory.Tuno)
            .CreateAsync();

        var smeagol = await Member(userManager).Nickname("Smeagol").Name("Claudio", "Moreira")
            .Role("Member").Instrument(InstrumentType.Estandarte).Category(MemberCategory.Tuno).CreateAsync();

        var delay = await Member(userManager).Nickname("Delay").Name("José", "Gonçalves")
            .Role("Member").Instrument(InstrumentType.Estandarte).Category(MemberCategory.Tuno).CreateAsync();

        var buceta = await Member(userManager).Nickname("Buceta").Name("David", "Ferreira")
            .Role("Member").Instrument(InstrumentType.Estandarte).Category(MemberCategory.Tuno).CreateAsync();

        // LEITÕES
        var merdas1 = await Member(userManager).Nickname("Merdas 1").Name("Porquinho", "Leitao1")
            .Role("Member").Category(MemberCategory.Leitao).CreateAsync();

        var merdas2 = await Member(userManager).Nickname("Merdas 2").Name("Porquinho", "Leitao2")
            .Role("Member").Category(MemberCategory.Leitao).CreateAsync();

        var merdas3 = await Member(userManager).Nickname("Merdas 3").Name("Porquinho", "Leitao3")
            .Role("Member").Category(MemberCategory.Leitao).CreateAsync();


        // ===== 3. FISCAL YEAR =====
        if (!await dbContext.FiscalYears.AnyAsync())
        {
            var year = new FiscalYear { StartYear = 2025, EndYear = 2026 };
            await dbContext.FiscalYears.AddAsync(year);
            await dbContext.SaveChangesAsync();
        }

        // ===== 4. ROLE ASSIGNMENTS =====
        if (!await dbContext.RoleAssignments.AnyAsync())
        {
            var currentYear = DateTime.Now.Year;

            var assignments = new (string Nickname, Position Pos)[]
            {
                ("Calhau",            Position.Magister),
                ("Arbusto",           Position.ViceMagister),
                ("Nininho",           Position.Secretario),
                ("Prepúcio",          Position.PrimeiroTesoureiro),
                ("Rolhas",            Position.SegundoTesoureiro),

                ("Ambrósio",          Position.PresidenteMesaAssembleia),
                ("Mealheiro",         Position.PrimeiroSecretarioMesaAssembleia),
                ("Nharro",            Position.SegundoSecretarioMesaAssembleia),

                ("Tainada",           Position.PresidenteConselhoFiscal),
                ("Bombeiro",          Position.PrimeiroRelatorConselhoFiscal),
                ("Casilhas",          Position.SegundoRelatorConselhoFiscal),

                ("Pássaro Maluco",    Position.PresidenteConselhoVeteranos),
            };

            var toAdd = new List<RoleAssignment>();
            foreach (var (nick, pos) in assignments)
            {
                var username = UsernameFromNickname(nick);
                var user = await userManager.FindByNameAsync(username);
                if (user == null) continue;

                toAdd.Add(RoleAssignment.Create(user.Id, pos, currentYear, currentYear + 1));
            }

            if (toAdd.Count > 0)
            {
                await dbContext.RoleAssignments.AddRangeAsync(toAdd);
                await dbContext.SaveChangesAsync();
            }
        }

        // ===== MENTOR MAP =====
        // Mentor on the right
        SetMentor(tampas, matchero);

        SetMentor(mealheiro, infra);

        SetMentor(nharro, txaio);
        SetMentor(meiagrama, txaio);
        SetMentor(buceta, txaio);

        SetMentor(slimmy, conchita);
        SetMentor(calimero, conchita);
        SetMentor(croquetes, conchita);
        SetMentor(batesacas, conchita);
        SetMentor(atchim, conchita);
        SetMentor(frango, conchita);
        SetMentor(elchapo, conchita);

        SetMentor(arbusto, batesacas);
        SetMentor(calhau, batesacas);

        SetMentor(rolhas, zecadiabo);
        SetMentor(ambrosio, zecadiabo);
        SetMentor(nabo, zecadiabo);
        SetMentor(pardal, zecadiabo);

        SetMentor(prepucio, frango);

        SetMentor(malelo, atchim);

        SetMentor(nininho, malelo);

        SetMentor(bombeiro, arbusto);

        SetMentor(kimkana, ambrosio);

        SetMentor(passaromal, pilao);
        SetMentor(cigano, pilao);

        SetMentor(tainada, cigano);

        SetMentor(ownerUser, speaker);
        SetMentor(badjoncas, speaker);
        SetMentor(drift, speaker);
        SetMentor(tumtum, speaker);
        SetMentor(zecadiabo, speaker);
        SetMentor(bronha, speaker);
        SetMentor(rufus, speaker);
        SetMentor(vinhas, speaker);

        SetMentor(sacarabos, vinhas);

        SetMentor(autoscopio, snoopy);
        SetMentor(borat, autoscopio);

        SetMentor(pilao, coma);
        SetMentor(mija, pilao);
    }

    private static void SetMentor(ApplicationUser? mentee, ApplicationUser? mentor)
    {
        mentee!.MentorId = mentor!.Id;
    }

    private static string UsernameFromNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "unknown";
        string decomp = nickname.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomp.Length);
        foreach (var ch in decomp)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc == UnicodeCategory.NonSpacingMark) continue;

            char c = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }
}
