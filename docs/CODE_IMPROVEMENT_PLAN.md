# RTUB Code Improvement Plan

> **Document Version**: 1.0  
> **Created**: 2026-01-07  
> **Purpose**: Guide incremental refactoring to reduce repetition and improve maintainability

---

## 1. Executive Summary

This document outlines a strategic plan to reduce code repetition and improve the maintainability of the RTUB codebase. The analysis reveals that while the codebase has good foundational patterns in place (Repository pattern, base classes, component library), there are opportunities to consolidate repeated patterns across services and Razor pages.

### Key Goals
- **Reduce code duplication** by 30-40% in identified areas
- **Improve developer experience** through consistent patterns
- **Maintain backward compatibility** during the migration
- **Follow SOLID principles** and .NET/Blazor best practices

---

## 2. Current Architecture Assessment

### What's Working Well ✅

#### Services Layer
1. **Repository Pattern**: Properly implemented with interfaces (`IEventRepository`, `IRehearsalRepository`, etc.)
2. **Storage Service Hierarchy**: Excellent abstraction with:
   - `BaseStorageService<T>` - Common S3 operations
   - `BaseCloudflareStorageService<T>` - Cloudflare R2 specifics
   - `BaseDriveStorageService<T>` - iDrive specifics
3. **Dependency Injection**: Consistent use of constructor injection throughout
4. **Interface Segregation**: Well-defined interfaces in `RTUB.Application/Interfaces/`

#### Component Library (`RTUB.Shared`)
1. **Reusable UI Components**: Cards, Modals, Forms, Tables, etc.
2. **`CrudTablePageBase<TEntity>`**: Excellent base class providing:
   - Search, sorting, and pagination helpers
   - Standard data lifecycle methods
3. **`CrudModalManager<TEntity>`**: Generic modal handling for CRUD operations
4. **Helper Classes**: `SearchHelper`, `SortableTableHelper`, `PaginationHelper`

#### Code Organization
1. **Clean Architecture layers**: Core → Application → Web
2. **Separation of concerns**: Entities, DTOs, Interfaces, Services properly separated
3. **Async/await patterns**: Consistently used throughout

---

## 3. Identified Issues

### 3.1 Services Layer Repetition

#### Issue A: Multiple Similar Cloudflare Storage Services
**Location**: `src/RTUB.Application/Services/`

There are 6 Cloudflare storage services with similar patterns:
- `CloudflareDocumentStorageService`
- `CloudflareEventMediaStorageService`
- `CloudflareEventVideoStorageService`
- `CloudflareGalleryMediaStorageService`
- `CloudflareImageStorageService`
- `CloudflareSongVideoStorageService`

**Current Problem**: Each service has:
- Similar constructor patterns
- Repeated upload/delete logic with slight variations
- Duplicate URL generation logic
- Similar metadata handling

**Example of Repetition**:
```csharp
// Pattern repeated in each service:
var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
var sanitizedFileName = SanitizeFileName(fileName);
var objectKey = $"events/{_environment}/images/{mediaType}/{eventId}_{timestamp}_{sanitizedFileName}";
// ... similar PutObjectRequest setup
```

#### Issue B: "Get by ID, throw if not found" Pattern
**Location**: Multiple services

This pattern is repeated across many services:
```csharp
var entity = await _repository.GetByIdAsync(id);
if (entity == null)
    throw new EntityNotFoundException($"Entity with ID {id} not found");
```

#### Issue C: Similar Audit Log Creation
**Location**: Services that create audit logs

Repeated pattern for creating audit logs:
```csharp
_context.AuditLogs.Add(new AuditLog
{
    EntityType = "...",
    Action = "...",
    UserId = _auditContext.UserId,
    // ... same pattern
});
await _context.SaveChangesAsync();
```

---

### 3.2 Razor Pages Repetition

#### Issue D: Modal State Management Bloat
**Location**: `src/RTUB.Web/Pages/Members/Members.razor` (and others)

