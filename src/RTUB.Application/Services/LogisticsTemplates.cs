using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

/// <summary>
/// The Logística templates (task 067): the four workflow columns and suggested tasks, with checklists and deadlines
/// relative to the event date (days before it; 0 = the day). The page previews them with real dates from the chosen
/// event, lets the manager edit or leave out any task, and creates the board only on confirmation
/// (<see cref="LogisticsKanbanService.ComposeBoardAsync"/>). Nothing is assigned or notified.
/// </summary>
public static class LogisticsTemplates
{
    private static LogisticsTemplateCardDto Task(string title, int? offset, string priority, params string[] checklist) =>
        new(title, "", priority, offset, checklist);

    private static IReadOnlyList<LogisticsTemplateListDto> Columns(params LogisticsTemplateCardDto[] todo) =>
    [
        new("Por fazer", "Todo", todo),
        new("Em curso", "InProgress", []),
        new("Bloqueado", "Blocked", []),
        new("Feito", "Done", []),
    ];

    public static readonly IReadOnlyList<LogisticsTemplateDto> All =
    [
        new("fitab", "Organizar FITAB", "O festival: tunas convidadas, alojamento, júri, patrocínios, credenciais e o dia.", Columns(
            Task("Confirmar tunas convidadas", -60, "High", "Enviar convites", "Confirmar número de elementos por tuna", "Pedir rider técnico"),
            Task("Patrocínios", -45, "Normal", "Lista de patrocinadores a contactar", "Enviar propostas", "Recolher logótipos para o cartaz"),
            Task("Reservar alojamento", -45, "High", "Pavilhão ou escola", "Colchões e mantas", "Horário das chaves"),
            Task("Júri e prémios", -30, "Normal", "Convidar o júri", "Encomendar troféus", "Presunto"),
            Task("Cartaz e divulgação", -21, "Normal", "Cartaz final", "Publicações nas redes sociais", "Bilhética"),
            Task("Som e luz", -14, "Normal", "Pedir orçamentos", "Confirmar horário de montagem"),
            Task("Credenciais", -14, "Normal", "Lista final de nomes", "Imprimir", "Plastificar"),
            Task("Bar e jantares", -10, "Normal", "Encomendas de bebida e comida", "Escalas do bar"),
            Task("Dia do festival", 0, "Urgent", "Escalas da organização", "Receção às tunas", "Limpeza final"))),
        new("deslocacao", "Preparar deslocação/atuação", "Uma atuação fora: quem vai, transporte, alojamento, repertório e material.", Columns(
            Task("Confirmar presenças", -14, "High", "Lista de quem vai", "Instrumentos por naipe"),
            Task("Transporte", -10, "High", "Reservar autocarro ou carrinhas", "Hora e local de partida", "Lista de lugares"),
            Task("Alojamento e refeições", -10, "Normal", "Confirmar dormida", "Refeições incluídas?"),
            Task("Repertório", -7, "Normal", "Alinhamento", "Ensaio extra"),
            Task("Traje e material", -3, "Normal", "Trajes completos", "Cordas e acessórios", "Estandarte"),
            Task("Dia da atuação", 0, "Urgent", "Ponto de encontro", "Contactos da organização"))),
    ];
}
