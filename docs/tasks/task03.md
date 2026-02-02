**Copilot Agent Command Task — Investigate & Fix Critical Strike Chance (too many crits)**

Players report getting far too many critical hits. A configured crit chance like **3%** appears to behave like **~30%**. Investigate the full crit calculation pipeline, identify the root cause, and fix it so crit frequency matches configuration.

### Requirements

#### 1) Locate all crit chance sources & conversions

Audit the entire path that produces a crit:

* Config values (e.g., `appsettings`, DB fields, item stats, buffs)
* Derived stats aggregation (base + gear + buffs + level scaling)
* Combat roll logic (random roll + threshold)
* Any UI display formatting vs internal representation (percent vs fraction)

Specifically look for classic bugs:

* **Percent vs fraction mismatch** (3 vs 0.03)
* **Double scaling** (multiplying by 100 twice or dividing twice)
* **Incorrect RNG threshold** (`rng.Next(0,100) < 0.03` always false, or `rng.NextDouble() < 3` always true-ish depending on clamp)
* **Integer truncation / rounding** turning 0.03 into 0 or 3 into 3
* **Using 0–1 RNG but comparing to 0–100 value** (or the reverse)
* **Misinterpreted “3” as 3x multiplier instead of 3%**
* Multiple independent crit rolls per attack (e.g., per hit + per effect) being counted as “one attack crits too much”

#### 2) Make crit representation unambiguous and consistent

Choose one canonical internal representation and enforce it everywhere:

* Either **fraction** in `[0..1]` (recommended)
  Example: 3% = `0.03`
* Or **percent** in `[0..100]`
  Example: 3% = `3.0`

Then:

* Normalize all sources into that representation at stat-build time.
* Ensure UI formatting converts appropriately without feeding back into combat logic.

#### 3) Fix combat roll to match the chosen representation

Ensure crit is rolled exactly once per attack (unless intentionally multi-hit), using the correct RNG scale:

* If using fraction `[0..1]`: `NextDouble() < critChance`
* If using percent `[0..100]`: `(NextDouble() * 100) < critChance`

Clamp final crit chance to safe bounds:

* `0 <= critChance <= critCap` (critCap configurable, e.g., 0.75)

#### 4) Add logging / temporary instrumentation for verification

Add a debug-only or feature-flagged log capturing:

* computed crit chance (raw + clamped)
* RNG roll value
* crit outcome
* attack identifier (so it’s easy to sample)

Remove or disable by default once verified.

#### 5) Tests (required)

Add deterministic tests by injecting a seeded RNG or RNG abstraction:

* **Conversion tests**

  * Config 3% becomes `0.03` (or 3.0 if percent-scale chosen), no double conversion.

* **Roll tests**

  * With critChance = 0.03, over a large deterministic sample (e.g., 100k rolls) observed rate is within tolerance (e.g., 2.7%–3.3%).
  * Boundary tests: 0% never crits, 100% always crits (or critCap behavior).

* **Single-roll per attack**

  * Verify exactly one crit evaluation occurs per attack event (unless explicitly multi-hit).

### Acceptance Criteria

* A configured **3%** crit chance behaves like **~3%** in practice (verified via tests and/or instrumentation).
* No accidental percent/fraction mismatch remains in code.
* Crit logic is centralized, unit-tested, and consistent across Arena/Stage/CPU modes.
