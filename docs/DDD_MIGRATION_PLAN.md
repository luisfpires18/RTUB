# Domain Driven Design (DDD) Migration Plan

## Overview

This document outlines the step-by-step plan to migrate the RTUB application from Clean Architecture to Domain Driven Design (DDD) architecture while maintaining all existing functionality.

**Current State**: Clean Architecture with Repository Pattern
**Target State**: Domain Driven Design with Bounded Contexts, Aggregates, Value Objects, Domain Events, and Rich Domain Models

## Target Project Structure

After migration, the project structure will be organized by bounded contexts with clear separation of concerns:

```
RTUB/
├── src/
│   ├── RTUB.Domain/                    # Domain Layer (Core Business Logic)
│   │   ├── SharedKernel/               # Shared domain concepts
│   │   │   ├── ValueObjects/          # Shared value objects (Email, Money, etc.)
│   │   │   ├── Entities/              # BaseEntity, etc.
│   │   │   ├── Events/                # Base domain event classes
│   │   │   └── Exceptions/            # Domain exceptions
│   │   │
│   │   ├── EventManagement/           # Event Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Event.cs          # Aggregate root
│   │   │   ├── Entities/
│   │   │   │   ├── Enrollment.cs
│   │   │   │   ├── EventRepertoire.cs
│   │   │   │   └── EventVideo.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── EventLocation.cs
│   │   │   │   └── EventDateRange.cs
│   │   │   ├── Events/
│   │   │   │   ├── EventCreated.cs
│   │   │   │   ├── EventCancelled.cs
│   │   │   │   └── EnrollmentCreated.cs
│   │   │   └── Services/
│   │   │       └── EventSchedulingService.cs
│   │   │
│   │   ├── RepertoireManagement/      # Songs/Albums Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Album.cs           # Aggregate root
│   │   │   ├── Entities/
│   │   │   │   ├── Song.cs
│   │   │   │   ├── SongVideo.cs
│   │   │   │   └── SongYouTubeUrl.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── SongMetadata.cs
│   │   │   │   └── Duration.cs
│   │   │   ├── Events/
│   │   │   │   ├── SongAdded.cs
│   │   │   │   └── AlbumCreated.cs
│   │   │   └── Services/
│   │   │       └── RepertoirePlanningService.cs
│   │   │
│   │   ├── MemberManagement/          # Members Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Member.cs         # Aggregate root (refactored from ApplicationUser)
│   │   │   ├── Entities/
│   │   │   │   ├── MemberStatus.cs
│   │   │   │   ├── MemberInstrument.cs
│   │   │   │   └── RoleAssignment.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── Email.cs
│   │   │   │   └── MemberProfile.cs
│   │   │   ├── Events/
│   │   │   │   ├── MemberRegistered.cs
│   │   │   │   └── MemberStatusChanged.cs
│   │   │   └── Services/
│   │   │       └── PermissionDomainService.cs
│   │   │
│   │   ├── RehearsalManagement/       # Rehearsals Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Rehearsal.cs
│   │   │   ├── Entities/
│   │   │   │   └── RehearsalAttendance.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── RehearsalSchedule.cs
│   │   │   └── Events/
│   │   │       ├── RehearsalScheduled.cs
│   │   │       └── AttendanceRecorded.cs
│   │   │
│   │   ├── MeetingManagement/        # Meetings Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Meeting.cs
│   │   │   ├── Entities/
│   │   │   │   ├── MeetingParticipation.cs
│   │   │   │   ├── MeetingAta.cs
│   │   │   │   └── MeetingAtaAgendaPoint.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── MeetingSchedule.cs
│   │   │   └── Events/
│   │   │       ├── MeetingCreated.cs
│   │   │       └── AtaGenerated.cs
│   │   │
│   │   ├── Discussion/               # Discussions Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Discussion.cs
│   │   │   ├── Entities/
│   │   │   │   ├── Post.cs
│   │   │   │   └── Comment.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── PostContent.cs
│   │   │   │   └── CommentContent.cs
│   │   │   └── Events/
│   │   │       ├── PostCreated.cs
│   │   │       └── CommentAdded.cs
│   │   │
│   │   ├── Messaging/                # Messaging Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── Conversation.cs
│   │   │   ├── Entities/
│   │   │   │   ├── Message.cs
│   │   │   │   └── ConversationUserSettings.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── MessageContent.cs
│   │   │   └── Events/
│   │   │       ├── MessageSent.cs
│   │   │       └── ConversationCreated.cs
│   │   │
│   │   ├── Inventory/                # Inventory Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   ├── Product.cs
│   │   │   │   └── Instrument.cs
│   │   │   ├── Entities/
│   │   │   │   └── ProductReservation.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── ProductDetails.cs
│   │   │   │   └── ReservationPeriod.cs
│   │   │   └── Events/
│   │   │       ├── ProductReserved.cs
│   │   │       └── ReservationCancelled.cs
│   │   │
│   │   ├── Logistics/               # Logistics Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── LogisticsBoard.cs
│   │   │   ├── Entities/
│   │   │   │   ├── LogisticsList.cs
│   │   │   │   ├── LogisticsCard.cs
│   │   │   │   └── LogisticsCardAssignment.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── CardStatus.cs
│   │   │   │   └── Priority.cs
│   │   │   └── Events/
│   │   │       ├── CardCreated.cs
│   │   │       └── CardAssigned.cs
│   │   │
│   │   ├── Financial/               # Financial Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   ├── FiscalYear.cs
│   │   │   │   └── Transaction.cs
│   │   │   ├── Entities/
│   │   │   │   ├── MemberDebt.cs
│   │   │   │   └── Activity.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── Money.cs
│   │   │   │   └── TransactionDetails.cs
│   │   │   ├── Events/
│   │   │   │   ├── TransactionRecorded.cs
│   │   │   │   └── DebtCreated.cs
│   │   │   └── Services/
│   │   │       └── FinancialCalculationService.cs
│   │   │
│   │   ├── Gaming/                  # Gaming Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   ├── Game.cs
│   │   │   │   └── Bet.cs
│   │   │   ├── Entities/
│   │   │   │   ├── GameScore.cs
│   │   │   │   ├── BetOption.cs
│   │   │   │   ├── UserBet.cs
│   │   │   │   └── BetComment.cs
│   │   │   ├── ValueObjects/
│   │   │   │   ├── Score.cs
│   │   │   │   └── BetDetails.cs
│   │   │   └── Events/
│   │   │       ├── BetCreated.cs
│   │   │       └── BetPlaced.cs
│   │   │
│   │   ├── Content/                # Content Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   ├── GalleryMedia.cs
│   │   │   │   └── Slideshow.cs
│   │   │   ├── Entities/
│   │   │   │   └── GalleryMediaPersonTag.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── MediaMetadata.cs
│   │   │   └── Events/
│   │   │       ├── MediaUploaded.cs
│   │   │       └── TagAdded.cs
│   │   │
│   │   ├── Education/              # Education Bounded Context
│   │   │   ├── Aggregates/
│   │   │   │   └── NaipeContent.cs
│   │   │   ├── Entities/
│   │   │   │   ├── NaipeComment.cs
│   │   │   │   └── NaipePlayCount.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── ContentMetadata.cs
│   │   │   └── Events/
│   │   │       ├── ContentPublished.cs
│   │   │       └── CommentAdded.cs
│   │   │
│   │   └── QnA/                    # Q&A Bounded Context
│   │       ├── Aggregates/
│   │       │   └── Question.cs
│   │       ├── Entities/
│   │       │   └── QuestionReply.cs
│   │       ├── ValueObjects/
│   │       │   └── QuestionContent.cs
│   │       └── Events/
│   │           ├── QuestionAsked.cs
│   │           └── ReplyAdded.cs
│   │
│   ├── RTUB.Application/            # Application Layer (Use Cases & Orchestration)
│   │   ├── Shared/                  # Shared application concerns
│   │   │   ├── Mappings/            # AutoMapper profiles or manual mappers
│   │   │   ├── Extensions/          # Application-level extensions
│   │   │   └── Helpers/             # Application helpers
│   │   │
│   │   ├── EventManagement/         # Event Bounded Context Application Layer
│   │   │   ├── Services/
│   │   │   │   └── EventApplicationService.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── EventDto.cs
│   │   │   │   ├── EnrollmentDto.cs
│   │   │   │   └── EventRepertoireDto.cs
│   │   │   └── Mappings/
│   │   │       └── EventMappingProfile.cs
│   │   │
│   │   ├── RepertoireManagement/     # Repertoire Application Layer
│   │   │   ├── Services/
│   │   │   │   └── RepertoireApplicationService.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── AlbumDto.cs
│   │   │   │   └── SongDto.cs
│   │   │   └── Mappings/
│   │   │       └── RepertoireMappingProfile.cs
│   │   │
│   │   ├── MemberManagement/        # Member Application Layer
│   │   │   ├── Services/
│   │   │   │   └── MemberApplicationService.cs
│   │   │   ├── DTOs/
│   │   │   │   └── MemberDto.cs
│   │   │   └── Mappings/
│   │   │       └── MemberMappingProfile.cs
│   │   │
│   │   ├── RehearsalManagement/     # Rehearsal Application Layer
│   │   │   ├── Services/
│   │   │   │   └── RehearsalApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── MeetingManagement/       # Meeting Application Layer
│   │   │   ├── Services/
│   │   │   │   └── MeetingApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Discussion/              # Discussion Application Layer
│   │   │   ├── Services/
│   │   │   │   └── DiscussionApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Messaging/               # Messaging Application Layer
│   │   │   ├── Services/
│   │   │   │   └── MessagingApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Inventory/               # Inventory Application Layer
│   │   │   ├── Services/
│   │   │   │   └── InventoryApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Logistics/              # Logistics Application Layer
│   │   │   ├── Services/
│   │   │   │   └── LogisticsApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Financial/               # Financial Application Layer
│   │   │   ├── Services/
│   │   │   │   └── FinancialApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Gaming/                  # Gaming Application Layer
│   │   │   ├── Services/
│   │   │   │   └── GamingApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Content/                 # Content Application Layer
│   │   │   ├── Services/
│   │   │   │   └── ContentApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Education/               # Education Application Layer
│   │   │   ├── Services/
│   │   │   │   └── EducationApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── QnA/                     # Q&A Application Layer
│   │   │   ├── Services/
│   │   │   │   └── QnAApplicationService.cs
│   │   │   └── DTOs/
│   │   │
│   │   └── Infrastructure/          # Application Infrastructure (Event Handlers)
│   │       ├── EventHandlers/       # Domain event handlers
│   │       │   ├── EventCreatedHandler.cs
│   │       │   ├── EnrollmentCreatedHandler.cs
│   │       │   └── ...
│   │       └── BackgroundServices/ # Background jobs
│   │
│   ├── RTUB.Infrastructure/         # Infrastructure Layer (Persistence & External)
│   │   ├── Persistence/             # Data Access
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/     # EF Core configurations
│   │   │   │   ├── EventManagement/
│   │   │   │   │   └── EventConfiguration.cs
│   │   │   │   ├── RepertoireManagement/
│   │   │   │   │   └── AlbumConfiguration.cs
│   │   │   │   └── ...
│   │   │   ├── Repositories/       # Repository implementations
│   │   │   │   ├── EventManagement/
│   │   │   │   │   └── EventRepository.cs
│   │   │   │   ├── RepertoireManagement/
│   │   │   │   │   └── AlbumRepository.cs
│   │   │   │   └── ...
│   │   │   └── UnitOfWork/
│   │   │       └── UnitOfWork.cs
│   │   │
│   │   ├── Storage/                 # File/Media Storage
│   │   │   ├── ImageStorageService.cs
│   │   │   ├── VideoStorageService.cs
│   │   │   └── DocumentStorageService.cs
│   │   │
│   │   ├── Messaging/               # External messaging services
│   │   │   ├── EmailService.cs
│   │   │   └── PushNotificationService.cs
│   │   │
│   │   ├── Events/                 # Domain event publishing
│   │   │   ├── DomainEventDispatcher.cs
│   │   │   └── DomainEventPublisher.cs
│   │   │
│   │   └── Migrations/             # EF Core migrations
│   │
│   ├── RTUB.Shared/                 # Shared UI Components (Unchanged)
│   │   ├── Components/             # Blazor components
│   │   ├── Base/                   # Base components
│   │   └── Enums/                  # UI enums
│   │
│   └── RTUB.Web/                    # Presentation Layer (Blazor Web App)
│       ├── Components/             # Page-specific components
│       ├── Pages/                  # Blazor pages/routes
│       │   ├── Events/            # Event pages
│       │   ├── Repertoire/        # Repertoire pages
│       │   ├── Members/           # Member pages
│       │   └── ...
│       ├── Controllers/           # API controllers (if needed)
│       ├── Hubs/                  # SignalR hubs
│       ├── Services/              # Presentation services
│       ├── Program.cs             # Startup/DI configuration
│       └── wwwroot/               # Static files
│
└── tests/                          # Test Projects
    ├── RTUB.Domain.Tests/          # Domain unit tests
    │   ├── EventManagement/
    │   ├── RepertoireManagement/
    │   └── ...
    ├── RTUB.Application.Tests/      # Application unit tests
    │   ├── EventManagement/
    │   └── ...
    └── RTUB.Infrastructure.Tests/  # Integration tests
        └── Persistence/
```

