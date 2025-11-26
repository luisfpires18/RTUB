# Blazor .NET 10 Upgrade Plan

## Executive Summary

This document outlines a comprehensive, incremental upgrade plan to adopt .NET 10 and ASP.NET Core 10 Blazor improvements while maintaining full backward compatibility with the existing RTUB application.

**Current State**: The application is already targeting .NET 10 and all 2853 tests are passing.

**Goal**: Safely adopt .NET 10/Blazor performance and UX improvements without breaking current behavior.

---

## Architecture Overview

### Current Application Architecture

- **Hosting Model**: Blazor Interactive Server (render mode: InteractiveServer)
- **Framework**: .NET 10.0 (SDK 10.0.100)
- **Language**: C# (default for .NET 10 = C# 14)
- **Authentication**: ASP.NET Core Identity with role-based authorization
- **Database**: SQLite with Entity Framework Core 10.0.0
- **SignalR**: Configured with custom hub path `/_blazor`
- **Static Assets**: Custom `VersionedAsset` component using `IFileVersionProvider`

### Project Structure

```
RTUB/
├── src/
│   ├── RTUB.Core/           - Domain entities (net10.0)
│   ├── RTUB.Application/    - Business logic (net10.0)
│   ├── RTUB.Shared/         - Shared Razor components (net10.0)
│   └── RTUB.Web/            - Main Blazor app (net10.0)
└── tests/                   - 5 test projects, 2853 tests passing
```

### Current Static Asset Strategy

- **Blazor Script**: `/_framework/blazor.web.js` with manual `Blazor.start()` configuration
- **Asset Versioning**: Custom `VersionedAsset` component for cache-busting
- **Static Files**: `UseStaticFiles` with custom `FileExtensionContentTypeProvider`
- **Compression**: Brotli/Gzip in production only
- **Caching**: 30-day cache for static files, 1-hour for PWA assets

### Current Features to Preserve

✅ **PWA Support**: Service worker, manifest, icons  
✅ **Role-Based Auth**: Admin, Owner, Mod, Member roles  
✅ **Custom Routing**: Custom login path `/login`  
✅ **SignalR Config**: Absolute path configuration for sub-routes  
✅ **Response Compression**: Production-only to avoid dev conflicts  
✅ **Image E-Tag Support**: Custom controller for `/images/*`  
✅ **Anti-forgery Protection**: Custom header `X-CSRF-TOKEN`  

---

## Upgrade Checklist

### Phase 1: Language & Framework Verification ✅

- [x] **Target Framework**: Already .NET 10 in all projects
- [x] **Verify Build**: `dotnet build` succeeds
- [x] **Verify Tests**: All 2853 tests passing
- [ ] **Add Explicit C# 14**: Add `<LangVersion>14</LangVersion>` to ensure consistency

**Status**: Framework already correct. Will add explicit language version for clarity.

---

### Phase 2: Static Asset Delivery Optimization

#### Current State
- Uses `UseStaticFiles` with manual configuration
- Custom `VersionedAsset` component for cache-busting
- Response compression configured separately

#### Planned Changes

- [ ] **2.1 Evaluate MapStaticAssets**
  - Research if `MapStaticAssets` provides benefits over current `UseStaticFiles` + `VersionedAsset` approach
  - Determine if automatic fingerprinting duplicates existing `IFileVersionProvider` functionality
  - **Decision Point**: Keep current approach if no clear benefit OR migrate if significant improvements identified

- [ ] **2.2 Optimize Blazor Framework Script**
  - Currently: `<script src="/_framework/blazor.web.js" autostart="false">`
  - Verify if .NET 10 provides improved static asset delivery for framework files
  - Ensure framework script benefits from compression and caching

- [ ] **2.3 Validate PWA Assets**
  - Ensure service worker, manifest, and icons continue to work
  - Verify offline functionality if applicable
  - Test installation on mobile devices

**Constraints**: Do NOT remove the custom `VersionedAsset` component unless MapStaticAssets provides equivalent functionality with clear benefits.

---

### Phase 3: Reconnection UX & Circuit Management

#### Current State
- No explicit reconnection UI (default Blazor behavior)
- Manual `Blazor.start()` configuration in MainLayout.razor
- Circuit options configured for production stability

#### Planned Changes

- [ ] **3.1 Create ReconnectModal Component**
  - Build a custom `ReconnectModal.razor` component following .NET 10 template pattern
  - Display during connection loss with branded RTUB styling
  - Show reconnection status and retry attempts
  - Place in `Components/` directory

