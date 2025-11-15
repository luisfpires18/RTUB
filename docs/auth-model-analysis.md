# Authorization Model Analysis

## Executive Summary

This document provides a comprehensive analysis of the current authentication and authorization model in the RTUB application as part of the migration to a claims-based, policy-driven system.

**Current State:**
- 53 category-based authorization checks across the codebase
- 88 role-based authorization checks
- 3 ASP.NET Identity Roles (Admin, Member, Owner)
- 7 Member Categories (Leitao, Caloiro, Tuno, Veterano, Tunossauro, TunoHonorario, Fundador)
- 14 Positions (organizational roles)
- Extension methods in ApplicationUserExtensions.cs providing helper methods
- Authorization logic scattered across Razor pages and components

---

## 1. Current Roles

### ASP.NET Identity Roles

| Role | Description | Usage Count | Primary Use Cases |
|------|-------------|-------------|-------------------|
| **Owner** | System owner with full privileges | ~30 usages | - Page permissions admin<br>- Critical configuration<br>- User role management<br>- Audit log access<br>- Label management<br>- Album/song management |
| **Admin** | Administrator with elevated privileges | ~60 usages | - Event management<br>- Rehearsal management<br>- Finance management<br>- Request approval<br>- Logistics management<br>- Inventory management<br>- Shop management |
| **Member** | Regular authenticated member | ~5 usages | - View roles page<br>- Basic member features |

### Role Enforcement Patterns

1. **Attribute-based** (3 pages):
   ```csharp
   @attribute [Authorize(Roles = "Owner")]
   ```
   - AuditLog.razor
   - UserRoles.razor
   - Labels.razor

2. **AuthorizeView component** (~80 usages):
   ```razor
   <AuthorizeView Roles="Admin">
       <!-- Protected content -->
   </AuthorizeView>
   ```

3. **Programmatic checks** (~10 usages):
   ```csharp
   var isAdmin = user.IsInRole("Admin");
   var isOwner = user.IsInRole("Owner");
   ```

4. **Policy-based** (1 policy):
   ```csharp
   o.AddPolicy("RequireAdministratorRole", p => p.RequireRole("Admin"));
   ```

---

## 2. Current Categories

### Member Categories Enum

| Category | Description | Usage Count | Key Characteristics |
|----------|-------------|-------------|---------------------|
| **Leitao** | Probationary member | 15 | - Not official member<br>- Limited permissions<br>- Cannot participate in some activities |
| **Caloiro** | First year member | 10 | - Official member<br>- Cannot hold certain positions<br>- Special admin rules (Caloiro Admin) |
| **Tuno** | Full member | 12 | - Full participation rights<br>- Can be mentor<br>- Can hold most positions |
| **Veterano** | 2+ years as Tuno | 1 | - Seniority recognition<br>- Leadership eligibility |
| **Tunossauro** | 6+ years as Tuno | 1 | - Highest seniority<br>- Special recognition |
| **TunoHonorario** | Honorary member | 1 | - Special status<br>- Recognition without progression |
| **Fundador** | Founder (1991) | 5 | - Historical significance<br>- Special timeline display |

### Category Check Patterns

**Extension Methods** (ApplicationUserExtensions.cs):
```csharp
- IsLeitao() - Checks if user has Leitao category
- IsCaloiro() - Checks if user has Caloiro category
- IsTuno() - Checks if user has Tuno category
- IsVeterano() - Checks if user has Veterano category
- IsTunossauro() - Checks if user has Tunossauro category
- IsTunoHonorario() - Checks if user has TunoHonorario category
- IsFundador() - Checks if user has Fundador category
```

**Composite Checks** (ApplicationUserExtensions.cs):
```csharp
- IsOnlyLeitao() - Only Leitao, not progressed
- IsTunoOrHigher() - Tuno, Veterano, or Tunossauro (12 usages)
- IsEffectiveMember() - Caloiro or higher (excludes Leitao)
- CanBeMentor() - Alias for IsTunoOrHigher()
- CanHoldPresidentPosition() - Alias for IsTunoOrHigher()
- IsNotOnlyLeitao() - Has progressed beyond Leitao
```

### Category Storage

Categories are stored as JSON in the `CategoriesJson` column of ApplicationUser:
```csharp
public string? CategoriesJson { get; set; }

public List<MemberCategory> Categories
{
    get => JsonSerializer.Deserialize<List<MemberCategory>>(CategoriesJson) ?? new List<MemberCategory>();
    set => CategoriesJson = JsonSerializer.Serialize(value);
}
```