### Key Structural Changes

**Before (Current)**:
- `RTUB.Core` - All entities together
- `RTUB.Application` - Services, Repositories, DTOs all mixed
- Flat organization by technical concern

**After (DDD)**:
- `RTUB.Domain` - Organized by bounded contexts, each with Aggregates/Entities/ValueObjects/Events
- `RTUB.Application` - Organized by bounded contexts, each with Services/DTOs/Mappings
- `RTUB.Infrastructure` - Organized by technical concern (Persistence, Storage, Messaging)
- Clear separation: Domain → Application → Infrastructure → Presentation

### Dependency Flow

```
RTUB.Web (Presentation)
    ↓ depends on
RTUB.Application (Use Cases)
    ↓ depends on
RTUB.Domain (Business Logic)
    ↑ implemented by
RTUB.Infrastructure (Persistence, External Services)
```

**Important**: 
- Domain has **no dependencies** (pure business logic)
- Application depends only on Domain
- Infrastructure implements Domain interfaces
- Web depends on Application (not directly on Domain or Infrastructure)

## Important Architectural Decisions

### CQRS: Not Required for This Application

**Decision**: Use **Application Services** pattern instead of CQRS (Command Query Responsibility Segregation).

**Rationale**:
- RTUB is a standard CRUD application for a university music group
- Uses SQLite (single database, no read/write separation needs)
- Moderate complexity - doesn't require separate read/write models
- CQRS adds significant complexity without clear benefits
- Application Services pattern is simpler and sufficient

