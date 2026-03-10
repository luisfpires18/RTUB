using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MbwayTransferService
/// Tests MBWAY transfer CRUD operations and member guard clauses
/// </summary>
public class MbwayTransferServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MbwayTransferService _service;

    public MbwayTransferServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _service = new MbwayTransferService(_fixture.CreateContextFactory());
    }

    #region Helper Methods

    private async Task<ApplicationUser> CreateTestUser(string id = "test-user-1", string nickname = "TestUser")
    {
        // Use a separate context to avoid tracking conflicts
        using var ctx = _fixture.CreateContext();
        var existing = await ctx.Users.FindAsync(id);
        if (existing != null)
            return existing;

        var user = new ApplicationUser
        {
            Id = id,
            UserName = $"{nickname}@test.com",
            NormalizedUserName = $"{nickname}@TEST.COM",
            Email = $"{nickname}@test.com",
            NormalizedEmail = $"{nickname}@TEST.COM",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            Nickname = nickname,
            PhoneNumber = "900000000"
        };

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    private async Task<MbwayTransfer> CreateTestTransfer(
        string memberUserId,
        decimal amount = 25.00m,
        string? description = null,
        string? phone = null,
        string transferTo = "Test Recipient")
    {
        var transfer = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = amount,
            MemberUserId = memberUserId,
            TransferTo = transferTo,
            Description = description,
            Phone = phone
        };
        _context.MbwayTransfers.Add(transfer);
        await _context.SaveChangesAsync();
        return transfer;
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithNoTransfers_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithTransfers_ReturnsAllWithMemberIncluded()
    {
        // Arrange
        var user = await CreateTestUser();
        await CreateTestTransfer(user.Id, 10.00m);
        await CreateTestTransfer(user.Id, 20.00m);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.Member.Should().NotBeNull());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByDateDescending()
    {
        // Arrange
        var user = await CreateTestUser();
        var older = new MbwayTransfer
        {
            Date = DateTime.UtcNow.AddDays(-5),
            Amount = 10.00m,
            MemberUserId = user.Id,
            TransferTo = "Recipient A"
        };
        var newer = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 20.00m,
            MemberUserId = user.Id,
            TransferTo = "Recipient B"
        };
        _context.MbwayTransfers.AddRange(older, newer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.First().Amount.Should().Be(20.00m);
        result.Last().Amount.Should().Be(10.00m);
    }

    #endregion

    #region GetByFiscalYearAsync Tests

    [Fact]
    public async Task GetByFiscalYearAsync_ReturnsOnlyMatchingTransfers()
    {
        // Arrange
        var user = await CreateTestUser();
        var fy = FiscalYear.Create(2025, 2026);
        _context.FiscalYears.Add(fy);
        await _context.SaveChangesAsync();

        var withFy = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 50.00m,
            MemberUserId = user.Id,
            TransferTo = "FY Recipient",
            FiscalYearId = fy.Id
        };
        var withoutFy = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 30.00m,
            MemberUserId = user.Id,
            TransferTo = "No FY Recipient"
        };
        _context.MbwayTransfers.AddRange(withFy, withoutFy);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByFiscalYearAsync(fy.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Amount.Should().Be(50.00m);
    }

    #endregion

    #region AddTransferAsync Tests

    [Fact]
    public async Task AddTransferAsync_WithValidTransfer_ReturnsSuccess()
    {
        // Arrange
        var user = await CreateTestUser();
        var transfer = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 100.00m,
            MemberUserId = user.Id,
            TransferTo = "Valid Recipient",
            Phone = "912345678"
        };

        // Act
        var (success, message) = await _service.AddTransferAsync(transfer);

        // Assert
        success.Should().BeTrue();
        message.Should().Contain("sucesso");
        transfer.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddTransferAsync_WithEmptyMemberUserId_ReturnsFailure()
    {
        var transfer = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 50.00m,
            MemberUserId = "",
            TransferTo = "Some Recipient"
        };

        var (success, message) = await _service.AddTransferAsync(transfer);

        success.Should().BeFalse();
        message.Should().Contain("Membro");
    }

    [Fact]
    public async Task AddTransferAsync_WithNullMemberUserId_ReturnsFailure()
    {
        var transfer = new MbwayTransfer
        {
            Date = DateTime.UtcNow,
            Amount = 50.00m,
            MemberUserId = null!,
            TransferTo = "Some Recipient"
        };

        var (success, message) = await _service.AddTransferAsync(transfer);

        success.Should().BeFalse();
    }

    #endregion

    #region UpdateTransferAsync Tests

    [Fact]
    public async Task UpdateTransferAsync_WithExistingTransfer_UpdatesAndReturnsSuccess()
    {
        // Arrange
        var user = await CreateTestUser();
        var transfer = await CreateTestTransfer(user.Id, 25.00m);

        var updated = new MbwayTransfer
        {
            Id = transfer.Id,
            Date = transfer.Date,
            Amount = 75.00m,
            MemberUserId = user.Id,
            TransferTo = "Updated Recipient",
            Description = "Updated description",
            Phone = "961234567"
        };

        // Act
        var (success, message) = await _service.UpdateTransferAsync(updated);

        // Assert
        success.Should().BeTrue();

        using var verifyCtx = _fixture.CreateContext();
        var fetched = await verifyCtx.MbwayTransfers.FindAsync(transfer.Id);
        fetched!.Amount.Should().Be(75.00m);
        fetched.Description.Should().Be("Updated description");
        fetched.Phone.Should().Be("961234567");
    }

    [Fact]
    public async Task UpdateTransferAsync_WithEmptyMemberUserId_ReturnsFailure()
    {
        var transfer = new MbwayTransfer
        {
            Id = 1,
            Date = DateTime.UtcNow,
            Amount = 50.00m,
            MemberUserId = "",
            TransferTo = "Some Recipient"
        };

        var (success, message) = await _service.UpdateTransferAsync(transfer);

        success.Should().BeFalse();
        message.Should().Contain("Membro");
    }

    [Fact]
    public async Task UpdateTransferAsync_WithNonExistentTransfer_ReturnsFailure()
    {
        var transfer = new MbwayTransfer
        {
            Id = 999,
            Date = DateTime.UtcNow,
            Amount = 50.00m,
            MemberUserId = "some-user",
            TransferTo = "Some Recipient"
        };

        var (success, _) = await _service.UpdateTransferAsync(transfer);

        success.Should().BeFalse();
    }

    #endregion

    #region DeleteTransferAsync Tests

    [Fact]
    public async Task DeleteTransferAsync_WithExistingTransfer_DeletesAndReturnsSuccess()
    {
        // Arrange
        var user = await CreateTestUser();
        var transfer = await CreateTestTransfer(user.Id);

        // Act
        var (success, message) = await _service.DeleteTransferAsync(transfer.Id);

        // Assert
        success.Should().BeTrue();

        using var verifyCtx = _fixture.CreateContext();
        var fetched = await verifyCtx.MbwayTransfers.FindAsync(transfer.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTransferAsync_WithNonExistentId_ReturnsFailure()
    {
        var (success, message) = await _service.DeleteTransferAsync(999);

        success.Should().BeFalse();
        message.Should().Contain("não encontrada");
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
