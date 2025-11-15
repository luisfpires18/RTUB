using System.Security.Claims;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.Extensions;

namespace RTUB.Application.Services;

/// <summary>
/// Factory for creating claims from ApplicationUser
/// </summary>
public static class UserClaimsFactory
{
    public static IEnumerable<Claim> CreateClaims(ApplicationUser user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));
            
        var claims = new List<Claim>();
        
        // Category claims - add all categories as separate claims
        foreach (var category in user.Categories)
        {
            claims.Add(new Claim(RtubClaimTypes.Category, MapCategoryToClaim(category)));
        }
        
        // Primary category - the highest category
        var primaryCategory = GetPrimaryCategory(user.Categories);
        if (primaryCategory != null)
        {
            claims.Add(new Claim(RtubClaimTypes.PrimaryCategory, primaryCategory));
        }
        
        // Position claims - add all positions as separate claims
        foreach (var position in user.Positions)
        {
            claims.Add(new Claim(RtubClaimTypes.Position, MapPositionToClaim(position)));
        }
        
        // Years as Tuno
        var yearsAsTuno = user.GetYearsAsTuno();
        if (yearsAsTuno.HasValue)
        {
            claims.Add(new Claim(RtubClaimTypes.YearsAsTuno, yearsAsTuno.Value.ToString()));
        }
        
        // Founder flag
        if (user.IsFundador())
        {
            claims.Add(new Claim(RtubClaimTypes.IsFounder, "true"));
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
