# Phase 3: Build Complete Practice Session Tracker

Build a complete practice session tracking system with statistics, streaks, and XP gamification.

## CONTEXT
- Repository: luisfpires18/RTUB
- Integrate with existing XP/Ranking system (check existing IRankingService or ApplicationUser. Xp)
- Track practice time per instrument, calculate consecutive day streaks
- Award XP for practice sessions (max 100 XP/day per user)

## DELIVERABLES

### 1. DATABASE LAYER

Create in `src/RTUB.Core/`:

**Entities/PracticeSession.cs:**
```csharp
public class PracticeSession : BaseEntity
{
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    
    public InstrumentType InstrumentType { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime?  EndTime { get; set; }
    public int DurationMinutes { get; set; } // Calculated on end
    
    public int?  SongId { get; set; }
    public Song?  Song { get; set; }
    
    public string?  Notes { get; set; }
    public int XpAwarded { get; set; }
}
```

Update `src/RTUB.Application/Data/ApplicationDbContext.cs`:
- Add `DbSet<PracticeSession>`
- Configure relationships: 
  - User:  required, OnDelete Cascade
  - Song: optional, OnDelete SetNull
- Add indexes: 
  - (UserId, StartTime) for user session queries
  - (InstrumentType) for statistics
- Create migration "AddPracticeTracking"

### 2. DTOs

Create `src/RTUB.Application/DTOs/PracticeSessionDto.cs`:
```csharp
public class PracticeSessionDto
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int?  DurationMinutes { get; set; }
    public string Instrument { get; set; }
    public string?  SongTitle { get; set; }
    public string? Notes { get; set; }
    public int XpAwarded { get; set; }
}

public class PracticeStatsDto
{
    public Dictionary<string, int> MinutesByInstrument { get; set; } = new();
    public int TotalMinutes { get; set; }
    public int SessionCount { get; set; }
    public int StreakDays { get; set; }
    public int XpEarned { get; set; }
    public List<TopSongDto> MostPracticedSongs { get; set; } = new();
    public int WeeklyGoalMinutes { get; set; }
    public int WeeklyMinutes { get; set; }
}

public class TopSongDto
{
    public int SongId { get; set; }
    public string Title { get; set; }
    public int SessionCount { get; set; }
    public int TotalMinutes { get; set; }
}
```

### 3. BACKEND SERVICE

Create `src/RTUB.Application/Interfaces/IPracticeTrackingService.cs`:
```csharp
Task<int> StartSessionAsync(string userId, InstrumentType instrument, int?  songId = null, CancellationToken ct = default);
Task EndSessionAsync(int sessionId, string?  notes = null, CancellationToken ct = default);
Task<PracticeSessionDto> QuickLogAsync(string userId, InstrumentType instrument, int durationMinutes, int? songId = null, string? notes = null, CancellationToken ct = default);
Task<PracticeStatsDto> GetStatsAsync(string userId, DateTime?  startDate = null, DateTime? endDate = null, CancellationToken ct = default);
Task<PagedResult<PracticeSessionDto>> GetSessionsAsync(string userId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);
Task<PracticeSessionDto?> GetActiveSessionAsync(string userId, CancellationToken ct = default);
```

Create `src/RTUB.Application/Services/PracticeTrackingService.cs`:

**Configuration** (add to appsettings.json):
```json
"PracticeTracking": {
  "XpPerQuarterHour": 10,
  "MaxDailyXp": 100,
  "WeeklyGoalMinutes": 180
}
```

**Implementation:**

