# Phase 2: Build Complete Chord Library

Build a complete chord library feature with visual diagrams for tuna instruments.

## CONTEXT
- Repository: luisfpires18/RTUB
- Build on existing InstrumentType enum from Phase 1
- Need visual chord diagrams rendered on HTML5 canvas
- Support for barre chords and different string counts

## DELIVERABLES

### 1. DATABASE LAYER

Create in `src/RTUB.Core/`:

**Enums/DifficultyLevel.cs:**
- Easy, Medium, Hard
- Add [Display(Name = "Fácil")], [Display(Name = "Médio")], [Display(Name = "Difícil")]

**Entities/ChordDiagram.cs:**
```csharp
public class ChordDiagram :  BaseEntity
{
    public InstrumentType InstrumentType { get; set; }
    public string ChordName { get; set; } // e.g., "C Major", "Am"
    public DifficultyLevel Difficulty { get; set; }
    public string FingeringData { get; set; } // JSON
    public string?  ImageUrl { get; set; }
    public string? AudioSampleUrl { get; set; }
}
```

Create `src/RTUB.Application/DTOs/ChordDiagramDto.cs`:
```csharp
public class ChordDiagramDto
{
    public int Id { get; set; }
    public string Instrument { get; set; }
    public string ChordName { get; set; }
    public string Difficulty { get; set; }
    public ChordFingeringDto Fingering { get; set; }
}

public class ChordFingeringDto
{
    public int Strings { get; set; } // 4 or 6
    public int[] Frets { get; set; } // -1=muted, 0=open, 1-12=fret number
    public int[] Fingers { get; set; } // 0=unused, 1-4=finger number
    public int BaseFret { get; set; } // Starting fret (1=open position)
    public BarreDto[]? Barres { get; set; }
}

public class BarreDto
{
    public int Fret { get; set; }
    public int FromString { get; set; } // 1-based
    public int ToString { get; set; }
}
```

Update `src/RTUB.Application/Data/ApplicationDbContext.cs`:
- Add `DbSet<ChordDiagram>`
- Configure JSON column for FingeringData (SQLite)
- Add unique constraint:  (InstrumentType, ChordName)
- Create migration "AddChordLibrary"

### 2. SEED DATA

Create `src/RTUB.Application/Data/ChordSeeder.cs`:

Seed at least 20 chords: 

**Guitarra (Easy):**
```csharp
new ChordDiagram {
    InstrumentType = InstrumentType.Guitarra,
    ChordName = "C",
    Difficulty = DifficultyLevel.Easy,
    FingeringData = JsonSerializer.Serialize(new {
        strings = 6,
        frets = new[] { -1, 3, 2, 0, 1, 0 },
        fingers = new[] { 0, 3, 2, 0, 1, 0 },
        baseFret = 1
    })
}
```

Chords to seed:
- **Guitarra**: C, D, E, F (barre), G, A, Am, Dm, Em, G7
- **Cavaquinho**: C, D, G, Am, Em
- **Baixo**: C (root), D, E, G, A

Call `ChordSeeder.SeedAsync(context)` from `SeedData.InitializeAsync()` in Program.cs

### 3. BACKEND SERVICE

Create `src/RTUB.Application/Interfaces/IChordLibraryService.cs`:
```csharp
Task<PagedResult<ChordDiagramDto>> SearchAsync(
    InstrumentType?  instrument, 
    DifficultyLevel? difficulty, 
    string? searchTerm, 
    int pageNumber, 
    int pageSize, 
    CancellationToken ct = default);

Task<ChordDiagramDto? > GetByIdAsync(int id, CancellationToken ct = default);

Task<IEnumerable<ChordDiagramDto>> GetCommonChordsAsync(
    InstrumentType instrument, 
    int limit = 20, 
    CancellationToken ct = default);
```

Create `src/RTUB.Application/Services/ChordLibraryService.cs`:
- Inject ApplicationDbContext, IMemoryCache, ILogger
- Implement caching (1 hour expiration for common chords)
- Case-insensitive search on ChordName
- Map ChordDiagram entities ↔ ChordDiagramDto
- Deserialize JSON FingeringData
- Validate JSON structure before saving

