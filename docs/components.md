# RTUB.Shared Components

This document describes all reusable components in the **RTUB.Shared** project. These components are designed to be used across the application for consistency and maintainability.

## Table of Contents

- [Badges](#badges)
- [Cards](#cards)
- [Common Components](#common-components)
- [Discussion Components](#discussion-components)
- [Form Components](#form-components)
- [Modals](#modals)
- [Profile Components](#profile-components)
- [Ranking Components](#ranking-components)
- [Table Components](#table-components)
- [UI Components](#ui-components)
- [Upload Components](#upload-components)

---

## Badges

### CategoryBadge
**Purpose:** Display a member category with colored badge.

**Parameters:**
- `Category` (MemberCategory) - The category to display (Tuno, Veterano, Caloiro, etc.)
- `AdditionalClasses` (string) - Optional CSS classes

**Usage:** Member lists, profiles, hierarchy pages.

---

### PositionBadge
**Purpose:** Display a position/role (cargo) with colored badge.

**Parameters:**
- `Position` (Position) - The position to display (Magister, Secretario, etc.)
- `AdditionalClasses` (string) - Optional CSS classes

**Usage:** Member lists, hierarchy pages, role assignment displays.

---

### RoleBadge
**Purpose:** Display ASP.NET Identity role (Admin, Owner, Member, Visitor).

**Parameters:**
- `Role` (string) - The role name
- `AdditionalClasses` (string) - Optional CSS classes

**Usage:** User role management pages, audit logs.

---

### StatusBadge
**Purpose:** Generic status badge for various entity states.

**Parameters:**
- `Status` (string) - Status text
- `StatusClass` (string) - CSS class for styling
- `AdditionalClasses` (string) - Optional CSS classes

**Usage:** Event status, request status, inventory status displays.

---

### LoginStatusBadge
**Purpose:** Display last login status for members.

**Parameters:**
- `LastLoginDate` (DateTime?) - Last login timestamp
- `AdditionalClasses` (string) - Optional CSS classes

**Usage:** Member management pages, admin dashboards.

---

## Cards

### AlbumCard
**Purpose:** Display a music album with thumbnail and metadata.

**Parameters:**
- `AlbumId` (int) - Album identifier
- `Title` (string) - Album title
- `ThumbnailUrl` (string) - Cover image URL
- `SongCount` (int) - Number of songs
- `IsAdmin` (bool) - Show admin actions
- `OnView`, `OnEdit`, `OnDelete` (EventCallback) - Action callbacks

**Usage:** Music/Albums page (`/music`).

---

### EventCard
**Purpose:** Display an event/performance with details and enrollment.

**Parameters:**
- `EventId` (int)
- `Title`, `Location`, `Date` (string)
- `EventType` (EventType)
- `ThumbnailUrl` (string)
- `IsAdmin` (bool)
- `OnView`, `OnEdit`, `OnDelete` (EventCallback)

**Usage:** Events page (`/events`).

---

### MemberListItem
**Purpose:** Table row component for member listings.

**Parameters:**
- `User` (ApplicationUser) - Member data
- `CurrentFiscalYearPosition` (Position?) - Current position
- `IsAdmin`, `IsOwner` (bool) - Permission flags
- `OnViewDetails`, `OnEdit`, `OnDelete` (EventCallback)

**Usage:** Members page (`/members`).

---

### RehearsalCard
**Purpose:** Display rehearsal information with attendance tracking.

**Parameters:**
- `RehearsalId` (int)
- `Title`, `Location`, `Date` (string)
- `AttendanceCount` (int)
- `IsAdmin` (bool)
- `OnView`, `OnEdit`, `OnDelete` (EventCallback)

**Usage:** Rehearsals page (`/rehearsals`).

---

### LeaderboardCard
**Purpose:** Display member ranking with XP, level, and statistics.

**Parameters:**
- `Position` (int) - Rank position
- `AvatarUrl`, `Nickname`, `FullName` (string)
- `Instrument` (string)
- `Level` (int)
- `RankName` (string) - Rank title
- `Xp` (int) - Experience points
- `RehearsalsAttended`, `EventsAttended` (int)
- `OnClick` (EventCallback)

**Usage:** Leaderboard page (`/leaderboard`).

---

### RequestCard
**Purpose:** Display member request with status and approval actions.

**Parameters:**
- `RequestId` (int)
- `Title`, `Description`, `Status` (string)
- `RequestDate` (DateTime)
- `IsAdmin` (bool)
- `OnApprove`, `OnReject`, `OnView` (EventCallback)

**Usage:** Requests page (`/requests`).

---

### SongCard / SongCardSkeleton
**Purpose:** Display song in album with metadata. Skeleton for loading state.

**Parameters (SongCard):**
- `SongId` (int)
- `Title`, `Artist`, `Duration` (string)
- `IsAdmin` (bool)
- `OnPlay`, `OnEdit`, `OnDelete` (EventCallback)

**Usage:** Songs page (`/music/songs/{AlbumId}`).

---

### DocumentCard, FolderCard
**Purpose:** File/folder display for document management.

**Usage:** Documentation page (`/documentation`).

---

### EnrollmentCard
**Purpose:** Display event enrollment with member details.

**Usage:** Event detail modals, enrollment statistics.

---

### InstrumentCircle
**Purpose:** Display instrument icon in a circular badge.

**Usage:** Member profiles, inventory.

---

### ReservationCard
**Purpose:** Display shop product reservation.

**Usage:** Shop page (`/shop`).

---

### BoardCard, LabelCard, ReportCard
**Purpose:** Display logistics boards, labels, and financial reports.

**Usage:** Logistics pages, finance management, owner/labels page.

---

### AvatarCard
**Purpose:** Simple user avatar display with fallback.

**Usage:** Various pages for user identification.

---

## Common Components

### EmptyState
**Purpose:** Display friendly "no data" message with icon.

**Parameters:**
- `Title` (string) - Message title
- `Icon` (string) - Bootstrap icon class
- `Description` (string) - Optional description

**Usage:** All list pages when no items exist.

---

### ErrorDisplay
**Purpose:** Display error messages in a consistent format.

**Parameters:**
- `ErrorMessage` (string)
- `ShowDetails` (bool) - Show technical details

**Usage:** Error handling throughout the application.

---

## Discussion Components

### PostCard
**Purpose:** Display a discussion post with author and content.

**Parameters:**
- `PostId` (int)
- `Author`, `Content`, `PostDate` (string)
- `CanEdit`, `CanDelete` (bool)
- `OnEdit`, `OnDelete`, `OnReply` (EventCallback)

**Usage:** Event discussion page (`/events/{eventId}/discussion`).

---

### CommentItem
**Purpose:** Display a comment on a post.

**Parameters:**
- `CommentId` (int)
- `Author`, `Content`, `CommentDate` (string)
- `CanEdit`, `CanDelete` (bool)
- `OnEdit`, `OnDelete` (EventCallback)

**Usage:** Event discussion page.

---

### PostComposer
**Purpose:** Form for creating a new post.

**Parameters:**
- `OnSubmit` (EventCallback<string>)

**Usage:** Event discussion page.

---

### CommentComposer
**Purpose:** Form for replying to a post.

**Parameters:**
- `PostId` (int)
- `OnSubmit` (EventCallback<string>)

**Usage:** Event discussion page.

---

## Form Components

All form components follow consistent patterns with validation, labels, and error display.

### FormTextField
**Purpose:** Text input field with validation.

**Parameters:**
- `Label` (string) - Field label
- `Value` (string) - Current value
- `ValueChanged` (EventCallback<string>)
- `Placeholder` (string)
- `Required` (bool)
- `Type` (string) - Input type (text, email, password, etc.)
- `MaxLength` (int?)
- `ErrorMessage` (string)

**Usage:** All forms requiring text input.

---

### FormTextArea
**Purpose:** Multi-line text input with validation.

**Parameters:**
- Similar to FormTextField
- `Rows` (int) - Number of visible rows

**Usage:** Forms needing long text (descriptions, notes).

---

### FormSelect
**Purpose:** Dropdown select with validation.

**Parameters:**
- `Label` (string)
- `Value` (string)
- `ValueChanged` (EventCallback<string>)
- `Options` (Dictionary<string, string>) - Key-value pairs
- `Required` (bool)
- `ErrorMessage` (string)

**Usage:** Forms with predefined options (roles, categories, instruments).

---

### FormSection
**Purpose:** Section wrapper for grouping form fields.

**Parameters:**
- `Title` (string)
- `ChildContent` (RenderFragment)

**Usage:** Forms with multiple logical sections.

---

### MonthYearPicker
**Purpose:** Date picker for month/year selection.

**Parameters:**
- `SelectedMonth`, `SelectedYear` (int?)
- `OnMonthYearChanged` (EventCallback<(int?, int?)>)
- `Label` (string)
- `Required` (bool)

**Usage:** Member profile dates (year/month Caloiro, Tuno, etc.).

---

## Modals

### Modal
**Purpose:** Base modal component for dialogs.

**Parameters:**
- `Show` (bool)
- `Title` (string)
- `Size` (string) - "sm", "lg", "xl"
- `BodyContent`, `FooterContent` (RenderFragment)
- `OnClose` (EventCallback)

**Usage:** Base for all modal dialogs.

---

### ConfirmDialog
**Purpose:** Confirmation dialog for destructive actions.

**Parameters:**
- `Show` (bool)
- `Title`, `Message`, `WarningMessage` (string)
- `ConfirmText`, `CancelText` (string)
- `ConfirmButtonClass` (string) - "btn-danger", "btn-primary", etc.
- `OnConfirm`, `OnCancel` (EventCallback)
- `ShowCloseButton` (bool)
- `BodyContent` (RenderFragment) - Custom content

**Usage:** Delete confirmations, important decisions. Used extensively across admin pages.

---

### CrudModalManager
**Purpose:** Generic modal for Create/Update operations.

**Parameters:**
- `Show` (bool)
- `Title` (string)
- `EntityContent` (RenderFragment)
- `OnSave`, `OnCancel` (EventCallback)
- `IsSaving` (bool)

**Usage:** Create/Edit modals for entities (events, members, albums, etc.).

---

### DetailsModal
**Purpose:** Display read-only entity details.

**Parameters:**
- `Show` (bool)
- `Title` (string)
- `DetailsContent` (RenderFragment)
- `OnClose` (EventCallback)

**Usage:** View details for entities without editing.

---

### InfoSection
**Purpose:** Labeled information display inside modals.

**Parameters:**
- `Label`, `Value` (string)

**Usage:** Detail modals to show field labels and values.

---

### ParticipationModal
**Purpose:** Manage event participation/enrollment.

**Parameters:**
- `EventId` (int)
- `Show` (bool)
- `OnClose` (EventCallback)

**Usage:** Event pages for enrollment management.

---

### RepertoireModal
**Purpose:** Manage event repertoire (songs).

**Parameters:**
- `EventId` (int)
- `Show` (bool)
- `OnClose` (EventCallback)

**Usage:** Event pages for selecting songs.

---

## Profile Components

### ProfileHeader
**Purpose:** Display user profile header with avatar and basic info.

**Parameters:**
- `User` (ApplicationUser)
- `IsOwnProfile` (bool)
- `OnEditProfile` (EventCallback)

**Usage:** Profile page (`/profile`).

---

### ProfileField
**Purpose:** Display a single profile field with label.

**Parameters:**
- `Label`, `Value` (string)
- `Icon` (string) - Bootstrap icon

**Usage:** Profile page to show user data.

---

### ProfileSection
**Purpose:** Section wrapper for profile fields.

**Parameters:**
- `Title` (string)
- `ChildContent` (RenderFragment)

**Usage:** Profile page to group related fields.

---

### ProfileTimeline, UnifiedTimeline
**Purpose:** Display member timeline (events, rehearsals, milestones).

**Parameters:**
- `UserId` (string)
- `Items` (List<TimelineItem>)

**Usage:** Profile page to show member activity history.

---

## Ranking Components

### LeaderboardCard
**Purpose:** (See [Cards](#leaderboardcard) section)

---

### RankCard
**Purpose:** Display rank badge/icon for leaderboard tiers.

**Parameters:**
- `Rank` (int)
- `RankName` (string)

**Usage:** Leaderboard page to highlight top ranks (gold, silver, bronze).

---

## Table Components

### TableSearchBar
**Purpose:** Search input with debouncing for filtering table data.

**Parameters:**
- `SearchTerm` (string)
- `Placeholder` (string)
- `OnSearchChanged` (EventCallback<string>)
- `DebounceDelay` (int) - Milliseconds (default 300)

**Usage:** All tables with search functionality (members, events, songs, etc.).

---

### TablePagination
**Purpose:** Pagination controls for tables.

**Parameters:**
- `CurrentPage` (int)
- `TotalPages` (int)
- `PageSize` (int)
- `TotalItems` (int)
- `OnPageChanged` (EventCallback<int>)
- `OnPageSizeChanged` (EventCallback<int>)

**Usage:** All paginated tables.

---

### SortableTableHeader
**Purpose:** Table header with sort indicators.

**Parameters:**
- `ColumnName` (string)
- `CurrentSortColumn` (string)
- `SortDirection` (string) - "asc" or "desc"
- `OnSort` (EventCallback<string>)

**Usage:** Tables with sortable columns.

---

## UI Components

### Alert
**Purpose:** Display alert messages (success, error, warning, info).

**Parameters:**
- `Message` (string)
- `Type` (string) - "success", "danger", "warning", "info"
- `Dismissible` (bool)
- `OnDismiss` (EventCallback)

**Usage:** Form submission feedback, notifications.

---

### LoadingSpinner
**Purpose:** Loading indicator.

**Parameters:**
- `Message` (string) - Optional message

**Usage:** Pages loading data, async operations.

---

### FilterDropdown
**Purpose:** Dropdown filter for tables.

**Parameters:**
- `Label` (string)
- `Options` (Dictionary<string, string>)
- `SelectedValue` (string)
- `OnFilterChanged` (EventCallback<string>)

**Usage:** Tables with filterable columns.

---

### EnrollmentStatisticsButton
**Purpose:** Button to view event enrollment statistics.

**Parameters:**
- `IsAdmin` (bool)

**Usage:** Events page (Admin only).

---

### MyEnrollmentsButton
**Purpose:** Button to view user's own enrollments.

**Usage:** Events page (All authenticated users).

---

### InstrumentCounter
**Purpose:** Display count of instruments by type.

**Parameters:**
- `InstrumentType` (InstrumentType)
- `Count` (int)

**Usage:** Inventory page.

---

### LabelEditButton
**Purpose:** Quick edit button for label management.

**Parameters:**
- `LabelId` (int)
- `OnEdit` (EventCallback)

**Usage:** Labels page (Owner only).

---

## Upload Components

### ImageUploadManager
**Purpose:** Full-featured image upload with validation and preview.

**Parameters:**
- `AllowedExtensions` (List<string>)
- `MaxFileSizeInMB` (int)
- `OnImageSelected` (EventCallback<IBrowserFile>)
- `PreviewUrl` (string)

**Usage:** Album uploads, profile pictures, event images.

---

### ImageCropper
**Purpose:** Image cropping interface (integrates with JavaScript library).

**Parameters:**
- `ImageUrl` (string)
- `AspectRatio` (double)
- `OnCropped` (EventCallback<string>)

**Usage:** Profile picture editing, album cover cropping.

---

## Slideshow Components

### SlideshowCard
**Purpose:** Display slideshow configuration for homepage carousel.

**Parameters:**
- `SlideshowId` (int)
- `Title`, `Description` (string)
- `ImageUrl` (string)
- `IntervalMs` (int)
- `IsActive` (bool)
- `OnEdit`, `OnDelete`, `OnToggleActive` (EventCallback)

**Usage:** Slideshows page (`/images`) - Admin only.

---

## Component Usage Guidelines

1. **Always use these shared components** instead of duplicating markup
2. **Pass parameters explicitly** - avoid relying on cascading parameters except for authentication
3. **Handle events properly** - use EventCallback for parent-child communication
4. **Keep components page-scoped** - use page-scoped CSS files (`.razor.css`) for component-specific styles
5. **Follow naming conventions** - Components use PascalCase, parameters use PascalCase, events use On[Action] pattern
6. **Document new components** - Add to this file when creating new shared components

---

## See Also

- [Pages Reference](pages.md) - Where components are used
- [Contributing Guide](contributing.md) - How to add new components
