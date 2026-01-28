using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

public class MyTunoChallengeRequest : BaseEntity
{
    public string RequesterUserId { get; set; } = string.Empty;
    public ApplicationUser? Requester { get; set; }

    public string TargetUserId { get; set; } = string.Empty;
    public ApplicationUser? Target { get; set; }

    public MyTunoChallengeStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
