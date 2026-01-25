using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Application specific user extending IdentityUser
/// Contains user profile data and member information
/// </summary>
public class ApplicationUser : IdentityUser
{
    // Override Email to add validation attributes
    [Required(ErrorMessage = "O email é obrigatório")]
    [EmailAddress(ErrorMessage = "O formato do email é inválido")]
    public override string? Email { get; set; }
    [Required(ErrorMessage = "O primeiro nome é obrigatório")]
    [MaxLength(80, ErrorMessage = "O primeiro nome não pode exceder 80 caracteres")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "O último nome é obrigatório")]
    [MaxLength(80, ErrorMessage = "O último nome não pode exceder 80 caracteres")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "O nome de tuna é obrigatório")]
    [MaxLength(80, ErrorMessage = "O nome de tuna não pode exceder 80 caracteres")]
    public string? Nickname { get; set; }

    // PhoneNumber is inherited from IdentityUser, override to add validation
    [Required(ErrorMessage = "O contacto telefónico é obrigatório")]
    [MaxLength(80, ErrorMessage = "O contacto telefónico não pode exceder 80 caracteres")]
    public override string? PhoneNumber { get; set; }

    [MaxLength(100, ErrorMessage = "A cidade não pode exceder 100 caracteres")]
    public string? City { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? Degree { get; set; }
    public int? YearLeitao { get; set; }
    public int? MonthLeitao { get; set; }
    public int? YearCaloiro { get; set; }
    public int? MonthCaloiro { get; set; }
    public int? YearTuno { get; set; }
    public int? MonthTuno { get; set; }
    public bool RequirePasswordChange { get; set; } = false;
    public DateTime? LastLoginDate { get; set; }

    // Ranking/Level system
    public int ExperiencePoints { get; set; } = 0;
    public int Level { get; set; } = 1;

    // Betting system - Fidelis currency balance (default 10 for new users)
    public decimal FidelisBalance { get; set; } = 10m;

    // Email notification preferences
    public bool Subscribed { get; set; } = true;

    // Retirement status - tracks if active members have been inactive 6+ months
    public bool IsRetired { get; set; } = false;

    // Expulsion status - tracks if member has been expelled and cannot login
    public bool IsExpelled { get; set; } = false;

    // Mentor/Padrinho relationship
    public string? MentorId { get; set; }
    public ApplicationUser? Mentor { get; set; }

    // UI-only property for category validation (not mapped to database)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? SelectedCategory { get; set; }

    // Image handling
    public string? ImageUrl { get; set; }

    // Positions and Categories - EF Core 10 handles storage as primitive collections
    public List<Position> Positions { get; set; } = new();
    public List<MemberCategory> Categories { get; set; } = new();

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

            var now = DateTime.Now;
            var tunoStartYear = YearTuno.Value;
            var tunoStartMonth = MonthTuno ?? 1; // Default to January if month not set

            var tunoStart = new DateTime(tunoStartYear, tunoStartMonth, 1);
            var monthsAsTuno = ((now.Year - tunoStart.Year) * 12) + (now.Month - tunoStart.Month);
            var yearsAsTuno = monthsAsTuno / 12.0;

            if (yearsAsTuno >= 6) return "TUNOSSAURO";
            if (yearsAsTuno >= 2) return "VETERANO";
            return "TUNO";
        }
    }

    public string ProfilePictureSrc => !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : "/images/default-avatar.webp";
}
