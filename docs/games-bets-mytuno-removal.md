# Games / Bets / MyTuno removal

Two phases, because the database cannot go in the same release as the code (N-1 rule,
`docs/release-and-rollback.md`): if production rolled back to a build that still has the features,
that build would fail at startup without the tables, and every user query would fail without
`AspNetUsers.FidelisBalance`.

## Phase 1 - task 026 (done on `dev`)

The features are gone from the running app; the schema is untouched (no migration,
`dotnet ef migrations has-pending-model-changes` reports none).

- Retired, no replacement, plain 404: `/games`, `/games/avoid-questions`, `/games/bmr-bebe-mais-rui`,
  `/games/passaro-maluco`, `/games/tomato-thrower`, `/bets`, `/my-tuno`, `/my-tuno/arena`, `/my-tuno/stages`,
  `/my-tuno/boss-mode`, `/my-tuno/survive`, `/my-tuno/all-characters`, `/owner/stage-enemies`,
  `/owner/weapon-drink-config`, and `GET /api/cdn/image` (`CdnProxyController`, the PixiJS image proxy).
  Guarded by `tests/RTUB.Integration.Tests/RetiredGamesRoutesTests.cs`.
- Removed: the pages, the "Jogos" nav dropdown, every game service/repository/DTO/helper, the PixiJS source
  (`src/RTUB.Web/pixi/`) and its bundles, the `BuildPixiTS` publish target, `pixi.js`/`cross-env`, the Node
  step of the release action, `wwwroot/sprites`, `wwwroot/sound`, game images/JS/CSS, the `Games` section of
  `appsettings.json`, `scaling.config.json`, the bet push notifications, `docs/my_tuno/`, 16 game test files, and
  `CacheBuster`, `NumberFormatter`, `JsonSerializerConstants`, `MediaUploadManager` (+ their tests), whose only callers were games.
  Publishing no longer needs Node.
- Backup (outside the repo, built from the committed blobs of `dev` @ `03f18c1d`):
  `F:\Workspace\Backups\RTUB\026-games-bets-mytuno\RTUB-026-games-bets-mytuno-03f18c1d.zip`
  (773 deleted files at their repo paths, pre-change copies of the 35 edited files under `modified-before/`,
  `MANIFEST.md` with per-file blob SHA and SHA-256, `SHA256SUMS`). Git history holds the same bytes.

## Phase 2 - the contract task (not started)

Only after the release carrying 026 is live in production and its rollback window is over. Take and keep
a production database backup first (it holds every Fidelis balance, bet and character). Rolling back across
it is a database restore, not an app rollback; the release that carries it may be MAJOR
(`docs/release-and-rollback.md`).

Migration name: **`DropGamesBetsMyTuno`**, in the same release as `DropMessagesConversations`
(`docs/messages-removal.md`): one contract release, one production backup. It drops, and nothing else:

| Table | Foreign keys | Indexes |
| --- | --- | --- |
| `UserBets` | `BetId`, `BetOptionId`, `UserId` cascade | `_BetId`, `_BetOptionId`, `_UserId`, `_UserId_BetId` |
| `BetComments` | `BetId` cascade; `AuthorId` -> users **restrict** | `_AuthorId`, `_BetId`, `_CreatedAt` |
| `BetOptions` | `BetId` cascade; `MemberAId`, `MemberBId` -> users **restrict** | `_BetId`, `_MemberAId`, `_MemberBId` |
| `Bets` | - | `_BetCategory`, `_DateTime`, `_IsCancelled` |
| `Games`, `GameScores` | `GameScores.UserId` cascade | `IX_Games_Key` (unique), `_UserId_GameKey` (unique), `_GameKey_Points_MaxLevel` |
| `Characters`, `InventoryItems`, `ForgedWeapons` | `UserId` cascade | unique `UserId` / `UserId_Type`; `_Level`, `_UserId_IsEquipped` |
| `StageProgresses`, `BossModeProgresses`, `SurviveModeProgresses` | `UserId` cascade | unique `UserId`; `Highest*` |
| `StageEnemies`, `ItemTypeConfigs`, `ForgeComboConfigs` | - | `StageEnemies`: `_Region`, `_Type`, `_Type_Region` |

plus the column `AspNetUsers.FidelisBalance` (`TEXT NOT NULL`, no database default: the property and the
column must go in the same change, or creating a user fails). No kept table has a foreign key into these.
Check that the generated SQL drops the column without rebuilding `AspNetUsers`.

Code kept in 026 only for this task, to delete with the migration:

- `src/RTUB.Core/Entities/`: `Bet`, `BetComment`, `BetOption`, `UserBet`, `Game`, `GameScore`, `Character`,
  `InventoryItem`, `ForgedWeapon`, `ForgeComboConfig`, `ItemTypeConfig`, `StageProgress`, `StageEnemy`,
  `BossModeProgress`, `SurviveModeProgress`
- `src/RTUB.Core/Enums/`: `BetCategory`, `EnemyType`, `EquipmentSlot`, `InventoryItemType`, `PlacementType`,
  `RegionType`, `WeaponType`; `src/RTUB.Core/Configuration/MyTunoScaling.cs`; `src/RTUB.Core/Helpers/WeaponTypeHelper.cs`
- the instrument-part members of `src/RTUB.Core/Helpers/InstrumentTypeHelper.cs` (keep `GetDisplayName`/`ParseDisplayName`)
- `src/RTUB.Application/Data/Configurations/`: the 13 `*Configuration.cs` of the entities above
- the 15 DbSets in `ApplicationDbContext.cs`, their cases in `ApplicationDbContext.EntityDisplay.cs`, the entries in
  `AuditConfiguration.cs`, the `FidelisBalance` skip in `AuditLogAppender.cs`, `ApplicationUser.FidelisBalance`
- the BetComments/BetOptions cleanup in `UserProfileService` member deletion (needed while the restrict keys exist)
- tests: `RTUB.Core.Tests/Entities/{Character,ForgedWeapon,GameScore}Tests.cs`, the game lines in `DatabaseFixture.cs`

## Left behind on purpose

- R2 objects (public-read, nothing reads them now): `images/{env}/bets/`, `images/{env}/bets/thumbnails/`,
  `images/{env}/bet-comments/{images,videos}/`, `item-configs/{env}/images/` (incl. `forge_*`). Delete by hand
  if wanted; nothing in the app will.
- `AuditLogs` rows about these entities stay as history.
