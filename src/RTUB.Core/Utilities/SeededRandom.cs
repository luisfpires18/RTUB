namespace RTUB.Core.Utilities;

/// <summary>
/// Wrapper for System.Random that uses a seed for deterministic random number generation
/// Used by the combat engine to ensure reproducible results
/// </summary>
public class SeededRandom
{
    private readonly Random _random;

    /// <summary>
    /// Creates a new SeededRandom instance with the specified seed
    /// </summary>
    /// <param name="seed">The seed value for random number generation</param>
    public SeededRandom(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Returns a random double between 0.0 and 1.0
    /// </summary>
    public double NextDouble()
    {
        return _random.NextDouble();
    }

    /// <summary>
    /// Returns a random integer within the specified range [min, max)
    /// </summary>
    /// <param name="min">The inclusive lower bound</param>
    /// <param name="max">The exclusive upper bound</param>
    public int Next(int min, int max)
    {
        return _random.Next(min, max);
    }

    /// <summary>
    /// Returns a random double within the specified range [min, max)
    /// </summary>
    /// <param name="min">The inclusive lower bound</param>
    /// <param name="max">The exclusive upper bound</param>
    public double Next(double min, double max)
    {
        return min + (max - min) * _random.NextDouble();
    }
}
