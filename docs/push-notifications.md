# Push notifications — catalogue and rules

Every Web Push RTUB sends, grouped by area, with the page a tap opens. Delivery, VAPID, subscriptions and the
`/profile` opt-in live in `docs/react-push-v2.md`; this doc is about **what** is sent and **where it lands**.

## Rules (task 058)

1. **One place builds a push:** `src/RTUB.Application/Factories/PushNotificationFactory.cs` (interface
   `IPushNotificationFactory`). No `new SendPushNotificationDto` in a service; the only exception is the Owner's free
   text push on `/notifications` (`OwnerOperationsService.SendPushAsync`).
2. **The link is a relative path to the item itself** — `/events/42`, never `/events`, never
   `https://host/...`. The factory takes no base URL: links built from the request's Host header failed the service
   worker's same-origin check whenever the sender used another host (`www.rtub.pt`, `rtub.azurewebsites.net`) and the
   tap opened the home page. A list is the target only when there is no single item (counts, summaries) or the page
   has no item view yet.
3. **A tap always reaches the page.** `service-worker.js` (`notificationclick`) stores the target for 60 s
   (`rtub-notification-target` cache), then focuses / navigates an open window or opens one. `js/sw-register.js` reads that entry
   on load and on return to the app and goes there once — this covers installed apps that start on `start_url`
   (iPhone / iPad Home Screen, the Play Store TWA) or miss the worker's navigation. Same-origin only.
4. **Tags** group a notification for the browser's own replacement rules; the worker makes each shown tag unique and
   keeps the original in `data.baseTag`.
5. **Every push is kept in /messages** (task 061): `NotificationInbox` saves title + body + link in each recipient's
   "Notificações RTUB" conversation, as read, push on or off. Not kept: chat-message pushes (`KeepInInbox = false`,
   the message is already in its chat) and the `/profile` test push.
