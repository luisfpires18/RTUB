namespace RTUB.Core.Entities;

/// <summary>Explicit access to a private Logística board (task 067). Granted and removed by Mod, Admin or Owner only.</summary>
public class LogisticsBoardMember : BaseEntity
{
    public int BoardId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public virtual LogisticsBoard Board { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}
