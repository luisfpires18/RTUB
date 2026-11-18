using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

// MeetingAccessTests: Roles And Positions tests.
public partial class MeetingAccessTests
{
    [Fact]
    public void Owner_WritesEveryAta()
    {
        var owner = Persona("Owner");

        foreach (var type in AllTypes)
        {
            CanWriteAta(owner, Meeting(type)).Should().BeTrue("Owner writes a {0} ata", type);
        }
    }

    [Fact]
    public void PresidenteDoCv_WritesCvAtasOnly()
    {
        var president = Persona("Presidente do CV");

        CanWriteAta(president, Meeting(MeetingType.ConselhoVeteranos)).Should().BeTrue();
        CanWriteAta(president, Meeting(MeetingType.AssembleiaGeralOrdinaria)).Should().BeFalse();
        CanWriteAta(president, Meeting(MeetingType.ReuniaoDirecao)).Should().BeFalse();
    }

    [Fact]
    public void TheTunoRepresentative_WritesTheirCvAta_AndNoOther()
    {
        var tuno = Persona("Tuno");

        CanWriteAta(tuno, Meeting(MeetingType.ConselhoVeteranos, representativeId: tuno.UserId)).Should().BeTrue("A5");
        CanWriteAta(tuno, Meeting(MeetingType.ConselhoVeteranos, representativeId: "another-tuno")).Should().BeFalse();
        CanWriteAta(tuno, Meeting(MeetingType.AssembleiaGeralOrdinaria)).Should().BeFalse();
    }

    [Fact]
    public void PresidenteDaMesa_WritesAssemblyAtasOnly()
    {
        var president = Persona("Presidente da Mesa");

        CanWriteAta(president, Meeting(MeetingType.AssembleiaGeralOrdinaria)).Should().BeTrue();
        CanWriteAta(president, Meeting(MeetingType.AssembleiaGeralExtraordinaria)).Should().BeTrue();
        CanWriteAta(president, Meeting(MeetingType.ConselhoVeteranos)).Should().BeFalse();
        CanWriteAta(president, Meeting(MeetingType.ReuniaoDirecao)).Should().BeFalse();
    }

    [Fact]
    public void SecretariosDaMesa_WriteOnlyAnAssemblyAtaAlreadySavedWithThem()
    {
        var first = Persona("1.º Secretário da Mesa");
        var second = Persona("2.º Secretário da Mesa");
        var assembly = Meeting(MeetingType.AssembleiaGeralOrdinaria);

        CanWriteAta(first, assembly).Should().BeFalse("the position alone does not write an ata that was never saved");
        CanWriteAta(second, assembly).Should().BeFalse();

        var saved = Ata(assembly, firstSecretaryId: first.UserId, secondSecretaryId: second.UserId);
        CanWriteAta(first, assembly, saved).Should().BeTrue();
        CanWriteAta(second, assembly, saved).Should().BeTrue();

        var someoneElses = Ata(assembly, firstSecretaryId: "other-1", secondSecretaryId: "other-2");
        CanWriteAta(first, assembly, someoneElses).Should().BeFalse("only the secretaries saved on that ata");
        CanWriteAta(second, assembly, someoneElses).Should().BeFalse();
    }

    [Fact]
    public void Magister_WritesDirecaoAtas_AndANamedSecretaryWritesTheirs()
    {
        var magister = Persona("Magister");
        var direcao = Meeting(MeetingType.ReuniaoDirecao);

        CanWriteAta(magister, direcao).Should().BeTrue();
        CanWriteAta(magister, Meeting(MeetingType.AssembleiaGeralOrdinaria)).Should().BeFalse();
        CanWriteAta(magister, Meeting(MeetingType.ConselhoVeteranos)).Should().BeFalse();

        var secretario = Persona("Secretário");
        CanWriteAta(secretario, direcao).Should().BeFalse("the Secretário writes a Direção ata once saved as its secretary");
        CanWriteAta(secretario, direcao, Ata(direcao, firstSecretaryId: secretario.UserId)).Should().BeTrue();
    }

    [Fact]
    public void ANamedCvSecretary_WritesThatCvAta()
    {
        var veterano = Persona("Veterano");
        var council = Meeting(MeetingType.ConselhoVeteranos);

        CanWriteAta(veterano, council).Should().BeFalse("a Veterano does not write a CV ata by default");
        CanWriteAta(veterano, council, Ata(council, firstSecretaryId: veterano.UserId)).Should().BeTrue();
        CanWriteAta(veterano, council, Ata(council, firstSecretaryId: "other")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Caloiro")]
    [InlineData("Tuno")]
    [InlineData("Veterano")]
    [InlineData("Tunossauro")]
    [InlineData("Membro")]
    [InlineData("Vice-Magister")]
    [InlineData("Presidente do CF")]
    public void PlainReaders_WriteNoAta(string who)
    {
        var access = Persona(who);

        foreach (var type in AllTypes)
        {
            CanWriteAta(access, Meeting(type)).Should().BeFalse("{0} writes no {1} ata", who, type);
        }
    }

    // ---------- small rules ----------

    [Theory]
    [InlineData(MeetingType.AssembleiaGeralOrdinaria, true)]
    [InlineData(MeetingType.AssembleiaGeralExtraordinaria, true)]
    [InlineData(MeetingType.ConselhoVeteranos, false)]
    [InlineData(MeetingType.ReuniaoDirecao, false)]
    public void IsAssembly_AgoAndAge(MeetingType type, bool expected)
    {
        MeetingAccess.IsAssembly(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(Position.Magister, true)]
    [InlineData(Position.ViceMagister, true)]
    [InlineData(Position.Secretario, true)]
    [InlineData(Position.PrimeiroTesoureiro, true)]
    [InlineData(Position.SegundoTesoureiro, true)]
    [InlineData(Position.PresidenteMesaAssembleia, false)]
    [InlineData(Position.PresidenteConselhoVeteranos, false)]
    [InlineData(Position.PresidenteConselhoFiscal, false)]
    [InlineData(Position.Ensaiador, false)]
    public void HoldsDirecaoPosition_TheFiveDirecaoPositions(Position position, bool expected)
    {
        MeetingAccess.HoldsDirecaoPosition(new ApplicationUser { Positions = [position] }).Should().Be(expected);
    }

    private static MeetingAta Ata(Meeting meeting, string? firstSecretaryId = null, string? secondSecretaryId = null) => new()
    {
        Id = 1,
        MeetingId = meeting.Id,
        Location = "Sede",
        PresidentUserId = "president",
        FirstSecretaryUserId = firstSecretaryId,
        SecondSecretaryUserId = secondSecretaryId,
        QuorumBasis = "HoraAgendada",
        AttendeesPresent = "[]",
        AttendeesAbsent = "[]",
    };
}
