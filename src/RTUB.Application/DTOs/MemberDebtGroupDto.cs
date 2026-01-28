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

    /// <summary>
    /// Earliest compromise-until date across this user's debts, if any.
    /// Used to display when the member has committed to pay by.
    /// </summary>
    public DateTime? EarliestCompromisedUntil =>
        Debts.Where(d => d.CompromisedUntil.HasValue)
             .Select(d => d.CompromisedUntil)
             .OrderBy(d => d)
             .FirstOrDefault();
}
