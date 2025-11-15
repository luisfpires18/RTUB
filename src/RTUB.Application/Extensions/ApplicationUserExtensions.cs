using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for ApplicationUser to simplify category and role checks
/// Eliminates repeated Categories.Contains() logic across the codebase
/// </summary>
public static class ApplicationUserExtensions
{
    #region Category Checkers
    
    /// <summary>
    /// Checks if the user has Leitao category
    /// </summary>
    public static bool IsLeitao(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Leitao);
    }
    
    /// <summary>
    /// Checks if the user has Caloiro category
    /// </summary>
    public static bool IsCaloiro(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Caloiro);
    }
    
    /// <summary>
    /// Checks if the user has Tuno category
    /// </summary>
    public static bool IsTuno(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Tuno);
    }
    
    /// <summary>
    /// Checks if the user has Veterano category (2+ years as Tuno)
    /// </summary>
    public static bool IsVeterano(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Veterano);
    }
    
    /// <summary>
    /// Checks if the user has Tunossauro category (6+ years as Tuno)
    /// </summary>
    public static bool IsTunossauro(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Tunossauro);
    }
    
    /// <summary>
    /// Checks if the user has TunoHonorario category
    /// </summary>
    public static bool IsTunoHonorario(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.TunoHonorario);
    }
    
    /// <summary>
    /// Checks if the user has Fundador category (Founder of the Tuna)
    /// </summary>
    public static bool IsFundador(this ApplicationUser user)
    {
        return user.Categories.Contains(MemberCategory.Fundador);
    }
    
    #endregion
    
    #region Combined Checks
    
    /// <summary>
    /// Checks if the user is ONLY Leitao (not Caloiro, Tuno, Veterano, or Tunossauro)
    /// Used for permission checks and UI visibility
    /// </summary>
    public static bool IsOnlyLeitao(this ApplicationUser user)
    {
        var categories = user.Categories;
        return categories.Contains(MemberCategory.Leitao) &&
               !categories.Contains(MemberCategory.Caloiro) &&
               !categories.Contains(MemberCategory.Tuno) &&
               !categories.Contains(MemberCategory.Veterano) &&
               !categories.Contains(MemberCategory.Tunossauro);
    }
    
    /// <summary>
    /// Checks if the user is Tuno or higher (Tuno, Veterano, or Tunossauro)
    /// Used for mentor eligibility and permission checks
    /// </summary>
    public static bool IsTunoOrHigher(this ApplicationUser user)
    {
        var categories = user.Categories;
        return categories.Contains(MemberCategory.Tuno) ||
               categories.Contains(MemberCategory.Veterano) ||
               categories.Contains(MemberCategory.Tunossauro);
    }
    
    /// <summary>
    /// Checks if the user is an effective member (Caloiro, Tuno, Veterano, or Tunossauro)
    /// Excludes Leitao who are not yet official members
    /// </summary>
    public static bool IsEffectiveMember(this ApplicationUser user)
    {
        var categories = user.Categories;
        return categories.Contains(MemberCategory.Caloiro) ||
               categories.Contains(MemberCategory.Tuno) ||
               categories.Contains(MemberCategory.Veterano) ||
               categories.Contains(MemberCategory.Tunossauro);
    }
    
    /// <summary>
    /// Checks if the user can be a mentor (Tuno or higher)
    /// Same as IsTunoOrHigher but more semantic for mentor-related logic
    /// </summary>
    public static bool CanBeMentor(this ApplicationUser user)
    {
        return user.IsTunoOrHigher();
    }
    
    /// <summary>
    /// Checks if the user can hold president position (not Leitao or Caloiro)
    /// Requires Tuno or higher category
    /// </summary>
    public static bool CanHoldPresidentPosition(this ApplicationUser user)
    {
        return user.IsTunoOrHigher();
    }
    
    /// <summary>
    /// Checks if the user is not just a Leitao (has progressed to Caloiro or beyond)
    /// </summary>
    public static bool IsNotOnlyLeitao(this ApplicationUser user)
    {
        return !user.IsOnlyLeitao();
    }
    
    #endregion
    
    #region Years Calculation
    
    /// <summary>
    /// Gets the number of years as Tuno (from YearTuno+MonthTuno to current year+month)
    /// Returns null if YearTuno is not set
    /// Uses month-aware calculation if MonthTuno is available, otherwise defaults to January
    /// </summary>
    public static int? GetYearsAsTuno(this ApplicationUser user)
    {
        if (user.YearTuno == null) return null;
        
        var now = DateTime.Now;
        var tunoStartYear = user.YearTuno.Value;
        var tunoStartMonth = user.MonthTuno ?? 1; // Default to January if month not set
        
        var tunoStart = new DateTime(tunoStartYear, tunoStartMonth, 1);
        var monthsAsTuno = ((now.Year - tunoStart.Year) * 12) + (now.Month - tunoStart.Month);
        
        return monthsAsTuno / 12; // Integer division for complete years
    }
    
    /// <summary>
    /// Checks if the user has been Tuno for at least the specified number of years
    /// Uses month-aware calculation
    /// </summary>
    public static bool HasBeenTunoForYears(this ApplicationUser user, int years)
    {
        var yearsAsTuno = user.GetYearsAsTuno();
        return yearsAsTuno.HasValue && yearsAsTuno.Value >= years;
    }
    
    /// <summary>
    /// Checks if the user qualifies for Veterano status (2+ years as Tuno)
    /// Uses month-aware calculation
    /// </summary>
    public static bool QualifiesForVeterano(this ApplicationUser user)
    {
        return user.HasBeenTunoForYears(2);
    }
    
    /// <summary>
    /// Checks if the user qualifies for Tunossauro status (6+ years as Tuno)
    /// Uses month-aware calculation
    /// Note: Original comment in ApplicationUser.cs says 4+ years for Tunossauro,
    /// but CurrentRole property uses 6+ years. Using 6 for consistency with CurrentRole.
    /// </summary>
    public static bool QualifiesForTunossauro(this ApplicationUser user)
    {
        return user.HasBeenTunoForYears(6);
    }
    
    #endregion
    
    #region Display Name Methods
    
    /// <summary>
    /// Gets the display name in the format: Nickname (First Name Last Name)
    /// Example: Jeans (Luís Pires)
    /// If no nickname exists, returns First Name Last Name
    /// </summary>
    public static string GetDisplayName(this ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Nickname))
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }
        return $"{user.Nickname} ({user.FirstName} {user.LastName})".Trim();
    }
    #endregion
}
