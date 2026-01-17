# Naipes Feature Documentation

## Feature Overview

### What is the Naipes Page?

The **Naipes** page is an educational content management feature in the RTUB application that provides learning resources for each instrument type. "Naipes" (Spanish/Portuguese for "suits" or "types") organizes multimedia educational materials by musical instrument category, making it easy for band members to access training videos and reference images specific to their instruments.

### Purpose

- **Centralized Learning Hub**: Provides a single location for educational videos and images organized by instrument type
- **Instrument-Specific Content**: Users can filter content by their instrument type (trumpet, trombone, saxophone, etc.)
- **Interactive Learning**: Supports community engagement through comments and discussions on educational materials
- **Usage Tracking**: Monitors content engagement through play/view counts to identify popular resources

### Who Can Use It?

- **All Authenticated Members**: Can view content, watch videos, see images, add comments, and track their viewing history
- **Administrators**: Have additional privileges to create, edit, and delete content (videos and images)

---

## Current Implementation

### Core Features

#### 1. Instrument Type Organization
- **Tabbed Interface**: Users can switch between different instrument types using an intuitive tab selector
- **"All" View**: Option to view content from all instrument types simultaneously
- **Filter by Instrument**: Each instrument type (from the `InstrumentType` enum) has its own dedicated section

#### 2. Content Types
- **Video Support**: Upload and display educational videos with proper MIME type handling
- **Image Support**: Upload and display reference images and diagrams
- **Rich Metadata**: Each content item includes:
  - Title (max 200 characters)
  - Description (optional, max 1000 characters)
  - URL for the media file
  - MIME type
  - Sort order for custom arrangement
  - Creator information (tracked via `CreatedByUserId`)

#### 3. Comments System
- **User Engagement**: Authenticated users can comment on any video or image
- **No Likes Feature**: Unlike other parts of the application, Naipes comments do not support likes/reactions
- **Soft Delete**: Comments use soft deletion (tracked via `DeletedAt` timestamp)
- **Permissions**: 
  - Users can delete their own comments
  - Administrators can delete any comment
- **Character Limit**: Comments are limited to 1000 characters
- **Author Display**: Shows commenter's nickname or username and profile picture

#### 4. Play/View Count Tracking
- **Engagement Metrics**: Tracks every play (video) or view (image) event
- **User Attribution**: Optionally associates views with authenticated users
- **Anonymous Support**: Can track views without user attribution
- **Timestamp**: Records exact time of each play/view event (`PlayedAt`)
- **Statistics**: Provides total play/view count for each content item

#### 5. Admin CRUD Operations
Administrators can:
- **Create**: Add new videos or images with metadata
- **Update**: Edit title, description, and sort order
- **Delete**: Remove content (hard delete, not soft delete)
- **Organize**: Control content ordering via `SortOrder` property

#### 6. Search and Pagination
- Built-in support for filtering and pagination (implementation details in repositories)
- Efficient querying with Entity Framework Core indexes

#### 7. Audit Logging
Comprehensive audit trail for all major operations (see Audit Log Events section below)

---

## Technical Details

### Routes
- **Primary Route**: `/naipes`
- **Authorization**: `[Authorize]` attribute - requires authenticated users

### Backend Architecture

#### Entities

**NaipeContent** (`RTUB.Core.Entities.NaipeContent`)
```csharp
public class NaipeContent : BaseEntity
{
    public InstrumentType InstrumentType { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public string Url { get; set; }
    public string MimeType { get; set; }
    public bool IsVideo { get; set; }
    public decimal SortOrder { get; set; }
    public string CreatedByUserId { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser CreatedByUser { get; set; }
    public virtual ICollection<NaipeComment> Comments { get; set; }
    public virtual ICollection<NaipePlayCount> PlayCounts { get; set; }
}
```

**NaipeComment** (`RTUB.Core.Entities.NaipeComment`)
```csharp
public class NaipeComment : BaseEntity
{
    public int NaipeContentId { get; set; }
    public string AuthorId { get; set; }
    public string Text { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual NaipeContent NaipeContent { get; set; }
    public virtual ApplicationUser Author { get; set; }
}
```

**NaipePlayCount** (`RTUB.Core.Entities.NaipePlayCount`)
```csharp
public class NaipePlayCount : BaseEntity
{
    public int NaipeContentId { get; set; }
    public string? UserId { get; set; }
    public DateTime PlayedAt { get; set; }
    
    // Navigation properties
    public virtual NaipeContent? NaipeContent { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
```

#### Service Interface

**INaipeService** (`RTUB.Application.Interfaces.INaipeService`)

