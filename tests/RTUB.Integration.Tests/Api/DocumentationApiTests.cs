using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The documentation on /api/documentation (React track 022, was the Blazor /documentation) through the real host: real
/// login, antiforgery and SQLite. Document storage is <see cref="FakeDocumentStorage"/>, an in-memory bucket: nothing
/// reaches R2. Documentation sends no notifications. The storage is emptied before every test.
/// </summary>
public class DocumentationApiTests : IClassFixture<DocumentationApiFactory>
{
    private static readonly string Year = FiscalYearHelper.GetCurrentFiscalYearString();
    private readonly DocumentationApiFactory _factory;

    public DocumentationApiTests(DocumentationApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Objects.Clear();
    }

    private FakeDocumentStorage Storage => _factory.Storage;

    private static string Key(string folder, string? name = null, string year = "") =>
        $"docs/Test/{(year == "" ? Year : year)}/{folder}/{name}";

    private void Seed()
    {
        Storage.Put(Key("Regulamentos", "Estatutos.pdf"), 1234);
        Storage.Put(Key("Regulamentos", "Anexo.docx"), 2048);
        Storage.Put(Key($"Atas CV {Year}", "Ata CV 1.pdf"), 10);
        Storage.Put(Key($"Atas AG {Year}", "Ata AG 1.pdf"), 10);
        Storage.Put(Key("Logistics/Jantar", "orcamento.xlsx"), 99);
        Storage.Put(Key("Logistics/Arraial", "lista.csv"), 5);
        Storage.Put(Key("Vazia"), 0);
    }

