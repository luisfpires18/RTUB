using QuestPDF.Fluent;
using QuestPDF.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// The body sections of the ata, in page order. Signatures and attachments are in AtaPdfService.Signatures.cs.
public partial class AtaPdfService
{
    private static void AddIdentification(ColumnDescriptor column, MeetingAta ata, bool isAG)
    {
        column.Item().Text("Identificação").FontSize(14).Bold().FontColor(BrandPurple);
        column.Item().PaddingTop(10).Column(idColumn =>
        {
            idColumn.Item().Text("Entidade: Real Tuna Universitária de Bragança — Boémios e Trovadores (RTUB)").FontSize(10);
            if (ata.Meeting != null)
            {
                idColumn.Item().Text($"Órgão: {GetOrganDisplayName(ata.Meeting.Type)}").FontSize(10);
                // Only show "Tipo de reunião" for AG meetings, not CV
                if (isAG)
                {
                    idColumn.Item().Text($"Tipo de reunião: {GetMeetingSubType(ata.Meeting.Type)}").FontSize(10);
                }
            }
            if (!string.IsNullOrEmpty(ata.AtaNumber))
            {
                idColumn.Item().Text($"Ata n.º: {ata.AtaNumber}").FontSize(10);
            }
            idColumn.Item().Text($"Data: {ata.Meeting?.Date:dd/MM/yyyy}").FontSize(10);
            idColumn.Item().Text($"Hora marcada: {ata.Meeting?.Date:HH:mm}").FontSize(10);
            idColumn.Item().Text($"Hora de início efetiva: {ata.ActualStartTime:HH:mm}").FontSize(10);
            if (!string.IsNullOrEmpty(ata.Location))
            {
                idColumn.Item().Text($"Local: {ata.Location}").FontSize(10);
            }
        });
    }

    // AG only.
    private static void AddConvocation(ColumnDescriptor column, ref int sectionNumber)
    {
        SectionTitle(column, ref sectionNumber, "Convocatória");
        column.Item().PaddingTop(10).Text("A Assembleia Geral foi convocada nos termos previstos, com indicação da ordem de trabalhos, local, dia e hora.")
            .FontSize(10);
    }

    // Mesa (AG) / constituição e secretariado (CV and others).
    private static void AddBoard(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata, bool isAG, bool isCV)
    {
        SectionTitle(column, ref sectionNumber, isAG ? "Mesa da Assembleia Geral" : "Constituição e secretariado");
        column.Item().PaddingTop(10).Column(mesaColumn =>
        {
            var presidentTitle = isAG ? "Presidente da Mesa" : "Presidente do CV";
            if (ata.PresidentUser != null)
            {
                mesaColumn.Item().Text($"{presidentTitle}: {FormatUserName(ata.PresidentUser)}").FontSize(10);
            }
            if (ata.FirstSecretaryUser != null)
            {
                // CV: just "Secretário", AG: "1.º Secretário"
                var firstSecLabel = isCV ? "Secretário" : "1.º Secretário";
                mesaColumn.Item().Text($"{firstSecLabel}: {FormatUserName(ata.FirstSecretaryUser)}").FontSize(10);
            }
            // Only show 2.º Secretário for AG meetings
            if (isAG && ata.SecondSecretaryUser != null)
            {
                mesaColumn.Item().Text($"2.º Secretário: {FormatUserName(ata.SecondSecretaryUser)}").FontSize(10);
            }
        });
    }

    private static void AddAttendance(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata, bool isAG)
    {
        SectionTitle(column, ref sectionNumber, isAG ? "Presenças e quórum" : "Presenças");

        // Attendees are the MeetingParticipation rows with WillAttend=true
        var presentParticipants = GetFormattedParticipants(ata.Meeting?.Participations, willAttend: true);

        column.Item().PaddingTop(10).Column(presColumn =>
        {
            if (presentParticipants.Any())
            {
                presColumn.Item().Text($"Presentes ({presentParticipants.Count}):").FontSize(10).Bold();
                foreach (var name in presentParticipants)
                {
                    presColumn.Item().Text($"  • {name}").FontSize(10);
                }
            }
        });
    }

