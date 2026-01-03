# Phase 4: Extend Songs with Learning Features

Extend the existing Song entity to support difficulty classification and instrument tagging for the learning platform.

## CONTEXT
- Repository:  luisfpires18/RTUB
- Existing Song entity should already exist in src/RTUB.Core/Entities/
- This phase adds learning metadata to songs without breaking existing functionality
- Build on InstrumentType and DifficultyLevel enums from previous phases

## DELIVERABLES

### 1. EXTEND SONG ENTITY

Update `src/RTUB.Core/Entities/Song.cs`:

Add new properties (all nullable for backward compatibility):
```csharp
public class Song :  BaseEntity
{
    // ...  existing properties ... 
    
    // Learning platform properties
    public DifficultyLevel?  Difficulty { get; set; }
    public InstrumentType? PrimaryInstrument { get; set; }
    public string? SecondaryInstruments { get; set; } // JSON array of InstrumentType
    public string? SkillTags { get; set; } // JSON array of strings
    public int? EstimatedPracticeHours { get; set; }
}
```

Update `src/RTUB.Application/Data/ApplicationDbContext.cs`:
- Configure JSON columns for SecondaryInstruments and SkillTags (SQLite compatible)
- Add indexes:  (Difficulty), (PrimaryInstrument)
- Create migration "AddSongLearningMetadata"

### 2. DTOs

Create `src/RTUB.Application/DTOs/SongLearningDto.cs`:
```csharp
public class SongLearningDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string?  AlbumTitle { get; set; }
    public string?  Difficulty { get; set; }
    public string? PrimaryInstrument { get; set; }
    public List<string> SecondaryInstruments { get; set; } = new();
    public List<string> SkillTags { get; set; } = new();
    public int? EstimatedPracticeHours { get; set; }
    public string? YouTubeUrl { get; set; }
    public bool HasTabData { get; set; }
}
```

### 3. SEED LEARNING DATA

Create `src/RTUB.Application/Data/SongLearningSeeder.cs`:

Classify at least 10 existing songs with learning metadata: 

```csharp
public static async Task SeedAsync(ApplicationDbContext context)
{
    var songs = await context.Songs. ToListAsync();
    
    if (! songs.Any()) return;
    
    // Example classifications (adjust based on your actual songs)
    var classifications = new Dictionary<string, (DifficultyLevel, InstrumentType, string[])>
    {
        // Song title → (Difficulty, Primary Instrument, Skill Tags)
        { "fado", (DifficultyLevel.Medium, InstrumentType.Guitarra, new[] { "fingerpicking", "chord-changes" }) },
        { "corridinho", (DifficultyLevel. Easy, InstrumentType. Cavaquinho, new[] { "strumming", "rhythm" }) },
        { "marcha", (DifficultyLevel.Easy, InstrumentType.Percussao, new[] { "rhythm", "tempo" }) },
        // Add more... 
    };
    
    foreach (var song in songs)
    {
        var titleLower = song.Title.ToLower();
        foreach (var kvp in classifications)
        {
            if (titleLower.Contains(kvp.Key))
            {
                song.Difficulty = kvp.Value.Item1;
                song. PrimaryInstrument = kvp.Value.Item2;
                song.SkillTags = JsonSerializer.Serialize(kvp. Value.Item3);
                song.EstimatedPracticeHours = kvp.Value.Item1 switch
                {
                    DifficultyLevel.Easy => 5,
                    DifficultyLevel.Medium => 15,
                    DifficultyLevel.Hard => 40,
                    _ => null
                };
                break;
            }
        }
        
        // Default values if no match
        if (! song.Difficulty.HasValue)
        {
            song. Difficulty = DifficultyLevel. Medium;
            song.PrimaryInstrument = InstrumentType.Guitarra;
        }
    }
    
    await context.SaveChangesAsync();
}
```

Call from `SeedData. InitializeAsync()` in Program.cs:
```csharp
await SongLearningSeeder.SeedAsync(db);
```

### 4. EXTEND SONG SERVICE

Update `src/RTUB.Application/Interfaces/ISongService.cs`:

Add new methods:
```csharp
Task<PagedResult<SongLearningDto>> SearchForLearningAsync(
    InstrumentType? instrument,
    DifficultyLevel?  difficulty,
    string? skillTag,
    int?  maxPracticeHours,
    string? searchTerm,
    int pageNumber,
    int pageSize,
    CancellationToken ct = default);

Task<List<SongLearningDto>> GetRecommendationsAsync(
    string userId,
    InstrumentType? preferredInstrument,
    int limit = 5,
    CancellationToken ct = default);
```

Update existing `SongService` implementation (or create partial class):

