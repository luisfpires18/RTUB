# Page Permissions Administration System

## Overview

This document describes the design for an Owner-only administration interface that allows dynamic configuration of page-level permissions using the claims-based authorization model. This system enables the Owner to control who can access which pages without code changes.

---

## 1. Requirements

### 1.1 Functional Requirements

1. **Owner-Only Access**: Only users with the "Owner" role can access the page permissions admin interface
2. **Page Discovery**: System lists all routable pages in the application
3. **Permission Configuration**: Owner can configure required roles, categories, and positions for each page
4. **Rule Storage**: Permission rules stored in database for persistence
5. **Dynamic Enforcement**: Middleware/filter checks permissions on each request
6. **Fallback Behavior**: Pages without explicit rules use existing `[Authorize]` attributes
7. **Owner Protection**: Owner role cannot be locked out of the admin interface

### 1.2 Non-Functional Requirements

1. **Performance**: Permission checks must be fast (cached)
2. **Security**: No way to bypass permission checks
3. **Auditability**: Changes to permissions are logged
4. **Usability**: Simple, clear UI for non-technical users
5. **Maintainability**: Easy to add new pages without code changes

---

## 2. Data Model

### 2.1 PagePermission Entity

```csharp
namespace RTUB.Core.Entities;

/// <summary>
/// Represents permission requirements for a specific page/route
/// </summary>
public class PagePermission
{
    public int Id { get; set; }
    
    /// <summary>
    /// The route path (e.g., "/member/events", "/owner/audit-log")
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Route { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the page (e.g., "Event Management")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what the page does
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Category of the page (e.g., "Administration", "Member Features", "Music")
    /// </summary>
    [MaxLength(100)]
    public string? Category { get; set; }
    
    /// <summary>
    /// Required ASP.NET Identity roles (JSON array)
    /// Empty = any authenticated user
    /// Example: ["Admin", "Owner"]
    /// </summary>
    public string? RequiredRolesJson { get; set; }
    
    /// <summary>
    /// Required member categories (JSON array)
    /// Empty = no category requirement
    /// Example: ["TUNO", "VETERANO", "TUNOSSAURO"]
    /// </summary>
    public string? RequiredCategoriesJson { get; set; }
    
    /// <summary>
    /// Required positions (JSON array)
    /// Empty = no position requirement
    /// Example: ["MAGISTER", "TESOUREIRO"]
    /// </summary>
    public string? RequiredPositionsJson { get; set; }
    
    /// <summary>
    /// Minimum category level required (uses hierarchy)
    /// Example: "CALOIRO" means Caloiro or higher (Tuno, Veterano, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? MinimumCategoryLevel { get; set; }
    
    /// <summary>
    /// Logic operator for combining requirements
    /// "AND" = user must meet ALL requirements
    /// "OR" = user must meet ANY requirement
    /// </summary>
    [MaxLength(10)]
    public string LogicOperator { get; set; } = "AND";
    
    /// <summary>
    /// Whether this permission rule is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this rule should override [Authorize] attributes
    /// </summary>
    public bool OverrideAttributeAuthorization { get; set; } = false;
    
    /// <summary>
    /// Custom error message when access denied
    /// </summary>
    [MaxLength(500)]
    public string? CustomDeniedMessage { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Helper properties (not mapped to database)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> RequiredRoles
    {
        get => JsonSerializer.Deserialize<List<string>>(RequiredRolesJson ?? "[]") ?? new();
        set => RequiredRolesJson = JsonSerializer.Serialize(value);
    }
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> RequiredCategories
    {
        get => JsonSerializer.Deserialize<List<string>>(RequiredCategoriesJson ?? "[]") ?? new();
        set => RequiredCategoriesJson = JsonSerializer.Serialize(value);
    }
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> RequiredPositions
    {
        get => JsonSerializer.Deserialize<List<string>>(RequiredPositionsJson ?? "[]") ?? new();
        set => RequiredPositionsJson = JsonSerializer.Serialize(value);
    }
}
```

### 2.2 Database Schema

