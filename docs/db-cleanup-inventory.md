# Database cleanup inventory (tasks 042 / 043)

Written by task 042 (cleanup audit, 2026-10-13; no schema change). **Task 043** (branch `chore/043-db-cleanup`) removes
the CONFIRMED DEAD rows below with two migrations; see *Task 043* at the end for the migrations, what they do, the scratch
validation and the production release plan. **Nothing here has been applied to DEV or PROD.**

Rules that apply to every row below (`docs/release-and-rollback.md`):

- **N-1 / expand-contract.** A table goes only after the release that stopped using it is live in production and its
  rollback window is over; otherwise a rollback to the previous build fails at startup or on first query.
- **Production backup first**, kept outside the app's rotation; rolling back across a drop is a database restore.
- **Never** `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings` (Messaging v2, task 041).
- Delete the entity, its configuration, its `DbSet`, its `EntityDisplay` case and any `AuditConfiguration` entry **in
  the same change** as the migration, then check `dotnet ef migrations has-pending-model-changes` reports none.
- Do not edit historical migrations to remove names; the new migration drops, the old ones stay.

## Summary

| Object | Class | Task 043 outcome |
| --- | --- | --- |
| 15 Games / Bets / MyTuno tables | **CONFIRMED DEAD** | **REMOVED IN 043** (`RemoveRetiredGamesLabelsSlideshowsSchema`); applied only in the release window |
| `AspNetUsers.FidelisBalance` | **CONFIRMED DEAD** | **REMOVED IN 043** (`RemoveFidelisBalanceFromApplicationUser`) |
| `Labels` | **CONFIRMED DEAD** | **REMOVED IN 043**; export the production `ranking_story` row first |
| `Slideshows` | **CONFIRMED DEAD** | **REMOVED IN 043**; R2 `slideshows` images are a separate manual clean-up |
| `PostMedia`, `CommentImages` | **LIKELY DEAD** | **DEFERRED**: production row counts unknown (see below) |
| `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings` | **KEEP** | Nothing. Messaging v2 |
| `AlbumAccesses` | **KEEP** | Nothing (18 stale rows for album 12, ignored by the rules; `docs/react-music.md`) |
| `LogisticsCardReminders` | **KEEP** | Nothing (written by the React board; no job sends them, a product decision) |
| `AuditLogs` rows about removed entities | **KEEP** | Nothing (history) |
| Migration classes without `[Migration]`; non-dated `__EFMigrationsHistory` ids | **UNKNOWN/RISKY** | Do not touch in 043 |

## CONFIRMED DEAD

### Games / Bets / MyTuno (15 tables) and `AspNetUsers.FidelisBalance`

The authoritative drop list (tables, foreign keys, indexes, the `FidelisBalance` column rule) is
`docs/games-bets-mytuno-removal.md` → *Phase 2*. Shared facts:

- **Associated historical feature:** Jogos, Apostas (Fidelis currency), MyTuno RPG; removed from the app by task 026.
- **Current code references (all kept only for the drop):** the 15 entities in `src/RTUB.Core/Entities/` and the enums
  `BetCategory`, `EnemyType`, `EquipmentSlot`, `InventoryItemType`, `PlacementType`, `RegionType`, `WeaponType`;
  `src/RTUB.Core/Configuration/MyTunoScaling.cs`; `src/RTUB.Core/Helpers/WeaponTypeHelper.cs`; the game members of
  `InstrumentTypeHelper` (keep `GetDisplayName` / `ParseDisplayName`); 13 `Data/Configurations/*Configuration.cs`
  (`ItemTypeConfig` and `ForgeComboConfig` have none); 15 `DbSet`s in `ApplicationDbContext.cs`; the `Bet`,
  `UserBet`, `BetComment`, `GameScore`, `Character` cases in `ApplicationDbContext.EntityDisplay.cs`; the game entries
  in `src/RTUB.Core/Constants/AuditConfiguration.cs`; the `FidelisBalance` skip in `AuditLogAppender.cs`;
  `ApplicationUser.FidelisBalance`; the `BetComments` / `BetOptions` cleanup in `UserProfileService` member deletion
  (needed while the restrict keys exist; delete with the tables).
- **Current API / UI references:** none. Routes 404 (`RetiredGamesRoutesTests`), no endpoint, no React page, no menu.
- **Current test references:** `tests/RTUB.Core.Tests/Entities/{CharacterTests,ForgedWeaponTests,GameScoreTests}.cs`;
  the game lines in `tests/RTUB.Application.Tests/Fixtures/DatabaseFixture.cs` (`CleanDatabase`).
