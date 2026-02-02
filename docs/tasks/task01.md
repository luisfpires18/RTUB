## Copilot Agent Command Task — **my_tuno + arena + stage mode**

Use the existing **Arena mode** implementation as the source of truth for layout + responsiveness, and bring **Stage mode** to parity (no horizontal scrolling on mobile, fits viewport). Keep changes aligned with the project’s clean architecture and testing expectations. 

---

### 1) Leaderboard tab active separator color → purple (not blue)

**Goal:** the active tab/pill highlight (and any “separator” look caused by active background) must be **purple**.

**Do:**

* Find the leaderboard tabs CSS (likely a scoped CSS file next to the leaderboard component/page, or `site.css/app.css`).
* Override the active pill background color to purple **in the leaderboard scope**, not globally.
* Prefer setting Bootstrap’s CSS variable if available in that scope; otherwise override the selector.

**Patch (choose one approach):**

```css
/* Preferred: scoped override using Bootstrap variable (leaderboard container wrapper assumed) */
.leaderboard {
  --bs-nav-pills-link-active-bg: #6f42c1; /* purple */
}

/* Fallback: explicit override */
.leaderboard .nav-pills .nav-link.active,
.leaderboard .nav-pills .show > .nav-link {
  background-color: #6f42c1 !important;
}
```

**Acceptance checks**

* Active tab is purple on desktop + mobile.
* No other pages’ `.nav-pills` are affected.

---

### 2) Stage Mode mobile layout: identical fit behavior to Arena mode

**Goal:** Stage mode should **not** horizontally scroll on mobile, and should visually behave like Arena mode (same responsive container rules, same scaling for sprites/board, etc.).

**Do:**

* Diff Arena vs Stage markup/CSS.
* Make Stage reuse the same container structure + responsive CSS rules as Arena.
* Ensure:

  * outer wrapper uses `width: 100%` / `max-width: 100%` and avoids fixed pixel widths
  * sprite images use `max-width: 100%`, `height: auto`
  * any horizontal overflow is prevented at the correct container level (don’t just slap `overflow-x:hidden` on `body` unless Arena does)
  * use `flex-wrap` where Arena wraps
  * avoid `100vw` pitfalls that cause overflow due to scrollbar; prefer `width:100%` unless you know what you’re doing

**Acceptance checks**

* On a ~375px wide viewport: Stage mode has **zero** horizontal scroll.
* Arena and Stage look consistent (same spacing and scaling logic).
* No regressions in desktop layout.

---

### 3) Stage system: biome folders + random enemy selection + config-driven stage rules

**Goal:** Implement a scalable “infinite” stage system that:

* Picks random **non-boss** enemy sprites from biome folders
* Uses `appsettings` to configure rules (enemy counts per stage range, stat scaling, biome thresholds)
* Boss stages use deterministic boss sprite names (`boss_*`), no randomness

#### 3.1 Biome folders

* Forest: `wwwroot/sprites/games/my-tuno/enemies/forest`
* Desert: `wwwroot/sprites/games/my-tuno/enemies/desert`
* Future biomes follow the same convention.

**Rules**

* Random selection must **exclude** filenames starting with `boss_`.
* Boss stages pick boss sprites by name pattern (e.g., `boss_1_bear`, etc.) — no random selection needed for bosses.

#### 3.2 Stage-to-enemy-count rules (configurable)

Implement these as configuration (don’t hardcode), with this intended mapping:

* Stage 1–2: 1 enemy
* Stage 3–4: 2 enemies
* Stage 5–6: 3 enemies
* Stage 7–8: 4 enemies
* Stage 9: 5 enemies
* Stage 10: boss
* Repeat the same pattern per “decade” (11–19 same, 20 boss, …) up to 100 (final Forest boss), then biome switches.

*(This resolves the overlap in the original text by treating “7–8 => 4” and “9 => 5”, with boss on 10; implement via config so it’s adjustable.)*

#### 3.3 Biome progression

* Forest: stages **1–100**
* Desert: stages **101–200**
* Next biomes can be added later using config (e.g., 201+)