**When CQRS Would Be Beneficial**:
- Very high read/write separation needs (e.g., millions of reads vs thousands of writes)
- Different scaling requirements (read replicas, separate read databases)
- Complex read models that differ significantly from write models
- Event sourcing requirements
- Microservices with separate read/write services

**You can still do full DDD without CQRS** - DDD focuses on:
- Rich domain models
- Aggregates and consistency boundaries
- Value objects
- Domain events
- Bounded contexts

CQRS is a separate pattern that can complement DDD but is not required.

---

## Phase 1: Domain Analysis & Bounded Context Identification

### 1.1 Domain Analysis
- [ ] Analyze existing entities and their relationships
- [ ] Identify domain concepts and business rules
- [ ] Document current business workflows
- [ ] Identify shared concepts and potential conflicts

### 1.2 Bounded Context Identification
Based on the current domain, identify the following bounded contexts:

- [ ] **Event Management Context**
  - Events, Enrollments, Event Repertoire, Event Videos, Trophies
  - Domain: Event planning, member participation, performance tracking

- [ ] **Repertoire Management Context**
  - Songs, Albums, Song Videos, Song YouTube URLs, Song Play Counts
  - Domain: Music catalog, song management, media content

- [ ] **Member Management Context**
  - ApplicationUser, MemberStatus, MemberInstrument, RoleAssignment
  - Domain: User accounts, member lifecycle, roles and permissions

