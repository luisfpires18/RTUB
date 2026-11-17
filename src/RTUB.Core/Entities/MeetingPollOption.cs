using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

public class MeetingPollOption
{
    public int Id { get; set; }

    public int MeetingPollId { get; set; }

    [Required(ErrorMessage = "A opção é obrigatória")]
    [MaxLength(200, ErrorMessage = "A opção não pode exceder 200 caracteres")]
    public string Text { get; set; } = string.Empty;

    public MeetingPoll? MeetingPoll { get; set; }

    public List<MeetingPollVote> Votes { get; set; } = new();
}
