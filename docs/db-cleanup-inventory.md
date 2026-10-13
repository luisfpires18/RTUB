# Database cleanup inventory (for task 043)

Written by task 042 (cleanup audit, 2026-10-13). **042 changed no schema, wrote no migration and touched no data**: it
removed only code with no caller. This file is what task 043 needs to write its migration without re-auditing the
repository. Evidence is as of `dev` after 042.

Rules that apply to every row below (`docs/release-and-rollback.md`):

- **N-1 / expand-contract.** A table goes only after the release that stopped using it is live in production and its
  rollback window is over; otherwise a rollback to the previous build fails at startup or on first query.
- **Production backup first**, kept outside the app's rotation; rolling back across a drop is a database restore.
- **Never** `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings` (Messaging v2, task 041).
- Delete the entity, its configuration, its `DbSet`, its `EntityDisplay` case and any `AuditConfiguration` entry **in
  the same change** as the migration, then check `dotnet ef migrations has-pending-model-changes` reports none.
- Do not edit historical migrations to remove names; the new migration drops, the old ones stay.

## Summary

| Object | Class | Task 043 action |
| --- | --- | --- |
| 15 Games / Bets / MyTuno tables + `AspNetUsers.FidelisBalance` | **CONFIRMED DEAD** | Drop (`DropGamesBetsMyTuno`, `docs/games-bets-mytuno-removal.md`), release window only |
| `Labels` | **CONFIRMED DEAD** | Copy out the `ranking_story` row if the owner wants it, then drop |
| `Slideshows` | **CONFIRMED DEAD** | Drop; R2 `slideshows` images are separate (by hand) |
| `PostMedia`, `CommentImages` | **LIKELY DEAD** | Count rows on production first; decide the R2 objects; then drop with a code change |
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

### `PostMedia` and `CommentImages`

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