```csharp
public class PracticeTrackingService : IPracticeTrackingService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<PracticeTrackingService> _logger;
    
    public async Task<int> StartSessionAsync(string userId, InstrumentType instrument, int? songId, CancellationToken ct)
    {
        var session = new PracticeSession
        {
            UserId = userId,
            InstrumentType = instrument,
            SongId = songId,
            StartTime = DateTime.UtcNow
        };
        
        _context. PracticeSessions.Add(session);
        await _context.SaveChangesAsync(ct);
        
        return session.Id;
    }
    
    public async Task EndSessionAsync(int sessionId, string?  notes, CancellationToken ct)
    {
        var session = await _context.PracticeSessions.FindAsync(sessionId);
        if (session == null || session.EndTime.HasValue)
            throw new InvalidOperationException("Session not found or already ended");
        
        session.EndTime = DateTime. UtcNow;
        session.DurationMinutes = (int)(session.EndTime. Value - session.StartTime).TotalMinutes;
        session.Notes = notes;
        
        // Calculate XP:  10 XP per 15 minutes
        var xpEarned = (session.DurationMinutes / 15) * _config. GetValue<int>("PracticeTracking:XpPerQuarterHour");
        
        // Check daily XP limit
        var today = DateTime.UtcNow.Date;
        var todaysXp = await _context.PracticeSessions
            .Where(s => s.UserId == session.UserId && s.StartTime.Date == today)
            .SumAsync(s => s.XpAwarded, ct);
        
        var maxDaily = _config.GetValue<int>("PracticeTracking:MaxDailyXp");
        session.XpAwarded = Math. Min(xpEarned, maxDaily - todaysXp);
        
        // Update user's total XP (integrate with existing system)
        // Option 1: If ApplicationUser has Xp property
        var user = await _context.Users.FindAsync(session.UserId);
        if (user != null)
        {
            // Assuming ApplicationUser has an Xp or TotalXp property
            // user. Xp += session.XpAwarded;
        }
        
        // Option 2: If there's a separate ranking service
        // await _rankingService.AwardXpAsync(session.UserId, session.XpAwarded);
        
        await _context.SaveChangesAsync(ct);
    }
    
    public async Task<PracticeStatsDto> GetStatsAsync(string userId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
    {
        var query = _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime.HasValue);
        
        if (startDate.HasValue)
            query = query.Where(s => s. StartTime >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(s => s.StartTime <= endDate.Value);
        
        var sessions = await query.ToListAsync(ct);
        
        // Calculate streak
        var streak = CalculateStreak(userId);
        
        // Weekly stats
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var weeklyMinutes = await _context.PracticeSessions
            .Where(s => s. UserId == userId && s.StartTime >= weekStart && s.EndTime. HasValue)
            .SumAsync(s => s.DurationMinutes, ct);
        
        return new PracticeStatsDto
        {
            MinutesByInstrument = sessions
                .GroupBy(s => s.InstrumentType. ToString())
                .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationMinutes)),
            TotalMinutes = sessions.Sum(s => s.DurationMinutes),
            SessionCount = sessions.Count,
            StreakDays = streak,
            XpEarned = sessions.Sum(s => s.XpAwarded),
            MostPracticedSongs = await GetTopSongsAsync(userId, ct),
            WeeklyGoalMinutes = _config.GetValue<int>("PracticeTracking:WeeklyGoalMinutes"),
            WeeklyMinutes = weeklyMinutes
        };
    }
    
    private int CalculateStreak(string userId)
    {
        var distinctDates = _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime.HasValue)
            .Select(s => s.StartTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
        
        if (! distinctDates.Any()) return 0;
        
        var streak = 1;
        var currentDate = DateTime.UtcNow.Date;
        
        // Allow today or yesterday as start
        if (distinctDates[0] == currentDate || distinctDates[0] == currentDate.AddDays(-1))
        {
            for (int i = 1; i < distinctDates.Count; i++)
            {
                if (distinctDates[i] == distinctDates[i - 1]. AddDays(-1))
                    streak++;
                else
                    break;
            }
        }
        else
        {
            streak = 0; // Streak broken
        }
        
        return streak;
    }
    
    private async Task<List<TopSongDto>> GetTopSongsAsync(string userId, CancellationToken ct)
    {
        return await _context.PracticeSessions
            .Where(s => s.UserId == userId && s. SongId. HasValue && s.EndTime.HasValue)
            .GroupBy(s => new { s.SongId, s.Song! .Title })
            .Select(g => new TopSongDto
            {
                SongId = g.Key.SongId! . Value,
                Title = g. Key.Title,
                SessionCount = g. Count(),
                TotalMinutes = g.Sum(s => s. DurationMinutes)
            })
            .OrderByDescending(s => s.TotalMinutes)
            .Take(5)
            .ToListAsync(ct);
    }
}
```

### 4. BLAZOR COMPONENTS

Create `src/RTUB.Web/Components/Learning/PracticeLogger.razor`:

**Floating Action Button (FAB) + Modal:**

