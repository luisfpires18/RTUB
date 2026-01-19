using RTUB.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a betting event where members can place Fidelis wagers
/// Domain entity - contains only business logic, no infrastructure concerns
/// </summary>
public class Bet : BaseEntity
{
    [Required(ErrorMessage = "O título da aposta é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título da aposta não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "A descrição não pode exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;

    // Image handling - store reference/path, actual storage handled by infrastructure
    public string? ImageSrc { get; set; }

    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data e hora da aposta são obrigatórias")]
    public DateTime DateTime { get; set; }

    [Required(ErrorMessage = "A categoria da aposta é obrigatória")]
    public BetCategory BetCategory { get; set; }

    // Cancellation tracking
    public bool IsCancelled { get; set; }

    [MaxLength(1000, ErrorMessage = "O motivo de cancelamento não pode exceder 1000 caracteres")]
    public string? CancellationReason { get; set; }

    // Resolution tracking - set by admin after event is past
    public int? WinningOptionId { get; set; }

    // Navigation properties
    public virtual ICollection<BetOption> Options { get; set; } = new List<BetOption>();
    public virtual ICollection<UserBet> UserBets { get; set; } = new List<UserBet>();
    public virtual Discussion? Discussion { get; set; }

    // Public constructor for EF Core and form initialization
    public Bet() { }

    // Factory method - ensures valid entity creation
    public static Bet Create(string title, DateTime dateTime, BetCategory category, string description = "", string location = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da aposta não pode estar vazio", nameof(title));

        // Using local time for consistency with Events
        if (dateTime <= DateTime.Now)
            throw new ArgumentException("A data e hora da aposta devem ser no futuro", nameof(dateTime));

        return new Bet
        {
            Title = title,
            DateTime = dateTime,
            BetCategory = category,
            Description = description,
            Location = location
        };
    }

    // Business methods
    public void UpdateDetails(string title, DateTime dateTime, BetCategory category, string description, string location)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da aposta não pode estar vazio", nameof(title));

        // Don't allow updating past bets
        if (IsResolved())
            throw new InvalidOperationException("Não é possível atualizar uma aposta já resolvida");

        Title = title;
        DateTime = dateTime;
        BetCategory = category;
        Description = description;
        Location = location;
    }

    public void SetImage(string? imageSrc)
    {
        ImageSrc = imageSrc;
    }

    public string GetImageSource()
    {
        return !string.IsNullOrEmpty(ImageSrc) ? ImageSrc : "";
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo de cancelamento é obrigatório", nameof(reason));

        if (IsResolved())
            throw new InvalidOperationException("Não é possível cancelar uma aposta já resolvida");

        IsCancelled = true;
        CancellationReason = reason;
    }

    public void Uncancel()
    {
        if (IsResolved())
            throw new InvalidOperationException("Não é possível reativar uma aposta já resolvida");

        IsCancelled = false;
        CancellationReason = null;
    }

    public void Resolve(int winningOptionId)
    {
        if (IsCancelled)
            throw new InvalidOperationException("Não é possível resolver uma aposta cancelada");

        if (IsResolved())
            throw new InvalidOperationException("A aposta já foi resolvida");

        // Verify the winning option belongs to this bet
        if (!Options.Any(o => o.Id == winningOptionId))
            throw new ArgumentException("A opção vencedora não pertence a esta aposta", nameof(winningOptionId));

        WinningOptionId = winningOptionId;
    }

    public bool IsResolved()
    {
        return WinningOptionId.HasValue;
    }

    public bool IsPast()
    {
        // Using local time for consistency with Events
        return DateTime <= DateTime.Now;
    }

    public bool IsFuture()
    {
        // Using local time for consistency with Events
        return DateTime > DateTime.Now;
    }
}