- **Migration/history references:** created across the 2025-2026 game migrations; present in
  `ApplicationDbContextModelSnapshot.cs`. Historical migrations stay as they are.
- **Evidence it is unused:** no service, repository, endpoint or page reads or writes them since 026 (repository-wide
  grep after 042: only the files listed above); `has-pending-model-changes` was clean at 026 and is unchanged.
- **Dependencies / foreign keys:** per table below. No kept table has a foreign key into these.
- **Data-loss risk:** **high and irreversible** - every Fidelis balance, bet, bet comment, character, inventory and
  progress row. Production backup first; the owner confirms nobody wants an export.
- **Recommended action:** one migration `DropGamesBetsMyTuno` in a release of its own (likely MAJOR), only after the
  release carrying 026 has been live past its rollback window. Drop children before parents (or rely on SQLite's
  table-rebuild order EF generates); drop the column without rebuilding `AspNetUsers` if possible (check the SQL).
  R2 leftovers (`images/{env}/bets/`, `images/{env}/bet-comments/`, `item-configs/{env}/images/`) are a separate,
  manual storage clean-up.

| Table | Entity | Foreign keys | Indexes |
| --- | --- | --- | --- |
| `UserBets` | `UserBet` | `BetId`, `BetOptionId`, `UserId` cascade | `_BetId`, `_BetOptionId`, `_UserId`, `_UserId_BetId` |
| `BetComments` | `BetComment` | `BetId` cascade; `AuthorId` → users **restrict** | `_AuthorId`, `_BetId`, `_CreatedAt` |
| `BetOptions` | `BetOption` | `BetId` cascade; `MemberAId`, `MemberBId` → users **restrict** | `_BetId`, `_MemberAId`, `_MemberBId` |
| `Bets` | `Bet` | - | `_BetCategory`, `_DateTime`, `_IsCancelled` |
| `Games` | `Game` | - | `IX_Games_Key` (unique) |
| `GameScores` | `GameScore` | `UserId` cascade | `_UserId_GameKey` (unique), `_GameKey_Points_MaxLevel` |
| `Characters` | `Character` | `UserId` cascade | unique `UserId`; `_Level` |
| `InventoryItems` | `InventoryItem` | `UserId` cascade | `_UserId`; unique `_UserId_Type` |
| `ForgedWeapons` | `ForgedWeapon` | `UserId` cascade | `_UserId`, `_UserId_IsEquipped` |
| `StageProgresses`, `BossModeProgresses`, `SurviveModeProgresses` | `StageProgress`, `BossModeProgress`, `SurviveModeProgress` | `UserId` cascade | unique `UserId`; `Highest*` |
| `StageEnemies` | `StageEnemy` | - | `_Region`, `_Type`, `_Type_Region` |
| `ItemTypeConfigs`, `ForgeComboConfigs` | `ItemTypeConfig`, `ForgeComboConfig` | - | - |
| column `AspNetUsers.FidelisBalance` | `ApplicationUser.FidelisBalance` | - | - (`TEXT NOT NULL`, no DB default: property and column go together) |

### `Labels`

- **Object/table:** `Labels` (`Id`, `Reference` ≤ 100, `Title` ≤ 200, `Content` ≤ 5000, `IsActive`, audit columns).
- **Associated EF entity:** `src/RTUB.Core/Entities/Label.cs` (no configuration class; conventions only).
- **Associated historical feature:** the Blazor `/labels` admin ("Conteúdo", editable home texts) and the Leaderboard
  story; admin removed by 029A, story fixed in code (`LeaderboardService.Story`).
- **Current code references:** `DbSet<Label> Labels` (`ApplicationDbContext.cs`); `case "Label"` in
  `ApplicationDbContext.EntityDisplay.cs`; `SeedData.Labels.cs` (seeds when the table is empty) and its call in
  `SeedData.cs`. `ILabelService` / `LabelService` / `ILabelRepository` / `LabelRepository` were removed by 042 (no
  caller).
- **Current API references:** none (`PUT /api/leaderboard/story` went in 029A).
- **Current UI references:** none (`/labels` is 404; the React home copy is static).
- **Current test references:** `tests/RTUB.Core.Tests/Entities/LabelTests.cs`; `ApplicationDbContextTests` (one label
  insert); `DatabaseFixture.CleanDatabase`; `LeaderboardApiTests` writes a `ranking_story` row to prove it is not read;
  `RetiredImagesLabelsRoutesTests.LabelAndSlideshowCode_IsGone_ButTheirTablesStayFor043` (asserts the DbSet still
  exists: update it in 043).
