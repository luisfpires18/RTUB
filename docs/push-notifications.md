# Push notifications: catalogue and rules

Every Web Push RTUB sends, grouped by area, with the page a tap opens. Delivery, VAPID, subscriptions and the `/profile`
opt-in: [react-push-v2](react-push-v2.md). This doc is about what is sent and where it lands.

## Rules

1. One place builds a push: `src/RTUB.Application/Factories/PushNotificationFactory.cs` (`IPushNotificationFactory`).
   No `new SendPushNotificationDto` in a service; the only exception is the Owner's free text on `/notifications`
   (`OwnerOperationsService.SendPushAsync`).
2. The link is a relative path to the item itself (`/events/42`, never `/events`, never `https://host/...`). The factory
   takes no base URL: links built from the request Host failed the service worker's same-origin check when the sender
   used another host (`www.rtub.pt`, `rtub.azurewebsites.net`) and the tap opened the home page. A list is the target
   only when there is no single item (counts, summaries) or the page has no item view yet.
3. A tap always reaches the page. `service-worker.js` (`notificationclick`) stores the target for 60 s
   (`rtub-notification-target` cache), then focuses or navigates an open window, or opens one. `js/sw-register.js`
   reads that entry on load and on return to the app and goes there once, which covers installed apps that start on
   `start_url` (iPhone/iPad Home Screen, the Play Store TWA). Same-origin only.
4. Tags group a notification for the browser's replacement rules; the worker makes each shown tag unique and keeps the
   original in `data.baseTag`.
5. Every push is also saved in `/messages` ("Notificações RTUB" conversation, as read), push on or off. Not saved:
   chat-message pushes (`KeepInInbox = false`) and the `/profile` test push. Mechanics: `rtub-push` skill.
6. Every push carries a preference key (`SendPushNotificationDto.PreferenceKey`). The factory sets it and
   `PushNotificationService` drops the device push for members who turned that key off, after the inbox copy is saved.
   `PushPreferenceFilterTests` fails when a factory method has no key. See *Preference keys*.

Deep links the React pages accept: `/events/{id}`, `/events/{id}/discussion`, `/rehearsals/{id}`,
`/meetings?meeting={id}`, `/messages?c={id}`, `/gallery?item={id}`, `/music/albums/{id}`, `/music/play/{songId}`,
`/naipes?instrument={InstrumentType}`, `/treasury/calotes`, `/profile#push`, `/logistics/{boardId}?card={cardId}` (a
card or board the member can no longer see shows "não existe ou não tem acesso" and nothing of the card).
`/questions` and `/requests` have no item view yet; when they get one (`?question=` / `?request=`), switch the links.

## Catalogue

Recipients: all = `BroadcastAsync` (every subscribed member), some = `SendToSelectedUsersAsync`, one =
`SendToUserAsync` (possibly in a loop).

Reformados (`IsRetired`) receive every push their recipient rule picks, except the event, rehearsal and meeting
reminders (`ActivityReminderBackgroundService` loads `!IsRetired`). `PushNotificationService` and
`GetAllActiveAsync` never look at `IsRetired`. Background jobs run once a day at their `ScheduledTime` (Portugal time),
all `Enabled: true` in `appsettings.json`. Nothing reaches DEV (no VAPID keys). Azure App Settings may override an
`Enabled` flag: `scripts/config/audit-azure-app-settings.ps1`.

### Eventos
| Factory method | Trigger (caller) | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateEventNotification(e, isReminder: false)` | event created (`EventAgendaService`, `POST /api/events`; members-only events too) | all | event name | `/events/{id}` | `event-{id}` |
| `CreateEventNotification(e, isReminder: true)` | daily 11:30 (`ActivityReminderBackgroundService`): Festival 15/7/1/0 days before, others 1/0; every non-retired member, enrolled or not | some | "Lembrete: {name}" | `/events/{id}` | `event-reminder-{id}-{yyyyMMdd}` |
| `CreateEventCustomNotification` | manager's push (`EventAdminService`, `POST /api/events/{id}/notice`) | some | event name | `/events/{id}` | `event-custom-{id}` |
| `CreateEventEnrollmentNotification` | a member says "vou" (`EnrollmentService`) | some (enrolled) | "Nova inscrição" | `/events/{id}` | `event-enrollment-{id}` |
| `CreateEventCancellationNotification` | a member cancels, or a manager removes one | some | "Inscrição cancelada" | `/events/{id}` | `event-cancellation-{id}` |
| `CreateEventNonEnrollmentNotification` | a member says "não vou" | some | "Não vai ao evento" | `/events/{id}` | `event-non-enrollment-{id}` |
| `CreateEventVideoUploadNotification` | video added (`EventService`) | one (every account but the uploader) | "Novo vídeo" | `/events/{id}` | `event-video-{id}` |

The three answer pushes go only to members who said "vou", only for future events; manager-added participants and
contact calls skip them (`skipNotification`).

### Discussão (event discussion)
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateDiscussionPostNotification` | new post (`PostService`); a transport offer is a post | one (enrolled) | "Novo post de {nick}" | `/events/{id}/discussion` | `event-discussion-{id}` |
| `CreateDiscussionCommentNotification` | new comment (`CommentService`) | one (post owner, earlier commenters) | "Novo comentário de {nick}" | `/events/{id}/discussion` | `event-discussion-{id}` |
| `CreateMentionNotification` | @mention in post / comment, any event date | one | "{nick} mencionou-te" | `/events/{id}/discussion` | `mention-{id}` |

