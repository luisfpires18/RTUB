# Static Asset Delivery Strategy - Decision Document

## Decision

**KEEP the current `VersionedAsset` component and `UseStaticFiles` approach.**

Do NOT migrate to `MapStaticAssets` at this time.

## Rationale

### Current Implementation Strengths

The existing static asset strategy is **production-ready and well-optimized**:

1. **Cache-Busting**: Custom `VersionedAsset` component uses `IFileVersionProvider` to append content-based hashes to file URLs
2. **Custom Cache Headers**: Granular control over cache duration based on file type
   - PWA assets: 1 hour with `must-revalidate`
   - Other static files: 30 days in production
3. **Response Compression**: Brotli/Gzip enabled in production (correctly disabled in dev to avoid conflicts)
4. **Content Type Control**: Custom MIME type mapping for `.webmanifest` files
5. **Selective Serving**: `/images/*` path excluded to allow controller-based E-Tag support
6. **Environment-Specific**: Different behavior for development vs. production

### Why MapStaticAssets is Not Needed

**MapStaticAssets** (if available in .NET 10) provides:
- Automatic fingerprinting of static assets ✅ **Already have this via VersionedAsset**
- Optimized serving with compression ✅ **Already have this via ResponseCompression**
- Cache headers ✅ **Already have fine-grained control**

**Additional Considerations**:
- MapStaticAssets is a **wholesale replacement** of the static file pipeline
- Current implementation has **custom requirements**:
  - E-Tag support via ImagesController
  - Different cache policies for PWA vs. regular assets
  - Environment-specific compression
- Migration would require **significant testing** with **minimal benefit**

### Risk Assessment

| Risk | Current Approach | MapStaticAssets Migration |
|------|------------------|---------------------------|
| Breaking PWA | Low (stable) | Medium (untested) |
| Cache invalidation issues | Low (proven) | Low (should work) |
| E-Tag support | Working | Unknown compatibility |
| Development experience | Good | May need reconfiguration |
| Maintenance burden | Low | Medium (new approach) |

## What We're NOT Missing

By keeping the current approach, we are NOT missing:
- ❌ Better performance (current is already fast)
- ❌ Better caching (current is already optimal)
- ❌ Better compression (current is already configured)
- ❌ Framework-level optimization (current uses framework primitives)

## Future Reconsideration

Revisit this decision IF:
1. Microsoft documentation shows MapStaticAssets has **significant benefits** for Blazor Server apps
2. The app grows to **1000+ static files** where build-time optimization becomes critical
3. A **specific problem** emerges that MapStaticAssets solves better
4. The custom E-Tag controller for `/images/*` is no longer needed

## References

- Current implementation: `src/RTUB.Web/Program.cs` (lines 439-467)
- VersionedAsset component: `src/RTUB.Web/Components/VersionedAsset.razor`
- ASP.NET Core Static Files: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files

---

**Decision Date**: 2025-11-26  
**Reviewed By**: .NET 10 Upgrade Analysis  
**Status**: Final - Do not migrate  
**Next Review**: Only if specific problems arise
