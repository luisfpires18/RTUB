using RTUB.Core.Constants;
using RTUB.Core.Enums;
using System.Security.Claims;

namespace RTUB.Core.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to check Categories and Positions via claims
/// </summary>
public static class ClaimsPrincipalExtensions
{
    #region Category Checkers
    
    /// <summary>
    /// Checks if the principal has a specific category claim
    /// </summary>
    public static bool HasCategory(this ClaimsPrincipal principal, MemberCategory category)
    {
        return principal.HasClaim(CustomClaimTypes.Category, category.ToString());
    }
    
    /// <summary>
    /// Checks if the principal has Leitao category
    /// </summary>
    public static bool IsLeitao(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.Leitao);
    }
    
    /// <summary>
    /// Checks if the principal has Caloiro category
    /// </summary>
    public static bool IsCaloiro(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.Caloiro);
    }
    
    /// <summary>
    /// Checks if the principal has Tuno category
    /// </summary>
    public static bool IsTuno(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.Tuno);
    }
    
    /// <summary>
    /// Checks if the principal has Veterano category (2+ years as Tuno)
    /// </summary>
    public static bool IsVeterano(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.Veterano);
    }
    
    /// <summary>
    /// Checks if the principal has Tunossauro category (6+ years as Tuno)
    /// </summary>
    public static bool IsTunossauro(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.Tunossauro);
    }
    
    /// <summary>
    /// Checks if the principal has TunoHonorario category
    /// </summary>
    public static bool IsTunoHonorario(this ClaimsPrincipal principal)
    {
        return principal.HasCategory(MemberCategory.TunoHonorario);
    }
    
    #endregion
    
    #region Position Checkers
    
    /// <summary>
    /// Checks if the principal has a specific position claim
    /// </summary>
    public static bool HasPosition(this ClaimsPrincipal principal, Position position)
    {
        return principal.HasClaim(CustomClaimTypes.Position, position.ToString());
    }
    
    /// <summary>
    /// Checks if the principal has Magister position
    /// </summary>
    public static bool IsMagister(this ClaimsPrincipal principal)
    {
        return principal.HasPosition(Position.Magister);
    }
    
    #endregion
    
    #region Combined Checks
    
    /// <summary>
    /// Checks if the principal is ONLY Leitao (not Caloiro, Tuno, Veterano, or Tunossauro)
    /// Used for permission checks and UI visibility
    /// </summary>
    public static bool IsOnlyLeitao(this ClaimsPrincipal principal)
    {
        var categoryClaimsCount = principal.Claims
            .Count(c => c.Type == CustomClaimTypes.Category);
        
        return principal.IsLeitao() && categoryClaimsCount == 1;
    }
    
    /// <summary>
    /// Checks if the principal is Tuno or higher (Tuno, Veterano, or Tunossauro)
    /// Used for mentor eligibility and permission checks
    /// </summary>
    public static bool IsTunoOrHigher(this ClaimsPrincipal principal)
    {
        return principal.IsTuno() || principal.IsVeterano() || principal.IsTunossauro();
    }
    
    /// <summary>
    /// Checks if the principal is an effective member (Caloiro, Tuno, Veterano, or Tunossauro)
    /// Excludes Leitao who are not yet official members
    /// </summary>
    public static bool IsEffectiveMember(this ClaimsPrincipal principal)
    {
        return principal.IsCaloiro() || principal.IsTuno() || 
               principal.IsVeterano() || principal.IsTunossauro();
    }
    
    /// <summary>
    /// Checks if the principal can be a mentor (Tuno or higher)
    /// Same as IsTunoOrHigher but more semantic for mentor-related logic
    /// </summary>
    public static bool CanBeMentor(this ClaimsPrincipal principal)
    {
        return principal.IsTunoOrHigher();
    }
    
    /// <summary>
    /// Checks if the principal can hold president position (not Leitao or Caloiro)
    /// Requires Tuno or higher category
    /// </summary>
    public static bool CanHoldPresidentPosition(this ClaimsPrincipal principal)
    {
        return principal.IsTunoOrHigher();
    }
    
    /// <summary>
    /// Checks if the principal is not just a Leitao (has progressed to Caloiro or beyond)
    /// </summary>
    public static bool IsNotOnlyLeitao(this ClaimsPrincipal principal)
    {
        return !principal.IsOnlyLeitao();
    }
    
    /// <summary>
    /// Gets all categories for the principal
    /// </summary>
    public static IEnumerable<MemberCategory> GetCategories(this ClaimsPrincipal principal)
    {
        return principal.Claims
            .Where(c => c.Type == CustomClaimTypes.Category)
            .Select(c => Enum.Parse<MemberCategory>(c.Value));
    }
    
    /// <summary>
    /// Gets all positions for the principal
    /// </summary>
    public static IEnumerable<Position> GetPositions(this ClaimsPrincipal principal)
    {
        return principal.Claims
            .Where(c => c.Type == CustomClaimTypes.Position)
            .Select(c => Enum.Parse<Position>(c.Value));
    }
    
    #endregion
}
