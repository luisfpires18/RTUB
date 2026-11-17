namespace RTUB.Application.DTOs;

public sealed record MeetingPollBoardDto(
    IReadOnlyList<MeetingPollAudienceOptionDto> CreateAudiences,
    IReadOnlyList<MeetingPollDto> Polls);

public sealed record MeetingPollAudienceOptionDto(string Value, string Label);

public sealed record MeetingPollDto(
    int Id,
    string Audience,
    string AudienceLabel,
    string Question,
    string CreatedAt,
    string? ClosesAt,
    bool Closed,
    bool CanManage,
    bool CanVote,
    int? MyOptionId,
    int TotalVotes,
    IReadOnlyList<MeetingPollOptionDto> Options);

public sealed record MeetingPollOptionDto(int Id, string Text, int Votes);

public sealed record MeetingPollInput(string? Audience, string? Question, IReadOnlyList<string>? Options, string? ClosesAt);

public sealed record MeetingPollVoteInput(int OptionId);