- [ ] **Rehearsal Management Context**
  - Rehearsals, RehearsalAttendance
  - Domain: Practice scheduling and attendance tracking

- [ ] **Meeting Management Context**
  - Meetings, MeetingRequest, MeetingParticipation, MeetingAta, MeetingAtaAgendaPoint, MeetingAtaAttachment, MeetingAtaConfirmation
  - Domain: Meeting organization, minutes, and participation

- [ ] **Discussion & Communication Context**
  - Discussions, Posts, Comments, CommentImages, PostMedia
  - Domain: Event discussions, community engagement

- [ ] **Messaging Context**
  - Conversations, Messages, ConversationUserSettings
  - Domain: Direct messaging between members

- [ ] **Inventory & Shop Context**
  - Instruments, Products, ProductReservations, Trophies (physical)
  - Domain: Equipment management, merchandise, reservations

- [ ] **Logistics Context**
  - LogisticsBoard, LogisticsList, LogisticsCard, LogisticsCardAssignment, LogisticsCardReminder
  - Domain: Task management, event logistics

- [ ] **Financial Management Context**
  - Transactions, FiscalYear, MemberDebt, Activities
  - Domain: Financial tracking, budgeting, debt management

- [ ] **Gaming & Engagement Context**
  - Games, GameScores, Bets, BetOptions, UserBets, BetComments
  - Domain: Gamification, member engagement

- [ ] **Content & Media Context**
  - GalleryMedia, GalleryMediaPersonTag, Slideshows, Reports
  - Domain: Media library, content management

- [ ] **Education Context**
  - NaipeContent, NaipeComment, NaipePlayCount, NaipeTypeConfig
  - Domain: Educational content and learning materials

