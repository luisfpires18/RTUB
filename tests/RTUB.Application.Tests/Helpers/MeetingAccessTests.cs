using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Task 034: who may do what on /meetings (<see cref="MeetingAccess"/>), persona by persona, against the matrix in
/// docs/react-meetings.md. Pure: no database, no host. "Veterano" / "Tunossauro" / "Tuno" are by time (YearTuno /
/// MonthTuno), never by category; a Leitão is refused before any of these rules is read (<see cref="MeetingAccess.IsBlocked"/>).
/// Each table's columns are the meeting (or request) types: AGO, AGE, CV, Direção.
/// </summary>
public partial class MeetingAccessTests
{
    private static readonly MeetingAtaService Atas =
        new(Mock.Of<IMeetingAtaRepository>(), Mock.Of<IDbContextFactory<ApplicationDbContext>>());

    // ---------- the personas ----------

    [Fact]
    public void Personas_AreWhoTheySayTheyAre()
    {
        Persona("Tuno").User.CurrentRole.Should().Be("TUNO");
        Persona("Veterano").User.CurrentRole.Should().Be("VETERANO");
        Persona("Tunossauro").User.CurrentRole.Should().Be("TUNOSSAURO");
        Persona("Presidente do CV Veterano").User.CurrentRole.Should().Be("VETERANO");
        Persona("Veterano só de categoria").User.CurrentRole.Should().Be("N/A", "the category alone gives no years as Tuno");
        Persona("Caloiro").User.CurrentRole.Should().Be("N/A");
    }

    [Theory]
    [InlineData("Leitão", true)]
    [InlineData("Leitão Owner Admin", true)]
    [InlineData("Caloiro", false)]
    [InlineData("Tuno", false)]
    [InlineData("Veterano", false)]
    [InlineData("Tunossauro", false)]
    [InlineData("Veterano só de categoria", false)]
    [InlineData("Tuno Honorário", false)]
    [InlineData("Fundador", false)]
    [InlineData("Membro", false)]
    [InlineData("Admin", false)]
    [InlineData("Admin Tuno", false)]
    [InlineData("Owner", false)]
    [InlineData("Magister", false)]
    [InlineData("Presidente da Mesa", false)]
    [InlineData("Presidente do CV", false)]
    public void IsBlocked_OnlyALeitao_WhateverTheirRole(string who, bool blocked)
    {
        Persona(who).IsBlocked.Should().Be(blocked, who);
    }

    // ---------- creating and managing meetings (A6) ----------

    [Theory]
    [InlineData("Caloiro", false)]
    [InlineData("Tuno", false)]
    [InlineData("Veterano", false)]
    [InlineData("Tunossauro", false)]
    [InlineData("Veterano só de categoria", false)]
    [InlineData("Tuno Honorário", false)]
    [InlineData("Fundador", false)]
    [InlineData("Membro", false)]
    [InlineData("Admin", false)]
    [InlineData("Admin Tuno", false)]
    [InlineData("Owner", true)]
    [InlineData("Magister", true)]
    [InlineData("Vice-Magister", false)]
    [InlineData("Secretário", false)]
    [InlineData("1.º Tesoureiro", false)]
    [InlineData("2.º Tesoureiro", false)]
    [InlineData("Presidente da Mesa", true)]
    [InlineData("1.º Secretário da Mesa", false)]
    [InlineData("2.º Secretário da Mesa", false)]
    [InlineData("Presidente do CV", true)]
    [InlineData("Presidente do CV Veterano", true)]
    [InlineData("Presidente do CF", false)]
    [InlineData("Mesa e CV", true)]
    public void CanCreateMeetings_OwnerAndTheThreePresidents(string who, bool expected)
    {
        Persona(who).CanCreateMeetings.Should().Be(expected, who);
    }

    [Theory]
    [InlineData("Caloiro", "")]
    [InlineData("Tuno", "")]
    [InlineData("Veterano", "")]
    [InlineData("Tunossauro", "")]
    [InlineData("Veterano só de categoria", "")]
    [InlineData("Tuno Honorário", "")]
    [InlineData("Fundador", "")]
    [InlineData("Membro", "")]
    [InlineData("Admin", "")]
    [InlineData("Admin Tuno", "")]
    [InlineData("Owner", "AGO AGE CV DIR")]
    [InlineData("Magister", "DIR")]
    [InlineData("Vice-Magister", "")]
    [InlineData("Secretário", "")]
    [InlineData("1.º Tesoureiro", "")]
    [InlineData("2.º Tesoureiro", "")]
    [InlineData("Presidente da Mesa", "AGO AGE")]
    [InlineData("1.º Secretário da Mesa", "")]
    [InlineData("2.º Secretário da Mesa", "")]
    [InlineData("Presidente do CV", "CV")]
    [InlineData("Presidente do CV Veterano", "CV")]
    [InlineData("Presidente do CF", "")]
    [InlineData("Mesa e CV", "AGO AGE CV")]
    public void FormTypes_AreTheCreatableTypes_InTheFormsOrder(string who, string expected)
    {
        Persona(who).FormTypes.Should().Equal(Types(expected), "{0} gets exactly these types, in the form's order", who);
    }

