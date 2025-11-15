# Claims-Based Authorization Refactoring - Master Plan

## Executive Summary

This master plan documents the complete strategy for migrating RTUB from scattered boolean authorization checks to a unified, claims-based, policy-driven authorization model. The migration will happen in 5 phases, preserving backward compatibility and improving maintainability.

---

## Problem Statement

### Current Issues
- **Scattered Logic**: 141 authorization checks across 20+ files
- **Inconsistent Patterns**: Mix of attributes, AuthorizeView, and programmatic checks
- **Hard to Maintain**: Changes require updates in multiple places
- **No Dynamic Control**: Cannot change permissions without code deployment
- **Testing Complexity**: Need to test many combinations of roles and categories

### Current State
- 3 ASP.NET Identity Roles (Admin, Member, Owner)
- 7 Member Categories (Leitao through Fundador)
- 14 Organizational Positions
- 53 category-based checks
- 88 role-based checks
- Extension methods providing helpers (ApplicationUserExtensions.cs)

---

## Solution Overview

### Goals
1. **Centralize** authorization logic using claims and policies
2. **Simplify** Razor pages by moving checks to policies
3. **Enable** dynamic page permissions controlled by Owner
4. **Maintain** backward compatibility during migration
5. **Improve** testability and maintainability

### Approach
- Use ASP.NET Core claims-based authorization
- Define named authorization policies
- Issue claims at login based on user data
- Create helper extensions for ClaimsPrincipal
- Build Owner-only admin UI for page permissions

---

## Implementation Phases

### Phase 1: Analysis ✅ COMPLETE

**Objective**: Document current state

**Deliverables**:
- [x] `/docs/auth-model-analysis.md` - Complete analysis of current authorization model
  - All 53 category checks documented
  - All 88 role checks documented
  - Usage patterns identified
  - Decision matrix created

**Key Findings**:
- MainLayout.razor has most complex authorization (12 checks)
- "Caloiro Admin" pattern requires special handling
- Positions stored but not used for authorization
- No unified policy system

### Phase 2: Design ✅ COMPLETE

**Objective**: Design claims-based model

**Deliverables**:
- [x] `/docs/claims-model-design.md` - Complete design for claims system
  - Claim type constants defined
  - 15 authorization policies designed
  - ClaimsPrincipal extension methods designed
  - UserClaimsFactory designed
  - Migration strategy defined

**Key Design Elements**:
- Standard claim types: `rtub:category`, `rtub:position`, `rtub:primary_category`
- Category hierarchy support (Leitao < Caloiro < Tuno < Veterano < Tunossauro)
- Named policies: RequireTunoOrHigher, CanBeMentor, IsNotCaloiroAdmin, etc.
- Backward compatibility via obsolete attributes on old methods

### Phase 3: Implementation (Not Started)

**Objective**: Implement claims infrastructure

**Tasks**:
1. **Create Constants Classes**
   - [ ] `ClaimTypes` - Claim type constants
   - [ ] `CategoryClaims` - Category value constants with hierarchy
   - [ ] `PositionClaims` - Position value constants
   - [ ] `AuthorizationPolicies` - Policy name constants

2. **Create Claims Factory**
   - [ ] `UserClaimsFactory` - Convert ApplicationUser to claims
   - [ ] Unit tests for claims factory

3. **Create Extensions**
   - [ ] `ClaimsPrincipalExtensions` - Helper methods for claims
   - [ ] Unit tests for extensions

4. **Integrate with Sign-In**
   - [ ] Update Program.cs login endpoint to issue claims
   - [ ] Use SignInWithClaimsAsync instead of SignInAsync

5. **Configure Policies**
   - [ ] `AuthorizationPolicyConfiguration` - Configure all 15 policies
   - [ ] Register in Program.cs

6. **Testing**
   - [ ] Unit tests for all extensions
   - [ ] Unit tests for policy configuration
   - [ ] Integration tests for claims issuance

**Estimated Effort**: 2-3 days

### Phase 4: Migration (Not Started)

**Objective**: Replace old checks with claims/policies

