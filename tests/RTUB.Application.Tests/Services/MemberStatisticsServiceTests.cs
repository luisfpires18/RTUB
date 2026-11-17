using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberStatisticsService
/// Tests business logic for member statistics and aggregated data queries
/// </summary>
public partial class MemberStatisticsServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MemberStatisticsService _service;

    // Test XP configuration constants
    private const int TestXpPerRehearsal = 12;
    private const int TestXpForFestival = 80;
    private const int TestXpForAtuacao = 30;
    private const int TestXpForConvivio = 50;

    public MemberStatisticsServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Create mock XpSettings for tests using constants
        var xpSettings = Options.Create(new XpSettings
        {
            XpPerRehearsal = TestXpPerRehearsal,
            XpPerEventType = new Dictionary<string, int>
            {
                { "Festival", TestXpForFestival },
                { "Atuacao", TestXpForAtuacao },
                { "Convivio", TestXpForConvivio }
            }
        });

        _service = new MemberStatisticsService(_fixture.CreateContextFactory(), xpSettings);
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
