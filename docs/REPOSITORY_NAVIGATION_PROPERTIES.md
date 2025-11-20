# Repository Navigation Property Loading Guide

## Overview

This document provides guidance on which repository methods include navigation properties (eager loading) and which don't. This helps developers understand data availability and avoid null reference exceptions.

## Navigation Property Loading Strategy

### ✅ **Explicit Methods with Navigations**
Repository methods that specifically load related entities should use descriptive names that indicate what's included.

**Recommended Naming Pattern:**
- `GetWithXAsync()` - Loads entity with specific navigation property X
- `GetAllWithXAsync()` - Loads all entities with specific navigation property X
- `GetXWithYAndZAsync()` - Loads X with multiple related entities Y and Z

### ⚠️ **Base Methods Without Navigations**
By default, base repository methods (`GetByIdAsync`, `GetAllAsync`, `FindAsync`) **DO NOT** include navigation properties unless explicitly overridden.

**Rationale:**
- Performance: Avoid unnecessary joins when navigations aren't needed
- Explicit Intent: Developers must consciously choose to load related data
- Consistency: Predictable behavior across all repositories

---

## Repository-by-Repository Guide

### ✅ Repositories WITH Navigation Property Overrides

#### **EnrollmentRepository**
```csharp
// ✅ Includes navigation properties
GetAllAsync()          → Includes Event entity
GetByEventIdAsync()    → Includes User entity  
GetByUserIdAsync()     → Includes Event entity

// ⚠️ Does NOT include navigations
GetByIdAsync()         → Base implementation (no navigations)
FindAsync()            → Base implementation (no navigations)
```

**Recommendation**: Add explicit methods:
```csharp
Task<IEnumerable<Enrollment>> GetAllWithoutEventAsync();  // For performance when Event not needed
Task<Enrollment?> GetByIdWithEventAndUserAsync(int id);   // When both needed
```

---

#### **RehearsalAttendanceRepository**
```csharp
// ✅ Includes navigation properties
GetAttendancesByRehearsalIdAsync() → Includes Rehearsal entity

// ⚠️ Does NOT include navigations  
GetByIdAsync()         → Base implementation (no navigations)
GetAllAsync()          → Base implementation (no navigations)
```

**Recommendation**: Add explicit methods:
```csharp
Task<IEnumerable<RehearsalAttendance>> GetAllWithRehearsalAsync();
Task<RehearsalAttendance?> GetByIdWithRehearsalAsync(int id);
Task<IEnumerable<RehearsalAttendance>> GetByUserIdWithRehearsalAsync(string userId);
```

---

#### **LogisticsCardRepository**
```csharp
// ✅ Includes navigation properties
GetCardsByListIdAsync() → Includes AssignedToUser + Event
GetCardsByUserIdAsync() → Includes List → Board → Event, plus Event
SearchCardsAsync()      → Includes AssignedToUser + List → Board

// ⚠️ Does NOT include navigations
GetByIdAsync()          → Base implementation (no navigations)
```

**Recommendation**: Method already well-named. Consider adding:
```csharp
Task<LogisticsCard?> GetByIdWithNavigationsAsync(int id);  // For full entity graph
```

---

#### **LogisticsBoardRepository**
```csharp
// ✅ Includes navigation properties
GetBoardWithListsAndCardsAsync() → Full entity graph (Event, Lists, Cards, Users)
GetBoardByEventIdAsync()         → Includes Event only
SearchBoardsAsync()              → Includes Event only

// ⚠️ Does NOT include navigations
GetByIdAsync()                   → Base implementation (no navigations)
GetAllAsync()                    → Base implementation (no navigations)
```

**Recommendation**: Well-designed! Methods clearly indicate what's loaded.

---

#### **LogisticsListRepository**
```csharp
// ✅ Includes navigation properties
GetListsByBoardIdAsync() → No navigations (just lists)
GetListWithCardsAsync()  → Includes Cards → (AssignedToUser + Event)

// ⚠️ Does NOT include navigations
GetByIdAsync()           → Base implementation (no navigations)
GetAllAsync()            → Base implementation (no navigations)
```

**Recommendation**: Well-designed! Consider renaming for clarity:
```csharp
Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync();  // Current - minimal
Task<IEnumerable<LogisticsList>> GetListsByBoardIdWithCardsAsync(int boardId);  // New - with cards
```

---

### ⚠️ Repositories WITHOUT Navigation Property Overrides

These repositories use base implementation for all methods (no eager loading):

- **EventRepository** - Override `GetAllAsync` to include cancellation/type filtering if needed
- **RehearsalRepository** - Override `GetAllAsync` to include attendance stats if needed
- **MeetingRepository** - Override `GetAllAsync` to include organizer if needed
- **TransactionRepository** - No navigations needed
- **ProductRepository** - No navigations needed
- **InstrumentRepository** - No navigations needed
- **SongRepository** - No navigations needed
- **AlbumRepository** - Override to include songs/photos if needed
- **PostRepository** - Override to include author/comments if needed
- **CommentRepository** - Override to include author if needed
- **DiscussionRepository** - Override to include posts if needed
- **AuditLogRepository** - No navigations needed (performance-critical)

