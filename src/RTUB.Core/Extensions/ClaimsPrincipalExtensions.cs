using System.Security.Claims;
using RTUB.Core.Constants;

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
        if (principal == null || string.IsNullOrEmpty(category))
            return false;
            
        return principal.HasClaim(RtubClaimTypes.Category, category.ToUpperInvariant());
    }
    
    /// <summary>
    /// Checks if user has at least the specified category level
    /// Uses category hierarchy: Leitao &lt; Caloiro &lt; Tuno &lt; Veterano &lt; Tunossauro
    /// </summary>
    public static bool IsAtLeastCategory(this ClaimsPrincipal principal, string minimumCategory)
    {
        if (principal == null || string.IsNullOrEmpty(minimumCategory))
            return false;
            
        var minLevel = CategoryClaims.GetLevel(minimumCategory);
        if (minLevel < 0) return false;
        
        var userCategories = principal.FindAll(RtubClaimTypes.Category)
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
        if (principal == null)
            return false;
            
        return principal.IsCategory(CategoryClaims.Leitao) &&
               !principal.IsAtLeastCategory(CategoryClaims.Caloiro);
    }
    
    /// <summary>
    /// Checks if user has a specific position claim
    /// </summary>
    public static bool HasPosition(this ClaimsPrincipal principal, string position)
    {
        if (principal == null || string.IsNullOrEmpty(position))
            return false;
            
        return principal.HasClaim(RtubClaimTypes.Position, position.ToUpperInvariant());
    }
    
    /// <summary>
    /// Gets all categories for the user
    /// </summary>
    public static IEnumerable<string> GetCategories(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return Enumerable.Empty<string>();
            
        return principal.FindAll(RtubClaimTypes.Category)
            .Select(c => c.Value);
    }
    
    /// <summary>
    /// Gets all positions for the user
    /// </summary>
    public static IEnumerable<string> GetPositions(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return Enumerable.Empty<string>();
            
        return principal.FindAll(RtubClaimTypes.Position)
            .Select(c => c.Value);
    }
    
    /// <summary>
    /// Gets the primary category for the user
    /// </summary>
    public static string? GetPrimaryCategory(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return null;
            
        return principal.FindFirst(RtubClaimTypes.PrimaryCategory)?.Value;
    }
    
    /// <summary>
    /// Checks if user is a founder
    /// </summary>
    public static bool IsFounder(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return false;
            
        return principal.HasClaim(RtubClaimTypes.IsFounder, "true");
    }
    
    /// <summary>
    /// Gets years as Tuno
    /// </summary>
    public static int? GetYearsAsTuno(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return null;
            
        var claim = principal.FindFirst(RtubClaimTypes.YearsAsTuno);
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
        if (principal == null)
            return false;
            
        return principal.IsAtLeastCategory(CategoryClaims.Tuno);
    }
    
    /// <summary>
    /// Checks if user can hold president position
    /// </summary>
    public static bool CanHoldPresidentPosition(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return false;
            
        return principal.IsAtLeastCategory(CategoryClaims.Tuno);
    }
    
    /// <summary>
    /// Checks if user is "Caloiro Admin" (Admin who is Caloiro, but not Owner)
    /// </summary>
    public static bool IsCaloiroAdmin(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return false;
            
        return principal.IsCategory(CategoryClaims.Caloiro) &&
               principal.IsInRole("Admin") &&
               !principal.IsInRole("Owner");
    }
}
