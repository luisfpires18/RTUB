using Microsoft.AspNetCore.Authorization;
using RTUB.Core.Constants;
using RTUB.Core.Enums;

namespace RTUB.Application.Authorization;

/// <summary>
/// Helper class for building authorization policies
/// Provides fluent API for creating complex authorization policies
/// </summary>
public static class PolicyHelper
{
    #region Permission-Based Policies
    
    /// <summary>
    /// Creates a policy that requires a specific permission
    /// </summary>
    public static AuthorizationPolicy RequirePermission(string permission)
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy that requires any of the specified permissions
    /// </summary>
    public static AuthorizationPolicy RequireAnyPermission(params string[] permissions)
    {
        var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder();
        
        foreach (var permission in permissions)
        {
            policy.AddRequirements(new PermissionRequirement(permission));
        }
        
        return policy.Build();
    }
    
    #endregion
    
    #region Category-Based Policies
    
    /// <summary>
    /// Creates a policy that requires a specific category
    /// </summary>
    public static AuthorizationPolicy RequireCategory(MemberCategory category)
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .AddRequirements(new CategoryRequirement(category.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be Leitão
    /// </summary>
    public static AuthorizationPolicy RequireLeitao()
    {
        return RequireCategory(MemberCategory.Leitao);
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be Caloiro
    /// </summary>
    public static AuthorizationPolicy RequireCaloiro()
    {
        return RequireCategory(MemberCategory.Caloiro);
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be Tuno or higher
    /// </summary>
    public static AuthorizationPolicy RequireTuno()
    {
        return RequirePermission(Permissions.CanBeMentor);
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be an effective member (not just Leitão)
    /// </summary>
    public static AuthorizationPolicy RequireEffectiveMember()
    {
        return RequirePermission(Permissions.IsEffectiveMember);
    }
    
    #endregion
    
    #region Position-Based Policies
    
    /// <summary>
    /// Creates a policy that requires a specific position
    /// </summary>
    public static AuthorizationPolicy RequirePosition(Position position)
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .AddRequirements(new PositionRequirement(position.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be Magister
    /// </summary>
    public static AuthorizationPolicy RequireMagister()
    {
        return RequirePosition(Position.Magister);
    }
    
    /// <summary>
    /// Creates a policy that requires the user to be Ensaiador
    /// </summary>
    public static AuthorizationPolicy RequireEnsaiador()
    {
        return RequirePosition(Position.Ensaiador);
    }
    
    #endregion
    
    #region Composite Policies (Category + Role)
    
    /// <summary>
    /// Creates a policy for Leitão with Member role
    /// </summary>
    public static AuthorizationPolicy LeitaoMember()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Member")
            .AddRequirements(new CategoryRequirement(MemberCategory.Leitao.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for Leitão with Admin role (shouldn't normally exist, but for completeness)
    /// </summary>
    public static AuthorizationPolicy LeitaoAdmin()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Admin")
            .AddRequirements(new CategoryRequirement(MemberCategory.Leitao.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for Caloiro with Member role
    /// </summary>
    public static AuthorizationPolicy CaloiroMember()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Member")
            .AddRequirements(new CategoryRequirement(MemberCategory.Caloiro.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for Caloiro with Admin role
    /// </summary>
    public static AuthorizationPolicy CaloiroAdmin()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Admin")
            .AddRequirements(new CategoryRequirement(MemberCategory.Caloiro.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for Tuno with Member role
    /// </summary>
    public static AuthorizationPolicy TunoMember()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Member")
            .AddRequirements(new CategoryRequirement(MemberCategory.Tuno.ToString()))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for Tuno with Admin role
    /// </summary>
    public static AuthorizationPolicy TunoAdmin()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Admin")
            .AddRequirements(new CategoryRequirement(MemberCategory.Tuno.ToString()))
            .Build();
    }
    
    #endregion
    
    #region Page Access Policies
    
    /// <summary>
    /// Creates a policy for viewing member pages
    /// </summary>
    public static AuthorizationPolicy ViewMemberPages()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(Permissions.ViewMemberPages))
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for viewing owner pages
    /// </summary>
    public static AuthorizationPolicy ViewOwnerPages()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireRole("Owner")
            .Build();
    }
    
    /// <summary>
    /// Creates a policy for managing finances (Magister or Treasurers)
    /// </summary>
    public static AuthorizationPolicy ManageFinances()
    {
        return new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(context =>
                context.User.HasClaim(c => 
                    c.Type == CustomClaimTypes.Permission && 
                    (c.Value == Permissions.ManageAllFinances || 
                     c.Value == Permissions.ManageTreasurerFinances)))
            .Build();
    }
    
    #endregion
}