- **Migration/history references:** created by `20251026142327_Db`; in the model snapshot.
- **Evidence it is unused:** after 042 nothing outside `Data/` (seed, DbSet, display name) names the entity; no
  endpoint, service or page reads it.
- **Dependencies/foreign keys:** none in or out; no indexes besides the primary key.
- **Data-loss risk:** low - site texts. One row matters: `ranking_story`, if production edited it after 029A (that text is
  no longer shown). Export it for the owner before the drop.
- **Recommended Task 043 action:** drop `Labels`; delete `Label.cs`, the DbSet, the display case, `SeedData.Labels.cs`
  + its call, `LabelTests.cs`, the fixture/context-test lines, and update the two integration tests above.

### `Slideshows`

- **Object/table:** `Slideshows` (`Id`, `Title` ≤ 200, `Description` ≤ 1000, `ImageUrl`, `Order`, `IntervalMs`,
  `IsActive`, `IsExclusive`, audit columns).
- **Associated EF entity:** `src/RTUB.Core/Entities/Slideshow.cs` (no configuration class).
- **Associated historical feature:** the old Blazor home carousel and its `/images` admin; carousel gone since React
  task 004, admin removed by 029A.
- **Current code references:** `DbSet<Slideshow> Slideshows`; `case "Slideshow"` in `EntityDisplay`;
  `SeedData.Slideshows.cs` and its call in `SeedData.cs`. `ISlideshowService` / `SlideshowService` /
  `ISlideshowRepository` / `SlideshowRepository` were removed by 042 (no caller).
- **Current API references:** none (`/images` is an explicit 404; `/images/<file>` is `ImagesController`, unrelated).
- **Current UI references:** none.
- **Current test references:** `tests/RTUB.Core.Tests/Entities/SlideshowTests.cs`; `DatabaseFixture.CleanDatabase`;
  the `RetiredImagesLabelsRoutesTests` DbSet assertion (update in 043).
- **Migration/history references:** created by `20251026142327_Db`; in the model snapshot.
- **Evidence it is unused:** as for `Labels`.
- **Dependencies/foreign keys:** none; no indexes besides the primary key.
- **Data-loss risk:** low - slide metadata. The images are R2 objects (upload category `slideshows`), public-read and
  unused; dropping the table loses the list of their URLs, so list them first if the owner wants the R2 objects
  deleted.
- **Recommended Task 043 action:** drop `Slideshows`; delete `Slideshow.cs`, the DbSet, the display case,
  `SeedData.Slideshows.cs` + its call, `SlideshowTests.cs`, the fixture line.

## LIKELY DEAD (verify data first)

### `PostMedia` and `CommentImages` (DEFERRED by 043)

043 left them in place: whether production holds rows (and so member-uploaded R2 media) can only be read on a
production snapshot, which 043 does not touch. Missing evidence: the row counts of both tables on production (or a
fresh sanitized DEV refresh), and the owner's decision on their R2 objects.


- **Object/table:** `PostMedia` (`PostId`, `Url` ≤ 2048, `MediaType`, `MimeType`, `SizeBytes`, `SortOrder`);
  `CommentImages` (`CommentId`, `Url`, `MimeType`, `SizeBytes`, `SortOrder`).
- **Associated EF entity:** `PostMedia`, `CommentImage` (+ `Post.Media`, `Comment.Images` navigations;
  `PostMediaConfiguration`, `CommentImageConfiguration`).
- **Associated historical feature:** photos / videos attached to Blazor event-discussion posts and comments.
- **Current code references:** `IPostMediaRepository` / `PostMediaRepository`, `ICommentImageRepository` /
  `CommentImageRepository`; read only by `PostService.SoftDeleteAsync` and `CommentService` delete, which delete the
  R2 objects and the rows when a post / comment is deleted (reached from the React discussion delete).
- **Current API references:** indirect only (`DELETE` of a discussion post / comment). Nothing creates rows (the upload
  paths went in 013 / 039) and nothing returns them (`EventDiscussionBoardService` has no media in its DTOs).
- **Current UI references:** none.
- **Current test references:** none by name.
- **Migration/history references:** created by `20251203113004_AddEventMediaTables`.
- **Evidence it is unused:** write-dead and display-dead since React track 013; the only reader is delete clean-up.
- **Dependencies/foreign keys:** `PostMedia.PostId` → `Posts` cascade; `CommentImages.CommentId` → `Comments` cascade;
  indexes `IX_PostMedia_PostId`, `IX_PostMedia_PostId_SortOrder`, `IX_CommentImages_CommentId`,
  `IX_CommentImages_CommentId_SortOrder`.
