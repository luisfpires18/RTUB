using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RankingServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly RankingConfiguration _config;
    private readonly RankingService _service;

    public RankingServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();

        // Setup test configuration
        _config = new RankingConfiguration
        {
            XpPerRehearsal = 10,
            XpPerEventType = new Dictionary<string, int>
            {
                { "Festival", 50 },
                { "Atuacao", 25 },
                { "Casamento", 30 }
            },
            Levels = new List<LevelDefinition>
            {
                new() { Level = 1, Name = "Lei Seca", XpThreshold = 0 },
                new() { Level = 2, Name = "Vai mais um copo", XpThreshold = 100 },
                new() { Level = 3, Name = "Já vai alto", XpThreshold = 250 },
                new() { Level = 4, Name = "Tá na brasa", XpThreshold = 500 },
                new() { Level = 5, Name = "Veterano das Cervejas", XpThreshold = 1000 }
            }
        };


        var configOptions = Options.Create(_config);
        var attendanceRepo = new RehearsalAttendanceRepository(_fixture.CreateContextFactory());
        var enrollmentRepo = new EnrollmentRepository(_fixture.CreateContextFactory());
        _service = new RankingService(
            attendanceRepo,
            enrollmentRepo,
            configOptions);
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