- [ ] **Q&A Context**
  - Questions, QuestionReplies
  - Domain: Knowledge base, FAQ management

- [ ] **Infrastructure/Shared Kernel**
  - AuditLog, GeocodingCache, PushSubscription, Labels, Requests
  - Domain: Cross-cutting concerns, shared utilities

### 1.3 Context Mapping
- [ ] Create context map diagram showing relationships between bounded contexts
- [ ] Identify upstream/downstream relationships
- [ ] Document integration patterns (Shared Kernel, Customer-Supplier, Conformist, etc.)
- [ ] Identify anti-corruption layers needed

---

## Phase 2: Domain Model Refactoring

### 2.1 Create Domain Layer Structure
- [ ] Create `RTUB.Domain` project (or refactor `RTUB.Core`)
- [ ] Organize by bounded contexts:
  ```
  RTUB.Domain/
  ├── SharedKernel/          # Shared value objects, base classes
  ├── EventManagement/        # Event bounded context
  ├── RepertoireManagement/  # Songs/Albums bounded context
  ├── MemberManagement/      # Users/Members bounded context
  ├── RehearsalManagement/   # Rehearsals bounded context
  ├── MeetingManagement/     # Meetings bounded context
  ├── Discussion/            # Discussions bounded context
  ├── Messaging/             # Messaging bounded context
  ├── Inventory/             # Inventory bounded context
  ├── Logistics/             # Logistics bounded context
  ├── Financial/             # Financial bounded context
  ├── Gaming/                # Gaming bounded context
  ├── Content/               # Content bounded context
  ├── Education/             # Education bounded context
  └── QnA/                   # Q&A bounded context
  ```

### 2.2 Implement Value Objects
- [ ] Create `Money` value object for financial operations
- [ ] Create `Email` value object for email addresses
- [ ] Create `Address` value object for locations
- [ ] Create `DateRange` value object for date intervals
- [ ] Create `Duration` value object for time spans
- [ ] Create `Url` value object for URLs
- [ ] Create `InstrumentType` value object (if not already enum)
- [ ] Create `EventType` value object (if not already enum)
- [ ] Ensure all value objects are immutable and have equality comparison

### 2.3 Identify and Refactor Aggregates
For each bounded context, identify aggregate roots:

- [ ] **Event Management**
  - Aggregate Root: `Event`
  - Entities: `Enrollment`, `EventRepertoire`, `EventVideo`
  - Value Objects: `EventLocation`, `EventDateRange`

- [ ] **Repertoire Management**
  - Aggregate Root: `Album`
  - Entities: `Song`, `SongVideo`, `SongYouTubeUrl`
  - Value Objects: `SongMetadata`, `Duration`

- [ ] **Member Management**
  - Aggregate Root: `Member` (refactor from ApplicationUser)
  - Entities: `MemberStatus`, `MemberInstrument`, `RoleAssignment`
  - Value Objects: `Email`, `MemberProfile`

- [ ] **Rehearsal Management**
  - Aggregate Root: `Rehearsal`
  - Entities: `RehearsalAttendance`
  - Value Objects: `RehearsalSchedule`

- [ ] **Meeting Management**
  - Aggregate Root: `Meeting`
  - Entities: `MeetingParticipation`, `MeetingAta`, `MeetingAtaAgendaPoint`, `MeetingAtaAttachment`, `MeetingAtaConfirmation`
  - Value Objects: `MeetingSchedule`

- [ ] **Discussion**
  - Aggregate Root: `Discussion`
  - Entities: `Post`, `Comment`
  - Value Objects: `PostContent`, `CommentContent`

- [ ] **Messaging**
  - Aggregate Root: `Conversation`
  - Entities: `Message`, `ConversationUserSettings`
  - Value Objects: `MessageContent`

- [ ] **Inventory**
  - Aggregate Root: `Product` or `Instrument`
  - Entities: `ProductReservation`
  - Value Objects: `ProductDetails`, `ReservationPeriod`

- [ ] **Logistics**
  - Aggregate Root: `LogisticsBoard`
  - Entities: `LogisticsList`, `LogisticsCard`, `LogisticsCardAssignment`, `LogisticsCardReminder`
  - Value Objects: `CardStatus`, `Priority`

- [ ] **Financial**
  - Aggregate Root: `FiscalYear` or `Transaction`
  - Entities: `MemberDebt`, `Activity`
  - Value Objects: `Money`, `TransactionDetails`

- [ ] **Gaming**
  - Aggregate Root: `Game` or `Bet`
  - Entities: `GameScore`, `BetOption`, `UserBet`, `BetComment`
  - Value Objects: `Score`, `BetDetails`