```sql
CREATE TABLE PagePermissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Route TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL,
    Description TEXT,
    Category TEXT,
    RequiredRolesJson TEXT,
    RequiredCategoriesJson TEXT,
    RequiredPositionsJson TEXT,
    MinimumCategoryLevel TEXT,
    LogicOperator TEXT NOT NULL DEFAULT 'AND',
    IsActive INTEGER NOT NULL DEFAULT 1,
    OverrideAttributeAuthorization INTEGER NOT NULL DEFAULT 0,
    CustomDeniedMessage TEXT,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT,
    UpdatedAt TEXT,
    UpdatedBy TEXT
);

CREATE INDEX idx_page_permissions_route ON PagePermissions(Route);
CREATE INDEX idx_page_permissions_active ON PagePermissions(IsActive);
```

---

## 3. Service Layer

### 3.1 IPagePermissionService Interface

```csharp
namespace RTUB.Application.Interfaces;

public interface IPagePermissionService
{
    // CRUD operations
    Task<PagePermission?> GetByRouteAsync(string route);
    Task<List<PagePermission>> GetAllAsync();
    Task<List<PagePermission>> GetActivePer missionsAsync();
    Task<PagePermission> CreateAsync(PagePermission permission);
    Task<PagePermission> UpdateAsync(PagePermission permission);
    Task DeleteAsync(int id);
    
    // Authorization checks
    Task<bool> IsAuthorizedAsync(string route, ClaimsPrincipal user);
    Task<PagePermissionResult> CheckPermissionAsync(string route, ClaimsPrincipal user);
    
    // Page discovery
    Task<List<RouteInfo>> DiscoverRoutesAsync();
    Task SyncRoutesAsync(); // Create missing PagePermission entries for new routes
    
    // Cache management
    void ClearCache();
    void ClearCache(string route);
}

public class PagePermissionResult
{
    public bool IsAuthorized { get; set; }
    public string? DeniedReason { get; set; }
    public string? CustomMessage { get; set; }
    public List<string> MissingRequirements { get; set; } = new();
}

public class RouteInfo
{
    public string Route { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool HasExistingPermission { get; set; }
}
```

### 3.2 PagePermissionService Implementation

