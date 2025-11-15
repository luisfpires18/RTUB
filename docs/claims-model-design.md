# Claims-Based Authorization Model Design

## Overview

This document defines the claims-based authorization model that will replace the scattered boolean checks across the RTUB application. The design maintains backward compatibility while providing a cleaner, more maintainable authorization system.

---

## 1. Claim Types

### 1.1 Standard Claim Type Constants

```csharp
namespace RTUB.Core.Constants;

/// <summary>
/// Defines standard claim types used throughout the application
/// </summary>
public static class ClaimTypes
{
    /// <summary>
    /// Member category claim (LEITAO, CALOIRO, TUNO, VETERANO, TUNOSSAURO, etc.)
    /// Can have multiple values for users with multiple categories
    /// </summary>
    public const string Category = "rtub:category";
    
    /// <summary>
    /// Organizational position claim (MAGISTER, TESOUREIRO, etc.)
    /// Can have multiple values for users with multiple positions
    /// </summary>
    public const string Position = "rtub:position";
    
    /// <summary>
    /// Primary category - the highest/most relevant category for the user
    /// Used for display and simple authorization decisions
    /// </summary>
    public const string PrimaryCategory = "rtub:primary_category";
    
    /// <summary>
    /// Years as Tuno - numeric value
    /// </summary>
    public const string YearsAsTuno = "rtub:years_as_tuno";
    
    /// <summary>
    /// Whether user is a founder (1991)
    /// </summary>
    public const string IsFounder = "rtub:is_founder";
}
```

### 1.2 Claim Value Constants

```csharp
namespace RTUB.Core.Constants;

/// <summary>
/// Defines standard claim values for categories
/// </summary>
public static class CategoryClaims
{
    public const string Leitao = "LEITAO";
    public const string Caloiro = "CALOIRO";
    public const string Tuno = "TUNO";
    public const string Veterano = "VETERANO";
    public const string Tunossauro = "TUNOSSAURO";
    public const string TunoHonorario = "TUNO_HONORARIO";
    public const string Fundador = "FUNDADOR";
    
    /// <summary>
    /// Defines category hierarchy for comparison operations
    /// Lower index = lower rank
    /// </summary>
    public static readonly string[] Hierarchy = new[]
    {
        Leitao,      // 0 - Probationary
        Caloiro,     // 1 - First year
        Tuno,        // 2 - Full member
        Veterano,    // 3 - 2+ years
        Tunossauro   // 4 - 6+ years
    };
    
    /// <summary>
    /// Gets the hierarchy level for a category (higher = more senior)
    /// </summary>
    public static int GetLevel(string category)
    {
        var index = Array.IndexOf(Hierarchy, category?.ToUpperInvariant());
        return index >= 0 ? index : -1;
    }
}

/// <summary>
/// Defines standard claim values for positions
/// </summary>
public static class PositionClaims
{
    // Direção
    public const string Magister = "MAGISTER";
    public const string ViceMagister = "VICE_MAGISTER";
    public const string Secretario = "SECRETARIO";
    public const string PrimeiroTesoureiro = "PRIMEIRO_TESOUREIRO";
    public const string SegundoTesoureiro = "SEGUNDO_TESOUREIRO";
    
    // Mesa da Assembleia
    public const string PresidenteMesaAssembleia = "PRESIDENTE_MESA_ASSEMBLEIA";
    public const string PrimeiroSecretarioMesaAssembleia = "PRIMEIRO_SECRETARIO_MESA_ASSEMBLEIA";
    public const string SegundoSecretarioMesaAssembleia = "SEGUNDO_SECRETARIO_MESA_ASSEMBLEIA";
    
    // Conselho Fiscal
    public const string PresidenteConselhoFiscal = "PRESIDENTE_CONSELHO_FISCAL";
    public const string PrimeiroRelatorConselhoFiscal = "PRIMEIRO_RELATOR_CONSELHO_FISCAL";
    public const string SegundoRelatorConselhoFiscal = "SEGUNDO_RELATOR_CONSELHO_FISCAL";
    
    // Conselho de Veteranos
    public const string PresidenteConselhoVeteranos = "PRESIDENTE_CONSELHO_VETERANOS";
    
    // Outros
    public const string Ensaiador = "ENSAIADOR";
}
```

---

## 2. Authorization Policies

### 2.1 Policy Definitions

