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
        string dateFormatted,
        string eventLocation,
        string eventLink,
        string nickname = "",
        string fullName = "")
    {
        var model = new EventNotificationModel
        {
            EventTitle = eventTitle,
            DateFormatted = dateFormatted,
            EventLocation = eventLocation,
            EventLink = eventLink,
            Nickname = nickname,
            FullName = fullName
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
}