### 4. JAVASCRIPT CHORD RENDERER

Create `src/RTUB.Web/wwwroot/js/chord-renderer.js`:

```javascript
/**
 * Renders a chord diagram on an HTML5 canvas
 * @param {string} canvasId - Canvas element ID
 * @param {object} fingeringData - Chord fingering data
 * @param {object} options - Rendering options
 */
export function renderChordDiagram(canvasId, fingeringData, options = {}) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    
    const ctx = canvas. getContext('2d');
    const defaults = {
        frets: 5,
        showFingers: true,
        colorScheme: 'light', // or 'dark'
        nutThickness: 8,
        stringSpacing: 20,
        fretSpacing: 30
    };
    
    const opts = { ...defaults, ...options };
    
    // Auto-scale to canvas size (maintain 3:4 aspect ratio)
    // Draw fretboard grid (strings vertical, frets horizontal)
    // Draw nut (thick line at top if baseFret === 1)
    // Draw fret numbers (left side if baseFret > 1)
    // Draw finger positions (filled circles on grid intersections)
    // Draw open strings (hollow circle above nut)
    // Draw muted strings (X above nut)
    // Draw barre indicators (curved line connecting strings)
    // Draw finger numbers inside circles (if showFingers)
    
    // Color scheme: 
    // Light: black on white (#000 on #fff)
    // Dark: white on dark (#fff on #1a1a1a)
}
```

### 5. BLAZOR COMPONENT

Create `src/RTUB.Web/Components/Learning/ChordLibrary.razor`:

**Layout:**
```razor
<div class="row">
    <div class="col-md-3 mb-3">
        <!-- Instrument tabs (Guitarra/Bandolim/Cavaquinho/Baixo) -->
        <ul class="nav nav-pills flex-column">
            @foreach (var instrument in instruments)
            {
                <li class="nav-item">
                    <button class="nav-link @(selectedInstrument == instrument ? "active" : "")"
                            @onclick="() => OnInstrumentChanged(instrument)">
                        @instrument.GetDisplayName()
                    </button>
                </li>
            }
        </ul>
        
        <!-- Difficulty filter -->
        <div class="mt-3">
            <h6>Dificuldade</h6>
            @foreach (var diff in Enum.GetValues<DifficultyLevel>())
            {
                <div class="form-check">
                    <input type="checkbox" class="form-check-input" 
                           checked="@selectedDifficulties.Contains(diff)"
                           @onchange="e => OnDifficultyToggle(diff, e)">
                    <label class="form-check-label">@diff.GetDisplayName()</label>
                </div>
            }
        </div>
    </div>
    
    <div class="col-md-9">
        <!-- Search bar -->
        <input type="text" class="form-control mb-3" 
               placeholder="Pesquisar acordes..."
               @bind="searchTerm" 
               @bind:event="oninput"
               @bind:after="OnSearchChanged" />
        
        <!-- Chord grid -->
        <div class="row row-cols-1 row-cols-md-3 row-cols-lg-4 g-4">
            @if (isLoading)
            {
                @* Skeleton loading cards *@
                @for (int i = 0; i < 12; i++)
                {
                    <div class="col">
                        <div class="card">
                            <div class="card-body placeholder-glow">
                                <span class="placeholder col-6"></span>
                                <div class="placeholder-canvas"></div>
                            </div>
                        </div>
                    </div>
                }
            }
            else if (! chords.Any())
            {
                <div class="col-12 text-center text-muted py-5">
                    <p>Nenhum acorde encontrado</p>
                </div>
            }
            else
            {
                @foreach (var chord in chords)
                {
                    <div class="col">
                        <div class="card h-100">
                            <div class="card-header">
                                <strong>@chord.ChordName</strong>
                                <span class="badge bg-@GetDifficultyColor(chord. Difficulty) float-end">
                                    @chord. Difficulty
                                </span>
                            </div>
                            <div class="card-body">
                                <canvas id="chord-@chord.Id" width="200" height="250"></canvas>
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-sm btn-outline-primary">
                                    <i class="bi bi-star"></i> Favoritar
                                </button>
                            </div>
                        </div>
                    </div>
                }
            }
        </div>
        
        <!-- Pagination -->
        <nav class="mt-4" aria-label="Chord pagination">
            <ul class="pagination justify-content-center">
                <!-- Pagination controls -->
            </ul>
        </nav>
    </div>
</div>

@code {
    [Inject] private IChordLibraryService ChordLibraryService { get; set; }
    [Inject] private IJSRuntime JS { get; set; }
    
    private IJSObjectReference? chordRendererModule;
    private List<InstrumentType> instruments = new();
    private InstrumentType selectedInstrument = InstrumentType.Guitarra;
    private List<DifficultyLevel> selectedDifficulties = new();
    private string searchTerm = "";
    private List<ChordDiagramDto> chords = new();
    private bool isLoading = false;
    private int currentPage = 1;
    private const int PageSize = 20;
    
    protected override async Task OnInitializedAsync()
    {
        instruments = Enum.GetValues<InstrumentType>()
            .Where(i => i != InstrumentType.Acordeao && i != InstrumentType. Percussao)
            .ToList();
        await LoadChordsAsync();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            chordRendererModule = await JS. InvokeAsync<IJSObjectReference>(
                "import", "./js/chord-renderer.js");
        }
        
        if (chords.Any() && chordRendererModule != null)
        {
            foreach (var chord in chords)
            {
                await chordRendererModule.InvokeVoidAsync(
                    "renderChordDiagram", 
                    $"chord-{chord.Id}", 
                    chord. Fingering,
                    new { colorScheme = "light" });
            }
        }
    }
    
    private Timer? debounceTimer;
    private async Task OnSearchChanged()
    {
        debounceTimer?.Dispose();
        debounceTimer = new Timer(async _ => 
        {
            await InvokeAsync(async () => 
            {
                await LoadChordsAsync();
                StateHasChanged();
            });
        }, null, 300, Timeout.Infinite);
    }
    
    private async Task LoadChordsAsync()
    {
        isLoading = true;
        StateHasChanged();
        
        var result = await ChordLibraryService.SearchAsync(
            selectedInstrument,
            selectedDifficulties.Any() ? null : null,
            searchTerm,
            currentPage,
            PageSize);
        
        chords = result.Items. ToList();
        isLoading = false;
    }
    
    public async ValueTask DisposeAsync()
    {
        debounceTimer?.Dispose();
        if (chordRendererModule != null)
        {
            await chordRendererModule. DisposeAsync();
        }
    }
}
```

