namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for reporting email send progress in bulk operations
/// </summary>
public class EmailSendProgress
{
    /// <summary>
    /// Total number of emails to be sent
    /// </summary>
    public int Total { get; set; }
    
    /// <summary>
    /// Number of emails successfully sent so far
    /// </summary>
    public int Sent { get; set; }
    
    /// <summary>
    /// Email address of the last recipient to receive an email
    /// </summary>
    public string? LastRecipient { get; set; }
}
