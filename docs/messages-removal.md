# Messages / Conversas removal

Two phases, like `docs/games-bets-mytuno-removal.md` and for the same reason (N-1 rule,
`docs/release-and-rollback.md`): a build that still has Messages runs `SyncDefaultGroupsAsync` at startup and
rethrows its errors, so it would not start without the `Conversations` table.

Phase A removed the old inbox UI; nothing replaced it. The schema is kept on purpose: **Messaging v2** (planned) will
audit and evolve the existing messaging tables. They must not be dropped.

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
- (037) The Blazor host and its SignalR circuit are gone too; no SignalR hub or client package is left (039 removed
  the unused `Microsoft.AspNetCore.SignalR.Client`). Messaging v2 decides its own transport.
  `IMentionService` stays (event discussions use it); event discussions are a separate feature on separate tables.
- No R2 objects belong to messaging; nothing to clean in storage.
- Backup (outside the repo, built from the committed blobs of `dev` @ `e5a30a9a`):
  `F:\Workspace\Backups\RTUB\027-messages\RTUB-027-messages-e5a30a9a.zip` - deleted files at their repo paths,
  pre-change copies of the edited files under `modified-before/`, the Phase B files under `phase-b-kept/`,
  `MANIFEST.md` (blob SHA and SHA-256 per file), `SHA256SUMS`. **Code only - no message data.**
- Guarded by `tests/RTUB.Integration.Tests/RetiredMessagesRoutesTests.cs`, which also holds the rollback-safety
  test: with the four tables dropped, a signed-in page, every push fan-out and a member deletion run without one SQL
  statement naming them. (Written for the cancelled Phase B; kept as proof that nothing touches the tables.)

## Phase B - cancelled: the tables are preserved for Messaging v2

**The messaging tables are not dropped.** `Conversations`, `Messages`, `MessageReactions` and
`ConversationUserSettings` stay, with their data, entities, configurations, DbSets, repositories and migrations.
**Messaging v2** is planned: it will audit the existing schema and evolve it with additive migrations, not replace it.
There is no `DropMessagesConversations` migration and none is to be written; the Games / Bets / MyTuno contract task
(`docs/games-bets-mytuno-removal.md`) does not touch these tables.

What that means today:

| Table | Foreign keys | Indexes |
| --- | --- | --- |
| `MessageReactions` | `MessageId` -> Messages cascade; `UserId` -> users cascade | `IX_MessageReaction_MessageId_UserId` (unique), `IX_MessageReactions_UserId` |
| `Messages` | `ConversationId` -> Conversations cascade; `SenderId` -> users **set null** | `IX_Message_SenderId`, `IX_Message_ConversationId_CreatedAt` |
| `ConversationUserSettings` | `ConversationId` -> Conversations cascade; `UserId` -> users cascade | `IX_ConversationUserSettings_ConversationId_UserId` (unique), `IX_ConversationUserSettings_UserId` |
| `Conversations` | - | `IX_Conversation_Participants`, `IX_Conversation_LastMessageAt_IsArchived` |

No `AspNetUsers` column belongs to messaging, and no other table has a foreign key into these four. Kept code (input
for Messaging v2's audit, not dead code to clean up):

- `src/RTUB.Core/Entities/`: `Conversation`, `ConversationUserSettings`, `Message`, `MessageReaction`
- `src/RTUB.Application/Data/Configurations/`: the 4 `*Configuration.cs` of those entities
- the 4 DbSets in `ApplicationDbContext.cs`; `Message`, `Conversation`, `ConversationUserSettings` in
  `AuditConfiguration.ExcludedEntityTypes` (and the matching test in `AuditLogServiceTests`)
- tests: `RTUB.Core.Tests/Entities/{Conversation,ConversationUserSettings,Message}Tests.cs`, the two
  messaging lines in `DatabaseFixture.cs`

Until Messaging v2 ships, nothing in the app reads or writes the four tables (`RetiredMessagesRoutesTests` proves it).
The tables hold private member chats: treat any production or refreshed-DEV database copy accordingly.

## Where the old data still is

In the four tables, which keep every message (nothing reads or writes them), and in every database copy: the
pre-migration snapshots, the daily R2 database backups, any manual production backup, local `app.db` copies, and the
DEV database after a refresh (`DatabaseSanitizer` does not scrub messages).
