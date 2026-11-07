namespace RTUB.Application.Services;

/// <summary>
/// The push kinds a member can turn off on /profile (task 082, issue #263; owner decisions of #262 in
/// docs/push-notifications.md → Preference keys). The server is the one source: keys, pt-PT labels, groups and which
/// key is mandatory come from here, the client only renders them. Keys are stable and per account; a key with no row
/// is on. Only <see cref="PendingActions"/> is mandatory (shown checked and locked, never stored). Every push built by
/// <see cref="Factories.PushNotificationFactory"/> carries one of these keys (the factory test checks it), except the
/// /profile test push.
/// </summary>
public static class PushPreferenceCatalog
{
    public const string PendingActions = "pending-actions.daily";
    public const string EventsNew = "events.new";
    public const string EventsReminder = "events.reminder";
    public const string EventsMemberAnswers = "events.member-answers";
    public const string EventsManagerNotice = "events.manager-notice";
    public const string EventsVideo = "events.video";
    public const string DiscussionActivity = "discussion.activity";
    public const string Mentions = "mentions";
    public const string RehearsalsReminder = "rehearsals.reminder";
    public const string RehearsalsMemberAnswers = "rehearsals.member-answers";
    public const string RehearsalsManagerNotice = "rehearsals.manager-notice";
    public const string RehearsalsMyAttendance = "rehearsals.my-attendance";
    public const string RehearsalsApprovalsPending = "rehearsals.approvals-pending";
    public const string MeetingsNew = "meetings.new";
    public const string MeetingsReminder = "meetings.reminder";
    public const string MeetingsManagerNotice = "meetings.manager-notice";
    public const string MeetingsRequests = "meetings.requests";
    public const string RequestsPublic = "requests.public";
    public const string Questions = "questions";
    public const string Messages = "messages";
    public const string LeaderboardFirstPlace = "leaderboard.first-place";
    public const string LeaderboardComments = "leaderboard.comments";
    public const string MembersStatus = "members.status";
    public const string MembersReactivated = "members.reactivated";
    public const string MediaNew = "media.new";
    public const string GalleryTag = "gallery.tag";
    public const string LogisticsAssigned = "logistics.assigned";
    public const string LogisticsReminder = "logistics.reminder";
    public const string TreasuryCalotes = "treasury.calotes";
    public const string SummaryWeekly = "summary.weekly";
    public const string OwnerAnnouncement = "owner.announcement";

    /// <param name="Help">Shown under the label: a schedule or what it covers. Null when the label says it all.</param>
    public sealed record Item(string Key, string Group, string Label, string? Help, bool Mandatory);

    public static readonly IReadOnlyList<Item> Items =
    [
        new(PendingActions, "Geral", "Lembrete: As minhas ações pendentes",
            "Obrigatório. Todos os dias às 18:00, só quando tens algo pendente.", Mandatory: true),
        new(SummaryWeekly, "Geral", "Resumo semanal", "Segunda-feira, uma vez por semana.", false),
        new(OwnerAnnouncement, "Geral", "Avisos da RTUB", null, false),
        new(Mentions, "Geral", "Menções", "Quando alguém te menciona numa discussão ou na Logística.", false),
        new(Messages, "Geral", "Mensagens", "Cada conversa pode ser silenciada à parte em Mensagens.", false),

        new(EventsNew, "Atuações", "Novas atuações", null, false),
        new(EventsReminder, "Atuações", "Lembretes de atuações", null, false),
        new(EventsMemberAnswers, "Atuações", "Quando um membro vai ou não à atuação", "Só nas atuações em que disseste que vais.", false),
        new(EventsManagerNotice, "Atuações", "Avisos da direção sobre a atuação", null, false),
        new(DiscussionActivity, "Atuações", "Posts e comentários na discussão", null, false),
        new(EventsVideo, "Atuações", "Novos vídeos de atuações", null, false),

        new(RehearsalsReminder, "Ensaios", "Lembretes de ensaios", "Na véspera e no próprio dia.", false),
        new(RehearsalsMemberAnswers, "Ensaios", "Quando um membro vai ou não ao ensaio", "Só nos ensaios em que disseste que vais.", false),
        new(RehearsalsManagerNotice, "Ensaios", "Avisos sobre o ensaio", null, false),
        new(RehearsalsMyAttendance, "Ensaios", "A minha presença no ensaio (aprovada ou recusada)", null, false),
        new(RehearsalsApprovalsPending, "Ensaios", "Ensaios por aprovar", "Só para quem aprova presenças, às 15:00.", false),

        new(MeetingsNew, "Reuniões", "Reuniões convocadas", null, false),
        new(MeetingsReminder, "Reuniões", "Lembretes de reuniões", null, false),
        new(MeetingsManagerNotice, "Reuniões", "Avisos sobre a reunião", null, false),
        new(MeetingsRequests, "Reuniões", "Pedidos de reunião", "Para quem aprova pedidos, e para quem os fez.", false),

        new(RequestsPublic, "Tuna", "Pedidos de atuação", "Só para quem trata os pedidos do site.", false),
        new(Questions, "Tuna", "Perguntas", null, false),
        new(LeaderboardFirstPlace, "Tuna", "Novo 1.º lugar na Classificação", null, false),
        new(LeaderboardComments, "Tuna", "Comentários e likes no meu perfil", null, false),
        new(MembersStatus, "Tuna", "O meu estado na Tuna", "Avisos sobre a tua atividade e a passagem a Reformado.", false),
        new(MembersReactivated, "Tuna", "Membros reativados", null, false),
        new(MediaNew, "Tuna", "Novos conteúdos (música, naipes)", null, false),
        new(GalleryTag, "Tuna", "Fui identificado na Galeria", null, false),
        new(LogisticsAssigned, "Tuna", "Logística: tarefas atribuídas", null, false),
        new(LogisticsReminder, "Tuna", "Lembretes de Logística", null, false),
        new(TreasuryCalotes, "Tuna", "Calotes pendentes", "Só quando deves algo à tuna.", false),
    ];

    private static readonly Dictionary<string, Item> ByKey = Items.ToDictionary(i => i.Key, StringComparer.Ordinal);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);

    public static bool IsMandatory(string key) => ByKey.TryGetValue(key, out var item) && item.Mandatory;
}
