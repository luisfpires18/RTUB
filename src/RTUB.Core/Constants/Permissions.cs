namespace RTUB.Core.Constants;

/// <summary>
/// Defines permissions for fine-grained authorization
/// These permissions can be assigned to users based on their role, category, or position
/// </summary>
public static class Permissions
{
    #region Page Access Permissions
    
    // Public Pages (no authentication required)
    public const string ViewPublicPages = "pages:public:view";
    
    // Member Pages (requires authentication)
    public const string ViewMemberPages = "pages:member:view";
    public const string ViewMembersPage = "pages:members:view";
    public const string ViewProfilePage = "pages:profile:view";
    public const string ViewEventsPage = "pages:events:view";
    public const string ViewRehearsalsPage = "pages:rehearsals:view";
    public const string ViewInventoryPage = "pages:inventory:view";
    public const string ViewFinancePage = "pages:finance:view";
    public const string ViewReportsPage = "pages:reports:view";
    public const string ViewShopPage = "pages:shop:view";
    public const string ViewMusicPage = "pages:music:view";
    
    // Admin Pages (requires admin privileges)
    public const string ViewSlideshowsPage = "pages:slideshows:view";
    
    // Owner Pages (requires owner privileges)
    public const string ViewOwnerPages = "pages:owner:view";
    public const string ViewUserRolesPage = "pages:user-roles:view";
    public const string ViewAuditLogPage = "pages:audit-log:view";
    public const string ViewPermissionsPage = "pages:permissions:view";
    
    #endregion
    
    #region Member Management Permissions
    
    public const string ViewMembers = "members:view";
    public const string CreateMember = "members:create";
    public const string EditMember = "members:edit";
    public const string DeleteMember = "members:delete";
    public const string ManageMemberRoles = "members:manage-roles";
    public const string ManageMemberCategories = "members:manage-categories";
    public const string ManageMemberPositions = "members:manage-positions";
    
    #endregion
    
    #region Event Management Permissions
    
    public const string ViewEvents = "events:view";
    public const string CreateEvent = "events:create";
    public const string EditEvent = "events:edit";
    public const string DeleteEvent = "events:delete";
    public const string ManageEventEnrollments = "events:manage-enrollments";
    public const string EnrollInEvents = "events:enroll";
    public const string EnrollAsPerformer = "events:enroll-as-performer";
    
    #endregion
    
    #region Rehearsal Management Permissions
    
    public const string ViewRehearsals = "rehearsals:view";
    public const string CreateRehearsal = "rehearsals:create";
    public const string EditRehearsal = "rehearsals:edit";
    public const string DeleteRehearsal = "rehearsals:delete";
    public const string ManageRehearsalAttendance = "rehearsals:manage-attendance";
    
    #endregion
    
    #region Finance Permissions
    
    public const string ViewFinance = "finance:view";
    public const string CreateTransaction = "finance:create-transaction";
    public const string EditTransaction = "finance:edit-transaction";
    public const string DeleteTransaction = "finance:delete-transaction";
    public const string ManageFiscalYears = "finance:manage-fiscal-years";
    public const string ViewFinancialReports = "finance:view-reports";
    
    #endregion
    
    #region Inventory Permissions
    
    public const string ViewInventory = "inventory:view";
    public const string CreateInventoryItem = "inventory:create-item";
    public const string EditInventoryItem = "inventory:edit-item";
    public const string DeleteInventoryItem = "inventory:delete-item";
    public const string ManageInventoryCategories = "inventory:manage-categories";
    
    #endregion
    
    #region Music & Repertoire Permissions
    
    public const string ViewSongs = "music:view";
    public const string CreateSong = "music:create";
    public const string EditSong = "music:edit";
    public const string DeleteSong = "music:delete";
    public const string ManageAlbums = "music:manage-albums";
    public const string ManageMusicRepertoire = "music:manage-repertoire";
    
    #endregion
    
    #region Shop Permissions
    
    public const string ViewShop = "shop:view";
    public const string CreateProduct = "shop:create-product";
    public const string EditProduct = "shop:edit-product";
    public const string DeleteProduct = "shop:delete-product";
    public const string ReserveProduct = "shop:reserve-product";
    public const string ManageReservations = "shop:manage-reservations";
    
    #endregion
    
    #region Position-Specific Permissions
    
    // Magister (highest authority)
    public const string ManageAllFinances = "position:magister:manage-all-finances";
    public const string AssignAllPositions = "position:magister:assign-all-positions";
    public const string ViewAuditLogs = "position:magister:view-audit-logs";
    
    // Ensaiador (rehearsal coordinator)
    public const string ManageAllRehearsals = "position:ensaiador:manage-all-rehearsals";
    public const string ManageEnsaiadorRepertoire = "position:ensaiador:manage-repertoire";
    
    // Treasurer Positions
    public const string ManageTreasurerFinances = "position:treasurer:manage-finances";
    
    #endregion
    
    #region Category-Specific Permissions
    
    // Leitão restrictions (inverse permissions - what they CANNOT do)
    public const string LeitaoRestricted = "category:leitao:restricted";
    
    // Tuno+ permissions
    public const string CanBeMentor = "category:tuno:can-be-mentor";
    public const string CanVote = "category:tuno:can-vote";
    public const string CanHoldPresidentPosition = "category:tuno:can-hold-president-position";
    
    // Caloiro+ permissions
    public const string IsEffectiveMember = "category:effective-member";
    
    #endregion
    
    #region System Permissions
    
    public const string ManagePermissions = "system:manage-permissions";
    public const string ManageRoles = "system:manage-roles";
    public const string ViewSystemLogs = "system:view-logs";
    public const string ManageSystemSettings = "system:manage-settings";
    
    #endregion
}