```csharp
namespace RTUB.Application.Services;

public class PagePermissionService : IPagePermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PagePermissionService> _logger;
    private const string CacheKeyPrefix = "page_permission:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    
    public async Task<PagePermissionResult> CheckPermissionAsync(string route, ClaimsPrincipal user)
    {
        // Owner always has access (except to prevent lockout)
        if (user.IsInRole("Owner"))
        {
            return new PagePermissionResult { IsAuthorized = true };
        }
        
        // Get permission rule from cache or database
        var permission = await GetByRouteAsync(route);
        if (permission == null || !permission.IsActive)
        {
            // No rule defined - allow access (fallback to attribute authorization)
            return new PagePermissionResult { IsAuthorized = true };
        }
        
        var result = new PagePermissionResult();
        var requirements = new List<bool>();
        var missingReqs = new List<string>();
        
        // Check role requirements
        if (permission.RequiredRoles.Any())
        {
            var hasRole = permission.RequiredRoles.Any(role => user.IsInRole(role));
            requirements.Add(hasRole);
            if (!hasRole)
            {
                missingReqs.Add($"Required role: {string.Join(" OR ", permission.RequiredRoles)}");
            }
        }
        
        // Check category requirements
        if (permission.RequiredCategories.Any())
        {
            var hasCategory = permission.RequiredCategories.Any(cat => user.IsCategory(cat));
            requirements.Add(hasCategory);
            if (!hasCategory)
            {
                missingReqs.Add($"Required category: {string.Join(" OR ", permission.RequiredCategories)}");
            }
        }
        
        // Check minimum category level
        if (!string.IsNullOrEmpty(permission.MinimumCategoryLevel))
        {
            var hasLevel = user.IsAtLeastCategory(permission.MinimumCategoryLevel);
            requirements.Add(hasLevel);
            if (!hasLevel)
            {
                missingReqs.Add($"Minimum category: {permission.MinimumCategoryLevel}");
            }
        }
        
        // Check position requirements
        if (permission.RequiredPositions.Any())
        {
            var hasPosition = permission.RequiredPositions.Any(pos => user.HasPosition(pos));
            requirements.Add(hasPosition);
            if (!hasPosition)
            {
                missingReqs.Add($"Required position: {string.Join(" OR ", permission.RequiredPositions)}");
            }
        }
        
        // Apply logic operator
        result.IsAuthorized = permission.LogicOperator.ToUpperInvariant() == "OR"
            ? requirements.Any(r => r)  // Any requirement met
            : requirements.All(r => r); // All requirements met
        
        result.MissingRequirements = missingReqs;
        result.CustomMessage = permission.CustomDeniedMessage;
        
        if (!result.IsAuthorized)
        {
            result.DeniedReason = string.Join(", ", missingReqs);
        }
        
        return result;
    }
    
    public async Task<List<RouteInfo>> DiscoverRoutesAsync()
    {
        // Use reflection or endpoint data source to discover all routes
        // This is a simplified example
        var routes = new List<RouteInfo>();
        
        // Get existing permissions
        var existingPermissions = await GetAllAsync();
        var existingRoutes = existingPermissions.ToDictionary(p => p.Route);
        
        // Example routes (in real implementation, discover from endpoint routing)
        var knownRoutes = new[]
        {
            new { Route = "/", Name = "Home", Category = "Public" },
            new { Route = "/member/events", Name = "Event Management", Category = "Events" },
            new { Route = "/member/rehearsals", Name = "Rehearsal Management", Category = "Rehearsals" },
            new { Route = "/member/members", Name = "Members List", Category = "Members" },
            new { Route = "/member/finance", Name = "Financial Management", Category = "Finance" },
            new { Route = "/owner/audit-log", Name = "Audit Log", Category = "Administration" },
            new { Route = "/owner/user-roles", Name = "User Role Management", Category = "Administration" },
            // Add more routes...
        };
        
        foreach (var route in knownRoutes)
        {
            routes.Add(new RouteInfo
            {
                Route = route.Route,
                DisplayName = route.Name,
                Category = route.Category,
                HasExistingPermission = existingRoutes.ContainsKey(route.Route)
            });
        }
        
        return routes;
    }
}
```

---

## 4. Authorization Middleware

### 4.1 PagePermissionMiddleware

```csharp
namespace RTUB.Web.Middleware;

/// <summary>
/// Middleware that enforces dynamic page permissions
/// </summary>
public class PagePermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PagePermissionMiddleware> _logger;
    
    public PagePermissionMiddleware(RequestDelegate next, ILogger<PagePermissionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, IPagePermissionService permissionService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip for static files, API endpoints, auth endpoints
        if (path.StartsWith("/_") || 
            path.StartsWith("/api/") ||
            path.StartsWith("/auth/") ||
            path.Contains("."))
        {
            await _next(context);
            return;
        }
        
        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context); // Let normal auth handle this
            return;
        }
        
        // Check page permission
        var result = await permissionService.CheckPermissionAsync(path, context.User);
        
        if (!result.IsAuthorized)
        {
            _logger.LogWarning(
                "User {UserName} denied access to {Path}. Reason: {Reason}",
                context.User.Identity.Name,
                path,
                result.DeniedReason);
            
            // Redirect to access denied page with custom message
            context.Response.Redirect($"/access-denied?reason={Uri.EscapeDataString(result.CustomMessage ?? result.DeniedReason ?? "Access Denied")}");
            return;
        }
        
        await _next(context);
    }
}
```

---

## 5. Admin UI

### 5.1 Page Permissions Admin Page

