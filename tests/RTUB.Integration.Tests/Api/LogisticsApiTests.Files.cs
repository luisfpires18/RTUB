// LogisticsApiTests tests: board and card files in Documentation, moves and ETags.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class LogisticsApiTests
{
    /// <summary>
    /// 067: a private board's pre-067 folder is hidden in Documentação from members off its access list - fail closed
    /// (security review): also for a board whose folder is unresolved (null) or shared with another board (""), by its
    /// sanitised name, whatever the case. Managers still see every folder.
    /// </summary>
    [Fact]
    public async Task Documentation_HidesAPrivateBoardsOldFolder()
    {
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var (boardId, _, _) = await SeedBoardAsync();
        var folder = $"{Tag()}legacy";
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = folder);
        _factory.Storage.Put($"docs/Test/{year}/Logistics/{folder}/plano.pdf", 10);
        var shared = new List<string>();
        foreach (var unresolved in new string?[] { null, "" })
        {
            var (other, _, _) = await SeedBoardAsync();
            var name = $"{Tag()}Partilhado";
            shared.Add(name);
            _factory.Storage.Put($"docs/Test/{year}/Logistics/{name}/plano.pdf", 10);
            await UpdateBoardAsync(other, b =>
            {
                b.Name = name.ToUpperInvariant();
                b.LegacyFolder = unresolved;
                b.Visibility = BoardVisibility.Private;
            });
        }

        var (member, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var managed = await mod.GetStringAsync("/api/documentation");
        managed.Should().Contain(folder).And.Contain(shared[0]).And.Contain(shared[1], "the folders are listed: managers see every one");
        var listed = await member.GetStringAsync("/api/documentation");
        listed.Should().Contain(folder, "members-visible board: as before");
        foreach (var name in shared)
        {
            listed.Should().NotContain(name, "a private board hides its folder even when LegacyFolder is null or shared");
            (await member.GetAsync($"/api/documentation/file?folder=Logistics/{name}&name=plano.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        (await member.GetStringAsync("/api/documentation")).Should().NotContain(folder);
        (await member.GetAsync($"/api/documentation/file?folder=Logistics/{folder}&name=plano.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// 104: a card's image and a board file are documents of the board's Documentação folder; deleting the card file in
    /// Documentação removes it from the card (one history line); deleting the board leaves the folder.
    /// </summary>
    [Fact]
    public async Task LogisticsFiles_ShowInDocumentation_AndADeleteThereLeavesTheCardToo()
    {
        var name = $"{Tag()} Jantar";
        var (boardId, _, cardId) = await SeedBoardAsync(name: name);
        var (mod, _) = await SignInAsync("Mod");
        var (owner, _) = await SignInAsync("Owner");
        var (member, _) = await SignInAsync();
        await WithTokenAsync(mod);
        await WithTokenAsync(owner);
        var file = (await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("foto.png", "image/png"))))
            .GetProperty("attachments").EnumerateArray().Single().GetProperty("id").GetInt32();
        await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("plano.pdf")));
        (await BoardFolderAsync(boardId)).Should().Be(name, "the folder is the board's name");

        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var docs = await Json(member, "/api/documentation");
        docs.GetProperty("folders").EnumerateArray().Single(f => f.GetProperty("name").GetString() == $"Logistics/{name}")
            .GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("name").GetString()).Should().BeEquivalentTo("foto.png", "plano.pdf");
        (await Json(member, $"/api/logistics/boards/{boardId}/file-list")).EnumerateArray().Select(f => f.GetProperty("name").GetString())
            .Should().Equal(new[] { "plano.pdf" }, "a card's files show on their card, not in the board's own list");
        (await Json(member, $"/api/logistics/cards/{cardId}/attachments/{file}")).GetProperty("url").GetString()
            .Should().Contain($"docs/Test/{year}/Logistics/{name}/foto.png");

        (await owner.DeleteAsync($"/api/documentation/documents?fiscalYear={year}&folder={Uri.EscapeDataString($"Logistics/{name}")}&name=foto.png"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var card = await Json(member, $"/api/logistics/cards/{cardId}");
        card.GetProperty("attachments").GetArrayLength().Should().Be(0, "deleted in Documentação, gone from the card");
        card.GetProperty("activity").EnumerateArray().First().GetProperty("detail").GetString().Should().Be("Anexo removido em Documentação: foto.png");
        _factory.Storage.Objects.Keys.Should().NotContain($"docs/Test/{year}/Logistics/{name}/foto.png");

        // A storage failure on delete leaves the card detached and the document in place; deleting again finishes it.
        await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("convite.png", "image/png")));
        var convite = $"docs/Test/{year}/Logistics/{name}/convite.png";
        _factory.Storage.Fails = (op, key) => op == "delete" && key == convite;
        try
        {
            (await owner.DeleteAsync($"/api/documentation/documents?fiscalYear={year}&folder={Uri.EscapeDataString($"Logistics/{name}")}&name=convite.png"))
                .StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            _factory.Storage.Fails = null;
        }

        (await Json(member, $"/api/logistics/cards/{cardId}")).GetProperty("attachments").GetArrayLength().Should().Be(0,
            "the card never points at a file the failed delete may have removed");
        _factory.Storage.Objects.Keys.Should().Contain(convite);
        (await owner.DeleteAsync($"/api/documentation/documents?fiscalYear={year}&folder={Uri.EscapeDataString($"Logistics/{name}")}&name=convite.png"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().NotContain(convite);

        // The CV atas stay with Veteranos, Tunossauros and the Magister, Logística folders or not.
        _factory.Storage.Put($"docs/Test/{year}/Atas CV {year}/Ata CV 1.pdf", 10);
        _factory.Services.GetRequiredService<DocumentTreeCache>().Forget($"docs/Test/{year}/");
        (await member.GetStringAsync("/api/documentation")).Should().NotContain("Atas CV");
        (await mod.GetStringAsync("/api/documentation")).Should().NotContain("Atas CV", "Mod is no Veterano: the CV rule is not a management right");
        (await member.GetAsync($"/api/documentation/file?fiscalYear={year}&folder={Uri.EscapeDataString($"Atas CV {year}")}&name=Ata%20CV%201.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("recibo.png", "image/png")));
        (await mod.DeleteAsync($"/api/logistics/boards/{boardId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().Contain(new[] { $"docs/Test/{year}/Logistics/{name}/plano.pdf", $"docs/Test/{year}/Logistics/{name}/recibo.png" },
            "deleting a board leaves its Documentação folder");
        (await member.GetStringAsync("/api/documentation")).Should().NotContain(name, "a folder no board owns may hold a private board's files");
        (await mod.GetStringAsync("/api/documentation")).Should().Contain(name, "Mod, Admin and Owner still see it");
    }

    /// <summary>
    /// 104 (review): startup moves survive refused deletes - every file stays reachable, the card points at its copy - and
    /// the next start finishes them without "name (2)" copies; card-file leftovers are removed.
    /// </summary>
    [Fact]
    public async Task FileMoves_SurviveAStorageFailure_AndTheNextStartFinishesThem()
    {
        var name = $"{Tag()} Tuna";
        var (boardId, _, cardId) = await SeedBoardAsync(name: name);
        var old = $"{Tag()} Velho";
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = old);
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var previous = $"{int.Parse(year[..4]) - 1}-{int.Parse(year[..4])}";
        var sources = new[]
        {
            $"docs/Test/{previous}/Logistics/{old}/ata.pdf", $"logistics/Test/boards/{boardId}/files/orcamento.pdf", $"logistics/Test/cards/{cardId}/abc/foto.png",
        };
        _factory.Storage.Put(sources[0], 30);
        _factory.Storage.Put(sources[1], 10);
        _factory.Storage.Put(sources[2], 20, "image/png");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = cardId, Kind = CardAttachmentKind.File, Name = "foto.png", StoragePath = sources[2], ContentType = "image/png", SizeBytes = 20,
            });
            await db.SaveChangesAsync();
        }

        _factory.Storage.Fails = (op, key) => op == "delete" && sources.Contains(key);
        try
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
        }
        finally
        {
            _factory.Storage.Fails = null;
        }

        (await BoardFolderAsync(boardId)).Should().Be(name, "every copy landed, so the folder moved");
        _factory.Storage.Objects.Keys.Should().Contain(sources, "a refused delete keeps its source")
            .And.Contain(new[] { $"docs/Test/{previous}/Logistics/{name}/ata.pdf", $"docs/Test/{year}/Logistics/{name}/orcamento.pdf" });
        (await AttachmentPathAsync(cardId)).Should().Be($"docs/Test/{year}/Logistics/{name}/foto.png", "the card points at the copy");

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>();
            await service.MoveFilesIntoDocumentationAsync();
            (await service.MoveFilesIntoDocumentationAsync()).Should().Be(0, "idempotent");
        }

        _factory.Storage.Objects.Keys.Should().NotContain(new[] { sources[1], sources[2] }, "the retry reuses the copy and drops the leftovers")
            .And.NotContain(k => k.Contains($"/Logistics/{name}/") && k.Contains(" (2)"));
        _factory.Storage.Objects.Keys.Should().Contain(sources[0], "a source left in a folder no board owns is only a duplicate");
        (await AttachmentPathAsync(cardId)).Should().Be($"docs/Test/{year}/Logistics/{name}/foto.png");
    }

    /// <summary>
    /// 105 (security review): a retry reuses only a copy with the source's content. A member's different file of the same
    /// name and size, put in the board's folder between two starts, is never taken for it, kept untouched, and the source
    /// gets its own "name (2)" copy before it is deleted.
    /// </summary>
    [Fact]
    public async Task ARetriedMove_NeverTakesAPlantedSameSizeFileForItsCopy()
    {
        var name = $"{Tag()} Cortejo";
        var (boardId, _, _) = await SeedBoardAsync(name: name);
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = name);
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var source = $"logistics/Test/boards/{boardId}/files/orcamento.pdf";
        var copy = $"docs/Test/{year}/Logistics/{name}/orcamento.pdf";
        _factory.Storage.Put(source, 10);

        _factory.Storage.Fails = (op, key) => op == "delete" && key == source;
        try
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
        }
        finally
        {
            _factory.Storage.Fails = null;
        }

        _factory.Storage.Objects.Keys.Should().Contain(new[] { source, copy });
        _factory.Storage.Put(copy, 10, etag: "planted"); // same name, same size, other content

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
        }

        _factory.Storage.Objects[copy].ETag.Should().Be("planted", "the member's file is left as it is");
        _factory.Storage.Objects.Keys.Should().Contain($"docs/Test/{year}/Logistics/{name}/orcamento (2).pdf", "the source got its own copy")
            .And.NotContain(source);
    }

    /// <summary>105 (review): ETags compare without quotes and hex case; a missing one never matches.</summary>
    [Theory]
    [InlineData("\"ABC123\"", "abc123", true)]
    [InlineData("abc123", "ABC123", true)]
    [InlineData("abc123", "abc124", false)]
    [InlineData(null, "abc123", false)]
    [InlineData("\"\"", "\"\"", false)]
    public void ETags_CompareWithoutQuotesOrCase(string? a, string? b, bool same) =>
        LogisticsKanbanService.SameETag(a, b).Should().Be(same);

    private async Task<string?> AttachmentPathAsync(int cardId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardAttachments.Where(a => a.CardId == cardId)
            .Select(a => a.StoragePath).SingleAsync();
    }
}
