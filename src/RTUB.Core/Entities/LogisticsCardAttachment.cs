using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// A file or external link attached to one Logística card (task 066) - not the board's files, not a link to another card.
/// Files live at a stable storage key (<c>StoragePath</c>, outside the fiscal-year Documentação tree); links keep their
/// http(s) <c>Url</c>.
/// </summary>
public class LogisticsCardAttachment : BaseEntity
{
    public int CardId { get; set; }

    public CardAttachmentKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? StoragePath { get; set; }

    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public string? UploadedById { get; set; }

    public virtual LogisticsCard Card { get; set; } = null!;
    public virtual ApplicationUser? UploadedBy { get; set; }
}
