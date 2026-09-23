# RTUB - Real Tuna Universitária de Bragança

A modern **Blazor Web Application** for managing and promoting the Real Tuna Universitária de Bragança, a traditional Portuguese university music group ("tuna" in Portuguese).

## Table of Contents

- [About](#about)
- [Architecture](#architecture)
- [Technologies](#technologies)
  - [Progressive Web App (PWA)](#progressive-web-app-pwa)
- [Features](#features)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

## About

RTUB (Real Tuna Universitária de Bragança) is a comprehensive web platform built with **Blazor Interactive Server** that serves as the digital hub for the university's traditional tuna music group. A "tuna" is a traditional Portuguese university music ensemble with deep cultural roots.

This interactive application provides tools for managing members, events, performances, repertoire, rehearsals, meetings, finances, internal communication, media, and an RPG-style gamification system — all through a modern, responsive, installable PWA interface.

## Architecture

The project follows a **Clean Architecture** pattern with clear separation of concerns:

```
RTUB/
├── src/
│   ├── RTUB.Core/          # Domain entities and business logic
│   ├── RTUB.Application/   # Application services and use cases
│   ├── RTUB.Shared/        # Shared Razor components library
│   └── RTUB.Web/           # Blazor Web App (Interactive Server)
└── tests/                  # Unit and integration tests
```

### Architecture Layers

- **RTUB.Core**: Contains domain models, entities, enums, and core business rules
- **RTUB.Application**: Implements application services, repositories, DTOs, interfaces, and business logic orchestration
- **RTUB.Shared**: Houses 90+ reusable Razor components (cards, badges, modals, forms, tables, uploads)
- **RTUB.Web**: **Blazor Web App** with Interactive Server components, pages, SignalR hubs, API controllers, and PixiJS game engine

## Technologies

### Core Framework
- **Blazor Web App (Interactive Server)** - Modern web UI framework with C#
- **.NET 10** - Latest .NET platform with C# 14
- **ASP.NET Core 10** - Web framework foundation with enhanced Blazor features
- **Entity Framework Core 10** - ORM for database operations
- **SQLite** - Lightweight database for data persistence

### Language Composition
- **C#** - Backend development, Blazor components, and business logic
- **TypeScript** - PixiJS game engine (battle scenes, survive mode, arena)
- **JavaScript** - Client-side features, mini-games, audio playback, maps
- **HTML/CSS** - Razor markup and ITCSS-organized styling

### Key Technologies & Libraries

#### Frontend
- **Blazor Interactive Server** - Real-time UI updates via SignalR
- **Razor Components** - Component-based UI architecture (90+ shared components)
- **Bootstrap 5** - Responsive CSS framework
- **PixiJS 8** - 2D WebGL game engine for battle animations
- **Leaflet** - Interactive member map with geocoding
- **Cropper.js** - Image cropping and upload
- **Vite** - TypeScript build tooling for PixiJS bundles
- **Web Audio API** - Audio playback for music and game sounds
- **Media Session API** - Lockscreen music controls

#### Backend & Services
- **ASP.NET Core Identity** - Authentication and authorization
- **SignalR** - Real-time messaging and chat
- **QuestPDF** - PDF generation for meeting minutes and reports
- **Response Compression** (Brotli/Gzip) - Performance optimization
- **Memory Cache** - In-memory caching for high-traffic data
- **Background Services** - 7 scheduled notification/reminder services

#### Storage & Integrations
- **Cloudflare R2** - Primary cloud storage (images, audio, videos, documents)
- **Google Drive** - Secondary storage for audio and documents
- **Nominatim** - OpenStreetMap geocoding for member locations
- **SMTP** - Email notifications with rate limiting
- **Web Push API** - Browser push notifications

#### Database & Migrations
- **SQLite** - Primary database
- **Entity Framework Core Migrations** - Database version control
- **Automatic Migration & Seeding** - Database initialization on startup

### Progressive Web App (PWA)

RTUB is a **production-ready Progressive Web App** that can be installed on devices and work offline:

- **Installable**: Add to home screen on mobile and desktop
- **Offline Support**: Service Worker caches core assets for offline functionality
- **App-like Experience**: Runs in standalone mode without browser UI
- **Push Notifications**: Native push notification support
- **Mobile App Ready**:
  - Android: Packaged via Trusted Web Activities (TWA) → [Android Guide](docs/android-twa-checklist.md)
  - iOS: Packaged via PWABuilder or Xcode → [iOS Guide](docs/ios-app-store-guide.md)

**PWA Features**:
- Web App Manifest with TWA-compatible `id` field
- Service Worker with intelligent caching strategies
- Automatic service worker registration and updates
- iOS-optimized with Apple touch icons and web app meta tags
- 192x192 and 512x512 app icons (maskable)
- HTTPS-ready for production deployment
- Media Session API with lockscreen controls (PWA-only Next/Previous)
- 4 shortcuts: Events, Rehearsals, Messages, Gallery

**Documentation**:
- [PWA Setup Guide](docs/pwa-setup.md) - Testing, development, and deployment
- [Android TWA Packaging](docs/android-twa-checklist.md) - Google Play Store submission
- [TWA Configuration Guide](docs/twa-configuration-guide.md) - Digital Asset Links setup
- [TWA Release Runbook](docs/twa-release-runbook.md) - Maintenance and troubleshooting
- [iOS App Store Guide](docs/ios-app-store-guide.md) - Apple App Store submission
- [PWA Media Session](docs/pwa-media-session.md) - Lockscreen music controls

## Features

### Event Management

- Create, schedule, and manage performances and events
- 11 event types: Festival, Atuacao, Casamento, Serenata, Arraial, Convivio, Nerba, Missa, Batizado, Arruada, Aniversario
- Member enrollment with instrument selection (primary + additional instruments)
- Event repertoire planning — assign songs from the library to events
- Event discussion threads with posts, comments, and @mentions
- Event video uploads and management
- Attendance tracking and enrollment statistics
- Cancellation workflow with reason tracking
- Event filtering and search

### Rehearsal Management

- Schedule rehearsals with date, location, theme, and time range
- Attendance tracking with instrument selection per rehearsal
- Approval reminder background service for pending attendances
- Rehearsal statistics and participation analytics
- Cancellation support with reason

### Member Management

- Full member profiles: first name, last name, nickname (nome de tuna), email, phone, city, date of birth, degree
- Profile pictures with image upload and cropping
- Member categories with automatic progression:
  - **Leitao** — Not yet an official member
  - **Caloiro** — New member
  - **Tuno** — Active member
  - **Veterano** — 2+ years of membership
  - **Tunossauro** — 4+ years of membership
  - **Tuno Honorario** — Honorary member
  - **Fundador** — Founding member
- Organizational positions (governance roles):
  - **Direcao**: Magister, Vice-Magister, Secretario, Primeiro Tesoureiro, Segundo Tesoureiro
  - **Mesa da Assembleia**: Presidente, Primeiro Secretario, Segundo Secretario
  - **Conselho Fiscal**: Presidente, Primeiro Relator, Segundo Relator
  - **Conselho de Veteranos**: Presidente
  - **Ensaiador** (Rehearsal conductor)
- Member map — geographic visualization of member locations using Leaflet with Nominatim geocoding (background queue for batch processing, caching)
- Hierarchy visualization — organizational structure display
- Mentor/mentee relationships
- Musical instrument assignments (13 instrument types: Guitarra, Bandolim, Cavaquinho, Acordeao, Fagote, Flauta, Baixo, Contrabaixo, Percussao, Pandeireta, Estandarte, Violino, Saxofone)
- Retirement tracking with automatic status updates (background service)
- Expulsion system with forced logout
- Member statistics and filtering
- XP and level system tied to gamification

### Meeting & Governance

- 4 meeting types: Assembleia Geral Ordinaria, Assembleia Geral Extraordinaria, Conselho de Veteranos, Reuniao de Direcao
- Meeting request and scheduling workflow
- Participation tracking with attendance records
- Ata (minutes) system with full workflow:
  - Draft creation with agenda points
  - President, first secretary, and second secretary assignment
  - Quorum tracking (basis, present, absent)
  - File attachments
  - Publish and digital confirmation by participants
  - PDF generation and cloud storage (QuestPDF)
  - Status workflow: Draft → Published → Confirmed
- Meeting cancellation with reason

### Music & Repertoire

- Song library with comprehensive metadata: title, track number, lyric author, music author, adaptation notes, full lyrics, duration
- Album management with privacy controls (public, private, exclusive with access control)
- Spotify URL integration per song
- YouTube video links per song
- Song video uploads to cloud storage
- Play count tracking and statistics
- Audio playback using Web Audio API with HTML audio fallback
- Media Session API integration — lockscreen controls (play/pause, next/previous) in PWA mode
- Song content serving from Cloudflare R2 or Google Drive

### Media & Gallery

- Photo and media gallery with album organization
- Person tagging on gallery media (tag members in photos)
- Image upload with cropping (Cropper.js integration)
- Slideshow management for public display on homepage
- Event video uploads and playback
- Document storage and management (meeting documents, reports)
- Cloud storage via Cloudflare R2 with CDN proxy
- Download support for photos and documents

### Financial Management

- Transaction tracking with income and expense categorization
- Activity-based financial grouping (each event/activity tracks its own budget)
- Fiscal year management (academic year periods)
- Member debts (calotes) system:
  - Track individual member debts with descriptions
  - Compromise dates to delay notifications
  - Automated calotes notification background service
  - Public calotes page for transparency
- MBWAY transfer tracking — record mobile payment transfers for reconciliation
- Nerba supply orders — manage daily supply orders for multi-day events:
  - Item, stock quantity, price per unit
  - Auto-calculated total price (stock x price per unit)
  - Organized by event and order date
- Financial reports with PDF export
- Receipt uploads to cloud storage

### MyTuno — RPG & Gamification System

A full RPG-style character system integrated into the platform:

#### Character System
- Each member has a character with stats: HP, Power, Speed, Defense, Critical Chance
- XP-based leveling system with configurable scaling
- Multi-character support
- Daily reward claims
- Fidelis currency (earned through gameplay and betting)

#### Game Modes
- **Stage Mode** — PvE campaign with 20,000 floors across 20 biomes (Forest, Swamp, Mountain, Desert, Tundra, Volcano, Ocean, Jungle, Cavern, Ruins, Skylands, Underworld, Crystal, Shadow, Storm, Celestial, Inferno, Void, Nexus, Abyss), checkpoints every 10 stages, boss fights
- **Arena Mode** — PvP battles between player characters, rating system, unlocked after completing Stage mode
- **Boss Mode** — Endless boss progression, costs Fitab currency to enter, daily boss with persistent HP, tracks total runs and stages cleared
- **Survive Mode** — Endless survival gameplay

#### Battle Engine
- Deterministic combat engine with seeded RNG for fair, reproducible battles
- PixiJS 2D WebGL rendering with sprite animations, projectile system, particle effects, and VFX
- Audio system with sound effects (Web Audio API)
- Real-time combat action service for interactive battles
- Weapon-specific attack animations and visual effects

#### Inventory System
- **Consumables**: Fino (25% HP heal), Caneca (50% HP heal), Shot (+5% all stats buff), Cigarro (+10% dodge shield), Canhao (AOE damage), Penalty (0.5% lifesteal)
- **Equipment**: 6 armor slots (Head, Shoulders, Chest, Gloves, Legs, Boots) with normal and rare set variants
- **Weapons**: 11 types across one-handed (Sword, Axe, Mace, Shield, Dagger) and two-handed (Staff, Bow, Greatsword, Spear, Greataxe, Hammer)
- **Instrument Parts**: 13 types (one per musical instrument) — dropped from stage enemies
- **Drinks**: 10 tiers (Cerveja through Aguardente) — used for forging
- **Currencies**: Leitao (Boss Mode), Fitab (Boss Mode entry)

#### Forge System
- Combine instrument parts + drinks to create weapons via forge combos
- Weapon leveling and upgrading
- Player-named weapons with stat bonuses
- Configurable forge combo recipes

#### Upgrades & Improvements
- Character stat upgrades (cast speed, energy capacity, energy regeneration, double gathering chance)
- Consumable upgrades (improve effectiveness)
- Upgrade shop interface

#### Configuration
- Full game balance configurable via `scaling.config.json` — base stats, level scaling, combat mechanics, consumable values, equipment effects, gathering system

### Mini-Games

4 browser-based mini-games with leaderboards:
- **Avoid Questions** — Dodge-style game
- **BMR (Bebe Mais Rui)** — Themed mini-game
- **Passaro Maluco** — Bird-style game
- **Tomato Thrower** — Throwing game

Each game tracks high scores with the GameScore system, ranks players on leaderboards, and awards Fidelis currency.

### Betting System

- Create bets with multiple options and configurable odds
- Bet categories: Match and Decision
- Wager Fidelis currency on outcomes
- Result settlement with automatic Fidelis payouts based on odds
- Bet commenting for discussion
- Bet cancellation support
- Image/thumbnail support for bets

### Internal Messaging

- Real-time chat powered by SignalR WebSocket hub
- 1-on-1 and group conversations
- Typing indicators (started/stopped)
- Unread message tracking and badge counter
- Message editing and soft deletion
- Announcement-only channels (restrict who can post)
- Conversation archiving
- Group conversation sync
- Message composer with @mention support
- Auto-scroll to latest messages

### Questions (Q&A for Orgaos Sociais)

- Members submit questions directed to governance bodies (Direcao, Mesa da Assembleia, Conselho Fiscal, Conselho de Veteranos)
- Assign questions to specific positions or individual members
- Status workflow: Unanswered → In Discussion → Answered → Closed
- Threaded replies with editing support
- Server-side pagination with denormalized LastActivityAt for performance
- Background notification service for unanswered questions
- Soft delete support

### Naipes (Sub-Organizations)

- Configurable naipe types with custom settings
- Content management per naipe (text, media)
- Media uploads (images, audio) to cloud storage
- Comments on naipe content
- Play count tracking
- Authorization control per naipe
- Content filtering

### Logistics & Task Management

- Kanban-style boards with drag-and-drop (JavaScript integration)
- Multiple lists per board
- Task cards with:
  - Assignments to specific members
  - Status tracking
  - Reminders with configurable frequency
- Board and list management

### Discussion Forums

- Discussion threads attached to events
- Posts with titles, rich text body, and media attachments
- Comments with image attachments
- @mention detection with notification delivery
- Post pinning and locking (moderation)
- Soft delete for posts and comments
- Last activity tracking for ordering

### Leaderboard & Rankings

- Member rankings with customizable scoring
- Comments on leaderboard entries with like system
- Hall of Fame page for notable achievements
- Trophy management for events

### Notifications

Multi-channel notification system:

- **Email** (SMTP): Configurable templates, rate limiting, Gmail App Password support
- **Web Push**: Browser push notifications via Web Push API with subscription management
- **7 Background Notification Services**:
  1. Birthday email reminders
  2. Calotes (debt) notifications
  3. Weekly digest emails
  4. Rehearsal approval reminders
  5. Question notification reminders
  6. Activity engagement reminders
  7. Member status update notifications

### Audit & Administration

- Comprehensive audit logging — tracks all entity changes with before/after values
- Critical action flagging (role changes, user modifications, deletions)
- User-friendly change descriptions (binary data summarized, user IDs resolved to nicknames)
- Database viewer for debugging and data inspection
- Role management UI — assign/remove roles per user
- Label/tag system for content organization

### Security & Authentication

- ASP.NET Core Identity integration
- Role-based authorization: Owner, Admin, Member, Visitor
- Cookie authentication with security stamp validation
- Automatic logout on role change or expulsion
- Email confirmation required for account activation
- Simple password policy (min 4 characters)
- Account lockout after 5 failed attempts (5-minute lockdown)
- Anti-forgery protection
- Cascading authentication state in Blazor

### User Roles

- **Owner**: Full system access, audit logging, user role management, database viewer
- **Admin**: Full entity management, configuration, operational control
- **Member**: Access to member features, event participation, rehearsals, discussions, messaging, games
- **Visitor**: Public access to general information, events, and media gallery

For detailed information about roles, categories, and positions, see [Authentication & Business Rules](docs/auth-and-rules.md).

### .NET 10 Blazor Enhancements

This application leverages the latest .NET 10 and Blazor features:

- **C# 14**: Explicit language version for consistency and access to latest language features
- **ReconnectModal**: Enhanced user experience during SignalR circuit disconnections with Portuguese branding
- **Blazor Metrics**: Production-ready telemetry for circuit health and navigation performance monitoring
- **Optimized Static Assets**: Content-based versioning with granular cache control for maximum performance

For complete details on the .NET 10 upgrade, see [BLAZOR-NET10-CHANGELOG.md](BLAZOR-NET10-CHANGELOG.md).

## Project Structure

```
RTUB/
├── src/
│   ├── RTUB.Core/
│   │   ├── Entities/            # 85+ domain models
│   │   ├── Enums/               # 24 enumeration types
│   │   ├── Constants/           # Application constants
│   │   ├── Exceptions/          # Custom exception types
│   │   └── Helpers/             # Domain helpers
│   │
│   ├── RTUB.Application/
│   │   ├── Data/                # DbContext, configurations, seed data
│   │   ├── Interfaces/          # 198+ service and repository contracts
│   │   ├── Services/            # 136 business logic services
│   │   └── Repositories/        # 63 data access repositories
│   │
│   ├── RTUB.Shared/
│   │   └── Components/          # 90+ reusable Razor components
│   │       ├── Badges/          # Category, position, role, status badges
│   │       ├── Cards/           # 30+ card components for all entities
│   │       ├── Common/          # Empty state, error display, pagination
│   │       ├── Discussion/      # Post and comment components
│   │       ├── Forms/           # Form inputs, selects, date pickers
│   │       ├── Game/            # Fantasy tiles, game UI elements
│   │       ├── Modals/          # Modal dialogs and confirm dialogs
│   │       ├── Profile/         # Profile fields, headers, timelines
│   │       ├── Ranking/         # Leaderboard and rank display
│   │       ├── Tables/          # Search bars, sortable headers, pagination
│   │       ├── UI/              # Filters, spinners, navigation, popups
│   │       └── Uploads/         # Image cropper, media upload managers
│   │
│   └── RTUB.Web/
│       ├── Pages/               # 65+ Blazor pages across 12 sections
│       │   ├── Activities/      # Events, rehearsals, meetings, leaderboard, naipes
│       │   ├── Games/           # Mini-games (Avoid Questions, BMR, etc.)
│       │   ├── MyTuno/          # RPG system (Stage, Arena, Boss, Survive, Shop)
│       │   ├── Management/      # Finance, questions, logistics, roles, reports
│       │   ├── Members/         # Member list, profiles, map, hierarchy
│       │   ├── Media/           # Gallery, albums, songs, slideshows, documents
│       │   ├── Messages/        # Internal messaging inbox
│       │   └── Operations/      # Audit logs, database viewer, notifications
│       ├── Hubs/                # SignalR hub (MessagesHub)
│       ├── Controllers/         # API controllers (CDN proxy, downloads, push)
│       ├── Extensions/          # Service registration extensions
│       ├── pixi/                # TypeScript source for PixiJS game engine
│       │   ├── scenes/          # Battle, arena, survive scenes
│       │   ├── managers/        # Audio, input, particles, projectiles, UI
│       │   └── weapons/         # Weapon system and definitions
│       ├── wwwroot/             # Static files
│       │   ├── css/             # ITCSS-organized stylesheets
│       │   ├── js/              # 31 JavaScript files (games, audio, maps, UI)
│       │   ├── sprites/         # Game sprite sheets
│       │   ├── sound/           # Audio files
│       │   └── icons/           # PWA app icons
│       ├── App.razor            # Root component
│       └── Program.cs           # Application entry point
│
├── tests/
│   ├── RTUB.Core.Tests/         # Domain entity and business rule tests
│   ├── RTUB.Application.Tests/  # Service and repository tests
│   ├── RTUB.Shared.Tests/       # Shared component tests (bUnit)
│   ├── RTUB.Web.Tests/          # Page and integration tests (bUnit)
│   └── RTUB.Integration.Tests/  # End-to-end integration tests
│
└── docs/                        # Project documentation
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) (for PixiJS TypeScript build, optional)
- Visual Studio 2022 / Visual Studio Code / Rider
- Git

### Installation

1. **Clone the repository:**
```bash
git clone https://github.com/luisfpires18/RTUB.git
cd RTUB
```

2. **Restore NuGet packages:**
```bash
dotnet restore
```

3. **Configure the application:**
   - Copy `appsettings.Development.json.example` to `appsettings.Development.json` (if not present)
   - Update your local `appsettings.Development.json` with your development credentials
   - The file is ignored by git to keep your secrets safe

4. **Run the application:**
```bash
cd src/RTUB.Web
dotnet run
```

5. **Access the application:**
   - Navigate to `https://localhost:5001` or `http://localhost:5000`
   - The database will be automatically migrated and seeded on first run

### Configuration

#### Local Development

For local development, store secrets with [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), **not** in `appsettings.Development.json`.

> **`appsettings.Development.json` is NOT git-ignored** — there is no rule for it in `.gitignore`, so anything put there can be committed by accident. Keep credentials out of it.

User Secrets are stored outside the repository, per developer, and are loaded automatically in the Development environment. `src/RTUB.Web/RTUB.csproj` already declares a `UserSecretsId`, so no setup is needed beyond setting values:

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "AdminUser:Password" "<owner-password>"
```

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "SeedData:MemberPassword" "<member-seed-password>"
```

Replace each `<...>` placeholder with a real value of your own. `AdminUser:Password` is required the first time you start against an empty database; `SeedData:MemberPassword` is required only for the full member seed (see below). The same command sets any other secret, for example `EmailSettings:SmtpPassword` or `IDrive:SecretKey`.

List what is set with `dotnet user-secrets list --project src/RTUB.Web/RTUB.csproj`.

Non-secret local settings can still live in `appsettings.Development.json`:

```json
{
  "AdminUser": {
    "Username": "your-username",
    "Email": "your-email@example.com"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-gmail-app-password",
    "SenderEmail": "sender@example.com",
    "SenderName": "RTUB"
  },
  "IDrive": {
    "Endpoint": "s3.endpoint.example.com",
    "Bucket": "your-bucket"
  }
}
```

#### Resetting a local development database (destructive, opt-in)

`SeedData.ResetDevDataAsync` sanitises a local development database: it clears every push
subscription, resets **every** user's password to one configured value, and rewrites every email
to `{UserName}@rtub.pt`.

**It is destructive and disabled by default.** It runs only when the host environment is
`Development` **or** `Staging` — the Azure DEV App Service — **and** `DevelopmentDataReset:Enabled`
is `true`. The environment check is an allow-list and comes first, so `Production`, `Test` and any
other environment never run it whatever the configuration says. A normal startup with the setting
absent or `false` does not touch any password, email or push subscription.

`Staging` is included deliberately: Azure DEV carries a seeded member dataset that exists to be
reset to one shared development password on demand. Never enable it against a database whose
credentials matter.

Switch it on for a single intentional reset with User Secrets:

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Enabled" "true"
```

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Password" "<development-reset-password>"
```

- Replace `<development-reset-password>` with a real value of your own. There is no default and no
  hardcoded hash; the password is required whenever `Enabled` is `true` and is validated **before**
  anything is written, so a missing value leaves the database untouched rather than half reset. The
  value is never written to a log or an error message.
- Use it **only** when you intend to sanitise or reset your local development users. It overwrites
  the passwords seeded from `AdminUser:Password` and `SeedData:MemberPassword`.
- **Set `Enabled` back to `false` (or remove it) once the reset has run**, otherwise every
  subsequent startup resets the database again:

```bash
dotnet user-secrets remove --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Enabled"
```

- **Never put `DevelopmentDataReset:Password` in a tracked `appsettings` file.** User Secrets only.

#### Seeding a full development database

By default the application bootstraps the Owner account only. To create the full member dataset on a **fresh** database, set `isEmptyDb` to `false` in `SeedData.InitializeAsync` (`src/RTUB.Application/Data/SeedData.cs`) and make sure both `AdminUser:Password` and `SeedData:MemberPassword` are set as User Secrets first. Seeding fails with a clear error, creating no users at all, if either is missing.

**Note:** Keep `SmtpPassword`, `IDrive:AccessKey` and `IDrive:SecretKey` in User Secrets, not in the file above. For Gmail SMTP, use an [App Password](https://support.google.com/accounts/answer/185833) (not your regular Gmail password). Generate one in your Google Account settings under Security > 2-Step Verification > App passwords.

#### Production Deployment

For production, **do not include credentials in JSON files**. Instead, use environment variables:

**SMTP Configuration:**
- `EmailSettings__SmtpUsername` - SMTP username
- `EmailSettings__SmtpPassword` - SMTP password (for Gmail, use an App Password)
- `EmailSettings__SenderEmail` - Sender email address

**Admin User Configuration:**
- `AdminUser__Username` - Default admin username
- `AdminUser__Email` - Default admin email
- `AdminUser__Password` - Admin/Owner password. **Required** the first time the application
  starts against an empty database: the Owner account is created from this value and there is no
  default. Seeding fails with a clear error if it is missing, blank or left as a placeholder. Not
  needed once the database has users.

**Seed Data Configuration:**
- `SeedData__MemberPassword` - Password given to every member created by the bulk member seed.
  Only required when that seed is switched on (`isEmptyDb` set to `false` in
  `SeedData.InitializeAsync`) to build a full development database. There is no default, and it is
  validated before any user is written.

**Development Data Reset (local Development only):**
- `DevelopmentDataReset__Enabled` - Opt-in switch for the destructive local reset described above.
  Defaults to off. **Ignored outside the `Development` environment** — setting it on a `Production`
  or `Staging` host (including the Azure DEV App Service) does nothing.
- `DevelopmentDataReset__Password` - Password every user is reset to when the switch is on.
  Required whenever `Enabled` is `true`, validated before any write, and never logged. There is no
  default. Keep it in User Secrets, not in a tracked `appsettings` file.

**IDrive/S3 Configuration:**
- `IDrive__AccessKey` - S3-compatible storage access key
- `IDrive__SecretKey` - S3-compatible storage secret key
- `IDrive__Endpoint` - S3-compatible storage endpoint
- `IDrive__Bucket` - Storage bucket name

The configuration system follows the standard ASP.NET Core hierarchy (from lowest to highest priority):
1. `appsettings.json` (base settings)
2. `appsettings.{Environment}.json` (environment-specific overrides)
3. Environment variables (overrides all JSON settings)

### Default Configuration

The application uses SQLite by default with connection string:
```
Data Source=app.db
```

The database is automatically:
- Created on first run
- Migrated to the latest schema
- Seeded with initial data

## Development

### Building the Solution

```bash
# Build all projects
dotnet build

# Build in Release mode
dotnet build -c Release

# Build specific project
dotnet build src/RTUB.Web
```

### Running in Development Mode

```bash
cd src/RTUB.Web
dotnet watch run
```

This enables hot reload for Blazor components and C# code during development.

### Building PixiJS Game Engine

```bash
cd src/RTUB.Web/pixi
npm install
npm run build
```

This compiles TypeScript battle scenes into IIFE bundles using Vite.

### Development Features

- **Detailed Errors**: Enabled in development mode
- **Hot Reload**: Automatic UI updates on code changes
- **SignalR Debugging**: Detailed SignalR errors in development
- **HTTPS Redirection**: Automatically configured

### Code Style

The project follows standard C# coding conventions:
- PascalCase for public members
- camelCase for private fields
- Async suffix for asynchronous methods
- XML documentation for public APIs
- Clean code principles

## Testing

The project includes **5 test projects** with **4,500+ tests**:

| Project | Scope | Framework |
|---------|-------|-----------|
| `RTUB.Core.Tests` | Domain entities, business rules, enums | xUnit, FluentAssertions |
| `RTUB.Application.Tests` | Services, repositories, audit logging | xUnit, Moq, FluentAssertions |
| `RTUB.Shared.Tests` | Shared Razor components | xUnit, bUnit, FluentAssertions |
| `RTUB.Web.Tests` | Pages, page components | xUnit, bUnit, Moq |
| `RTUB.Integration.Tests` | End-to-end with EF Core InMemory | xUnit, FluentAssertions |

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test tests/RTUB.Application.Tests
```

## Deployment

### Azure Deployment

Production and DEV are deployed only by GitHub Actions - never by a manual or Visual Studio
publish, which would bypass the release archive and the smoke test:

- merge into `dev` → **Deploy • DEV** (`rtub-dev`)
- bump the root `VERSION` (SemVer), merge `dev` → `master` → **Deploy • PROD** (`rtub`)
- **Rollback • PROD** redeploys any archived version, e.g. `2.0.0`, without rebuilding
- `GET /api/version` reports the running version and commit

How to release, roll back, hotfix and restore the database: [`docs/release-and-rollback.md`](docs/release-and-rollback.md).
Environments, workflows and App Service settings: [`docs/ci-cd-and-azure-environments.md`](docs/ci-cd-and-azure-environments.md).
The application migrates its SQLite database on startup, after taking a pre-migration snapshot.

### Production Configuration

Update `appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=/path/to/production/app.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### Performance Optimization

The application includes:
- **Response Compression** (Brotli/Gzip)
- **Response Caching** for static files (30 days in production)
- **Memory Caching** for frequently accessed data (IMemoryCache)
- **SignalR optimization** for Blazor Interactive Server
- **Static file caching** with content-based versioning
- **Server-side pagination** with denormalized columns for high-traffic queries
- **Read projections** to minimize data transfer

## Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Contribution Guidelines

For detailed guidelines on coding style, adding components, creating pages, testing, and documentation, please see the [Contributing Guide](docs/contributing.md).

**Quick Guidelines:**
- Follow the existing code style (see [Contributing Guide](docs/contributing.md))
- Write meaningful commit messages
- Add tests for new features
- Update documentation in `/docs` as needed
- Ensure all tests pass before submitting PR

## Documentation

Comprehensive documentation is available in the `/docs` folder and root directory:

### Core Documentation
- **[README](README.md)** - This file: Project overview, setup, and getting started
- **[Changelog](docs/changelog.md)** - Version history and notable changes
- **[Work Log](docs/work-log.md)** - Detailed chronological development log

### Technical Documentation
- **[Backend Practices](docs/backend-practices.md)** - Backend coding guidelines
- **[Frontend Practices](docs/frontend-practices.md)** - Frontend best practices
- **[PWA Practices](docs/pwa-practices.md)** - Progressive Web App best practices
- **[Frontend Performance Optimizations](FRONTEND-PERFORMANCE-OPTIMIZATIONS.md)** - Performance improvements
- **[.NET 10 Upgrade Changelog](BLAZOR-NET10-CHANGELOG.md)** - Complete .NET 10 upgrade details
- **[Static Assets Decision](STATIC-ASSETS-DECISION.md)** - Asset management strategy

### Game System Documentation
- **[MyTuno Equipment](docs/my_tuno/)** - Equipment, weapons, scaling, and survive mode docs

### Architecture & Decisions
- **[Architectural Decision Records (ADRs)](docs/decisions/)** - Technical decisions and rationale

### Developer Guides
- **[Authentication & Business Rules](docs/auth-and-rules.md)** - Roles, categories, positions
- **[Contributing Guide](docs/contributing.md)** - Guidelines for developers

## License

This project is developed for the Real Tuna Universitária de Bragança.

## Contact

**Real Tuna Universitária de Bragança**

Project Maintainer: [@luisfpires18](https://github.com/luisfpires18)

Project Link: [https://github.com/luisfpires18/RTUB](https://github.com/luisfpires18/RTUB)

Project Live: [https://rtub.azurewebsites.net/](https://rtub.azurewebsites.net/)

---

**Built with Blazor** | Made for the Real Tuna Universitaria de Braganca
