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
public class MeetingAccessTests
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
    [InlineData("Magister", false)]
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
    public void CanCreateMeetings_OnlyOwnerAndTheTwoPresidents(string who, bool expected)
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
    [InlineData("Magister", "")]
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
    [InlineData("Mesa e CV", "AGO AGE")]
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
    [InlineData("Magister", false, false, false, false)]
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
    [InlineData("Mesa e CV", true, true, false, false)]
    public void CanManage_ByMeetingType(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        ByType(type => access.CanManage(Meeting(type))).Should().Equal(new[] { ago, age, cv, direcao },
            "{0} manages AGO, AGE, CV, Direção like this (the Mesa wins over the CV)", who);
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
    [InlineData("Admin Tuno", false, false, false, true)]
    [InlineData("Owner", true, true, true, true)]
    [InlineData("Magister", false, false, false, true)]
    [InlineData("Vice-Magister", false, false, false, true)]
    [InlineData("Secretário", false, false, false, true)]
    [InlineData("1.º Tesoureiro", false, false, false, true)]
    [InlineData("2.º Tesoureiro", false, false, false, true)]
    [InlineData("Presidente da Mesa", true, true, false, false)]
    [InlineData("1.º Secretário da Mesa", false, false, false, false)]
    [InlineData("2.º Secretário da Mesa", false, false, false, false)]
    [InlineData("Presidente do CV", false, false, true, false)]
    [InlineData("Presidente do CV Veterano", false, false, true, false)]
    [InlineData("Presidente do CF", false, false, false, false)]
    [InlineData("Mesa e CV", true, true, false, false)]
    public void CanCreateType_IsTheOldRule_EvenWhereTheFormNeverOpens(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        ByType(access.CanCreateType).Should().Equal(new[] { ago, age, cv, direcao }, who);
    }

    // ---------- proposals and the requests section (A4) ----------

    [Theory]
    [InlineData("Caloiro", false, false, false)]
    [InlineData("Tuno", false, false, false)]
    [InlineData("Veterano", true, false, false)]
    [InlineData("Tunossauro", true, false, false)]
    [InlineData("Veterano só de categoria", false, false, false)]
    [InlineData("Tuno Honorário", false, false, false)]
    [InlineData("Fundador", false, false, false)]
    [InlineData("Membro", false, false, false)]
    [InlineData("Admin", false, false, false)]
    [InlineData("Admin Tuno", false, true, false)]
    [InlineData("Owner", false, true, true)]
    [InlineData("Magister", true, true, true)]
    [InlineData("Vice-Magister", false, true, false)]
    [InlineData("Secretário", false, true, false)]
    [InlineData("1.º Tesoureiro", false, true, false)]
    [InlineData("2.º Tesoureiro", false, true, false)]
    [InlineData("Presidente da Mesa", false, false, false)]
    [InlineData("1.º Secretário da Mesa", false, false, false)]
    [InlineData("2.º Secretário da Mesa", false, false, false)]
    [InlineData("Presidente do CV", false, false, true)]
    [InlineData("Presidente do CV Veterano", false, false, true)]
    [InlineData("Presidente do CF", false, false, true)]
    [InlineData("Mesa e CV", false, false, true)]
    public void Proposals_CvDirecaoAndAssembly(string who, bool cv, bool direcao, bool assembly)
    {
        var access = Persona(who);

        access.CanProposeCv.Should().Be(cv, "{0}: Propor Reunião de CV", who);
        access.CanProposeDirecao.Should().Be(direcao, "{0}: Propor Reunião Direção", who);
        access.CanProposeAg.Should().Be(assembly, "{0}: Propor Assembleia Geral", who);
    }

    [Theory]
    [InlineData("Caloiro", false)]
    [InlineData("Tuno", false)]
    [InlineData("Veterano", true)]
    [InlineData("Tunossauro", true)]
    [InlineData("Veterano só de categoria", false)]
    [InlineData("Tuno Honorário", false)]
    [InlineData("Fundador", false)]
    [InlineData("Membro", false)]
    [InlineData("Admin", false)]
    [InlineData("Admin Tuno", false)]
    [InlineData("Owner", false)]
    [InlineData("Magister", true)]
    [InlineData("Vice-Magister", false)]
    [InlineData("Secretário", false)]
    [InlineData("1.º Tesoureiro", false)]
    [InlineData("2.º Tesoureiro", false)]
    [InlineData("Presidente da Mesa", false)]
    [InlineData("1.º Secretário da Mesa", false)]
    [InlineData("2.º Secretário da Mesa", false)]
    [InlineData("Presidente do CV", false)]
    [InlineData("Presidente do CV Veterano", true)]
    [InlineData("Presidente do CF", false)]
    [InlineData("Mesa e CV", false)]
    public void CanSeeRequests_VeteranoOrTunossauroByTime_OrMagister(string who, bool expected)
    {
        Persona(who).CanSeeRequests.Should().Be(expected, "{0} (A4: Owner is not exempt)", who);
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
    [InlineData("Magister", false, false, false, false)]
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
    public void CanDecide_ByRequestType(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        ByType(type => access.CanDecide(Request(type, "someone-else"))).Should().Equal(new[] { ago, age, cv, direcao },
            "{0} accepts / rejects AGO, AGE, CV, Direção requests like this", who);
    }

    [Fact]
    public void OwnsRequest_TheAuthor_OrOwnerForAny()
    {
        var author = Persona("Veterano");
        var other = Persona("Magister");
        var request = Request(MeetingType.ConselhoVeteranos, author.UserId);

        author.OwnsRequest(request).Should().BeTrue("the author deletes and reminds about their own request");
        other.OwnsRequest(request).Should().BeFalse();
        Persona("Admin").OwnsRequest(request).Should().BeFalse();
        Persona("Owner").OwnsRequest(request).Should().BeTrue("Owner may delete or remind any request");
    }

    // ---------- participations (A7) ----------

    [Theory]
    [InlineData("Caloiro", false)]
    [InlineData("Tuno", false)]
    [InlineData("Veterano", false)]
    [InlineData("Membro", false)]
    [InlineData("Admin", true)]
    [InlineData("Admin Tuno", true)]
    [InlineData("Owner", true)]
    [InlineData("Magister", false)]
    [InlineData("Presidente da Mesa", false)]
    [InlineData("Presidente do CV", false)]
    public void CanAddParticipants_AdminOrOwner(string who, bool expected)
    {
        Persona(who).CanAddParticipants.Should().Be(expected, who);
    }

    [Fact]
    public void CanRemoveParticipation_YourOwn_OrAnyoneForOwner_NeverForAdmin()
    {
        var member = Persona("Caloiro");
        var admin = Persona("Admin");
        var owner = Persona("Owner");
        var mine = new MeetingParticipation { MeetingId = 1, UserId = member.UserId };

        member.CanRemoveParticipation(mine).Should().BeTrue();
        Persona("Tuno").CanRemoveParticipation(mine).Should().BeFalse("nobody removes someone else's answer");
        Persona("Presidente da Mesa").CanRemoveParticipation(mine).Should().BeFalse();
        admin.CanRemoveParticipation(mine).Should().BeFalse("Admin adds members but does not remove answers");
        admin.CanRemoveParticipation(new MeetingParticipation { MeetingId = 1, UserId = admin.UserId }).Should().BeTrue();
        owner.CanRemoveParticipation(mine).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, "Tuno")]
    [InlineData(true, "Veterano")]
    [InlineData(true, "Tunossauro")]
    [InlineData(true, "TunoHonorario")]
    [InlineData(true, "Fundador")]
    [InlineData(false, "Leitao")]
    [InlineData(false, "Caloiro")]
    [InlineData(false, "Tuno,Leitao")]
    [InlineData(false, "Tuno,Caloiro")]
    public void CanBeAddedToMeeting_AnyoneButLeitoesAndCaloiros(bool expected, string categories)
    {
        var parsed = categories.Split(',').Select(c => Enum.Parse<MemberCategory>(c)).ToList();
        MeetingAccess.CanBeAddedToMeeting(new ApplicationUser { Categories = parsed }).Should().Be(expected, categories);
    }

    [Fact]
    public void CanBeAddedToMeeting_NeedsCategories()
    {
        MeetingAccess.CanBeAddedToMeeting(new ApplicationUser { Categories = null! }).Should().BeFalse();
    }

    // ---------- atas (A1, A2, A5) ----------

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Admin", false)]
    [InlineData("Admin Tuno", false)]
    [InlineData("Magister", false)]
    [InlineData("Presidente da Mesa", false)]
    [InlineData("Presidente do CV", false)]
    [InlineData("Veterano", false)]
    public void AtaRoles_AreOwnerOnly_NeverAdmin(string who, bool owner)
    {
        var roles = Persona(who).AtaRoles;

        roles.Should().NotContain("Admin", "A2: Admin alone never writes an ata");
        roles.Should().Equal(owner ? new[] { "Owner" } : Array.Empty<string>(), who);
    }

    [Fact]
    public void AtaRoles_OfAnOwnerWhoIsAlsoAdmin_StillLeaveAdminOut()
    {
        new MeetingAccess(new ApplicationUser(), isOwner: true, isAdmin: true).AtaRoles.Should().Equal("Owner");
    }

    [Theory]
    [InlineData("Caloiro", true, true, false, true)]
    [InlineData("Tuno", true, true, false, true)]
    [InlineData("Veterano", true, true, true, true)]
    [InlineData("Tunossauro", true, true, true, true)]
    [InlineData("Veterano só de categoria", true, true, false, true)]
    [InlineData("Tuno Honorário", true, true, false, true)]
    [InlineData("Fundador", true, true, false, true)]
    [InlineData("Membro", true, true, false, true)]
    [InlineData("Admin", true, true, false, true)]
    [InlineData("Admin Tuno", true, true, false, true)]
    [InlineData("Owner", true, true, true, true)]
    [InlineData("Magister", true, true, true, true)]
    [InlineData("Vice-Magister", true, true, false, true)]
    [InlineData("Secretário", true, true, false, true)]
    [InlineData("1.º Tesoureiro", true, true, false, true)]
    [InlineData("2.º Tesoureiro", true, true, false, true)]
    [InlineData("Presidente da Mesa", true, true, false, true)]
    [InlineData("1.º Secretário da Mesa", true, true, false, true)]
    [InlineData("2.º Secretário da Mesa", true, true, false, true)]
    [InlineData("Presidente do CV", true, true, false, true)]
    [InlineData("Presidente do CV Veterano", true, true, true, true)]
    [InlineData("Presidente do CF", true, true, false, true)]
    [InlineData("Mesa e CV", true, true, false, true)]
    public void CanReadPublishedAta_ByMeetingType(string who, bool ago, bool age, bool cv, bool direcao)
    {
        var access = Persona(who);

        // On top of seeing the meeting: a Direção ata is for whoever sees that meeting (Direção positions, Admin Tuno).
        ByType(type => access.CanReadPublishedAta(Meeting(type))).Should().Equal(new[] { ago, age, cv, direcao },
            "{0} reads the published atas of AGO, AGE, CV, Direção like this", who);
    }

    [Fact]
    public void CanReadPublishedAta_ATunoReadsTheCvTheyRepresent_AndNoOther()
    {
        var tuno = Persona("Tuno");
        var represented = Meeting(MeetingType.ConselhoVeteranos, representativeId: tuno.UserId);
        var other = Meeting(MeetingType.ConselhoVeteranos, representativeId: Persona("Tuno").UserId);

        tuno.CanReadPublishedAta(represented).Should().BeTrue("A5: the Tuno representative reads their CV's ata");
        tuno.CanReadPublishedAta(other).Should().BeFalse();
        Persona("Caloiro").CanReadPublishedAta(represented).Should().BeFalse();
    }

    [Fact]
    public void CanReadPublishedAta_ALeitaoReadsNoAssembly()
    {
        Persona("Leitão").CanReadPublishedAta(Meeting(MeetingType.AssembleiaGeralOrdinaria)).Should().BeFalse();
        Persona("Leitão").CanReadPublishedAta(Meeting(MeetingType.AssembleiaGeralExtraordinaria)).Should().BeFalse();
    }

    // ---------- MeetingAtaService.CanCreateOrEditAta, fed with MeetingAccess.AtaRoles ----------

    [Fact]
    public void Admin_AloneNeverWritesAnAta_A2()
    {
        foreach (var who in new[] { "Admin", "Admin Tuno" })
        {
            var admin = Persona(who);
            foreach (var type in AllTypes)
            {
                CanWriteAta(admin, Meeting(type)).Should().BeFalse("{0} does not write a {1} ata (A2)", who, type);
            }
        }

        var ownerAdmin = new MeetingAccess(new ApplicationUser(), isOwner: true, isAdmin: true);
        CanWriteAta(ownerAdmin, Meeting(MeetingType.ConselhoVeteranos)).Should().BeTrue("an Owner writes because of Owner, not Admin");
    }

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

    private static MeetingRequest Request(MeetingType type, string authorId) => new()
    {
        Id = 1,
        RequestedMeetingType = type,
        Title = "Pedido",
        ProposedDateTime = DateTime.Today.AddDays(10),
        Description = "Assuntos",
        AuthorUserId = authorId,
    };

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
