using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Golden hashes of the generated ata PDFs. There is no PDF text reader in the solution, so the
/// layout is pinned by the PDF bytes with the creation date and document id blanked: any change
/// to wording, numbering, order, fonts or spacing changes the hash. Captured from the code before
/// <c>AtaPdfService</c> was split into section methods.
/// </summary>
public class AtaPdfServiceGoldenTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 6, 1, 12, 30, 0, TimeSpan.Zero);
    }

    private static ApplicationUser User(string id, string first, string last, string? nick) =>
        new() { Id = id, FirstName = first, LastName = last, Nickname = nick! };

    private static MeetingAta Ata(MeetingType type, bool secondSecretary, bool secretary, bool withBody)
    {
        var meeting = new Meeting
        {
            Id = 7,
            Title = "Reunião",
            Date = new DateTime(2025, 5, 30, 21, 0, 0),
            Type = type,
            Participations = new List<MeetingParticipation>
            {
                new() { WillAttend = true, User = User("p1", "Ana", "Silva", "Aninhas") },
                new() { WillAttend = true, User = User("p2", "Rui", "Costa", null) },
                new() { WillAttend = false, User = User("p3", "Luís", "Pires", "Ausente") },
            },
        };

        var ata = new MeetingAta
        {
            Id = 7,
            MeetingId = 7,
            Meeting = meeting,
            AtaNumber = "ATA 03/2025",
            ActualStartTime = new DateTime(2025, 5, 30, 21, 10, 0),
            Location = "Sala 1",
            PresidentUser = User("u1", "João", "Presidente", "Pres"),
            UpdatedAt = new DateTime(2025, 5, 31, 8, 0, 0),
            Attachments = new List<MeetingAtaAttachment>(),
            AgendaPoints = new List<MeetingAtaAgendaPoint>(),
        };

        if (secretary) ata.FirstSecretaryUser = User("u2", "Maria", "Secretária", "Sec");
        if (secondSecretary) ata.SecondSecretaryUser = User("u3", "Pedro", "Segundo", null);
        if (!withBody) return ata;

        ata.ActualEndTime = new DateTime(2025, 5, 30, 23, 5, 0);
        ata.ClosingText = "Nota final da ata.";
        ata.AgendaPoints = new List<MeetingAtaAgendaPoint>
        {
            new()
            {
                PointNumber = 2, Title = "Segundo ponto", DiscussionSummary = "Resumo B",
                DecisionText = "Proposta B", VotesFor = 3, VotesAgainst = 9, VotesAbstain = 1, VoteResult = "Rejeitado",
            },
            new()
            {
                PointNumber = 1, Title = "Primeiro ponto", DiscussionSummary = "Resumo A",
                DecisionText = "Proposta A", VotesFor = 10, VoteResult = "Aprovado",
            },
            new() { PointNumber = 3, Title = "Sem votos" },
        };
        ata.Attachments = new List<MeetingAtaAttachment>
        {
            new() { Name = "Relatório", Description = "Contas 2024", IncludeInPdf = true },
            new() { Name = "Escondido", IncludeInPdf = false },
            new() { Name = "Plano", IncludeInPdf = true },
        };
        return ata;
    }

    private static string Fingerprint(MeetingAta ata)
    {
        var service = new AtaPdfService(new MemoryCache(new MemoryCacheOptions()), new FixedTime());
        var pdf = Encoding.Latin1.GetString(service.GenerateAtaPdf(ata));
        pdf = Regex.Replace(pdf, @"/(CreationDate|ModDate) ?\(D:[^)]*\)", "/$1()");
        pdf = Regex.Replace(pdf, @"/ID ?\[[^\]]*\]", "/ID[]");
        return Convert.ToHexString(SHA256.HashData(Encoding.Latin1.GetBytes(pdf)));
    }

    [Fact]
    public void Pdf_IsStableAcrossRuns()
    {
        var first = Fingerprint(Ata(MeetingType.AssembleiaGeralOrdinaria, true, true, true));
        Thread.Sleep(1100);
        Fingerprint(Ata(MeetingType.AssembleiaGeralOrdinaria, true, true, true)).Should().Be(first);
    }

    public static TheoryData<string, MeetingType, bool, bool, bool, string> Cases => new()
    {
        { "ag-full", MeetingType.AssembleiaGeralOrdinaria, true, true, true, "4CD61BE15C545DDD49E1A817EA0485665A1E2E4B474577EAC4F6C0ACBC91433E" },
        { "ag-extra-bare", MeetingType.AssembleiaGeralExtraordinaria, false, false, false, "BA655F3AEC85C99E4F48C6AFA82F62C1BAF0A05B9F67F418AFA8393CF518CBAB" },
        { "ag-no-second", MeetingType.AssembleiaGeralOrdinaria, false, true, true, "9FCA5672C9B70388F8BA5BE508D93D26BB1732D1C624228ED058FDB38F038377" },
        { "cv-full", MeetingType.ConselhoVeteranos, false, true, true, "BCA7FFDEDEC4A2134448F21170E2885DDB41C1FB37F3394159B97530C6422997" },
        { "cv-no-secretary", MeetingType.ConselhoVeteranos, false, false, false, "CD524E2B0B4CF0C5534161677846A1E67B05379BB595B2521F5A42F28CEBE677" },
        { "direcao", MeetingType.ReuniaoDirecao, false, true, true, "A796BF1449D14983FA703D90F411F53A07039B925EFDD852F340F35B277A67CC" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pdf_MatchesGolden(string name, MeetingType type, bool second, bool secretary, bool body, string expected)
    {
        var hash = Fingerprint(Ata(type, second, secretary, body));

        hash.Should().Be(expected, name);
    }
}
