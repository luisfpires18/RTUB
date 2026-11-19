using RTUB.Core.Entities;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    // (mentee, mentor) by nickname. Not by name: two members are both "Bruno Costa".
    private static readonly (string Mentee, string Mentor)[] MentorPairs =
    {
("Tampas", "Matchero"),

        ("Mealheiro", "Infra"),

        ("Nharro", "Txaio"),
        ("Meia Grama", "Txaio"),
        ("Buceta", "Txaio"),

        ("Slimmy", "Conchita"),
        ("Calimero", "Conchita"),
        ("Croquetes", "Conchita"),
        ("Bate Sacas", "Conchita"),
        ("Atchim", "Conchita"),
        ("Frango", "Conchita"),
        ("El Chapo", "Conchita"),

        ("Arbusto", "Bate Sacas"),
        ("Calhau", "Bate Sacas"),

        ("Rolhas", "Zeca Diabo"),
        ("Ambrósio", "Zeca Diabo"),
        ("Nabo", "Zeca Diabo"),
        ("Pardal", "Zeca Diabo"),

        ("Prepúcio", "Frango"),

        ("Malelo", "Atchim"),

        ("Nininho", "Malelo"),

        ("Bombeiro", "Arbusto"),

        ("KimKana", "Ambrósio"),

        ("Pássaro Maluco", "Pilão"),
        ("Cigano", "Pilão"),

        ("Tainada", "Cigano"),

        ("Jeans", "Erbalife"),
        ("Badjoncas", "Erbalife"),
        ("Drift", "Erbalife"),
        ("TumTum", "Erbalife"),
        ("Zeca Diabo", "Erbalife"),
        ("Bronha", "Erbalife"),
        ("Rufus", "Erbalife"),
        ("Vinhas", "Erbalife"),

        ("Saca Rabos", "Vinhas"),

        ("Autoscópio", "Snoopy"),
        ("Borat", "Autoscópio"),

        ("Pilão", "Coma"),
        ("Mija", "Pilão"),

        ("Indigesto", "Piroco"),
        ("Jornal", "Piroco"),
        ("Krika", "Piroco"),

        ("Erbalife", "Foda Nice"),
        ("Flor", "Foda Nice"),
        ("Piroco", "Foda Nice"),

        ("Joy", "Flor"),
        ("dimitri", "Flor"),
        ("Frutis", "Flor"),

        ("Smeagol", "Frutis"),
        ("Matchero", "Frutis"),

        ("Cestinho", "Joy"),

        ("Delay", "Cestinho"),
        ("Conchita", "Cestinho"),

        ("Caldas", "Alentejano"),
        ("Foda Nice", "Mago De Quintanilha"),

        ("Lacas", "Bean"),

        ("Parabento", "Lacas"),
        ("Pirikito", "Lacas"),

        ("Txaio", "Matrix"),
        ("Mangas", "Matrix"),
        ("Chicaestrume", "Matrix"),
        ("Matrix", "Xantrao"),

        ("Esbarrafat", "Exkill Bill"),
        ("Snoopy", "Exkill Bill"),

        ("Castanholas", "Pinta a Calça"),
        ("Pinta a Calça", "Metralha"),
        ("Botox", "Metralha"),

        ("Barbatov", "Esbarrafat"),
    };

    private static void AssignMentors(IEnumerable<ApplicationUser> members)
    {
        var byNickname = members.ToDictionary(m => m.Nickname!);
        foreach (var (mentee, mentor) in MentorPairs)
        {
            byNickname[mentee].MentorId = byNickname[mentor].Id;
        }
    }
}