```razor
@inject IPracticeTrackingService PracticeTrackingService
@inject ISongService SongService
@inject AuthenticationStateProvider AuthStateProvider
@inject IJSRuntime JS
@implements IAsyncDisposable

<!-- FAB Button -->
<button class="fab-button btn @(isSessionActive ? "btn-danger" : "btn-primary")" 
        @onclick="OpenModal"
        title="@(isSessionActive ? "Sessão Ativa" : "Iniciar Prática")">
    <i class="bi @(isSessionActive ? "bi-stop-circle" : "bi-play-circle")"></i>
</button>

<!-- Bootstrap Modal -->
<div class="modal fade" id="practiceModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">
                    @(isSessionActive ?  "Sessão Ativa" : "Iniciar Prática")
                </h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                @if (isSessionActive)
                {
                    <!-- Active session view -->
                    <div class="text-center">
                        <div class="display-4 mb-3">@FormatDuration(elapsedSeconds)</div>
                        <p class="text-muted">
                            <i class="bi bi-@GetInstrumentIcon(selectedInstrument)"></i>
                            @selectedInstrument. GetDisplayName()
                        </p>
                    </div>
                    
                    <div class="mb-3">
                        <label class="form-label">Notas (opcional)</label>
                        <textarea class="form-control" rows="3" @bind="sessionNotes" 
                                  placeholder="Como correu a prática? "></textarea>
                    </div>
                }
                else
                {
                    <!-- Start session form -->
                    <div class="mb-3">
                        <label class="form-label">Instrumento</label>
                        <select class="form-select" @bind="selectedInstrument">
                            @foreach (var instrument in Enum.GetValues<InstrumentType>())
                            {
                                <option value="@instrument">@instrument.GetDisplayName()</option>
                            }
                        </select>
                    </div>
                    
                    <div class="mb-3">
                        <label class="form-label">Música (opcional)</label>
                        <input type="text" class="form-control" 
                               @bind="songSearchTerm" 
                               @bind:event="oninput"
                               placeholder="Pesquisar música..." />
                        @if (songSuggestions.Any())
                        {
                            <div class="list-group mt-2">
                                @foreach (var song in songSuggestions)
                                {
                                    <button type="button" class="list-group-item list-group-item-action"
                                            @onclick="() => SelectSong(song)">
                                        @song.Title
                                    </button>
                                }
                            </div>
                        }
                    </div>
                    
                    <div class="text-muted small">
                        <strong>Dica:</strong> Ganha 10 XP por cada 15 minutos de prática (max 100 XP/dia)
                    </div>
                }
            </div>
            <div class="modal-footer">
                @if (isSessionActive)
                {
                    <button class="btn btn-danger" @onclick="EndSession">
                        <i class="bi bi-stop-circle"></i> Terminar Sessão
                    </button>
                }
                else
                {
                    <button class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                    <button class="btn btn-primary" @onclick="StartSession">
                        <i class="bi bi-play-circle"></i> Iniciar
                    </button>
                    <button class="btn btn-outline-primary" @onclick="ShowQuickLog">
                        <i class="bi bi-clock-history"></i> Registar Manualmente
                    </button>
                }
            </div>
        </div>
    </div>
</div>

@code {
    private bool isSessionActive = false;
    private int?  activeSessionId;
    private DateTime sessionStartTime;
    private int elapsedSeconds = 0;
    private Timer? timerInterval;
    
    private InstrumentType selectedInstrument = InstrumentType.Guitarra;
    private int?  selectedSongId;
    private string sessionNotes = "";
    private string songSearchTerm = "";
    private List<SongDto> songSuggestions = new();
    private string?  userId;
    
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider. GetAuthenticationStateAsync();
        userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Check for active session
        var activeSession = await PracticeTrackingService.GetActiveSessionAsync(userId);
        if (activeSession != null)
        {
            isSessionActive = true;
            activeSessionId = activeSession.Id;
            sessionStartTime = activeSession.StartTime;
            StartTimer();
        }
    }
    
    private async Task OpenModal()
    {
        await JS.InvokeVoidAsync("eval", "new bootstrap.Modal(document.getElementById('practiceModal')).show()");
    }
    
    private async Task StartSession()
    {
        activeSessionId = await PracticeTrackingService.StartSessionAsync(userId!, selectedInstrument, selectedSongId);
        isSessionActive = true;
        sessionStartTime = DateTime.UtcNow;
        StartTimer();
        
        await JS.InvokeVoidAsync("eval", "bootstrap.Modal.getInstance(document.getElementById('practiceModal')).hide()");
    }
    
    private async Task EndSession()
    {
        StopTimer();
        await PracticeTrackingService.EndSessionAsync(activeSessionId!. Value, sessionNotes);
        
        isSessionActive = false;
        activeSessionId = null;
        elapsedSeconds = 0;
        sessionNotes = "";
        
        await JS.InvokeVoidAsync("eval", "bootstrap.Modal. getInstance(document.getElementById('practiceModal')).hide()");
        await JS.InvokeVoidAsync("alert", $"Sessão terminada!  Praticaste durante {FormatDuration(elapsedSeconds)}");
    }
    
    private void StartTimer()
    {
        timerInterval = new Timer(_ =>
        {
            elapsedSeconds = (int)(DateTime.UtcNow - sessionStartTime).TotalSeconds;
            InvokeAsync(StateHasChanged);
        }, null, 0, 1000);
    }
    
    private void StopTimer()
    {
        timerInterval?.Dispose();
        timerInterval = null;
    }
    
    private string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var secs = seconds % 60;
        return $"{minutes:D2}:{secs:D2}";
    }
    
    public async ValueTask DisposeAsync()
    {
        StopTimer();
    }
}
```

