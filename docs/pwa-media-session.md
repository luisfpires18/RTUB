# PWA Media Session - Next/Previous Lockscreen Controls

## Overview

This feature implements lockscreen/Control Center media controls (Previous/Next) for the RTUB music player, **exclusively when running as an installed PWA** (Progressive Web App). 

When running as a normal web app in a browser tab, the behavior remains unchanged to avoid any regressions.

## Implementation Details

### Components

1. **`pwaMediaSession.js`** - Client-side JavaScript module
   - Detects PWA mode (standalone display mode)
   - Manages playback queue
   - Implements Media Session API handlers
   - Handles Next/Previous logic entirely client-side
   - Updates position state for lockscreen display

2. **`MediaQueueService.cs`** - C# service for queue management
   - Builds ordered queue from album tracks
   - Handles Next/Previous navigation logic
   - Implements 3-second restart rule for Previous button
   - Well-tested with 13 unit tests

3. **`MediaSessionInterop.cs`** - Blazor-JavaScript interop
   - Provides C# API to interact with pwaMediaSession.js
   - PWA mode detection
   - Queue management
   - Metadata updates

4. **`Songs.razor`** - Updated page component
   - Detects PWA mode on page load
   - Initializes PWA Media Session when user first plays a song (when audio element is created)
   - Builds queue and binds handlers on first play
   - Pre-loads audio URLs for all tracks
   - Sends queue to JavaScript module

### Behavior

#### Queue Management
- When a user clicks "Play" on a track (e.g., track #7):
  - All tracks in the album are added to the queue
  - Tracks are ordered by track number
  - Current track index is set to the played track
  - Audio URLs are pre-loaded for smooth transitions

#### Next Button
- Advances to the next track in the queue
- Stops playback when reaching the end (no wrap-around)
- Updates metadata (title, artist, album, artwork)

#### Previous Button
- **If current playback time > 3 seconds**: Restarts the current track
- **If current playback time ≤ 3 seconds**: Goes to the previous track
- **If at start of queue**: Restarts the current track

#### PWA-Only Activation
The feature only activates when:
```javascript
// Standard display mode check
window.matchMedia('(display-mode: standalone)').matches === true

// OR iOS Safari fallback
window.navigator.standalone === true
```

In normal browser mode, none of the PWA Media Session handlers are registered.

### Technical Highlights

- **No SignalR dependency for Next/Previous**: Track changes happen entirely client-side for better performance
- **Automatic position state updates**: Duration, position, and playback rate are updated via audio element event listeners
- **Graceful degradation**: If Media Session API is not available, playback continues normally
- **URL caching**: Pre-signed S3 URLs are cached to minimize API calls
- **Lazy initialization**: Media Session handlers are initialized only when the first song is played (when audio element exists in DOM), ensuring proper binding

## Testing

### Automated Tests

13 unit tests validate the queue management logic:
```bash
cd tests/RTUB.Web.Tests
dotnet test --filter "MediaQueueServiceTests"
```

All tests verify:
- Queue building and ordering
- Next/Previous navigation
- 3-second restart rule
- Edge cases (start/end of queue, null track numbers)

### Manual Testing

#### iOS (Add to Home Screen)
1. Open https://rtub.azurewebsites.net in Safari
2. Tap Share → Add to Home Screen
3. Open the installed app
4. Navigate to Music → [Album] → Play a song
5. Lock the screen
6. **Expected**: Lockscreen shows Previous/Next buttons
7. Test Previous (< 3s and > 3s scenarios)
8. Test Next through multiple tracks

#### Android (TWA via PWA Builder)
1. Install the TWA app from Google Play (when published)
2. Open the app
3. Navigate to Music → [Album] → Play a song
4. Lock the screen or pull down notification shade
5. **Expected**: Media notification shows Previous/Next buttons
6. Test navigation

#### Web Mode (Regression Check)
1. Open https://rtub.azurewebsites.net in any browser (regular tab)
2. Navigate to Music → [Album] → Play a song
3. Lock the screen
4. **Expected**: Only Play/Pause controls (no Next/Previous)
5. Playback continues normally

## Known Limitations

### iOS Limitations
- **Inconsistent behavior**: iOS may not always show Previous/Next buttons consistently, even with proper Media Session API implementation
- **App switching**: Switching apps while playing may cause controls to disappear temporarily
- **First launch**: May require restarting playback after installing PWA

### General
- **No shuffle/repeat**: This implementation does not include shuffle or repeat modes (can be added later)
- **Single album context**: Queue is limited to the current album only
- **Audio format**: Only MP3 files are supported (based on existing implementation)

## Architecture Decisions

### Why Client-Side Track Loading?
Originally, the implementation considered using Blazor callbacks for Next/Previous, but this would introduce SignalR latency. By handling everything in JavaScript, track changes are instant and more responsive.

### Why Pre-load URLs?
S3 pre-signed URLs are generated on-demand. Pre-loading all URLs when building the queue ensures smooth transitions between tracks without waiting for server round-trips.

### Why MediaQueueService?
Extracting queue logic into a separate C# service allows for:
- Thorough unit testing
- Reusability across different components
- Clear separation of concerns
- Future enhancements (e.g., shuffle, cross-album playlists)

## Future Enhancements

Potential improvements for future iterations:

1. **Shuffle Mode**: Randomize playback order
2. **Repeat Mode**: Loop single track or entire queue
3. **Cross-Album Playlists**: Create custom playlists across multiple albums
4. **Background Playback**: Ensure playback continues when app is in background (may require native capabilities)
5. **Seeking**: Implement seekbackward/seekforward with better UI feedback

## Files Changed

### New Files
- `src/RTUB.Web/wwwroot/js/pwaMediaSession.js`
- `src/RTUB.Web/Services/MediaQueueService.cs`
- `tests/RTUB.Web.Tests/Services/MediaQueueServiceTests.cs`
- `docs/pwa-media-session.md` (this file)

### Modified Files
- `src/RTUB.Web/Interop/MediaSessionInterop.cs` - Added PWA-specific methods
- `src/RTUB.Web/Pages/Media/Songs.razor` - Integrated PWA queue and handlers
- `src/RTUB.Web/Shared/MainLayout.razor` - Added pwaMediaSession.js script reference
- `src/RTUB.Web/Program.cs` - Registered MediaQueueService

## References

- [Media Session API - MDN](https://developer.mozilla.org/en-US/docs/Web/API/Media_Session_API)
- [PWA Best Practices](https://web.dev/learn/pwa/)
- [iOS PWA Limitations](https://firt.dev/notes/pwa-ios/)