**Current Pattern** - Multiple boolean flags per page:
```csharp
private bool showEditModal = false;
private bool showDeleteModal = false;
private bool showViewDetailsModal = false;
private bool showSetNicknameModal = false;
private bool showAnniversariesModal = false;
private bool showBirthdayEmailModal = false;
private bool showActiveMembersManagementModal = false;
// ... Open/Close methods for each
```

**Problem**: Each page duplicates modal state management, even though `CrudModalManager<T>` exists.

#### Issue E: Multiple Pagination Helpers Per Page
**Location**: `Members.razor`, `Events.razor`, and others

**Current Pattern**:
```csharp
private PaginationHelper<ApplicationUser> paginationHelper = new() { PageSize = 30 };
private PaginationHelper<ApplicationUser> paginationHelperLeitoes = new() { PageSize = 18 };
private PaginationHelper<ApplicationUser> anniversariesPaginationHelper = new() { PageSize = 12 };
private PaginationHelper<ActiveMemberData> activeMembersPaginationHelper = new() { PageSize = 12 };
```

Plus corresponding methods:
```csharp
private void HandleRegularMembersPageChange(int newPage) { ... }
private void HandleLeitoesPageChange(int newPage) { ... }
private async Task ChangeAnniversariesPage(int newPage) { ... }
private async Task ChangeActiveMembersPage(int newPage) { ... }
```

#### Issue F: Similar Search/Filter Logic
**Location**: Multiple pages

Repeated pattern:
```csharp
private void ApplyActiveMembersSearch(string term) { ... }
private void ApplyAnniversariesSearch(string term) { ... }
// Same logic, different types
```

#### Issue G: Loading/Empty State Patterns
**Location**: Most pages

Similar patterns for handling loading and empty states:
```csharp
@if (data == null)
{
    <LoadingSpinner />
}
else if (!data.Any())
{
    <EmptyState Title="..." Icon="..." />
}
else
{
    // Content
}
```

---

### 3.3 Code Organization Issues

#### Issue H: Large Razor Page Code Blocks
**Location**: `Members.razor` (3000+ lines)

The page contains:
- Multiple entity types (ActiveMemberData, NicknameFormModel)
- Extensive business logic mixed with UI state management
- Many helper methods that could be extracted

#### Issue I: Nested Classes in Razor Pages
**Example from `Members.razor`**:
```csharp
public class ActiveMemberData
{
    public ApplicationUser Member { get; set; } = null!;
    public MemberStatusResult? StatusData { get; set; }
}

public class NicknameFormModel
{
    [Required(ErrorMessage = "...")]
    public string? Nickname { get; set; }
}
```

---

## 4. Improvement Recommendations

### 4.1 Services Layer Improvements

#### Recommendation S1: Create Generic Media Upload Helper
**Priority**: Medium  
**Effort**: 3-5 days

Create a shared helper for media uploads in `BaseCloudflareStorageService`:

> **Testing Note**: Since this helper consolidates logic from 6 different storage services, comprehensive unit tests are critical. Create tests covering various file types, content types, metadata combinations, and error scenarios before migration.

```csharp
// In BaseCloudflareStorageService.cs
protected async Task<string> UploadMediaAsync(
    Stream fileStream,
    string fileName,
    string contentType,
    string pathTemplate,
    int entityId,
    string publicBaseUrl,
    Dictionary<string, string>? additionalMetadata = null)
{
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    var sanitizedFileName = SanitizeFileName(fileName);
    var objectKey = string.Format(pathTemplate, _environment, entityId, timestamp, sanitizedFileName);
    
    var putRequest = new PutObjectRequest
    {
        BucketName = _bucketName,
        Key = objectKey,
        InputStream = fileStream,
        ContentType = contentType,
        CannedACL = S3CannedACL.PublicRead,
        UseChunkEncoding = false
    };
    
    putRequest.Headers.CacheControl = "public, max-age=31536000, immutable";
    putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
    putRequest.Metadata.Add("x-amz-meta-environment", _environment);
    
    // Add additional metadata
    if (additionalMetadata != null)
    {
        foreach (var kvp in additionalMetadata)
        {
            putRequest.Metadata.Add(kvp.Key, kvp.Value);
        }
    }
    
    await _s3Client.PutObjectAsync(putRequest);
    return $"{publicBaseUrl}/{objectKey}";
}

protected static string SanitizeFileName(string fileName)
{
    var sanitized = string.Concat(fileName.Where(c => 
        char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'));
    return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
}
```

