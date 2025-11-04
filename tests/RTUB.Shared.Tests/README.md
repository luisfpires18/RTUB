# RTUB.Shared.Tests

Component tests for reusable Blazor components in RTUB.Shared project.

## Overview

This test project contains comprehensive tests for all shared UI components using bUnit, FluentAssertions, and AutoFixture.

## Test Statistics

- **Total Tests**: 230
- **Components Tested**: 14
- **Pass Rate**: 100%
- **Execution Time**: < 1 second

## Components Tested

| Component | Tests | Description |
|-----------|-------|-------------|
| Modal | 24 | Modal dialogs with various sizes |
| ConfirmDialog | 20 | Confirmation dialogs |
| SortableTableHeader | 13 | Table header with sorting |
| TablePagination | 25 | Pagination controls |
| Alert | 21 | Alert messages (success, error, warning, info) |
| TableSearchBar | 17 | Search input with debounce |
| LoadingSpinner | 19 | Loading indicators |
| EmptyTableState | 18 | Empty state displays |
| RoleBadge | 10 | User role badges |
| StatusBadge | 8 | Request status badges |
| CategoryBadge | 11 | Member category badges |
| PositionBadge | 11 | Leadership position badges |
| ImageCropper | 14 | Image cropping modal |
| LabelEditButton | 9 | Admin edit button |

## Running Tests

```bash
# Run all Shared component tests
dotnet test tests/RTUB.Shared.Tests/RTUB.Shared.Tests.csproj

# Run with verbose output
dotnet test tests/RTUB.Shared.Tests/RTUB.Shared.Tests.csproj --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~ModalTests"
```

## Test Patterns

All tests follow these patterns:
- **AAA Pattern**: Arrange, Act, Assert
- **FluentAssertions**: Readable assertion syntax
- **Theory Tests**: Testing multiple scenarios with `[Theory]` and `[InlineData]`
- **AutoFixture**: Mock entity creation (for complex components)

## Dependencies

- bUnit 1.35.3 - Blazor component testing
- FluentAssertions 8.8.0 - Assertion library
- AutoFixture 4.18.1 - Test data generation
- xUnit 2.9.2 - Test framework