- [ ] **Content**
  - Aggregate Root: `GalleryMedia` or `Slideshow`
  - Entities: `GalleryMediaPersonTag`
  - Value Objects: `MediaMetadata`

- [ ] **Education**
  - Aggregate Root: `NaipeContent`
  - Entities: `NaipeComment`, `NaipePlayCount`
  - Value Objects: `ContentMetadata`

- [ ] **Q&A**
  - Aggregate Root: `Question`
  - Entities: `QuestionReply`
  - Value Objects: `QuestionContent`

### 2.4 Implement Rich Domain Models
For each aggregate root:

- [ ] Move business logic from services into domain entities
- [ ] Add domain methods that enforce invariants
- [ ] Ensure aggregates maintain consistency boundaries
- [ ] Add validation logic within domain entities
- [ ] Remove anemic domain model patterns (getters/setters only)

### 2.5 Implement Domain Events
- [ ] Create `IDomainEvent` interface
- [ ] Create base `DomainEvent` class
- [ ] Implement domain events for each aggregate:
  - [ ] `EventCreated`, `EventCancelled`, `EventUpdated`
  - [ ] `EnrollmentCreated`, `EnrollmentUpdated`
  - [ ] `SongAdded`, `SongUpdated`
  - [ ] `MemberRegistered`, `MemberStatusChanged`
  - [ ] `RehearsalScheduled`, `AttendanceRecorded`
  - [ ] `MeetingCreated`, `AtaGenerated`
  - [ ] `PostCreated`, `CommentAdded`
  - [ ] `MessageSent`, `ConversationCreated`
  - [ ] `ProductReserved`, `ReservationCancelled`
  - [ ] `CardCreated`, `CardAssigned`
  - [ ] `TransactionRecorded`, `DebtCreated`
  - [ ] `BetCreated`, `BetPlaced`
  - [ ] `MediaUploaded`, `TagAdded`
  - [ ] `ContentPublished`, `CommentAdded`
  - [ ] `QuestionAsked`, `ReplyAdded`

### 2.6 Create Domain Services
Identify operations that don't naturally belong to a single aggregate:

- [ ] `EventSchedulingService` - coordinates between Event and Rehearsal aggregates
- [ ] `MemberEnrollmentService` - coordinates Event enrollment business rules
- [ ] `RepertoirePlanningService` - coordinates song selection for events
- [ ] `FinancialCalculationService` - complex financial calculations
- [ ] `NotificationDomainService` - domain-level notification rules
- [ ] `PermissionDomainService` - domain-level permission checks

---

## Phase 3: Application Layer Refactoring

### 3.1 Create Application Layer Structure
- [ ] Organize application layer by bounded contexts:
  ```
  RTUB.Application/
  ├── Shared/                # Shared application concerns
  ├── EventManagement/
  │   ├── Services/         # Application services (orchestration)
  │   ├── DTOs/             # Data transfer objects
  │   └── Mappings/         # Domain to DTO mappings (optional)
  │   # If using CQRS, add:
  │   ├── Commands/         # Command handlers (CQRS only)
  │   └── Queries/          # Query handlers (CQRS only)
  ├── RepertoireManagement/
  ├── MemberManagement/
  ├── RehearsalManagement/
  ├── MeetingManagement/
  ├── Discussion/
  ├── Messaging/
  ├── Inventory/
  ├── Logistics/
  ├── Financial/
  ├── Gaming/
  ├── Content/
  ├── Education/
  └── QnA/
  ```

### 3.2 Application Services Pattern (Recommended - Simpler Approach)

**Note on CQRS**: CQRS (Command Query Responsibility Segregation) is **optional** and not required for DDD. For most applications, especially those with:
- Standard CRUD operations
- Moderate read/write complexity
- Single database (like SQLite)
- No need for separate read/write models

A simpler **Application Services** pattern is recommended. CQRS adds complexity and is only beneficial when you have:
- Very high read/write separation needs
- Different scaling requirements for reads vs writes
- Complex read models that differ significantly from write models
- Event sourcing requirements

**Choose one approach:**

#### Option A: Application Services (Recommended for RTUB)
- [ ] Create application services that orchestrate domain operations
- [ ] Services handle both commands (writes) and queries (reads)
- [ ] Keep existing service pattern but refactor to work with aggregates
- [ ] Services coordinate between repositories and domain entities