```csharp
namespace RTUB.Core.Constants;

/// <summary>
/// Defines named authorization policies
/// </summary>
public static class AuthorizationPolicies
{
    // Role-based policies (existing)
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireOwner = "RequireOwner";
    public const string RequireAdminOrOwner = "RequireAdminOrOwner";
    
    // Category-based policies
    public const string RequireCaloiroOrHigher = "RequireCaloiroOrHigher";
    public const string RequireTunoOrHigher = "RequireTunoOrHigher";
    public const string RequireVeteranoOrHigher = "RequireVeteranoOrHigher";
    public const string ExcludeLeitao = "ExcludeLeitao";
    public const string LeitaoOnly = "LeitaoOnly";
    
    // Composite policies
    public const string CanBeMentor = "CanBeMentor";
    public const string CanHoldPresidentPosition = "CanHoldPresidentPosition";
    public const string CanManageEvents = "CanManageEvents";
    public const string CanManageFinance = "CanManageFinance";
    public const string CanManageRoles = "CanManageRoles";
    public const string CanViewFullFinance = "CanViewFullFinance";
    
    // Special policies
    public const string IsNotCaloiroAdmin = "IsNotCaloiroAdmin"; // Admin but not Caloiro
    public const string CanManageSlideshows = "CanManageSlideshows"; // Admin/Owner but not Caloiro
}
```

### 2.2 Policy Configuration

```csharp
namespace RTUB.Web.Configuration;

public static class AuthorizationPolicyConfiguration
{
    public static void ConfigureAuthorizationPolicies(this AuthorizationOptions options)
    {
        // Role-based policies
        options.AddPolicy(AuthorizationPolicies.RequireAdmin, 
            policy => policy.RequireRole("Admin"));
        
        options.AddPolicy(AuthorizationPolicies.RequireOwner, 
            policy => policy.RequireRole("Owner"));
        
        options.AddPolicy(AuthorizationPolicies.RequireAdminOrOwner, 
            policy => policy.RequireRole("Admin", "Owner"));
        
        // Category-based policies
        options.AddPolicy(AuthorizationPolicies.RequireCaloiroOrHigher, 
            policy => policy.RequireAssertion(context =>
                context.User.IsAtLeastCategory(CategoryClaims.Caloiro)));
        
        options.AddPolicy(AuthorizationPolicies.RequireTunoOrHigher, 
            policy => policy.RequireAssertion(context =>
                context.User.IsAtLeastCategory(CategoryClaims.Tuno)));
        
        options.AddPolicy(AuthorizationPolicies.RequireVeteranoOrHigher, 
            policy => policy.RequireAssertion(context =>
                context.User.IsAtLeastCategory(CategoryClaims.Veterano)));
        
        options.AddPolicy(AuthorizationPolicies.ExcludeLeitao, 
            policy => policy.RequireAssertion(context =>
                !context.User.IsCategory(CategoryClaims.Leitao) || 
                 context.User.IsAtLeastCategory(CategoryClaims.Caloiro)));
        
        options.AddPolicy(AuthorizationPolicies.LeitaoOnly, 
            policy => policy.RequireAssertion(context =>
                context.User.IsCategory(CategoryClaims.Leitao) &&
                !context.User.IsAtLeastCategory(CategoryClaims.Caloiro)));
        
        // Composite policies
        options.AddPolicy(AuthorizationPolicies.CanBeMentor, 
            policy => policy.RequireAssertion(context =>
                context.User.IsAtLeastCategory(CategoryClaims.Tuno)));
        
        options.AddPolicy(AuthorizationPolicies.CanHoldPresidentPosition, 
            policy => policy.RequireAssertion(context =>
                context.User.IsAtLeastCategory(CategoryClaims.Tuno)));
        
        options.AddPolicy(AuthorizationPolicies.CanManageEvents, 
            policy => policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") || context.User.IsInRole("Owner")));
        
        options.AddPolicy(AuthorizationPolicies.CanManageFinance, 
            policy => policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") || context.User.IsInRole("Owner")));
        
        options.AddPolicy(AuthorizationPolicies.CanManageRoles, 
            policy => policy.RequireRole("Admin"));
        
        options.AddPolicy(AuthorizationPolicies.CanViewFullFinance, 
            policy => policy.RequireAssertion(context =>
                !context.User.IsCategory(CategoryClaims.Leitao) ||
                 context.User.IsAtLeastCategory(CategoryClaims.Caloiro)));
        
        // Special policies
        options.AddPolicy(AuthorizationPolicies.IsNotCaloiroAdmin, 
            policy => policy.RequireAssertion(context =>
                !context.User.IsCategory(CategoryClaims.Caloiro) || 
                !context.User.IsInRole("Admin") ||
                 context.User.IsInRole("Owner")));
        
        options.AddPolicy(AuthorizationPolicies.CanManageSlideshows, 
            policy => policy.RequireAssertion(context =>
                (context.User.IsInRole("Admin") || context.User.IsInRole("Owner")) &&
                (!context.User.IsCategory(CategoryClaims.Caloiro) || 
                  context.User.IsInRole("Owner"))));
    }
}
```

