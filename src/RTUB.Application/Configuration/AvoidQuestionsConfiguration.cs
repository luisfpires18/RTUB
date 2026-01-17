namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for the Avoid Questions game
/// </summary>
public class AvoidQuestionsConfiguration
{
    public const string SectionName = "Games:AvoidQuestions";
    public List<string> Questions { get; set; } = new();
    public double BaseSpawnRate { get; set; } = 1.5;
    public double BaseFallingSpeed { get; set; } = 100;
    public double SpawnRateDecreasePerLevel { get; set; } = 0.1;
    public double SpeedIncreasePerLevel { get; set; } = 15;
    public int LevelDurationSeconds { get; set; } = 60;
    public int MaxLives { get; set; } = 5;
    public int PointsPerDodge { get; set; } = 1;
}
