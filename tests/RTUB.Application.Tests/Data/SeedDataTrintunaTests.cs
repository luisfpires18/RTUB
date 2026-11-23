using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// #366: the 7 Trintuna almoços come from the dev seed only (full dataset, never PROD). 1-6 are historical; the
/// 7º (Bragança) is the next one, under Próximos. A second run adds nothing.
/// </summary>
public class SeedDataTrintunaTests
{
    [Fact]
    public async Task SeedTrintuna_AddsSixHistoricalAndTheNextOne_Once()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());

        await SeedData.SeedTrintunaAsync(db);
        await SeedData.SeedTrintunaAsync(db);

        var rows = await db.Gatherings.OrderBy(g => g.Number).ToListAsync();
        rows.Select(g => g.Number).Should().Equal(1, 2, 3, 4, 5, 6, 7);
        rows.Take(6).Should().OnlyContain(g => g.IsHistorical);
        rows[6].IsHistorical.Should().BeFalse();
        rows[6].Place.Should().Be("Bragança");
        rows.Should().OnlyContain(g => g.Date == null && g.OrganizerUserId == null);
    }
}