### 6. PAGE & NAVIGATION

Create `src/RTUB.Web/Components/Pages/Learning/ChordLibraryPage.razor`:
```razor
@page "/learning/chords"
@attribute [Authorize]

<PageTitle>Biblioteca de Acordes - RTUB</PageTitle>

<div class="container mt-4">
    <h2>Biblioteca de Acordes</h2>
    <p class="text-muted">
        Aprenda acordes para guitarra, bandolim, cavaquinho e baixo.
    </p>

    <ChordLibrary />

    <div class="mt-4 text-muted text-center">
        <small>
            Falta algum acorde? 
            <a href="/contact">Contacte-nos</a> ou contribua! 
        </small>
    </div>
</div>
```

**Update navigation:**
- Add "Acordes" link under "Aprender" menu
- Icon: bi-music-note-list
- Href: /learning/chords

### 7. SERVICE REGISTRATION

Update `AddLearningServices()` in ServiceCollectionExtensions: 
```csharp
services.AddScoped<IChordLibraryService, ChordLibraryService>();
```

## ACCEPTANCE CRITERIA

✅ Migration runs successfully  
✅ Seed data creates 20+ chords  
✅ Search works with 300ms debounce  
✅ Instrument and difficulty filters update results immediately  
✅ Chord diagrams render correctly (including barre chords)  
✅ Canvas auto-scales to container size  
✅ Pagination works (20 chords per page)  
✅ Dark mode support (if app has dark theme)  
✅ Mobile responsive (sidebar stacks on mobile)  
✅ Page accessible at /learning/chords  
✅ All text in Portuguese  
✅ No memory leaks (proper disposal)  

**Estimated Time:** 75 minutes