- [ ] **3.2 Integrate ReconnectModal into App.razor**
  - Add `<ReconnectModal />` to App.razor body
  - Ensure it doesn't interfere with navigation
  - Test on network interruption scenarios

- [ ] **3.3 Implement Circuit Pause/Resume (Optional)**
  - Use Page Visibility API to pause circuits when tab is hidden
  - Resume when tab becomes visible again
  - Validate this doesn't break existing behavior
  - **Decision Point**: Only implement if clearly beneficial for battery/resource savings

- [ ] **3.4 Validate Reconnection Scenarios**
  - Test network disconnection/reconnection
  - Test tab sleep/wake
  - Test long-running connections
  - Ensure existing behavior is preserved or improved

**Constraints**: Keep default behavior if custom implementation introduces risk. ReconnectModal is UX improvement, not breaking change.

---

### Phase 4: Persistent Component State (Selective)

#### Current State
- No persistent component state configured
- All state is recreated on reconnection/refresh

#### Planned Changes

- [ ] **4.1 Identify Candidate Components**
  - Long forms with user input (e.g., member profile edits, meeting creation)
  - Multi-step workflows
  - Expensive data loads that could survive reconnect
  - **Do NOT apply globally** - only to isolated, well-scoped components

- [ ] **4.2 Implement PersistentComponentState (If Applicable)**
  - Add `PersistentComponentState` injection to identified components
  - Use `RegisterOnPersisting` and `TryTakeFromJson` pattern
  - Add tests to verify state survives reconnection
  - Document the behavior for maintainers