```razor
@page "/owner/page-permissions"
@attribute [Authorize(Roles = "Owner")]
@inject IPagePermissionService PermissionService
@inject NavigationManager Navigation

<PageTitle>Page Permissions</PageTitle>

<div class="container-fluid py-4">
    <div class="row mb-4">
        <div class="col">
            <h1>Page Permissions Administration</h1>
            <p class="text-muted">
                Configure who can access different pages in the application.
                Only users with the Owner role can access this page.
            </p>
        </div>
    </div>
    
    @if (isLoading)
    {
        <div class="text-center py-5">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    }
    else
    {
        <!-- Filter and Search -->
        <div class="row mb-3">
            <div class="col-md-4">
                <input type="text" class="form-control" 
                       @bind="searchQuery" 
                       @bind:event="oninput"
                       placeholder="Search pages..." />
            </div>
            <div class="col-md-3">
                <select class="form-select" @bind="categoryFilter">
                    <option value="">All Categories</option>
                    @foreach (var cat in categories)
                    {
                        <option value="@cat">@cat</option>
                    }
                </select>
            </div>
            <div class="col-md-3">
                <button class="btn btn-primary" @onclick="SyncRoutes">
                    <i class="bi bi-arrow-clockwise"></i> Sync Routes
                </button>
            </div>
        </div>
        
        <!-- Permissions Table -->
        <div class="card">
            <div class="card-body">
                <table class="table table-hover">
                    <thead>
                        <tr>
                            <th>Page</th>
                            <th>Category</th>
                            <th>Required Roles</th>
                            <th>Required Categories</th>
                            <th>Min Category Level</th>
                            <th>Active</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var permission in FilteredPermissions)
                        {
                            <tr>
                                <td>
                                    <strong>@permission.DisplayName</strong><br />
                                    <small class="text-muted">@permission.Route</small>
                                </td>
                                <td>@permission.Category</td>
                                <td>
                                    @if (permission.RequiredRoles.Any())
                                    {
                                        <span>@string.Join(", ", permission.RequiredRoles)</span>
                                    }
                                    else
                                    {
                                        <span class="text-muted">Any</span>
                                    }
                                </td>
                                <td>
                                    @if (permission.RequiredCategories.Any())
                                    {
                                        <span>@string.Join(", ", permission.RequiredCategories)</span>
                                    }
                                    else
                                    {
                                        <span class="text-muted">None</span>
                                    }
                                </td>
                                <td>
                                    @if (!string.IsNullOrEmpty(permission.MinimumCategoryLevel))
                                    {
                                        <span>@permission.MinimumCategoryLevel+</span>
                                    }
                                    else
                                    {
                                        <span class="text-muted">None</span>
                                    }
                                </td>
                                <td>
                                    <span class="badge @(permission.IsActive ? "bg-success" : "bg-secondary")">
                                        @(permission.IsActive ? "Active" : "Inactive")
                                    </span>
                                </td>
                                <td>
                                    <button class="btn btn-sm btn-primary" 
                                            @onclick="() => EditPermission(permission)">
                                        <i class="bi bi-pencil"></i> Edit
                                    </button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>
    }
</div>

<!-- Edit Modal -->
@if (editingPermission != null)
{
    <div class="modal show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Edit Page Permission</h5>
                    <button type="button" class="btn-close" @onclick="CloseEditModal"></button>
                </div>
                <div class="modal-body">
                    <!-- Edit form fields here -->
                    <div class="mb-3">
                        <label class="form-label">Display Name</label>
                        <input type="text" class="form-control" @bind="editingPermission.DisplayName" />
                    </div>
                    
                    <div class="mb-3">
                        <label class="form-label">Required Roles</label>
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" 
                                   checked="@editingPermission.RequiredRoles.Contains("Admin")"
                                   @onchange="(e) => ToggleRole("Admin", e)" />
                            <label class="form-check-label">Admin</label>
                        </div>
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" 
                                   checked="@editingPermission.RequiredRoles.Contains("Member")"
                                   @onchange="(e) => ToggleRole("Member", e)" />
                            <label class="form-check-label">Member</label>
                        </div>
                    </div>
                    
                    <!-- More form fields... -->
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" @onclick="CloseEditModal">Cancel</button>
                    <button type="button" class="btn btn-primary" @onclick="SavePermission">Save Changes</button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private List<PagePermission> permissions = new();
    private List<string> categories = new();
    private bool isLoading = true;
    private string searchQuery = string.Empty;
    private string categoryFilter = string.Empty;
    private PagePermission? editingPermission;
    
    private IEnumerable<PagePermission> FilteredPermissions =>
        permissions.Where(p => 
            (string.IsNullOrEmpty(searchQuery) || 
             p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Route.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(categoryFilter) || p.Category == categoryFilter));
    
    protected override async Task OnInitializedAsync()
    {
        await LoadPermissions();
    }
    
    private async Task LoadPermissions()
    {
        isLoading = true;
        permissions = await PermissionService.GetAllAsync();
        categories = permissions.Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList()!;
        isLoading = false;
    }
    
    private async Task SyncRoutes()
    {
        await PermissionService.SyncRoutesAsync();
        await LoadPermissions();
    }
    
    private void EditPermission(PagePermission permission)
    {
        editingPermission = permission;
    }
    
    private void CloseEditModal()
    {
        editingPermission = null;
    }
    
    private async Task SavePermission()
    {
        if (editingPermission != null)
        {
            await PermissionService.UpdateAsync(editingPermission);
            await LoadPermissions();
            CloseEditModal();
        }
    }
    
    private void ToggleRole(string role, ChangeEventArgs e)
    {
        if (editingPermission == null) return;
        
        var isChecked = (bool)(e.Value ?? false);
        if (isChecked && !editingPermission.RequiredRoles.Contains(role))
        {
            editingPermission.RequiredRoles.Add(role);
        }
        else if (!isChecked)
        {
            editingPermission.RequiredRoles.Remove(role);
        }
    }
}
```

