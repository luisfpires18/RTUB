# Phase 1: Build Complete Instrument Tuner

Build a complete instrument tuner feature for the RTUB application. 

## CONTEXT
- Repository: luisfpires18/RTUB
- Blazor Server app for Portuguese university tuna (musical group)
- Architecture: Clean Architecture (Core/Application/Web)
- Database: SQLite with EF Core
- Instruments: Guitarra, Bandolim, Cavaquinho, Baixo, Acordeão, Percussão

## DELIVERABLES

### 1. DATABASE LAYER

Create in `src/RTUB.Core/`:
- **Enums/InstrumentType.cs** (6 instruments with Portuguese Display names)
- **Entities/InstrumentTuning.cs**: 
  - Id, InstrumentType, Name, TuningNotes (JSON string), IsDefault
  - Inherits from BaseEntity

Update `src/RTUB.Application/Data/ApplicationDbContext.cs`:
- Add `DbSet<InstrumentTuning>`
- Configure JSON column for TuningNotes (SQLite compatible)
- Seed default tunings: 
  - Guitarra: ["E","A","D","G","B","E"]
  - Bandolim:  ["G","D","A","E"]
  - Cavaquinho:  ["D","G","B","D"]
  - Baixo:  ["E","A","D","G"]
  - Acordeão: ["C"] (reference)
  - Percussão:  [] (no tuning)
- Create migration "AddInstrumentTuner"

### 2. BACKEND SERVICES

Create `src/RTUB.Application/Interfaces/ITunerService.cs`:
```csharp
Task<IEnumerable<InstrumentTuning>> GetTuningPresetsAsync(InstrumentType instrument, CancellationToken ct = default);
PitchInfo ValidatePitch(double frequency, double referencePitch = 440);
```

Create `src/RTUB.Application/DTOs/PitchInfo.cs`:
- string NoteName
- int Octave
- double Frequency
- double CentsOffset
- bool InTune

Create `src/RTUB.Application/Services/TunerService.cs`:
- Implement pitch-to-note conversion:  `noteNumber = 12 × log2(freq/440) + 69`
- Calculate cents offset: `1200 × log2(actual/target)`
- InTune = |centsOffset| <= 5
- Use IConfiguration for settings

Add to `appsettings.json`:
```json
"Tuner": {
  "ReferenceFrequency": 440,
  "ToleranceCents": 5
}
```

### 3. JAVASCRIPT PITCH DETECTION

Create `src/RTUB.Web/wwwroot/js/tuner.js`:
```javascript
export class TunerEngine {
    async start() { 
        // Web Audio API + microphone access
        // Create AudioContext and AnalyserNode
        // FFT size: 2048, sample rate: 44100 Hz
    }
    
    stop() { 
        // Cleanup audio streams and context
    }
    
    detectPitch() { 
        // Autocorrelation algorithm
        // Return {frequency, clarity}
        // Range: 80-1200 Hz
        // Clarity threshold: 0.9
    }
    
    onPitchDetected(callback) {
        // Register callback for pitch updates
    }
}
```

### 4. BLAZOR COMPONENT

Create `src/RTUB.Web/Components/Learning/Tuner.razor`:

**Injections:**
- ITunerService
- IJSRuntime
- Implement IAsyncDisposable

**UI Elements:**
- Instrument dropdown (InstrumentType enum)
- Tuning preset dropdown (from database)
- Large note display (3rem font)
- Frequency display (1rem)
- Needle indicator (SVG/CSS, -50 to +50 cents range)
- Color coding:  Green (<5 cents), Yellow (5-10), Red (>10)
- String indicator showing expected notes from tuning
- Start/Stop button (minimum 44px touch target)

**JS Interop:**
- Load tuner.js module in OnInitializedAsync
- Pass DotNetObjectReference to JavaScript
- Create [JSInvokable] callback method for pitch updates
- Proper cleanup in DisposeAsync

**Animations:**
- Smooth needle transitions (0.1s CSS transition)

### 5. PAGE & NAVIGATION

Create `src/RTUB.Web/Components/Pages/Learning/TunerPage.razor`:
```razor
@page "/learning/tuner"
@attribute [Authorize]

<PageTitle>Afinador - RTUB</PageTitle>

<div class="container mt-4">
    <h2>Afinador de Instrumentos</h2>
    <p class="text-muted">
        Afine o seu instrumento usando o microfone.  
        Para melhores resultados, use um ambiente silencioso.
    </p>

    <Tuner />

    <div class="mt-4 alert alert-info">
        <h5>Dicas: </h5>
        <ul>
            <li>Toque cada corda individualmente</li>
            <li>Aguarde o indicador estabilizar antes de afinar</li>
            <li>Verde = afinado, Amarelo = quase, Vermelho = desafinado</li>
        </ul>
    </div>
</div>
```

**Update navigation** (find main nav component in src/RTUB.Web/Components/Layout/):
- Add "Aprender" menu dropdown
- Add "Afinador" link: 
  - Icon: bi-music-note-beamed
  - Href: /learning/tuner
  - Text: Afinador

### 6. SERVICE REGISTRATION

Create or update `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`:
```csharp
public static IServiceCollection AddLearningServices(this IServiceCollection services)
{
    services.AddScoped<ITunerService, TunerService>();
    return services;
}
```

Update `src/RTUB.Web/Program.cs`:
```csharp
builder.Services.AddLearningServices();
```

## ACCEPTANCE CRITERIA

✅ Migration runs successfully  
✅ Tuner converts 440Hz → A4 correctly  
✅ Real-time pitch detection works (±5 cents accuracy)  
✅ Page accessible at /learning/tuner  
✅ Requires authentication  
✅ Navigation link visible to authenticated users  
✅ Works on mobile (tested with Chrome DevTools)  
✅ No memory leaks (proper disposal implemented)  
✅ All text in Portuguese  
✅ No console errors  

**Estimated Time:** 60 minutes