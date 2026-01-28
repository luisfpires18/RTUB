using FluentAssertions;
using RTUB.Application.Data;
using RTUB.Application.Services.MyTuno;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public class MyTunoUpgradeServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly ApplicationDbContext _context;
    private readonly MyTunoUpgradeService _service;

    public MyTunoUpgradeServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _context = _fixture.CreateContext();
        var characterService = new MyTunoCharacterService(_context);
        _service = new MyTunoUpgradeService(_context, characterService);
    }

    [Fact]
    public async Task UpgradeAsync_WithExactBalance_DoesNotGoNegative()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "test",
            Nickname = "test",
            FidelisBalance = 10m
        };

        var character = new Character
        {
            UserId = user.Id,
            Hp = 10,
            Power = 5,
            Speed = 5
        };

        _context.Users.Add(user);
        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        var result = await _service.UpgradeAsync(user.Id, MyTunoStatType.Hp);

        result.FidelisBalance.Should().Be(0m);
        result.HpUpgrades.Should().Be(1);
        result.NewHp.Should().Be(12);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
