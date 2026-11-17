using FluentAssertions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

// MeetingAccessTests: Requests Participants And Atas tests.
public partial class MeetingAccessTests
{
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

    private static MeetingRequest Request(MeetingType type, string authorId) => new()
    {
        Id = 1,
        RequestedMeetingType = type,
        Title = "Pedido",
        ProposedDateTime = DateTime.Today.AddDays(10),
        Description = "Assuntos",
        AuthorUserId = authorId,
    };
}