#### Recommendation S2: Create GetByIdOrThrowAsync Extension
**Priority**: High  
**Effort**: 1 day

Create an extension method to reduce repetition:

```csharp
// In RTUB.Application/Extensions/RepositoryExtensions.cs
public static class RepositoryExtensions
{
    public static async Task<TEntity> GetByIdOrThrowAsync<TEntity, TKey>(
        this IRepository<TEntity, TKey> repository,
        TKey id,
        string? customMessage = null) where TEntity : class
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null)
        {
            var entityName = typeof(TEntity).Name;
            throw new EntityNotFoundException(
                customMessage ?? $"{entityName} with ID '{id}' was not found.");
        }
        return entity;
    }
}
```

**Usage**:
```csharp
// Before
var eventEntity = await _eventRepository.GetByIdAsync(id);
if (eventEntity == null)
    throw new EntityNotFoundException($"Event with ID {id} not found");

// After (generic type parameters inferred from repository type)
var eventEntity = await _eventRepository.GetByIdOrThrowAsync<Event, int>(id);
```

#### Recommendation S3: Create Audit Log Helper Service
**Priority**: Medium  
**Effort**: 2 days

Extract audit logging to a dedicated helper:

```csharp
// In RTUB.Application/Services/AuditLogHelper.cs
public interface IAuditLogHelper
{
    Task LogAsync(string entityType, int? entityId, string action, 
                  string entityDisplayName, string changes, bool isCritical = false);
}

public class AuditLogHelper : IAuditLogHelper
{
    private readonly ApplicationDbContext _context;
    private readonly AuditContext _auditContext;
    private readonly ILogger<AuditLogHelper> _logger;

    public async Task LogAsync(string entityType, int? entityId, string action,
                               string entityDisplayName, string changes, bool isCritical = false)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                UserId = _auditContext.UserId,
                UserName = _auditContext.UserName,
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = entityDisplayName,
                IsCriticalAction = isCritical
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log failed for {EntityType} {Action}", entityType, action);
        }
    }
}
```

---

### 4.2 Razor Pages/Components Improvements

#### Recommendation P1: Create Multi-Modal State Manager Component
**Priority**: High  
**Effort**: 3-4 days

Create a component that manages multiple modals:

```csharp
// In RTUB.Shared/Base/MultiModalState.cs
public class MultiModalState<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, bool> _states = new();
    
    public bool IsOpen(TKey key) => _states.GetValueOrDefault(key, false);
    
    public void Open(TKey key)
    {
        CloseAll(); // Only one modal open at a time
        _states[key] = true;
    }
    
    public void Close(TKey key) => _states[key] = false;
    
    public void CloseAll()
    {
        foreach (var key in _states.Keys.ToList())
            _states[key] = false;
    }
    
    public void Toggle(TKey key) => _states[key] = !IsOpen(key);
}
```

**Usage in Razor Page**:
```csharp
@code {
    private enum ModalType { Edit, Delete, View, SetNickname, Anniversaries }
    private MultiModalState<ModalType> modals = new();
    
    // Replace: showEditModal, showDeleteModal, showViewDetailsModal...
    // With: modals.IsOpen(ModalType.Edit), modals.Open(ModalType.Edit)...
}
```

#### Recommendation P2: Create Paginated List Component
**Priority**: Medium  
**Effort**: 3-4 days

Create a component that encapsulates pagination logic:

