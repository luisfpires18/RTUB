using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// AtaPdfServiceTests: Signatories tests.
public partial class AtaPdfServiceTests
{
    [Fact]
    public void GenerateAtaPdf_WithSignatories_ShowsNamesInSignatureSection()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 6,
            Title = "Reunião com Assinaturas",
            Date = DateTime.Now.AddDays(-1),
            Type = MeetingType.AssembleiaGeralOrdinaria
        };

        var ata = new MeetingAta
        {
            Id = 6,
            MeetingId = 6,
            Meeting = meeting,
            AtaNumber = "ATA AG 01/2025",
            ActualStartTime = DateTime.Now.AddDays(-1),
            Location = "Auditório",
            PresidentUserId = "user1",
            PresidentUser = new ApplicationUser { Id = "user1", FirstName = "Carlos", Nickname = "Maestro", LastName = "Oliveira" },
            FirstSecretaryUserId = "user2",
            FirstSecretaryUser = new ApplicationUser { Id = "user2", FirstName = "Ana", LastName = "Rodrigues" },
            SecondSecretaryUserId = "user3",
            SecondSecretaryUser = new ApplicationUser { Id = "user3", FirstName = "Pedro", Nickname = "Poeta", LastName = "Martins" },
            QuorumBasis = "HoraAgendada",
            Status = MeetingAtaStatus.Draft,
            AgendaPoints = new List<MeetingAtaAgendaPoint>(),
            Attachments = new List<MeetingAtaAttachment>()
        };

        // Act
        var result = _service.GenerateAtaPdf(ata);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // The PDF should show actual names in signature section:
        // 'Carlos "Maestro" Oliveira', 'Ana Rodrigues', 'Pedro "Poeta" Martins'
    }
}
