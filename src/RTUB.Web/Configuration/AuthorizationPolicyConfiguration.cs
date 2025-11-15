using Microsoft.AspNetCore.Authorization;
using RTUB.Core.Constants;
using RTUB.Core.Extensions;

namespace RTUB.Web.Configuration;

/// <summary>
/// Configuration for authorization policies
/// </summary>
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