```csharp
public async Task<PagedResult<SongLearningDto>> SearchForLearningAsync(...)
{
    var query = _context.Songs. AsQueryable();
    
    if (instrument.HasValue)
        query = query.Where(s => s.PrimaryInstrument == instrument);
    
    if (difficulty. HasValue)
        query = query.Where(s => s. Difficulty == difficulty);
    
    if (! string.IsNullOrEmpty(skillTag))
        query = query.Where(s => s. SkillTags != null && s.SkillTags.Contains(skillTag));
    
    if (maxPracticeHours.HasValue)
        query = query.Where(s => s.EstimatedPracticeHours <= maxPracticeHours);
    
    if (!string.IsNullOrEmpty(searchTerm))
        query = query.Where(s => s.Title.Contains(searchTerm));
    
    var total = await query.CountAsync(ct);
    var items = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Include(s => s.Album)
        .Select(s => new SongLearningDto
        {
            Id = s.Id,
            Title = s.Title,
            AlbumTitle = s.Album != null ? s.Album.Title :  null,
            Difficulty = s.Difficulty. HasValue ? s.Difficulty. Value. ToString() : null,
            PrimaryInstrument = s.PrimaryInstrument. HasValue ? s.PrimaryInstrument.Value.ToString() : null,
            SecondaryInstruments = s.SecondaryInstruments != null 
                ? JsonSerializer.Deserialize<List<string>>(s.SecondaryInstruments) ??  new()
                : new(),
            SkillTags = s.SkillTags != null 
                ? JsonSerializer. Deserialize<List<string>>(s.SkillTags) ?? new()
                : new(),
            EstimatedPracticeHours = s.EstimatedPracticeHours,
            // Check if song has tab data (Phase 5 feature)
            HasTabData = false // Will be updated in Phase 5
        })
        .ToListAsync(ct);
    
    return new PagedResult<SongLearningDto>(items, total, pageNumber, pageSize);
}

public async Task<List<SongLearningDto>> GetRecommendationsAsync(
    string userId,
    InstrumentType? preferredInstrument,
    int limit,
    CancellationToken ct)
{
    // Get user's practice history
    var practicedSongIds = await _context.PracticeSessions
        .Where(s => s.UserId == userId && s.SongId. HasValue)
        .Select(s => s.SongId! .Value)
        .Distinct()
        .ToListAsync(ct);
    
    // Determine user's current level based on practice history
    var totalPracticeMinutes = await _context.PracticeSessions
        .Where(s => s.UserId == userId)
        .SumAsync(s => s.DurationMinutes, ct);
    
    var userLevel = totalPracticeMinutes switch
    {
        < 600 => DifficultyLevel.Easy, // Less than 10 hours
        < 3000 => DifficultyLevel. Medium, // 10-50 hours
        _ => DifficultyLevel.Hard
    };
    
    // Get most practiced instrument
    var mostPracticedInstrument = preferredInstrument ??  await _context.PracticeSessions
        .Where(s => s.UserId == userId)
        .GroupBy(s => s.InstrumentType)
        .OrderByDescending(g => g.Sum(s => s.DurationMinutes))
        .Select(g => g.Key)
        .FirstOrDefaultAsync(ct);
    
    // Find songs that match user's level and instrument, excluding already practiced
    var recommendations = await _context. Songs
        .Where(s => s. Difficulty == userLevel)
        .Where(s => ! practicedSongIds.Contains(s.Id))
        .Where(s => s.PrimaryInstrument == mostPracticedInstrument)
        .OrderBy(_ => Guid.NewGuid()) // Random order
        .Take(limit)
        .Select(s => new SongLearningDto
        {
            Id = s.Id,
            Title = s.Title,
            Difficulty = s.Difficulty.HasValue ? s.Difficulty.Value.ToString() : null,
            PrimaryInstrument = s. PrimaryInstrument.HasValue ? s.PrimaryInstrument.Value.ToString() : null,
            EstimatedPracticeHours = s.EstimatedPracticeHours
        })
        .ToListAsync(ct);
    
    return recommendations;
}
```

### 5. BLAZOR COMPONENTS

Create `src/RTUB.Web/Components/Learning/SongBrowser.razor`:

```razor
@inject ISongService SongService

<div class="row">
    <div class="col-md-3">
        <!-- Filters -->
        <div class="card">
            <div class="card-body">
                <h6>Filtros</h6>
                
                <div class="mb-3">
                    <label class="form-label">Dificuldade</label>
                    @foreach (var diff in Enum.GetValues<DifficultyLevel>())
                    {
                        <div class="form-check">
                            <input type="checkbox" class="form-check-input" 
                                   @onchange="e => ToggleDifficulty(diff, e.Value)">
                            <label class="form-check-label">@diff. GetDisplayName()</label>
                        </div>
                    }
                </div>
                
                <div class="mb-3">
                    <label class="form-label">Instrumento</label>
                    @foreach (var inst in instruments)
                    {
                        <div class="form-check">
                            <input type="checkbox" class="form-check-input"
                                   @onchange="e => ToggleInstrument(inst, e.Value)">
                            <label class="form-check-label">@inst. GetDisplayName()</label>
                        </div>
                    }
                </div>
                
                <div class="mb-3">
                    <label class="form-label">Tempo de Prática (max horas)</label>
                    <input type="range" class="form-range" min="1" max="50" 