---

## 3. Claims Issuance

### 3.1 Claims Factory

```csharp
namespace RTUB.Application.Services;

/// <summary>
/// Factory for creating claims from ApplicationUser
/// </summary>
public class UserClaimsFactory
{
    public static IEnumerable<Claim> CreateClaims(ApplicationUser user)
    {
        var claims = new List<Claim>();
        
        // Category claims - add all categories as separate claims
        foreach (var category in user.Categories)
        {
            claims.Add(new Claim(ClaimTypes.Category, MapCategoryToClaim(category)));
        }
        
        // Primary category - the highest category
        var primaryCategory = GetPrimaryCategory(user.Categories);
        if (primaryCategory != null)
        {
            claims.Add(new Claim(ClaimTypes.PrimaryCategory, primaryCategory));
        }
        
        // Position claims - add all positions as separate claims
        foreach (var position in user.Positions)
        {
            claims.Add(new Claim(ClaimTypes.Position, MapPositionToClaim(position)));
        }
        
        // Years as Tuno
        var yearsAsTuno = user.GetYearsAsTuno();
        if (yearsAsTuno.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.YearsAsTuno, yearsAsTuno.Value.ToString()));
        }
        
        // Founder flag
        if (user.IsFundador())
        {
            claims.Add(new Claim(ClaimTypes.IsFounder, "true"));
        }
        
        return claims;
    }
    
    private static string MapCategoryToClaim(MemberCategory category)
    {
        return category switch
        {
            MemberCategory.Leitao => CategoryClaims.Leitao,
            MemberCategory.Caloiro => CategoryClaims.Caloiro,
            MemberCategory.Tuno => CategoryClaims.Tuno,
            MemberCategory.Veterano => CategoryClaims.Veterano,
            MemberCategory.Tunossauro => CategoryClaims.Tunossauro,
            MemberCategory.TunoHonorario => CategoryClaims.TunoHonorario,
            MemberCategory.Fundador => CategoryClaims.Fundador,
            _ => category.ToString().ToUpperInvariant()
        };
    }
    
    private static string MapPositionToClaim(Position position)
    {
        return position switch
        {
            Position.Magister => PositionClaims.Magister,
            Position.ViceMagister => PositionClaims.ViceMagister,
            Position.Secretario => PositionClaims.Secretario,
            Position.PrimeiroTesoureiro => PositionClaims.PrimeiroTesoureiro,
            Position.SegundoTesoureiro => PositionClaims.SegundoTesoureiro,
            Position.PresidenteMesaAssembleia => PositionClaims.PresidenteMesaAssembleia,
            Position.PrimeiroSecretarioMesaAssembleia => PositionClaims.PrimeiroSecretarioMesaAssembleia,
            Position.SegundoSecretarioMesaAssembleia => PositionClaims.SegundoSecretarioMesaAssembleia,
            Position.PresidenteConselhoFiscal => PositionClaims.PresidenteConselhoFiscal,
            Position.PrimeiroRelatorConselhoFiscal => PositionClaims.PrimeiroRelatorConselhoFiscal,
            Position.SegundoRelatorConselhoFiscal => PositionClaims.SegundoRelatorConselhoFiscal,
            Position.PresidenteConselhoVeteranos => PositionClaims.PresidenteConselhoVeteranos,
            Position.Ensaiador => PositionClaims.Ensaiador,
            _ => position.ToString().ToUpperInvariant()
        };
    }
    
    private static string? GetPrimaryCategory(List<MemberCategory> categories)
    {
        // Priority order: Tunossauro > Veterano > Tuno > Caloiro > Leitao
        // Fundador and TunoHonorario are special and not part of hierarchy
        if (categories.Contains(MemberCategory.Tunossauro))
            return CategoryClaims.Tunossauro;
        if (categories.Contains(MemberCategory.Veterano))
            return CategoryClaims.Veterano;
        if (categories.Contains(MemberCategory.Tuno))
            return CategoryClaims.Tuno;
        if (categories.Contains(MemberCategory.Caloiro))
            return CategoryClaims.Caloiro;
        if (categories.Contains(MemberCategory.Leitao))
            return CategoryClaims.Leitao;
        if (categories.Contains(MemberCategory.TunoHonorario))
            return CategoryClaims.TunoHonorario;
        if (categories.Contains(MemberCategory.Fundador))
            return CategoryClaims.Fundador;
        
        return null;
    }
}
```