**Tasks**:
1. **Update ApplicationUserExtensions**
   - [ ] Mark methods as `[Obsolete]` with migration guidance
   - [ ] Keep methods functional for backward compatibility

2. **Replace Direct Checks** (file by file):
   - [ ] MainLayout.razor (12 checks)
   - [ ] Public/Roles.razor (48 checks)
   - [ ] Member/Events.razor (15 checks)
   - [ ] Member/Rehearsals.razor (10 checks)
   - [ ] Member/Profile.razor (8 checks)
   - [ ] Member/Members.razor (8 checks)
   - [ ] Member/Requests.razor (8 checks)
   - [ ] Member/Finance.razor (7 checks)
   - [ ] Member/Hierarchy.razor (4 checks)
   - [ ] Member/Slideshows.razor (4 checks)
   - [ ] And 10 more files...

3. **Refactoring Patterns**:
   - Replace `@if (user.IsLeitao())` with `<AuthorizeView Policy="@Policies.LeitaoOnly">`
   - Replace `user.IsTunoOrHigher()` with `authState.User.IsAtLeastCategory(CategoryClaims.Tuno)`
   - Add `[Authorize(Policy = "...")]` attributes where appropriate

4. **Testing**:
   - [ ] Run full test suite after each file
   - [ ] Add tests for new policies
   - [ ] Verify UI behaves identically
   - [ ] Test all authorization scenarios

**Estimated Effort**: 3-4 days

### Phase 5: Page Permissions Admin (Not Started)

**Objective**: Enable dynamic page permissions

**Deliverables**:
- [x] `/docs/page-permissions-design.md` - Complete design for permissions admin

**Tasks**:
1. **Data Layer**
   - [ ] Create `PagePermission` entity
   - [ ] Create migration for `PagePermissions` table
   - [ ] Add to ApplicationDbContext
   - [ ] Seed initial permissions

2. **Service Layer**
   - [ ] Create `IPagePermissionService` interface
   - [ ] Implement `PagePermissionService`
   - [ ] Implement route discovery
   - [ ] Add memory caching
   - [ ] Add audit logging

3. **Middleware**
   - [ ] Create `PagePermissionMiddleware`
   - [ ] Register in Program.cs
   - [ ] Create access denied page
   - [ ] Test middleware

4. **Admin UI**
   - [ ] Create `/owner/page-permissions` page
   - [ ] Implement permission listing
   - [ ] Implement permission editing
   - [ ] Add route discovery/sync
   - [ ] Add search and filtering

5. **Testing**:
   - [ ] Unit tests for service
   - [ ] Integration tests for middleware
   - [ ] E2E tests for admin UI
   - [ ] Security tests (Owner lockout prevention)

**Estimated Effort**: 3-4 days

---

## Migration Strategy

### Backward Compatibility Approach

1. **Keep Old Methods**: ApplicationUserExtensions methods remain functional
2. **Add Obsolete Attributes**: Guide developers to new methods
3. **Gradual Migration**: Replace checks file by file
4. **Test Continuously**: Run tests after each change
5. **Document Changes**: Update developer documentation

### Example Migration

**Before**:
```razor
@if (currentUser != null && currentUser.IsTuno())
{
    <button>Create Event</button>
}
```

**After (Policy)**:
```razor
<AuthorizeView Policy="@AuthorizationPolicies.RequireTunoOrHigher">
    <button>Create Event</button>
</AuthorizeView>
```

**After (Claims)**:
```razor
@if (authState.User.IsAtLeastCategory(CategoryClaims.Tuno))
{
    <button>Create Event</button>
}
```

### Rollback Plan