Posts and comments push only while `Event.Date >= DateTime.UtcNow` (stops at the event's start, UTC).

### Ensaios
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateRehearsalNotification` | manager's push (`RehearsalAdminService`, `POST /api/rehearsals/{id}/notice`) | some | "Ensaio - dd/MMM Dia" | `/rehearsals/{id}` | `rehearsal-{id}` |
| `CreateRehearsalReminderNotification` | day before and same day (`ActivityReminderBackgroundService`), every non-retired member | some | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-reminder-{id}` |
| `CreateRehearsalAttendanceNotification` | a member says "vou" (`RehearsalAttendanceService`, `RehearsalAgendaService.MarkAttendanceAsync`) | some (those who said "vou") | "Nova presença" | `/rehearsals/{id}` | `rehearsal-attendance-{id}` |
| `CreateRehearsalCancellationNotification` | a member cancels, or a manager removes a presença | some | "Presença cancelada" | `/rehearsals/{id}` | `rehearsal-cancellation-{id}` |
| `CreateRehearsalNonAttendanceNotification` | a member says "não vou" | some | "Não vai ao ensaio" | `/rehearsals/{id}` | `rehearsal-non-attendance-{id}` |
| `CreateRehearsalAttendanceApprovalNotification` | `POST .../attendances/{attendanceId}/approve` | one (the member) | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-attendance-approval-{id}` |
| `CreateRehearsalAttendanceRejectionNotification` | `POST .../attendances/{attendanceId}/refuse` (`RehearsalAdminService.RefuseAsync`) | one (the member) | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-attendance-rejection-{id}` |
| `CreatePendingRehearsalApprovalsReminderNotification` | daily 15:00, past rehearsals with pending presenças (`RehearsalApprovalReminderBackgroundService`) | one (approvers) | "Lembrete: Ensaios Pendentes" | `/rehearsals` | `pending-rehearsal-approvals-reminder` |

The React "remove" of a presença uses `DeleteAttendanceAsync` and sends no refusal.

### Reuniões
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateMeetingNotification(m, isReminder: false)` | meeting convened (`MeetingService.CreateMeetingAsync`); a request proposed (`MeetingRequestService`, no id yet) to Owners + the type's president | one (the type's audience) | "Nova Reunião Convocada: {type}" | `/meetings?meeting={id}` (`/meetings` for a request) | `meeting-{id}` |
| `CreateMeetingNotification(m, isReminder: true)` | day before and same day (`ActivityReminderBackgroundService`), non-retired members who can see it | some | "Lembrete: {type}" | `/meetings?meeting={id}` | `meeting-reminder-{id}` |
| `CreateMeetingCustomNotification` | manager's push (`MeetingBoardService`, `POST /api/meetings/{id}/push`) | one | "Reunião - {date}" | `/meetings?meeting={id}` | `meeting-custom-{id}` |
| `CreatePendingMeetingRequestReminderNotification` | manual reminder on `/meetings` + daily 10:00 (`PendingRequestReminderService`) | one (approvers) | "Lembrete: Pedido de {type}" | `/meetings` | `pending-meeting-request-{id}` |
| `CreateMeetingRequestRejectedNotification` | request rejected, or expired by the daily job | one (author) | "Pedido de reunião rejeitado" | `/meetings` | `meeting-request-rejected-{id}` |

### Pedidos de atuação
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateRequestNotification` | public request on `/request` (`PublicRequestEndpoints`, `RequestService`) | one (Admin, Owner) | "Novo Pedido de Atuação" | `/requests` | `request-{id}` |
| `CreatePendingPublicRequestsReminderNotification` | daily 10:00 (`PendingRequestReminderService`), when any pending | one (Admin, Owner) | "Lembrete: Pedidos Pendentes" | `/requests` | `pending-requests-reminder` |

### Perguntas
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateNewQuestionNotification` | question asked (`QuestionService`, `QuestionBoardService`) | one (asked member) | "Nova Pergunta" | `/questions` | `question-{id}` |
| `CreateQuestionAnsweredNotification` | asked member answers | one (author) | "Pergunta Respondida" | `/questions` | `question-reply-{replyId}` |
| `CreateQuestionReplyNotification` | author replies | one | "Nova Resposta à Pergunta" | `/questions` | `question-reply-{replyId}` |
| `CreateQuestionReminderNotification` | author's reminder | one | "Lembrete: Pergunta Pendente" | `/questions` | `question-reminder-{id}` |
| `CreatePendingQuestionsNotification` | daily 09:00 by default (`QuestionNotificationBackgroundService`) | one | "Pergunta Pendente" / "N Perguntas Pendentes" | `/questions` | `question-reminder` |

### Mensagens
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateConversationMessageNotification` | chat message (`MessagingService`); never the text; not while the chat is muted | some (participants) | "Nova mensagem" | `/messages?c={id}` | `message-{id}` |

### Classificação
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateLeaderboardFirstPlaceNotification` | Mondays with the weekly summary (`WeeklyNotificationBackgroundService.NotifyNewLeaderAsync`) | all | "Novo 1º Lugar no Ranking!" | `/leaderboard` | `leaderboard-first-place` |
| `CreateLeaderboardCommentNotification` | comment on a member's profile, from someone else (`LeaderboardCommentService`) | one | "Novo comentário no teu perfil" | `/leaderboard` | `leaderboard-comment` |
| `CreateLeaderboardCommentLikeNotification` | like on a comment, from someone else | one | "Novo like no teu comentário" | `/leaderboard` | `leaderboard-comment-like` |

New-leader rule: the all-time leaderboard's first row (level, XP; ties in users order) is compared with the leader
recorded last week (`AuditLog` `LeaderboardFirstPlace`, user "System"). New leader: broadcast and record. First run
only records. Nobody with XP: nothing. A comment/like push failure is swallowed with no log.

### Membros
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateMemberActivityReminderNotification` | manager's button on `/members` (`POST /api/members/{id}/reminder`, `MemberAdminService`) | one | "Lembrete: Participa numa Atividade!" | `/events` | `member-reminder-{userId}-{ticks}` |
| `CreateMemberReactivatedNotification` | Reformado back to active at 3/3 (`MemberStatusService`) | all | "Membro Reativado" | `/leaderboard` | `member-reactivated-{nick}` |
| `CreateMemberRetirementWarningNotification` | one month to Reformado (`MemberStatusService`) | one (that member) | "1 mês até reforma" | `/leaderboard` | `member-retirement-{nick}` |

The last two come only from the daily status run (`MemberStatusUpdate:ScheduledTime`, 00:00); `PushNotificationsEnabled`
is `true` in `appsettings.json` (an App Setting can turn it off). Never from triggers, the Owner sync or a member's
first evaluation; an approval that reactivates first stays silent. See [member-activity-status](member-activity-status.md).

### Música, Naipes, Galeria
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateSongVideoUploadNotification` | video added to a song (`SongService`) | one (every account but the uploader; exclusive album: its members) | "Novo vídeo" | `/music/albums/{albumId}` | `song-video-{id}` |
| `CreateSongLearnNotification` | Owner's bell on `/music/learn` (`MusicService.NotifyPlayAlongAsync`); never automatic | all (exclusive album: its list) | "Nova música para aprender" | `/music/play/{songId}` | `song-learn-{id}` |
| `CreateNaipeContentNotification` | naipe video / image (`NaipeService`) | all | "Novo {vídeo/imagem} em {instrument}" | `/naipes?instrument={InstrumentType}` | `naipe-content-{instrument}` |
| `CreateGalleryTagNotification` | tagged on upload (`GalleryManagementService`) | some (tagged) | "Nova media na Galeria" | `/gallery?item={id}` | `gallery-tag` |

### Logística
Card title and board name only: never the card's details or a comment's text. Never to the member who made the change.
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateLogisticsNotification(Owner)` | a manager makes a member the card's responsible (`LogisticsKanbanService`) | one (new owner) | "Logística: tarefa atribuída" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Collaborator)` | a manager adds a collaborator | one | "Logística: tarefa atribuída" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Mention)` | @mention in a new comment (an edit never notifies again) | some (mentioned) | "Logística: mencionaram-te" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Reminder)` | due card reminder (`LogisticsReminderBackgroundService` every minute, `LogisticsReminders:Enabled`, via `LogisticsReminderDelivery`; recipients re-checked at send time) | some (still seeing the board) | "Lembrete de Logística" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |

Rules: [react-logistics](react-logistics.md) → *Reminders and notifications*.

### Tesouraria
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateCalotesReminderNotification` | daily 10:30 (`CalotesNotificationBackgroundService`) | one (members who owe) | "Calote Pendente" | `/treasury/calotes` | `calotes-reminder` |

### Resumos
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateWeeklySummaryNotification` | Mondays (`WeeklyNotificationBackgroundService`) | one (every account) | "Resumo Semanal" | `/events`, else `/rehearsals`, else `/meetings`, else `/` | `weekly-summary` |
| `CreatePendingActionsReminderNotification` | daily 18:00 Portugal (`PendingActionsReminderService`), once per day, only a member whose `/pending-actions` has something pending (`CountPendingAsync > 0`) | one | "As minhas ações pendentes" (no count, no item details) | `/pending-actions` | `pending-actions-reminder` |

### Sistema
| Source | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreatePushTestNotification` | `/profile` → "Enviar notificação de teste" (`PushEndpoints`), 30 s cooldown | this device | "RTUB: notificação de teste" | `/profile#push` | `push-test` |
| `OwnerOperationsService.SendPushAsync` | Owner's `/notifications` | some | Owner's text | `/` | `announcement-{ticks}` |

## Adding a push

1. Add a `Create…Notification` to `IPushNotificationFactory` + `PushNotificationFactory`: pt-PT copy, a relative URL to
   the item, a tag.
2. Call it from the service with the narrowest method (`SendToUserAsync` / `SendToSelectedUsersAsync`; `BroadcastAsync`
   only for news meant for everyone).
3. Set `PreferenceKey` to a `PushPreferenceCatalog` key (existing, or a new `Item` with its pt-PT label, group and
   `Mandatory: false`; a new key starts on for everyone because no row means on). `PushPreferenceFilterTests` refuses a
   factory method without one.
4. Add the row to the catalogue above and to the *Preference keys* table; assert the URL in
   `PushNotificationFactoryTests`.
5. A page that should open one item needs a deep link (a route or a query parameter it reads on load).

## Preference keys

What a member receives, per account. Keys, pt-PT labels, groups and the mandatory flag live in
`PushPreferenceCatalog` (`src/RTUB.Application/Services/PushPreferenceCatalog.cs`), the one source. A member's choices
are `PushPreferences` rows: one row = one key off; no row = on. Read and saved through `GET` / `PUT
/api/push/preferences` from `/profile` → "Escolher o que receber" ([react-push-v2](react-push-v2.md) → *Preferences*).
Keys are stable, fine-grained and per account (not per device). New events, member answers and reminders are separate
keys on purpose. Mand. = mandatory (shown checked and locked).

| Key | pt-PT label | Factory methods | Mand. |
|---|---|---|---|
| `pending-actions.daily` | Lembrete: As minhas ações pendentes | `CreatePendingActionsReminderNotification` | yes |
| `events.new` | Novas atuações | `CreateEventNotification(isReminder: false)` | |
| `events.reminder` | Lembretes de atuações | `CreateEventNotification(isReminder: true)` | |
| `events.member-answers` | Quando um membro vai ou não à atuação | `CreateEventEnrollmentNotification`, `…CancellationNotification`, `…NonEnrollmentNotification` | |
| `events.manager-notice` | Avisos da direção sobre a atuação | `CreateEventCustomNotification` | |
| `events.video` | Novos vídeos de atuações | `CreateEventVideoUploadNotification` | |
| `discussion.activity` | Posts e comentários na discussão | `CreateDiscussionPostNotification`, `CreateDiscussionCommentNotification` | |
| `mentions` | Menções | `CreateMentionNotification`, `CreateLogisticsNotification(Mention)` | |
| `rehearsals.reminder` | Lembretes de ensaios | `CreateRehearsalReminderNotification` | |
| `rehearsals.member-answers` | Quando um membro vai ou não ao ensaio | `CreateRehearsalAttendanceNotification`, `…CancellationNotification`, `…NonAttendanceNotification` | |
| `rehearsals.manager-notice` | Avisos sobre o ensaio | `CreateRehearsalNotification` | |
| `rehearsals.my-attendance` | A minha presença no ensaio (aprovada ou recusada) | `CreateRehearsalAttendanceApprovalNotification`, `CreateRehearsalAttendanceRejectionNotification` | |
| `rehearsals.approvals-pending` | Ensaios por aprovar | `CreatePendingRehearsalApprovalsReminderNotification` | |
| `meetings.new` | Reuniões convocadas | `CreateMeetingNotification(isReminder: false)` | |
| `meetings.reminder` | Lembretes de reuniões | `CreateMeetingNotification(isReminder: true)` | |
| `meetings.manager-notice` | Avisos sobre a reunião | `CreateMeetingCustomNotification` | |
| `meetings.requests` | Pedidos de reunião | `CreateMeetingNotification` (proposal branch: meeting `Id` 0), `CreatePendingMeetingRequestReminderNotification`, `CreateMeetingRequestRejectedNotification` | |
| `requests.public` | Pedidos de atuação | `CreateRequestNotification`, `CreatePendingPublicRequestsReminderNotification` | |
| `questions` | Perguntas | `CreateNewQuestionNotification`, `…Answered…`, `…Reply…`, `…Reminder…`, `CreatePendingQuestionsNotification` | |
| `messages` | Mensagens | `CreateConversationMessageNotification` (per-chat mute applies on top: both must be on) | |
| `leaderboard.first-place` | Novo 1º lugar | `CreateLeaderboardFirstPlaceNotification` | |
| `leaderboard.comments` | Comentários e likes no meu perfil | `CreateLeaderboardCommentNotification`, `…CommentLikeNotification` | |
| `members.status` | O meu estado na Tuna | `CreateMemberRetirementWarningNotification`, `CreateMemberActivityReminderNotification` | |
| `members.reactivated` | Membros reativados | `CreateMemberReactivatedNotification` | |
| `media.new` | Novos conteúdos (música, naipes) | `CreateSongVideoUploadNotification`, `CreateSongLearnNotification`, `CreateNaipeContentNotification` | |
| `gallery.tag` | Fui identificado na Galeria | `CreateGalleryTagNotification` | |
| `logistics.assigned` | Logística: tarefas atribuídas | `CreateLogisticsNotification(Owner)`, `(Collaborator)` | |
| `logistics.reminder` | Lembretes de Logística | `CreateLogisticsNotification(Reminder)` | |
| `treasury.calotes` | Calotes pendentes | `CreateCalotesReminderNotification` | |
| `summary.weekly` | Resumo semanal | `CreateWeeklySummaryNotification` | |
| `owner.announcement` | Avisos da RTUB | `OwnerOperationsService.SendPushAsync` (outside the factory) | |

`CreatePushTestNotification` has no key and no switch.

Policy:

- Only `pending-actions.daily` is mandatory; every other key is optional, the Owner's announcements included. The
  mandatory key never has a row: `PUT` refuses it with a field error (never silently normalised) and
  `MutedUserIdsAsync` answers nobody for it. An unknown key is refused too, so a future key needs a catalogue entry
  first.
- Every optional key starts on, for existing and new accounts.
- Turning a key off stops the device push only; the "Notificações RTUB" copy in `/messages` is still saved (the copy is
  written before the preference is applied).
- Keys stay separate. The one combination is `rehearsals.my-attendance` (approval and refusal); a later split copies
  its value into both new keys.
- A preference never overrides a browser/OS permission or a missing device subscription; the device switch in
  `/profile` stays separate. The server enforces it before sending, on every path (one member, selected members,
  broadcast, background jobs, the Owner's text): `PushNotificationService.MutedAsync`. A stale client cannot bypass it.

Storage: per-key rows, not a bit field, so a key can be added or split without a migration. `PushPreferences`
(`UserId`, `Key` ≤ 64, `MutedAt`; unique `UserId + Key`, index on `Key`, FK to `AspNetUsers` with cascade), migration
`AddPushPreferences`.

Code: `PushNotificationFactory.cs`, `PushPreferenceCatalog.cs`, `PushNotificationService.cs`. Tests:
`PushNotificationFactoryTests`, `PushPreferenceFilterTests`.
