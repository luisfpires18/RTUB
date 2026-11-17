using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
/// The member admin tools of the React /members (React track 018, docs/react-members.md). Same
/// behaviour, rules now server-side (<see cref="MembersAuthorization"/>; the old page only hid buttons):
/// - create: username from the nickname (or, for a Leitão "sem alcunha", the email's local part), a generated password
///   the member must change, role Member, the categories, the instruments, and the welcome email;
/// - edit: everything but the username; a complete Leitão / Caloiro / Tuno date pair changes only for Owner or the
///   current Magister (others' changes to it are ignored, as before); Fundador = Tuno since December 1991, Fundador and
///   Tuno Honorário have no Leitão / Caloiro dates and no padrinho; nobody is their own padrinho;
/// - instruments one by one (the first is the primary; removing the primary promotes the next);
/// - Leitões: "Definir alcunha" (new nickname and username, email to the member), expel and reactivate;
/// - the push reminder, under the old list's conditions ("Tornar ativo" and its permanent override removed in 044B);
/// - a new member starts active only as a Leitão, whose Leitão date defaults to the creation month
///   (<see cref="MemberActivityStatusEvaluator.ApplyInitialState"/>);
/// - delete: the old hard delete with related data; Owner any member, Admin Leitões only.
/// No schema change.
/// </summary>
public sealed partial class MemberAdminService : IMemberAdminService
{
    public const int MentorLimit = 20;
    public const int MinYear = 1990;
    private static readonly Dictionary<string, MemberCategory> BaseCategories = new()
    {
        ["Leitao"] = MemberCategory.Leitao,
        ["Caloiro"] = MemberCategory.Caloiro,
        ["Tuno"] = MemberCategory.Tuno,
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRoleAssignmentService _assignments;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberMentorService _mentors;
    private readonly IMemberStatusService _status;
    private readonly IUserProfileService _profiles;
    private readonly IEmailNotificationService _email;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _pushFactory;
    private readonly ILogger<MemberAdminService> _logger;
    private readonly TimeProvider _time;

    public MemberAdminService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IRoleAssignmentService assignments,
        IMemberInstrumentService instruments,
        IMemberMentorService mentors,
        IMemberStatusService status,
        IUserProfileService profiles,
        IEmailNotificationService email,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        ILogger<MemberAdminService> logger,
        TimeProvider time)
    {
        _time = time;
        _contexts = contexts;
        _users = users;
        _assignments = assignments;
        _instruments = instruments;
        _mentors = mentors;
        _status = status;
        _profiles = profiles;
        _email = email;
        _push = push;
        _pushFactory = pushFactory;
        _logger = logger;
    }
}
