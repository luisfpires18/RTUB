# Database schema notes: removals and what to keep

## Rules for dropping anything

- **N-1 / expand-contract.** A table or column goes only after the release that stopped using it is live in production
  and its rollback window is over; otherwise a rollback to the previous build fails at startup or on first query.
- **Production backup first**, kept outside the app's rotation (how: `docs/release-and-rollback.md` → *Migrations: the
  N-1 rule*). Rolling back across a drop is a database restore
  (`docs/release-and-rollback.md`).
- Delete the entity, its configuration, its `DbSet`, its `EntityDisplay` case and any `AuditConfiguration` entry in the
  same change as the migration; then `dotnet ef migrations has-pending-model-changes` must report none.
- Never edit a migration that has run in production. The new migration drops. (The chain up to 2026-11-17 is squashed
  into `Baseline`, task 130; the names below are in git history.)
- On SQLite, dropping a column makes EF rebuild the table with `PRAGMA foreign_keys` outside a transaction. Keep such a
  migration on its own so a failure cannot leave other drops committed without their history row.

## Never drop

- `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`: Messaging v2 (`docs/react-messages.md`).
  Never dropped, renamed or cleared.
- `AlbumAccesses`: read by the Music exclusivity rules (`AlbumRepository`).
- `LogisticsCardReminders`: written by the Logística board, sent by `LogisticsReminderDelivery`.
- `AuditLogs` rows that name removed entities: history.

## Removed (in production)

The retired Games / Bets / MyTuno module, the Labels admin and the slideshows went with two migrations:

1. `20261013093127_RemoveRetiredGamesLabelsSlideshowsSchema` drops `BetComments`, `BossModeProgresses`, `Characters`,
   `ForgeComboConfigs`, `ForgedWeapons`, `Games`, `GameScores`, `InventoryItems`, `ItemTypeConfigs`, `Labels`,
   `Slideshows`, `StageEnemies`, `StageProgresses`, `SurviveModeProgresses`, `UserBets`, `BetOptions`, `Bets`.
2. `20261013093143_RemoveFidelisBalanceFromApplicationUser` rebuilds `AspNetUsers` without `FidelisBalance`.

**Rollback across them is a full database restore only.** `Down` restores the schema (as the EF model defines it, not
exactly as the chain built it) and no rows; it is for local/DEV schema testing, not a production rollback path. An
app-only rollback to an older build fails on the first sign-in (it still maps `FidelisBalance`). The Fidelis balances
exist only in the pre-migration backup.

Left behind on purpose: the R2 objects under `images/{env}/bets/`, `images/{env}/bets/thumbnails/`,
`images/{env}/bet-comments/{images,videos}/`, `item-configs/{env}/images/` and the slideshow images. Nothing reads them;
delete by hand if wanted. The old URLs (`/images`, `/labels`, the game pages) are plain 404s.

## Pending drop: five unused columns (task 131)

No code read or wrote `AspNetUsers.ExperiencePoints` / `Level`, `MemberStatuses.OverrideRetired`,
`MeetingAtaAttachments.FileUrl` or `Reports.PdfData` (all empty or at their defaults on the local snapshot). Release N
(task 131) stops mapping them:
- `UserXpLevelDefaults` rebuilds `AspNetUsers` to give `ExperiencePoints` a default of 0 and `Level` 1 (both NOT NULL,
  no default before), so inserts work without them. Expand-compatible: every column stays.
- `StopMappingUnusedColumns` changes only the model snapshot; `Up` and `Down` are empty. The five columns stay in every
  existing database. A database created from the Baseline after task 131 has them too.

The drop (release N+1, a MAJOR bump) goes only after N is live on PROD and its rollback window is over. It must be
written by hand: the snapshot no longer lists the columns, so `migrations add` generates nothing. Five `DropColumn`
operations in their own migration; take the production backup first. Until then, an EF-generated rebuild of
`AspNetUsers`, `MemberStatuses`, `MeetingAtaAttachments` or `Reports` would drop the column early: check any migration
on those tables.

## Pending decision: `PostMedia`, `CommentImages`

Photos/videos once attached to event-discussion posts and comments. Nothing creates or returns rows any more; the only
reader is delete clean-up (`PostService.SoftDeleteAsync`, `CommentService`), which removes the R2 objects and rows when a
post or comment is deleted. Next step: count rows on a production snapshot. Zero rows → drop both tables with their
repositories, entities, configurations, navigations and the clean-up code. Rows present → the owner decides whether to
delete their R2 objects first.

## Do not touch

- A local `__EFMigrationsHistory` once showed 8 non-dated ids, unexplained. Check production read-only before a release
  that migrates.
