# MyTuno Performance Optimization Plan

## Problem
With 2-3 concurrent players, MyTuno pages feel slow. Root cause: excessive sequential DB round-trips on hot paths.

## Bottleneck Summary

| # | Severity | Issue | Queries Saved |
|---|----------|-------|---------------|
| 1 | **CRITICAL** | 13+ sequential inventory queries on page load → batch into 1 | ~12 |
| 2 | **CRITICAL** | Individual `AddItemAsync` + `SaveChangesAsync` per item drop → batch | ~10-40 per reward |
| 3 | **HIGH** | `LoadUpgradeCosts` loads character 5 times → compute from already-loaded character | ~5 |
| 4 | **HIGH** | `LoadDataAsync` waterfall → `Task.WhenAll` for independent calls | Latency cut ~60-70% |
| 5 | **HIGH** | Redundant character loads in `GetCurrentEnergyAsync`, `Use*Async` methods | ~3-5 |
| 6 | **MEDIUM** | No caching for config/picture lookups (rarely change) | ~3 per page load |
| 7 | **HIGH** | Double character save in arena reward flow | ~1 wasted SaveChanges |
| 8 | **HIGH** | 2 sequential inventory checks on arena death → batched | ~1 |
| 9 | **MEDIUM** | Separate FITAB drop call after consumable drops → merged | ~1 |
| 10 | **MEDIUM** | Sequential loads in BossMode/SurviveMode/AllCharacters sub-pages | Latency cut ~50% |

## Implementation Progress

### Fix 1: Batch Inventory Queries ✅
- [x] Add `GetAllUpgradeCosts` method to `IUpgradeService` / `UpgradeService` (sync, zero DB queries)
- [x] Add `GetUserInventorySummaryAsync` to `IInventoryService` / `InventoryService` (1 query → all quantities)
- [x] Refactor `MyTunoHome.razor` `LoadDataAsync` to use batched methods
- [x] Verify existing tests pass (121/121)

### Fix 2: Batch AddItemsAsync for Rewards ✅
- [x] Add `AddItemsAsync(userId, Dictionary<type, qty>)` to `IInventoryRepository` / `InventoryRepository`
- [x] Refactor `BattleService.TryDropConsumablesAsync` to use batch method
- [x] Refactor `StageService.ApplyRunRewardsAsync` to use batch method
- [x] Refactor `BossModeService.ApplyBossRunRewardsAsync` to use batch method
- [x] Refactor `SurviveModeService.ApplyRunRewardsAsync` to use batch method
- [x] Verify existing tests pass (121/121)

### Fix 3: Parallelize LoadDataAsync ✅
- [x] Group 7 independent calls into `Task.WhenAll` batch
- [x] Extract inventory summary, upgrade costs, energy from parallel results
- [x] Verify page data still loads correctly

### Fix 4: Cache Config Data ✅
- [x] Add `IMemoryCache` to `ItemTypeConfigService` for `GetAllConfigsAsync` / `GetAllForgeComboPicturesAsync` (10-min TTL)
- [x] Add cache invalidation on all write methods (Update, UploadPicture, RemovePicture, UploadForgeCombo, RemoveForgeCombo)
- [x] Add `IMemoryCache` to `NaipeService` for `GetAllTypeConfigsAsync` (10-min TTL)
- [x] Add cache invalidation on `UpdateTypeConfigAsync` and `UploadTypeConfigPictureAsync`
- [x] Verify existing tests pass (121/121)

### Fix 5: Fix Redundant Character Loads ✅
- [x] Add `GetCurrentEnergyForCharacterAsync(Character)` overload to `IInventoryService` / `InventoryService`
- [x] Use overload in `MyTunoHome.razor` `LoadDataAsync` to avoid duplicate character load
- [x] Verify existing tests pass (121/121)

### Fix 6: Eliminate Double Character Save in Arena ✅
- [x] Remove `_characterRepository.UpdateAsync(character)` from `ApplyRewardsAsync` (caller saves)
- [x] Verify existing tests pass (131/131)

### Fix 7: Batch Healing-Item Check on Arena Death ✅
- [x] Replace 2 sequential `GetItemAsync` calls with single `GetUserInventoryAsync` in `FinalizeAndApplyRewardsAsync`
- [x] Verify existing tests pass (131/131)

### Fix 8: Merge FITAB Drop into Consumable Drop Flow ✅
- [x] Move FITAB roll logic into `TryDropConsumablesAsync`, remove `TryDropFitabAsync`
- [x] Verify existing tests pass (131/131)

### Fix 9: Parallelize Sub-Page Loads ✅
- [x] Parallelize `CharacterService.GetOrCreateCharacterAsync` + `BossModeService.GetOrCreateBossModeProgressAsync` in `BossMode.razor`
- [x] Parallelize `CharacterService.GetOrCreateCharacterAsync` + `SurviveModeService.GetOrCreateProgressAsync` in `SurviveMode.razor`
- [x] Parallelize `GetAllCharactersOrderedByLevelAsync` + `StageProgresses` query in `AllCharacters.razor`
- [x] Verify existing tests pass (131/131)

## Files Modified
- `src/RTUB.Application/Interfaces/IUpgradeService.cs` — added `GetAllUpgradeCosts(Character)`
- `src/RTUB.Application/Services/UpgradeService.cs` — implemented `GetAllUpgradeCosts`
- `src/RTUB.Application/Interfaces/IInventoryService.cs` — added `GetUserInventorySummaryAsync`, `GetCurrentEnergyForCharacterAsync`
- `src/RTUB.Application/Services/InventoryService.cs` — implemented summary + energy-from-character methods
- `src/RTUB.Application/Interfaces/IInventoryRepository.cs` — added `AddItemsAsync(dict)` batch method
- `src/RTUB.Application/Repositories/InventoryRepository.cs` — implemented batch `AddItemsAsync`
- `src/RTUB.Application/Services/BattleService.cs` — refactored `TryDropConsumablesAsync` → batch; removed double save from `ApplyRewardsAsync`; batched healing-item check; merged FITAB drop
- `src/RTUB.Application/Services/StageService.cs` — refactored `ApplyRunRewardsAsync` → batch
- `src/RTUB.Application/Services/BossModeService.cs` — refactored `ApplyBossRunRewardsAsync` → batch
- `src/RTUB.Application/Services/SurviveModeService.cs` — refactored `ApplyRunRewardsAsync` → batch
- `src/RTUB.Application/Services/ItemTypeConfigService.cs` — added `IMemoryCache` with invalidation
- `src/RTUB.Application/Services/NaipeService.cs` — added `IMemoryCache` with invalidation
- `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor` — rewrote `LoadDataAsync` (parallel + batched)
- `src/RTUB.Web/Pages/MyTuno/BossMode.razor` — parallelized character + progress loads
- `src/RTUB.Web/Pages/MyTuno/SurviveMode.razor` — parallelized character + progress loads
- `src/RTUB.Web/Pages/MyTuno/AllCharacters.razor` — parallelized characters + stage map loads
- `tests/RTUB.Application.Tests/Services/NaipeServiceTests.cs` — added `IMemoryCache` to constructor