Key methods:
- `GetContentByInstrumentTypeAsync(InstrumentType type)` - Retrieve all content for a specific instrument
- `GetAllContentAsync()` - Retrieve all content across all instruments
- `GetContentByIdAsync(int id)` - Get specific content item
- `CreateContentAsync(...)` - Create new video or image content
- `UpdateContentAsync(int id, ...)` - Update existing content metadata
- `DeleteContentAsync(int id)` - Remove content
- `IncrementPlayCountAsync(int contentId, string? userId)` - Track play/view events
- `GetPlayCountAsync(int contentId)` - Get total play/view count
- `GetCommentsAsync(int contentId, string? currentUserId)` - Retrieve comments with permission checks
- `AddCommentAsync(int contentId, string authorId, string text)` - Add new comment
- `DeleteCommentAsync(int commentId, string userId, bool isAdmin)` - Remove comment (soft delete)

#### Repositories
- **INaipeContentRepository** / **NaipeContentRepository**: Handles content data access
- **INaipeCommentRepository** / **NaipeCommentRepository**: Handles comment data access

#### Frontend Components

**Main Page**: `RTUB.Web/Pages/Activities/Naipes.razor`
- Interactive server-side rendering
- Instrument type selector/tabs
- Admin controls for adding content
- Content display and filtering

**Shared Components**:
- **NaipeCard** (`RTUB.Shared/Components/Cards/NaipeCard.razor`): Displays individual content items (video/image cards)
- **NaipeCommentItem** (`RTUB.Shared/Components/Naipes/NaipeCommentItem.razor`): Renders individual comments with delete functionality

---

## Audit Log Events

All significant operations in the Naipes feature are logged for auditing and compliance purposes. Each audit log entry includes:
- **Event Type**: Descriptive name of the action
- **Username**: The user who performed the action (Nickname or UserName)
- **Content Title**: The title of the affected content
- **Instrument Type**: The instrument category
- **Timestamp**: When the action occurred

### Event Types

#### Content Creation
- **"Video Created"**: Logged when an administrator uploads a new video
  - Example: `Video 'Scale Exercises' created for Trumpet by JohnDoe`
- **"Image Created"**: Logged when an administrator uploads a new image
  - Example: `Image 'Fingering Chart' created for Saxophone by AdminUser`

#### Content Interaction
- **"Video Played"**: Logged when an authenticated user plays a video
  - Example: `Video 'Breathing Techniques' for Flute played by JaneSmith`
- **"Image Viewed"**: Logged when an authenticated user views an image
  - Example: `Image 'Posture Guide' for Trombone viewed by StudentUser`
- Note: Anonymous views are tracked in the play count but not audited

#### Content Modification
- **"Video Modified"**: Logged when an administrator edits video metadata
  - Example: `Video 'Advanced Tonguing' for Clarinet modified by AdminUser`
- **"Image Modified"**: Logged when an administrator edits image metadata
  - Example: `Image 'Embouchure Tips' for Trumpet modified by TeacherUser`

#### Content Deletion
- **"Video Deleted"**: Logged when an administrator removes a video (marked as critical)
  - Example: `Video 'Old Tutorial' for Percussion deleted by AdminUser`
- **"Image Deleted"**: Logged when an administrator removes an image (marked as critical)
  - Example: `Image 'Outdated Chart' for French Horn deleted by AdminUser`

---

## Database Tables

### NaipeContents
Stores educational content items (videos and images).

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key, auto-increment |
| `InstrumentType` | int (enum) | The instrument category this content belongs to |
| `Title` | nvarchar(200) | Content title (required) |
| `Description` | nvarchar(1000) | Optional detailed description |
| `Url` | nvarchar(2048) | URL to the media file (required) |
| `MimeType` | nvarchar(100) | Media MIME type (e.g., video/mp4, image/png) |
| `IsVideo` | bit | True if video, false if image |
| `SortOrder` | decimal | Controls display order within instrument type |
| `CreatedByUserId` | nvarchar(450) | Foreign key to AspNetUsers (creator) |
| `CreatedAt` | datetime2 | Creation timestamp (from BaseEntity) |
| `UpdatedAt` | datetime2 | Last update timestamp (from BaseEntity) |

**Indexes**:
- `IX_NaipeContent_InstrumentType_SortOrder` - Optimizes filtering and sorting
- `IX_NaipeContent_CreatedByUserId` - Optimizes creator lookups

**Relationships**:
- Many-to-One with `AspNetUsers` (CreatedByUser)
- One-to-Many with `NaipeComments`
- One-to-Many with `NaipePlayCounts`

