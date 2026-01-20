using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Text.Json;

namespace RTUB.Application.Services;

/// <summary>
/// Service for generating meeting minutes (Ata) PDFs using QuestPDF with caching support.
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

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                page.Header().ShowOnce().AlignCenter().Column(column =>
                {
                    column.Item().Text("Real Tuna Universitária de Bragança")
                        .FontSize(16).Bold().FontColor("#6f42c1");

                    column.Item().PaddingTop(5).Text("ATA")
                        .FontSize(24).Bold().FontColor(Colors.Black);

                    if (!string.IsNullOrEmpty(ata.AtaNumber))
                    {
                        column.Item().Text(ata.AtaNumber)
                            .FontSize(12).FontColor(Colors.Grey.Darken1);
                    }

                    if (ata.Meeting != null)
                    {
                        column.Item().PaddingTop(5).Text(GetMeetingTypeDisplayName(ata.Meeting.Type))
                            .FontSize(12).Bold().FontColor("#6f42c1");
                    }
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    // Meeting Information
                    column.Item().Background("#f8f9fa").Padding(15).Column(infoColumn =>
                    {
                        infoColumn.Item().Text("Informações da Reunião")
                            .FontSize(14).Bold().FontColor("#6f42c1");

                        if (ata.Meeting != null)
                        {
                            infoColumn.Item().PaddingTop(10).Text($"Título: {ata.Meeting.Title}")
                                .FontSize(11);
                        }

                        infoColumn.Item().PaddingTop(5).Text($"Data Agendada: {ata.Meeting?.Date:dd/MM/yyyy HH:mm}")
                            .FontSize(11);

                        infoColumn.Item().PaddingTop(5).Text($"Início Efetivo: {ata.ActualStartTime:dd/MM/yyyy HH:mm}")
                            .FontSize(11);

                        if (ata.ActualEndTime.HasValue)
                        {
                            infoColumn.Item().PaddingTop(5).Text($"Término: {ata.ActualEndTime.Value:dd/MM/yyyy HH:mm}")
                                .FontSize(11);
                        }

                        if (!string.IsNullOrEmpty(ata.Location))
                        {
                            infoColumn.Item().PaddingTop(5).Text($"Local: {ata.Location}")
                                .FontSize(11);
                        }

                        if (ata.PresidentUser != null)
                        {
                            infoColumn.Item().PaddingTop(5).Text($"Presidente: {ata.PresidentUser.Nickname ?? ata.PresidentUser.FirstName}")
                                .FontSize(11);
                        }

                        if (ata.FirstSecretaryUser != null)
                        {
                            infoColumn.Item().PaddingTop(5).Text($"1º Secretário: {ata.FirstSecretaryUser.Nickname ?? ata.FirstSecretaryUser.FirstName}")
                                .FontSize(11);
                        }

                        if (ata.SecondSecretaryUser != null)
                        {
                            infoColumn.Item().PaddingTop(5).Text($"2º Secretário: {ata.SecondSecretaryUser.Nickname ?? ata.SecondSecretaryUser.FirstName}")
                                .FontSize(11);
                        }

                        if (!string.IsNullOrEmpty(ata.QuorumBasis))
                        {
                            var quorumText = ata.QuorumBasis == "HoraAgendadaMais30Minutos"
                                ? "Iniciada 30 minutos após hora agendada"
                                : "Iniciada à hora agendada";
                            infoColumn.Item().PaddingTop(5).Text($"Quórum: {quorumText}")
                                .FontSize(11);
                        }
                    });

                    // Attendees
                    var presentIds = DeserializeAttendeeIds(ata.AttendeesPresent);
                    var absentIds = DeserializeAttendeeIds(ata.AttendeesAbsent);

                    if (presentIds.Any() || absentIds.Any())
                    {
                        column.Item().PaddingTop(20).Text("Presenças")
                            .FontSize(14).Bold().FontColor("#6f42c1");

                        if (presentIds.Any())
                        {
                            column.Item().PaddingTop(10).Text($"Presentes ({presentIds.Count}): {string.Join(", ", presentIds)}")
                                .FontSize(10);
                        }

                        if (absentIds.Any())
                        {
                            column.Item().PaddingTop(5).Text($"Ausentes ({absentIds.Count}): {string.Join(", ", absentIds)}")
                                .FontSize(10).FontColor(Colors.Grey.Darken1);
                        }
                    }

                    // Agenda Points
                    if (ata.AgendaPoints != null && ata.AgendaPoints.Any())
                    {
                        column.Item().PaddingTop(20).Text("Ordem de Trabalhos")
                            .FontSize(14).Bold().FontColor("#6f42c1");

                        foreach (var point in ata.AgendaPoints.OrderBy(p => p.PointNumber))
                        {
                            column.Item().PaddingTop(15).Column(pointColumn =>
                            {
                                pointColumn.Item().Background("#f0f0f0").Padding(10).Column(headerColumn =>
                                {
                                    headerColumn.Item().Text($"{point.PointNumber}. {point.Title}")
                                        .FontSize(12).Bold();

                                    if (!string.IsNullOrEmpty(point.DiscussionSummary))
                                    {
                                        headerColumn.Item().PaddingTop(5).Text(point.DiscussionSummary)
                                            .FontSize(10);
                                    }

                                    if (!string.IsNullOrEmpty(point.DecisionText))
                                    {
                                        headerColumn.Item().PaddingTop(5).Text($"Decisão: {point.DecisionText}")
                                            .FontSize(10).Bold();
                                    }

                                    if (point.VotesFor.HasValue || point.VotesAgainst.HasValue || point.VotesAbstain.HasValue)
                                    {
                                        var votingText = $"Votação: {point.VotesFor ?? 0} a favor, {point.VotesAgainst ?? 0} contra, {point.VotesAbstain ?? 0} abstenções";
                                        if (!string.IsNullOrEmpty(point.VoteResult))
                                        {
                                            votingText += $" - {point.VoteResult}";
                                        }
                                        var resultColor = point.VoteResult == "Aprovado" ? "#28a745" : "#dc3545";
                                        headerColumn.Item().PaddingTop(5).Text(votingText)
                                            .FontSize(10).FontColor(resultColor);
                                    }
                                });
                            });
                        }
                    }

                    // Closing Text
                    if (!string.IsNullOrEmpty(ata.ClosingText))
                    {
                        column.Item().PaddingTop(20).Text("Encerramento")
                            .FontSize(14).Bold().FontColor("#6f42c1");

                        column.Item().PaddingTop(10).Text(ata.ClosingText)
                            .FontSize(11);
                    }

                    // Attachments
                    if (ata.Attachments != null && ata.Attachments.Any(a => a.IncludeInPdf))
                    {
                        column.Item().PaddingTop(20).Text("Anexos")
                            .FontSize(14).Bold().FontColor("#6f42c1");

                        foreach (var attachment in ata.Attachments.Where(a => a.IncludeInPdf))
                        {
                            var attachmentText = attachment.Name;
                            if (!string.IsNullOrEmpty(attachment.Description))
                            {
                                attachmentText += $" - {attachment.Description}";
                            }
                            column.Item().PaddingTop(5).Text($"• {attachmentText}")
                                .FontSize(10);
                        }
                    }

                    // Signatures
                    column.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(sigCol =>
                        {
                            sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(50);
                            sigCol.Item().Text("Presidente").FontSize(10).AlignCenter();
                        });

                        row.ConstantItem(30);

                        row.RelativeItem().Column(sigCol =>
                        {
                            sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(50);
                            sigCol.Item().Text("1º Secretário").FontSize(10).AlignCenter();
                        });

                        if (ata.SecondSecretaryUser != null)
                        {
                            row.ConstantItem(30);

                            row.RelativeItem().Column(sigCol =>
                            {
                                sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(50);
                                sigCol.Item().Text("2º Secretário").FontSize(10).AlignCenter();
                            });
                        }
                    });
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

    private static string GetMeetingTypeDisplayName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => "Reunião"
    };

    private static List<string> DeserializeAttendeeIds(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
