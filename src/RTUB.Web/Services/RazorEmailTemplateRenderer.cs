using RTUB.Application.Interfaces;
using RTUB.Web.EmailTemplates.Models;

namespace RTUB.Web.Services;

/// <summary>
/// Implementation of email template renderer using Razor views
/// </summary>
public class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IEmailTemplateService _templateService;

    public RazorEmailTemplateRenderer(IEmailTemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task<string> RenderNewRequestNotificationAsync(
        string requestName,
        string requestEmail,
        string phone,
        string eventType,
        string dateInfo,
        string location,
        string message,
        DateTime createdAt)
    {
        var model = new NewRequestNotificationModel
        {
            RequestName = requestName,
            RequestEmail = requestEmail,
            Phone = phone,
            EventType = eventType,
            DateInfo = dateInfo,
            Location = location,
            Message = message,
            CreatedAt = createdAt
        };

        return await _templateService.RenderTemplateAsync("NewRequestNotification", model);
    }

    public async Task<string> RenderWelcomeEmailAsync(
        string userName,
        string fullName,
        string nickName,
        string password)
    {
        var model = new WelcomeEmailModel
        {
            UserName = userName,
            FullName = fullName,
            NickName = nickName,
            Password = password
        };

        return await _templateService.RenderTemplateAsync("WelcomeEmail", model);
    }

    public async Task<string> RenderEventNotificationAsync(
        string eventTitle,
        DateTime startDate,
        DateTime? endDate,
        string eventLocation,
        string eventLink,
        string nickname = "",
        string fullName = "",
        string eventDescription = "")
    {
        var model = new EventNotificationModel
        {
            EventTitle = eventTitle,
            StartDate = startDate,
            EndDate = endDate,
            EventLocation = eventLocation,
            EventLink = eventLink,
            Nickname = nickname,
            FullName = fullName,
            EventDescription = eventDescription
        };

        return await _templateService.RenderTemplateAsync("EventNotification", model);
    }

    public async Task<string> RenderPasswordResetAsync(string callbackUrl)
    {
        var model = new PasswordResetModel
        {
            CallbackUrl = callbackUrl
        };

        return await _templateService.RenderTemplateAsync("PasswordReset", model);
    }

    public async Task<string> RenderBirthdayNotificationAsync(
        string birthdayPersonNickname,
        string birthdayPersonFullName,
        string recipientNickname = "",
        string recipientFullName = "")
    {
        var model = new BirthdayNotificationModel
        {
            BirthdayPersonNickname = birthdayPersonNickname,
            BirthdayPersonFullName = birthdayPersonFullName,
            RecipientNickname = recipientNickname,
            RecipientFullName = recipientFullName
        };

        return await _templateService.RenderTemplateAsync("BirthdayNotification", model);
    }

    public async Task<string> RenderEventCancellationNotificationAsync(
        string eventTitle,
        DateTime startDate,
        DateTime? endDate,
        string eventLocation,
        string cancellationReason,
        string eventLink,
        string nickname = "",
        string fullName = "")
    {
        var model = new EventCancellationNotificationModel
        {
            EventTitle = eventTitle,
            StartDate = startDate,
            EndDate = endDate,
            EventLocation = eventLocation,
            CancellationReason = cancellationReason,
            EventLink = eventLink,
            Nickname = nickname,
            FullName = fullName
        };

        return await _templateService.RenderTemplateAsync("EventCancellationNotification", model);
    }
    
    public async Task<string> RenderEventReminderNotificationAsync(
        string eventTitle,
        DateTime startDate,
        DateTime? endDate,
        string eventLocation,
        string eventLink,
        int daysUntilEvent,
        string nickname = "",
        string fullName = "",
        string eventDescription = "",
        List<(string displayName, string category, string instrument, string? notes, bool isLeitao)>? participants = null,
        List<(string title, string? albumTitle, DateTime repertoireDate)>? repertoireSongs = null)
    {
        var model = new EventReminderNotificationModel
        {
            EventTitle = eventTitle,
            StartDate = startDate,
            EndDate = endDate,
            EventLocation = eventLocation,
            EventLink = eventLink,
            DaysUntilEvent = daysUntilEvent,
            Nickname = nickname,
            FullName = fullName,
            EventDescription = eventDescription,
            Participants = participants?.Select(p => new EventParticipantModel
            {
                DisplayName = p.displayName,
                Category = p.category,
                IsLeitao = p.isLeitao
            }).ToList() ?? new(),
            RepertoireSongs = repertoireSongs?.Select(r => new EventRepertoireSongModel
            {
                Title = r.title,
                AlbumTitle = r.albumTitle,
                RepertoireDate = r.repertoireDate
            }).ToList() ?? new()
        };

        return await _templateService.RenderTemplateAsync("EventReminderNotification", model);
    }
    
    public async Task<string> RenderAnnouncementEmailAsync(
        string title,
        string content,
        string nickname = "",
        string fullName = "")
    {
        var model = new AnnouncementEmailModel
        {
            Title = title,
            Content = content,
            RecipientNickname = nickname,
            RecipientFullName = fullName
        };

        return await _templateService.RenderTemplateAsync("AnnouncementEmail", model);
    }
    
    public async Task<string> RenderMeetingNotificationAsync(
        string meetingType,
        string meetingTitle,
        string dateFormatted,
        string location,
        string statement,
        string senderNickname,
        string senderCity,
        string? senderPosition = null,
        string nickname = "",
        string fullName = "")
    {
        var model = new MeetingNotificationModel
        {
            MeetingType = meetingType,
            MeetingTitle = meetingTitle,
            DateFormatted = dateFormatted,
            Location = location,
            Statement = statement,
            SenderNickname = senderNickname,
            SenderCity = senderCity,
            SenderPosition = senderPosition,
            Nickname = nickname,
            FullName = fullName
        };

        return await _templateService.RenderTemplateAsync("MeetingNotification", model);
    }
    
    public async Task<string> RenderMeetingCancellationAsync(
        string meetingType,
        string meetingTitle,
        string dateFormatted,
        string location,
        string cancellationReason,
        string senderNickname,
        string senderCity,
        string? senderPosition = null,
        string nickname = "",
        string fullName = "")
    {
        var model = new MeetingCancellationModel
        {
            MeetingType = meetingType,
            MeetingTitle = meetingTitle,
            DateFormatted = dateFormatted,
            Location = location,
            CancellationReason = cancellationReason,
            SenderNickname = senderNickname,
            SenderCity = senderCity,
            SenderPosition = senderPosition,
            Nickname = nickname,
            FullName = fullName
        };

        return await _templateService.RenderTemplateAsync("MeetingCancellation", model);
    }
    
    public async Task<string> RenderUsernameChangedEmailAsync(
        string fullName,
        string nickname,
        string oldUsername,
        string newUsername)
    {
        var model = new UsernameChangedEmailModel
        {
            FullName = fullName,
            Nickname = nickname,
            OldUsername = oldUsername,
            NewUsername = newUsername
        };

        return await _templateService.RenderTemplateAsync("UsernameChangedEmail", model);
    }
}
