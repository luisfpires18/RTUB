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
