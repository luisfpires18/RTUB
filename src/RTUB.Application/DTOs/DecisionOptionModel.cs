namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for decision bet options
/// Used in Bets page for creating decision-type bets with custom options
/// </summary>
public class DecisionOptionModel
{
    public string Title { get; set; } = string.Empty;
    public decimal Odds { get; set; } = 1.50m;
}
