// DocumentationApiTests: Cache tests.
using System.Net;
using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Integration.Tests.Api;

public partial class DocumentationApiTests
{
    // ---------- #226 F01: storage round trips ----------

    [Fact]
    public async Task Page_IsOneListingForTheWholeYear_ThenServedFromMemory()
    {
        Seed();
        var (member, _) = await SignInAsync();
        var before = Storage.AllListings;

        Folders(await AreaHttp.Json(member, "/api/documentation")).Should().Equal($"Atas AG {Year}", "Logistics/Arraial", "Logistics/Jantar", "Regulamentos", "Vazia");
        Storage.AllListings.Should().Be(before + 1,
            "the year, the Logistics boards and every folder's documents come from one listing (was 2 in a row + 1 per folder)");

        await AreaHttp.Json(member, "/api/documentation");
        var (owner, _) = await SignInAsync("Owner");
        Folders(await AreaHttp.Json(owner, "/api/documentation")).Should().Contain($"Atas CV {Year}", "the shared listing is storage metadata, filtered per caller");
        Storage.AllListings.Should().Be(before + 1, "the next requests, by anyone, reuse it");
        (await member.GetStringAsync("/api/documentation")).Should().NotContain("Ata CV", "another caller's request never widens what a member sees");
    }

    [Fact]
    public async Task Download_ListsNothingWhenThePageWasJustLoaded_AndAtMostOnceCold()
    {
        Seed();
        var (member, _) = await SignInAsync();
        var cold = Storage.AllListings;
        (await AreaHttp.Json(member, "/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).GetProperty("url").GetString()
            .Should().EndWith("Regulamentos/Estatutos.pdf?download=True");
        Storage.AllListings.Should().Be(cold + 1, "a cold download is one listing (was three in a row)");

        var warm = Storage.AllListings;
        await AreaHttp.Json(member, "/api/documentation/file?folder=Logistics%2FJantar&name=orcamento.xlsx");
        Storage.AllListings.Should().Be(warm, "after the page (or another download) nothing is listed before signing");

        (await member.GetAsync($"/api/documentation/file?folder={Uri.EscapeDataString($"Atas CV {Year}")}&name={Uri.EscapeDataString("Ata CV 1.pdf")}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "a shared listing does not open a folder the member cannot see");
        (await member.GetAsync("/api/documentation/file?folder=Regulamentos&name=..%2F..%2Fx.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EveryWrite_DropsTheYearListing_AndOtherYearsKeepTheirOwn()
    {
        Seed();
        var previous = FiscalYearHelper.GetCurrentFiscalYearStartYear() - 1;
        var previousYear = $"{previous}-{previous + 1}";
        await AddFiscalYearAsync(previous);
        Storage.Put(Key("Antigos", "velho.pdf", previousYear), 1);
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);

        await AreaHttp.Json(owner, "/api/documentation");
        Folders(await AreaHttp.Json(owner, $"/api/documentation?fiscalYear={previousYear}")).Should().Equal(new[] { "Antigos" }, "each year has its own listing");
        var warm = Storage.AllListings;

        (await owner.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Novo.pdf"))).StatusCode.Should().Be(HttpStatusCode.OK);
        Documents(await AreaHttp.Json(owner, "/api/documentation"), "Regulamentos").Should().Contain("Novo.pdf", "the upload dropped the listing");
        Folders(await AreaHttp.Json(owner, $"/api/documentation?fiscalYear={previousYear}")).Should().Equal(new[] { "Antigos" });
        Storage.AllListings.Should().Be(warm + 1, "only the written year is listed again");

        (await owner.DeleteAsync("/api/documentation/documents?folder=Regulamentos&name=Novo.pdf")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Documents(await AreaHttp.Json(owner, "/api/documentation"), "Regulamentos").Should().NotContain("Novo.pdf");

        (await Json(await owner.PostAsJsonAsync("/api/documentation/folders", new { name = "Nova Pasta" }))).GetProperty("name").GetString().Should().Be("Nova Pasta");
        Folders(await AreaHttp.Json(owner, "/api/documentation")).Should().Contain("Nova Pasta");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/documentation/folders", new { name = "nova pasta" })))["name"].Single()
            .Should().Be("Uma pasta com este nome já existe.");

        (await owner.DeleteAsync("/api/documentation/folders?folder=Nova%20Pasta")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Folders(await AreaHttp.Json(owner, "/api/documentation")).Should().NotContain("Nova Pasta");
    }

    [Fact]
    public async Task StorageFailure_ShowsNoFolders_AndIsNotRemembered()
    {
        Seed();
        var (member, _) = await SignInAsync();
        Storage.Broken = true;
        (await AreaHttp.Json(member, "/api/documentation")).GetProperty("folders").GetArrayLength()
            .Should().Be(0, "as before: a storage error lists nothing and the page still opens");
        (await member.GetAsync("/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        Storage.Broken = false;
        Folders(await AreaHttp.Json(member, "/api/documentation")).Should().Contain("Regulamentos", "a failed listing is never kept");
    }
}
