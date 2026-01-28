using RTUB.Core.Entities;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for grouping member debts by user
/// </summary>
public class MemberDebtGroupDto
{
    public ApplicationUser User { get; set; } = null!;
    public List<MemberDebt> Debts { get; set; } = new();
    public decimal TotalAmount => Debts.Sum(d => d.AmountOwed);
}