    [Theory]
    [InlineData("Caloiro", false, false, false, false)]
    [InlineData("Tuno", false, false, false, false)]
    [InlineData("Veterano", false, false, false, false)]
    [InlineData("Tunossauro", false, false, false, false)]
    [InlineData("Veterano só de categoria", false, false, false, false)]
    [InlineData("Tuno Honorário", false, false, false, false)]
    [InlineData("Fundador", false, false, false, false)]
    [InlineData("Membro", false, false, false, false)]
    [InlineData("Admin", false, false, false, false)]
    [InlineData("Admin Tuno", false, false, false, false)]
    [InlineData("Owner", true, true, true, true)]
    [InlineData("Magister", false, false, false, true)]
    [InlineData("Vice-Magister", false, false, false, false)]
    [InlineData("Secretário", false, false, false, false)]
    [InlineData("1.º Tesoureiro", false, false, false, false)]
    [InlineData("2.º Tesoureiro", false, false, false, false)]
    [InlineData("Presidente da Mesa", true, true, false, false)]
    [InlineData("1.º Secretário da Mesa", false, false, false, false)]
    [InlineData("2.º Secretário da Mesa", false, false, false, false)]
    [InlineData("Presidente do CV", false, false, true, false)]
    [InlineData("Presidente do CV Veterano", false, false, true, false)]
    [InlineData("Presidente do CF", false, false, false, false)]
    [InlineData("Mesa e CV", true, true, true, false)]
    public void CanManage_ByMeetingType(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        ByType(type => access.CanManage(Meeting(type))).Should().Equal(new[] { ago, age, cv, direcao },
            "{0} manages AGO, AGE, CV, Direção like this (075: each body's president, the union for several)", who);
    }

    [Theory]
    [InlineData("Veterano", true, true, false, false, false, false)]
    [InlineData("Magister", true, true, true, false, true, false)]
    [InlineData("Presidente do CV", true, true, false, true, false, false)]
    [InlineData("Presidente da Mesa", false, true, false, false, false, true)]
    [InlineData("Admin Tuno", false, true, true, false, false, false)]
    [InlineData("Caloiro", false, true, false, false, false, false)]
    [InlineData("Owner", false, true, false, true, true, true)]
    [InlineData("Leitão", false, false, false, false, false, false)]
    public void PollAccess_SeparatesBodyVotersFromManagers(
        string who,
        bool cvVoter,
        bool assemblyVoter,
        bool directionVoter,
        bool cvManager,
        bool directionManager,
        bool assemblyManager)
    {
        var access = Persona(who);

        access.CanVoteInPoll(MeetingPollAudience.ConselhoVeteranos).Should().Be(cvVoter, $"{who} CV voter");
        access.CanVoteInPoll(MeetingPollAudience.AssembleiaGeral).Should().Be(assemblyVoter, $"{who} AG voter");
        access.CanVoteInPoll(MeetingPollAudience.Direcao).Should().Be(directionVoter, $"{who} Direção voter");
        access.CanManagePoll(MeetingPollAudience.ConselhoVeteranos).Should().Be(cvManager, $"{who} CV manager");
        access.CanManagePoll(MeetingPollAudience.Direcao).Should().Be(directionManager, $"{who} Direção manager");
        access.CanManagePoll(MeetingPollAudience.AssembleiaGeral).Should().Be(assemblyManager, $"{who} AG manager");
    }

    [Theory]
    [InlineData("Caloiro", false, false, false, false)]
    [InlineData("Tuno", false, false, false, false)]
    [InlineData("Veterano", false, false, false, false)]
    [InlineData("Tunossauro", false, false, false, false)]
    [InlineData("Veterano só de categoria", false, false, false, false)]
    [InlineData("Tuno Honorário", false, false, false, false)]
    [InlineData("Fundador", false, false, false, false)]
    [InlineData("Membro", false, false, false, false)]
    [InlineData("Admin", false, false, false, false)]
    [InlineData("Admin Tuno", false, false, false, false)]
    [InlineData("Owner", true, true, true, true)]
    [InlineData("Magister", false, false, false, true)]
    [InlineData("Vice-Magister", false, false, false, false)]
    [InlineData("Secretário", false, false, false, false)]
    [InlineData("1.º Tesoureiro", false, false, false, false)]
    [InlineData("2.º Tesoureiro", false, false, false, false)]
    [InlineData("Presidente da Mesa", true, true, false, false)]
    [InlineData("1.º Secretário da Mesa", false, false, false, false)]
    [InlineData("2.º Secretário da Mesa", false, false, false, false)]
    [InlineData("Presidente do CV", false, false, true, false)]
    [InlineData("Presidente do CV Veterano", false, false, true, false)]
    [InlineData("Presidente do CF", false, false, false, false)]
    [InlineData("Mesa e CV", true, true, true, false)]
    public void CanCreateType_IsEachBodysPresident_AndOwner(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        ByType(access.CanCreateType).Should().Equal(new[] { ago, age, cv, direcao }, who);
    }

