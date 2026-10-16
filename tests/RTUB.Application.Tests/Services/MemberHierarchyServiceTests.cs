using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// #223: the padrinho → afilhado rules of /members/hierarchy, documented before the tree redesign. Effective members
/// only (Caloiro, Tuno, Veterano, Tunossauro; never Leitões); only Tunos and above are padrinhos; anyone without a valid
/// padrinho is a root; roots by first then last name. The only change is defensive: a self-padrinho or a padrinho loop
/// no longer makes members vanish from the tree.
/// </summary>
public class MemberHierarchyServiceTests
{
    private readonly MemberHierarchyService _service = new();

    private static ApplicationUser Member(string id, string first, string last = "Silva", string? mentor = null, params MemberCategory[] categories) =>
        new() { Id = id, UserName = id, FirstName = first, LastName = last, Nickname = id, MentorId = mentor, Categories = categories.ToList() };

    private static ApplicationUser Tuno(string id, string first, string? mentor = null, string last = "Silva") => Member(id, first, last, mentor, MemberCategory.Tuno);
    private static ApplicationUser Caloiro(string id, string first, string? mentor = null) => Member(id, first, "Silva", mentor, MemberCategory.Caloiro);

    private (List<string> Roots, Dictionary<string, List<string>> Children) Build(params ApplicationUser[] members)
    {
        var (roots, children) = _service.BuildHierarchy(members);
        return (roots.Select(r => r.Id).ToList(), children.ToDictionary(c => c.Key, c => c.Value.Select(a => a.Id).ToList()));
    }

    /// <summary>Every member reachable from the roots, each exactly once.</summary>
    private static List<string> Walk(List<string> roots, Dictionary<string, List<string>> children)
    {
        var seen = new List<string>();
        void Visit(string id)
        {
            seen.Add(id);
            foreach (var child in children.GetValueOrDefault(id) ?? [])
                Visit(child);
        }

        roots.ForEach(Visit);
        return seen;
    }

    [Fact]
    public void ATunoPadrinho_HasHisAfilhados_InNameOrder_AndTheyAreNotRoots()
    {
        var (roots, children) = Build(
            Tuno("p", "Pedro"),
            Caloiro("c2", "Zeca", mentor: "p"),
            Tuno("t1", "Ana", mentor: "p"),
            Member("v", "Vasco", "Silva", "p", MemberCategory.Veterano));

        roots.Should().Equal("p");
        children["p"].Should().Equal("t1", "v", "c2");
    }

    [Fact]
    public void VeteranosAndTunossauros_ArePadrinhosToo_DeeperLevelsNest()
    {
        var (roots, children) = Build(
            Member("vet", "Vera", "Silva", null, MemberCategory.Veterano),
            Member("sau", "Saulo", "Silva", "vet", MemberCategory.Tunossauro),
            Tuno("t", "Tó", mentor: "sau"),
            Caloiro("c", "Cris", mentor: "t"));

        roots.Should().Equal("vet");
        children["vet"].Should().Equal("sau");
        children["sau"].Should().Equal("t");
        children["t"].Should().Equal("c");
    }

    [Fact]
    public void ACaloiroIsNoPadrinho_TheirAfilhadoIsARoot()
    {
        var (roots, children) = Build(Caloiro("c", "Carla"), Caloiro("a", "Abel", mentor: "c"));

        roots.Should().Equal("a", "c");
        children.Should().BeEmpty();
    }

    [Fact]
    public void Leitoes_AreLeftOut_AndAMemberWhosePadrinhoIsALeitaoIsARoot()
    {
        var (roots, children) = Build(
            Member("l", "Luís", "Silva", null, MemberCategory.Leitao),
            Caloiro("c", "Carla", mentor: "l"),
            Member("l2", "Lara", "Silva", "c", MemberCategory.Leitao));

        roots.Should().Equal("c");
        Walk(roots, children).Should().NotContain(new[] { "l", "l2" });
    }

    [Fact]
    public void APadrinhoWhoIsNoLongerThere_LeavesTheAfilhadoAsARoot()
    {
        Build(Caloiro("c", "Carla", mentor: "deleted-account")).Roots.Should().Equal("c");
    }

    [Fact]
    public void ManyRoots_AreOrderedByFirstThenLastName_IncludingMembersWithNoLinks()
    {
        var (roots, _) = Build(
            Tuno("z", "Zé"), Tuno("b2", "Bia", last: "Rocha"), Tuno("b1", "Bia", last: "Pires"), Caloiro("a", "Ana"), Tuno("child", "Rui", mentor: "z"));

        roots.Should().Equal("a", "b1", "b2", "z");
    }

    [Fact]
    public void ASelfPadrinho_IsAPlainRoot_NeverItsOwnAfilhado()
    {
        var (roots, children) = Build(Tuno("t", "Tó", mentor: "t"), Caloiro("c", "Cris", mentor: "t"));

        roots.Should().Equal("t");
        children["t"].Should().Equal("c");
        Walk(roots, children).Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Fact]
    public void APadrinhoLoop_IsCutAtItsFirstMemberByName_SoNobodyVanishes()
    {
        // Bruno → Ana → Carlos → Bruno, all Tunos: before #223 the three disappeared from the tree.
        var (roots, children) = Build(
            Tuno("bruno", "Bruno", mentor: "carlos"),
            Tuno("ana", "Ana", mentor: "bruno"),
            Tuno("carlos", "Carlos", mentor: "ana"),
            Caloiro("dora", "Dora", mentor: "ana"),
            Tuno("eva", "Eva"));

        roots.Should().Equal("ana", "eva");
        children["ana"].Should().Equal("carlos", "dora");
        children["carlos"].Should().Equal("bruno");
        children["bruno"].Should().BeEmpty("Ana is the root now, not Bruno's afilhada");
        Walk(roots, children).Should().BeEquivalentTo(new[] { "ana", "bruno", "carlos", "dora", "eva" });
    }

    [Fact]
    public void TheStoredPadrinho_IsNeverChangedByTheGuard()
    {
        var a = Tuno("a", "Ana", mentor: "b");
        var b = Tuno("b", "Bia", mentor: "a");

        _service.BuildHierarchy([a, b]);

        (a.MentorId, b.MentorId).Should().Be(("b", "a"));
    }

    [Fact]
    public void EveryEffectiveMember_AppearsExactlyOnce()
    {
        var members = new List<ApplicationUser>
        {
            Tuno("r1", "Rita"), Tuno("r2", "Rui"), Caloiro("c1", "Carla", "r1"), Tuno("t1", "Tiago", "r1"), Caloiro("c2", "Celso", "t1"),
            Member("l", "Leo", "Silva", "r2", MemberCategory.Leitao), Tuno("x", "Xana", "y"), Tuno("y", "Yuri", "x"), Caloiro("s", "Sara", "s"),
            Member("h", "Hugo", "Silva", "r2", MemberCategory.Tuno, MemberCategory.TunoHonorario),
        };

        var (roots, children) = Build(members.ToArray());

        Walk(roots, children).Should().OnlyHaveUniqueItems().And.BeEquivalentTo(members.Where(m => m.Id != "l").Select(m => m.Id));
    }
}