6. **Every push belongs to a category** (table headings below). Today the category is documentation only; the
   preference keys below (#262, undecided) will make it data.

Deep links the React pages accept: `/events/{id}`, `/events/{id}/discussion`, `/rehearsals/{id}`,
`/meetings?meeting={id}` (opens the meeting's details, 058), `/messages?c={id}`, `/gallery?item={id}`,
`/music/albums/{id}`, `/naipes?instrument={InstrumentType}`, `/treasury/calotes`, `/profile#push`,
`/logistics/{boardId}?card={cardId}` (opens the card on its board, 069; a card or board the member can no longer see
shows "não existe ou não tem acesso" and nothing of the card).

## Catalogue

Recipients: **all** = `BroadcastAsync` (every subscribed member), **some** = `SendToSelectedUsersAsync`,
**one** = `SendToUserAsync` (possibly in a loop).

### Eventos
| Factory method | Trigger (caller) | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateEventNotification(e, isReminder: false)` | event created (`EventAgendaService`) | all | event name | `/events/{id}` | `event-{id}` |
| `CreateEventNotification(e, isReminder: true)` | daily reminder (`ActivityReminderBackgroundService`) | some | "Lembrete: {name}" | `/events/{id}` | `event-reminder-{id}-{yyyyMMdd}` |
| `CreateEventCustomNotification` | manager's push from the event (`EventAdminService`) | some | event name | `/events/{id}` | `event-custom-{id}` |
| `CreateEventEnrollmentNotification` | a member says "vou" (`EnrollmentService`) | some (enrolled) | "Nova inscrição" | `/events/{id}` | `event-enrollment-{id}` |
| `CreateEventCancellationNotification` | a member cancels (`EnrollmentService`) | some | "Inscrição cancelada" | `/events/{id}` | `event-cancellation-{id}` |
| `CreateEventNonEnrollmentNotification` | a member says "não vou" (`EnrollmentService`) | some | "Não vai ao evento" | `/events/{id}` | `event-non-enrollment-{id}` |
| `CreateEventVideoUploadNotification` | video added to an event (`EventService`) | one | "Novo vídeo" | `/events/{id}` | `event-video-{id}` |

### Discussão (event discussion)
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateDiscussionPostNotification` | new post (`PostService`) | one (enrolled) | "Novo post de {nick}" | `/events/{id}/discussion` | `event-discussion-{id}` |
| `CreateDiscussionCommentNotification` | new comment (`CommentService`) | one (post owner, commenters) | "Novo comentário de {nick}" | `/events/{id}/discussion` | `event-discussion-{id}` |
| `CreateMentionNotification` | @mention in post / comment | one | "{nick} mencionou-te" | `/events/{id}/discussion` | `mention-{id}` |

### Ensaios
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateRehearsalNotification` | manager's push (`RehearsalAdminService`) | some | "Ensaio - dd/MMM Dia" | `/rehearsals/{id}` | `rehearsal-{id}` |
| `CreateRehearsalReminderNotification` | day before / same day (`ActivityReminderBackgroundService`) | some | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-reminder-{id}` |
| `CreateRehearsalAttendanceNotification` | a member says "vou" (`RehearsalAttendanceService`) | some | "Nova presença" | `/rehearsals/{id}` | `rehearsal-attendance-{id}` |
| `CreateRehearsalCancellationNotification` | a member cancels | some | "Presença cancelada" | `/rehearsals/{id}` | `rehearsal-cancellation-{id}` |
| `CreateRehearsalNonAttendanceNotification` | a member says "não vou" | some | "Não vai ao ensaio" | `/rehearsals/{id}` | `rehearsal-non-attendance-{id}` |
| `CreateRehearsalAttendanceApprovalNotification` | presença approved | one | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-attendance-approval-{id}` |
| `CreateRehearsalAttendanceRejectionNotification` | presença refused | one | "Ensaio - …" | `/rehearsals/{id}` | `rehearsal-attendance-rejection-{id}` |
| `CreatePendingRehearsalApprovalsReminderNotification` | daily, past rehearsals to approve (`RehearsalApprovalReminderBackgroundService`) | one (approvers) | "Lembrete: Ensaios Pendentes" | `/rehearsals` | `pending-rehearsal-approvals-reminder` |

### Reuniões
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateMeetingNotification(m, isReminder: false)` | meeting convened (`MeetingService`); a request proposed (`MeetingRequestService`, no id yet) | one (by meeting type) | "Nova Reunião Convocada: {type}" | `/meetings?meeting={id}` (`/meetings` for a request) | `meeting-{id}` |
| `CreateMeetingNotification(m, isReminder: true)` | daily reminder (`ActivityReminderBackgroundService`) | some | "Lembrete: {type}" | `/meetings?meeting={id}` | `meeting-reminder-{id}` |
| `CreateMeetingCustomNotification` | manager's push (`MeetingBoardService`) | one | "Reunião - {date}" | `/meetings?meeting={id}` | `meeting-custom-{id}` |
| `CreatePendingMeetingRequestReminderNotification` | request pending (`PendingRequestReminderService`, manual reminder) | one (approvers) | "Lembrete: Pedido de {type}" | `/meetings` | `pending-meeting-request-{id}` |
| `CreateMeetingRequestRejectedNotification` | request rejected / expired | one (author) | "Pedido de reunião rejeitado" | `/meetings` | `meeting-request-rejected-{id}` |

### Pedidos de atuação
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateRequestNotification` | public request on `/request` (`RequestService`) | one (Admin, Owner) | "Novo Pedido de Atuação" | `/requests` | `request-{id}` |
| `CreatePendingPublicRequestsReminderNotification` | daily (`PendingRequestReminderService`) | one (Admin, Owner) | "Lembrete: Pedidos Pendentes" | `/requests` | `pending-requests-reminder` |

### Perguntas
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateNewQuestionNotification` | question asked (`QuestionService`) | one (asked member) | "Nova Pergunta" | `/questions` | `question-{id}` |
| `CreateQuestionAnsweredNotification` | asked member answers | one (author) | "Pergunta Respondida" | `/questions` | `question-reply-{replyId}` |
| `CreateQuestionReplyNotification` | author replies | one | "Nova Resposta à Pergunta" | `/questions` | `question-reply-{replyId}` |
| `CreateQuestionReminderNotification` | author's reminder | one | "Lembrete: Pergunta Pendente" | `/questions` | `question-reminder-{id}` |
| `CreatePendingQuestionsNotification` | background (`QuestionNotificationBackgroundService`) | one | "Pergunta Pendente" / "N Perguntas Pendentes" | `/questions` | `question-reminder` |

`/questions` and `/requests` have no item view yet: when they get one (`?question=` / `?request=`), switch the links.

### Mensagens
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateConversationMessageNotification` | chat message (`MessagingService`; never the text; not while muted) | some (participants) | "Nova mensagem" | `/messages?c={id}` | `message-{id}` |

### Classificação
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateLeaderboardFirstPlaceNotification` | new all-time leader, checked Mondays (`WeeklyNotificationBackgroundService`) | all | "Novo 1º Lugar no Ranking!" | `/leaderboard` | `leaderboard-first-place` |
| `CreateLeaderboardCommentNotification` | comment on a member's profile (`LeaderboardCommentService`) | one | "Novo comentário no teu perfil" | `/leaderboard` | `leaderboard-comment` |
| `CreateLeaderboardCommentLikeNotification` | like on a comment | one | "Novo like no teu comentário" | `/leaderboard` | `leaderboard-comment-like` |

### Membros
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateMemberActivityReminderNotification` | manager's reminder (`MemberAdminService`) | one | "Lembrete: Participa numa Atividade!" | `/events` | `member-reminder-{userId}-{ticks}` |
| `CreateMemberReactivatedNotification` | Reformado back to active at 3/3, scheduled run only (`MemberStatusService`) | all | "Membro Reativado" | `/leaderboard` | `member-reactivated-{nick}` |
| `CreateMemberRetirementWarningNotification` | one month to Reformado, scheduled run only (`MemberStatusService`) | one (that member) | "1 mês até reforma" | `/leaderboard` | `member-retirement-{nick}` |

### Música, Naipes, Galeria
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateSongVideoUploadNotification` | video added to a song (`SongService`) | one | "Novo vídeo" | `/music/albums/{albumId}` | `song-video-{id}` |
| `CreateNaipeContentNotification` | naipe video / image (`NaipeService`) | all | "Novo {vídeo/imagem} em {instrument}" | `/naipes?instrument={InstrumentType}` | `naipe-content-{instrument}` |
| `CreateGalleryTagNotification` | tagged on upload (`GalleryManagementService`) | some (tagged) | "Nova media na Galeria" | `/gallery?item={id}` | `gallery-tag` |

### Logística (task 069)
Card title and board name only: never the card's details or a comment's text. Never to the member who made the change.
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateLogisticsNotification(Owner)` | a manager makes a member the card's responsible (card created or edited, `LogisticsKanbanService`) | one (new owner) | "Logística: tarefa atribuída" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Collaborator)` | a manager adds a collaborator | one | "Logística: tarefa atribuída" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Mention)` | @mention in a new comment (an edit never notifies again) | some (mentioned) | "Logística: mencionaram-te" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |
| `CreateLogisticsNotification(Reminder)` | a due card reminder (`LogisticsReminderDelivery`, every minute) | some (still seeing the board) | "Lembrete de Logística" | `/logistics/{boardId}?card={id}` | `logistics-card-{id}` |

### Tesouraria
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateCalotesReminderNotification` | members who owe (`CalotesNotificationBackgroundService`) | one | "Calote Pendente" | `/treasury/calotes` | `calotes-reminder` |

### Resumos
| Factory method | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreateWeeklySummaryNotification` | Mondays (`WeeklyNotificationBackgroundService`) | one | "Resumo Semanal" | `/events`, else `/rehearsals`, else `/meetings`, else `/` | `weekly-summary` |
| `CreatePendingActionsReminderNotification` (078) | daily 18:00 Portugal (`PendingActionsReminderService`) | one, only a member whose `/pending-actions` has something pending | "As minhas ações pendentes" (no count, no item details) | `/pending-actions` | `pending-actions-reminder` |

### Sistema
| Source | Trigger | Recipients | Title | Opens | Tag |
|---|---|---|---|---|---|
| `CreatePushTestNotification` | `/profile` → "Enviar notificação de teste" (`PushEndpoints`) | this device | "RTUB: notificação de teste" | `/profile#push` | `push-test` |
| `OwnerOperationsService.SendPushAsync` | Owner's `/notifications` | some | Owner's text | `/` | `announcement-{ticks}` |

## Does it send? (code audit, 2026-11-02)

Traced from each factory call to a live endpoint or a registered hosted job (`AddBackgroundServices`), with its
conditions. "Sends" means the code path runs in production; it reaches only members with push on in `/profile`, and
nothing reaches DEV (no VAPID keys there). Jobs run once a day at their `ScheduledTime` (Portugal time), all
`Enabled: true` in `appsettings.json`.

Reformado column: `PushNotificationService` and `PushSubscriptionRepository.GetAllActiveAsync` never look at
`IsRetired` ("active" there means every stored subscription). Only `ActivityReminderBackgroundService` (event,
rehearsal and meeting reminders) loads `Where(u => !u.IsRetired)`. Every other push reaches a Reformado whenever the
sender's own recipient rule picks them. A Leitão starts active (`InitialIsRetired`).

| Push | Verdict | Reformado (`IsRetired = true`) gets it? | Path / condition |
|---|---|---|---|
| Novo evento | ✅ Sends | Yes | `POST /api/events` → `EventAgendaService.CreateEventAsync` → all (members-only events too) |
| Lembrete evento | ✅ Sends | **No** (`!IsRetired` filter) | `ActivityReminderBackgroundService` (11:30): Festival 15/7/1/0 days before, others 1/0; every non-retired member, enrolled or not |
| Event custom push | ✅ Sends | Yes, if chosen | `POST /api/events/{id}/notice` (kind push) → chosen recipients |
| Nova inscrição / Inscrição cancelada / Não vai | ✅ Sends | Yes, if they said "vou" | member answers on `/events/{id}` (`EventAgendaService` → `EnrollmentService`); only future events, only to members who said "vou". Manager-added participants and contact calls skip it (`skipNotification`); a manager *removing* a member sends "Inscrição cancelada" |
| Novo vídeo (evento) | ✅ Sends | Yes | `POST /api/events/{id}/videos` → every account but the uploader |
| Novo post / comentário (discussão) | ⚠️ Narrow | Yes, if they said "vou" / took part | `/events/{id}/discussion`; only while `Event.Date >= DateTime.UtcNow` (stops at the event's start time, UTC) and only to members who said "vou" (comment: post author + earlier commenters); a transport offer is a post, so it pushes too |
| Menção | ✅ Sends | Yes | @mention in a post / comment, any event date |
| Ensaio custom push | ✅ Sends | Yes, if chosen | `POST /api/rehearsals/{id}/notice` |
| Lembrete ensaio | ✅ Sends | **No** (`!IsRetired` filter) | `ActivityReminderBackgroundService`: day before and same day, every non-retired member |
| Nova presença / Presença cancelada / Não vai ao ensaio | ✅ Sends | Yes, if they said "vou" | member answers on `/rehearsals/{id}` (`RehearsalAgendaService.MarkAttendanceAsync`) to members who said "vou". A manager removing a presença sends "Presença cancelada" to the others |
| Presença aprovada | ✅ Sends | Yes (own presença) | `POST /api/rehearsals/{id}/attendances/{attendanceId}/approve` (`RehearsalAdminService`) → the member |
| Presença recusada | ✅ Sends | Yes (own presença) | `POST /api/rehearsals/{id}/attendances/{attendanceId}/refuse` (`RehearsalAdminService.RefuseAsync` → `CancelAttendanceAsync(id, refuserId)`) → the member. (Corrected 2026-11-07; earlier marked never sent.) The React "remove" uses `DeleteAttendanceAsync` and sends no refusal |
| Lembrete: Ensaios Pendentes | ✅ Sends | Yes (the Ensaiador, any status) | `RehearsalApprovalReminderBackgroundService` (15:00) → approvers, when past rehearsals have pending presenças |
| Nova Reunião Convocada | ✅ Sends | Yes, if in the type's audience | `POST /api/meetings` → `MeetingService.CreateMeetingAsync` → the type's audience |
| Pedido de reunião (proposal) | ✅ Sends | Yes (Owners / president) | `/meetings` proposal → `MeetingRequestService.CreateAsync` → Owners + the type's president |
| Lembrete reunião | ✅ Sends | **No** (`!IsRetired` filter) | `ActivityReminderBackgroundService`: day before and same day, members who can see the meeting |
| Reunião custom push | ✅ Sends | Yes, if chosen | `POST /api/meetings/{id}/push` |
| Lembrete: Pedido de reunião | ✅ Sends | Yes (approvers) | manual reminder (`/meetings` requests) + `PendingRequestReminderService` (10:00) |
| Pedido de reunião rejeitado | ✅ Sends | Yes (own request) | reject on `/meetings`, or expired by the daily job |
| Novo Pedido de Atuação | ✅ Sends | Yes (Admin / Owner) | public `/request` (`PublicRequestEndpoints`) → Admin + Owner |
| As minhas ações pendentes | ✅ Sends (078) | Yes (own page not empty) | `PendingActionsReminderService` (18:00) → each member with `CountPendingAsync > 0`, once per day |
| Lembrete: Pedidos Pendentes | ✅ Sends | Yes (Admin / Owner) | `PendingRequestReminderService` (10:00) → Admin + Owner, when any pending |
| Nova Pergunta / Respondida / Resposta / Lembrete | ✅ Sends | Yes (asked member / author) | `/questions` (`QuestionBoardService`) |
| Perguntas Pendentes | ✅ Sends | Yes | `QuestionNotificationBackgroundService` (09:00 by default) |
| Nova mensagem | ✅ Sends | Yes (participant) | `/messages`; not while the chat is muted |
| Novo 1º Lugar no Ranking | ✅ Sends (058) | Yes (all) | Mondays with the weekly summary (`WeeklyNotificationBackgroundService.NotifyNewLeaderAsync`): the all-time leaderboard's first row (level, XP; ties in users order) vs the leader recorded last week (`AuditLog` `LeaderboardFirstPlace`, user "System"); new leader → broadcast + record; first run only records; nobody with XP → nothing. Was never sent before 058 (`RankingService.UpdateUserRankingAsync`, no caller, removed) |
| Comentário no teu perfil / Like no teu comentário | ✅ Sends | Yes | `/leaderboard` comment / like, only from someone else; a failure is swallowed with no log |
| Lembrete: Participa numa Atividade | ✅ Sends | Yes (aimed at them) | manager's button on `/members` (`POST /api/members/{id}/reminder`) |
| Membro Reativado / 1 mês até reforma | ✅ Sends (058) | Reativado: yes (all); reforma warning: no (only the member losing the status) | daily member status run (`MemberStatusUpdate:ScheduledTime`, 00:00) only; `PushNotificationsEnabled` is `true` in `appsettings.json` since 058 (an App Setting can still turn it off). Never from triggers, the Owner sync or a member's first evaluation; an approval that reactivates first stays silent. Detail: `docs/member-activity-status.md` → *Transitions* |
| Novo vídeo (música) | ✅ Sends | Yes | `/music` song video → every account (exclusive album: its members) but the uploader |
| Novo conteúdo Naipes | ✅ Sends | Yes | `/naipes` upload → all |
| Nova media na Galeria | ✅ Sends | Yes, if tagged | `/gallery` upload with people tagged → the tagged |
| Calote Pendente | ✅ Sends | Yes, if they owe | `CalotesNotificationBackgroundService` (10:30) → members who owe |
| Resumo Semanal | ✅ Sends | Yes (every account) | `WeeklyNotificationBackgroundService`, Mondays |
| Teste (`/profile`) | ✅ Sends | Yes (own device) | own device, 30 s cooldown |
| Owner custom (`/notifications`) | ✅ Sends | Yes, if chosen | Owner only |
| Logística: tarefa atribuída / mencionaram-te (069) | ✅ Sends | Yes, if picked | a manager sets the card's owner or adds a collaborator; a worker @mentions board viewers in a new comment; never the actor |
| Lembrete de Logística (069) | ✅ Sends | Yes, if targeted | `LogisticsReminderBackgroundService` (every minute, `LogisticsReminders:Enabled`) → `LogisticsReminderDelivery`: due card reminders, recipients re-checked at send time. Rules: `docs/react-logistics.md` → *Reminders and notifications* |

Not checked here: Azure App Settings that may override an `Enabled` flag (run
`scripts/config/audit-azure-app-settings.ps1`), and delivery on real devices.

## Adding a push

1. Add a `Create…Notification` to `IPushNotificationFactory` + `PushNotificationFactory`: pt-PT copy, a relative URL
   to the item, a tag.
2. Call it from the service; send with the narrowest method (`SendToUserAsync` / `SendToSelectedUsersAsync`;
   `BroadcastAsync` only for news meant for everyone).
3. Add the row to the catalogue above under its category, and assert the URL in `PushNotificationFactoryTests`.
4. A page that should open one item needs a deep link (a route or a query parameter it reads on load).

## Preference keys for #263 (audit #262, decided 2026-11-07)

Re-verified against source on 2026-11-07: the catalogue and "Does it send?" table above still hold (079 and 080 added
no push). The only pushes built outside the factory are the Owner's `/notifications` text
(`OwnerOperationsService.SendPushAsync`) and the `/profile` test (factory, sent by `PushEndpoints`). No dead factory method: "Presença recusada" is sent by the refuse
route (`RehearsalAdminService.RefuseAsync`); the earlier audit missed it.

Nothing below is built yet: #263 implements it. Owner decisions are recorded in the last two columns and below.
Keys are stable, fine-grained, and per account (not per device). New events, member answers and reminders are separate
keys on purpose. Abbreviations: Mand. = mandatory (shown checked and locked); Opt. = optional.

| Key (proposed) | pt-PT label | Factory method / branch | Recipients, frequency | Status | Mand./Opt. | Default for existing accounts |
|---|---|---|---|---|---|---|
| `pending-actions.daily` | Lembrete: As minhas ações pendentes | `CreatePendingActionsReminderNotification` | one, daily 18:00 if page not empty | sends (078) | **Mand. (owner)** | on, locked |
| `events.new` | Novas atuações | `CreateEventNotification(isReminder: false)` | all, on create | sends | Opt. | on |
| `events.reminder` | Lembretes de atuações | `CreateEventNotification(isReminder: true)` | non-Reformados, 11:30 | sends | Opt. | on |
| `events.member-answers` | Quando um membro vai ou não à atuação | `CreateEventEnrollmentNotification`, `…CancellationNotification`, `…NonEnrollmentNotification` | members who said "vou", per answer | sends | Opt. | on |
| `events.manager-notice` | Avisos da direção sobre a atuação | `CreateEventCustomNotification` | chosen | sends | Opt. | on |
| `events.video` | Novos vídeos de atuações | `CreateEventVideoUploadNotification` | every account but uploader | sends | Opt. | on |
| `discussion.activity` | Posts e comentários na discussão | `CreateDiscussionPostNotification`, `CreateDiscussionCommentNotification` | "vou" / participants, until event start | sends (narrow) | Opt. | on |
| `mentions` | Menções | `CreateMentionNotification`, `CreateLogisticsNotification(Mention)` | mentioned | sends | Opt. | on |
| `rehearsals.reminder` | Lembretes de ensaios | `CreateRehearsalReminderNotification` | non-Reformados, day before + same day | sends | Opt. | on |
| `rehearsals.member-answers` | Quando um membro vai ou não ao ensaio | `CreateRehearsalAttendanceNotification`, `…CancellationNotification`, `…NonAttendanceNotification` | members who said "vou" | sends | Opt. | on |
| `rehearsals.manager-notice` | Avisos sobre o ensaio | `CreateRehearsalNotification` | chosen | sends | Opt. | on |
| `rehearsals.my-attendance` | A minha presença no ensaio (aprovada ou recusada) | `CreateRehearsalAttendanceApprovalNotification`, `CreateRehearsalAttendanceRejectionNotification` | one, on approve / refuse | sends | Opt. | on |
| `rehearsals.approvals-pending` | Ensaios por aprovar | `CreatePendingRehearsalApprovalsReminderNotification` | approvers, 15:00 | sends | Opt. | on |
| `meetings.new` | Reuniões convocadas | `CreateMeetingNotification(isReminder: false)` | meeting type audience | sends | Opt. | on |
| `meetings.reminder` | Lembretes de reuniões | `CreateMeetingNotification(isReminder: true)` | non-Reformados who see it | sends | Opt. | on |
| `meetings.manager-notice` | Avisos sobre a reunião | `CreateMeetingCustomNotification` | chosen | sends | Opt. | on |
| `meetings.requests` | Pedidos de reunião | `CreateMeetingNotification` (proposal branch), `CreatePendingMeetingRequestReminderNotification`, `CreateMeetingRequestRejectedNotification` | approvers / author | sends | Opt. | on |
| `requests.public` | Pedidos de atuação | `CreateRequestNotification`, `CreatePendingPublicRequestsReminderNotification` | Admin + Owner, on request + 10:00 | sends | Opt. | on |
| `questions` | Perguntas | `CreateNewQuestionNotification`, `…Answered…`, `…Reply…`, `…Reminder…`, `CreatePendingQuestionsNotification` | asked member / author, + 09:00 job | sends | Opt. | on |
| `messages` | Mensagens | `CreateConversationMessageNotification` (not kept in inbox; per-chat mute stays) | participants | sends | Opt. | on |
| `leaderboard.first-place` | Novo 1º lugar | `CreateLeaderboardFirstPlaceNotification` | all, Mondays | sends | Opt. | on |
| `leaderboard.comments` | Comentários e likes no meu perfil | `CreateLeaderboardCommentNotification`, `…CommentLikeNotification` | one | sends | Opt. | on |
| `members.status` | O meu estado na Tuna | `CreateMemberRetirementWarningNotification`, `CreateMemberActivityReminderNotification` | that member | sends | Opt. | on |
| `members.reactivated` | Membros reativados | `CreateMemberReactivatedNotification` | all, daily status run | sends | Opt. | on |
| `media.new` | Novos conteúdos (música, naipes) | `CreateSongVideoUploadNotification`, `CreateNaipeContentNotification` | all / album members | sends | Opt. | on |
| `gallery.tag` | Fui identificado na Galeria | `CreateGalleryTagNotification` | tagged | sends | Opt. | on |
| `logistics.assigned` | Logística: tarefas atribuídas | `CreateLogisticsNotification(Owner)`, `(Collaborator)` | one | sends (069) | Opt. | on |
| `logistics.reminder` | Lembretes de Logística | `CreateLogisticsNotification(Reminder)` | targeted, every minute job | sends (069) | Opt. | on |
| `treasury.calotes` | Calotes pendentes | `CreateCalotesReminderNotification` | members who owe, 10:30 | sends | Opt. | on |
| `summary.weekly` | Resumo semanal | `CreateWeeklySummaryNotification` | every account, Mondays (keep schedule) | sends | Opt. | on |
| `owner.announcement` | Avisos da RTUB | `OwnerOperationsService.SendPushAsync` (outside factory) | chosen by Owner | sends | Opt. | on |
| — | (no switch) | `CreatePushTestNotification` | own device, on request | sends | not a preference | — |

Owner decisions (#262, 2026-11-07):

1. Only `pending-actions.daily` is mandatory (checked and locked). Every other key is optional, the Owner's
   announcements included.
2. Every optional key starts **on**, for existing and new accounts: the rollout changes nothing until a member turns
   one off.
3. Turning a key off stops the device push only; the "Notificações RTUB" copy in `/messages` is still saved.
4. Keys stay separate. The only combination is `rehearsals.my-attendance` (approval and refusal); a later split copies
   its value into both new keys.

Also fixed: a preference never overrides a browser / OS permission or a missing device
subscription; the device switch in `/profile` stays separate; the server enforces the preference before sending
(`PushNotificationService`, subscriptions already loaded with `.Include(s => s.User)`), so a stale client cannot bypass it.
Storage shape (per-key rows vs a bit field) is decided in #263 once the keys are final.
