using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for generating meeting minutes (Ata) PDFs using QuestPDF with caching support.
/// Follows the official RTUB templates for AG and CV meetings.
/// </summary>
public partial class AtaPdfService : IAtaPdfService
{
    private const string BrandPurple = "#6f42c1";
    private const string PointBackground = "#f0f0f0";
    private const string ApprovedGreen = "#28a745";
    private const string RejectedRed = "#dc3545";

    private readonly TimeProvider _time;

    /// <summary>The "Gerado em" stamp: Portugal's wall clock.</summary>
    internal string GeneratedAt() => PortugalTime.Now(_time).ToString("dd/MM/yyyy HH:mm");

    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public AtaPdfService(IMemoryCache cache, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
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

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                page.Header().ShowOnce().AlignCenter().Column(column => ComposeHeader(column, ata));

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    // "Identificação" counts as section 1 but is printed without a number.
                    var sectionNumber = 1;

                    AddIdentification(column, ata, isAG);
                    if (isAG) AddConvocation(column, ref sectionNumber);
                    AddBoard(column, ref sectionNumber, ata, isAG, isCV);
                    AddAttendance(column, ref sectionNumber, ata, isAG);
                    AddAgenda(column, ref sectionNumber, ata);
                    AddProceedings(column, ref sectionNumber, ata, isAG);
                    AddClosing(column, ref sectionNumber, ata, isAG);
                    AddSignatures(column, ref sectionNumber, ata, isCV);
                    if (ata.Attachments != null && ata.Attachments.Any(a => a.IncludeInPdf))
                    {
                        AddAttachments(column, ref sectionNumber, ata);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(Colors.Grey.Medium));
                    text.Span("Gerado em: ");
                    text.Span(GeneratedAt());
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

    private static void ComposeHeader(ColumnDescriptor column, MeetingAta ata)
    {
        column.Item().Text("Real Tuna Universitária de Bragança — Boémios e Trovadores (RTUB)")
            .FontSize(14).Bold().FontColor(BrandPurple);

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
    }

    /// <summary>Numbered section heading: advances <paramref name="number"/> and prints "n) text".</summary>
    private static void SectionTitle(ColumnDescriptor column, ref int number, string text)
    {
        number++;
        column.Item().PaddingTop(20).Text($"{number}) {text}").FontSize(14).Bold().FontColor(BrandPurple);
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