    [Fact]
    public async Task Visitors_GetNothing_AndThePageIsReact()
    {
        Seed();
        var before = Storage.Objects.Keys.OrderBy(k => k).ToList();
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        (await anonymous.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/documentation/folders", new { name = "Nova" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/documentation/documents", Upload("Regulamentos", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/documentation");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fdocumentation");

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/documentation")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Media.Documentation").Should().BeNull("the Blazor documentation page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/documentation", "/shop", "/inventory", "/leaderboard", "/members/manage", "/member/events",
                "/member/gallery", "/member/roles", "/hierarchy" });
        Storage.Objects.Keys.OrderBy(k => k).Should().Equal(before, "nothing was written");
    }

    [Fact]
    public async Task Leitoes_AreRefused_UnlessOwner()
    {
        Seed();
        var (leitao, me) = await SignInAsync();
        await UpdateAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await WithTokenAsync(leitao);

        (await leitao.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.GetAsync("/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.PostAsync("/api/documentation/documents", Upload("Regulamentos", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (owner, boss) = await SignInAsync("Owner");
        await UpdateAsync(boss.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        (await owner.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.OK, "Owner was never sent away");
    }

    [Fact]
    public async Task Folders_FollowTheOldVisibility_AndOrder_WithoutLeakingStorageKeys()
    {
        Seed();
        var (member, _) = await SignInAsync();
        var docs = await Json(member, "/api/documentation");
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
        Folders(await Json(veterano, "/api/documentation")).Should().Contain($"Atas CV {Year}");

        var (magister, mag) = await SignInAsync();
        await UpdateAsync(mag.Id, u => u.Positions = new List<Position> { Position.Magister });
        Folders(await Json(magister, "/api/documentation")).Should().Contain($"Atas CV {Year}");

        foreach (var role in new[] { "Mod", "Admin" })
        {
            var (client, _) = await SignInAsync(role);
            var seen = await Json(client, "/api/documentation");
            Folders(seen).Should().NotContain($"Atas CV {Year}", "{0} had no extra rights on the old page", role);
            seen.GetProperty("canManage").GetBoolean().Should().BeFalse(role);
        }

        var (owner, _) = await SignInAsync("Owner");
        var all = await Json(owner, "/api/documentation");
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

        var shown = await Json(member, $"/api/documentation?fiscalYear={previousYear}");
        shown.GetProperty("fiscalYear").GetString().Should().Be(previousYear);
        Folders(shown).Should().Equal("Antigos");
        shown.GetProperty("fiscalYears").EnumerateArray().Select(y => y.GetProperty("value").GetString()).Should().Contain(previousYear);
        (await Json(member, "/api/documentation?fiscalYear=")).GetProperty("fiscalYear").GetString().Should().Be(Year);

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

        var link = await Json(member, "/api/documentation/file?folder=Regulamentos&name=Estatutos.pdf");
        link.GetProperty("url").GetString().Should().Be($"https://r2.test/{Key("Regulamentos", "Estatutos.pdf")}?download=True");
        (await Json(member, "/api/documentation/file?folder=Logistics%2FJantar&name=orcamento.xlsx")).GetProperty("url").GetString()
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
        var (member, _) = await SignInAsync("Mod");
        (await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Novo.pdf"))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "every write needs the antiforgery token");
        await WithTokenAsync(member);

        var created = await Json(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Novo.pdf")));
        created.GetProperty("name").GetString().Should().Be("Novo.pdf");
        Storage.Objects[Key("Regulamentos", "Novo.pdf")].ContentType.Should().Be("application/pdf");

        (await Json(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", @"..\..\fora.pdf")))).GetProperty("name").GetString()
            .Should().Be("fora.pdf");
        Storage.Objects.Keys.Should().Contain(Key("Regulamentos", "fora.pdf")).And.OnlyContain(k => k.StartsWith("docs/Test/"));

        (await Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "script.exe"))))["file"].Single()
            .Should().StartWith("Tipo de ficheiro não permitido");
        (await Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "vazio.pdf", Array.Empty<byte>())))).Keys.Should().Equal("file");
        (await Errors(await member.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Estatutos.pdf"))))["file"].Single()
            .Should().Be("Já existe um documento com este nome nesta pasta.");
        Storage.Objects[Key("Regulamentos", "Estatutos.pdf")].Size.Should().Be(1234, "a member never replaces a document");
        (await member.PostAsync("/api/documentation/documents", Upload($"Atas CV {Year}", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync("/api/documentation/documents", Upload("Nao Existe", "x.pdf"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync("/api/documentation/documents", Upload("Logistics/Jantar", "fatura.pdf"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        (await owner.PostAsync("/api/documentation/documents", Upload("Regulamentos", "Estatutos.pdf"))).StatusCode.Should().Be(HttpStatusCode.OK);
        Storage.Objects[Key("Regulamentos", "Estatutos.pdf")].Size.Should().Be(3, "Owner replaces by uploading the same name");
    }

    [Fact]
    public async Task Folders_AndDeletes_AreOwnerOnly_WithTheOldValidation()
    {
        Seed();
        foreach (var role in new[] { (string?)null, "Mod", "Admin" })
        {
            var (client, _) = await SignInAsync(role);
            await WithTokenAsync(client);
            (await client.PostAsJsonAsync("/api/documentation/folders", new { name = "Nova" })).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await client.DeleteAsync("/api/documentation/documents?folder=Regulamentos&name=Estatutos.pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await client.DeleteAsync("/api/documentation/folders?folder=Regulamentos")).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
        }

        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        foreach (var bad in new[] { "", "  ", "Atas/../x", "Ação", new string('a', 51), "regulamentos" })
        {
            (await Errors(await owner.PostAsJsonAsync("/api/documentation/folders", new { name = bad }))).Keys.Should().Equal(new[] { "name" }, bad);
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

    // ---------- helpers ----------

    private static IEnumerable<string?> Folders(JsonElement body) =>
        body.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("name").GetString());

    private static IEnumerable<string?> Labels(JsonElement body) =>
        body.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("label").GetString());

    private static MultipartFormDataContent Upload(string folder, string fileName, byte[]? bytes = null)
    {
        var content = new ByteArrayContent(bytes ?? new byte[] { 1, 2, 3 });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { new StringContent(folder), "folder" }, { content, "file", fileName } };
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private async Task UpdateAsync(string userId, Action<ApplicationUser> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.Users.SingleAsync(u => u.Id == userId));
        await db.SaveChangesAsync();
    }

    private async Task AddFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.FiscalYears.AnyAsync(f => f.StartYear == start))
        {
            db.FiscalYears.Add(FiscalYear.Create(start, start + 1));
            await db.SaveChangesAsync();
        }
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"doc{Guid.NewGuid():N}"[..20], $"10.91.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.92.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}

/// <summary>The test host with document storage replaced by an in-memory bucket: nothing is written to R2.</summary>
public sealed class DocumentationApiFactory : TestWebApplicationFactory
{
    public FakeDocumentStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton<IDocumentStorageService>(Storage);
        });
    }
}

/// <summary>
/// An in-memory S3-like bucket with the listing semantics of <c>CloudflareDocumentStorageService</c>: "folder/" marker
/// objects, delimiter listing in ordinal key order, documents by file name. Pre-signed URLs are fake strings.
/// </summary>
public sealed class FakeDocumentStorage : IDocumentStorageService
{
    public sealed record StoredObject(long Size, string ContentType);

    public ConcurrentDictionary<string, StoredObject> Objects { get; } = new();

    /// <summary>Seeds an object; a key ending in "/" is a folder marker. Parent folder markers are added too.</summary>
    public void Put(string key, long size, string contentType = "application/pdf")
    {
        Objects[key] = new StoredObject(size, contentType);
        for (var i = key.IndexOf('/', "docs/".Length); i > 0 && i < key.Length - 1; i = key.IndexOf('/', i + 1))
        {
            Objects.TryAdd(key[..(i + 1)], new StoredObject(0, "application/x-directory"));
        }
    }

    public Task<string?> GetDocumentUrlAsync(string documentPath, bool forceDownload = false) =>
        Task.FromResult(Objects.ContainsKey(documentPath) ? $"https://r2.test/{documentPath}?download={forceDownload}" : null);

    public Task<bool> DocumentExistsAsync(string documentPath) => Task.FromResult(Objects.ContainsKey(documentPath));

    public Task<List<string>> ListFoldersAsync(string prefix = "docs/") => ListSubfoldersAsync($"{prefix}Test/");

    public Task<List<string>> ListSubfoldersAsync(string folderPath)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        return Task.FromResult(Objects.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.IndexOf('/', prefix.Length) > prefix.Length)
            .Select(k => k[prefix.Length..k.IndexOf('/', prefix.Length)])
            .Distinct()
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList());
    }

    public Task<List<DocumentMetadata>> ListDocumentsInFolderAsync(string folderPath)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        return Task.FromResult(Objects
            .Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal) && o.Key.Length > prefix.Length && o.Key.IndexOf('/', prefix.Length) < 0)
            .Select(o => new DocumentMetadata
            {
                FileName = Path.GetFileName(o.Key),
                FilePath = o.Key,
                SizeBytes = o.Value.Size,
                LastModified = DateTime.UtcNow,
                Extension = Path.GetExtension(o.Key),
            })
            .OrderBy(d => d.FileName)
            .ToList());
    }

    public async Task<string> UploadDocumentAsync(string folderPath, string fileName, Stream fileStream, string contentType)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy);
        Put(prefix + fileName, copy.Length, contentType);
        return prefix + fileName;
    }

    public Task CreateFolderAsync(string folderPath)
    {
        Put(folderPath.EndsWith('/') ? folderPath : folderPath + "/", 0, "application/x-directory");
        return Task.CompletedTask;
    }

    public Task<long> GetFileSizeAsync(string documentPath) =>
        Task.FromResult(Objects.TryGetValue(documentPath, out var o) ? o.Size : 0);

    public Task DeleteDocumentAsync(string documentPath)
    {
        Objects.TryRemove(documentPath, out _);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string folderPath)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        foreach (var key in Objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Objects.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