---

## 3. Current Positions

### Position Enum (14 positions)

| Position | Section | Description |
|----------|---------|-------------|
| **Magister** | Direção | President/Leader |
| **ViceMagister** | Direção | Vice President |
| **Secretario** | Direção | Secretary |
| **PrimeiroTesoureiro** | Direção | First Treasurer |
| **SegundoTesoureiro** | Direção | Second Treasurer |
| **PresidenteMesaAssembleia** | Mesa da Assembleia | Assembly President |
| **PrimeiroSecretarioMesaAssembleia** | Mesa da Assembleia | First Assembly Secretary |
| **SegundoSecretarioMesaAssembleia** | Mesa da Assembleia | Second Assembly Secretary |
| **PresidenteConselhoFiscal** | Conselho Fiscal | Fiscal Council President |
| **PrimeiroRelatorConselhoFiscal** | Conselho Fiscal | First Fiscal Reporter |
| **SegundoRelatorConselhoFiscal** | Conselho Fiscal | Second Fiscal Reporter |
| **PresidenteConselhoVeteranos** | Conselho de Veteranos | Veterans Council President |
| **Ensaiador** | Outros Cargos | Rehearsal Director |

### Position Storage

Positions are stored as JSON in the `PositionsJson` column of ApplicationUser:
```csharp
public string? PositionsJson { get; set; }

public List<Position> Positions
{
    get => JsonSerializer.Deserialize<List<Position>>(PositionsJson) ?? new List<Position>();
    set => PositionsJson = JsonSerializer.Serialize(value);
}
```

**Note**: Positions are currently stored but not actively used for authorization. They are displayed on the Roles page and in user profiles.

---

## 4. Usage Locations

### By File Type

| File Type | Category Checks | Role Checks | Total |
|-----------|----------------|-------------|-------|
| Razor Pages | 44 | 82 | 126 |
| C# Services | 3 | 2 | 5 |
| Shared Components | 6 | 4 | 10 |
| **Total** | **53** | **88** | **141** |

### Critical Files with Complex Authorization Logic

#### 1. **MainLayout.razor** (12 checks)
- Role checks: Admin, Owner (5 usages)
- Category checks: IsLeitao, IsCaloiro, IsTuno (7 usages)
- **Purpose**: 
  - Show/hide admin menu items
  - Display user status badge (Leitão/Caloiro/Tuno)
  - Control access to administrative features

#### 2. **Public/Roles.razor** (48 checks)
- Role checks: Admin only (47 usages)
- Category checks: IsLeitao, IsCaloiro, IsTuno (3 usages)
- **Purpose**: 
  - Manage member roles and categories
  - Validate position assignments
  - Control who can modify roles

#### 3. **Member/Events.razor** (15 checks)
- Role checks: Admin, Owner (13 usages)
- Category checks: IsCaloiro, ParticipationModal IsLeitao (2 usages)
- **Purpose**: 
  - Create/edit/delete events
  - Manage event participation
  - Control event visibility

#### 4. **Member/Rehearsals.razor** (10 checks)
- Role checks: Admin, Owner (9 usages)
- Category checks: IsCaloiro, ParticipationModal IsLeitao (3 usages)
- **Purpose**: 
  - Manage rehearsals
  - Track attendance
  - Caloiro admin restrictions

#### 5. **Member/Profile.razor** (8 checks)
- Category checks: IsLeitao, IsCaloiro, IsTuno, IsFundador, IsTunoOrHigher (8 usages)
- **Purpose**: 
  - Display timeline based on category
  - Show/hide profile sections
  - Control mentor assignment visibility

#### 6. **Member/Members.razor** (8 checks)
- Role checks: Admin, Owner (4 usages)
- Category checks: IsLeitao, IsFundador (4 usages)
- **Purpose**: 
  - List and filter members
  - Separate Leitões from regular members
  - Control member editing capabilities

#### 7. **Member/Requests.razor** (8 checks)
- Role checks: Admin, Owner (5 usages)
- Category checks: IsCaloiro via IsCaloiroAdmin() (3 usages)
- **Purpose**: 
  - Request approval workflow
  - Special handling for "Caloiro Admin" role

#### 8. **Member/Finance.razor** (7 checks)
- Role checks: Admin, Owner (6 usages)
- Category checks: IsLeitao (1 usage)
- **Purpose**: 
  - Financial transaction management
  - Restrict certain operations to admins
  - Different fiscal year visibility for Leitao

