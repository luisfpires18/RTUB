using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Data.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    // ---- public entrypoint ----
    public static async Task SeedMembersAsync(
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        bool isEmptyDb)
    {
        // ===== 1. OWNER =====
        var defaultUsername = configuration["AdminUser:Username"] ?? "rtub";
        var defaultEmail = configuration["AdminUser:Email"] ?? "admin@rtub.pt";
        var adminPassword = configuration["AdminUser:Password"];
        var memberPassword = configuration["SeedData:MemberPassword"];

        // The bulk member seed below runs only when the Owner-only bootstrap is switched off.
        // Every seeded member needs a password and there is deliberately no default, so it must
        // come from configuration. Validated here, before anything is written, so a missing
        // value cannot leave a half-seeded database behind.
        if (!isEmptyDb)
        {
            RequireSeedPassword(memberPassword, "SeedData:MemberPassword", "run the bulk member seed");
        }

        // Every member is built through this, so the password is supplied in exactly one place.
        MemberBuilder Member(UserManager<ApplicationUser> manager) =>
            new MemberBuilder(manager).Password(memberPassword!);

        var ownerUser = await userManager.FindByNameAsync(defaultUsername);
        if (ownerUser == null)
        {
            // Fail closed: never bootstrap a privileged account with a password that is
            // absent, blank, or one of the documented placeholders. There is deliberately
            // no fallback default. The value itself is never logged.
            RequireSeedPassword(adminPassword, "AdminUser:Password", "bootstrap the Owner account");

            ownerUser = new ApplicationUser
            {
                UserName = defaultUsername,
                Email = defaultEmail,
                EmailConfirmed = true,
                FirstName = "Luís",
                LastName = "Pires",
                Nickname = "Jeans",
                PhoneNumber = "936854524",
                Positions = new List<Position>(),
                Categories = new List<MemberCategory> { MemberCategory.Tuno },
                YearLeitao = 2013,
                YearCaloiro = 2017,
                YearTuno = 2019,
                Degree = "Engenharia Informática",
                City = "Bragança",
                Subscribed = true,
            };

            var result = await userManager.CreateAsync(ownerUser, adminPassword!);
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

        // An empty-database bootstrap seeds the Owner and nothing else. This check used to sit
        // inside the create-success branch, so a call with the Owner already present fell through
        // to the bulk member seeding below. That path is unreachable from InitializeAsync (it
        // returns early once any user exists) and the bulk members now fail closed for want of a
        // password, so the guard is hoisted to cover both cases.
        if (isEmptyDb)
        {
            return;
        }

        // ===== 2. MEMBERS =====
        var nabo = await Member(userManager).Nickname("Nabo").Name("Rafael", "Magalhães")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var nharro = await Member(userManager).Nickname("Nharro").Name("Alexandre", "Caldeira")
            .Role("Mod").Instrument(InstrumentType.Guitarra)
            .Position(Position.SegundoSecretarioMesaAssembleia)
            .Category(MemberCategory.Caloiro).YearCaloiro(2020)
            .CreateAsync();

        var atchim = await Member(userManager).Nickname("Atchim").Name("Bruno", "Costa")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var arbusto = await Member(userManager).Nickname("Arbusto").Name("Diogo", "Couto")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.ViceMagister)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var matchero = await Member(userManager).Nickname("Matchero").Name("Ricardo", "Lameirão")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var speaker = await Member(userManager).Nickname("Erbalife").Name("Carlos", "Silva")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true)
            .CreateAsync();

        var badjoncas = await Member(userManager).Nickname("Badjoncas").Name("Rafael", "Gomes")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2021)
            .CreateAsync();

        var snoopy = await Member(userManager).Nickname("Snoopy").Name("Diogo", "Morais")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var calimero = await Member(userManager).Nickname("Calimero").Name("Tiago", "Maia")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2022)
            .CreateAsync();

        var vinhas = await Member(userManager).Nickname("Vinhas").Name("José", "Rebelo")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var malelo = await Member(userManager).Nickname("Malelo").Name("Bruno", "Neves")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2020)
            .CreateAsync();

        var prepucio = await Member(userManager).Nickname("Prepúcio").Name("João", "Nunes")
            .Role("Admin").Instrument(InstrumentType.Guitarra)
            .Position(Position.PrimeiroTesoureiro)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var nininho = await Member(userManager).Nickname("Nininho").Name("Luís", "Prôta")
            .Role("Mod").Instrument(InstrumentType.Guitarra)
            .Position(Position.Secretario)
            .Category(MemberCategory.Caloiro).YearCaloiro(2023)
            .CreateAsync();

        var pilao = await Member(userManager).Nickname("Pilão").Name("Samuel", "Silva")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var tainada = await Member(userManager).Nickname("Tainada").Name("Daniel", "Afonso")
            .Role("Admin").Instrument(InstrumentType.Bandolim)
            .Position(Position.PresidenteConselhoFiscal)
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true)
            .CreateAsync();

        var cigano = await Member(userManager).Nickname("Cigano").Name("Ruben", "Freire")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var infra = await Member(userManager).Nickname("Infra").Name("Alvaro", "Rosas")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2020)
            .CreateAsync();

        var drift = await Member(userManager).Nickname("Drift").Name("João", "Cunha")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var sacarabos = await Member(userManager).Nickname("Saca Rabos").Name("Zé Tó", "")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2021)
            .CreateAsync();

        var tampas = await Member(userManager).Nickname("Tampas").Name("Helder", "Martins")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var indigesto = await Member(userManager).Nickname("Indigesto").Name("André", "Batista")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var piroco = await Member(userManager).Nickname("Piroco").Name("Cláudio", "Espadanedo")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2013)
            .IsRetired(true)
            .CreateAsync();

        var mago = await Member(userManager).Nickname("Mago De Quintanilha").Name("Bruno", "Miranda")
              .Role("Member").Instrument(InstrumentType.Flauta)
              .Category(MemberCategory.Tuno).YearTuno(2005)
              .IsRetired(true)
              .CreateAsync();

        var fodanice = await Member(userManager).Nickname("Foda Nice").Name("Bruno", "Berra")
              .Role("Member").Instrument(InstrumentType.Flauta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var flor = await Member(userManager).Nickname("Flor").Name("Marco", "Pinheiro")
              .Role("Member").Instrument(InstrumentType.Pandeireta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var jornal = await Member(userManager).Nickname("Jornal").Name("Nuno", "Jornal")
              .Role("Member").Instrument(InstrumentType.Estandarte)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var krika = await Member(userManager).Nickname("Krika").Name("André", "Marçal")
              .Role("Member").Instrument(InstrumentType.Bandolim)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var joy = await Member(userManager).Nickname("Joy").Name("", "")
              .Role("Member").Instrument(InstrumentType.Pandeireta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var dimitri = await Member(userManager).Nickname("dimitri").Name("Luis", "Martins")
              .Role("Member").Instrument(InstrumentType.Pandeireta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var frutis = await Member(userManager).Nickname("Frutis").Name("João", "Patuleia")
              .Role("Member").Instrument(InstrumentType.Pandeireta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var cestinho = await Member(userManager).Nickname("Cestinho").Name("Daniel", "Ramos")
              .Role("Member").Instrument(InstrumentType.Pandeireta)
              .Category(MemberCategory.Tuno).YearTuno(2006)
              .IsRetired(true)
              .CreateAsync();

        var pinta = await Member(userManager).Nickname("Pinta a Calça").Name("André", "Gomes")
              .Role("Member").Instrument(InstrumentType.Acordeao)
              .Category(MemberCategory.Tuno).YearTuno(2015)
              .IsRetired(true)
              .CreateAsync();

        var metralha = await Member(userManager).Nickname("Metralha").Name("Ruben", "Santos")
              .Role("Member").Instrument(InstrumentType.Acordeao)
              .Category(MemberCategory.Tuno).YearTuno(2005)
              .IsRetired(true)
              .CreateAsync();

        var brandao = await Member(userManager).Nickname("Brandao").Name("Jorge", "Brandão")
              .Role("Member").Instrument(InstrumentType.Guitarra)
              .Category(MemberCategory.Tuno).YearTuno(2005)
              .IsRetired(true)
              .CreateAsync();

        var esquilo = await Member(userManager).Nickname("Exkill Bill").Name("Miguel", "Vicente")
              .Role("Member").Instrument(InstrumentType.Bandolim)
              .Category(MemberCategory.Tuno).YearTuno(2005)
              .IsRetired(true)
              .CreateAsync();

        var pirikito = await Member(userManager).Nickname("Pirikito").Name("Jorge", "Ferreira")
              .Role("Member").Instrument(InstrumentType.Guitarra)
              .Category(MemberCategory.Tuno).YearTuno(2012)
              .IsRetired(true)
              .CreateAsync();

        var autoscopio = await Member(userManager).Nickname("Autoscópio").Name("Sergio", "Silva")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var matacaes = await Member(userManager).Nickname("Mata-cães").Name("Henrique", "Spiessens")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Caloiro).YearCaloiro(2024)
            .CreateAsync();

        var borat = await Member(userManager).Nickname("Borat").Name("Rui", "Almeida")
            .Role("Member").Instrument(InstrumentType.Cavaquinho)
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true)
            .CreateAsync();

        var castanholas = await Member(userManager).Nickname("Castanholas").Name("Renato", "Alves")
            .Role("Member").Instrument(InstrumentType.Cavaquinho)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var pardal = await Member(userManager).Nickname("Pardal").Name("Andre", "Fernandes")
            .Role("Member").Instrument(InstrumentType.Cavaquinho)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var ambrosio = await Member(userManager).Nickname("Ambrósio").Name("Pedro", "Pereira")
            .Role("Admin").Instrument(InstrumentType.Acordeao)
            .Position(Position.PresidenteMesaAssembleia)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var tumtum = await Member(userManager).Nickname("TumTum").Name("Francisco", "Lima")
            .Role("Member").Instrument(InstrumentType.Acordeao)
            .Category(MemberCategory.Caloiro).YearCaloiro(2023)
            .CreateAsync();

        var kimkana = await Member(userManager).Nickname("KimKana").Name("Joni", "Figueiredo")
            .Role("Member").Instrument(InstrumentType.Fagote)
            .Category(MemberCategory.Caloiro).YearCaloiro(2024)
            .CreateAsync();

        var txaio = await Member(userManager).Nickname("Txaio").Name("Bruno", "Rafael")
            .Role("Member").Instrument(InstrumentType.Flauta)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var slimmy = await Member(userManager).Nickname("Slimmy").Name("Nuno", "Oliveira")
            .Role("Member").Instrument(InstrumentType.Flauta)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var zecadiabo = await Member(userManager).Nickname("Zeca Diabo").Name("Marcos", "António")
            .Role("Member").Instrument(InstrumentType.Baixo)
            .Category(MemberCategory.Tuno).YearTuno(2015)
            .IsRetired(true)
            .CreateAsync();

        var mija = await Member(userManager).Nickname("Mija").Name("Vitor", "Teixeira")
            .Role("Member").Instrument(InstrumentType.Baixo)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var rolhas = await Member(userManager).Nickname("Rolhas").Name("Afonso", "Martins")
            .Role("Mod").Instrument(InstrumentType.Baixo)
            .Position(Position.SegundoTesoureiro)
            .Category(MemberCategory.Caloiro).YearCaloiro(2023)
            .CreateAsync();

        var passaromal = await Member(userManager).Nickname("Pássaro Maluco").Name("Joel", "Gaspar")
            .Role("Admin").Instrument(InstrumentType.Percussao)
            .Position(Position.PresidenteConselhoVeteranos)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var bronha = await Member(userManager).Nickname("Bronha").Name("Eduardo", "Cuevas")
            .Role("Member").Instrument(InstrumentType.Percussao)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var meiagrama = await Member(userManager).Nickname("Meia Grama").Name("João", "Pinheiro")
            .Role("Member").Instrument(InstrumentType.Percussao)
            .Category(MemberCategory.Tuno).YearTuno(2020)
            .CreateAsync();

        var coma = await Member(userManager).Nickname("Coma").Name("Vitor", "Silva")
            .Role("Member").Instrument(InstrumentType.Percussao)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var frango = await Member(userManager).Nickname("Frango").Name("Samuel", "Carneiro")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var croquetes = await Member(userManager).Nickname("Croquetes").Name("Pedro", "Morais")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var conchita = await Member(userManager).Nickname("Conchita").Name("Manuel", "Esteves")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var elchapo = await Member(userManager).Nickname("El Chapo").Name("Luis", "Pinto")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2020)
            .CreateAsync();

        var bombeiro = await Member(userManager).Nickname("Bombeiro").Name("Alexandre", "Figueiredo")
            .Role("Mod").Instrument(InstrumentType.Estandarte)
            .Position(Position.PrimeiroRelatorConselhoFiscal)
            .Category(MemberCategory.Caloiro).YearCaloiro(2021)
            .CreateAsync();

        var rufus = await Member(userManager).Nickname("Rufus").Name("Helder", "Vieira")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2017)
            .IsRetired(true)
            .CreateAsync();

        var batesacas = await Member(userManager).Nickname("Bate Sacas").Name("Bernardo", "Carvalho")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var calhau = await Member(userManager).Nickname("Calhau").Name("Leonardo", "Cardoso")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.Magister)
            .Category(MemberCategory.Tuno).YearTuno(2022)
            .CreateAsync();

        var casilhas = await Member(userManager).Nickname("Casilhas").Name("Gonçalo", "Borges")
            .Role("Mod").Instrument(InstrumentType.Estandarte)
            .Position(Position.SegundoRelatorConselhoFiscal)
            .Category(MemberCategory.Tuno).YearTuno(2020)
            .CreateAsync();

        var mealheiro = await Member(userManager).Nickname("Mealheiro").Name("Rui", "Guimarães")
            .Role("Admin").Instrument(InstrumentType.Estandarte)
            .Position(Position.PrimeiroSecretarioMesaAssembleia)
            .Category(MemberCategory.Tuno).YearTuno(2019)
            .CreateAsync();

        var smeagol = await Member(userManager).Nickname("Smeagol").Name("Claudio", "Moreira")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var delay = await Member(userManager).Nickname("Delay").Name("José", "Gonçalves")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2018)
            .IsRetired(true)
            .CreateAsync();

        var buceta = await Member(userManager).Nickname("Buceta").Name("David", "Ferreira")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2021)
            .CreateAsync();

        var machadi = await Member(userManager).Nickname("Barbatov").Name("Luis", "Machado")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2016)
            .IsRetired(true)
            .CreateAsync();

        var teddyboy = await Member(userManager).Nickname("Xantrao").Name("Maciel", "Santos")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var matrix = await Member(userManager).Nickname("Matrix").Name("Bruno", "Teixeira")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var alan = await Member(userManager).Nickname("Alentejano").Name("Tiago", "Baroa")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var bean = await Member(userManager).Nickname("Bean").Name("Pedro", "Pinto")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var mangas = await Member(userManager).Nickname("Mangas").Name("Tiago", "Henriques")
            .Role("Member").Instrument(InstrumentType.Bandolim)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var meiokg = await Member(userManager).Nickname("Meio Kg").Name("Sérgio", "Ferreira")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2000)
            .IsRetired(true)
            .CreateAsync();

        var lacas = await Member(userManager).Nickname("Lacas").Name("Daniel", "Cunha")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();

        var parabento = await Member(userManager).Nickname("Parabento").Name("David", "Gomes")
            .Role("Member").Instrument(InstrumentType.Pandeireta)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();

        var caldas = await Member(userManager).Nickname("Caldas").Name("Vitor", "Caldas")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();

        var botox = await Member(userManager).Nickname("Botox").Name("Nurio", "Fernandes")
            .Role("Member").Instrument(InstrumentType.Estandarte)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();

        var chicaestrume = await Member(userManager).Nickname("Chicaestrume").Name("Luis", "Salsas")
            .Role("Member").Instrument(InstrumentType.Cavaquinho)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();

        var esbarrafat = await Member(userManager).Nickname("Esbarrafat").Name("Bruno", "Costa")
            .Role("Member").Instrument(InstrumentType.Guitarra)
            .Category(MemberCategory.Tuno).YearTuno(2010)
            .IsRetired(true)
            .CreateAsync();


        // LEITÕES
        var merdas1 = await Member(userManager).Nickname("Merdas 1").Name("Porquinho", "Leitao1")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025)
            .CreateAsync();

        var merdas2 = await Member(userManager).Nickname("Merdas 2").Name("Porquinho", "Leitao2")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025)
            .CreateAsync();

        var merdas3 = await Member(userManager).Nickname("Merdas 3").Name("Porquinho", "Leitao3")
            .Role("Member").Category(MemberCategory.Leitao).YearLeitao(2025)
            .CreateAsync();

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

        SetMentor(indigesto, piroco);
        SetMentor(jornal, piroco);
        SetMentor(krika, piroco);

        SetMentor(speaker, fodanice);
        SetMentor(flor, fodanice);
        SetMentor(piroco, fodanice);

        SetMentor(joy, flor);
        SetMentor(dimitri, flor);
        SetMentor(frutis, flor);

        SetMentor(smeagol, frutis);
        SetMentor(matchero, frutis);

        SetMentor(cestinho, joy);

        SetMentor(delay, cestinho);
        SetMentor(conchita, cestinho);

        SetMentor(caldas, alan);
        SetMentor(fodanice, mago);

        SetMentor(lacas, bean);

        SetMentor(parabento, lacas);
        SetMentor(pirikito, lacas);

        SetMentor(txaio, matrix);
        SetMentor(mangas, matrix);
        SetMentor(chicaestrume, matrix);
        SetMentor(matrix, teddyboy);

        SetMentor(esbarrafat, esquilo);
        SetMentor(snoopy, esquilo);

        SetMentor(castanholas, pinta);
        SetMentor(pinta, metralha);
        SetMentor(botox, metralha);

        SetMentor(machadi, esbarrafat);
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

    /// <summary>
    /// Throws unless <paramref name="password"/> is a real, externally supplied secret. Each
    /// caller checks only on the path that actually creates users, so a database that needs no
    /// seeding still starts without any of these settings configured.
    /// </summary>
    private static void RequireSeedPassword(string? password, string configKey, string what)
    {
        if (!string.IsNullOrWhiteSpace(password) && !SeedPasswordPlaceholders.Contains(password))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot {what}: the configuration value '{configKey}' (environment variable " +
            $"'{configKey.Replace(":", "__", StringComparison.Ordinal)}') is missing, blank, or " +
            "still set to a placeholder. Supply a real password from a secure configuration " +
            "source before seeding. There is no default password.");
    }

    private static readonly HashSet<string> SeedPasswordPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "your-admin-password",
            "changeme",
            "change-me",
            "password",
        };
}
