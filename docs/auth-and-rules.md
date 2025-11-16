# Authentication & Business Rules

This document explains RTUB's authentication system, role model, and important business rules.

## Table of Contents

- [Authentication Overview](#authentication-overview)
- [Roles vs Categories vs Positions](#roles-vs-categories-vs-positions)
- [Member Categories](#member-categories)
- [Positions (Cargos)](#positions-cargos)
- [ASP.NET Identity Roles](#aspnet-identity-roles)
- [Permission Model](#permission-model)
- [Important Business Rules](#important-business-rules)

---

## Authentication Overview

RTUB uses **ASP.NET Core Identity** for authentication and authorization. All users must have an account and confirm their email before accessing member features.

### User Registration Flow
1. User registers with email, password, and basic info
2. System sends confirmation email
3. User clicks link in email to confirm
4. User can now log in

### Password Requirements
- Minimum 6 characters (configurable in `Program.cs`)
- At least one non-alphanumeric character
- At least one digit
- At least one uppercase letter

---

## Roles vs Categories vs Positions

RTUB has **three distinct concepts** for member classification. Understanding the difference is critical:

### **Roles** (ASP.NET Identity)
- **Purpose:** Application-level permissions (what features you can access)
- **Examples:** Owner, Admin, Member, Visitor
- **Storage:** ASP.NET Identity tables (`AspNetRoles`, `AspNetUserRoles`)
- **Usage:** `[Authorize(Roles = "Admin")]`, `user.IsInRole("Admin")`
- **Assignment:** Owner manages via `/owner/user-roles`

### **Categories** (Domain Model)
- **Purpose:** Member classification within the tuna (Tuno, Caloiro, Leitão, etc.)
- **Examples:** Tuno, Veterano, Tunossauro, Caloiro, Leitão, Fundador
- **Storage:** `ApplicationUser.CategoriesJson` (JSON array in user table)
- **Usage:** Badge display, timeline milestones, business logic for progression
- **Assignment:** Admin manages via member edit modal

### **Positions** (Corporate Structure / Cargos)
- **Purpose:** Organizational roles within RTUB's corporate structure
- **Examples:** Magister, Secretário, Presidente da Mesa da Assembleia
- **Storage:** `RoleAssignment` table (links user + position + fiscal year)
- **Usage:** Hierarchy page, governance, historical record
- **Assignment:** Admin manages via `/hierarchy`

### Summary Table

| Concept | Determines | Stored In | Managed By | Changes |
|---------|------------|-----------|------------|---------|
| **Role** | App permissions | Identity tables | Owner | Rarely |
| **Category** | Tuna status | User.CategoriesJson | Admin | As member progresses |
| **Position** | Corporate role | RoleAssignment table | Admin | Per fiscal year |

---

## Member Categories

Member categories represent the progression and status within the tuna. Defined in `RTUB.Core.Enums.MemberCategory`.

### Category Types

#### **Leitão**
- **Status:** Not yet an official member
- **Description:** Prospective member in trial period
- **Duration:** Varies (typically a few months)
- **Privileges:** Limited access, observing/learning
- **Progression:** → Caloiro (after decision by group)

#### **Caloiro**
- **Status:** New member (first year)
- **Description:** Newly accepted member learning tuna traditions
- **Duration:** Up to 2 years as Tuno
- **Privileges:** Full membership rights
- **Progression:** → Tuno (automatic based on date)

#### **Tuno**
- **Status:** Full member
- **Description:** Standard active member of the tuna
- **Duration:** Until 2+ years as Tuno
- **Privileges:** Full participation, voting rights
- **Progression:** → Veterano (automatic after 2 years)

#### **Veterano**
- **Status:** Senior member (2+ years as Tuno)
- **Description:** Experienced member with seniority
- **Duration:** Until 4+ additional years as Veterano
- **Privileges:** Leadership roles, mentorship, special recognition
- **Progression:** → Tunossauro (automatic after additional time)

#### **Tunossauro**
- **Status:** Very senior member (6+ total years as Tuno)
- **Description:** Highly experienced, long-serving member
- **Duration:** Permanent
- **Privileges:** Highest seniority, advisory role
- **Progression:** None

#### **Tuno Honorário**
- **Status:** Honorary member
- **Description:** Special recognition for exceptional cases (former members, contributors)
- **Duration:** Permanent
- **Privileges:** Honorary status, no active participation required
- **Progression:** None

#### **Fundador**
- **Status:** Founding member (1991)
- **Description:** One of the original founders of RTUB
- **Duration:** Permanent
- **Privileges:** Historical significance, special recognition
- **Progression:** None

### Category Progression Logic

The `ApplicationUser` entity has a calculated property `CurrentRole` that determines category based on dates:

```csharp
public string CurrentRole
{
    get
    {
        if (YearTuno == null) return "N/A";
        
        var now = DateTime.Now;
        var tunoStartYear = YearTuno.Value;
        var tunoStartMonth = MonthTuno ?? 1;
        
        var tunoStart = new DateTime(tunoStartYear, tunoStartMonth, 1);
        var monthsAsTuno = ((now.Year - tunoStart.Year) * 12) + (now.Month - tunoStart.Month);
        var yearsAsTuno = monthsAsTuno / 12.0;
        
        if (yearsAsTuno >= 6) return "TUNOSSAURO";
        if (yearsAsTuno >= 2) return "VETERANO";
        return "TUNO";
    }
}
```

**Important:** Category progression is time-based but must be manually updated by admins in the system. The `CurrentRole` property is for display only.

---

## Positions (Cargos)

Positions represent RTUB's formal organizational structure (similar to a corporation or association). Defined in `RTUB.Core.Enums.Position`.

### Organizational Bodies

#### **Direção** (Board of Directors)
- **Magister** - President, overall leadership
- **Vice-Magister** - Vice President
- **Secretário** - Secretary, administrative tasks
- **Primeiro Tesoureiro** - First Treasurer, financial management
- **Segundo Tesoureiro** - Second Treasurer, financial support

#### **Mesa da Assembleia Geral** (General Assembly Board)
- **Presidente da Mesa da Assembleia** - Assembly President, chairs meetings
- **Primeiro Secretário da Mesa da Assembleia** - First Secretary
- **Segundo Secretário da Mesa da Assembleia** - Second Secretary

#### **Conselho Fiscal** (Fiscal Council)
- **Presidente do Conselho Fiscal** - Fiscal Council President, financial oversight
- **Primeiro Relator do Conselho Fiscal** - First Reporter
- **Segundo Relator do Conselho Fiscal** - Second Reporter

#### **Conselho de Veteranos** (Council of Veterans)
- **Presidente do Conselho de Veteranos** - Veterans Council President, advisory role

#### **Outros Cargos** (Other Positions)
- **Ensaiador** - Rehearsal director, musical leadership

### Position Management

Positions are assigned for specific **fiscal years** (e.g., 2023-2024) via the `RoleAssignment` entity:

```csharp
public class RoleAssignment
{
    public string UserId { get; set; }
    public Position Position { get; set; }
    public int StartYear { get; set; }  // e.g., 2023
    public int EndYear { get; set; }    // e.g., 2024
    public string? Notes { get; set; }
}
```

**Key Points:**
- A member can hold multiple positions simultaneously (e.g., Magister + Presidente do Conselho de Veteranos)
- Positions are historical - old assignments are preserved for record-keeping
- The `/hierarchy` page shows current fiscal year positions prominently
- Admins can view and manage historical assignments

---

## ASP.NET Identity Roles

System-level roles control application features and permissions.

### Role Hierarchy

#### **Owner** (Highest)
- **Purpose:** System owner/superuser
- **Permissions:** 
  - All Admin permissions
  - Audit log access (`/owner/tracing`)
  - User role management (`/owner/user-roles`)
  - Label management (`/owner/labels`)
  - Delete users
  - Truncate audit logs
- **Count:** Should be very limited (1-2 people)
- **Assignment:** Only Owner can assign Owner role

#### **Admin**
- **Purpose:** Administrative staff
- **Permissions:**
  - Create/edit/delete events, albums, songs
  - Create/edit/delete rehearsals
  - Manage financial transactions and reports
  - Manage inventory (instruments, products, trophies)
  - Manage logistics boards
  - Manage slideshows
  - Manage documents
  - View/approve/reject requests
  - Edit member profiles
  - Assign positions to members
  - View all member data
- **Count:** Several trusted members
- **Assignment:** Owner assigns via `/owner/user-roles`

#### **Member**
- **Purpose:** Regular authenticated users (standard role for members)
- **Permissions:**
  - View all member areas
  - Enroll in events and rehearsals
  - Participate in discussions
  - Reserve shop products
  - View own profile
  - View leaderboard
  - View inventory (read-only)
  - View financial summaries (if implemented)
- **Count:** All active members
- **Assignment:** Automatically assigned on registration (configurable)

#### **Visitor**
- **Purpose:** Limited access for guests/observers
- **Permissions:**
  - View public pages
  - View member directory (if allowed)
  - Limited interaction
- **Count:** Rarely used
- **Assignment:** Owner assigns manually

### Default Role Assignment

New users are typically assigned **Member** role on registration (configured in `Program.cs` via seeding or registration logic). Owner must manually promote users to Admin or Owner.

---

## Permission Model

### How Authorization Works

1. **Page-level:** `@attribute [Authorize(Roles = "Admin,Owner")]` blocks entire page
2. **Component-level:** `<AuthorizeView Roles="Admin">` shows/hides UI elements
3. **Code-level:** `user.IsInRole("Admin")` checks in C# code

### Permission Patterns by Feature

| Feature | View | Create | Edit | Delete |
|---------|------|--------|------|--------|
| Events | All | Admin+ | Admin+ | Admin+ |
| Rehearsals | All | Admin+ | Admin+ | Admin+ |
| Members | All | - | Admin+ | Owner |
| Albums/Songs | All | Admin+ | Admin+ | Admin+ |
| Finance | Admin+ | Admin+ | Admin+ | Admin+ |
| Inventory | All | Admin+ | Admin+ | Admin+ |
| Logistics | Admin+ | Admin+ | Admin+ | Admin+ |
| Slideshows | Admin+ | Admin+ | Admin+ | Admin+ |
| Documents | Admin+ | Admin+ | Admin+ | Admin+ |
| Requests | Admin+ | Public | Admin+ | Admin+ |
| Audit Log | Owner | - | - | Owner |
| User Roles | Owner | - | Owner | Owner |
| Labels | Owner | Owner | Owner | Owner |

**Legend:** "Admin+" means Admin or Owner, "All" means any authenticated user.

---

## Important Business Rules

### 1. Email Confirmation Required
- Users cannot log in until they confirm their email
- Confirmation links expire after a configurable period
- Resend confirmation is available on the login page

### 2. Category Progression is Manual
- Despite time-based logic in `CurrentRole`, admins must manually update `Categories` in the database
- This allows for exceptions and special cases
- Timeline events can trigger reminders for admin review

### 3. Positions are Fiscal-Year-Based
- Positions span a fiscal year (e.g., September 2023 - August 2024)
- Historical assignments are preserved
- A member cannot have the same position twice in the same fiscal year (enforced in business logic)

### 4. XP and Levels
- Members earn XP by attending events and rehearsals
- XP calculation: Base XP + bonuses
- Level progression is automatic based on XP thresholds
- Leaderboard is recalculated on-demand (not real-time)

### 5. Enrollment and Attendance
- Members enroll in events in advance
- Attendance is marked separately (enrolled ≠ attended)
- XP is only awarded for actual attendance, not just enrollment
- Admins can mark attendance for members who forgot to enroll

### 6. Financial Transactions
- All transactions are categorized as Income or Expense
- Transactions belong to a fiscal year
- Reports are generated per fiscal year
- Transactions cannot be edited after a certain period (to maintain audit integrity - enforced via business logic or admin discretion)

### 7. Instrument Tracking
- Instruments have a condition status (Excellent, Good, Fair, Poor, Broken)
- Instruments can be assigned to members (tracked in Instrument entity)
- A member's `MainInstrument` is for display; actual instrument assignments are separate

### 8. Repertoire Assignment
- Songs belong to albums
- Songs can be assigned to event repertoires (many-to-many)
- Repertoire is managed via the RepertoireModal on event pages

### 9. Audit Logging
- All CRUD operations are logged to the `AuditLog` table
- Logs include: user, action type, entity type, entity ID, timestamp, details
- Owner can export and truncate logs
- Logs are for accountability and troubleshooting, not user-facing features

### 10. Image Storage
- Images are stored in cloud storage (IDrive/S3-compatible)
- Local file system is used as fallback in development
- Image URLs are stored in the database
- Images should be optimized/resized before upload (handled by `ImageUploadManager`)

### 11. Discussion @Mentions
- Users can mention others in posts using `@username`
- Mentions trigger notifications (if notification system is implemented)
- Mention parsing is handled by `MentionService`

### 12. Request Workflow
- Public users submit requests via `/request`
- Requests enter "Pending" status
- Admins review and approve/reject at `/requests`
- Approved requests may create corresponding events (manual process for now)

### 13. Slideshow Display
- Only active slideshows appear on the homepage
- Slideshows display in order (admin-defined or by ID)
- Interval is per-slide (milliseconds)

### 14. Role Assignment Rules
- Only Owner can assign/remove Owner role
- Owner can assign/remove Admin role
- Only Owner can delete users
- Removing all roles from a user leaves them with no access (except public pages)

### 15. Database Migrations
- Migrations are applied automatically on app startup
- Seeding happens after migrations (admin user, initial data)
- Migrations should be tested in development before deploying

---

## Business Logic Examples

### Check if User is Admin or Owner
```csharp
var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
var user = authState.User;
bool isAdminOrOwner = user.IsInRole("Admin") || user.IsInRole("Owner");
```

### Get Member's Current Category Badge
```csharp
// In Razor component
@foreach (var category in user.Categories)
{
    <CategoryBadge Category="category" />
}
```

### Get Member's Position for Current Fiscal Year
```csharp
var currentYear = DateTime.Now.Year;
var currentMonth = DateTime.Now.Month;
var fiscalYearStart = currentMonth >= 9 ? currentYear : currentYear - 1;
var fiscalYearEnd = fiscalYearStart + 1;

var position = await context.RoleAssignments
    .Where(ra => ra.UserId == userId 
        && ra.StartYear == fiscalYearStart 
        && ra.EndYear == fiscalYearEnd)
    .Select(ra => ra.Position)
    .FirstOrDefaultAsync();
```

### Calculate XP for Event Attendance
```csharp
const int BaseEventXp = 50;
const int BonusXp = 10; // Example bonus for special events
int earnedXp = BaseEventXp + (isSpecialEvent ? BonusXp : 0);

user.ExperiencePoints += earnedXp;
user.Level = CalculateLevel(user.ExperiencePoints);
```

---

## Common Pitfalls

1. **Confusing Roles with Positions**
   - Don't use Positions for authorization checks
   - Positions are for organizational display, not permissions

2. **Assuming Category Progression is Automatic**
   - The `CurrentRole` property is calculated but doesn't update `Categories`
   - Admins must manually update categories

3. **Forgetting to Check Both Admin and Owner**
   - Always check `user.IsInRole("Admin") || user.IsInRole("Owner")`
   - Or use `[Authorize(Roles = "Admin,Owner")]`

4. **Editing Position Assignments Without Checking Fiscal Year**
   - Always ensure StartYear < EndYear
   - Validate fiscal year boundaries

5. **Not Logging Important Actions**
   - All CRUD operations should be logged for audit purposes
   - Use `IAuditLogService` consistently

---

## See Also

- [Pages Reference](pages.md) - Page-specific permissions
- [Components Guide](components.md) - Badge and auth-related components
- [Contributing Guide](contributing.md) - How to add auth logic to new features