```razor
@* In RTUB.Shared/Components/Tables/PaginatedList.razor *@
@typeparam TItem

<CascadingValue Value="this">
    @if (IsLoading)
    {
        @LoadingContent
    }
    else if (!Items.Any())
    {
        @EmptyContent
    }
    else
    {
        @ItemsContent(PaginatedItems)
        
        <TablePagination CurrentPage="@CurrentPage"
                        PageSize="@PageSize"
                        TotalItems="@Items.Count()"
                        ItemLabel="@ItemLabel"
                        PageSizeOptions="@PageSizeOptions"
                        OnPageChanged="@HandlePageChange"
                        OnPageSizeChanged="@HandlePageSizeChange" />
    }
</CascadingValue>

@code {
    [Parameter] public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public int PageSize { get; set; } = 30;
    [Parameter] public int[] PageSizeOptions { get; set; } = new[] { 30, 60, 90 };
    [Parameter] public string ItemLabel { get; set; } = "items";
    [Parameter] public RenderFragment? LoadingContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public RenderFragment<IEnumerable<TItem>>? ItemsContent { get; set; }
    [Parameter] public EventCallback OnPageChanged { get; set; }
    
    private PaginationHelper<TItem> _paginationHelper = new();
    private int CurrentPage => _paginationHelper.CurrentPage;
    private IEnumerable<TItem> PaginatedItems => _paginationHelper.GetPageData(Items.ToList());
    
    // ... implementation
}
```

#### Recommendation P3: Create Searchable List Component
**Priority**: Medium  
**Effort**: 2-3 days

Combine search + filter + list into reusable component:

```razor
@* In RTUB.Shared/Components/Common/SearchableList.razor *@
@typeparam TItem

<div class="searchable-list">
    <SearchBar @bind-Value="SearchTerm"
               Placeholder="@SearchPlaceholder"
               OnSearch="@ApplySearch" />
    
    @if (FilterOptions != null)
    {
        <FilterDropdown SelectedValue="@SelectedFilter"
                       SelectedValueChanged="@OnFilterChanged"
                       OnChange="@ApplyFilters"
                       AllItemsLabel="@FilterLabel"
                       Options="@FilterOptions" />
    }
    
    @ChildContent(FilteredItems)
</div>

@code {
    [Parameter] public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
    [Parameter] public string SearchPlaceholder { get; set; } = "Search...";
    [Parameter] public List<Func<TItem, string>>? SearchSelectors { get; set; }
    [Parameter] public IEnumerable<FilterDropdownOption>? FilterOptions { get; set; }
    [Parameter] public string FilterLabel { get; set; } = "Filter";
    [Parameter] public RenderFragment<IEnumerable<TItem>>? ChildContent { get; set; }
    
    private SearchHelper<TItem> _searchHelper = new();
    private string SearchTerm { get; set; } = string.Empty;
    private string SelectedFilter { get; set; } = string.Empty;
    
    private IEnumerable<TItem> FilteredItems => ApplySearchAndFilters();
    
    // ... implementation
}
```

#### Recommendation P4: Create LoadableContent Wrapper
**Priority**: High  
**Effort**: 1 day

Create a simple wrapper for loading/empty/content states:

```razor
@* In RTUB.Shared/Components/Common/LoadableContent.razor *@
@typeparam TItem

@if (IsLoading)
{
    @if (LoadingContent != null)
    {
        @LoadingContent
    }
    else
    {
        <LoadingSpinner />
    }
}
else if (Items == null || !Items.Any())
{
    @if (EmptyContent != null)
    {
        @EmptyContent
    }
    else
    {
        <EmptyState Title="@EmptyTitle" 
                   Icon="@EmptyIcon" 
                   Message="@EmptyMessage" />
    }
}
else
{
    @ChildContent(Items)
}

@code {
    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string EmptyTitle { get; set; } = "No items found";
    [Parameter] public string EmptyIcon { get; set; } = "bi-info-circle";
    [Parameter] public string? EmptyMessage { get; set; }
    [Parameter] public RenderFragment? LoadingContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public RenderFragment<IEnumerable<TItem>>? ChildContent { get; set; }
}
```

---

### 4.3 Code Organization Improvements

#### Recommendation O1: Extract DTOs from Razor Pages
**Priority**: High  
**Effort**: 2 days

Move nested classes to proper DTO files:

```
src/RTUB.Application/DTOs/
├── ActiveMemberData.cs     (move from Members.razor)
├── NicknameFormModel.cs    (move from Members.razor)
└── ...
```

#### Recommendation O2: Extract Complex Logic to Services
**Priority**: Medium  
**Effort**: 3-5 days

Move complex logic from Razor pages to dedicated services:

