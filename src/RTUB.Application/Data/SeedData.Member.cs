using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with initial data such as administrator role and user.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedMembersAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        // Keep single owner - Luís Pires (Jeans)
        await CreateSampleUser(userManager,
            email: "jeans@rtub.pt",
            password: "Admin123!",
            role: "Member",
            firstName: "Luís",
            lastName: "Pires",
            nickname: "Jeans",
            phone: "936854524",
            degree: "Engenharia Informática",
            instrument: InstrumentType.Guitarra,
            new List<Position>(),
            new List<MemberCategory> { MemberCategory.Tuno },
            yearLeitao: 2013,
            yearCaloiro: 2017,
            yearTuno: 2019);

        // ========== GUITARRA ==========
        await CreateSampleUser(userManager, EmailFromNickname("Nabo"), "Sample123!", "Member", "Rafael", "Magalhães", "Nabo", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Nharro"), "Sample123!", "Admin", "Alexandre", "Caldeira", "Nharro", "", null, InstrumentType.Guitarra, new List<Position> { Position.SegundoSecretarioMesaAssembleia }, new List<MemberCategory> { MemberCategory.Caloiro });
        await CreateSampleUser(userManager, EmailFromNickname("Atchim"), "Sample123!", "Member", "Bruno", "Costa", "Atchim", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Arbusto"), "Sample123!", "Admin", "Diogo", "Couto", "Arbusto", "", null, InstrumentType.Guitarra, new List<Position> { Position.ViceMagister }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Matchero"), "Sample123!", "Member", "Ricardo", "Lameirão", "Matchero", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Erbalife"), "Sample123!", "Member", "Carlos", "Silva", "Erbalife", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Badjoncas"), "Sample123!", "Member", "Rafael", "Gomes", "Badjoncas", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Snoopy"), "Sample123!", "Member", "Diogo", "Morais", "Snoopy", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Calimero"), "Sample123!", "Member", "Tiago", "Maia", "Calimero", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Vinhas"), "Sample123!", "Member", "José", "Rebelo", "Vinhas", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Malelo"), "Sample123!", "Member", "Bruno", "Neves", "Malelo", "", null, InstrumentType.Guitarra, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Prepúcio"), "Sample123!", "Admin", "João", "Nunes", "Prepúcio", "", null, InstrumentType.Guitarra, new List<Position> { Position.PrimeiroTesoureiro }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Nininho"), "Sample123!", "Admin", "Luís", "Prôta", "Nininho", "", null, InstrumentType.Guitarra, new List<Position> { Position.Secretario }, new List<MemberCategory> { MemberCategory.Caloiro });

        // ========== BANDOLIM ==========
        await CreateSampleUser(userManager, EmailFromNickname("Pilão"), "Sample123!", "Member", "Samuel", "Silva", "Pilão", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Tainada"), "Sample123!", "Admin", "Daniel", "Afonso", "Tainada", "", null, InstrumentType.Bandolim, new List<Position> { Position.PresidenteConselhoFiscal }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Cigano"), "Sample123!", "Member", "Ruben", "Freire", "Cigano", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Infra"), "Sample123!", "Member", "Alvaro", "Rosas", "Infra", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Drift"), "Sample123!", "Member", "João", "Cunha", "Drift", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Saca Rabos"), "Sample123!", "Member", "Zé Tó", "", "Saca Rabos", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Tampas"), "Sample123!", "Member", "Helder", "Martins", "Tampas", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Indigesto"), "Sample123!", "Member", "André", "Batista", "Indigesto", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Autoscópio"), "Sample123!", "Member", "Sergio", "Silva", "Autoscópio", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Mata-cães"), "Sample123!", "Member", "Henrique", "Spiessens", "Mata-cães", "", null, InstrumentType.Bandolim, new List<Position>(), new List<MemberCategory> { MemberCategory.Caloiro });

        // ========== CAVAQUINHO ==========
        await CreateSampleUser(userManager, EmailFromNickname("Borat"), "Sample123!", "Member", "Rui", "Almeida", "Borat", "", null, InstrumentType.Cavaquinho, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Castanholas"), "Sample123!", "Member", "Renato", "Alves", "Castanholas", "", null, InstrumentType.Cavaquinho, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Pardal"), "Sample123!", "Member", "Andre", "Fernandes", "Pardal", "", null, InstrumentType.Cavaquinho, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });

        // ========== ACORDEÃO ==========
        await CreateSampleUser(userManager, EmailFromNickname("Ambrósio"), "Sample123!", "Admin", "Pedro", "Pereira", "Ambrósio", "", null, InstrumentType.Acordeao, new List<Position> { Position.PresidenteMesaAssembleia }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("TumTum"), "Sample123!", "Member", "Francisco", "Lima", "TumTum", "", null, InstrumentType.Acordeao, new List<Position>(), new List<MemberCategory> { MemberCategory.Caloiro });

        // ========== FAGOTE ==========
        await CreateSampleUser(userManager, EmailFromNickname("KimKana"), "Sample123!", "Member", "Joni", "Figueiredo", "KimKana", "", null, InstrumentType.Fagote, new List<Position>(), new List<MemberCategory> { MemberCategory.Caloiro });

        // ========== FLAUTA ==========
        await CreateSampleUser(userManager, EmailFromNickname("Txaio"), "Sample123!", "Member", "Bruno", "Rafael", "Txaio", "", null, InstrumentType.Flauta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Slimmy"), "Sample123!", "Member", "Nuno", "Oliveira", "Slimmy", "", null, InstrumentType.Flauta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });

        // ========== BAIXO ==========
        await CreateSampleUser(userManager, EmailFromNickname("Zeca Diabo"), "Sample123!", "Member", "Marcos", "António", "Zeca Diabo", "", null, InstrumentType.Baixo, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Mija"), "Sample123!", "Member", "Vitor", "Teixeira", "Mija", "", null, InstrumentType.Baixo, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Rolhas"), "Sample123!", "Admin", "Afonso", "Martins", "Rolhas", "", null, InstrumentType.Baixo, new List<Position> { Position.SegundoTesoureiro }, new List<MemberCategory> { MemberCategory.Caloiro });

        // ========== PERCUSSÃO ==========
        await CreateSampleUser(userManager, EmailFromNickname("Pássaro Maluco"), "Sample123!", "Admin", "Joel", "Gaspar", "Pássaro Maluco", "", null, InstrumentType.Percussao, new List<Position> { Position.PresidenteConselhoVeteranos }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Bronha"), "Sample123!", "Member", "Eduardo", "Cuevas", "Bronha", "", null, InstrumentType.Percussao, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Meia Grama"), "Sample123!", "Member", "João", "Pinheiro", "Meia Grama", "", null, InstrumentType.Percussao, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Coma"), "Sample123!", "Member", "Vitor", "Silva", "Coma", "", null, InstrumentType.Percussao, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });

        // ========== PANDEIRETA ==========
        await CreateSampleUser(userManager, EmailFromNickname("Frango"), "Sample123!", "Member", "Samuel", "Carneiro", "Frango", "", null, InstrumentType.Pandeireta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Croquetes"), "Sample123!", "Member", "Pedro", "Morais", "Croquetes", "", null, InstrumentType.Pandeireta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Conchita"), "Sample123!", "Member", "Manuel", "Esteves", "Conchita", "", null, InstrumentType.Pandeireta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("El Chapo"), "Sample123!", "Member", "Luis", "Pinto", "El Chapo", "", null, InstrumentType.Pandeireta, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });

        // ========== ESTANDARTE ==========
        await CreateSampleUser(userManager, EmailFromNickname("Bombeiro"), "Sample123!", "Admin", "Alexandre", "Figueiredo", "Bombeiro", "", null, InstrumentType.Estandarte, new List<Position> { Position.PrimeiroRelatorConselhoFiscal }, new List<MemberCategory> { MemberCategory.Caloiro });
        await CreateSampleUser(userManager, EmailFromNickname("Rufus"), "Sample123!", "Member", "Helder", "Vieira", "Rufus", "", null, InstrumentType.Estandarte, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Bate Sacas"), "Sample123!", "Member", "Bernardo", "Carvalho", "Bate Sacas", "", null, InstrumentType.Estandarte, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Calhau"), "Sample123!", "Admin", "Leonardo", "Cardoso", "Calhau", "", null, InstrumentType.Estandarte, new List<Position> { Position.Magister }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Casilhas"), "Sample123!", "Admin", "Gonçalo", "Borges", "Casilhas", "", null, InstrumentType.Estandarte, new List<Position> { Position.SegundoRelatorConselhoFiscal }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Mealheiro"), "Sample123!", "Admin", "Rui", "Guimarães", "Mealheiro", "", null, InstrumentType.Estandarte, new List<Position> { Position.PrimeiroSecretarioMesaAssembleia }, new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Smeagol"), "Sample123!", "Member", "Claudio", "Moreira", "Smeagol", "", null, InstrumentType.Estandarte, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Delay"), "Sample123!", "Member", "José", "Gonçalves", "Delay", "", null, InstrumentType.Estandarte, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });
        await CreateSampleUser(userManager, EmailFromNickname("Buceta"), "Sample123!", "Member", "David", "Ferreira", "Buceta", "", null, InstrumentType.Estandarte, new List<Position>(), new List<MemberCategory> { MemberCategory.Tuno });

        // ========== LEITÕES ==========
        await CreateSampleUser(userManager, EmailFromNickname("Merdas 1"), "Sample123!", "Member", "Porquinho", "Leitao1", "Merdas 1", "", null, null, new List<Position>(), new List<MemberCategory> { MemberCategory.Leitao });
        await CreateSampleUser(userManager, EmailFromNickname("Merdas 2"), "Sample123!", "Member", "Porquinho", "Leitao2", "Merdas 2", "", null, null, new List<Position>(), new List<MemberCategory> { MemberCategory.Leitao });
        await CreateSampleUser(userManager, EmailFromNickname("Merdas 3"), "Sample123!", "Member", "Porquinho", "Leitao3", "Merdas 3", "", null, null, new List<Position>(), new List<MemberCategory> { MemberCategory.Leitao });

        // -------- Role assignments (by nickname) --------
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
    }

    private static async Task CreateSampleUser(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string? role,
        string firstName,
        string lastName,
        string nickname,
        string phone,
        string? degree,
        InstrumentType? instrument,
        List<Position>? positions,
        List<MemberCategory>? categories,
        int? yearTuno = null,
        int? yearCaloiro = null,
        int? yearLeitao = null)
    {
        // Build login username from the nickname
        var username = UsernameFromNickname(nickname);

        // Find by username (login), not by email
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = username,          // <-- login key
                Email = email,                // still stored for contact/notifications
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Nickname = nickname,
                PhoneContact = phone,
                Degree = degree,
                MainInstrument = instrument,
                YearTuno = yearTuno,
                YearCaloiro = yearCaloiro,
                YearLeitao = yearLeitao,
                Positions = positions ?? new List<Position>(),
                Categories = categories ?? new List<MemberCategory>(),
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded && !string.IsNullOrEmpty(role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
        else
        {
            user.Email = email;              // keep email up to date
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Nickname = nickname;
            user.PhoneContact = phone;
            user.Degree = degree;
            user.Positions = positions ?? new List<Position>();
            user.Categories = categories ?? new List<MemberCategory>();
            user.YearTuno = yearTuno;
            user.YearCaloiro = yearCaloiro;
            user.YearLeitao = yearLeitao;
            user.MainInstrument = instrument;
            await userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Build nickname-based email: lowercase, remove spaces/accents/punctuation.
    /// </summary>
    private static string EmailFromNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "unknown@rtub.pt";

        // Strip diacritics
        string decomp = nickname.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomp.Length);
        foreach (var ch in decomp)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc == UnicodeCategory.NonSpacingMark) continue;

            char c = char.ToLowerInvariant(ch);
            // keep only letters and digits
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return $"{sb}@rtub.pt";
    }

    /// <summary>
    /// Build a username from nickname: lowercase, remove spaces/accents/punctuation.
    /// </summary>
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