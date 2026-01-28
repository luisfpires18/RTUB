namespace RTUB.Core.Combat;

public sealed class DeterministicRandom
{
    private readonly Random _random;

    public DeterministicRandom(long seed)
    {
        _random = new Random(unchecked((int)seed));
    }

    public int NextIntInclusive(int min, int max)
    {
        return _random.Next(min, max + 1);
    }
}