**Example - Extract member filtering logic**:
```csharp
// In RTUB.Application/Services/MemberFilterService.cs
public interface IMemberFilterService
{
    List<ApplicationUser> FilterByCategory(IEnumerable<ApplicationUser> users, 
                                           string category, string? subCategory = null);
    List<ApplicationUser> FilterByInstrument(IEnumerable<ApplicationUser> users,
                                             string instrumentType,
                                             Dictionary<string, List<MemberInstrument>> userInstruments);
    List<ApplicationUser> FilterActiveOnly(IEnumerable<ApplicationUser> users);
    List<ApplicationUser> SortAndPaginate(IEnumerable<ApplicationUser> users,
                                          string sortColumn, bool ascending,
                                          int page, int pageSize);
}
```

#### Recommendation O3: Create Page Base Classes for Common Patterns
**Priority**: Medium  
**Effort**: 2-3 days

Extend `CrudTablePageBase<T>` or create specialized base classes:

```csharp
// In RTUB.Shared/Base/ManagedModalPageBase.cs
public abstract class ManagedModalPageBase<TEntity> : CrudTablePageBase<TEntity>
{
    protected MultiModalState<string> Modals { get; } = new();
    
    protected TEntity? EditingEntity { get; set; }
    protected TEntity? DeletingEntity { get; set; }
    protected TEntity? ViewingEntity { get; set; }
    
    protected bool IsCreateMode { get; set; }
    protected bool IsSaving { get; set; }
    
    protected virtual void OpenCreateModal()
    {
        IsCreateMode = true;
        EditingEntity = CreateNewEntity();
        Modals.Open("edit");
    }
    
    protected virtual void OpenEditModal(TEntity entity)
    {
        IsCreateMode = false;
        EditingEntity = CloneForEdit(entity);
        Modals.Open("edit");
    }
    
    protected abstract TEntity CreateNewEntity();
    protected abstract TEntity CloneForEdit(TEntity entity);
}
```

---

## 5. Implementation Priority

### High Priority (Complete First)
| # | Recommendation | Impact | Effort |
|---|---------------|--------|--------|
| S2 | GetByIdOrThrowAsync Extension | High | 1 day |
| P4 | LoadableContent Wrapper | High | 2-3 days |
| O1 | Extract DTOs from Razor Pages | High | 2 days |
| P1 | Multi-Modal State Manager | High | 3-4 days |

### Medium Priority (Phase 2)
| # | Recommendation | Impact | Effort |
|---|---------------|--------|--------|
| S1 | Generic Media Upload Helper | Medium | 3-5 days |
| S3 | Audit Log Helper Service | Medium | 2 days |
| P2 | Paginated List Component | Medium | 3-4 days |
| P3 | Searchable List Component | Medium | 2-3 days |
| O2 | Extract Logic to Services | Medium | 3-5 days |

### Low Priority (Phase 3)
| # | Recommendation | Impact | Effort |
|---|---------------|--------|--------|
| O3 | Page Base Classes | Medium | 2-3 days |

---

## 6. Migration Strategy

### Phase 1: Foundation (Weeks 1-2)
1. **Create new helpers and components** without modifying existing code
   - Implement `GetByIdOrThrowAsync` extension
   - Create `LoadableContent` component
   - Create `MultiModalState` class
   
2. **Move DTOs** from Razor pages to proper locations
   - No breaking changes - just file moves with namespace updates

3. **Write tests** for new components

### Phase 2: Gradual Adoption (Weeks 3-4)
1. **Pilot with one page** (e.g., `Events.razor`)
   - Replace loading/empty patterns with `LoadableContent`
   - Replace modal booleans with `MultiModalState`
   - Document lessons learned

2. **Refactor storage services**
   - Add `UploadMediaAsync` helper to base class
   - Update one service at a time
   - Verify storage operations continue to work

### Phase 3: Rollout (Weeks 5-8)
1. **Apply patterns to remaining pages**
   - Prioritize largest/most complex pages
   - One page per PR for easy review and rollback

2. **Apply service patterns**
   - Add `GetByIdOrThrowAsync` to services one at a time
   - Add `AuditLogHelper` integration