- **Data-loss risk:** medium - existing rows point at member-uploaded R2 media that is no longer shown; dropping the
  tables orphans those objects for good.
- **Recommended Task 043 action:** read-only row count on a production snapshot. Zero rows → drop both tables with the
  repositories, entities, configurations, navigations and the clean-up code in `PostService` / `CommentService`.
  Rows present → the owner decides: delete their R2 objects first, or keep the tables one more release.

## KEEP

- **`Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`** - Messaging v2 (`MessagingService`,
  `/api/conversations*`, React `/messages`); never dropped, renamed or cleared (`docs/messages-removal.md`,
  `docs/react-messages.md`). 042 did not touch them or their rows.
- **`AlbumAccesses`** - read by the Music exclusivity rules through `AlbumRepository`.
- **`LogisticsCardReminders`** - written by the React board (`LogisticsKanbanService`); delivery is a product decision.
- **`AuditLogs`** rows that name removed entities - history.
- Everything else in `ApplicationDbContext` has a live reader (042 grep of every `DbSet` outside `Data/`).

## UNKNOWN/RISKY (leave alone in 043)

- **`UpdateCardStatusEnumValues`, `NerbaOrderEventRequired`** migration classes have no `[Migration]` attribute, so EF
  never ran them; both hold destructive SQL. Do not "fix" them by adding the attribute.
- **`__EFMigrationsHistory`** in a local copy showed 8 non-dated ids (task 003); unexplained. Check production
  read-only before a release that migrates.

## Task 043

