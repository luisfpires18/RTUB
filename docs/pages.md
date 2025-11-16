# RTUB Pages Reference

This document lists all Razor pages in the RTUB application with their routes, descriptions, main components, and required permissions.

## Table of Contents

- [Public Pages](#public-pages)
- [Identity Pages](#identity-pages)
- [Member Pages](#member-pages)
- [Music Pages](#music-pages)
- [Owner Pages](#owner-pages)

---

## Public Pages

### Home / Index
**Route:** `/`

**Description:** Public landing page with carousel slideshow, about sections, and general information about RTUB.

**Main Components:**
- Carousel (Bootstrap)
- `AboutUsContent`, `HistoryContent`, `HierarchyContent`, `JoinUsContent`, `FitabContent` (Portal components)

**Permissions:** None (public access)

**Notes:** Displays active slideshows from the database. First page visitors see.

---

### Public Roles
**Route:** `/roles`

**Description:** Public view of RTUB's organizational structure showing current positions (Direção, Mesa da Assembleia, Conselho Fiscal, Conselho de Veteranos).

**Main Components:**
- `PositionBadge`
- `AvatarCard`

**Permissions:** None (public access)

**Notes:** Read-only view of hierarchy. Members can see the full interactive version at `/hierarchy`.

---

### Public Request
**Route:** `/request`

**Description:** Public form for external users to submit performance requests to RTUB.

**Main Components:**
- `FormTextField`, `FormTextArea`, `FormSelect`
- `Alert`

**Permissions:** None (public access)

**Notes:** Creates request entries that admins can review at `/requests`.

---

## Identity Pages

### Login
**Route:** `/login`

**Description:** User authentication page with email/password login.

**Main Components:**
- `FormTextField` (email, password)
- `Alert` (error messages)

**Permissions:** None (public access, redirects if already authenticated)

**Notes:** Uses ASP.NET Core Identity. Includes "Forgot Password" and "Resend Confirmation" links.

---

### Forgot Password
**Route:** `/forgot-password`

**Description:** Request password reset email.

**Main Components:**
- `FormTextField` (email)
- `Alert`

**Permissions:** None (public access)

**Notes:** Sends password reset link via email.

---

### Reset Password
**Route:** `/reset-password`

**Description:** Reset password using token from email.

**Main Components:**
- `FormTextField` (password, confirm password)
- `Alert`

**Permissions:** None (requires valid reset token in URL)

**Notes:** Token expires after configured time period.

---

### Confirm Email
**Route:** `/confirm-email`

**Description:** Email confirmation page (users must confirm email to login).

**Main Components:**
- `Alert` (success/error)

**Permissions:** None (requires valid confirmation token in URL)

**Notes:** Automatically confirms email on page load with valid token.

---

## Member Pages

### Events
**Route:** `/events`

**Description:** Main events page showing all performances and activities. Members can enroll/unenroll. Admins can create/edit/delete events.

**Main Components:**
- `EventCard`
- `EnrollmentStatisticsButton` (Admin only)
- `MyEnrollmentsButton` (All authenticated)
- `CrudModalManager` (Create/Edit)
- `ConfirmDialog` (Delete)
- `TableSearchBar`, `TablePagination`
- `FilterDropdown` (by EventType)

**Permissions:** 
- **View:** All authenticated users (implicit via `AuthorizeView`)
- **Create/Edit/Delete:** Admin, Owner roles

**Notes:** Core page for event management. Supports enrollment tracking, image upload, repertoire assignment.

---

### Event Discussion
**Route:** `/events/{eventId:int}/discussion`

**Attribute:** `[Authorize]`

**Description:** Discussion board for a specific event. Members can post messages and reply.

**Main Components:**
- `PostCard`, `CommentItem`
- `PostComposer`, `CommentComposer`
- `ConfirmDialog` (Delete)

**Permissions:** All authenticated users

**Notes:** Real-time discussion via SignalR. Posts support @mentions.

---

### Members
**Route:** `/members`

**Attribute:** `[Authorize]`

**Description:** Member directory with profiles, roles, and categories.

**Main Components:**
- `MemberListItem` (table rows)
- `TableSearchBar`, `TablePagination`, `SortableTableHeader`
- `DetailsModal` (View profile)
- `CrudModalManager` (Edit - Admin only)
- `ConfirmDialog` (Delete - Owner only)
- `CategoryBadge`, `PositionBadge`, `RoleBadge`

**Permissions:**
- **View:** All authenticated users
- **Edit:** Admin, Owner
- **Delete:** Owner only

**Notes:** Shows categories (Tuno, Caloiro, etc.), positions (Magister, etc.), and instruments.

---

### Profile
**Route:** `/profile`

**Attribute:** `[Authorize]`

**Description:** User's own profile page with detailed information and activity timeline.

**Main Components:**
- `ProfileHeader`
- `ProfileSection`, `ProfileField`
- `UnifiedTimeline` (events, rehearsals, milestones)
- `CategoryBadge`, `PositionBadge`
- `InstrumentCircle`

**Permissions:** All authenticated users (own profile only)

**Notes:** Shows personal info, dates (Leitão, Caloiro, Tuno), XP/level, enrollment history.

---

### Hierarchy
**Route:** `/hierarchy`

**Attribute:** `[Authorize]`

**Description:** Full organizational hierarchy with role assignment management.

**Main Components:**
- `PositionBadge`
- `AvatarCard`
- `CrudModalManager` (Assign roles - Admin only)
- `ConfirmDialog` (Remove assignment)

**Permissions:**
- **View:** All authenticated users
- **Assign/Remove:** Admin, Owner

**Notes:** Shows Direção, Mesa da Assembleia, Conselho Fiscal, Conselho de Veteranos. Admins can assign members to positions for fiscal years.

---

### Rehearsals
**Route:** `/rehearsals`

**Attribute:** `[Authorize]`

**Description:** Rehearsal schedule and attendance tracking.

**Main Components:**
- `RehearsalCard`
- `ParticipationModal` (Attendance)
- `CrudModalManager` (Create/Edit - Admin)
- `ConfirmDialog` (Delete - Admin)
- `TableSearchBar`, `TablePagination`

**Permissions:**
- **View:** All authenticated users
- **Create/Edit/Delete:** Admin, Owner
- **Mark attendance:** All authenticated users

**Notes:** Attendance affects XP/level system.

---

### Leaderboard
**Route:** `/leaderboard`

**Attribute:** `[Authorize]`

**Description:** Member ranking based on XP (earned from events, rehearsals, etc.).

**Main Components:**
- `LeaderboardCard`
- `RankCard` (Top 3 display)

**Permissions:** All authenticated users

**Notes:** Gamification system. XP earned from event/rehearsal attendance. Levels and rank names displayed.

---

### Finance
**Route:** `/finance`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Financial management with transactions, budgets, and reports by fiscal year.

**Main Components:**
- Transaction list table
- `CrudModalManager` (Create/Edit transactions)
- `ConfirmDialog` (Delete)
- `ReportCard`
- `TableSearchBar`, `TablePagination`
- `FilterDropdown` (Income/Expense, Fiscal Year)

**Permissions:** Admin, Owner only

**Notes:** Tracks income/expenses, generates fiscal year summaries, links to detailed reports.

---

### Finance Report
**Route:** `/finance/report/{ReportId:int}`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Detailed financial report view with PDF export.

**Main Components:**
- Report summary sections
- `InfoSection`
- PDF download button

**Permissions:** Admin, Owner only

**Notes:** Uses QuestPDF for PDF generation.

---

### Inventory
**Route:** `/inventory`

**Attribute:** `[Authorize]`

**Description:** Manage instruments, products, and trophies.

**Main Components:**
- Tabbed interface (Instruments, Products, Trophies)
- `InstrumentCircle`
- `InstrumentCounter`
- `CrudModalManager` (Create/Edit)
- `ConfirmDialog` (Delete)
- `TableSearchBar`, `TablePagination`
- `FilterDropdown` (Condition, Type)

**Permissions:**
- **View:** All authenticated users
- **Create/Edit/Delete:** Admin, Owner (implicit via button visibility)

**Notes:** Tracks instrument condition, product stock, trophy inventory.

---

### Shop
**Route:** `/shop`

**Attribute:** `[Authorize]`

**Description:** Product reservation system for members.

**Main Components:**
- Product grid
- `ReservationCard`
- `CrudModalManager` (Reserve product)
- `ConfirmDialog` (Cancel reservation)

**Permissions:** All authenticated users

**Notes:** Members can reserve products. Admins manage inventory via `/inventory`.

---

### Requests
**Route:** `/requests`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Review and manage performance requests from public form.

**Main Components:**
- `RequestCard`
- `DetailsModal` (View details)
- Approve/Reject buttons
- `TableSearchBar`, `TablePagination`
- `FilterDropdown` (Status)

**Permissions:** Admin, Owner only

**Notes:** Requests come from `/request` public page.

---

### Logistics
**Route:** `/logistics`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Logistics board management (e.g., task boards for event planning).

**Main Components:**
- `BoardCard`
- `CrudModalManager` (Create/Edit boards)
- `ConfirmDialog` (Delete)

**Permissions:** Admin, Owner only

**Notes:** Creates boards that contain cards (tasks). Navigate to `/logistics/{BoardId}` to view cards.

---

### Logistics Board
**Route:** `/logistics/{BoardId:int}`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** View and manage cards within a logistics board.

**Main Components:**
- Card list by status (To Do, In Progress, Done)
- `CrudModalManager` (Create/Edit cards)
- `ConfirmDialog` (Delete)
- Drag-and-drop support (via JS)

**Permissions:** Admin, Owner only

**Notes:** Cards have status, assignments, due dates.

---

### Slideshows
**Route:** `/images`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Manage homepage carousel slideshows.

**Main Components:**
- `SlideshowCard`
- `CrudModalManager` (Create/Edit)
- `ConfirmDialog` (Delete)
- `ImageUploadManager`

**Permissions:** Admin, Owner only

**Notes:** Controls homepage carousel at `/`. Set image, title, description, interval, and active status.

---

### Documentation
**Route:** `/documentation`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Document management system (upload/download files).

**Main Components:**
- `DocumentCard`, `FolderCard`
- `CrudModalManager` (Upload)
- `ConfirmDialog` (Delete)
- Folder navigation

**Permissions:** Admin, Owner only

**Notes:** Integrated with cloud storage (IDrive/S3-compatible).

---

## Music Pages

### Albums (Music)
**Route:** `/music`

**Description:** Music album library. No explicit `[Authorize]` attribute, but admin features hidden via `AuthorizeView`.

**Main Components:**
- `AlbumCard`
- `CrudModalManager` (Create/Edit - Admin)
- `ConfirmDialog` (Delete - Admin)
- `ImageUploadManager`
- `TableSearchBar`, `TablePagination`

**Permissions:**
- **View:** All authenticated users (implicit)
- **Create/Edit/Delete:** Admin, Owner

**Notes:** Albums contain songs. Click album to view songs at `/music/songs/{AlbumId}`.

---

### Songs
**Route:** `/music/songs/{AlbumId:int}`

**Description:** Song list for a specific album.

**Main Components:**
- `SongCard`, `SongCardSkeleton` (loading)
- `CrudModalManager` (Add song - Admin)
- `ConfirmDialog` (Delete - Admin)
- `TableSearchBar`

**Permissions:**
- **View:** All authenticated users (implicit)
- **Create/Edit/Delete:** Admin, Owner

**Notes:** Songs can be assigned to event repertoires.

---

## Owner Pages

Owner pages are restricted to users with the **Owner** role. This is the highest permission level for system administration and audit.

### Audit Log
**Route:** `/owner/tracing`

**Attribute:** `[Authorize(Roles = "Owner")]`

**Description:** System-wide audit log tracking all user actions.

**Main Components:**
- Audit log table
- `FilterDropdown` (User, Action Type, Entity Type)
- `TableSearchBar`, `TablePagination`, `SortableTableHeader`
- Export to JSON button
- Truncate All button with `ConfirmDialog`

**Permissions:** Owner only

**Notes:** Logs all CRUD operations with user, timestamp, entity, action, and details. Critical for accountability and troubleshooting.

---

### User Roles
**Route:** `/owner/user-roles`

**Attribute:** `[Authorize(Roles = "Owner")]`

**Description:** Manage ASP.NET Identity roles for users (assign/remove Admin, Member, Owner, Visitor).

**Main Components:**
- User table with role badges
- `RoleBadge`
- `CrudModalManager` (Assign/remove roles)
- `ConfirmDialog`
- `TableSearchBar`, `TablePagination`

**Permissions:** Owner only

**Notes:** Role changes are immediately effective. Owner should exercise caution.

---

### Labels
**Route:** `/owner/labels`

**Attribute:** `[Authorize(Roles = "Owner")]`

**Description:** Manage system-wide labels/tags for categorization.

**Main Components:**
- `LabelCard`
- `CrudModalManager` (Create/Edit)
- `ConfirmDialog` (Delete)
- `LabelEditButton`

**Permissions:** Owner only

**Notes:** Labels can be used across various entities for filtering and organization.

---

## Permission Summary

| Page Category | Public | Member | Admin | Owner |
|--------------|--------|--------|-------|-------|
| Public Pages | ✓ | ✓ | ✓ | ✓ |
| Identity Pages | ✓ | ✓ | ✓ | ✓ |
| Member Pages (View) | - | ✓ | ✓ | ✓ |
| Member Pages (Edit) | - | - | ✓ | ✓ |
| Music Pages (View) | - | ✓ | ✓ | ✓ |
| Music Pages (Edit) | - | - | ✓ | ✓ |
| Owner Pages | - | - | - | ✓ |

**Notes:**
- **Public** = Anonymous users
- **Member** = Authenticated users with Member role
- **Admin** = Users with Admin or Owner role
- **Owner** = Users with Owner role only

Some pages don't have explicit `[Authorize]` attributes but use `<AuthorizeView Roles="...">` to show/hide features within the page.

---

## Authorization Patterns

### Page-Level Authorization
```razor
@attribute [Authorize]
@attribute [Authorize(Roles = "Admin,Owner")]
@attribute [Authorize(Roles = "Owner")]
```

### Component-Level Authorization
```razor
<AuthorizeView>
    <Authorized>
        <!-- Content for authenticated users -->
    </Authorized>
    <NotAuthorized>
        <!-- Content for anonymous users -->
    </NotAuthorized>
</AuthorizeView>

<AuthorizeView Roles="Admin">
    <Authorized>
        <!-- Admin-only buttons -->
    </Authorized>
</AuthorizeView>
```

### Code-Behind Authorization
```csharp
var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
var user = authState.User;
bool isAdmin = user.IsInRole("Admin") || user.IsInRole("Owner");
```

---

## See Also

- [Components Guide](components.md) - Components used in these pages
- [Authentication & Business Rules](auth-and-rules.md) - Roles, categories, and permissions explained
- [Contributing Guide](contributing.md) - How to add new pages
