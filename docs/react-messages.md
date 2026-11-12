# React Mensagens (`/messages`, Messaging v2)

A WhatsApp-like chat in the React portal over the four v1 messaging tables: `Conversations`, `Messages`,
`MessageReactions`, `ConversationUserSettings`. Text only, no attachments or media. Signed-in members only (a visitor is
sent to sign in). The open conversation is `/messages?c=<id>`. `/messages/{id}` and `/hubs/messages` are 404; there is no
SignalR hub or package.

Code: `Endpoints/MessagingEndpoints.cs` -> `MessagingService`, `Services/MessagingGroups.cs`, `portal/src/Messages.tsx`,
`portal/src/unread.ts`, `MemberShell.tsx` (`MemberInbox`).

## Tables are preserved: never drop them

`Conversations`, `Messages`, `MessageReactions` and `ConversationUserSettings` stay with their data, entities,
configurations, DbSets and migrations. Evolve them with additive migrations only; no `DropMessagesConversations`
migration exists or is to be written. The previous release runs `SyncDefaultGroupsAsync` at startup and rethrows, so it
would not start without `Conversations` (N-1 rule, [release-and-rollback](release-and-rollback.md)).

| Table | Foreign keys | Indexes |
| --- | --- | --- |
| `MessageReactions` | `MessageId` -> Messages cascade; `UserId` -> users cascade | `IX_MessageReaction_MessageId_UserId` (unique), `IX_MessageReactions_UserId` |
| `Messages` | `ConversationId` -> Conversations cascade; `SenderId` -> users set null | `IX_Message_SenderId`, `IX_Message_ConversationId_CreatedAt` |
| `ConversationUserSettings` | `ConversationId` -> Conversations cascade; `UserId` -> users cascade | `IX_ConversationUserSettings_ConversationId_UserId` (unique), `IX_ConversationUserSettings_UserId` |
| `Conversations` | - | `IX_Conversation_Participants`, `IX_Conversation_LastMessageAt_IsArchived` |

No `AspNetUsers` column belongs to messaging and no other table has a foreign key into these four. Outside the endpoints
above nothing touches them; `RetiredMessagesRoutesTests` holds a rollback-safety test (with the four tables dropped, a
signed-in page, every push fan-out and a member deletion run without SQL naming them). Pinned by
`MigrationChainTests.MigratedDatabase_KeepsTheMessagingTables_ThatMessagingV2Uses` and
`Model_HasNoChangesThatAMigrationWouldHaveToCapture`.

The tables hold private member chats. Treat every database copy accordingly: pre-migration snapshots, daily R2 database
backups, manual production backups, local `app.db` copies, and the DEV database after a refresh (`DatabaseSanitizer`
does not scrub messages). No R2 objects belong to messaging.

## Model

| Need | Model |
| --- | --- |
| Participants | `Conversation.Participants` (`;`-separated user ids), `IsGroup`, `CreatedByUserId`; one-to-one pairs |
| Replies | `Message.ReplyToMessageId` + denormalized `ReplyToBody` (<= 200) and `ReplyToSenderName` (<= 100) |
| Reactions | `MessageReactions` (emoji, unique `MessageId + UserId`); fixed emoji set |
| Muting / pinning | `ConversationUserSettings.IsMuted` / `IsPinned`; pinned conversations sort first |
| Read state | `Message.ReadBy` (`;`-separated user ids): unread counts and "Vista" |
| System notices | `IsSystemConversation` ("Notificações RTUB"), `Message.IsSystem`, `Message.Link`. Every push also lands here (`NotificationInbox`, saved as read, "Abrir" opens its link); see [push-notifications](push-notifications.md) |
| Announcement channels | `Conversation.IsAnnouncementOnly` |
| "Delete" | archives the conversation for everyone (`IsArchived`); archived ones stay hidden |

## API

Signed-in only (401 otherwise), `no-store`, every write needs `X-CSRF-TOKEN`. A conversation the caller does not take
part in, or an archived one, is a 404 (its existence is not revealed).

| Method | Path | Does |
| --- | --- | --- |
| GET | `/api/conversations` | my conversations: title, avatar, last-message preview, unread count, muted, pinned, `canSend`; pinned first, then latest activity |
| GET | `/api/conversations/unread` | `{ unreadCount }`: unread messages summed over my conversations (header badge); same rows as the list's `unreadCount` |
| GET | `/api/conversations/recipients` | members I can start a chat with (not me, not expelled) |
| GET | `/api/conversations/{id}?before=<messageId>` | the other participants and 50 messages (oldest first), `hasMore`, the reaction choices |
| GET | `/api/conversations/direct/{userId}` | `{ id }`: my one-to-one chat with that member, `null` if none (or archived) |
| POST | `/api/conversations` `{ userIds, title?, body }` | starts a conversation with its first message: one member = the one-to-one chat (an existing one is reused, un-archived); two or more + `title` (1-200) = a new group. Nothing is stored before a message |
| POST | `/api/conversations/{id}/messages` `{ body, replyToMessageId? }` | text 1-2000 after trimming; a reply must quote a message of the same conversation |
| DELETE | `/api/conversations/{id}` | "Remover conversa": one-to-one chats only (403 for groups and Notificações RTUB); archived for both, messages kept; a new message to the person brings it back |
| POST | `/api/conversations/{id}/read` | marks everything not sent by me as read by me |
| POST | `/api/conversations/{id}/mute`, `/unmute` | my mute flag |
| POST | `/api/messages/{id}/reactions` `{ emoji }` | sets my reaction (one per message; a new one replaces it) |
| DELETE | `/api/messages/{id}/reactions` | removes my reaction |

