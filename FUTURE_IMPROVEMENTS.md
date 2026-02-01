# Future Improvements for My Tuno

## Code Review Suggestions for Future Enhancements

### 1. Configuration Validation
**Current State**: Configuration defaults in MyTunoHome.razor must manually match appsettings.json  
**Suggestion**: Implement configuration validator or shared configuration class  
**Benefits**: Prevents sync issues between fallback values and configured values  
**Priority**: Medium

**Implementation Options**:
```csharp
// Option 1: Configuration class with validation
public class MyTunoOptions
{
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "...";
    public string NextFeatures { get; set; } = "...";
    
    public void Validate()
    {
        if (string.IsNullOrEmpty(Version))
            throw new InvalidOperationException("MyTuno:Version is required");
    }
}

// Option 2: Startup validation
services.Configure<MyTunoOptions>(Configuration.GetSection("MyTuno"));
services.AddOptions<MyTunoOptions>()
    .Validate(opts => !string.IsNullOrEmpty(opts.Version));
```

### 2. Fisher-Yates Algorithm Documentation
**Current State**: Algorithm documented, but filtering logic rationale could be clearer  
**Suggestion**: Add comment explaining two-stage approach (filter → shuffle)  
**Benefits**: Better understanding of matchmaking algorithm  
**Priority**: Low

**Example Comment**:
```csharp
// Two-stage matchmaking algorithm:
// 1. Filter candidates by level range (±3 or ±5) to ensure balanced matches
// 2. Apply Fisher-Yates shuffle to filtered list for fair random selection
// This ensures both competitive balance AND variety in opponent selection
```

### 3. Generic HP Update Method Name
**Current State**: `ApplyAttackerHPChangesAsync` is specific to current AI vs Player battles  
**Suggestion**: Rename to `ApplyBattleHPChangesAsync` for easier PvP extension  
**Benefits**: More generic method name ready for PvP implementation  
**Priority**: Low (current name is clear and accurate)

**Migration Path**:
```csharp
// Future PvP implementation
private async Task ApplyBattleHPChangesAsync(
    Character attacker, 
    Character defender,  // Currently not updated
    CombatResult combatResult)
{
    // Update both combatants when PvP is enabled
    attacker.CurrentHP = combatResult.AttackerFinalHP;
    defender.CurrentHP = combatResult.DefenderFinalHP;
    
    await _characterRepository.UpdateAsync(attacker);
    await _characterRepository.UpdateAsync(defender);
}
```

## Planned Features (from Summary)

### High Priority
1. **HP Regeneration**: Passive HP recovery over time (e.g., 10% per hour)
2. **Healing Items**: Inventory items that restore HP (consumables)
3. **PvP Support**: Extend persistent HP to both players in battles

### Medium Priority
4. **HP Upgrades**: Allow increasing max HP in upgrade shop
5. **Battle Difficulty Scaling**: Reward scaling based on opponent's remaining HP
6. **HP Milestones**: Achievements for surviving with low HP (e.g., "Last Stand" < 10% HP)

### Low Priority
7. **Dynamic Revival Cost**: Scale based on HP deficit, not just level
8. **Battle History HP Tracking**: Show HP before/after in battle log

## Testing Recommendations

### Unit Tests Needed
- [ ] Character.TakeDamage() edge cases
  - Negative damage input (should throw?)
  - Damage exceeding current HP
  - Damage when already at 0 HP
  
- [ ] Character.Heal() edge cases
  - Negative healing input (should throw?)
  - Healing beyond max HP
  - Healing when already at max HP
  
- [ ] GetRandomOpponentsAsync scenarios
  - 0 available opponents
  - 1-3 opponents (less than requested)
  - 10+ opponents (more than requested)
  - All opponents same level
  - All opponents very different levels

- [ ] Revival cost calculation
  - Level 1 character cost (10 Fidelis)
  - Level 10 character cost (100 Fidelis)
  - Edge cases (level 0, negative level?)

### Integration Tests Needed
- [ ] End-to-end battle flow with HP persistence
- [ ] Revival mechanism (Fidelis deduction + HP restoration)
- [ ] Arena blocking when character is defeated
- [ ] Random opponent selection maintains level filtering

### Performance Tests Needed
- [ ] Fisher-Yates shuffle with 1000+ characters
- [ ] Battle history pagination with large datasets
- [ ] HP bar rendering performance on mobile

## Documentation Updates Needed

### Developer Onboarding
- [ ] Add migration guide to README
- [ ] Document revival mechanism for new developers
- [ ] Create architecture diagram showing HP flow

### User Documentation
- [ ] Help modal explaining HP system
- [ ] Tutorial for first-time players
- [ ] FAQ section for common questions

## Security Considerations

### Current Implementation
- ✅ Revival cost server-side validated
- ✅ HP updates only through BattleService
- ✅ Character ownership verified before operations

### Future Hardening
- [ ] Rate limiting on revival requests (prevent spam)
- [ ] Audit logging for HP changes
- [ ] Anti-cheat validation (HP never increases without heal/revival)

## Monitoring & Analytics

### Metrics to Track
- Average battles before character defeat
- Revival frequency per character level
- Fidelis spend on revivals vs upgrades
- Opponent selection distribution (verify randomness)

### Alerts to Configure
- Unusually high revival rates (possible exploit)
- Low HP persistence (database issues)
- Long opponent selection times (performance degradation)

---

**Last Updated**: February 1, 2026  
**Status**: Roadmap for post-MVP improvements  
**Priority**: Review quarterly and implement based on user feedback