### 3.2 Claims Integration in Sign-In Pipeline

Modify `Program.cs` login endpoint:

```csharp
// After password validation, before SignInAsync
var additionalClaims = UserClaimsFactory.CreateClaims(user);
await signInManager.SignInWithClaimsAsync(user, remember, additionalClaims);
```

---

## 4. ClaimsPrincipal Extensions

### 4.1 Extension Methods

```csharp
namespace RTUB.Core.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to simplify claims-based authorization
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Checks if user has a specific category claim
    /// </summary>
    public static bool IsCategory(this ClaimsPrincipal principal, string category)
    {
        return principal.HasClaim(ClaimTypes.Category, category?.ToUpperInvariant());
    }
    
    /// <summary>
    /// Checks if user has at least the specified category level
    /// Uses category hierarchy: Leitao < Caloiro < Tuno < Veterano < Tunossauro
    /// </summary>
    public static bool IsAtLeastCategory(this ClaimsPrincipal principal, string minimumCategory)
    {
        var minLevel = CategoryClaims.GetLevel(minimumCategory);
        if (minLevel < 0) return false;
        
        var userCategories = principal.FindAll(ClaimTypes.Category)
            .Select(c => c.Value)
            .ToList();
        
        // Check if any user category meets or exceeds the minimum level
        return userCategories.Any(cat => CategoryClaims.GetLevel(cat) >= minLevel);
    }
    
    /// <summary>
    /// Checks if user is ONLY Leitao (hasn't progressed to Caloiro or higher)
    /// </summary>
    public static bool IsOnlyLeitao(this ClaimsPrincipal principal)
    {
        return principal.IsCategory(CategoryClaims.Leitao) &&
               !principal.IsAtLeastCategory(CategoryClaims.Caloiro);
    }
    
    /// <summary>
    /// Checks if user has a specific position claim
    /// </summary>
    public static bool HasPosition(this ClaimsPrincipal principal, string position)
    {
        return principal.HasClaim(ClaimTypes.Position, position?.ToUpperInvariant());
    }
    
    /// <summary>
    /// Gets all categories for the user
    /// </summary>
    public static IEnumerable<string> GetCategories(this ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Category)
            .Select(c => c.Value);
    }
    
    /// <summary>
    /// Gets all positions for the user
    /// </summary>
    public static IEnumerable<string> GetPositions(this ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Position)
            .Select(c => c.Value);
    }
    
    /// <summary>
    /// Gets the primary category for the user
    /// </summary>
    public static string? GetPrimaryCategory(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.PrimaryCategory)?.Value;
    }
    
    /// <summary>
    /// Checks if user is a founder
    /// </summary>
    public static bool IsFounder(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(ClaimTypes.IsFounder, "true");
    }
    
    /// <summary>
    /// Gets years as Tuno
    /// </summary>
    public static int? GetYearsAsTuno(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.YearsAsTuno);
        if (claim != null && int.TryParse(claim.Value, out var years))
        {
            return years;
        }
        return null;
    }
    
    /// <summary>
    /// Checks if user is Tuno or higher (for mentor eligibility)
    /// </summary>
    public static bool CanBeMentor(this ClaimsPrincipal principal)
    {
        return principal.IsAtLeastCategory(CategoryClaims.Tuno);
    }
    
    /// <summary>
    /// Checks if user can hold president position
    /// </summary>
    public static bool CanHoldPresidentPosition(this ClaimsPrincipal principal)
    {
        return principal.IsAtLeastCategory(CategoryClaims.Tuno);
    }
    
    /// <summary>
    /// Checks if user is "Caloiro Admin" (Admin who is Caloiro, but not Owner)
    /// </summary>
    public static bool IsCaloiroAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsCategory(CategoryClaims.Caloiro) &&
               principal.IsInRole("Admin") &&
               !principal.IsInRole("Owner");
    }
}
```

---

## 5. Migration Strategy

### 5.1 Backward Compatibility

