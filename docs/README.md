# Authorization System Documentation

This directory contains comprehensive documentation for the RTUB authorization system migration from scattered boolean checks to a unified, claims-based, policy-driven model.

## Documents

### 📋 [Master Plan](master-plan.md)
**Start here** - Complete overview of the entire migration project.

- Executive summary
- 5-phase implementation plan
- Timeline (8-11 days estimated)
- Risk management
- Success criteria
- Next steps

### 📊 [Auth Model Analysis](auth-model-analysis.md)
**Phase 1 Complete** - Detailed analysis of the current authorization model.

- Current roles, categories, and positions
- 53 category checks documented
- 88 role checks documented
- Usage patterns and locations
- Authorization decision matrix
- Findings and recommendations

### 🏗️ [Claims Model Design](claims-model-design.md)
**Phase 2 Complete** - Complete design for the claims-based authorization system.

- Claim type constants
- 15 authorization policies
- ClaimsPrincipal extension methods
- UserClaimsFactory design
- Migration strategy
- Testing strategy
- Code examples

### 🔐 [Page Permissions Design](page-permissions-design.md)
**Phase 5 Design** - Design for the Owner-only page permissions admin system.

- PagePermission entity
- IPagePermissionService interface
- PagePermissionMiddleware
- Admin UI mockup
- Implementation plan
- Security considerations

## Quick Reference

### Current Roles
- **Owner** - Full system access, can manage all settings
- **Admin** - Elevated privileges, can manage most features
- **Member** - Standard authenticated user

### Member Categories (Hierarchy)
1. **Leitao** - Probationary member (lowest)
2. **Caloiro** - First year member
3. **Tuno** - Full member
4. **Veterano** - 2+ years as Tuno
5. **Tunossauro** - 6+ years as Tuno (highest)
6. **TunoHonorario** - Honorary member (special)
7. **Fundador** - Founder (special, 1991)

### Key Statistics
- **Total checks**: 141 authorization checks
- **Files affected**: 20+ files
- **Category checks**: 53
- **Role checks**: 88
- **Policies designed**: 15
- **Tests planned**: 190+

## Implementation Status

| Phase | Status | Deliverable | Duration |
|-------|--------|-------------|----------|
| Phase 1: Analysis | ✅ COMPLETE | auth-model-analysis.md | Done |
| Phase 2: Design | ✅ COMPLETE | claims-model-design.md | Done |
| Phase 3: Implementation | 📝 NOT STARTED | Code + Tests | 2-3 days |
| Phase 4: Migration | 📝 NOT STARTED | Code Refactoring | 3-4 days |
| Phase 5: Page Permissions | 📝 NOT STARTED | Admin UI + Middleware | 3-4 days |

## For Developers

### Reading Order

1. **Start with Master Plan** - Get the big picture
2. **Read Auth Model Analysis** - Understand current state
3. **Review Claims Model Design** - Learn new approach
4. **Check Page Permissions Design** - See the end goal

### When Implementing

**Phase 3 (Claims Infrastructure)**:
- Follow claims-model-design.md
- Create all constants classes first
- Implement factory and extensions
- Write unit tests immediately
- Test claims issuance thoroughly

**Phase 4 (Migration)**:
- Migrate one file at a time
- Run tests after each file
- Use provided refactoring patterns
- Mark old methods as obsolete
- Document any deviations

**Phase 5 (Page Permissions)**:
- Follow page-permissions-design.md
- Start with data layer
- Implement service with caching
- Add middleware carefully
- Build UI incrementally
- Test Owner protection thoroughly

## Migration Patterns

### Before (Old Way)
```razor
@if (currentUser != null && currentUser.IsTuno())
{
    <button>Admin Action</button>
}
```

### After (Policy)
```razor
<AuthorizeView Policy="@AuthorizationPolicies.RequireTunoOrHigher">
    <button>Admin Action</button>
</AuthorizeView>
```

### After (Claims)
```razor
@if (authState.User.IsAtLeastCategory(CategoryClaims.Tuno))
{
    <button>Admin Action</button>
}
```

## Testing

### Test Coverage Goals
- **Unit Tests**: 140+ tests for claims and extensions
- **Integration Tests**: 50+ tests for policies and middleware
- **E2E Tests**: Manual testing checklist provided

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/RTUB.Application.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Contributing

When making changes to the authorization system:

1. ✅ Read relevant documentation first
2. ✅ Follow the migration patterns
3. ✅ Write tests for new code
4. ✅ Update documentation if needed
5. ✅ Test with different user types
6. ✅ Verify no security regressions

## Questions?

- Check the master-plan.md for overview
- Check claims-model-design.md for implementation details
- Check auth-model-analysis.md for current state
- Review existing tests for examples

## Useful Constants

```csharp
// Claim Types
ClaimTypes.Category        // "rtub:category"
ClaimTypes.Position        // "rtub:position"
ClaimTypes.PrimaryCategory // "rtub:primary_category"

// Category Values
CategoryClaims.Leitao      // "LEITAO"
CategoryClaims.Caloiro     // "CALOIRO"
CategoryClaims.Tuno        // "TUNO"
CategoryClaims.Veterano    // "VETERANO"
CategoryClaims.Tunossauro  // "TUNOSSAURO"

// Policies
AuthorizationPolicies.RequireAdmin
AuthorizationPolicies.RequireTunoOrHigher
AuthorizationPolicies.CanBeMentor
AuthorizationPolicies.CanManageEvents
// ... see claims-model-design.md for all 15 policies
```

## License

These documents are part of the RTUB project and follow the same license as the main repository.

---

**Last Updated**: 2024 (Phase 1-2 Complete)
**Status**: Planning & Design Phase Complete, Ready for Implementation
