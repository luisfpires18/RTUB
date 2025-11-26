using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a request to create a meeting (CV or AG)
/// </summary>
public class MeetingRequest : BaseEntity
{
    [Required(ErrorMessage = "O tipo de reunião é obrigatório")]
    public MeetingType RequestedMeetingType { get; set; }

    [Required(ErrorMessage = "O título é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data proposta é obrigatória")]
    public DateTime ProposedDateTime { get; set; }

    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string? Location { get; set; }

    [Required(ErrorMessage = "A descrição é obrigatória")]
    [MaxLength(2000, ErrorMessage = "A descrição não pode exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    [Required(ErrorMessage = "O autor é obrigatório")]
    public string AuthorUserId { get; set; } = string.Empty;

    public ApplicationUser? Author { get; set; }
}
