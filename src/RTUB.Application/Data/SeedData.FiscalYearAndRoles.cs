using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    private static async Task SeedFiscalYearAsync(ApplicationDbContext dbContext)
    {
        if (!await dbContext.FiscalYears.AnyAsync())
        {
            var year = new FiscalYear { StartYear = 2025, EndYear = 2026 };
            await dbContext.FiscalYears.AddAsync(year);
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedRoleAssignmentsAsync(
        ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
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
