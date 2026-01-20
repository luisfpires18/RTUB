using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AtaPdfService
/// Tests PDF generation for meeting minutes
/// </summary>
public class AtaPdfServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly AtaPdfService _service;

    public AtaPdfServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new AtaPdfService(_cache);
    }

    [Fact]
    public void GenerateAtaPdf_WithValidAta_GeneratesNonEmptyPdf()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 1,
            Title = "Reunião de Teste",
            Date = DateTime.Now.AddDays(-1),
            Type = MeetingType.ConselhoVeteranos,
            Location = "Sala de Reuniões"
        };

        var ata = new MeetingAta
        {
            Id = 1,
            MeetingId = 1,
            Meeting = meeting,
            AtaNumber = "ATA CV 01/2025",
            ActualStartTime = DateTime.Now.AddDays(-1),
            ActualEndTime = DateTime.Now.AddDays(-1).AddHours(2),
            Location = "Sala de Reuniões",
            PresidentUserId = "user1",
            PresidentUser = new ApplicationUser { Id = "user1", Nickname = "Presidente", FirstName = "João" },
            FirstSecretaryUserId = "user2",
            FirstSecretaryUser = new ApplicationUser { Id = "user2", Nickname = "Secretário", FirstName = "Maria" },
            QuorumBasis = "HoraAgendada",
            ClosingText = "A reunião foi encerrada às 20h00.",
            Status = MeetingAtaStatus.Draft,
            AgendaPoints = new List<MeetingAtaAgendaPoint>
            {
                new MeetingAtaAgendaPoint
                {
                    Id = 1,
                    PointNumber = 1,
                    Title = "Aprovação da ordem de trabalhos",
                    DiscussionSummary = "A ordem de trabalhos foi apresentada e discutida.",
                    DecisionText = "Aprovada por unanimidade",
                    VotesFor = 10,
                    VotesAgainst = 0,
                    VotesAbstain = 0,
                    VoteResult = "Aprovado"
                }
            },
            Attachments = new List<MeetingAtaAttachment>()
        };

        // Act
        var result = _service.GenerateAtaPdf(ata);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateAtaPdf_WithMinimalData_GeneratesPdf()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 2,
            Title = "Reunião Mínima",
            Date = DateTime.Now.AddDays(-1),
            Type = MeetingType.AssembleiaGeralOrdinaria
        };

        var ata = new MeetingAta
        {
            Id = 2,
            MeetingId = 2,
            Meeting = meeting,
            ActualStartTime = DateTime.Now.AddDays(-1),
            PresidentUserId = "user1",
            FirstSecretaryUserId = "user2",
            Status = MeetingAtaStatus.Draft,
            AgendaPoints = new List<MeetingAtaAgendaPoint>(),
            Attachments = new List<MeetingAtaAttachment>()
        };

        // Act
        var result = _service.GenerateAtaPdf(ata);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateAtaPdf_SameAtaTwice_UsesCachedVersion()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 3,
            Title = "Cached Ata Test",
            Date = DateTime.Now.AddDays(-1),
            Type = MeetingType.ConselhoVeteranos
        };

        var ata = new MeetingAta
        {
            Id = 3,
            MeetingId = 3,
            Meeting = meeting,
            ActualStartTime = DateTime.Now.AddDays(-1),
            PresidentUserId = "user1",
            FirstSecretaryUserId = "user2",
            Status = MeetingAtaStatus.Draft,
            AgendaPoints = new List<MeetingAtaAgendaPoint>(),
            Attachments = new List<MeetingAtaAttachment>()
        };

        // Set UpdatedAt for consistent cache key
        typeof(MeetingAta).GetProperty("UpdatedAt")!.SetValue(ata, DateTime.UtcNow);

        // Act
        var result1 = _service.GenerateAtaPdf(ata);
        var result2 = _service.GenerateAtaPdf(ata);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().Equal(result2);
        result1.Should().BeSameAs(result2); // Same reference from cache
    }

    [Fact]
    public void GenerateAtaPdf_WithAttachments_GeneratesPdf()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 4,
            Title = "Reunião com Anexos",
            Date = DateTime.Now.AddDays(-1),
            Type = MeetingType.AssembleiaGeralExtraordinaria
        };

        var ata = new MeetingAta
        {
            Id = 4,
            MeetingId = 4,
            Meeting = meeting,
            ActualStartTime = DateTime.Now.AddDays(-1),
            PresidentUserId = "user1",
            FirstSecretaryUserId = "user2",
            Status = MeetingAtaStatus.Published,
            AgendaPoints = new List<MeetingAtaAgendaPoint>(),
            Attachments = new List<MeetingAtaAttachment>
            {
                new MeetingAtaAttachment
                {
                    Id = 1,
                    Name = "Convocatória",
                    Description = "Documento de convocação da reunião",
                    AttachmentType = "ConvocatoriaNotice",
                    IncludeInPdf = true
                },
                new MeetingAtaAttachment
                {
                    Id = 2,
                    Name = "Folha de Presenças",
                    AttachmentType = "AttendanceSheet",
                    IncludeInPdf = true
                }
            }
        };

        // Act
        var result = _service.GenerateAtaPdf(ata);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
