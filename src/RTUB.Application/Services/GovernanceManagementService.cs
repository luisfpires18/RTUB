using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /member/roles behind the React /roles (React track 016, docs/react-governance.md). Same behaviour,
/// rules now server-side (<see cref="GovernanceAuthorization"/>):
/// - fiscal years: create one from 1991 up to the current one, never twice, never in the future; no edit or delete
///   (the old page had neither);
/// - positions: the fixed 13 of <see cref="GovernanceService.Structure"/>; assign one member to a position of an
///   existing fiscal year under the existing <see cref="IRoleAssignmentValidationService"/> rules (no Leitão, no Caloiro
///   president, a Tuno Veterano for the Conselho de Veteranos, one holder per position, one position per member except
///   Ensaiador), or remove an assignment; in the current fiscal year both update the member's role and positions through
///   <see cref="IRoleManagementService"/>, as before. Expelled members are no longer offered or accepted;
/// - the RGI: a pre-signed URL of docs/rtub_rgi.pdf. No schema change.
/// </summary>
public sealed class GovernanceManagementService : IGovernanceManagementService
{
    public const int FirstStartYear = 1991;
    public const int SearchLimit = 20;
    public const string RgiPath = "docs/rtub_rgi.pdf";

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IRoleAssignmentService _assignments;
    private readonly IRoleAssignmentValidationService _validation;
    private readonly IRoleManagementService _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IServiceProvider _services;
    private readonly ILogger<GovernanceManagementService> _logger;

