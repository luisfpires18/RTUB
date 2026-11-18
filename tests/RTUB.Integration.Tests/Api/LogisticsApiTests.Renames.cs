// LogisticsApiTests tests: renames with storage failures and legacy columns.
using System.Net;
using FluentAssertions;
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
    /// <summary>105 (review): a retry reuses its earlier copy when only the ETag's casing differs (no "name (2)").</summary>
    [Fact]
    public async Task ARetriedMove_ReusesItsCopy_WhateverTheETagCasing()
    {
        var name = $"{Tag()} Ronda";
        var (boardId, _, _) = await SeedBoardAsync(name: name);
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = name);
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var source = $"logistics/Test/boards/{boardId}/files/plano.pdf";
        _factory.Storage.Put(source, 10, etag: "abcdef");
        _factory.Storage.Put($"docs/Test/{year}/Logistics/{name}/plano.pdf", 10, etag: "\"ABCDEF\""); // the earlier run's copy, as R2 reports it

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
        }

        _factory.Storage.Objects.Keys.Should().NotContain(source).And.NotContain($"docs/Test/{year}/Logistics/{name}/plano (2).pdf");
    }

    /// <summary>
    /// 104 (review): a rename whose copy fails changes nothing; one whose source delete fails still moves (rows and folder
    /// in one save), and the leftover sits in a folder no board owns, which members do not see.
    /// </summary>
    [Fact]
    public async Task ARenameWithStorageFailures_NeverLeavesACardPointingAtAMissingFile()
    {
        var name = $"{Tag()} Serenata";
        var (boardId, _, cardId) = await SeedBoardAsync(name: name);
        var (mod, _) = await SignInAsync("Mod");
        var (member, _) = await SignInAsync();
        await WithTokenAsync(mod);
        var file = (await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("cartaz.png", "image/png"))))
            .GetProperty("attachments").EnumerateArray().Single().GetProperty("id").GetInt32();
        await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("plano.pdf")));
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var source = $"docs/Test/{year}/Logistics/{name}/cartaz.png";
        var renamed = $"{name} 2027";
        async Task RenameAsync(string to) =>
            (await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}", new { name = to, description = "", eventId = (int?)null }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Storage.Fails = (op, key) => op == "copy" && key.EndsWith("/plano.pdf");
        try
        {
            await RenameAsync(renamed);
        }
        finally
        {
            _factory.Storage.Fails = null;
        }

        (await BoardFolderAsync(boardId)).Should().Be(name, "a failed copy changes nothing");
        _factory.Storage.Objects.Keys.Should().NotContain(k => k.StartsWith($"docs/Test/{year}/Logistics/{renamed}/") && !k.EndsWith('/'),
            "the copies already made are removed again");
        (await Json(mod, $"/api/logistics/cards/{cardId}/attachments/{file}")).GetProperty("url").GetString().Should().Contain(source);

        _factory.Storage.Fails = (op, key) => op == "delete" && key == source;
        try
        {
            using var scope = _factory.Services.CreateScope(); // the next start retries the move
            await scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
        }
        finally
        {
            _factory.Storage.Fails = null;
        }

        (await BoardFolderAsync(boardId)).Should().Be(renamed);
        (await Json(mod, $"/api/logistics/cards/{cardId}/attachments/{file}")).GetProperty("url").GetString()
            .Should().Contain($"docs/Test/{year}/Logistics/{renamed}/cartaz.png");
        _factory.Storage.Objects.Keys.Should().Contain(source, "the refused delete leaves a duplicate behind");
        var folders = (await Json(member, "/api/documentation")).GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();
        folders.Should().Contain($"Logistics/{renamed}").And.NotContain($"Logistics/{name}", "no board owns the old folder now");
        (await Json(mod, "/api/documentation")).GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("name").GetString())
            .Should().Contain($"Logistics/{name}");
    }

    /// <summary>104: renaming a board moves its folder in every fiscal year; card files follow; same names are kept apart.</summary>
    [Fact]
    public async Task RenamingABoard_MovesItsFolder_AndCardFilesFollow()
    {
        var name = $"{Tag()} Arraial";
        var (boardId, _, cardId) = await SeedBoardAsync(name: name);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var file = (await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("cartaz.png", "image/png"))))
            .GetProperty("attachments").EnumerateArray().Single().GetProperty("id").GetInt32();
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var previous = $"{int.Parse(year[..4]) - 1}-{int.Parse(year[..4])}";
        _factory.Storage.Put($"docs/Test/{previous}/Logistics/{name}/antigo.pdf", 10);

        var renamed = $"{name} 2027";
        (await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}", new { name = renamed, description = "", eventId = (int?)null }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BoardFolderAsync(boardId)).Should().Be(renamed);
        _factory.Storage.Objects.Keys.Should().Contain(new[] { $"docs/Test/{year}/Logistics/{renamed}/cartaz.png", $"docs/Test/{previous}/Logistics/{renamed}/antigo.pdf" })
            .And.NotContain(k => k.StartsWith($"docs/Test/{year}/Logistics/{name}/") || k.StartsWith($"docs/Test/{previous}/Logistics/{name}/"));
        (await Json(mod, $"/api/logistics/cards/{cardId}/attachments/{file}")).GetProperty("url").GetString()
            .Should().Contain($"docs/Test/{year}/Logistics/{renamed}/cartaz.png");
        (await Json(mod, $"/api/logistics/boards/{boardId}/file-list")).EnumerateArray().Select(f => f.GetProperty("id").GetString())
            .Should().Equal($"l:{previous}/antigo.pdf");
    }

    /// <summary>104: files kept outside Documentação, or under an older board name, move in at startup; a second run moves nothing.</summary>
    [Fact]
    public async Task OlderLogisticsFiles_MoveIntoDocumentation_Once()
    {
        var name = $"{Tag()} Festa";
        var (boardId, _, cardId) = await SeedBoardAsync(name: name);
        var old = $"{Tag()} Antigo";
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = old);
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var previous = $"{int.Parse(year[..4]) - 1}-{int.Parse(year[..4])}";
        _factory.Storage.Put($"logistics/Test/boards/{boardId}/files/orcamento.pdf", 10);
        _factory.Storage.Put($"logistics/Test/cards/{cardId}/abc/foto.png", 20, "image/png");
        _factory.Storage.Put($"docs/Test/{previous}/Logistics/{old}/ata.pdf", 30);
        _factory.Storage.Put($"docs/Test/{year}/Logistics/{name}/foto.png", 40, "image/png");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = cardId, Kind = CardAttachmentKind.File, Name = "foto.png", StoragePath = $"logistics/Test/cards/{cardId}/abc/foto.png",
                ContentType = "image/png", SizeBytes = 20,
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ILogisticsKanbanService>();
            (await service.MoveFilesIntoDocumentationAsync()).Should().BeGreaterThanOrEqualTo(3);
            (await service.MoveFilesIntoDocumentationAsync()).Should().Be(0, "idempotent");
        }

        // The folder "Name" already holds an unowned document, so the board takes "Name 2" and leaves it alone.
        var folder = $"{name} 2";
        (await BoardFolderAsync(boardId)).Should().Be(folder);
        _factory.Storage.Objects.Keys.Should().Contain(new[]
        {
            $"docs/Test/{year}/Logistics/{folder}/orcamento.pdf", $"docs/Test/{year}/Logistics/{folder}/foto.png",
            $"docs/Test/{previous}/Logistics/{folder}/ata.pdf", $"docs/Test/{year}/Logistics/{name}/foto.png",
        }).And.NotContain(k => k.StartsWith($"logistics/Test/boards/{boardId}/files/") || k.StartsWith($"logistics/Test/cards/{cardId}/abc/")
            || k.StartsWith($"docs/Test/{previous}/Logistics/{old}/"), "files and emptied folders leave (the fake adds parent markers R2 never has)");
        using (var scope = _factory.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardAttachments.AsNoTracking().SingleAsync(a => a.CardId == cardId))
                .StoragePath.Should().Be($"docs/Test/{year}/Logistics/{folder}/foto.png");
        }
    }

    /// <summary>
    /// 067 migration rule on real SQLite: a pre-067 column takes its majority status and the others split into
    /// "Name (STATE)" columns right after it, in order; no card changes status; an empty "Feito" column is DONE. Board file
    /// folders: the sanitised name, or none when two boards share it.
    /// </summary>
    [Fact]
    public async Task LegacyColumns_AreMappedWithoutChangingAnyCardsStatus()
    {
        int boardId, mixed, done, twinA, twinB, single;
        var twin = $"{Tag()} Gémeo";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = LogisticsBoard.Create($"{Tag()} Antigo");
            var a = LogisticsBoard.Create(twin);
            var b = LogisticsBoard.Create(twin + "?");
            db.LogisticsBoards.AddRange(board, a, b);
            await db.SaveChangesAsync();
            (boardId, twinA, twinB, single) = (board.Id, a.Id, b.Id, board.Id);
            var m = LogisticsList.Create("Material", board.Id, 0);
            var f = LogisticsList.Create("Feito", board.Id, 1);
            db.LogisticsLists.AddRange(m, f);
            await db.SaveChangesAsync();
            (mixed, done) = (m.Id, f.Id);
            var statuses = new[] { CardStatus.Todo, CardStatus.Done, CardStatus.Todo, CardStatus.InProgress, CardStatus.Done, CardStatus.Todo };
            for (var i = 0; i < statuses.Length; i++)
            {
                var card = LogisticsCard.Create($"c{i}", m.Id, i);
                card.SetStatus(statuses[i]);
                db.LogisticsCards.Add(card);
            }

            await db.SaveChangesAsync();
            await LogisticsWorkflow.MapLegacyAsync(db);
            await LogisticsWorkflow.MapLegacyAsync(db); // idempotent
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lists = await db.LogisticsLists.AsNoTracking().Where(l => l.BoardId == boardId).OrderBy(l => l.Position).ToListAsync();
            lists.Select(l => (l.Name, l.State)).Should().Equal(("Material", CardStatus.Todo), ("Material (WIP)", CardStatus.InProgress),
                ("Material (DONE)", CardStatus.Done), ("Feito", CardStatus.Done));
            var cards = await db.LogisticsCards.AsNoTracking().Where(c => lists.Select(l => l.Id).Contains(c.ListId)).ToListAsync();
            cards.Should().HaveCount(6).And.OnlyContain(c => lists.Single(l => l.Id == c.ListId).State == c.Status, "no card changes status");
            cards.Where(c => c.ListId == mixed).OrderBy(c => c.Position).Select(c => c.Title).Should().Equal("c0", "c2", "c5");
            cards.Where(c => c.Status == CardStatus.Done).OrderBy(c => c.Position).Select(c => c.Title).Should().Equal("c1", "c4");
            (await db.LogisticsBoards.AsNoTracking().SingleAsync(x => x.Id == single)).LegacyFolder.Should().NotBeNullOrEmpty();
            (await db.LogisticsBoards.AsNoTracking().Where(x => x.Id == twinA || x.Id == twinB).Select(x => x.LegacyFolder).ToListAsync())
                .Should().Equal(new[] { "", "" }, "two boards with one folder name cannot be told apart");
            _ = done;
        }
    }
}