#### Option B: CQRS Pattern (Optional - Only if needed)
- [ ] Create `ICommand` interface
- [ ] Create `ICommandHandler<TCommand>` interface
- [ ] Create `IQuery<TResult>` interface
- [ ] Create `IQueryHandler<TQuery, TResult>` interface
- [ ] Implement command handlers for write operations
- [ ] Implement query handlers for read operations
- [ ] Refactor existing services to use CQRS pattern

### 3.3 Create Application Services (Use Cases)

**If using Option A (Application Services):**
For each bounded context, create application services that orchestrate domain operations:

- [ ] **Event Management**
  - [ ] `EventApplicationService` with methods:
    - [ ] `CreateEventAsync(...)` - orchestrates event creation
    - [ ] `UpdateEventAsync(...)` - orchestrates event updates
    - [ ] `CancelEventAsync(...)` - orchestrates event cancellation
    - [ ] `EnrollMemberAsync(...)` - orchestrates enrollment
    - [ ] `GetEventAsync(...)` - retrieves event data
    - [ ] `GetEventsAsync(...)` - retrieves event list

**If using Option B (CQRS):**
- [ ] **Event Management**
  - [ ] `CreateEventCommand` / `CreateEventCommandHandler`
  - [ ] `UpdateEventCommand` / `UpdateEventCommandHandler`
  - [ ] `CancelEventCommand` / `CancelEventCommandHandler`
  - [ ] `EnrollInEventCommand` / `EnrollInEventCommandHandler`
  - [ ] `GetEventQuery` / `GetEventQueryHandler`
  - [ ] `GetEventsQuery` / `GetEventsQueryHandler`

- [ ] **Repertoire Management**
  - [ ] `RepertoireApplicationService` with methods for album/song operations

- [ ] **Member Management**
  - [ ] `MemberApplicationService` with methods for member operations

- [ ] **Rehearsal Management**
  - [ ] `RehearsalApplicationService` with methods for rehearsal operations

- [ ] **Meeting Management**
  - [ ] `MeetingApplicationService` with methods for meeting operations

- [ ] **Discussion**
  - [ ] `DiscussionApplicationService` with methods for discussion operations

- [ ] **Messaging**
  - [ ] `MessagingApplicationService` with methods for messaging operations

- [ ] **Inventory**
  - [ ] `InventoryApplicationService` with methods for inventory operations

- [ ] **Logistics**
  - [ ] `LogisticsApplicationService` with methods for logistics operations

- [ ] **Financial**
  - [ ] `FinancialApplicationService` with methods for financial operations

- [ ] **Gaming**
  - [ ] `GamingApplicationService` with methods for gaming operations

- [ ] **Content**
  - [ ] `ContentApplicationService` with methods for content operations

- [ ] **Education**
  - [ ] `EducationApplicationService` with methods for education operations

- [ ] **Q&A**
  - [ ] `QnAApplicationService` with methods for Q&A operations

### 3.4 Implement Domain Event Handlers
- [ ] Create `IDomainEventHandler<TEvent>` interface
- [ ] Implement event handlers for cross-cutting concerns:
  - [ ] `SendNotificationOnEventCreatedHandler`
  - [ ] `UpdateStatisticsOnEnrollmentHandler`
  - [ ] `SendEmailOnMeetingScheduledHandler`
  - [ ] `UpdateCacheOnSongUpdatedHandler`
  - [ ] `LogAuditOnMemberStatusChangedHandler`
  - [ ] Add more handlers as needed

### 3.5 Refactor DTOs
- [ ] Ensure DTOs are separate from domain entities
- [ ] Create mapping logic (AutoMapper profiles or manual mappers)
- [ ] Update DTOs to match new domain structure
- [ ] Remove business logic from DTOs

---

## Phase 4: Infrastructure Layer Refactoring

### 4.1 Repository Pattern Refactoring
- [ ] Create aggregate-specific repositories:
  - [ ] `IEventRepository` - returns `Event` aggregate
  - [ ] `IAlbumRepository` - returns `Album` aggregate
  - [ ] `IMemberRepository` - returns `Member` aggregate
  - [ ] `IRehearsalRepository` - returns `Rehearsal` aggregate
  - [ ] Continue for all aggregate roots

- [ ] Ensure repositories only return aggregate roots
- [ ] Implement Unit of Work pattern for transaction management
- [ ] Update repository implementations to work with aggregates

### 4.2 Domain Event Publishing
- [ ] Create `IDomainEventPublisher` interface
- [ ] Implement domain event dispatcher
- [ ] Integrate with existing event infrastructure (if any)
- [ ] Ensure events are published after successful persistence

