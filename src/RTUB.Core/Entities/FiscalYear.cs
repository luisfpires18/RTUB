namespace RTUB.Core.Entities;

/// <summary>
/// Represents a fiscal year period
/// </summary>
public class FiscalYear : BaseEntity
{
    public int StartYear { get; set; }
    public int EndYear { get; set; }

    // Private constructor for EF Core
    public FiscalYear() { }

    public static FiscalYear Create(int startYear, int endYear)
    {
        if (startYear >= endYear)
            throw new ArgumentException("End year must be after start year");

        return new FiscalYear
        {
            StartYear = startYear,
            EndYear = endYear
        };
    }

    public string GetFiscalYearString()
    {
        return $"{StartYear}-{EndYear}";
    }
    
    // Property alias for backward compatibility
    public string FiscalYearString => GetFiscalYearString();
}
