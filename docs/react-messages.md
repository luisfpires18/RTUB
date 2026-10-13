# React Mensagens (Messaging v2, task 041)

`/messages` is Messaging v2: a WhatsApp-like chat in the React portal over the **v1 messaging tables that task 027
preserved** (`Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`). It is the old feature rebuilt,
not a new system: same model, same rules, **no schema change, no migration**. Text only — no attachments, no media.

Signed-in members only (a visitor going to `/messages` is sent to sign in). The conversation open on the page is
`/messages?c=<id>`. `/messages/{id}` and `/hubs/messages` (the 027-retired inbox and SignalR hub) stay 404.

## v1 audit: what the preserved model already supports

| Need | v1 model | v2 |
| --- | --- | --- |
| Participants | `Conversation.Participants` (`;`-separated user ids), `IsGroup`, `CreatedByUserId` | reused; one-to-one pairs as v1 stored them |
| Replies | `Message.ReplyToMessageId` + denormalized `ReplyToBody` (≤200) and `ReplyToSenderName` (≤100) | reused as is |
| Reactions | `MessageReactions` (emoji, unique `MessageId + UserId`) | reused; emoji limited to a fixed set |
| Muting / pinning | `ConversationUserSettings.IsMuted` / `IsPinned` (unique per member + conversation) | mute exposed; pinned conversations still sort first |
| Read state | `Message.ReadBy` (`;`-separated user ids) | reused for unread counts and "Vista" |
| System notices | `IsSystemConversation` ("Sistema RTUB"), `Message.IsSystem`, `Message.Link` | shown read-only (their history is kept) |
| Announcement channels | `Conversation.IsAnnouncementOnly` | v1's sender rule kept |
| "Delete" | v1 archived the conversation for everyone (`IsArchived`) | archived ones stay hidden; no delete in v2 |

Nothing needed a migration (`MigrationChainTests` pins the columns; `Model_HasNoChangesThatAMigrationWouldHaveToCapture`
pins the model). Not reused: v1's repositories / services / hub (deleted in 027, history in `a121f3f1^`), the group
auto-sync (`GroupConversationSyncService`), unread badges and SignalR live updates.

## API (`Endpoints/MessagingEndpoints.cs` → `MessagingService`)

Signed-in only (401 otherwise), `no-store`, every write needs `X-CSRF-TOKEN`. A conversation the caller does not take
part in, or an archived one, is a **404** (its existence is not revealed).

| Method | Path | Does |
| --- | --- | --- |
| GET | `/api/conversations` | my conversations: title, avatar, last-message preview, unread count, muted, pinned, `canSend`; pinned first, then latest activity |
| GET | `/api/conversations/recipients` | members I can start a chat with (not me, not expelled) |
| GET | `/api/conversations/{id}?before=<messageId>` | one conversation: the other participants and 50 messages (oldest first), `hasMore`, the reaction choices |
| POST | `/api/conversations` `{ userId }` | opens (or reopens, un-archiving) the one-to-one conversation with that member |
| POST | `/api/conversations/{id}/messages` `{ body, replyToMessageId? }` | text 1-2000 after trimming; a reply must quote a message of the same conversation |
| POST | `/api/conversations/{id}/read` | marks everything not sent by me as read by me |
| POST | `/api/conversations/{id}/mute`, `/unmute` | my mute flag (`ConversationUserSettings`) |
| POST | `/api/messages/{id}/reactions` `{ emoji }` | sets my reaction (one per message; a new one replaces it) |
| DELETE | `/api/messages/{id}/reactions` | removes my reaction |

## Rules (from v1, now enforced server-side)

- **Who sees what:** only participants (`Conversation.HasParticipant`). **Admin and Owner have no special access** to
  other people's conversations.
- **Who writes:** any participant, except: "Sistema RTUB" conversations are read-only (no messages, no reactions);
  announcement-only channels take messages from the Owner and the current fiscal year's Magister, Vice-Magister,
  Presidents of the Conselho Fiscal / Mesa da Assembleia / Conselho de Veteranos and the Ensaiador (v1's list); a
  one-to-one chat whose other member was deleted or expelled takes no new messages.
- **Reactions:** 👍 ❤️ 😂 😮 😢 🙏 only (no free text); older v1 reactions outside the set still show.
- **Unread:** messages not sent by me whose `ReadBy` lacks my id. A muted conversation still counts unread.
- **Push:** every other participant who has not muted the conversation gets "Nova mensagem" — the body names the sender
  (and the group), **never the message text**; it opens `/messages?c=<id>` (relative, so no Host header is involved).
  Sent inline through `SendToSelectedUsersAsync`; a failure is logged with the conversation id only.
- **Old system links** keep only their path, so a link can only open a page of this site: anything that would leave
  it (`//host`, a backslash, tabs or other whitespace, non-https) is rejected (`MessagingSafeLinkTests`).

## Privacy: `/owner/tracing` and logs

- `AuditConfiguration.ExcludedEntityTypes` excludes `Message`, `Conversation`, `ConversationUserSettings` and (since
  041) `MessageReaction`: no audit row is written, and older ones are filtered out of every tracing query and export.
- Defence in depth: `AuditConfiguration.RedactedProperties` makes `AuditLogAppender` write `[redacted]` for
  `Message.Body / ReplyToBody / ReplyToSenderName / ReadBy / Link`, `Conversation.Title / Participants` and
  `MessageReaction.Emoji` if one of those entities were ever audited. Targeted per entity: `Title` or `Body` on other
  entities stay readable.
- `MessagingService` logs no text, title, preview or emoji. Push logging records only the generic title.
- Not changed: `/owner/db` (the Owner's raw database tool) can still `SELECT` from the messaging tables, as before 041.

## UI (`portal/src/Messages.tsx`)

Conversation list + chat; one pane at a time below 900px (the back button returns to the list), side by side above.
Bubbles (mine right), sender names in groups, day separators, reply quote, reaction chips + picker, "Vista" on my
messages someone has read, mute toggle in the chat header, "Nova" to start a chat, read-only notice where `canSend` is
false, "Mensagens anteriores" for older pages. Polls the open chat every 10 s and the list every 30 s while the tab is
visible; opening a chat marks it read. Menu: "Mensagens" in the member group.

## Tests

`MessagingApiTests` (API end to end: visibility, Admin/Owner refusal, validation, replies, reactions, mute + push,
read state, system / announcement / group / archived / expelled rules, paging, tracing), `MessagingV2SourceTests`
(route, no attachment path, no old inbox, no text in logs or push), `AuditLogAppenderTests` (redaction),
`MigrationChainTests.MigratedDatabase_KeepsTheMessagingTables_ThatMessagingV2Uses`, `RetiredMessagesRoutesTests`
(`/messages/{id}` and the hub stay 404; `/messages` is the React page).

## Follow-ups

- Group membership is frozen at what v1's auto-sync last wrote before task 027; no group creation or member
  management in v2 yet. Decide whether to bring back the default groups' sync.
- No live push to an open chat (polling); no unread badge in the member menu; no pin / archive UI.
- Push fan-out is inline and sequential: move to a background queue if large groups make sending slow.
- `/owner/db` can read private messages: decide whether to block the messaging tables there.
- DEV after a refresh holds production's messages unsanitized (`DatabaseSanitizer` skips them).
