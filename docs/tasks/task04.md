**Copilot Agent Command Task — Add new stat: DEFENSE (damage reduction + scaling config + combat calc)**

Add a new character stat called **DEFENSE** alongside **HP, SPEED, POWER, CRIT**. DEFENSE must be fully supported end-to-end (stats aggregation, UI, config scaling, persistence if applicable, and combat calculations). Higher DEFENSE ⇒ **less damage taken**. Rework damage calculations to include DEFENSE in a clear, centralized way.

---

## Requirements

### 1) Data model + stat pipeline

Add **Defense** everywhere the other stats exist:

* Base stats / character stats model
* Derived/final stats calculation (base + gear + buffs + scaling)
* Any DTOs/view models used by Arena/Stage/CPU challenge snapshot builder
* UI display wherever HP/SPEED/POWER/CRIT are shown

Defense should be treated the same as the other stats (including rounding rules, serialization, etc.).

---

### 2) Config-driven scaling (same style as other stats)

Update the scaling config file so DEFENSE can scale just like HP/SPEED/POWER/CRIT.

* Add `DefenseGrowthPerLevel` (or per stage, depending on your system)
* If you already have a generic “stat growth dictionary”, extend it with `Defense`.

**Acceptance**

* Changing the config changes DEFENSE scaling without code changes.

---

### 3) Damage formula including DEFENSE

Implement **one centralized damage function** (used by Arena, Stage, CPU challenges) that applies DEFENSE consistently.

Use a deterministic, safe damage reduction curve (no negative damage, no invulnerability unless intentionally allowed). Recommended:

#### Option A (recommended): diminishing returns

* Let `rawDamage` be the damage after POWER/skill multipliers and before mitigation.
* Let `def = target.Defense`.
* Let `K` be a tunable constant from config (e.g., 50, 100).

**Damage multiplier:**

* `mult = K / (K + def)`
  **Final damage:**
* `finalDamage = max(1, floor(rawDamage * mult))`

Add config:

* `DefenseK` (controls how strong defense is)
* `MinDamage` (e.g., 1)
* Optional `MaxDamageReduction` cap (e.g., 0.90) if you prefer hard caps

#### Option B: percent reduction (only if you already model stats as %)

* `reduction = clamp(defensePercent, 0, maxReduction)`
* `finalDamage = max(minDamage, floor(rawDamage * (1 - reduction)))`

Pick one approach and apply it everywhere. Option A is harder to break and scales nicely.

---

### 4) Arrange combat calculations (order of operations)

Update the attack resolution order to be consistent:

1. **Base attack power** (POWER, skill base damage, weapon, etc.)
2. **Multipliers** (skill modifiers, buffs/debuffs)
3. **Critical strike** (if crit triggers, apply crit multiplier here)
4. **DEFENSE mitigation** (apply target defense to reduce damage)
5. **Clamp & apply** (min damage, rounding, update HP)

**Important:** DEFENSE must reduce crit damage too (unless intentionally excluded—don’t exclude).

---

### 5) CPU Challenge + Stage integration

Ensure DEFENSE is included in:

* CPU snapshot builder (uses owner’s build stats; resets HP/resources as usual)
* Enemy/boss stat generation and stage scaling
* Any tooltips/stat summaries

---

### 6) Tests (required)

Add unit tests (seed RNG where needed):

1. **Defense reduces damage**

* With same rawDamage, higher DEFENSE ⇒ lower finalDamage.

2. **Ordering correctness**

* Ensure crit is applied before defense mitigation (crit damage still reduced by defense).

3. **Bounds**

* Damage never goes below `MinDamage`.
* With DEFENSE=0, multiplier ≈ 1 (finalDamage ≈ rawDamage).

4. **Config scaling**

* Changing scaling config modifies computed DEFENSE.

---

## Acceptance Criteria

* DEFENSE appears everywhere other stats do (UI + models + snapshots + scaling).
* Higher DEFENSE reliably reduces incoming damage with the chosen formula.
* Combat damage uses a single centralized calculation shared by all modes.
* Unit tests cover mitigation, ordering, and bounds.