    public GovernanceManagementService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IFiscalYearService fiscalYears,
        IRoleAssignmentService assignments,
        IRoleAssignmentValidationService validation,
        IRoleManagementService roles,
        UserManager<ApplicationUser> users,
        IServiceProvider services,
        ILogger<GovernanceManagementService> logger)
    {
        _contexts = contexts;
        _fiscalYears = fiscalYears;
        _assignments = assignments;
        _validation = validation;
        _roles = roles;
        _users = users;
        _services = services;
        _logger = logger;
    }

    public async Task<EventResult<GovernanceManageDto>> GetAsync(string? fiscalYear, ClaimsPrincipal user) =>
        Refusal<GovernanceManageDto>(user) ?? EventResult<GovernanceManageDto>.Ok(await BuildAsync(fiscalYear));

    public async Task<EventResult<GovernanceManageDto>> CreateFiscalYearAsync(GovernanceFiscalYearInput input, ClaimsPrincipal user)
    {
        if (Refusal<GovernanceManageDto>(user) is { } refused)
        {
            return refused;
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        if (input.StartYear < FirstStartYear || input.StartYear > current)
        {
            return EventResult<GovernanceManageDto>.Invalid("startYear",
                $"Escolha um ano entre {FirstStartYear} e {current}: não é possível criar anos letivos futuros.");
        }

        if (await _fiscalYears.GetFiscalYearByStartYearAsync(input.StartYear) is not null)
        {
            return EventResult<GovernanceManageDto>.Invalid("startYear", "Este ano letivo já existe.");
        }

        try
        {
            await _fiscalYears.CreateFiscalYearAsync(input.StartYear);
        }
        catch (InvalidOperationException)
        {
            // Created by someone else in the meantime.
            return EventResult<GovernanceManageDto>.Invalid("startYear", "Este ano letivo já existe.");
        }

        return EventResult<GovernanceManageDto>.Ok(await BuildAsync(GovernanceService.Label(input.StartYear)));
    }

    public async Task<EventResult<IReadOnlyList<GovernanceCandidateDto>>> SearchMembersAsync(string? query, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<GovernanceCandidateDto>>(user) is { } refused)
        {
            return refused;
        }

        var words = EventParticipantsAdminService.Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return EventResult<IReadOnlyList<GovernanceCandidateDto>>.Ok(Array.Empty<GovernanceCandidateDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: every member in memory (~100), as the old page did (Categories is a JSON column); page it if it grows.
        var members = await db.Users.AsNoTracking().Where(u => !u.IsExpelled).ToListAsync();

        return EventResult<IReadOnlyList<GovernanceCandidateDto>>.Ok(members
            .Where(u => !u.IsLeitao())
            .Select(u => (u, who: GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl),
                text: EventParticipantsAdminService.Fold($"{u.Nickname} {u.FirstName} {u.LastName} {u.Email}")))
            // Every word somewhere in the nickname, name or email, as the old multi-word search. The email is
            // matched, never returned.
            .Where(x => words.All(w => x.text.Contains(w)))
            .OrderBy(x => x.who.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new GovernanceCandidateDto(x.u.Id, x.who.DisplayName, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    public async Task<EventResult<GovernanceManageDto>> AssignAsync(GovernanceAssignmentInput input, ClaimsPrincipal user)
    {
        if (Refusal<GovernanceManageDto>(user) is { } refused)
        {
            return refused;
        }

        if (GovernanceService.TryParseStartYear(input.FiscalYear) is not { } start
            || await _fiscalYears.GetFiscalYearByStartYearAsync(start) is null)
        {
            return EventResult<GovernanceManageDto>.Invalid("fiscalYear", "Ano letivo inválido.");
        }

        // Names only: "3" would parse as a Position too.
        if (string.IsNullOrEmpty(input.Position) || !Enum.IsDefined(typeof(Position), input.Position))
        {
            return EventResult<GovernanceManageDto>.Invalid("position", "Cargo inválido.");
        }

        var position = Enum.Parse<Position>(input.Position);
        var member = string.IsNullOrEmpty(input.UserId) ? null : await _users.FindByIdAsync(input.UserId);
        if (member is null || member.IsExpelled)
        {
            return EventResult<GovernanceManageDto>.Invalid("userId", "Escolha um membro.");
        }

        var label = GovernanceService.Label(start);
        if (_validation.ValidateRoleAssignment(member, position, label, await _assignments.GetAllRoleAssignmentsAsync()) is { } error)
        {
            return EventResult<GovernanceManageDto>.Invalid("assignment", error);
        }

        await _assignments.CreateRoleAssignmentAsync(member.Id, position, start, start + 1, null, GovernanceAuthorization.UserId(user));
        await _roles.PromoteUserForPositionAsync(member, position, start, FiscalYearHelper.GetCurrentFiscalYearStartYear());

        return EventResult<GovernanceManageDto>.Ok(await BuildAsync(label));
    }

    public async Task<EventResult<GovernanceManageDto>> RemoveAsync(int assignmentId, ClaimsPrincipal user)
    {
        if (Refusal<GovernanceManageDto>(user) is { } refused)
        {
            return refused;
        }

        if (await _assignments.GetRoleAssignmentByIdAsync(assignmentId) is not { } assignment)
        {
            return EventResult<GovernanceManageDto>.Fail(EventResultStatus.NotFound);
        }

        await _assignments.DeleteRoleAssignmentAsync(assignment.Id);
        if (await _users.FindByIdAsync(assignment.UserId) is { } member)
        {
            await _roles.DemoteUserForPositionAsync(member, assignment.Position, assignment.StartYear, FiscalYearHelper.GetCurrentFiscalYearStartYear());
        }

        return EventResult<GovernanceManageDto>.Ok(await BuildAsync(GovernanceService.Label(assignment.StartYear)));
    }

    public async Task<EventResult<GovernanceRgiDto>> GetRgiAsync(ClaimsPrincipal user)
    {
        if (!GovernanceAuthorization.IsMember(user))
        {
            return EventResult<GovernanceRgiDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!GovernanceAuthorization.CanViewRgi(user))
        {
            return EventResult<GovernanceRgiDto>.Fail(EventResultStatus.Forbidden);
        }

        try
        {
            // Resolved here, not injected: storage that cannot be built (no R2 credentials) must not take the fiscal
            // year and assignment endpoints down with it.
            var documents = _services.GetRequiredService<IDocumentStorageService>();
            return EventResult<GovernanceRgiDto>.Ok(new GovernanceRgiDto(await documents.GetDocumentUrlAsync(RgiPath)));
        }
        catch (Exception ex)
        {
            // The old page said "not available" on any storage error; so does the React one.
            _logger.LogWarning(ex, "Could not sign the RGI URL");
            return EventResult<GovernanceRgiDto>.Ok(new GovernanceRgiDto(null));
        }
    }

    private async Task<GovernanceManageDto> BuildAsync(string? fiscalYear)
    {
        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var years = (await _fiscalYears.GetAllFiscalYearsAsync()).Select(y => y.StartYear).Distinct().OrderByDescending(y => y).ToList();
        var available = Enumerable.Range(FirstStartYear, Math.Max(0, current - FirstStartYear + 1)).Except(years).OrderByDescending(y => y).ToList();

        int? start = GovernanceService.TryParseStartYear(fiscalYear) is { } requested && years.Contains(requested)
            ? requested
            : years.Contains(current) ? current : years.Count > 0 ? years[0] : null;
        if (start is not { } shown)
        {
            return new GovernanceManageDto(Array.Empty<string>(), null, available, Array.Empty<GovernanceManageBodyDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var holders = await db.RoleAssignments
            .Where(a => a.StartYear == shown && a.EndYear == shown + 1)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { a.Id, a.Position, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        var bodies = GovernanceService.Structure
            .Select(body => new GovernanceManageBodyDto(body.Body, body.Positions
                .Select(p => new GovernanceManagePositionDto(p.Position.ToString(), p.Title, holders
                    .Where(h => h.Position == p.Position)
                    .OrderBy(h => h.Id)
                    .Select(h =>
                    {
                        var who = GovernanceService.ToMember(h.Nickname, h.FirstName, h.LastName, h.ImageUrl);
                        return new GovernanceHolderDto(h.Id, who.DisplayName, who.FullName, who.AvatarUrl);
                    })
                    .ToList()))
                .ToList()))
            .ToList();

        return new GovernanceManageDto(years.Select(GovernanceService.Label).ToList(), GovernanceService.Label(shown), available, bodies);
    }

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !GovernanceAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !GovernanceAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;
}