During migration, both old and new methods will coexist:

1. **Keep ApplicationUserExtensions.cs methods** - Mark as `[Obsolete]` with migration guidance
2. **Add ClaimsPrincipal extensions** - New preferred way
3. **Update gradually** - Replace usages file by file
4. **Test thoroughly** - Ensure both paths work during transition

### 5.2 ApplicationUser Extensions Compatibility Layer

```csharp
// Update ApplicationUserExtensions to use claims when user is available via HttpContext
public static bool IsLeitao(this ApplicationUser user)
{
    // Try claims first if available
    var httpContext = /* get from HttpContextAccessor if available */;
    if (httpContext?.User != null)
    {
        return httpContext.User.IsCategory(CategoryClaims.Leitao);
    }
    
    // Fallback to entity property
    return user.Categories.Contains(MemberCategory.Leitao);
}
```

### 5.3 Refactoring Pattern

**Before:**
```razor
@if (currentUser != null && currentUser.IsLeitao())
{
    <!-- Leitao-specific content -->
}
```

**After (using policy):**
```razor
<AuthorizeView Policy="@AuthorizationPolicies.LeitaoOnly">
    <!-- Leitao-specific content -->
</AuthorizeView>
```

**Or (using ClaimsPrincipal):**
```razor
@if (authState.User.IsCategory(CategoryClaims.Leitao))
{
    <!-- Leitao-specific content -->
}
```

---

## 6. Testing Strategy

### 6.1 Unit Tests for Claims Extensions

```csharp
[Fact]
public void IsAtLeastCategory_WithTuno_ReturnsTrue_ForCaloiro()
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Category, CategoryClaims.Tuno)
    };
    var identity = new ClaimsIdentity(claims);
    var principal = new ClaimsPrincipal(identity);
    
    principal.IsAtLeastCategory(CategoryClaims.Caloiro).Should().BeTrue();
}

[Fact]
public void IsAtLeastCategory_WithLeitao_ReturnsFalse_ForTuno()
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Category, CategoryClaims.Leitao)
    };
    var identity = new ClaimsIdentity(claims);
    var principal = new ClaimsPrincipal(identity);
    
    principal.IsAtLeastCategory(CategoryClaims.Tuno).Should().BeFalse();
}
```

### 6.2 Integration Tests for Policies

```csharp
[Fact]
public async Task RequireTunoOrHigher_WithTunoUser_AllowsAccess()
{
    // Arrange: Create user with Tuno category
    // Act: Attempt to access page with RequireTunoOrHigher policy
    // Assert: Access granted
}

[Fact]
public async Task RequireTunoOrHigher_WithLeitaoUser_DeniesAccess()
{
    // Arrange: Create user with Leitao category
    // Act: Attempt to access page with RequireTunoOrHigher policy
    // Assert: Access denied
}
```

---

## 7. Implementation Checklist

### Phase 2A: Infrastructure
- [ ] Create `ClaimTypes` constants class
- [ ] Create `CategoryClaims` constants class
- [ ] Create `PositionClaims` constants class
- [ ] Create `AuthorizationPolicies` constants class
- [ ] Create `AuthorizationPolicyConfiguration` class
- [ ] Create `UserClaimsFactory` class
- [ ] Create `ClaimsPrincipalExtensions` class

### Phase 2B: Integration
- [ ] Register policy configuration in Program.cs
- [ ] Integrate claims factory in sign-in pipeline
- [ ] Add unit tests for claim extensions
- [ ] Add unit tests for claims factory

### Phase 2C: Documentation
- [ ] Update developer documentation
- [ ] Create migration guide
- [ ] Document policy usage examples

---

## 8. Benefits of This Design

1. **Centralized**: All authorization logic in one place
2. **Testable**: Easy to unit test claim checks
3. **Discoverable**: Named policies make requirements clear
4. **Flexible**: Easy to add new policies without code changes
5. **Maintainable**: Changes to auth logic in one place
6. **Type-safe**: Constants prevent typos
7. **Backward Compatible**: Old methods still work during migration
8. **Claims-based**: Standard ASP.NET Core approach
9. **Auditable**: Claims in auth cookie for debugging
10. **Extensible**: Easy to add page permissions UI in Phase 5

---

## Conclusion

This claims-based design provides a solid foundation for:
- Simplified authorization checks
- Centralized policy management
- Future dynamic page permissions system
- Better maintainability and testability

The next phase will implement this design and begin migrating existing code to use the new claims-based system.
