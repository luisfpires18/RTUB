# Messages / Conversas removal

Two phases, like `docs/games-bets-mytuno-removal.md` and for the same reason (N-1 rule,
`docs/release-and-rollback.md`): a build that still has Messages runs `SyncDefaultGroupsAsync` at startup and
rethrows its errors, so it would not start without the `Conversations` table.

This is a removal, not a migration: nothing replaces the inbox. Any future chat is a new design task.

## Phase A - task 027 (done on `dev`)

The feature is gone from the running app; the schema is untouched (no migration,
`dotnet ef migrations has-pending-model-changes` reports none).

- Retired, plain 404: `/messages`, `/messages/{id}` (Blazor inbox) and `/hubs/messages` (SignalR `MessagesHub`,
  incl. `/hubs/messages/negotiate`). Also gone: the "Mensagens" item and unread badges in the top-bar user menu,
  the "Sync Chat" Owner button on `/users`, the "Mensagens" PWA manifest shortcut, the service worker's `/messages`
  click fallback, `messageComposer.js`, `messageScroller.js`, `unreadMessages.js`, the messaging CSS, and every
  messaging service, repository, DTO and test.
- **Push notifications still send, but no longer leave an in-app copy.** Before 027 every push
  (`SendToUserAsync`, `BroadcastAsync`, `SendToSelectedUsersAsync`) also wrote the text into the recipient's pinned
  "Sistema RTUB" conversation - about 99% of the `Messages` rows. A member without push (opted out, unsupported
  browser, iOS without the installed app) now sees those notifications nowhere in the app. `SendPushOnlyAsync`,
  the direct/group message notifications and the payload's `unreadCount` went with it.
- The service worker no longer sets the app-icon badge (it showed the unread-message count); tapping a
  notification still clears a badge an older worker left. `window.appBadge` in `pwa-helper.js` is now unused.
- SignalR itself stays: Blazor Interactive Server runs on it (`HubOptions` in `ServiceCollectionExtensions`).
  `IMentionService` stays (event discussions use it); event discussions are a separate feature on separate tables.
- No R2 objects belong to messaging; nothing to clean in storage.
- Backup (outside the repo, built from the committed blobs of `dev` @ `e5a30a9a`):
  `F:\Workspace\Backups\RTUB\027-messages\RTUB-027-messages-e5a30a9a.zip` - deleted files at their repo paths,
  pre-change copies of the edited files under `modified-before/`, the Phase B files under `phase-b-kept/`,
  `MANIFEST.md` (blob SHA and SHA-256 per file), `SHA256SUMS`. **Code only - no message data.**
- Guarded by `tests/RTUB.Integration.Tests/RetiredMessagesRoutesTests.cs`, which also holds the rollback-safety
  test: with the four tables dropped, a signed-in page, every push fan-out and a member deletion run without one SQL
  statement naming them - so a 027 build is a valid rollback target for Phase B.

## Phase B - the contract task (not started)

Bundle it with the Games / Bets / MyTuno contract task: one release, two migrations, one production backup. Only
after the release carrying 026 and 027 is live in production and its rollback window is over. Take and keep a
production database backup first, privately and no longer than needed (it holds private member chats). The release
that carries it may be MAJOR.

Migration name: **`DropMessagesConversations`**. It drops, and nothing else:

| Table | Foreign keys | Indexes |
| --- | --- | --- |
| `MessageReactions` | `MessageId` -> Messages cascade; `UserId` -> users cascade | `IX_MessageReaction_MessageId_UserId` (unique), `IX_MessageReactions_UserId` |
| `Messages` | `ConversationId` -> Conversations cascade; `SenderId` -> users **set null** | `IX_Message_SenderId`, `IX_Message_ConversationId_CreatedAt` |
| `ConversationUserSettings` | `ConversationId` -> Conversations cascade; `UserId` -> users cascade | `IX_ConversationUserSettings_ConversationId_UserId` (unique), `IX_ConversationUserSettings_UserId` |
| `Conversations` | - | `IX_Conversation_Participants`, `IX_Conversation_LastMessageAt_IsArchived` |

No `AspNetUsers` column belongs to messaging, and no kept table has a foreign key into these four.

Code kept in 027 only for this task, to delete with the migration:

- `src/RTUB.Core/Entities/`: `Conversation`, `ConversationUserSettings`, `Message`, `MessageReaction`
- `src/RTUB.Application/Data/Configurations/`: the 4 `*Configuration.cs` of those entities
- the 4 DbSets in `ApplicationDbContext.cs`; `Message`, `Conversation`, `ConversationUserSettings` in
  `AuditConfiguration.ExcludedEntityTypes` (and the matching test in `AuditLogServiceTests`)
- tests: `RTUB.Core.Tests/Entities/{Conversation,ConversationUserSettings,Message}Tests.cs`, the two
  messaging lines in `DatabaseFixture.cs`

## Where the old data still is

Until Phase B, the four tables keep every message (nothing reads or writes them). After it, copies remain only in
database backups: the pre-migration snapshots, the daily R2 database backups (until they rotate out), any manual
production backup, local `app.db` copies, and the DEV database after a refresh (`DatabaseSanitizer` does not scrub
messages).
