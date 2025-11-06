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
        // Data for generating members
        var firstNames = new[] { "João", "José", "António", "Manuel", "Francisco", "Pedro", "Miguel", "Rui", "Carlos", "Paulo",
            "Luís", "Marco", "Nuno", "Bruno", "Tiago", "Ricardo", "Diogo", "André", "Fernando", "Jorge",
            "Rafael", "Daniel", "Gonçalo", "Vítor", "Alexandre", "Sérgio", "Hugo", "Bernardo", "Rodrigo", "Fábio",
            "David", "Gabriel", "Leonardo", "Filipe", "Afonso", "Henrique", "Renato", "Samuel", "Tomás", "Martim",
            "Gustavo", "Cristiano", "Eduardo", "Leandro", "Marcelo", "Joaquim", "Guilherme", "Duarte", "Simão", "Edgar" };

        var lastNames = new[] { "Silva", "Santos", "Ferreira", "Pereira", "Oliveira", "Costa", "Rodrigues", "Martins", "Jesus", "Sousa",
            "Fernandes", "Gonçalves", "Gomes", "Lopes", "Marques", "Alves", "Almeida", "Ribeiro", "Pinto", "Carvalho",
            "Teixeira", "Moreira", "Correia", "Mendes", "Nunes", "Soares", "Vieira", "Monteiro", "Cardoso", "Rocha",
            "Neves", "Coelho", "Cruz", "Cunha", "Pires", "Ramos", "Reis", "Simões", "Antunes", "Matos",
            "Fonseca", "Morais", "Batista", "Campos", "Freitas", "Barbosa", "Macedo", "Castro", "Lourenço", "Azevedo" };

        var nicknames = new[] { "Zé", "Manel", "Chico", "Toninho", "Zeca", "Paulinho", "Zé Manel", "Marquinhos", "Nando", "Jorginho",
            "Rafa", "Dani", "Gonçalo", "Vitor", "Alex", "Serginho", "Hugão", "Bernas", "Rodrigão", "Fábio",
            "Davi", "Gabi", "Leo", "Fili", "Afonso", "Henrique", "Renato", "Samu", "Tomás", "Martim",
            "Guga", "Cris", "Edu", "Leandro", "Marcelo", "Quim", "Gui", "Dudu", "Simão", "Edgar",
            "Pipa", "Micas", "Trinca", "Bola", "Caracol", "Mocho", "Grilo", "Panda", "Tigre", "Urso",
            "Javali", "Coelho", "Raposa", "Texugo", "Falcão", "Coruja", "Gato", "Cão", "Lobo", "Lince",
            "Búfalo", "Touro", "Cavalo", "Pónei", "Zebra", "Girafa", "Elefante", "Rinoceronte", "Hipopótamo", "Camelo",
            "Gazela", "Antílope", "Veado", "Alce", "Rena", "Bisonte", "Cabra", "Ovelha", "Porco", "Javardo",
            "Perdiz", "Codorniz", "Gaivota", "Pelicano", "Flamingo", "Cisne", "Pato", "Ganso", "Pavão", "Faisão",
            "Papagaio", "Arara", "Cacatua", "Periquito", "Canário", "Andorinha", "Pardal", "Melro", "Pintassilgo", "Rouxinol",
            "Sardinha", "Carapau", "Atum", "Salmão", "Truta", "Bacalhau", "Dourada", "Robalo", "Linguado", "Pregado",
            "Polvo", "Lula", "Choco", "Camarão", "Lagosta", "Caranguejo", "Mexilhão", "Amêijoa", "Berbigão", "Ostra",
            "Morango", "Framboesa", "Amora", "Cereja", "Pêssego", "Alperce", "Ameixa", "Nectarina", "Tangerina", "Laranja",
            "Limão", "Lima", "Toranja", "Maçã", "Pera", "Marmelo", "Figo", "Kiwi", "Manga", "Papaia",
            "Abacaxi", "Melancia", "Melão", "Meloa", "Banana", "Uva", "Romã", "Caqui", "Lichia", "Maracujá",
            "Goiaba", "Pitanga", "Graviola", "Carambola", "Acerola", "Jambo", "Jabuticaba", "Cajá", "Caju", "Pequi",
            "Buriti", "Bacuri", "Cupuaçu", "Tucumã", "Açaí", "Guaraná", "Cacau", "Castanha", "Avelã", "Noz",
            "Amêndoa", "Pistache", "Macadâmia", "Pinhão", "Coco", "Dendê", "Babaçu", "Licuri", "Ouricuri", "Carnaúba" };

        var degrees = new[] { "Engenharia Informática", "Engenharia Civil", "Engenharia Mecânica", "Engenharia Eletrotécnica",
            "Medicina", "Direito", "Arquitetura", "Gestão", "Economia", "Comunicação Social",
            "Design", "Psicologia", "Biologia", "Física", "Matemática", "Química", "Farmácia" };

        var instruments = new[] { InstrumentType.Guitarra, InstrumentType.Bandolim, InstrumentType.Cavaquinho, InstrumentType.Baixo,
            InstrumentType.Acordeao, InstrumentType.Flauta, InstrumentType.Percussao, InstrumentType.Pandeireta,
            InstrumentType.Estandarte, InstrumentType.Fagote };

        var random = new Random(42); // Fixed seed for reproducibility
        var createdUsers = new List<ApplicationUser>();

        // Generate 200 members
        for (int i = 0; i < 200; i++)
        {
            // Calculate age: starts at 60 for founders, decreases to 18 for youngest
            // First 20 are founders (age 60-59), rest decrease linearly
            int age;
            if (i < 20)
            {
                // Founders: ages 60-59
                age = 60 - (i / 10);
            }
            else
            {
                // Remaining 180: ages 58 down to 18
                // 58 - ((i-20) * 40 / 180) gives us a spread from 58 to 18
                age = 58 - ((i - 20) * 40 / 180);
            }

            var dateOfBirth = DateTime.Now.AddYears(-age).AddDays(-random.Next(0, 365));

            // Select random attributes
            var firstName = firstNames[random.Next(firstNames.Length)];
            var lastName = lastNames[random.Next(lastNames.Length)];
            var nickname = $"{nicknames[i % nicknames.Length]} {i + 1}";
            var degree = degrees[random.Next(degrees.Length)];
            var instrument = instruments[random.Next(instruments.Length)];

            // Determine category and years based on age
            MemberCategory category;
            int? yearLeitao = null;
            int? yearCaloiro = null;
            int? yearTuno = null;

            if (age <= 20)
            {
                category = MemberCategory.Leitao;
                yearLeitao = DateTime.Now.Year - (20 - age);
            }
            else if (age <= 23)
            {
                category = MemberCategory.Caloiro;
                yearCaloiro = DateTime.Now.Year - (23 - age);
                yearLeitao = yearCaloiro - 2;
            }
            else
            {
                category = MemberCategory.Tuno;
                yearTuno = DateTime.Now.Year - (age - 23);
                yearCaloiro = yearTuno - 2;
                yearLeitao = yearCaloiro - 2;
            }

            // Determine mentor (padrinho)
            string? mentorId = null;
            if (i >= 20 && createdUsers.Count > 0)
            {
                // Pick a mentor from previous members (preferably older ones)
                // Bias towards earlier members (older)
                var maxMentorIndex = Math.Min(i - 1, createdUsers.Count - 1);
                var mentorIndex = random.Next(0, Math.Max(1, maxMentorIndex - 10)); // Prefer older members
                mentorId = createdUsers[mentorIndex].Id;
            }

            var user = await CreateSampleUser(
                userManager,
                email: EmailFromNickname(nickname),
                password: "Sample123!",
                role: "Member",
                firstName: firstName,
                lastName: lastName,
                nickname: nickname,
                phone: $"9{random.Next(10000000, 99999999)}",
                degree: degree,
                instrument: instrument,
                positions: new List<Position>(),
                categories: new List<MemberCategory> { category },
                yearTuno: yearTuno,
                yearCaloiro: yearCaloiro,
                yearLeitao: yearLeitao,
                mentorId: mentorId,
                dateOfBirth: dateOfBirth
            );

            if (user != null)
            {
                createdUsers.Add(user);
            }
        }

        // -------- Role assignments (assign some roles to generated members) --------
        if (!await dbContext.RoleAssignments.AnyAsync())
        {
            var currentYear = DateTime.Now.Year;
            var toAdd = new List<RoleAssignment>();

            // Assign roles to some of the generated members (using indices from createdUsers)
            if (createdUsers.Count >= 12)
            {
                var roleAssignments = new[]
                {
                    (createdUsers[0].Id, Position.Magister),
                    (createdUsers[1].Id, Position.ViceMagister),
                    (createdUsers[2].Id, Position.Secretario),
                    (createdUsers[3].Id, Position.PrimeiroTesoureiro),
                    (createdUsers[4].Id, Position.SegundoTesoureiro),
                    (createdUsers[5].Id, Position.PresidenteMesaAssembleia),
                    (createdUsers[6].Id, Position.PrimeiroSecretarioMesaAssembleia),
                    (createdUsers[7].Id, Position.SegundoSecretarioMesaAssembleia),
                    (createdUsers[8].Id, Position.PresidenteConselhoFiscal),
                    (createdUsers[9].Id, Position.PrimeiroRelatorConselhoFiscal),
                    (createdUsers[10].Id, Position.SegundoRelatorConselhoFiscal),
                    (createdUsers[11].Id, Position.PresidenteConselhoVeteranos),
                };

                foreach (var (userId, position) in roleAssignments)
                {
                    toAdd.Add(RoleAssignment.Create(userId, position, currentYear, currentYear + 1));
                }
            }

            if (toAdd.Count > 0)
            {
                await dbContext.RoleAssignments.AddRangeAsync(toAdd);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser?> CreateSampleUser(
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
        int? yearLeitao = null,
        string? mentorId = null,
        DateTime? dateOfBirth = null)
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
                MentorId = mentorId,
                DateOfBirth = dateOfBirth,
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
            user.MentorId = mentorId;
            user.DateOfBirth = dateOfBirth;
            await userManager.UpdateAsync(user);
        }

        return user;
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