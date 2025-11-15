namespace RTUB.Core.Constants;

/// <summary>
/// Defines standard claim values for categories
/// </summary>
public static class CategoryClaims
{
    public const string Leitao = "LEITAO";
    public const string Caloiro = "CALOIRO";
    public const string Tuno = "TUNO";
    public const string Veterano = "VETERANO";
    public const string Tunossauro = "TUNOSSAURO";
    public const string TunoHonorario = "TUNO_HONORARIO";
    public const string Fundador = "FUNDADOR";
    
    /// <summary>
    /// Defines category hierarchy for comparison operations
    /// Lower index = lower rank
    /// </summary>
    public static readonly string[] Hierarchy = new[]
    {
        Leitao,      // 0 - Probationary
        Caloiro,     // 1 - First year
        Tuno,        // 2 - Full member
        Veterano,    // 3 - 2+ years
        Tunossauro   // 4 - 6+ years
    };
    
    /// <summary>
    /// Gets the hierarchy level for a category (higher = more senior)
    /// </summary>
    public static int GetLevel(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return -1;
            
        var index = Array.IndexOf(Hierarchy, category.ToUpperInvariant());
        return index >= 0 ? index : -1;
    }
}