---

## Best Practices

### 1. **Explicit Method Names**
✅ **DO:**
```csharp
Task<Event?> GetEventWithEnrollmentsAsync(int id);
Task<Rehearsal?> GetRehearsalWithAttendancesAsync(int id);
Task<Meeting?> GetMeetingWithOrganizerAsync(int id);
```

❌ **DON'T:**
```csharp
Task<Event?> GetEventAsync(int id);  // Ambiguous - does it include navigations?
```

### 2. **Performance Considerations**
- **Use `.AsNoTracking()`** for read-only queries
- **Avoid loading collections in loops** - use batch methods
- **Project to DTOs** when only specific fields needed

✅ **DO:**
```csharp
// Batch load for multiple entities
var users = await _enrollmentRepository.Query()
    .Where(e => eventIds.Contains(e.EventId))
    .Include(e => e.User)
    .Select(e => e.User)
    .Distinct()
    .ToListAsync();
```

❌ **DON'T:**
```csharp
// N+1 query problem
foreach (var enrollment in enrollments)
{
    var user = await _userRepository.GetByIdAsync(enrollment.UserId);  // Separate query each iteration!
}
```

### 3. **Query() Method Usage**
When services need custom queries with specific includes, use the `Query()` method:

```csharp
// In service layer
public async Task<IEnumerable<Event>> GetUpcomingEventsWithEnrollmentCountAsync()
{
    return await _eventRepository.Query()
        .Include(e => e.Enrollments.Where(en => en.WillAttend))
        .Where(e => e.Date >= DateTime.Today && !e.IsCancelled)
        .OrderBy(e => e.Date)
        .ToListAsync();
}
```

### 4. **Documentation in Code**
Add XML comments to repository methods indicating what's included:

```csharp
/// <summary>
/// Gets enrollment by ID
/// Note: Does NOT include Event or User navigation properties
/// Use GetByIdWithEventAndUserAsync() if navigations needed
/// </summary>
Task<Enrollment?> GetByIdAsync(int id);

/// <summary>
/// Gets all enrollments with Event navigation property included
/// Note: Does NOT include User - use Query() if User needed
/// </summary>
Task<IEnumerable<Enrollment>> GetAllAsync();
```

---

## Migration Guide

### For Existing Code

**Step 1**: Identify repositories that override `GetAllAsync` or `GetByIdAsync`
```bash
# Find all overridden methods
grep -r "override.*GetAllAsync\|override.*GetByIdAsync" src/RTUB.Application/Repositories/
```

**Step 2**: Check which navigations are loaded
```csharp
// Look for .Include() calls
.Include(e => e.Event)
.ThenInclude(...)
```

**Step 3**: Create explicit methods for clarity
```csharp
// Instead of overriding GetAllAsync, create:
Task<IEnumerable<Enrollment>> GetAllWithEventAsync();
Task<IEnumerable<Enrollment>> GetAllWithEventAndUserAsync();

// Keep base GetAllAsync for performance when navigations not needed
```

**Step 4**: Update service layer calls
```csharp
// Before
var enrollments = await _enrollmentRepository.GetAllAsync();  // Includes Event (hidden behavior)

// After  
var enrollments = await _enrollmentRepository.GetAllWithEventAsync();  // Explicit!
```

---

## Summary Checklist

When creating or modifying repository methods:

- [ ] Use explicit method names that indicate included navigations
- [ ] Document what IS and ISN'T included in XML comments
- [ ] Consider performance - only load what's needed
- [ ] Use `.AsNoTracking()` for read-only queries
- [ ] Avoid N+1 queries - use batch loading
- [ ] Provide both with-navigation and without-navigation variants
- [ ] Update this document when adding new navigation loading patterns

---

## Quick Reference Table

| Repository | Method | Includes Navigations? | What's Included |
|------------|--------|----------------------|-----------------|
| EnrollmentRepository | `GetAllAsync()` | ✅ Yes | Event |
| EnrollmentRepository | `GetByIdAsync()` | ❌ No | None |
| EnrollmentRepository | `GetByEventIdAsync()` | ✅ Yes | User |
| RehearsalAttendanceRepository | `GetAttendancesByRehearsalIdAsync()` | ✅ Yes | Rehearsal |
| RehearsalAttendanceRepository | `GetByIdAsync()` | ❌ No | None |
| LogisticsBoardRepository | `GetBoardWithListsAndCardsAsync()` | ✅ Yes | Full graph |
| LogisticsBoardRepository | `GetByIdAsync()` | ❌ No | None |
| LogisticsCardRepository | `GetCardsByListIdAsync()` | ✅ Yes | User + Event |
| LogisticsListRepository | `GetListWithCardsAsync()` | ✅ Yes | Cards + Users |
| EventRepository | `GetAllAsync()` | ❌ No | None |
| MeetingRepository | `GetAllAsync()` | ❌ No | None |
| *(All other repos)* | `GetByIdAsync()` / `GetAllAsync()` | ❌ No | None |

---

**Last Updated**: 2025-11-20  
**Maintainer**: Architecture Team