#### 9. **Member/Hierarchy.razor** (4 checks)
- Category checks: IsTunoOrHigher (4 usages)
- **Purpose**: 
  - Display mentor-mentee hierarchy
  - Validate mentor eligibility
  - Show orphaned members without valid mentors

#### 10. **Member/Slideshows.razor** (4 checks)
- Role checks: Admin, Owner (3 usages)
- Category checks: IsCaloiro (1 usage)
- **Purpose**: 
  - Slideshow management
  - Special restriction: Caloiro Admins cannot add/edit/delete

### Special Authorization Patterns

#### "Caloiro Admin" Pattern
Found in 3 files (Events, Rehearsals, Requests):
```csharp
private bool IsCaloiroAdmin()
{
    return currentUser.IsCaloiro() && isAdmin && !isOwner;
}
```
**Purpose**: Admins who are Caloiros have restricted permissions compared to other admins.

#### Combined Category Checks
```razor
@if ((!user.IsLeitao() || user.IsCaloiro() || user.IsTuno()) && !user.IsFundador())
```
**Purpose**: Complex logic for showing timeline and profile sections.

---

## 5. Key Findings

### Strengths
1. **Centralized extension methods** reduce code duplication
2. **Clear role hierarchy** (Owner > Admin > Member)
3. **Comprehensive category system** covering all member lifecycle stages
4. **Position tracking** for organizational structure

### Weaknesses
1. **Scattered authorization logic** across many files
2. **Inconsistent patterns** (attributes, components, programmatic checks)
3. **Complex conditional logic** mixing roles and categories
4. **No unified policy definitions**
5. **Direct property access** in some places bypassing helpers
6. **Positions not used for authorization** (only display)

### Risks
1. **Hard to maintain** - Changes require updates in multiple places
2. **Easy to miss checks** when adding new features
3. **Testing complexity** - Need to test combinations of roles/categories
4. **Authorization bugs** from forgetting checks
5. **No central audit trail** of permission requirements

---

## 6. Recommended Migration Path

### Phase 1: Claims Infrastructure ✅ (Analysis Complete)
- Document current state ✓
- Map all usages ✓
- Identify patterns ✓

### Phase 2: Claims Model Design
- Define standard claim types
- Create ClaimsConstants class
- Design claim-to-role mapping
- Define authorization policies

### Phase 3: Claims Implementation
- Issue claims at login
- Create ClaimsPrincipal extension methods
- Update authorization configuration

### Phase 4: Migration & Refactoring
- Replace scattered checks with policies
- Move to [Authorize(Policy="...")] where possible
- Add tests for claims-based checks
- Mark legacy methods as obsolete

### Phase 5: Page Permissions Admin
- Create Owner-only admin interface
- Build dynamic authorization system
- Store permission rules in database
- Implement authorization middleware

---

## 7. Appendix: Complete Usage List

### Category Check Locations (53 usages)

**Application Layer (3):**
1. `StatusHelper.cs:148` - IsTuno()
2. `ApplicationUserExtensions.cs` - Method definitions (8)

**Web Pages (44):**
1. `MainLayout.razor` - IsLeitao(3), IsCaloiro(2), IsTuno(1)
2. `Public/Roles.razor` - IsLeitao(2), IsCaloiro(1), IsTuno(1)
3. `Member/Members.razor` - IsLeitao(2), IsFundador(2)
4. `Member/Hierarchy.razor` - IsTunoOrHigher(2)
5. `Member/Profile.razor` - IsLeitao(2), IsCaloiro(1), IsTuno(1), IsFundador(2), IsTunoOrHigher(1)
6. `Member/Slideshows.razor` - IsCaloiro(1)
7. `Member/Requests.razor` - IsCaloiro via IsCaloiroAdmin()(3)
8. `Member/Events.razor` - Categories.Contains(Leitao)(1), IsCaloiro via IsCaloiroAdmin()(3)
9. `Member/Rehearsals.razor` - Categories.Contains(Leitao)(1), IsCaloiro via IsCaloiroAdmin()(3)
10. `Member/Finance.razor` - IsLeitao(1)

**Shared Components (6):**
1. `Profile/ProfileTimeline.razor` - IsLeitao(1), IsCaloiro(2), IsTuno(2)
2. `Profile/UnifiedTimeline.razor` - IsLeitao(1), IsCaloiro(2), IsTuno(2), IsFundador(1)
3. `Modals/ParticipationModal.razor` - IsLeitao parameter (3)

