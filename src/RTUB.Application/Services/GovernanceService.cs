using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The public Órgãos Sociais (React track 008). Reads the existing RoleAssignments and member
/// names; no schema change. Fiscal years are listed only when someone held a position in them, so
/// the selector never offers an empty mandate, and the default falls back from the current fiscal
/// year to the latest recorded one instead of showing an empty page every September.
/// </summary>
public sealed class GovernanceService : IGovernanceService
{
    /// <summary>Bodies and positions in display order; the single source for titles and grouping.</summary>
    internal static readonly (string Body, (Position Position, string Title)[] Positions)[] Structure =
    {
        ("Direção", new[]
        {
            (Position.Magister, "Magister"),
            (Position.ViceMagister, "Vice-Magister"),
            (Position.Secretario, "Secretário"),
            (Position.PrimeiroTesoureiro, "1.º Tesoureiro"),
            (Position.SegundoTesoureiro, "2.º Tesoureiro"),
        }),
        ("Mesa da Assembleia", new[]
        {
            (Position.PresidenteMesaAssembleia, "Presidente"),
            (Position.PrimeiroSecretarioMesaAssembleia, "1.º Secretário"),
            (Position.SegundoSecretarioMesaAssembleia, "2.º Secretário"),
        }),
        ("Conselho Fiscal", new[]
        {
            (Position.PresidenteConselhoFiscal, "Presidente"),
            (Position.PrimeiroRelatorConselhoFiscal, "1.º Relator"),
            (Position.SegundoRelatorConselhoFiscal, "2.º Relator"),
        }),
        ("Conselho de Veteranos", new[]
        {
            (Position.PresidenteConselhoVeteranos, "Presidente"),
        }),
        ("Outros cargos", new[]
        {
            (Position.Ensaiador, "Ensaiador"),
        }),
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public GovernanceService(IDbContextFactory<ApplicationDbContext> contexts)
    {
        _contexts = contexts;
    }

    public async Task<PublicGovernanceDto> GetPublicGovernanceAsync(string? fiscalYear)
    {
        await using var db = await _contexts.CreateDbContextAsync();

        var startYears = await db.RoleAssignments
            .Select(a => a.StartYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        if (startYears.Count == 0)
        {
            return new PublicGovernanceDto(Array.Empty<string>(), null, Array.Empty<GovernanceBodyDto>());
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var start = TryParseStartYear(fiscalYear) is { } requested && startYears.Contains(requested)
            ? requested
            : startYears.Contains(current) ? current : startYears[0];

        // Only the public fields are read; nothing else of the member is ever loaded.
        var holders = await db.RoleAssignments
            .Where(a => a.StartYear == start)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new
            {
                a.Position,
                a.Id,
                u.Nickname,
                u.FirstName,
                u.LastName,
                u.ImageUrl
            })
            .ToListAsync();

        var bodies = Structure
            .Select(body => new GovernanceBodyDto(body.Body, body.Positions
                .Select(p => new GovernancePositionDto(p.Title, holders
                    .Where(h => h.Position == p.Position)
                    .OrderBy(h => h.Id)
                    .Select(h => ToMember(h.Nickname, h.FirstName, h.LastName, h.ImageUrl))
                    .ToList()))
                .ToList()))
            .ToList();

        return new PublicGovernanceDto(startYears.Select(Label).ToList(), Label(start), bodies);
    }

    private static string Label(int startYear) => $"{startYear}-{startYear + 1}";

    /// <summary>"2024-2025" → 2024. Anything else → null.</summary>
    private static int? TryParseStartYear(string? fiscalYear)
    {
        var parts = fiscalYear?.Split('-');
        return parts is { Length: 2 }
               && int.TryParse(parts[0], out var start)
               && int.TryParse(parts[1], out var end)
               && end == start + 1
            ? start
            : null;
    }

    internal static GovernanceMemberDto ToMember(string? nickname, string? firstName, string? lastName, string? imageUrl)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        var hasNickname = !string.IsNullOrWhiteSpace(nickname);
        var displayName = hasNickname ? nickname!.Trim() : fullName.Length > 0 ? fullName : "Tuno";

        return new GovernanceMemberDto(
            displayName,
            hasNickname && fullName.Length > 0 ? fullName : null,
            IsSafeImageUrl(imageUrl) ? imageUrl : null);
    }

    /// <summary>An https URL or a same-site path; anything else (javascript:, data:, //host) is dropped.</summary>
    private static bool IsSafeImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (UrlHelper.IsLocalUrl(url)
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));
}