    private static void AddAgenda(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata)
    {
        SectionTitle(column, ref sectionNumber, "Ordem de trabalhos");

        if (ata.AgendaPoints != null && ata.AgendaPoints.Any())
        {
            column.Item().PaddingTop(10).Column(agendaColumn =>
            {
                foreach (var point in ata.AgendaPoints.OrderBy(p => p.PointNumber))
                {
                    agendaColumn.Item().Text($"{point.PointNumber}. {point.Title}").FontSize(10);
                }
            });
        }
        else
        {
            column.Item().PaddingTop(10).Text("(Nenhum ponto de ordem de trabalhos registado)").FontSize(10).Italic();
        }
    }

    // Desenvolvimento dos trabalhos e deliberações: one shaded box per agenda point.
    private static void AddProceedings(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata, bool isAG)
    {
        SectionTitle(column, ref sectionNumber, isAG
            ? "Desenvolvimento dos trabalhos, propostas e deliberações"
            : "Desenvolvimento dos trabalhos e deliberações");

        if (ata.AgendaPoints == null || !ata.AgendaPoints.Any()) return;

        foreach (var point in ata.AgendaPoints.OrderBy(p => p.PointNumber))
        {
            AddAgendaPoint(column, point, isAG);
        }
    }

    private static void AddAgendaPoint(ColumnDescriptor column, MeetingAtaAgendaPoint point, bool isAG)
    {
        column.Item().PaddingTop(15).Column(pointColumn =>
        {
            pointColumn.Item().Background(PointBackground).Padding(10).Column(contentColumn =>
            {
                contentColumn.Item().Text($"Ponto {point.PointNumber} — {point.Title}")
                    .FontSize(12).Bold();

                if (!string.IsNullOrEmpty(point.DiscussionSummary))
                {
                    contentColumn.Item().PaddingTop(5).Text("Discussão (resumo):").FontSize(10).Bold();
                    contentColumn.Item().Text($"  {point.DiscussionSummary}").FontSize(10);
                }

                if (!string.IsNullOrEmpty(point.DecisionText))
                {
                    var decisionLabel = isAG ? "Proposta submetida:" : "Deliberação:";
                    contentColumn.Item().PaddingTop(5).Text(decisionLabel).FontSize(10).Bold();
                    contentColumn.Item().Text($"  {point.DecisionText}").FontSize(10);
                }

                if (point.VotesFor.HasValue || point.VotesAgainst.HasValue || point.VotesAbstain.HasValue)
                {
                    contentColumn.Item().PaddingTop(5).Text("Votação:").FontSize(10).Bold();
                    contentColumn.Item().Text($"  • A favor: {point.VotesFor ?? 0}").FontSize(10);
                    contentColumn.Item().Text($"  • Contra: {point.VotesAgainst ?? 0}").FontSize(10);
                    contentColumn.Item().Text($"  • Abstenções: {point.VotesAbstain ?? 0}").FontSize(10);

                    if (!string.IsNullOrEmpty(point.VoteResult))
                    {
                        var resultColor = point.VoteResult == "Aprovado" ? ApprovedGreen : RejectedRed;
                        contentColumn.Item().PaddingTop(3).Text($"Resultado: {point.VoteResult}")
                            .FontSize(10).Bold().FontColor(resultColor);
                    }
                }
            });
        });
    }

    private static void AddClosing(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata, bool isAG)
    {
        SectionTitle(column, ref sectionNumber, "Encerramento");
        column.Item().PaddingTop(10).Column(endColumn =>
        {
            var presidentTitle = isAG ? "Presidente da Mesa" : "Presidente";
            if (ata.ActualEndTime.HasValue)
            {
                endColumn.Item().Text($"Nada mais havendo a tratar, o {presidentTitle} deu por encerrada a sessão às {ata.ActualEndTime.Value:HH:mm}.")
                    .FontSize(10);
            }
            else
            {
                endColumn.Item().Text($"Nada mais havendo a tratar, o {presidentTitle} deu por encerrada a sessão.")
                    .FontSize(10);
            }

            if (!string.IsNullOrEmpty(ata.ClosingText))
            {
                endColumn.Item().PaddingTop(5).Text("Texto de encerramento/nota final:").FontSize(10).Bold();
                endColumn.Item().Text($"  {ata.ClosingText}").FontSize(10);
            }
        });
    }
}