If issues arise:
1. Old methods still work (not deleted)
2. Can revert file-by-file changes
3. Claims are additive (don't break existing auth)
4. Can disable page permissions middleware

---

## Testing Strategy

### Unit Tests
- Claims factory tests (20+ tests)
- Extension method tests (30+ tests)
- Policy configuration tests (15+ tests)
- Service tests (25+ tests)

### Integration Tests
- Claims issuance tests (10+ tests)
- Policy enforcement tests (20+ tests)
- Middleware tests (15+ tests)
- Page permission tests (20+ tests)

### E2E Tests
- Login and verify claims issued
- Test each policy with different users
- Test page permissions admin UI
- Test access denied scenarios

### Manual Testing Checklist
- [ ] Login as Owner - verify all access
- [ ] Login as Admin - verify restricted access
- [ ] Login as Leitao - verify limited access
- [ ] Login as Caloiro Admin - verify special restrictions
- [ ] Test each page with different user types
- [ ] Test page permissions admin UI
- [ ] Verify audit logging

---

## Risk Management

### Identified Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Break existing auth | High | Medium | Gradual migration, extensive testing |
| Performance degradation | Medium | Low | Caching, optimize claim checks |
| Owner lockout | High | Low | Hard-code Owner always has access |
| Incomplete migration | Medium | Medium | Systematic file-by-file approach |
| Complex testing | Medium | High | Clear test strategy, automate tests |
| User confusion | Low | Low | No UI changes during Phases 3-4 |

### Mitigation Strategies

1. **Preserve Backward Compatibility**: Keep old methods working
2. **Incremental Approach**: Migrate one file at a time
3. **Comprehensive Testing**: 100+ new tests across all phases
4. **Code Review**: Review all changes before merging
5. **Documentation**: Clear migration guides for developers
6. **Rollback Plan**: Can revert changes if needed

---

## Success Criteria

### Phase 3 Success
- [ ] All constants classes created
- [ ] Claims issued at login
- [ ] ClaimsPrincipal extensions working
- [ ] All 15 policies configured
- [ ] 100+ unit tests passing

### Phase 4 Success
- [ ] All files migrated to claims/policies
- [ ] Old methods marked obsolete
- [ ] All existing tests still pass
- [ ] New policy tests pass
- [ ] No authorization regressions

### Phase 5 Success
- [ ] Page permissions admin UI functional
- [ ] Owner can configure page access
- [ ] Middleware enforces permissions
- [ ] Route discovery works
- [ ] Audit logging works

### Overall Success
- [ ] Authorization logic centralized
- [ ] Code more maintainable
- [ ] Tests more comprehensive
- [ ] Dynamic permissions working
- [ ] No security vulnerabilities
- [ ] Performance acceptable
- [ ] Documentation complete

---

## Timeline

### Estimated Duration: 8-11 days

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 1: Analysis | DONE | None |
| Phase 2: Design | DONE | Phase 1 |
| Phase 3: Implementation | 2-3 days | Phase 2 |
| Phase 4: Migration | 3-4 days | Phase 3 |
| Phase 5: Page Permissions | 3-4 days | Phase 4 |

### Milestones

1. **Planning Complete** ✅ - Analysis and design done
2. **Claims Infrastructure** - Phase 3 complete, claims working
3. **Migration Complete** - Phase 4 complete, all files updated
4. **Dynamic Permissions** - Phase 5 complete, Owner UI working
5. **Production Ready** - All tests pass, documentation complete

---

## Documentation Index

1. **auth-model-analysis.md** (15KB)
   - Current state analysis
   - All 141 checks documented
   - Usage patterns identified
   - Decision matrix

2. **claims-model-design.md** (22KB)
   - Claim types design
   - Authorization policies design
   - Extension methods design
   - Migration strategy

3. **page-permissions-design.md** (27KB)
   - PagePermission entity design
   - Service layer design
   - Middleware design
   - Admin UI design

4. **master-plan.md** (This Document)
   - Complete overview
   - Phase breakdown
   - Timeline and risks
   - Success criteria

---

## Next Steps

### Immediate (Phase 3)
1. Review and approve design documents
2. Create branch for Phase 3 work
3. Implement constants classes
4. Implement claims factory
5. Implement extensions
6. Write unit tests

### After Phase 3
1. Update Program.cs for claims issuance
2. Configure authorization policies
3. Test claims are issued correctly
4. Begin Phase 4 migration

### After Phase 4
1. Verify all migrations complete
2. Mark old methods obsolete
3. Begin Phase 5 implementation
4. Build page permissions admin

---

## Conclusion

This comprehensive plan provides:
- **Clear Vision**: Move to claims-based authorization
- **Detailed Design**: All components designed
- **Risk Management**: Risks identified and mitigated
- **Backward Compatibility**: No breaking changes
- **Testing Strategy**: Comprehensive test coverage
- **Timeline**: Realistic 8-11 day estimate

The migration will significantly improve:
- **Maintainability**: Centralized authorization logic
- **Testability**: Clear policies to test
- **Flexibility**: Dynamic permissions system
- **Security**: Consistent enforcement
- **Developer Experience**: Clear patterns to follow

---

## Appendices

### A. File Change Summary

Files to modify in Phase 3:
- src/RTUB.Core/Constants/ClaimTypes.cs (new)
- src/RTUB.Core/Constants/CategoryClaims.cs (new)
- src/RTUB.Core/Constants/PositionClaims.cs (new)
- src/RTUB.Core/Constants/AuthorizationPolicies.cs (new)
- src/RTUB.Core/Extensions/ClaimsPrincipalExtensions.cs (new)
- src/RTUB.Application/Services/UserClaimsFactory.cs (new)
- src/RTUB.Web/Configuration/AuthorizationPolicyConfiguration.cs (new)
- src/RTUB.Web/Program.cs (modify)

Files to modify in Phase 4 (20+ files):
- src/RTUB.Web/Shared/MainLayout.razor
- src/RTUB.Web/Pages/Public/Roles.razor
- src/RTUB.Web/Pages/Member/Events.razor
- src/RTUB.Web/Pages/Member/Rehearsals.razor
- ... and 16 more files

Files to create in Phase 5:
- src/RTUB.Core/Entities/PagePermission.cs (new)
- src/RTUB.Application/Interfaces/IPagePermissionService.cs (new)
- src/RTUB.Application/Services/PagePermissionService.cs (new)
- src/RTUB.Web/Middleware/PagePermissionMiddleware.cs (new)
- src/RTUB.Web/Pages/Owner/PagePermissions.razor (new)

### B. Policy Reference

Quick reference for all 15 policies:

1. RequireAdmin - Admin role required
2. RequireOwner - Owner role required
3. RequireAdminOrOwner - Admin OR Owner role
4. RequireCaloiroOrHigher - Caloiro+ category
5. RequireTunoOrHigher - Tuno+ category
6. RequireVeteranoOrHigher - Veterano+ category
7. ExcludeLeitao - Not only Leitao
8. LeitaoOnly - Only Leitao
9. CanBeMentor - Tuno+ (mentor eligible)
10. CanHoldPresidentPosition - Tuno+ (position eligible)
11. CanManageEvents - Admin or Owner
12. CanManageFinance - Admin or Owner
13. CanManageRoles - Admin
14. CanViewFullFinance - Not only Leitao
15. IsNotCaloiroAdmin - Admin but not Caloiro
16. CanManageSlideshows - Admin/Owner but not Caloiro Admin

### C. Testing Checklist

Comprehensive testing checklist for all phases:

**Phase 3 Tests** (100+ tests):
- [ ] ClaimTypes constants exist
- [ ] CategoryClaims hierarchy correct
- [ ] PositionClaims complete
- [ ] UserClaimsFactory creates correct claims
- [ ] ClaimsPrincipal IsCategory works
- [ ] ClaimsPrincipal IsAtLeastCategory works
- [ ] ClaimsPrincipal HasPosition works
- [ ] Category hierarchy comparisons work
- [ ] Policy configuration valid
- [ ] Claims issued at login

**Phase 4 Tests** (50+ tests):
- [ ] All migrated files work correctly
- [ ] No authorization regressions
- [ ] Policy enforcement works
- [ ] AuthorizeView policies work
- [ ] Programmatic policy checks work

**Phase 5 Tests** (40+ tests):
- [ ] PagePermission CRUD works
- [ ] Permission checks correct
- [ ] Middleware enforces permissions
- [ ] Owner cannot be locked out
- [ ] Admin UI functional
- [ ] Route discovery works
- [ ] Caching works
- [ ] Audit logging works