Create CSS file `src/RTUB.Web/wwwroot/css/practice-logger.css`:
```css
.fab-button {
    position:  fixed;
    bottom: 2rem;
    right: 2rem;
    width: 60px;
    height: 60px;
    border-radius: 50%;
    font-size: 1.5rem;
    box-shadow: 0 4px 8px rgba(0,0,0,0.3);
    z-index: 1000;
    display: flex;
    align-items: center;
    justify-content: center;
}

.fab-button:hover {
    transform: scale(1.1);
    transition: transform 0.2s;
}
```

### 5. STATISTICS DASHBOARD

Create `src/RTUB.Web/Components/Learning/PracticeStats.razor`:

```razor
@inject IPracticeTrackingService PracticeTrackingService
@inject AuthenticationStateProvider AuthStateProvider

<div class="practice-stats">
    @if (stats == null)
    {
        <p>A carregar...</p>
    }
    else
    {
        <!-- Summary Cards -->
        <div class="row g-3 mb-4">
            <div class="col-md-3">
                <div class="card">
                    <div class="card-body text-center">
                        <h6 class="text-muted">Esta Semana</h6>
                        <div class="display-6">@stats.WeeklyMinutes min</div>
                        <small class="text-muted">Meta: @stats.WeeklyGoalMinutes min</small>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card">
                    <div class="card-body text-center">
                        <h6 class="text-muted">Streak</h6>
                        <div class="display-6">
                            <i class="bi bi-fire text-danger"></i> @stats. StreakDays
                        </div>
                        <small class="text-muted">dias consecutivos</small>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card">
                    <div class="card-body text-center">
                        <h6 class="text-muted">XP Ganho</h6>
                        <div class="display-6">@stats.XpEarned</div>
                        <small class="text-muted">desta prática</small>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card">
                    <div class="card-body text-center">
                        <h6 class="text-muted">Sessões</h6>
                        <div class="display-6">@stats.SessionCount</div>
                        <small class="text-muted">este mês</small>
                    </div>
                </div>
            </div>
        </div>
        
        <!-- Weekly Goal Progress -->
        <div class="card mb-4">
            <div class="card-body">
                <h6>Meta Semanal</h6>
                <div class="progress" style="height: 30px;">
                    <div class="progress-bar" role="progressbar" 
                         style="width: @GetProgressPercentage()%"
                         aria-valuenow="@stats.WeeklyMinutes" 
                         aria-valuemin="0" 
                         aria-valuemax="@stats.WeeklyGoalMinutes">
                        @stats.WeeklyMinutes / @stats.WeeklyGoalMinutes min
                    </div>
                </div>
                <p class="mt-2 mb-0 text-muted">
                    @GetMotivationalMessage()
                </p>
            </div>
        </div>
        
        <!-- Instrument Breakdown -->
        <div class="card mb-4">
            <div class="card-body">
                <h6>Tempo por Instrumento</h6>
                @foreach (var kvp in stats.MinutesByInstrument)
                {
                    var percentage = stats.TotalMinutes > 0 ?  (kvp.Value * 100.0 / stats.TotalMinutes) : 0;
                    <div class="mb-2">
                        <div class="d-flex justify-content-between">
                            <span>@kvp.Key</span>
                            <span>@kvp.Value min (@percentage.ToString("F1")%)</span>
                        </div>
                        <div class="progress" style="height: 20px;">
                            <div class="progress-bar bg-@GetInstrumentColor(kvp.Key)" 
                                 style="width: @percentage%"></div>
                        </div>
                    </div>
                }
            </div>
        </div>
        
        <!-- Most Practiced Songs -->
        @if (stats.MostPracticedSongs.Any())
        {
            <div class="card">
                <div class="card-body">
                    <h6>Músicas Mais Praticadas</h6>
                    <div class="list-group list-group-flush">
                        @foreach (var song in stats. MostPracticedSongs)
                        {
                            <div class="list-group-item d-flex justify-content-between align-items-center">
                                <span>@song.Title</span>
                                <span class="badge bg-primary rounded-pill">
                                    @song.TotalMinutes min · @song.SessionCount sessões
                                </span>
                            </div>
                        }
                    </div>
                </div>
            </div>
        }
    }
</div>

@code {
    private PracticeStatsDto? stats;
    private string?  userId;
    
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        stats = await PracticeTrackingService.GetStatsAsync(userId, monthStart);
    }
    
    private double GetProgressPercentage()
    {
        if (stats! .WeeklyGoalMinutes == 0) return 0;
        return Math.Min(100, (stats.WeeklyMinutes * 100.0 / stats.WeeklyGoalMinutes));
    }
    
    private string GetMotivationalMessage()
    {
        var progress = GetProgressPercentage();
        return progress switch
        {
            >= 100 => "🎉 Meta cumprida! Excelente trabalho!",
            >= 75 => "💪 Quase lá! Continua assim!",
            >= 50 => "👍 Bom progresso! Metade do caminho!",
            >= 25 => "🎵 Bom começo! Continua a praticar!",
            _ => "🎸 Vamos começar! A prática leva à perfeição!"
        };
    }
    
    private string GetInstrumentColor(string instrument)
    {
        return instrument switch
        {
            "Guitarra" => "primary",
            "Bandolim" => "success",
            "Cavaquinho" => "info",
            "Baixo" => "warning",
            _ => "secondary"
        };
    }
}
```