## Rules

- Who sees what: only participants (`Conversation.HasParticipant`). Admin and Owner have no special access to other
  people's conversations.
- Who writes: any participant, except: Notificações RTUB is read-only (no messages, no reactions); announcement-only
  channels take messages from the Owner and the current fiscal year's Magister, Vice-Magister, Presidents of the
  Conselho Fiscal / Mesa da Assembleia / Conselho de Veteranos and the Ensaiador; a one-to-one chat whose other member
  was deleted or expelled takes no new messages.
- Reactions: 👍 ❤️ 😂 😮 😢 🙏 only; older reactions outside the set still show.
- Unread: messages not sent by me whose `ReadBy` lacks my id. A muted conversation still counts unread.
- Push: every other participant who has not muted the conversation gets "Nova mensagem". The body names the sender (and
  the group), never the message text. It opens `/messages?c=<id>` (relative, no Host header). Sent inline through
  `SendToSelectedUsersAsync`; a failure is logged with the conversation id only.
- Old system links keep only their path, so a link can only open a page of this site; anything that would leave it
  (`//host`, a backslash, tabs or other whitespace, non-https) is rejected (`MessagingSafeLinkTests`).
- Premade groups are synced by `MessagingGroups` with every reconciliation of all members (the midnight job and `/users`
  "Sincronizar estado da Tuna") and once at every startup, even with `MemberStatusUpdate:Enabled` off: GERAL (every
  non-expelled account), NO ATIVO (not Reformado), TUNOS (Tuno, no Tuno Honorário), LEITÕES & CALOIROS; created if
  missing. Every other group created by the system (`CreatedByUserId = "system"`: VETERANOS, TUNOSSAUROS, ANUNCIOS,
  ORGANIZAÇÃO, Órgãos Sociais) is archived, messages kept. Expelled accounts are in none. Members' own groups are
  untouched.

## Privacy: `/owner/tracing` and logs

- `AuditConfiguration.ExcludedEntityTypes` excludes `Message`, `Conversation`, `ConversationUserSettings` and
  `MessageReaction`: no audit row is written, and older ones are filtered out of every tracing query and export.
- Defence in depth: `AuditConfiguration.RedactedProperties` makes `AuditLogAppender` write `[redacted]` for
  `Message.Body / ReplyToBody / ReplyToSenderName / ReadBy / Link`, `Conversation.Title / Participants` and
  `MessageReaction.Emoji` if one of those entities were ever audited. Targeted per entity: `Title` or `Body` on other
  entities stay readable.
- `MessagingService` logs no text, title, preview or emoji. Push logging records only the generic title.
- `/owner/db` (the Owner's raw database tool) can still `SELECT` from the messaging tables.

## UI

Conversation list + chat; one pane at a time below 900px (back button returns to the list), side by side above. Bubbles
(mine right), sender names in groups, day separators, reply quote, reaction chips + picker, "Vista" on my messages
someone has read, mute toggle, "Nova" to start a chat or group, a read-only notice where `canSend` is false, "Mensagens
anteriores" for older pages. Polls the open chat every 10 s and the list every 30 s while the tab is visible; opening a
chat marks it read. Entry: the inbox button in the member header (not the menu). Only Notificações RTUB wears the app
logo; groups show a plain people icon (`ChatFace`), people their photo.

"Nova": members are toggle buttons (`aria-pressed`); two or more ask for "Nome do grupo". Picking stores nothing: one
member whose chat exists (`/direct/{userId}`) opens it; otherwise a draft chat opens with the composer, and the first
message `POST`s `/api/conversations`, creating the conversation and that message together (no empty rows).

Header unread badge:
- Counts unread messages, not conversations: not sent by me and my id not in `ReadBy`, over my non-archived
  conversations (muted ones count). Shown as `1`..`99`, `99+`, nothing at 0; accessible name "Mensagens, N por ler".
- One number from `GET /api/conversations/unread`, polled every 10 s while the tab is visible, on `visibilitychange` and
  `online`. `/messages` publishes the sum from the list it loads and calls `useUnreadProvider()`, so while it is open the
  shell skips its own 10 s request. Every page load starts from the server's number; a failed poll keeps the last one.
- No push channel: "real time" is the poll, a message shows up within about 10 s.

Tests: `MessagingApiTests`, `MessagingV2SourceTests` (route, no attachment path, no text in logs or push),
`AuditLogAppenderTests` (redaction), `MessagingSafeLinkTests`, `RetiredMessagesRoutesTests` (`/messages/{id}` and the hub
stay 404).

## Follow-ups

- No member management (add / leave / rename) in groups; no pin / archive UI.
- No live push (polling); a hub is the option if 10 s is too slow.
- Push fan-out is inline and sequential: move to a background queue if large groups make sending slow.
- `/owner/db` can read private messages: decide whether to block the messaging tables there.
- DEV after a refresh holds production's messages unsanitized.
