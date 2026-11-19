// DocumentationApiTests: Folders tests.
using System.Net;
using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class DocumentationApiTests
{
    [Fact]
    public async Task Folders_FollowTheOldVisibility_AndOrder_WithoutLeakingStorageKeys()
    {
        Seed();
        var (member, _) = await SignInAsync();
        var docs = await AreaHttp.Json(member, "/api/documentation");
        docs.GetProperty("fiscalYear").GetString().Should().Be(Year, "the old page opened on the current year");
        docs.GetProperty("canManage").GetBoolean().Should().BeFalse();
        docs.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("fiscalYears", "fiscalYear", "folders", "extensions", "maxFileBytes", "canManage");
        Folders(docs).Should().Equal($"Atas AG {Year}", "Logistics/Arraial", "Logistics/Jantar", "Regulamentos", "Vazia");
        Labels(docs).Should().Equal($"Atas AG {Year}", "Arraial", "Jantar", "Regulamentos", "Vazia");

        var regulamentos = docs.GetProperty("folders").EnumerateArray().Single(f => f.GetProperty("name").GetString() == "Regulamentos");
        regulamentos.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "label", "documents");
        var files = regulamentos.GetProperty("documents").EnumerateArray().ToList();
        files.Select(f => f.GetProperty("name").GetString()).Should().Equal("Anexo.docx", "Estatutos.pdf");
        files[1].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "extension", "sizeBytes");
        (files[1].GetProperty("extension").GetString(), files[1].GetProperty("sizeBytes").GetInt64()).Should().Be((".pdf", 1234));
        (await member.GetStringAsync("/api/documentation")).Should().NotContain("docs/").And.NotContain("Ata CV");

        var (veterano, vet) = await SignInAsync();
        await UpdateAsync(vet.Id, u => { u.YearTuno = DateTime.Now.Year - 3; u.MonthTuno = 1; });
        Folders(await AreaHttp.Json(veterano, "/api/documentation")).Should().Contain($"Atas CV {Year}");

        var (magister, mag) = await SignInAsync();
        await UpdateAsync(mag.Id, u => u.Positions = new List<Position> { Position.Magister });
        Folders(await AreaHttp.Json(magister, "/api/documentation")).Should().Contain($"Atas CV {Year}");

        foreach (var role in new[] { "Mod", "Admin" })
        {
            var (client, _) = await SignInAsync(role);
            var seen = await AreaHttp.Json(client, "/api/documentation");
            Folders(seen).Should().NotContain($"Atas CV {Year}", "{0} had no extra rights on the old page", role);
            seen.GetProperty("canManage").GetBoolean().Should().BeTrue(role);
        }

        var (owner, _) = await SignInAsync("Owner");
        var all = await AreaHttp.Json(owner, "/api/documentation");
        Folders(all).Should().Equal($"Atas AG {Year}", $"Atas CV {Year}", "Logistics/Arraial", "Logistics/Jantar", "Regulamentos", "Vazia");
        all.GetProperty("canManage").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FiscalYears_AreValidated_AndEachShowsItsOwnFolders()
    {
        Seed();
        var previous = FiscalYearHelper.GetCurrentFiscalYearStartYear() - 1;
        var previousYear = $"{previous}-{previous + 1}";
        await AddFiscalYearAsync(previous);
        Storage.Put(Key("Antigos", "velho.pdf", previousYear), 1);
        var (member, _) = await SignInAsync();

        var shown = await AreaHttp.Json(member, $"/api/documentation?fiscalYear={previousYear}");
        shown.GetProperty("fiscalYear").GetString().Should().Be(previousYear);
        Folders(shown).Should().Equal("Antigos");
        shown.GetProperty("fiscalYears").EnumerateArray().Select(y => y.GetProperty("value").GetString()).Should().Contain(previousYear);
        (await AreaHttp.Json(member, "/api/documentation?fiscalYear=")).GetProperty("fiscalYear").GetString().Should().Be(Year);

        foreach (var bad in new[] { "1999-2000", "../..", $"{Year}/Regulamentos" })
        {
            (await member.GetAsync($"/api/documentation?fiscalYear={Uri.EscapeDataString(bad)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        }
    }

    [Fact]
    public async Task Download_IsAPreSignedAttachment_OnlyForAListedDocumentTheMemberSees()
    {
        Seed();
        var (member, _) = await SignInAsync();

        var link = await AreaHttp.Json(member, "/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf");
        link.GetProperty("url").GetString().Should().Be($"https://r2.test/{Key("Regulamentos", "Estatutos.pdf")}?download=True");
        (await AreaHttp.Json(member, "/api/documentation/file?folder=Logistics%2FJantar&name=orcamento.xlsx")).GetProperty("url").GetString()
            .Should().Contain("Logistics/Jantar/orcamento.xlsx");

        foreach (var query in new[]
                 {
                     $"folder={Uri.EscapeDataString($"Atas CV {Year}")}&name={Uri.EscapeDataString("Ata CV 1.pdf")}",
                     "folder=Regulamentos&name=nao-existe.pdf",
                     "folder=Regulamentos%2F..&name=Estatutos.pdf",
                     $"folder=..%2F..%2F{Year}%2FRegulamentos&name=Estatutos.pdf",
                     "folder=Regulamentos&name=..%2FEstatutos.pdf",
                 })
        {
            (await member.GetAsync($"/api/documentation/file?{query}")).StatusCode.Should().Be(HttpStatusCode.NotFound, query);
        }

        var (owner, _) = await SignInAsync("Owner");
        (await owner.GetAsync($"/api/documentation/file?folder={Uri.EscapeDataString($"Atas CV {Year}")}&name={Uri.EscapeDataString("Ata CV 1.pdf")}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_IsOpenToMembersOfTheFolder_WithTheOldRules_AndOnlyOwnerReplaces()
    {
        Seed();
        var (member, _) = await SignInAsync();
        (await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Novo.pdf"))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "every write needs the antiforgery token");
        await AreaHttp.WithTokenAsync(member);

        var created = await Json(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Novo.pdf")));
        created.GetProperty("name").GetString().Should().Be("Novo.pdf");
        Storage.Objects[Key("Regulamentos", "Novo.pdf")].ContentType.Should().Be("application/pdf");

        (await Json(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", @"..\..\fora.pdf")))).GetProperty("name").GetString()
            .Should().Be("fora.pdf");
        Storage.Objects.Keys.Should().Contain(Key("Regulamentos", "fora.pdf")).And.OnlyContain(k => k.StartsWith("docs/Test/"));

        (await AreaHttp.Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "script.exe"))))["file"].Single()
            .Should().StartWith("Tipo de ficheiro não permitido");
        (await AreaHttp.Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "vazio.pdf", Array.Empty<byte>())))).Keys.Should().Equal("file");
        (await AreaHttp.Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Estatutos.pdf"))))["file"].Single()
            .Should().Be("Já existe um documento com este nome nesta pasta.");
        Storage.Objects[Key("Regulamentos", "Estatutos.pdf")].Size.Should().Be(1234, "a member never replaces a document");
        (await member.PostAsync("/api/documentation/documents", Upload($"Atas CV {Year}", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync("/api/documentation/documents", Upload("Nao Existe", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync("/api/documentation/documents", Upload("Logistics/Jantar", "fatura.pdf"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);
        (await owner.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Estatutos.pdf"))).StatusCode.Should().Be(HttpStatusCode.OK);
        Storage.Objects[Key("Regulamentos", "Estatutos.pdf")].Size.Should().Be(3, "Owner replaces by uploading the same name");
    }

    [Fact]
    public async Task Folders_AndDeletes_AreOwnerOnly_WithTheOldValidation()
    {
        Seed();
        foreach (var role in new[] { (string?)null })
        {
            var (client, _) = await SignInAsync(role);
            await AreaHttp.WithTokenAsync(client);
            (await client.PostAsJsonAsync("/api/documentation/folders", new { name = "Nova" })).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await client.DeleteAsync("/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await client.DeleteAsync("/api/documentation/folders?folder=Regulamentos")).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
        }

        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        (await admin.PostAsJsonAsync("/api/documentation/folders", new { name = "Feita Por Admin" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);
        foreach (var bad in new[] { "", "  ", "Atas/../x", "Ação", new string('a', 51), "regulamentos" })
        {
            (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/documentation/folders", new { name = bad }))).Keys.Should().Equal(new[] { "name" }, bad);
        }

        (await Json(await owner.PostAsJsonAsync("/api/documentation/folders", new { name = " Atas Direcao-2026 " }))).GetProperty("name").GetString()
            .Should().Be("Atas Direcao-2026");
        Storage.Objects.Keys.Should().Contain(Key("Atas Direcao-2026"));

        (await owner.DeleteAsync("/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Storage.Objects.Keys.Should().NotContain(Key("Regulamentos", "Estatutos.pdf")).And.Contain(Key("Regulamentos", "Anexo.docx"));
        (await owner.DeleteAsync("/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await owner.DeleteAsync("/api/documentation/folders?folder=Logistics%2FJantar")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Storage.Objects.Keys.Should().NotContain(k => k.Contains("Logistics/Jantar")).And.Contain(Key("Logistics/Arraial", "lista.csv"));
        (await owner.DeleteAsync("/api/documentation/folders?folder=Logistics")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "only the listed folders (boards, not the Logistics root) can be deleted");
        (await owner.DeleteAsync("/api/documentation/folders?folder=..")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