### 6. PAGE & NAVIGATION

Create `src/RTUB.Web/Components/Pages/Learning/PracticePage.razor`:
```razor
@page "/learning/practice"
@attribute [Authorize]

<PageTitle>Meu Progresso - RTUB</PageTitle>

<div class="container mt-4">
    <h2>Meu Progresso de Prática</h2>
    <p class="text-muted">Acompanha o teu desenvolvimento musical</p>
    
    <PracticeStats />
    
    <!-- Recent Sessions Table (optional) -->
    <div class="card mt-4">
        <div class="card-header">
            <h6 class="mb-0">Sessões Recentes</h6>
        </div>
        <div class="card-body">
            <!-- Table component here -->
        </div>
    </div>
</div>

<!-- PracticeLogger FAB visible on all /learning/* pages -->
<PracticeLogger />
```

**Update navigation:**
- Add "Progresso" link under "Aprender" menu
- Icon: bi-graph-up
- Href: /learning/practice

**Update MainLayout or Learning layout:**
- Include `<PracticeLogger />` component to show FAB on all /learning/* pages

### 7. SERVICE REGISTRATION

Update `AddLearningServices()`:
```csharp
services.AddScoped<IPracticeTrackingService, PracticeTrackingService>();
```

## ACCEPTANCE CRITERIA

✅ Migration runs successfully  
✅ Session duration calculated correctly  
✅ XP awards limited to 100/day per user  
✅ XP integrates with existing ranking system  
✅ Streak calculation handles consecutive days correctly  
✅ Timer updates every second without lag  
✅ FAB button visible on all /learning/* pages  
✅ Warning shows if navigating away with active session  
✅ Quick log feature works  
✅ Statistics dashboard shows correct aggregations  
✅ Weekly goal progress bar accurate  
✅ Page accessible at /learning/practice  
✅ All text in Portuguese  
✅ Mobile responsive  

**Estimated Time:** 90 minutes