### 4.3 Persistence Configuration
- [ ] Update EF Core configurations for aggregates
- [ ] Configure value object persistence
- [ ] Update DbContext to work with aggregates
- [ ] Ensure proper aggregate loading (eager/lazy loading strategy)
- [ ] Configure domain event persistence (if storing events)

### 4.4 Anti-Corruption Layer
- [ ] Create ACL for external systems (if any)
- [ ] Create ACL for shared kernel access
- [ ] Document integration points

---

## Phase 5: Presentation Layer Updates

### 5.1 Update Controllers/Pages
- [ ] Refactor controllers to use command/query handlers
- [ ] Update Blazor pages to use new application services
- [ ] Ensure proper error handling and validation
- [ ] Update view models to match new DTOs

### 5.2 Update Dependency Injection
- [ ] Register command handlers
- [ ] Register query handlers
- [ ] Register domain event handlers
- [ ] Register aggregate repositories
- [ ] Update service registrations

---

## Phase 6: Testing & Validation

### 6.1 Unit Tests
- [ ] Write unit tests for domain entities
- [ ] Write unit tests for value objects
- [ ] Write unit tests for domain services
- [ ] Write unit tests for aggregate invariants
- [ ] Write unit tests for command handlers
- [ ] Write unit tests for query handlers

### 6.2 Integration Tests
- [ ] Write integration tests for repositories
- [ ] Write integration tests for command handlers
- [ ] Write integration tests for domain events
- [ ] Write integration tests for cross-bounded-context operations

### 6.3 Functional Testing
- [ ] Test all existing functionality still works
- [ ] Test event workflows end-to-end
- [ ] Test enrollment workflows end-to-end
- [ ] Test all CRUD operations
- [ ] Test business rule enforcement
- [ ] Test error scenarios

### 6.4 Performance Testing
- [ ] Verify aggregate loading performance
- [ ] Verify query performance
- [ ] Verify event handling performance
- [ ] Optimize slow operations

---

## Phase 7: Documentation & Cleanup

### 7.1 Documentation
- [ ] Document bounded contexts
- [ ] Document aggregates and their boundaries
- [ ] Document domain events
- [ ] Document value objects
- [ ] Update architecture diagrams
- [ ] Create developer guide for DDD patterns

### 7.2 Code Cleanup
- [ ] Remove unused code
- [ ] Remove deprecated patterns
- [ ] Update code comments
- [ ] Ensure consistent naming conventions
- [ ] Run code analysis and fix issues

### 7.3 Migration Validation
- [ ] Verify all tests pass
- [ ] Verify no functionality is broken
- [ ] Verify performance is maintained or improved
- [ ] Get stakeholder sign-off

---

## Best Practices Checklist

### Domain Layer
- [ ] Entities have rich behavior, not just properties
- [ ] Aggregates maintain consistency boundaries
- [ ] Value objects are immutable
- [ ] Domain events capture important business occurrences
- [ ] No infrastructure dependencies in domain layer
- [ ] Business rules are enforced in domain layer

### Application Layer
- [ ] Application services orchestrate domain operations
- [ ] Services coordinate between repositories and domain entities
- [ ] No business logic in application layer (business logic in domain)
- [ ] Proper use of DTOs for data transfer
- [ ] If using CQRS: Commands represent user intentions, Queries are read-only

### Infrastructure Layer
- [ ] Repositories only return aggregate roots
- [ ] Persistence concerns are isolated
- [ ] Domain events are properly published
- [ ] Unit of Work manages transactions

### General
- [ ] SOLID principles are followed
- [ ] Code is testable
- [ ] Dependencies point inward (toward domain)
- [ ] No circular dependencies
- [ ] Clear separation of concerns

---

## Migration Strategy

### Incremental Migration Approach
1. **Start with one bounded context** (recommend Event Management as it's core)
2. **Complete full DDD refactoring** for that context before moving to next
3. **Maintain backward compatibility** during migration
4. **Test thoroughly** after each context migration
5. **Document learnings** and adjust plan as needed

### Risk Mitigation
- [ ] Create feature flags for new DDD code paths
- [ ] Maintain old code paths during migration
- [ ] Gradual rollout with monitoring
- [ ] Rollback plan for each phase

---

## Notes

- **Last Updated**: [Date will be updated by agents]
- **Current Phase**: Phase 1 - Domain Analysis
- **Next Steps**: [Will be updated as work progresses]

---

## Progress Tracking

Agents working on this migration should:
1. Mark checkboxes as `[x]` when tasks are completed
2. Update "Last Updated" date
3. Update "Current Phase" section
4. Add notes about any deviations or learnings
5. Document any blockers or issues encountered
