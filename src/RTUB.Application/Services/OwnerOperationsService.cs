using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The work behind the React Owner tools /emails, /notifications, /users, /owner/tracing and /owner/db
/// (task 035, docs/react-owner-operations.md). The same services do the work (email sender, push sender, audit log,
/// database viewer, member status sync); what was only checked in the browser before (lengths, required fields, the SQL
/// rules, the "DELETE ALL" confirmation) is checked here. Owner only: the endpoints refuse everyone else first.
/// </summary>
public partial class OwnerOperationsService : IOwnerOperationsService
{
    private readonly TimeProvider _time;

    public const int EmailTitleMax = 200;
    public const int EmailContentMax = 5000;
    public const int PushTitleMax = 100;
    public const int PushBodyMax = 500;
    public const string TruncateConfirmation = "DELETE ALL";

    /// <summary>The roles the old dialog offered, highest first.</summary>
    public static readonly string[] Roles = ["Owner", "Admin", "Mod", "Member"];

    private static readonly int[] UserPageSizes = [20, 40, 60, 80, 100];
    private static readonly int[] AuditPageSizes = [20, 40, 60, 80, 100];
    private static readonly int[] TablePageSizes = [10, 25, 50, 100];

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailNotificationService _email;
    private readonly IPushNotificationService _push;
    private readonly IAuditLogService _audit;
    private readonly IMemberStatusService _memberStatus;
    private readonly IUserProfileService _profiles;
    private readonly IDatabaseViewerService _database;
    private readonly ISqlValidationService _sql;
    private readonly ILogger<OwnerOperationsService> _logger;

    public OwnerOperationsService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IEmailNotificationService email,
        IPushNotificationService push,
        IAuditLogService audit,
        IMemberStatusService memberStatus,
        IUserProfileService profiles,
        IDatabaseViewerService database,
        ISqlValidationService sql,
        ILogger<OwnerOperationsService> logger,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _users = users;
        _email = email;
        _push = push;
        _audit = audit;
        _memberStatus = memberStatus;
        _profiles = profiles;
        _database = database;
        _sql = sql;
        _logger = logger;
    }
}