---

### NaipeComments
Stores user comments on Naipe content.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key, auto-increment |
| `NaipeContentId` | int | Foreign key to NaipeContents (required) |
| `AuthorId` | nvarchar(450) | Foreign key to AspNetUsers (required) |
| `Text` | nvarchar(1000) | Comment text (required, max 1000 chars) |
| `DeletedAt` | datetime2 | Soft delete timestamp (nullable) |
| `CreatedAt` | datetime2 | Creation timestamp (from BaseEntity) |
| `UpdatedAt` | datetime2 | Last update timestamp (from BaseEntity) |

**Indexes**:
- `IX_NaipeComment_NaipeContentId_DeletedAt_CreatedAt` - Composite index for efficient comment retrieval
- `IX_NaipeComment_AuthorId` - Optimizes author lookups

**Relationships**:
- Many-to-One with `NaipeContents`
- Many-to-One with `AspNetUsers` (Author)

---

### NaipePlayCounts
Tracks play/view events for analytics.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key, auto-increment |
| `NaipeContentId` | int | Foreign key to NaipeContents (required) |
| `UserId` | nvarchar(450) | Foreign key to AspNetUsers (nullable for anonymous) |
| `PlayedAt` | datetime2 | Timestamp when content was played/viewed |
| `CreatedAt` | datetime2 | Creation timestamp (from BaseEntity) |
| `UpdatedAt` | datetime2 | Last update timestamp (from BaseEntity) |

**Indexes**:
- `IX_NaipePlayCount_NaipeContentId` - Optimizes count aggregation
- `IX_NaipePlayCount_UserId` - Optimizes user history lookups

**Relationships**:
- Many-to-One with `NaipeContents`
- Many-to-One with `AspNetUsers` (optional)

---

## Next Steps / Future Improvements

### Configuration & Management
- **Instrument Type Visibility Control**: Add admin interface to enable/disable specific instrument types from display
- **Content Categories/Tags**: Extend beyond instrument type to include tags like "beginner", "technique", "maintenance", "history"
- **Content Approval Workflow**: Add moderation queue for user-submitted content before publication

### Content Features
- **YouTube Embed Support**: Allow embedding external YouTube videos instead of only uploaded files
- **Playlists/Learning Paths**: Create curated sequences of content for structured learning
- **Difficulty Levels**: Tag content as beginner, intermediate, or advanced
- **Content Contributor Attribution**: Credit multiple contributors and external sources
- **Download Option**: Allow offline viewing of videos and images for practice sessions

### User Engagement
- **Likes/Reactions on Comments**: Add thumbs up/down or emoji reactions to comments
- **Favorite/Bookmark Functionality**: Let users save their favorite content for quick access
- **Progress Tracking**: Mark videos as watched/completed and display progress indicators
- **Recommended Content**: Suggest content based on user's instrument and viewing history
- **User Content Upload**: Allow members to submit their own educational content (with moderation)

### Analytics & Insights
- **Admin Analytics Dashboard**: 
  - Most popular content by instrument type
  - Engagement metrics (views, comments, watch time)
  - User engagement trends over time
  - Content gap analysis (underserved instruments)
- **Personal Analytics**: Show users their own learning progress and history

### Technical Enhancements
- **Mobile PWA Improvements**: Optimize video playback and offline caching for mobile devices
- **CDN Integration**: Use Content Delivery Network for faster video streaming
- **Transcoding Pipeline**: Automatically convert uploaded videos to multiple resolutions
- **Caption/Subtitle Support**: Add accessibility features for video content
- **Search Enhancement**: Full-text search across titles, descriptions, and comments
- **Advanced Filtering**: Filter by upload date, popularity, duration, or content creator

### Quality & Accessibility
- **Content Rating System**: Let users rate content quality and helpfulness
- **Report Inappropriate Content**: Flagging system for community moderation
- **Accessibility Compliance**: Ensure WCAG compliance for all UI components
- **Multi-language Support**: Internationalization for global band communities

---

## Related Documentation

- **Migration**: `20260117003541_AddNaipesFeature.cs` - Database schema creation
- **Frontend Implementation**: See `NAIPES_FRONTEND_IMPLEMENTATION.md` in repository root
- **Entity Framework Configurations**: 
  - `NaipeContentConfiguration.cs`
  - `NaipeCommentConfiguration.cs`
  - `NaipePlayCountConfiguration.cs`

---

**Last Updated**: 2025-01-17  
**Feature Status**: ✅ Production Ready  
**Version**: 1.0
