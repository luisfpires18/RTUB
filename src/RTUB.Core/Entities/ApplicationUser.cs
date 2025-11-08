using Microsoft.AspNetCore.Identity;
using RTUB.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RTUB.Core.Entities;

/// <summary>
/// Application specific user extending IdentityUser
/// Contains user profile data and member information
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "O primeiro nome é obrigatório")]
    [MaxLength(80, ErrorMessage = "O primeiro nome não pode exceder 80 caracteres")]
    public string? FirstName { get; set; }
    
    [Required(ErrorMessage = "O último nome é obrigatório")]
    [MaxLength(80, ErrorMessage = "O último nome não pode exceder 80 caracteres")]
    public string? LastName { get; set; }
    public string? Nickname { get; set; }

    [Required(ErrorMessage = "O contacto telefónico é obrigatório")]
    [MaxLength(80, ErrorMessage = "O contacto telefónico não pode exceder 80 caracteres")]
    public string? PhoneContact { get; set; }
    
    [MaxLength(100, ErrorMessage = "A cidade não pode exceder 100 caracteres")]
    public string? City { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    public string? Degree { get; set; }
    public int? YearLeitao { get; set; }
    public int? YearCaloiro { get; set; }
    public int? YearTuno { get; set; }
    public InstrumentType? MainInstrument { get; set; }
    public bool RequirePasswordChange { get; set; } = false;
    public DateTime? LastLoginDate { get; set; }
    
    // Email notification preferences
    public bool Subscribed { get; set; } = false;
    
    // Mentor/Padrinho relationship
    public string? MentorId { get; set; }
    public ApplicationUser? Mentor { get; set; }
    
    // Image handling
    public byte[]? ProfilePictureData { get; set; }
    public string? ProfilePictureContentType { get; set; }
    public string? S3ImageFilename { get; set; } // IDrive S3 storage filename for profile picture
    
    // Positions and Categories (stored as JSON)
    public string? PositionsJson { get; set; }
    public string? CategoriesJson { get; set; }
    
    // Helper properties
    public List<Position> Positions
    {
        get
        {
            if (string.IsNullOrEmpty(PositionsJson)) return new List<Position>();
            try
            {
                return JsonSerializer.Deserialize<List<Position>>(PositionsJson) ?? new List<Position>();
            }
            catch
            {
                return new List<Position>();
            }
        }
        set
        {
            PositionsJson = value != null && value.Any()
                ? JsonSerializer.Serialize(value)
                : null;
        }
    }
    
    public List<MemberCategory> Categories
    {
        get
        {
            if (string.IsNullOrEmpty(CategoriesJson)) return new List<MemberCategory>();
            try
            {
                return JsonSerializer.Deserialize<List<MemberCategory>>(CategoriesJson) ?? new List<MemberCategory>();
            }
            catch
            {
                return new List<MemberCategory>();
            }
        }
        set
        {
            CategoriesJson = value != null && value.Any()
                ? JsonSerializer.Serialize(value)
                : null;
        }
    }
    
    public int? Age
    {
        get
        {
            if (DateOfBirth == null) return null;
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
    
    public string CurrentRole
    {
        get
        {
            if (YearTuno == null) return "N/A";
            var yearsAsTuno = DateTime.Now.Year - YearTuno.Value;
            if (yearsAsTuno >= 6) return "TUNOSSAURO";
            if (yearsAsTuno >= 2) return "VETERANO";
            return "TUNO";
        }
    }
    
    public string ProfilePictureSrc => $"/api/images/profile/{Id}";
}