Two migrations on `chore/043-db-cleanup`, after `20261005175342_AddNewsPosts`. Split on purpose: to drop a column on
SQLite EF rebuilds `AspNetUsers` with `PRAGMA foreign_keys` outside a transaction ("cannot be executed in a
transaction"); kept on its own, a failure there cannot leave the 17 drops committed without their history row. Same
shape as `RemoveIsActiveFromApplicationUser`, which already ran in production.

1. **`20261013093127_RemoveRetiredGamesLabelsSlideshowsSchema`** (one transaction). Up: `DROP TABLE` `BetComments`,
   `BossModeProgresses`, `Characters`, `ForgeComboConfigs`, `ForgedWeapons`, `Games`, `GameScores`, `InventoryItems`,
   `ItemTypeConfigs`, `Labels`, `Slideshows`, `StageEnemies`, `StageProgresses`, `SurviveModeProgresses`, `UserBets`,
   `BetOptions`, `Bets` (children before parents; their indexes and foreign keys go with their tables; no kept table
   references them). Down: recreates the 17 tables with primary keys, foreign keys (users cascade / restrict as before)
   and 31 indexes, as the EF model defines them.
2. **`20261013093143_RemoveFidelisBalanceFromApplicationUser`**. Up: EF's SQLite rebuild of `AspNetUsers` without the
   column (copy into `ef_temp_AspNetUsers`, `PRAGMA foreign_keys = 0`, drop, rename, `PRAGMA foreign_keys = 1`,
   recreate `EmailIndex`, `IX_AspNetUsers_MentorId`, `UserNameIndex`); every row and every other column is copied.
   Down: `ALTER TABLE "AspNetUsers" ADD "FidelisBalance" TEXT NOT NULL DEFAULT '0.0'`.

Code removed with them: the 17 entities, the 7 game enums, `MyTunoScaling`, `WeaponTypeHelper`, the game members of
`InstrumentTypeHelper`, 13 configurations, 17 DbSets, 7 display-name cases, the game audit exclusions, the
`FidelisBalance` property and audit skip, the bet clean-up in member deletion, `SeedData.Labels.cs` /
`SeedData.Slideshows.cs`, and the entity tests of those types.

### Rollback across 043: full database restore only

**Down restores schema only; dropped production row data would require restoration from the pre-migration
backup/export.** Down is kept for local / DEV schema testing. **It is not a supported production rollback path**, and
an app-only rollback is not one either.

Down rebuilds what the EF model says, not what the migration chain built. A scratch copy of a real database, a fresh
database migrated to `AddNewsPosts` (both give the same pre-043 shape) and the result of Up + Down differ in:

| Object | Pre-043 (real and fresh chain) | After Down |
| --- | --- | --- |
| `Bets.DiscussionId` | `INTEGER NULL`, FK → `Discussions.Id` (NO ACTION), `IX_Bets_DiscussionId` (since `AddBettingSystem`; `RemoveDiscussionIdFromBet` is a no-op) | absent |
| `Labels.Reference`, `Title`, `Content` | nullable | `NOT NULL` |
| `Bets.IsApproved`, `ForgedWeapons.Level`, `Slideshows.IsExclusive` | `DEFAULT 0` | no default |
| `AspNetUsers.FidelisBalance` | `TEXT NOT NULL`, each member's balance | `TEXT NOT NULL DEFAULT '0.0'`, every member 0 |
| `AspNetUsers.PushNotificationsOptedOut` | `DEFAULT 0` | no default (Up's `AspNetUsers` rebuild drops it; Down does not bring it back) |
| rows of the 17 tables | production data | none |

What that means for an older build:

- **2.0.4 (production today) and any build before 026** use these tables at runtime. Its model does not map
  `Bets.DiscussionId` and wants the `Labels` columns required, and EF sends every column value on insert (no default
  is used), so the schema differences alone would not stop it. The missing rows would: no games, no stage enemies,
  no item configs, the home texts and slides gone, every Fidelis balance 0. Schema Down cannot fix that; only the
  backup can.
- **The release right before 043 (a 026-042 build)** still maps `ApplicationUser.FidelisBalance` (every user query
  selects it) and the 17 entities (member deletion touches `BetComments` / `BetOptions`). An app-only rollback onto
  the 043 schema fails on the first sign-in, whatever `allow_newer_schema` says.
- A faithful Down would need hand-written SQL that brings back a column the model dropped on purpose
  (`Bets.DiscussionId`) and two more `AspNetUsers` rebuilds, for a schema that still holds no rows. Not done: more risk
  on a live Identity table for no recovery gain.

**FidelisBalance:** no migration can bring the balances back. They live only in the pre-migration backup (and the
step-3 export); recovering them means restoring that backup.


**Scratch validation** (local only, never DEV or PROD): an online-backup copy of the local `app.db` (108 users, 145
conversations, 17,761 messages, rows in all 17 tables), first migrated to `AddNewsPosts`. Upgrade, rollback to
`AddNewsPosts` and reapply all passed; messaging and Identity row counts and checksums identical at every step,
`foreign_key_check` empty and `integrity_check` ok at every step, schema after reapply identical to the first upgrade. A
second copy was migrated by the app's own startup (pre-migration snapshot taken, both migrations applied); `/health`, the
React shell and, signed in, `/api/account/me`, `/api/members`, `/api/conversations`, `/api/me/profile`,
`/api/leaderboard`, `/api/hall-of-fame`, `/api/owner/users` answered 200. `MigrationChainTests` pins the drop set, the
Down path and the reapply.

### Production release plan (not done; release window only)

**Ordering is the hard constraint.** Production runs 2.0.4, which does **not** contain 026, 029A or 037-042: it still
reads these tables, and `ApplicationUser.FidelisBalance` is in every user query. Shipped in the same release as that
code, 043 would make an app rollback to 2.0.4 fail on the first sign-in.

0. Release `dev` **without** 043 first (it carries 026-042) and let it run past its rollback window. Only then merge 043
   into `dev` and release it on its own, with no other migration.
1. **Back up**: besides the app's own pre-migration snapshot, take an independent online backup of the production
   database (the same day's `current.db`, or a manual online backup) and keep it outside the rotation.
2. **Verify the backup**: `PRAGMA integrity_check` = `ok`; the 17 tables and `AspNetUsers.FidelisBalance` are present.
3. **Count** (read-only, on the backup): rows of the 17 tables; non-default `FidelisBalance`; `AspNetUsers`; and
   `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`.
4. **Preserve**: export `Labels` (at least `ranking_story`: the code keeps only the seeded text,
   `LeaderboardService.Story`, not later production edits), the `Slideshows` rows (their R2 image URLs) and any game
   data the owner wants kept; the verified backup is the full archive.
5. **Apply**: Deploy • PROD of the 043 release; startup takes the snapshot, then migrates.
6. **Validate**: `/api/version`, sign-in, `/members`, `/messages`, `/profile`; messaging and `AspNetUsers` counts equal
   step 3; the 17 tables and the column are gone.
7. **Rollback = full database restore (mandatory).** Stop the app, restore the step-1 backup (replace `app.db`, delete
   stale `-wal` / `-shm`), then Rollback • PROD to the previous release. Restoring brings back the 17 tables with their
   rows, the old balances and the exact pre-043 schema; it also discards anything written after the 043 release, so
   decide quickly. Never roll the app back alone (it fails on the first user query) and never use EF Down as the
   production rollback.

**Storage is separate.** Dropping the tables deletes no R2 object. Left for a separate manual clean-up:
`images/{env}/slideshows` (slideshow images), `images/{env}/bets/`, `images/{env}/bet-comments/`,
`item-configs/{env}/images/`.
