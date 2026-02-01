using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for generating meeting minutes (Ata) PDFs using QuestPDF with caching support.
/// Follows the official RTUB templates for AG and CV meetings.
/// </summary>
public class AtaPdfService : IAtaPdfService
{
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public AtaPdfService(IMemoryCache cache)
    {
        _cache = cache;
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Size = 1
        };
    }

    public byte[] GenerateAtaPdf(MeetingAta ata)
    {
        var timestamp = ata.UpdatedAt?.ToString("yyyyMMddHHmmss") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var cacheKey = $"ata-pdf-{ata.Id}-{timestamp}";

        if (_cache.TryGetValue<byte[]>(cacheKey, out var cachedPdf) && cachedPdf != null)
        {
            return cachedPdf;
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var isAG = ata.Meeting?.Type == MeetingType.AssembleiaGeralOrdinaria ||
                   ata.Meeting?.Type == MeetingType.AssembleiaGeralExtraordinaria;
        var isCV = ata.Meeting?.Type == MeetingType.ConselhoVeteranos;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                // Header - Official RTUB format
                page.Header().ShowOnce().AlignCenter().Column(column =>
                {
                    column.Item().Text("Real Tuna Universitária de Bragança — Boémios e Trovadores (RTUB)")
                        .FontSize(14).Bold().FontColor("#6f42c1");

                    if (ata.Meeting != null)
                    {
                        column.Item().PaddingTop(5).Text(GetOrganDisplayName(ata.Meeting.Type))
                            .FontSize(12).Bold();
                    }

                    column.Item().PaddingTop(10).Text("ATA")
                        .FontSize(20).Bold().FontColor(Colors.Black);

                    if (!string.IsNullOrEmpty(ata.AtaNumber))
                    {
                        column.Item().Text($"N.º {ata.AtaNumber}")
                            .FontSize(12).FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    var sectionNumber = 0;

                    // Section: Identificação
                    sectionNumber++;
                    column.Item().Text($"Identificação").FontSize(14).Bold().FontColor("#6f42c1");
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

                    // Section: Convocatória (AG only)
                    if (isAG)
                    {
                        sectionNumber++;
                        column.Item().PaddingTop(20).Text($"{sectionNumber}) Convocatória").FontSize(14).Bold().FontColor("#6f42c1");
                        column.Item().PaddingTop(10).Text("A Assembleia Geral foi convocada nos termos previstos, com indicação da ordem de trabalhos, local, dia e hora.")
                            .FontSize(10);
                    }

                    // Section: Mesa / Constituição e secretariado
                    sectionNumber++;
                    var mesaTitle = isAG ? $"{sectionNumber}) Mesa da Assembleia Geral" : $"{sectionNumber}) Constituição e secretariado";
                    column.Item().PaddingTop(20).Text(mesaTitle).FontSize(14).Bold().FontColor("#6f42c1");
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

                    // Section: Presenças e quórum
                    sectionNumber++;
                    var presencaTitle = isAG ? $"{sectionNumber}) Presenças e quórum" : $"{sectionNumber}) Presenças";
                    column.Item().PaddingTop(20).Text(presencaTitle).FontSize(14).Bold().FontColor("#6f42c1");

                    // Get attendees from MeetingParticipation with WillAttend=true
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

                        // Quorum section removed per user request
                    });

                    // Section: Ordem de trabalhos
                    sectionNumber++;
                    column.Item().PaddingTop(20).Text($"{sectionNumber}) Ordem de trabalhos").FontSize(14).Bold().FontColor("#6f42c1");

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

                    // Section: Desenvolvimento dos trabalhos e deliberações
                    sectionNumber++;
                    var devTitle = isAG
                        ? $"{sectionNumber}) Desenvolvimento dos trabalhos, propostas e deliberações"
                        : $"{sectionNumber}) Desenvolvimento dos trabalhos e deliberações";
                    column.Item().PaddingTop(20).Text(devTitle).FontSize(14).Bold().FontColor("#6f42c1");

                    if (ata.AgendaPoints != null && ata.AgendaPoints.Any())
                    {
                        foreach (var point in ata.AgendaPoints.OrderBy(p => p.PointNumber))
                        {
                            column.Item().PaddingTop(15).Column(pointColumn =>
                            {
                                pointColumn.Item().Background("#f0f0f0").Padding(10).Column(contentColumn =>
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
                                            var resultColor = point.VoteResult == "Aprovado" ? "#28a745" : "#dc3545";
                                            contentColumn.Item().PaddingTop(3).Text($"Resultado: {point.VoteResult}")
                                                .FontSize(10).Bold().FontColor(resultColor);
                                        }
                                    }
                                });
                            });
                        }
                    }

                    // Section: Encerramento
                    sectionNumber++;
                    column.Item().PaddingTop(20).Text($"{sectionNumber}) Encerramento").FontSize(14).Bold().FontColor("#6f42c1");
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

                    // Section: Assinaturas
                    sectionNumber++;
                    column.Item().PaddingTop(20).Text($"{sectionNumber}) Assinaturas").FontSize(14).Bold().FontColor("#6f42c1");

                    if (isCV)
                    {
                        // CV: Presidente do CV (always) + Secretário (only if selected)
                        var presidentName = ata.PresidentUser != null ? FormatUserName(ata.PresidentUser) : "";
                        var hasSecretary = ata.FirstSecretaryUser != null;
                        var secretaryName = hasSecretary ? FormatUserName(ata.FirstSecretaryUser!) : "";

                        column.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(sigCol =>
                            {
                                sigCol.Item().Text(presidentName).FontSize(10).AlignCenter();
                                sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
                                sigCol.Item().Text("O Presidente do CV").FontSize(9).AlignCenter();
                            });

                            if (hasSecretary)
                            {
                                row.ConstantItem(40);

                                row.RelativeItem().Column(sigCol =>
                                {
                                    sigCol.Item().Text(secretaryName).FontSize(10).AlignCenter();
                                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
                                    sigCol.Item().Text("O Secretário").FontSize(9).AlignCenter();
                                });
                            }
                        });
                    }
                    else
                    {
                        // AG: Presidente da Mesa (always) + 1.º Secretário (if selected) + 2.º Secretário (if selected)
                        var presidentName = ata.PresidentUser != null ? FormatUserName(ata.PresidentUser) : "";
                        var hasFirstSec = ata.FirstSecretaryUser != null;
                        var hasSecondSec = ata.SecondSecretaryUser != null;
                        var firstSecName = hasFirstSec ? FormatUserName(ata.FirstSecretaryUser!) : "";
                        var secondSecName = hasSecondSec ? FormatUserName(ata.SecondSecretaryUser!) : "";

                        column.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(sigCol =>
                            {
                                sigCol.Item().Text(presidentName).FontSize(10).AlignCenter();
                                sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
                                sigCol.Item().Text("O Presidente da Mesa").FontSize(9).AlignCenter();
                            });

                            if (hasFirstSec)
                            {
                                row.ConstantItem(20);

                                row.RelativeItem().Column(sigCol =>
                                {
                                    sigCol.Item().Text(firstSecName).FontSize(10).AlignCenter();
                                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
                                    sigCol.Item().Text("O 1.º Secretário").FontSize(9).AlignCenter();
                                });
                            }

                            if (hasSecondSec)
                            {
                                row.ConstantItem(20);

                                row.RelativeItem().Column(sigCol =>
                                {
                                    sigCol.Item().Text(secondSecName).FontSize(10).AlignCenter();
                                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
                                    sigCol.Item().Text("O 2.º Secretário").FontSize(9).AlignCenter();
                                });
                            }
                        });
                    }

                    // Section: Anexos
                    if (ata.Attachments != null && ata.Attachments.Any(a => a.IncludeInPdf))
                    {
                        sectionNumber++;
                        column.Item().PaddingTop(20).Text($"{sectionNumber}) Anexos").FontSize(14).Bold().FontColor("#6f42c1");
                        column.Item().PaddingTop(10).Column(attachColumn =>
                        {
                            foreach (var attachment in ata.Attachments.Where(a => a.IncludeInPdf))
                            {
                                var checkmark = "☑";
                                var attachmentText = attachment.Name;
                                if (!string.IsNullOrEmpty(attachment.Description))
                                {
                                    attachmentText += $" — {attachment.Description}";
                                }
                                attachColumn.Item().Text($"{checkmark} {attachmentText}").FontSize(10);
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(Colors.Grey.Medium));
                    text.Span("Gerado em: ");
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    text.Span(" | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        _cache.Set(cacheKey, pdfBytes, _cacheOptions);

        return pdfBytes;
    }

    private static string GetOrganDisplayName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos (CV)",
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral (AG)",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral (AG)",
        MeetingType.ReuniaoDirecao => "Direção",
        _ => "Reunião"
    };

    private static string GetMeetingSubType(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Ordinária",
        MeetingType.AssembleiaGeralOrdinaria => "Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Extraordinária",
        MeetingType.ReuniaoDirecao => "Ordinária",
        _ => "—"
    };

    private static string GetMeetingTypeDisplayName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => "Reunião"
    };

    /// <summary>
    /// Formats a user's name as "FirstName 'NickName' LastName" or "FirstName LastName" if no nickname
    /// </summary>
    private static string FormatUserName(ApplicationUser user)
    {
        if (user == null) return "Não disponível";

        var firstName = user.FirstName ?? "";
        var lastName = user.LastName ?? "";
        var nickname = user.Nickname;

        if (!string.IsNullOrEmpty(nickname))
        {
            return $"{firstName} \"{nickname}\" {lastName}".Trim();
        }

        return $"{firstName} {lastName}".Trim();
    }

    /// <summary>
    /// Gets formatted participant names from MeetingParticipation list based on attendance status
    /// </summary>
    private static List<string> GetFormattedParticipants(IEnumerable<MeetingParticipation>? participations, bool willAttend)
    {
        if (participations == null)
            return new List<string>();

        var result = new List<string>();
        foreach (var participation in participations)
        {
            if (participation.WillAttend == willAttend && participation.User != null)
            {
                result.Add(FormatUserName(participation.User));
            }
        }
        return result;
    }

}
