using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

/// <summary>
/// Database context for the application
/// Extends IdentityDbContext for ASP.NET Core Identity support
/// </summary>
public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditContext _auditContext;
    private readonly IAuditLogAppender _auditLogAppender;
    private bool _isAuditingEnabled = true;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor, AuditContext auditContext, IAuditLogAppender auditLogAppender)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditContext = auditContext;
        _auditLogAppender = auditLogAppender;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply EF Core configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
