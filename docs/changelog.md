# RTUB Changelog

All notable changes to the RTUB project will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added - 2025-11-26
- Comprehensive performance optimizations (backend + frontend)
- `ShouldRender()` implementation in 7 critical Blazor components
- Database indexes for Song, EventRepertoire, RoleAssignment, and Activity entities
- Enhanced service worker with intelligent asset caching strategies
- Test utilities: `MockHelpers` and `TestConstants` classes
- Documentation structure (`docs/` directory with work-log.md and changelog.md)

### Changed - 2025-11-26
- Migrated inline CSS to scoped CSS files (EventCard, ReconnectModal)
- Optimized MainLayout navigation to reduce database queries
- Improved JavaScript error handling and timeout protection
- Enhanced test organization and code quality

### Performance - 2025-11-26
- ~80-90% reduction in component re-renders
- ~70-80% reduction in database queries during navigation
- Improved page load times with better browser caching
- Better offline experience with enhanced service worker

### Documentation - 2025-11-26
- Created comprehensive [FRONTEND-PERFORMANCE-OPTIMIZATIONS.md](../FRONTEND-PERFORMANCE-OPTIMIZATIONS.md)
- Added work log for tracking project evolution
- Enhanced inline code documentation

---

## Previous Versions

### .NET 10 Upgrade - 2025
- Upgraded from .NET 8 to .NET 10
- Updated to C# 14 with explicit language version
- Implemented .NET 10 Blazor enhancements:
  - ReconnectModal with Portuguese branding
  - Blazor Metrics for circuit health monitoring
  - Optimized static assets with content-based versioning
- See [BLAZOR-NET10-CHANGELOG.md](../BLAZOR-NET10-CHANGELOG.md) for complete upgrade details

### Initial Release
- Blazor Web Application for Real Tuna Universitária de Bragança
- Event management system
- Member management with categories and positions
- Music and repertoire management
- Rehearsal tracking and attendance
- Media gallery with album management
- Financial management and reporting
- Inventory management (instruments, merchandise)
- Request system with approval workflow
- Email notification system
- Audit logging
- Role-based authorization (Owner, Admin, Member, Visitor)

---

## Documentation Links

- [Frontend Performance Optimizations](../FRONTEND-PERFORMANCE-OPTIMIZATIONS.md)
- [.NET 10 Upgrade Changelog](../BLAZOR-NET10-CHANGELOG.md)
- [Work Log](work-log.md) - Detailed chronological development log
- [README](../README.md) - Project overview and getting started guide

---

_This changelog is maintained by the Project Historian and Technical Writer agent._