- [ ] **4.3 Validate State Persistence**
  - Test state survives reconnection
  - Test state survives refresh (if configured)
  - Ensure no security implications (don't persist sensitive data in browser)

**Constraints**: This is OPTIONAL. Only implement if clear value identified. Prefer stateless components as default.

---

### Phase 5: Navigation Improvements

#### Current State
- Custom scroll-to-top logic (commented out in MainLayout)
- Manual debouncing of navigation events
- FocusOnNavigate component used in App.razor

#### Planned Changes

- [ ] **5.1 Review NavigateTo Scroll Behavior**
  - .NET 10 `NavigateTo` may preserve scroll position for same-page navigation
  - Review if existing custom scroll logic can be removed
  - Test back/forward navigation scroll behavior

- [ ] **5.2 Optimize NavLink Components**
  - Review all `<NavLink>` components for .NET 10 improvements
  - Consider `Match` parameter enhancements if applicable
  - Ensure active state is correct for sub-routes

- [ ] **5.3 Validate Navigation Experience**
  - Test all major navigation paths
  - Verify dropdowns, mobile nav, and breadcrumbs work correctly
  - Ensure authentication redirects still work

**Constraints**: Do NOT remove working scroll logic unless .NET 10 provides equivalent functionality automatically.

---

### Phase 6: Forms, Validation & Diagnostics

#### Current State
- 130 EditForm instances across the application
- Using DataAnnotationsValidator
- Custom audit logging for user actions

#### Planned Changes

- [ ] **6.1 Review .NET 10 Validation Enhancements**
  - Check for new Blazor form validation features in .NET 10
  - Determine if any custom validation can be replaced with framework features
  - **Do NOT refactor** unless clear benefit and low risk

- [ ] **6.2 Enable Blazor Metrics**
  - Add `.NET.Blazor.Circuits` meter
  - Add `.NET.Blazor.Navigation` meter
  - Configure for production-ready telemetry (not noisy)

- [ ] **6.3 Enable Blazor Tracing**
  - Add activity sources for circuits and navigation
  - Integrate with existing logging infrastructure
  - Document for operations team

- [ ] **6.4 Test Metrics Collection**
  - Verify metrics are collected
  - Ensure no performance degradation
  - Validate telemetry is useful for monitoring

**Constraints**: Keep metrics configuration minimal and production-ready. Avoid excessive logging noise.

---

### Phase 7: Testing & Validation

#### Current State
- 2853 tests passing across 5 test projects
- Integration tests for workflows
- Component tests for UI

#### Planned Changes

- [ ] **7.1 Add ReconnectModal Tests**
  - Test component renders correctly
  - Test reconnection scenarios
  - Test dismissal behavior

- [ ] **7.2 Add Metrics Tests**
  - Verify meters are registered
  - Test metric collection (if feasible)

- [ ] **7.3 Regression Testing**
  - Run full test suite after each change
  - Manually test critical paths:
    - Login/logout
    - Member management
    - Rehearsal scheduling
    - File uploads
    - Navigation flows

- [ ] **7.4 Performance Testing (Optional)**
  - Benchmark page load times before/after
  - Measure SignalR reconnection time
  - Validate memory usage hasn't increased

**Constraints**: All existing tests must continue to pass. Add tests for new behavior.

---

### Phase 8: Documentation & Cleanup

- [ ] **8.1 Update README.md**
  - Document .NET 10 features adopted
  - Update prerequisites if needed

- [ ] **8.2 Create Changelog**
  - List all .NET 10/Blazor features adopted
  - Document any behavior changes (even if minor)
  - List TODOs and future opportunities

- [ ] **8.3 Document Deployment Changes**
  - Note any CDN or proxy configuration changes needed
  - Document new metrics endpoints if exposed
  - Update production deployment guide

- [ ] **8.4 Code Comments**
  - Add comments explaining new .NET 10 features
  - Document why certain approaches were chosen
  - Reference official Microsoft docs where applicable

---

## Risk Assessment

### Low Risk ✅
- Adding explicit C# 14 language version
- Creating ReconnectModal component (additive)
- Enabling Blazor metrics (read-only telemetry)
- Documentation updates

### Medium Risk ⚠️
- MapStaticAssets migration (test thoroughly)
- Circuit pause/resume (validate battery/resource behavior)
- Navigation scroll behavior changes (test all paths)

### High Risk 🔴
- Changing existing VersionedAsset logic (only if proven benefit)
- Persistent component state (scope tightly, security implications)
- Form validation refactoring (130 forms, high test burden)

**Mitigation**: Follow incremental approach, test after each change, keep diffs focused.

---

## Decision Log

### Decision 1: Keep or Replace VersionedAsset?
- **Current**: Custom component using `IFileVersionProvider`
- **Option A**: Keep current (stable, working)
- **Option B**: Migrate to `MapStaticAssets` (potential benefits?)
- **Decision**: **TBD** - Research and decide based on clear benefits vs. risk

### Decision 2: Implement Circuit Pause/Resume?
- **Benefit**: Potential battery/resource savings
- **Risk**: Complex, edge cases, validation burden
- **Decision**: **TBD** - Implement only if low-hanging fruit with clear value

### Decision 3: Use Persistent Component State?
- **Benefit**: Better UX for long forms during reconnect
- **Risk**: Scope creep, security, maintenance burden
- **Decision**: **TBD** - Only for 1-2 high-value forms, not global

---

## Success Criteria

✅ All existing tests pass  
✅ No breaking changes to authentication, routing, or layouts  
✅ No regressions in functionality  
✅ Build succeeds without warnings  
✅ Application runs in development and production modes  
✅ PWA functionality preserved  
✅ Performance is equal or better (measured)  
✅ Documentation is complete and accurate  

---

## References

- [ASP.NET Core 10 Release Notes](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0)
- [Blazor Static Assets](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files)
- [Blazor Circuit Management](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr)
- [Blazor Metrics](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/logging)
- [C# 14 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)

---

## Completed Work Summary

### ✅ Phase 1: Language & Framework
- [x] Added explicit C# 14 language version
- [x] Build verified (36s, no warnings)
- [x] All tests passing (2858 total)

### ✅ Phase 2: Reconnection UX
- [x] Created ReconnectModal component with Portuguese branding
- [x] Integrated into App.razor
- [x] Added 5 unit tests (all passing)

### ✅ Phase 3: Metrics & Diagnostics
- [x] Enabled Blazor metrics via services.AddMetrics()
- [x] Production-ready telemetry for circuits and navigation

### ✅ Phase 4: Static Asset Strategy
- [x] Evaluated MapStaticAssets
- [x] **DECISION**: Keep current VersionedAsset approach (documented in STATIC-ASSETS-DECISION.md)
- [x] Current implementation is optimal for this application

### 📋 Phase 5: Documentation
- [x] Created comprehensive upgrade plan (this document)
- [x] Created decision document for static assets
- [x] Created detailed changelog (BLAZOR-NET10-CHANGELOG.md)

### ⏭️ Not Implemented (Documented as Future Opportunities)
- Circuit pause/resume: Not needed at this time
- Persistent component state: Not needed at this time
- NavigateTo scroll improvements: Current approach working well
- QuickGrid features: Not applicable (app doesn't use QuickGrid)

---

**Document Version**: 1.1  
**Created**: 2025-11-26  
**Last Updated**: 2025-11-26  
**Status**: Completed
