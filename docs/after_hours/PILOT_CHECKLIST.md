# After Hours private pilot: operator checklist

For running the private pilot on **DEV** (`rtub-dev`). Rules and details: [README.md](README.md), section
"Admin, tuning and pilot readiness (AH-010)".

## Before the pilot

- [ ] `AfterHours__Enabled=true` is set on `rtub-dev` (App Service setting, set by the owner).
- [ ] PROD (`rtub`) has **no** `AfterHours__Enabled` setting: the feature stays off there.
- [ ] The deployed build includes the AH-010 migration (`AddAfterHoursAdmin`). Migrations apply at startup;
      the admin page's "Database schema" check must pass.
- [ ] Your account has the RTUB **Owner** role and `/after-hours/admin` opens. A Member account gets
      refused there and sees no Admin button on `/after-hours`.
- [ ] The fiscal year for the pilot exists (Finance, Owner). If the Live cycle belongs to another academic
      year, that fiscal year exists too.
- [ ] Tuning reviewed: every setting is on its default, or you have changed it on purpose. No invalid
      rows are listed.

## Start

- [ ] Admin → Cycle controls → **Start a Pilot**: pick the fiscal year; keep start now and end +10 days
      (7–14 is the recommended range).
- [ ] Readiness shows **READY**. Warnings you don't expect (for example old finished cycles without an archive)
      have been read and accepted.
- [ ] At least **two** test accounts sign in and open `/after-hours`: each gets a level 1 state.

## During the pilot

- [ ] Crimes, a cover job, the bank (the fee matches the tuning), training and buying equipment.
- [ ] Cargo: fence cargo, deliver a buyer contract.
- [ ] **PvP**: an attack between the two accounts; the battle report opens for both players only.
      Pause PvP from the admin page: attacks are refused with the pause message and reports still open.
      Resume it.
- [ ] **Family**: create one (level 5 and the creation cost), invite, accept, donate. The treasury shows the
      donation.
- [ ] **Objectives and leaderboards**: progress appears; both players are on the individual board and the
      family is on the family board.
- [ ] The Suspicious PvP section lists the test battles. Repeated attacks between the two accounts are
      flagged; this is expected in testing.
- [ ] Optional: grant a cosmetic title and check that it shows on the player's dashboard. Revoke it if it was
      only a test.

## End: Pilot → Live

- [ ] Admin → **End Pilot → start Live**: set the Live fiscal year and Lisbon start and end, read the
      summary, tick the confirmation box and run it. Running it twice changes nothing.
- [ ] `/after-hours/yearbook` shows the Pilot as **Pilot**, non-official, with no champions.
- [ ] The test accounts start the Live cycle at level 1 with 400 cash, no gear and no cargo.
- [ ] Their family still exists with the same members. The treasury is 0.
- [ ] Cosmetic titles are still there.
- [ ] Readiness is **READY** again for the Live cycle. Check the "Next fiscal year" line: an annual rollover
      needs it to exist, and it can only be created once its September has started.

## Afterwards

- [ ] Reset any tuning that was changed only for testing.
- [ ] The deferred features are known and accepted for the first release: heists, territory, trading,
      auction house, family upgrades, automatic rollover scheduling, material winner rewards and
      database-editable catalogues.
- [ ] PROD stays disabled until the release decision.