---

## 6. Implementation Plan

### Phase 5A: Data Layer
- [ ] Create PagePermission entity
- [ ] Create migration for PagePermissions table
- [ ] Add DbSet to ApplicationDbContext
- [ ] Seed initial page permissions

### Phase 5B: Service Layer
- [ ] Create IPagePermissionService interface
- [ ] Implement PagePermissionService
- [ ] Implement route discovery mechanism
- [ ] Add memory caching
- [ ] Add audit logging

### Phase 5C: Middleware
- [ ] Create PagePermissionMiddleware
- [ ] Register middleware in Program.cs
- [ ] Create access denied page
- [ ] Add tests for middleware

### Phase 5D: Admin UI
- [ ] Create /owner/page-permissions page
- [ ] Implement permission listing
- [ ] Implement permission editing
- [ ] Add route discovery/sync feature
- [ ] Add search and filtering
- [ ] Style with existing UI framework

### Phase 5E: Testing
- [ ] Unit tests for permission service
- [ ] Integration tests for middleware
- [ ] E2E tests for admin UI
- [ ] Security tests (Owner lockout prevention)

---

## 7. Security Considerations

1. **Owner Protection**: Owner role must always have access to admin interface
2. **Cache Invalidation**: Clear cache when permissions change
3. **Audit Trail**: Log all permission changes
4. **Input Validation**: Validate all permission configurations
5. **SQL Injection**: Use parameterized queries
6. **XSS Protection**: Sanitize custom messages
7. **CSRF Protection**: Use antiforgery tokens

---

## 8. Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task CheckPermission_WithOwner_AlwaysAllows()
{
    // Test that Owner always has access
}

[Fact]
public async Task CheckPermission_WithRequiredRole_AllowsUserWithRole()
{
    // Test role-based permissions
}

[Fact]
public async Task CheckPermission_WithMinimumCategory_AllowsHigherCategory()
{
    // Test category hierarchy
}
```

### Integration Tests
```csharp
[Fact]
public async Task Middleware_BlocksUnauthorizedUser()
{
    // Test middleware blocks access correctly
}

[Fact]
public async Task AdminUI_OnlyAccessibleToOwner()
{
    // Test that only Owner can access admin page
}
```

---

## Conclusion

This page permissions system provides:
1. **Dynamic Control**: Owner can change permissions without deployments
2. **Flexibility**: Multiple authorization dimensions (roles, categories, positions)
3. **Security**: Prevents Owner lockout, audit trail
4. **Performance**: Cached permission checks
5. **User-Friendly**: Simple UI for non-technical configuration

The system integrates seamlessly with the claims-based authorization model from Phases 1-4, providing the final piece of a comprehensive, maintainable authorization system.
