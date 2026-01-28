# Test Structure

This document describes how tests are organized, where test data lives, and how Unit / Integration tests are separated.

## Project layout

| Project | Purpose |
|--------|---------|
| **RTUB.Core.Tests** | Unit tests for entities, enums, helpers, validation |
| **RTUB.Application.Tests** | Unit tests for services, repositories, DTOs |
| **RTUB.Shared.Tests** | Component tests (bUnit) for shared Blazor components |
| **RTUB.Web.Tests** | Component tests (bUnit) for pages, Web-specific components, controllers |
| **RTUB.Integration.Tests** | HTTP + DB integration tests (WebApplicationFactory, in-memory SQLite) |

## Separation of concerns

- **Unit / Component**: Isolated, mocked dependencies, no real HTTP or DB. Fast.
- **Integration**: Real app host, in-memory DB, HTTP client. Tests full request pipeline.

## Test data

- **Web page tests**: Use `RTUB.Web.Tests.TestData.PageTestDataBuilders` for Albums, Songs, Meetings, Rehearsals, Slideshows, Requests. Add more builders as new page tests are added.
- **Application / Integration**: Use in-memory DB, seed data, or mocks as needed. See `RTUB.Integration.Tests` and `TestWebApplicationFactory` for patterns.

## Naming

- Test methods: `MethodName_StateUnderTest_ExpectedBehavior`
- Test classes: `{ComponentOrPage}Tests` (e.g. `AlbumsPageTests`, `MediaUploadManagerTests`)

## See also

- `docs/tests-practices.md` – AAA, mocking, coverage
- `docs/CODE_IMPROVEMENT_REPORT.md` – Phase 0.5–0.7, test-first strategy
