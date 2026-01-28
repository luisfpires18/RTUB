using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for validating role assignments
/// Extracted from Roles.razor to improve separation of concerns
/// </summary>
public class RoleAssignmentValidationService : IRoleAssignmentValidationService
{
    /// <summary>
    /// Validates if a role assignment can be created for a member
    /// </summary>
    /// <param name="member">The member to assign the role to</param>
    /// <param name="position">The position to assign</param>
    /// <param name="fiscalYear">The fiscal year string (format: "YYYY-YYYY")</param>
    /// <param name="existingAssignments">Collection of existing role assignments to check for conflicts</param>
    /// <returns>Validation result with error message if validation fails, null if valid</returns>
    public string? ValidateRoleAssignment(
        ApplicationUser member,
        Position position,
        string fiscalYear,
        IEnumerable<RoleAssignment> existingAssignments)
    {
        // Check if member is LEITAO
        if (member.IsLeitao())
        {
            return "Não é possível atribuir cargos a membros LEITÃO.";
        }

        // President positions that Caloiro cannot hold
        var presidentPositions = new[]
        {
            Position.Magister,
            Position.PresidenteMesaAssembleia,
            Position.PresidenteConselhoFiscal,
            Position.PresidenteConselhoVeteranos
        };

        // Check if member is CALOIRO and trying to assign a President position
        if (member.IsCaloiro() && presidentPositions.Contains(position))
        {
            return "CALOIRO não pode ser atribuído a cargos de Presidente.";
        }

        // Check if position is Presidente do Conselho de Veteranos and member is not Tuno Veterano
        if (position == Position.PresidenteConselhoVeteranos)
        {
            var isTunoVeterano = member.IsTuno() && member.QualifiesForVeterano();

            if (!isTunoVeterano)
            {
                return "Presidente do Conselho de Veteranos deve ser TUNO VETERANO (mínimo 2 anos como Tuno).";
            }
        }

        // Parse fiscal year
        var parts = fiscalYear.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var startYear) || !int.TryParse(parts[1], out var endYear))
        {
            return "Ano letivo inválido.";
        }

        // Check if this position is already assigned for this fiscal year
        var existing = existingAssignments
            .FirstOrDefault(r => r.Position == position &&
                                 r.StartYear == startYear &&
                                 r.EndYear == endYear);

        if (existing != null)
        {
            return "Este cargo já está atribuído para este ano letivo.";
        }

        // Check if member already has a position assigned in this fiscal year
        // Skip this check for Ensaiador position since it's not an official member role
        if (position != Position.Ensaiador)
        {
            var memberHasAssignment = existingAssignments
                .Where(r => r.UserId == member.Id)
                .Any(r => r.StartYear == startYear && r.EndYear == endYear);

            if (memberHasAssignment)
            {
                return "Este membro já tem um cargo atribuído para este ano letivo.";
            }
        }

        return null; // Validation passed
    }
}