### Role Check Locations (88 usages)

**Application Layer (2):**
1. `UserProfileService.cs:161` - IsInRoleAsync()
2. `MemberBuilder.cs:166` - IsInRoleAsync()

**Web Pages (82):**
1. `MainLayout.razor` - Owner AuthorizeView(1), Admin IsInRole(2), Owner IsInRole(2)
2. `Music/Songs.razor` - Owner AuthorizeView(1), Owner IsInRole(4)
3. `Music/Albums.razor` - Owner AuthorizeView(2), Owner IsInRole(2)
4. `Public/Roles.razor` - Admin AuthorizeView(47), Admin IsInRole(1)
5. `Public/Request.razor` - Admin AuthorizeView(1)
6. `Owner/AuditLog.razor` - Owner Authorize attribute(1)
7. `Owner/UserRoles.razor` - Owner Authorize attribute(1)
8. `Owner/Labels.razor` - Owner Authorize attribute(1)
9. `Member/Members.razor` - Admin AuthorizeView(1), Admin IsInRole(1), Owner IsInRole(1)
10. `Member/Report.razor` - Admin IsInRole(1), Owner IsInRole(1)
11. `Member/LogisticsBoard.razor` - Admin AuthorizeView(3), Admin IsInRole(1)
12. `Member/Logistics.razor` - Admin AuthorizeView(1), Admin IsInRole(1)
13. `Member/Inventory.razor` - Admin/Owner AuthorizeView(2)
14. `Member/EventDiscussion.razor` - Admin IsInRole(1), Owner IsInRole(1)
15. `Member/Shop.razor` - Admin/Owner AuthorizeView(2), Owner AuthorizeView(1)
16. `Member/Slideshows.razor` - Admin/Owner Authorize attribute(1), Admin/Owner IsInRole(1)
17. `Member/Requests.razor` - Owner AuthorizeView(2), Admin IsInRole(1), Owner IsInRole(1)
18. `Member/Events.razor` - Admin AuthorizeView(1), Admin/Owner AuthorizeView(3), Owner AuthorizeView(1), Admin/Owner IsInRole(2)
19. `Member/Rehearsals.razor` - Admin/Owner IsInRole(2)
20. `Member/Finance.razor` - Admin AuthorizeView(2), Admin IsInRole(1), Owner IsInRole(1)

**Shared Components (4):**
1. `UI/LabelEditButton.razor` - Admin AuthorizeView(1)

**Program.cs (1):**
1. Policy definition: "RequireAdministratorRole" requires "Admin" role

---

## 8. Authorization Decision Matrix

| Page/Feature | Leitao | Caloiro | Tuno+ | Admin | Owner |
|--------------|--------|---------|-------|-------|-------|
| View Members | ✓ | ✓ | ✓ | ✓ | ✓ |
| Edit Members | ✗ | ✗ | ✗ | ✓ | ✓ |
| View Events | ✓ | ✓ | ✓ | ✓ | ✓ |
| Create Events | ✗ | ✗ | ✗ | ✓ | ✓ |
| Event Participation | Limited | ✓ | ✓ | ✓ | ✓ |
| View Finance | Limited | ✓ | ✓ | ✓ | ✓ |
| Manage Finance | ✗ | ✗ | ✗ | ✓ | ✓ |
| Manage Roles | ✗ | ✗ | ✗ | ✓ | ✓ |
| View Roles Page | ✗ | ✓ | ✓ | ✓ | ✓ |
| Manage Slideshows | ✗ | ✗ | ✗ | ✓ (not Caloiro) | ✓ |
| Be Mentor | ✗ | ✗ | ✓ | ✓ | ✓ |
| Hold President Position | ✗ | ✗ | ✓ | ✓ | ✓ |
| User Role Management | ✗ | ✗ | ✗ | ✗ | ✓ |
| Audit Log | ✗ | ✗ | ✗ | ✗ | ✓ |
| Label Management | ✗ | ✗ | ✗ | ✗ | ✓ |
| Album/Song Full Control | ✗ | ✗ | ✗ | ✗ | ✓ |

---

## Conclusion

The current authorization model is functional but shows clear signs of organic growth. The migration to a claims-based system will:

1. **Centralize** authorization logic in policies
2. **Simplify** conditional checks in Razor pages
3. **Improve** maintainability and testability
4. **Enable** dynamic page permissions for Owners
5. **Provide** clearer security boundaries

The next phase will focus on designing the claims model and defining authorization policies based on this analysis.