#### 3.4 Enemy stat scaling (infinite scaling)

* Each stage increases enemy and boss stats.
* Put scaling parameters in `appsettings` (e.g., HP multiplier, damage multiplier, armor multiplier, maybe growth curve).
* Keep scaling deterministic and testable.

**Example config shape (you can adjust names, but keep it structured):**

```json
"MyTunoStageMode": {
  "Biomes": [
    {
      "Name": "Forest",
      "StageMin": 1,
      "StageMax": 100,
      "EnemySpritePath": "sprites/games/my-tuno/enemies/forest",
      "BossSpritePrefix": "boss_"
    },
    {
      "Name": "Desert",
      "StageMin": 101,
      "StageMax": 200,
      "EnemySpritePath": "sprites/games/my-tuno/enemies/desert",
      "BossSpritePrefix": "boss_"
    }
  ],
  "EncounterRules": {
    "BossEveryNStages": 10,
    "EnemyCountByStageOffset": [
      { "From": 1, "To": 2, "Count": 1 },
      { "From": 3, "To": 4, "Count": 2 },
      { "From": 5, "To": 6, "Count": 3 },
      { "From": 7, "To": 8, "Count": 4 },
      { "From": 9, "To": 9, "Count": 5 }
    ]
  },
  "Scaling": {
    "HpGrowthPerStage": 0.06,
    "DamageGrowthPerStage": 0.05,
    "BossMultiplier": 2.5
  }
}
```

**Implementation notes**

* Use `IWebHostEnvironment.WebRootPath` + `Directory.EnumerateFiles()` to load sprite filenames for a biome at runtime (and cache results).
* Keep the random selection deterministic-testable by injecting an RNG abstraction (or allow seeding in tests).
* Don’t mix file IO directly in UI components; put it behind a service (Web layer service is fine, or Application service if you treat it as “game domain logic”).
* Ensure duplicate enemies in the same encounter are either allowed or prevented—pick one and document it (I recommend **prevent duplicates per encounter** unless there aren’t enough unique sprites).

**Acceptance checks**

* Forest stage 1 spawns 1 non-boss forest enemy.
* Forest stage 9 spawns 5 non-boss forest enemies.
* Forest stage 10 spawns a boss (file starting with `boss_`).
* Forest stage 100 is a boss (10x rule), then 101 switches to Desert logic and folder.
* Stats visibly increase per stage.

---

### 4) Combat targeting: player focuses one target until it’s defeated

**Goal:** Player attacks must “stick” to the current target and only switch when:

* target dies, or
* target becomes invalid/unreachable (if your rules include that)

**Do:**

* Add/ensure a `currentTargetId` (or similar) in player combat state.
* Target selection:

  * If `currentTargetId` is alive → keep using it
  * Else pick the next target (e.g., lowest HP, first in list, closest — pick a deterministic rule and keep it consistent across Arena/Stage)

**Acceptance checks**

* With 3 enemies alive, player does not bounce targets each tick/attack.
* When target dies, player smoothly picks the next target.

---

### 5) Tests (must be included)

Add unit tests for core stage logic (not UI):

* Stage → biome resolution (e.g., 1=Forest, 101=Desert)
* Stage → encounter type (boss vs non-boss)
* Stage → enemy count (e.g., 1→1, 4→2, 9→5, 10→boss)
* Sprite filtering excludes `boss_` from random pool
* Scaling increases stats with stage (and boss multiplier applies)

Keep tests deterministic by controlling RNG/seed.

---

### Deliverables checklist

* [ ] Leaderboard active tab purple (scoped)
* [ ] Stage mode mobile fit matches Arena (no horizontal scroll)
* [ ] Stage encounter generator using biome folders + config rules
* [ ] Scaling stats per stage + boss multiplier
* [ ] Player target focus behavior implemented and consistent
* [ ] Unit tests added for stage/biome/enemy-count/scaling logic

---

If you want this as an even tighter “single prompt” for Copilot, say so and I’ll compress it into a shorter agent instruction without losing any requirements.