3. **Create searchable/paginated components**
   - Use in new pages first
   - Gradually migrate existing pages

### Migration Principles
- ✅ **One change at a time** - Don't refactor multiple patterns simultaneously
- ✅ **Keep existing tests passing** - Don't break functionality
- ✅ **Feature flags** - Use toggles for risky changes (see examples below)
- ✅ **Small PRs** - Easy to review and rollback
- ✅ **Document as you go** - Update component documentation

#### Feature Flag Examples for This Project

**For Component Migrations** (in `appsettings.json`):
```json
{
  "FeatureFlags": {
    "UseNewLoadableContent": true,
    "UseMultiModalState": false
  }
}
```

**In Razor pages** (using `IOptions` pattern):
```razor
@inject IOptions<FeatureFlags> FeatureFlags

@if (FeatureFlags.Value.UseNewLoadableContent)
{
    <LoadableContent Items="@items" ...>
        @* New component *@
    </LoadableContent>
}
else
{
    @* Existing pattern *@
    @if (items == null) { <LoadingSpinner /> }
    else if (!items.Any()) { <EmptyState /> }
    else { @* content *@ }
}
```

**For Service Refactoring** (using conditional DI):
```csharp
// In Program.cs
if (featureFlags.UseNewAuditLogHelper)
{
    services.AddScoped<IAuditLogHelper, AuditLogHelper>();
}
else
{
    services.AddScoped<IAuditLogHelper, LegacyAuditLogHelper>();
}
```

This allows gradual rollout and easy rollback if issues arise.

---

## 7. Appendix: Component Examples

### Example: Using LoadableContent

**Before**:
```razor
@if (users == null)
{
    <div class="avatar-card-grid">
        @for (int i = 0; i < 8; i++)
        {
            <div class="avatar-card-skeleton-card">
                <div class="avatar-card-image-wrapper">
                    <div class="avatar-card-skeleton"></div>
                </div>
            </div>
        }
    </div>
}
else if (!filteredUsers.Any())
{
    <EmptyState Title="Nenhum membro encontrado"
                Icon="bi-people"
                Message="A pesquisa não devolveu resultados." />
}
else
{
    @* Content here *@
}
```

**After**:
```razor
<LoadableContent Items="@filteredUsers" 
                 IsLoading="@(users == null)"
                 EmptyTitle="Nenhum membro encontrado"
                 EmptyIcon="bi-people"
                 EmptyMessage="A pesquisa não devolveu resultados.">
    <LoadingContent>
        <div class="avatar-card-grid">
            @for (int i = 0; i < 8; i++)
            {
                <div class="avatar-card-skeleton-card">...</div>
            }
        </div>
    </LoadingContent>
    <ChildContent Context="users">
        @* Content here using @users *@
    </ChildContent>
</LoadableContent>
```

### Example: Using MultiModalState

**Before**:
```csharp
private bool showEditModal = false;
private bool showDeleteModal = false;
private bool showViewDetailsModal = false;

private void OpenEditModal(User user) {
    editingUser = user;
    showEditModal = true;
}

private void CloseEditModal() {
    showEditModal = false;
    editingUser = null;
}

// ... repeated for each modal
```

**After**:
```csharp
private enum Modal { Edit, Delete, ViewDetails }
private MultiModalState<Modal> modals = new();

private void OpenEditModal(User user) {
    editingUser = user;
    modals.Open(Modal.Edit);
}

private void CloseEditModal() {
    modals.Close(Modal.Edit);
    editingUser = null;
}

// In template
<Modal Show="@modals.IsOpen(Modal.Edit)" ...>
```

---

## 8. Success Metrics

Track these metrics to measure improvement:

| Metric | Current | Target |
|--------|---------|--------|
| Avg lines per Razor page | ~1500 | <800 |
| Modal boolean flags per page | 5-7 | 0 (use MultiModalState) |
| Duplicate pagination handlers | 4+ per page | 1 via component |
| Storage service upload code | ~50 lines each | ~10 lines (using helper) |
| "null check + throw" patterns | 50+ occurrences | 0 (use extension) |

---

*This is a living document. Update as patterns emerge and lessons are learned.*
