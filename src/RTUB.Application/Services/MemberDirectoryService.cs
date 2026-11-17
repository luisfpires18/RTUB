using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The read side of the React /members (React track 017,
/// docs/react-members.md). Same data and rules, delegated to the services the old page used:
/// <see cref="IMemberFilterService"/> (search over name, nickname, email, phone and city; category, Tuno sub-category,
/// instrument and "só ativos"; Tunos Honorários only when asked for or searched), <see cref="IActiveMemberFilterService"/>,
/// <see cref="IMemberAnniversaryService"/>, <see cref="IMemberHierarchyService"/>, <see cref="IMemberStatusService"/> and
/// <see cref="IMemberStatisticsService"/>. Like the old page load, a Leitão with no cached status gets it computed.
/// Signed-in members only (<see cref="MembersAuthorization"/>). No schema change.
/// </summary>
public sealed partial class MemberDirectoryService : IMemberDirectoryService
{
    private readonly TimeProvider _time;

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private static readonly string[] Categories = { "Leitao", "Caloiro", "Tuno", "TunoHonorario" };
    private static readonly string[] SubCategories = { "Tuno", "Veterano", "Tunossauro", "Fundador" };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IRoleAssignmentService _assignments;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberFilterService _filter;
    private readonly IActiveMemberFilterService _activeFilter;
    private readonly IMemberAnniversaryService _anniversaries;
    private readonly IMemberHierarchyService _hierarchy;
    private readonly IMemberStatusService _status;
    private readonly IMemberStatisticsService _statistics;
    private readonly ILogger<MemberDirectoryService> _logger;

    public MemberDirectoryService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IRoleAssignmentService assignments,
        IMemberInstrumentService instruments,
        IMemberFilterService filter,
        IActiveMemberFilterService activeFilter,
        IMemberAnniversaryService anniversaries,
        IMemberHierarchyService hierarchy,
        IMemberStatusService status,
        IMemberStatisticsService statistics,
        ILogger<MemberDirectoryService> logger,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _assignments = assignments;
        _instruments = instruments;
        _filter = filter;
        _activeFilter = activeFilter;
        _anniversaries = anniversaries;
        _hierarchy = hierarchy;
        _status = status;
        _statistics = statistics;
        _logger = logger;
    }
}
