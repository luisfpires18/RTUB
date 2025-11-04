using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a performance request from a potential client
/// </summary>
public class Request : BaseEntity
{
    [Required(ErrorMessage = "O nome é obrigatório")]
    [MaxLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O email é obrigatório")]
    [EmailAddress(ErrorMessage = "Endereço de email inválido")]
    [MaxLength(200, ErrorMessage = "O email não pode exceder 200 caracteres")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O telefone é obrigatório")]
    [MaxLength(20, ErrorMessage = "O telefone não pode exceder 20 caracteres")]
    public string Phone { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O tipo de evento é obrigatório")]
    [MaxLength(100, ErrorMessage = "O tipo de evento não pode exceder 100 caracteres")]
    public string EventType { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "A data preferida é obrigatória")]
    public DateTime PreferredDate { get; set; }
    
    public DateTime? PreferredEndDate { get; set; }
    public bool IsDateRange { get; set; }
    
    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = string.Empty;
    
    [MaxLength(2000, ErrorMessage = "A mensagem não pode exceder 2000 caracteres")]
    public string Message { get; set; } = string.Empty;
    
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    // Private constructor for EF Core
    public Request() { }

    public static Request Create(string name, string email, string phone, string eventType, 
        DateTime preferredDate, string location, string message)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome não pode estar vazio", nameof(name));
        
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O email não pode estar vazio", nameof(email));
        
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("O telefone não pode estar vazio", nameof(phone));
        
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("O tipo de evento não pode estar vazio", nameof(eventType));

        return new Request
        {
            Name = name,
            Email = email,
            Phone = phone,
            EventType = eventType,
            PreferredDate = preferredDate,
            Location = location,
            Message = message,
            Status = RequestStatus.Pending
        };
    }

    public void SetDateRange(DateTime endDate)
    {
        if (endDate < PreferredDate)
            throw new ArgumentException("A data de fim não pode ser anterior à data de início");

        PreferredEndDate = endDate;
        IsDateRange = true;
    }

    public void UpdateStatus(RequestStatus status)
    {
        Status = status;
    }
}
