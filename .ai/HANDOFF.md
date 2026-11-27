# Handoff (2026-11-27)

## Done (this session, all merged and released)
- Trintuna `/trintuna`, tasks 153-164, PRs #365-#381. Live on PROD **2.3.20** (tag `v2.3.20`). `dev` is on **2.3.21**, clean at `0f730077`.
- Owner standing orders:
  - Merge into `dev` without asking once Codex ACCEPTED and `Build & Test` is green (memory `merge-to-dev-when-green`).
  - "Go prod" means: a `dev → master` release PR, merged with a merge commit; no `releases/*.json` notes; then `master` back into `dev` and a VERSION bump.
- PROD answers on `rtub.azurewebsites.net`, `rtub.pt` and `www.rtub.pt` with **no redirect**. This keeps home-screen apps working on whatever host they were added from.
  - "Copiar link" uses `shareOrigin` (`App:BaseUrl`).
  - On iOS a tapped link always opens Safari; only an App Store wrapper or a push notification opens the installed app.

## Left / open with the owner
- Asked, no answer yet: should an almoço get a push button ("Avisar os membros") so a tap opens the installed app on iPhone? An App Store wrapper is the other route.
- Leftover scratch folder `D:\tmp\rtub-164`: the owner deletes it.
- The C: drive is full: set `TEMP`/`TMP` to `D:\tmp` before `git`, `gh`, `dotnet`, `npm`.

## Next: owner request (2026-11-27), task 166
> allow multi votes in polls. i should be able to choose one or more days

- Scope: the Trintuna date poll (`GatheringPollOption` / `GatheringPollVote`, `TrintunaService.Answers.cs` vote, `PUT /api/trintuna/gatherings/{id}/vote`, `PollCard` with `onVote` in `components/MeetingPollCard.tsx`).
- The poll today allows one vote per member: a new vote replaces the earlier one.
- Make it multi-choice: a member can pick one or more dates. Counts show votes per date.
- Check the unique index on votes (likely `GatheringId`+`UserId`). If it has to become `GatheringId`+`OptionId`+`UserId`, that is a migration: expand-only, N-1 safe.
- `PollCard` is shared with `/meetings` polls. Keep meetings single-choice unless an option or prop says otherwise (no behaviour change there).
- Settling a date (`POST /settle` from one option) is unchanged.
- Flow (Standard tier): Issue, plan PR (Codex), implement, Codex, merge into `dev` when green.
