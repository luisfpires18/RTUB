# API Migration Guide

This document tracks the Repository pattern migration and provides API controller examples for future API development.

## Architecture Overview

```
Mobile App ─┐
Web API ────┼──> Services (Business Logic) ──> Repositories (Data Access) ──> Database
Razor Pages ┘
```

### Benefits
- **Single Source of Truth**: All clients (Mobile, Web API, Razor Pages) use the same business logic
- **Testability**: Repository interfaces can be easily mocked
- **Flexibility**: Easy to swap data sources without touching services
- **Consistency**: Business rules enforced at service layer

---

## Migration Progress: 18 of 33 Entities (55%)

### ✅ Batch 1: Core Domain Entities (5/5)

#### 1. Event Entity
**Repository**: `IEventRepository` → `EventRepository`  
**Service**: `EventService`  
**Test Count**: 15 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    
    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Event>>> GetAllEvents()
    {
        var events = await _eventService.GetAllEventsAsync();
        return Ok(events);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Event>> GetEvent(int id)
    {
        var evt = await _eventService.GetEventByIdAsync(id);
        if (evt == null) return NotFound();
        return Ok(evt);
    }
    
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<Event>>> GetUpcomingEvents([FromQuery] int count = 10)
    {
        var events = await _eventService.GetUpcomingEventsAsync(count);
        return Ok(events);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Event>> CreateEvent([FromBody] CreateEventDto dto)
    {
        var evt = await _eventService.CreateEventAsync(dto.Name, dto.Date, dto.Location, dto.EventType);
        return CreatedAtAction(nameof(GetEvent), new { id = evt.Id }, evt);
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateEventDto dto)
    {
        await _eventService.UpdateEventAsync(id, dto.Name, dto.Date, dto.Location, dto.Description);
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        await _eventService.DeleteEventAsync(id);
        return NoContent();
    }
}
```

---

#### 2. Song Entity
**Repository**: `ISongRepository` → `SongRepository`  
**Service**: `SongService`  
**Test Count**: 24 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/songs")]
public class SongsController : ControllerBase
{
    private readonly ISongService _songService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Song>>> GetAllSongs()
    {
        var songs = await _songService.GetAllSongsAsync();
        return Ok(songs);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Song>> GetSong(int id)
    {
        var song = await _songService.GetSongByIdAsync(id);
        if (song == null) return NotFound();
        return Ok(song);
    }
    
    [HttpGet("album/{albumId}")]
    public async Task<ActionResult<IEnumerable<Song>>> GetSongsByAlbum(int albumId)
    {
        var songs = await _songService.GetSongsByAlbumIdAsync(albumId);
        return Ok(songs);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Musician")]
    public async Task<ActionResult<Song>> CreateSong([FromBody] CreateSongDto dto)
    {
        var song = await _songService.CreateSongAsync(dto.Title, dto.AlbumId, dto.Composer, dto.Arranger);
        return CreatedAtAction(nameof(GetSong), new { id = song.Id }, song);
    }
}
```

---

#### 3. Rehearsal Entity
**Repository**: `IRehearsalRepository` → `RehearsalRepository`  
**Service**: `RehearsalService`  
**Test Count**: 13 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/rehearsals")]
public class RehearsalsController : ControllerBase
{
    private readonly IRehearsalService _rehearsalService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> GetAllRehearsals()
    {
        var rehearsals = await _rehearsalService.GetAllRehearsalsAsync();
        return Ok(rehearsals);
    }
    
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> GetUpcomingRehearsals([FromQuery] int count = 5)
    {
        var rehearsals = await _rehearsalService.GetUpcomingRehearsalsAsync(count);
        return Ok(rehearsals);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Rehearsal>> GetRehearsal(int id)
    {
        var rehearsal = await _rehearsalService.GetRehearsalByIdAsync(id);
        if (rehearsal == null) return NotFound();
        return Ok(rehearsal);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Maestro")]
    public async Task<ActionResult<Rehearsal>> CreateRehearsal([FromBody] CreateRehearsalDto dto)
    {
        var rehearsal = await _rehearsalService.CreateRehearsalAsync(dto.Date, dto.Location, dto.Notes);
        return CreatedAtAction(nameof(GetRehearsal), new { id = rehearsal.Id }, rehearsal);
    }
}
```

---

#### 4. Meeting Entity
**Repository**: `IMeetingRepository` → `MeetingRepository`  
**Service**: `MeetingService` (also uses ApplicationDbContext for Veterano filtering)  
**Test Count**: 15 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/meetings")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Meeting>>> GetAllMeetings([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var meetings = await _meetingService.GetAllMeetingsAsync(page, pageSize);
        return Ok(meetings);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Meeting>> GetMeeting(int id)
    {
        var meeting = await _meetingService.GetMeetingByIdAsync(id);
        if (meeting == null) return NotFound();
        return Ok(meeting);
    }
    
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Meeting>>> SearchMeetings([FromQuery] string searchTerm, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var meetings = await _meetingService.SearchMeetingsAsync(searchTerm, page, pageSize);
        return Ok(meetings);
    }
}
```

---

#### 5. Album Entity
**Repository**: `IAlbumRepository` → `AlbumRepository`  
**Service**: `AlbumService`  
**Test Count**: 23 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/albums")]
public class AlbumsController : ControllerBase
{
    private readonly IAlbumService _albumService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Album>>> GetAllAlbums()
    {
        var albums = await _albumService.GetAllAlbumsAsync();
        return Ok(albums);
    }
    
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<Album>>> GetPublicAlbums()
    {
        var albums = await _albumService.GetPublicAlbumsAsync();
        return Ok(albums);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Album>> GetAlbum(int id)
    {
        var album = await _albumService.GetAlbumByIdAsync(id);
        if (album == null) return NotFound();
        return Ok(album);
    }
    
    [HttpGet("{id}/songs")]
    public async Task<ActionResult<IEnumerable<Song>>> GetAlbumSongs(int id)
    {
        var album = await _albumService.GetAlbumWithSongsAsync(id);
        if (album == null) return NotFound();
        return Ok(album.Songs);
    }
}
```

---

### ✅ Batch 2: Content & Communication (3/3)

#### 6. Post Entity
**Repository**: `IPostRepository` → `PostRepository`  
**Service**: `PostService`  
**Test Count**: 20 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/posts")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetPosts([FromQuery] int? discussionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (discussionId.HasValue)
        {
            var posts = await _postService.GetPostsByDiscussionIdAsync(discussionId.Value, page, pageSize);
            return Ok(posts);
        }
        var allPosts = await _postService.GetAllPostsAsync();
        return Ok(allPosts);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Post>> GetPost(int id)
    {
        var post = await _postService.GetPostByIdAsync(id);
        if (post == null) return NotFound();
        return Ok(post);
    }
    
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Post>> CreatePost([FromBody] CreatePostDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var post = await _postService.CreatePostAsync(dto.DiscussionId, userId, dto.Content);
        return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
    }
    
    [HttpPut("{id}/pin")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> PinPost(int id)
    {
        await _postService.PinPostAsync(id);
        return NoContent();
    }
}
```

---

#### 7. Comment Entity
**Repository**: `ICommentRepository` → `CommentRepository`  
**Service**: `CommentService`  
**Test Count**: 23 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/comments")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;
    
    [HttpGet("post/{postId}")]
    public async Task<ActionResult<IEnumerable<Comment>>> GetCommentsByPost(int postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var comments = await _commentService.GetCommentsByPostIdAsync(postId, page, pageSize);
        return Ok(comments);
    }
    
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Comment>> CreateComment([FromBody] CreateCommentDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var comment = await _commentService.CreateCommentAsync(dto.PostId, userId, dto.Content);
        return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, comment);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Comment>> GetComment(int id)
    {
        var comment = await _commentService.GetCommentByIdAsync(id);
        if (comment == null) return NotFound();
        return Ok(comment);
    }
}
```

---

#### 8. Transaction Entity
**Repository**: `ITransactionRepository` → `TransactionRepository`  
**Service**: `TransactionService`  
**Test Count**: 14 tests passing

**API Controller Example**:
```csharp
[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    
    [HttpGet]
    [Authorize(Roles = "Admin,Treasurer")]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetAllTransactions()
    {
        var transactions = await _transactionService.GetAllTransactionsAsync();
        return Ok(transactions);
    }
    
    [HttpGet("activity/{activityId}")]
    [Authorize(Roles = "Admin,Treasurer")]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactionsByActivity(int activityId)
    {
        var transactions = await _transactionService.GetTransactionsByActivityIdAsync(activityId);
        return Ok(transactions);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Treasurer")]
    public async Task<ActionResult<Transaction>> CreateTransaction([FromBody] CreateTransactionDto dto)
    {
        var transaction = await _transactionService.CreateTransactionAsync(
            dto.ActivityId, dto.Description, dto.Amount, dto.Date, dto.Type);
        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }
    
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Treasurer")]
    public async Task<ActionResult<Transaction>> GetTransaction(int id)
    {
        var transaction = await _transactionService.GetTransactionByIdAsync(id);
        if (transaction == null) return NotFound();
        return Ok(transaction);
    }
}
```

---

### ✅ Batch 3: Inventory & Members (5/5)

#### 9. Discussion Entity
**Repository**: `IDiscussionRepository` → `DiscussionRepository`  
**Service**: `DiscussionService`

**API Controller Example**:
```csharp
[ApiController]
[Route("api/discussions")]
public class DiscussionsController : ControllerBase
{
    private readonly IDiscussionService _discussionService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Discussion>>> GetAllDiscussions()
    {
        var discussions = await _discussionService.GetAllDiscussionsAsync();
        return Ok(discussions);
    }
    
    [HttpGet("event/{eventId}")]
    public async Task<ActionResult<IEnumerable<Discussion>>> GetDiscussionsByEvent(int eventId)
    {
        var discussions = await _discussionService.GetDiscussionsByEventIdAsync(eventId);
        return Ok(discussions);
    }
    
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Discussion>> CreateDiscussion([FromBody] CreateDiscussionDto dto)
    {
        var discussion = await _discussionService.CreateDiscussionAsync(dto.Title, dto.EventId);
        return CreatedAtAction(nameof(GetDiscussion), new { id = discussion.Id }, discussion);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Discussion>> GetDiscussion(int id)
    {
        var discussion = await _discussionService.GetDiscussionByIdAsync(id);
        if (discussion == null) return NotFound();
        return Ok(discussion);
    }
}
```

---

#### 10. Enrollment Entity
**Repository**: `IEnrollmentRepository` → `EnrollmentRepository`  
**Service**: `EnrollmentService`

#### 11. Instrument Entity
**Repository**: `IInstrumentRepository` → `InstrumentRepository`  
**Service**: `InstrumentService`

#### 12. Product Entity
**Repository**: `IProductRepository` → `ProductRepository`  
**Service**: `ProductService`

#### 13. AuditLog Entity
**Repository**: `IAuditLogRepository` → `AuditLogRepository`  
**Service**: `AuditLogService`

**API Controller Example**:
```csharp
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLog>>> GetAuditLogs(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50)
    {
        var logs = await _auditLogService.GetAuditLogsAsync(page, pageSize);
        return Ok(logs);
    }
    
    [HttpGet("critical")]
    public async Task<ActionResult<IEnumerable<AuditLog>>> GetCriticalLogs()
    {
        var logs = await _auditLogService.GetCriticalAuditLogsAsync();
        return Ok(logs);
    }
}
```

---

### ✅ Batch 4: Logistics & Join Tables (5/5)

#### 14. LogisticsBoard Entity
**Repository**: `ILogisticsBoardRepository` → `LogisticsBoardRepository`  
**Service**: `LogisticsBoardService` (also uses ApplicationDbContext for cascade deletes)

**API Controller Example**:
```csharp
[ApiController]
[Route("api/logistics/boards")]
[Authorize]
public class LogisticsBoardsController : ControllerBase
{
    private readonly ILogisticsBoardService _boardService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LogisticsBoard>>> GetAllBoards()
    {
        var boards = await _boardService.GetAllBoardsAsync();
        return Ok(boards);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<LogisticsBoard>> GetBoard(int id)
    {
        var board = await _boardService.GetBoardByIdAsync(id);
        if (board == null) return NotFound();
        return Ok(board);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Logistics")]
    public async Task<ActionResult<LogisticsBoard>> CreateBoard([FromBody] CreateBoardDto dto)
    {
        var board = await _boardService.CreateBoardAsync(dto.Name, dto.Description);
        return CreatedAtAction(nameof(GetBoard), new { id = board.Id }, board);
    }
}
```

---

#### 15. LogisticsList Entity
**Repository**: `ILogisticsListRepository` → `LogisticsListRepository`  
**Service**: `LogisticsListService`

#### 16. LogisticsCard Entity
**Repository**: `ILogisticsCardRepository` → `LogisticsCardRepository`  
**Service**: `LogisticsCardService`

#### 17. EventRepertoire Entity
**Repository**: `IEventRepertoireRepository` → `EventRepertoireRepository`  
**Service**: `EventRepertoireService`

**API Controller Example**:
```csharp
[ApiController]
[Route("api/events/{eventId}/repertoire")]
public class EventRepertoireController : ControllerBase
{
    private readonly IEventRepertoireService _repertoireService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Song>>> GetEventRepertoire(int eventId)
    {
        var songs = await _repertoireService.GetEventRepertoireAsync(eventId);
        return Ok(songs);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Maestro")]
    public async Task<IActionResult> AddSongToEvent(int eventId, [FromBody] AddSongDto dto)
    {
        await _repertoireService.AddSongToEventAsync(eventId, dto.SongId, dto.Order);
        return NoContent();
    }
    
    [HttpDelete("{songId}")]
    [Authorize(Roles = "Admin,Maestro")]
    public async Task<IActionResult> RemoveSongFromEvent(int eventId, int songId)
    {
        await _repertoireService.RemoveSongFromEventAsync(eventId, songId);
        return NoContent();
    }
}
```

---

#### 18. RehearsalAttendance Entity
**Repository**: `IRehearsalAttendanceRepository` → `RehearsalAttendanceRepository`  
**Service**: `RehearsalAttendanceService`

**API Controller Example**:
```csharp
[ApiController]
[Route("api/rehearsals/{rehearsalId}/attendance")]
[Authorize]
public class RehearsalAttendanceController : ControllerBase
{
    private readonly IRehearsalAttendanceService _attendanceService;
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RehearsalAttendance>>> GetAttendance(int rehearsalId)
    {
        var attendance = await _attendanceService.GetAttendanceByRehearsalIdAsync(rehearsalId);
        return Ok(attendance);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,Maestro")]
    public async Task<IActionResult> MarkAttendance(int rehearsalId, [FromBody] MarkAttendanceDto dto)
    {
        await _attendanceService.MarkAttendanceAsync(rehearsalId, dto.UserId, dto.Present);
        return NoContent();
    }
    
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetAttendanceStats(int rehearsalId)
    {
        var stats = await _attendanceService.GetAttendanceStatsAsync(rehearsalId);
        return Ok(stats);
    }
}
```

---

## 📋 Remaining Entities (15 of 33 - 45%)

### Supporting Entities
- [ ] **FiscalYear** - FiscalYearService
- [ ] **RoleAssignment** - RoleAssignmentService
- [ ] **Activity** - ActivityService
- [ ] **Request** - RequestService
- [ ] **Label** - LabelService
- [ ] **MeetingRequest** - MeetingRequestService
- [ ] **ProductReservation** - ProductReservationService
- [ ] **MemberInstrument** - MemberInstrumentService
- [ ] **Trophy** - TrophyService
- [ ] **Report** - ReportService
- [ ] **Slideshow** - SlideshowService
- [ ] **LeaderboardComment** - LeaderboardCommentService
- [ ] **LeaderboardCommentLike** - (if service exists)
- [ ] **SongYouTubeUrl** - (if service exists)
- [ ] **ApplicationUser** - UserProfileService (special handling needed for Identity)

---

## 🎯 Next Steps for API Development

1. **Create API Project**: `RTUB.API` - ASP.NET Core Web API project
2. **Reference Application Layer**: Add project reference to `RTUB.Application`
3. **Configure DI**: Use existing `ServiceCollectionExtensions.AddRepositories()` and service registration
4. **Add Authentication**: Configure JWT bearer token authentication
5. **Create Controllers**: Follow the examples above for each migrated entity
6. **Add DTOs**: Create request/response DTOs in `RTUB.API/Models` folder
7. **Add Swagger**: Configure OpenAPI documentation
8. **Versioning**: Implement API versioning (v1, v2, etc.)
9. **Rate Limiting**: Add rate limiting middleware
10. **CORS Policy**: Configure CORS for web and mobile clients

---

## 📝 Notes

- All repository-migrated services are **ready for API exposure** without modification
- Business logic remains in service layer - controllers are thin
- Authorization attributes should be applied at controller level based on business requirements
- DTOs should be created for complex request/response payloads to avoid over-posting
- Consider using AutoMapper or Mapperly for entity ↔ DTO conversions
- Rate limiting and caching strategies should be implemented at API gateway level

---

**Last Updated**: 2025-11-19  
**Migration Progress**: 18/33 entities (55%)  
**Test Pass Rate**: 99% (204/206 tests passing)
