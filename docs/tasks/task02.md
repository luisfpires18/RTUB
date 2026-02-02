**Copilot Agent Command Task — “CPU Challenges” must use a fresh snapshot + no rewards**

We have a mode where another user can fight my character while I’m offline (it’s my character acting as a CPU). Currently this incorrectly uses my *live/persisted* state: if I left at **54/110 HP**, challengers see **54/110** and fight that damaged state. Also, if challengers win, they currently receive rewards (XP/items/etc). Fix this so “battle vs someone’s CPU” behaves like a proper CPU encounter.

### Requirements

#### 1) CPU opponent must start at FULL HEALTH (and full resources)

* When a player challenges another player’s “CPU character”, the opponent must be built from a **fresh combat snapshot** derived from that player’s build (stats/gear/skills), but:

  * `CurrentHP` must start at `MaxHP` (e.g., 110/110)
  * Any other current resources (mana/energy/shields/etc) must reset to their max/default
  * No carry-over of last known combat HP/status effects/cooldowns

**Important:** Do not read or reuse any “current combat state” persisted for the player (like last HP). Only use stable build data (level, equipped items, base stats, abilities).

#### 2) Never persist CPU combat changes back to the owner player

* Damage taken, deaths, cooldown usage, etc. during a CPU defense battle must **not** be written back to the owner player.
* After the match ends, the owner player’s stored state must remain unchanged.

#### 3) Challengers get NO rewards for beating a CPU

* If a player fights a CPU version of another player and wins:

  * **No XP**
  * **No items**
  * **No gold/currency**
  * **No quest progress**
  * **No stats progression**
* (Optional if you already track it): allow only non-economic outcomes like “win/loss record” or “rank/elo” *only if explicitly intended*; otherwise also disable.

#### 4) Owner player also gets NO rewards for being a CPU opponent

* The defending player (whose character is used as CPU) must not gain XP/items/etc regardless of outcome.

#### 5) Make the battle type explicit and enforce rules centrally

Introduce a match/battle flag like:

* `BattleType = PVE_STAGE | PVE_ARENA | CPU_CHALLENGE | ...`

Then enforce:

* Snapshot reset rules apply when `BattleType == CPU_CHALLENGE`
* Reward pipeline returns **empty rewards** when `BattleType == CPU_CHALLENGE`
* Persistence pipeline never writes opponent combat state for `CPU_CHALLENGE`

#### 6) UI correctness

* The challenger should see the CPU opponent as starting full (e.g., `110/110` at match start).
* No UI should display stale HP (54/110) coming from the owner player’s last session.

### Implementation Notes

* Create a `CpuOpponentSnapshotBuilder` (or similar) that takes:

  * owner player’s build/loadout data
  * produces a `CombatantSnapshot` with **full HP/resources**
* Ensure the match engine uses the snapshot and never binds opponent to the persisted player object.

### Tests (required)

Add unit tests/integration tests to prevent regression:

1. **Snapshot health reset**

* Given owner player stored “current HP = 54” (or last known combat HP), CPU opponent snapshot starts at `MaxHP` and ignores 54.

2. **No persistence**

* Running a CPU challenge match must not modify owner player records (HP, cooldowns, etc).

3. **No rewards**

* Winner of `CPU_CHALLENGE` receives zero XP/items/currency.
* Owner player receives zero XP/items/currency.

### Acceptance Criteria

* Challenging someone’s CPU always starts them at **full HP/resources**.
* Beating a CPU grants **no rewards** to anyone.
* Owner player’s state is **never** affected by CPU defense battles.
* All tests pass.