    // ---------- helpers ----------

    private static readonly MeetingType[] AllTypes =
    {
        MeetingType.AssembleiaGeralOrdinaria, MeetingType.AssembleiaGeralExtraordinaria, MeetingType.ConselhoVeteranos, MeetingType.ReuniaoDirecao,
    };

    /// <summary>One persona per row of the matrix; ids are fresh GUIDs (IdentityUser's default).</summary>
    private static MeetingAccess Persona(string who) => who switch
    {
        "Leitão" => Access(Categories(MemberCategory.Leitao)),
        "Leitão Owner Admin" => Access(Categories(MemberCategory.Leitao), owner: true, admin: true),
        "Caloiro" => Access(Categories(MemberCategory.Caloiro)),
        "Tuno" => Access(TunoFor(1, MemberCategory.Tuno)),
        "Veterano" => Access(TunoFor(3, MemberCategory.Tuno, MemberCategory.Veterano)),
        "Tunossauro" => Access(TunoFor(7, MemberCategory.Tuno, MemberCategory.Tunossauro)),
        "Veterano só de categoria" => Access(Categories(MemberCategory.Veterano)),
        "Tuno Honorário" => Access(Categories(MemberCategory.TunoHonorario)),
        "Fundador" => Access(Categories(MemberCategory.Fundador)),
        "Membro" => Access(new ApplicationUser()),
        "Admin" => Access(new ApplicationUser(), admin: true),
        "Admin Tuno" => Access(Categories(MemberCategory.Tuno), admin: true),
        "Owner" => Access(new ApplicationUser(), owner: true),
        "Magister" => Access(Holding(Position.Magister)),
        "Vice-Magister" => Access(Holding(Position.ViceMagister)),
        "Secretário" => Access(Holding(Position.Secretario)),
        "1.º Tesoureiro" => Access(Holding(Position.PrimeiroTesoureiro)),
        "2.º Tesoureiro" => Access(Holding(Position.SegundoTesoureiro)),
        "Presidente da Mesa" => Access(Holding(Position.PresidenteMesaAssembleia)),
        "1.º Secretário da Mesa" => Access(Holding(Position.PrimeiroSecretarioMesaAssembleia)),
        "2.º Secretário da Mesa" => Access(Holding(Position.SegundoSecretarioMesaAssembleia)),
        "Presidente do CV" => Access(Holding(Position.PresidenteConselhoVeteranos)),
        "Presidente do CV Veterano" => Access(Holding(TunoFor(3, MemberCategory.Tuno, MemberCategory.Veterano), Position.PresidenteConselhoVeteranos)),
        "Presidente do CF" => Access(Holding(Position.PresidenteConselhoFiscal)),
        "Mesa e CV" => Access(Holding(Position.PresidenteMesaAssembleia, Position.PresidenteConselhoVeteranos)),
        _ => throw new ArgumentOutOfRangeException(nameof(who), who, "Unknown persona"),
    };

    private static MeetingAccess Access(ApplicationUser user, bool owner = false, bool admin = false) => new(user, owner, admin);

    private static ApplicationUser Categories(params MemberCategory[] categories) => new() { Categories = categories.ToList() };

    /// <summary>A member who became Tuno <paramref name="years"/> years ago this month (CurrentRole is computed from it).</summary>
    private static ApplicationUser TunoFor(int years, params MemberCategory[] categories)
    {
        var now = DateTime.Now;
        return new ApplicationUser { Categories = categories.ToList(), YearTuno = now.Year - years, MonthTuno = now.Month };
    }

    private static ApplicationUser Holding(params Position[] positions) => Holding(new ApplicationUser(), positions);

    private static ApplicationUser Holding(ApplicationUser user, params Position[] positions)
    {
        user.Positions = positions.ToList();
        return user;
    }

    private static Meeting Meeting(MeetingType type, string? representativeId = null) => new()
    {
        Id = 1,
        Type = type,
        Title = "Reunião",
        Date = DateTime.Today.AddDays(-2),
        Statement = "Ordem de trabalhos",
        TunoRepresentativeUserId = representativeId,
    };

    /// <summary>What the API passes to the old rule: the session's id, "Owner" or nothing (A2), and the member's positions.</summary>
    private static bool CanWriteAta(MeetingAccess access, Meeting meeting, MeetingAta? existing = null) =>
        Atas.CanCreateOrEditAta(access.UserId, meeting, access.AtaRoles, access.User.Positions, existing);

    private static bool[] ByType(Func<MeetingType, bool> rule) => AllTypes.Select(rule).ToArray();

    private static IEnumerable<MeetingType> Types(string codes) =>
        codes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(code => code switch
        {
            "AGO" => MeetingType.AssembleiaGeralOrdinaria,
            "AGE" => MeetingType.AssembleiaGeralExtraordinaria,
            "CV" => MeetingType.ConselhoVeteranos,
            "DIR" => MeetingType.ReuniaoDirecao,
            _ => throw new ArgumentOutOfRangeException(nameof(codes), code, "Unknown meeting type code"),
        }).ToList();
}
