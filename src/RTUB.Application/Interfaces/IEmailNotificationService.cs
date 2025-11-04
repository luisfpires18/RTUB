using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for sending email notifications
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Sends a notification email when a request status changes
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="requestName">The name of the requester</param>
    /// <param name="requestEmail">The email of the requester</param>
    /// <param name="oldStatus">The previous status</param>
    /// <param name="newStatus">The new status</param>
    Task SendRequestStatusChangedAsync(int requestId, string requestName, string requestEmail, RequestStatus oldStatus, RequestStatus newStatus);

    /// <summary>
    /// Sends a notification email when a new request is submitted
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="requestName">The name of the requester</param>
    /// <param name="requestEmail">The email of the requester</param>
    /// <param name="eventType">The type of event</param>
    Task SendNewRequestNotificationAsync(int requestId, string requestName, string requestEmail, string eventType);

    /// <summary>
    /// Sends a notification email when a new request is submitted (full details)
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="requestName">The name of the requester</param>
    /// <param name="requestEmail">The email of the requester</param>
    /// <param name="phone">The phone number</param>
    /// <param name="eventType">The type of event</param>
    /// <param name="preferredDate">The preferred date</param>
    /// <param name="preferredEndDate">The optional end date for date ranges</param>
    /// <param name="location">The location</param>
    /// <param name="message">Additional information</param>
    /// <param name="createdAt">When the request was created</param>
    Task SendNewRequestNotificationAsync(int requestId, string requestName, string requestEmail, string phone, 
        string eventType, DateTime preferredDate, DateTime? preferredEndDate, string location, string message, DateTime createdAt);

    /// <summary>
    /// Sends a welcome email with initial credentials to a newly created member
    /// </summary>
    /// <param name="userName">The username of the new member</param>
    /// <param name="email">The email address of the new member</param>
    /// <param name="firstName">The first name of the member</param>
    /// <param name="password">The generated password</param>
    Task SendWelcomeEmailAsync(string userName, string email, string firstName, string password);
}
