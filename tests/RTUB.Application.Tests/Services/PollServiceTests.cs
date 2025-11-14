using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for PollService
/// Tests business logic and service layer operations
/// </summary>
public class PollServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PollService _pollService;

    public PollServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext());
        _pollService = new PollService(_context);
    }

    #region Create Poll Tests

    [Fact]
    public async Task CreatePollAsync_WithValidData_ReturnsPoll()
    {
        // Arrange
        var title = "Escolha da próxima atuação";
        var description = "Votar no local da próxima atuação";
        var userId = "user123";

        // Act
        var result = await _pollService.CreatePollAsync(title, description, userId, PollType.SingleChoice, false, 1);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Description.Should().Be(description);
        result.CreatedByUserId.Should().Be(userId);
        result.PollType.Should().Be(PollType.SingleChoice);
        result.Status.Should().Be(PollStatus.Active);
    }

    [Fact]
    public async Task CreatePollAsync_WithFutureStartDate_SetsScheduledStatus()
    {
        // Arrange
        var title = "Votação futura";
        var userId = "user123";
        var futureDate = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await _pollService.CreatePollAsync(title, null, userId, PollType.SingleChoice, false, 1, futureDate, null);

        // Assert
        result.Status.Should().Be(PollStatus.Scheduled);
    }

    [Fact]
    public async Task CreatePollAsync_WithInvalidDates_ThrowsException()
    {
        // Arrange
        var title = "Votação com datas inválidas";
        var userId = "user123";
        var startDate = DateTime.UtcNow.AddDays(10);
        var endDate = DateTime.UtcNow.AddDays(5); // End before start

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.CreatePollAsync(title, null, userId, PollType.SingleChoice, false, 1, startDate, endDate)
        );
    }

    #endregion

    #region Get Poll Tests

    [Fact]
    public async Task GetPollAsync_ExistingPoll_ReturnsPoll()
    {
        // Arrange
        var poll = await CreateTestPoll();

        // Act
        var result = await _pollService.GetPollAsync(poll.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(poll.Id);
        result.Title.Should().Be(poll.Title);
    }

    [Fact]
    public async Task GetPollAsync_NonExistingPoll_ReturnsNull()
    {
        // Act
        var result = await _pollService.GetPollAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllPollsAsync_WithStatusFilter_ReturnsFilteredPolls()
    {
        // Arrange
        await CreateTestPoll("Poll 1", PollStatus.Active);
        await CreateTestPoll("Poll 2", PollStatus.Closed);
        await CreateTestPoll("Poll 3", PollStatus.Active);

        // Act
        var activePolls = await _pollService.GetAllPollsAsync(PollStatus.Active);

        // Assert
        activePolls.Should().HaveCount(2);
        activePolls.Should().OnlyContain(p => p.Status == PollStatus.Active);
    }

    #endregion

    #region Add Option Tests

    [Fact]
    public async Task AddOptionAsync_ValidOption_AddsOption()
    {
        // Arrange
        var poll = await CreateTestPoll();

        // Act
        var option = await _pollService.AddOptionAsync(poll.Id, "Nova opção");

        // Assert
        option.Should().NotBeNull();
        option.Text.Should().Be("Nova opção");
        option.PollId.Should().Be(poll.Id);
    }

    [Fact]
    public async Task AddOptionAsync_NonExistingPoll_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.AddOptionAsync(999, "Opção")
        );
    }

    #endregion

    #region Vote Tests

    [Fact]
    public async Task VoteAsync_SingleChoice_ValidVote_SuccessfullyVotes()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);
        var option = poll.Options.First();

        // Act
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id });

        // Assert
        var results = await _pollService.GetPollResultsAsync(poll.Id);
        results[option.Id].Should().Be(1);
    }

    [Fact]
    public async Task VoteAsync_MultipleChoice_ValidVotes_SuccessfullyVotes()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.MultipleChoice, maxVotes: 2);
        var options = poll.Options.Take(2).ToList();

        // Act
        await _pollService.VoteAsync(poll.Id, "user123", options.Select(o => o.Id).ToList());

        // Assert
        var results = await _pollService.GetPollResultsAsync(poll.Id);
        results[options[0].Id].Should().Be(1);
        results[options[1].Id].Should().Be(1);
    }

    [Fact]
    public async Task VoteAsync_SingleChoice_MultipleOptions_ThrowsException()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);
        var optionIds = poll.Options.Take(2).Select(o => o.Id).ToList();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.VoteAsync(poll.Id, "user123", optionIds)
        );
    }

    [Fact]
    public async Task VoteAsync_ExceedsMaxVotes_ThrowsException()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.MultipleChoice, maxVotes: 2);
        var optionIds = poll.Options.Take(3).Select(o => o.Id).ToList();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.VoteAsync(poll.Id, "user123", optionIds)
        );
    }

    [Fact]
    public async Task VoteAsync_DuplicateVote_NonAnonymous_ThrowsException()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice, isAnonymous: false);
        var option = poll.Options.First();
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id })
        );
    }

    [Fact]
    public async Task VoteAsync_ClosedPoll_ThrowsException()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);
        await _pollService.ClosePollAsync(poll.Id);
        var option = poll.Options.First();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id })
        );
    }

    [Fact]
    public async Task VoteAsync_InvalidOption_ThrowsException()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _pollService.VoteAsync(poll.Id, "user123", new List<int> { 9999 })
        );
    }

    [Fact]
    public async Task VoteAsync_Anonymous_AllowsMultipleVotesFromSameUser()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice, isAnonymous: true);
        var option1 = poll.Options.First();
        var option2 = poll.Options.Last();

        // Act - Vote twice with same user (should succeed for anonymous polls)
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option1.Id });
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option2.Id });

        // Assert
        var results = await _pollService.GetPollResultsAsync(poll.Id);
        results[option1.Id].Should().Be(1);
        results[option2.Id].Should().Be(1);
    }

    #endregion

    #region Change Vote Tests

    [Fact]
    public async Task RemoveVoteAsync_ExistingVote_RemovesVote()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice, isAnonymous: false);
        var option = poll.Options.First();
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id });

        // Act
        await _pollService.RemoveVoteAsync(poll.Id, "user123");

        // Assert
        var hasVoted = await _pollService.HasUserVotedAsync(poll.Id, "user123");
        hasVoted.Should().BeFalse();
    }

    [Fact]
    public async Task HasUserVotedAsync_AfterVoting_ReturnsTrue()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);
        var option = poll.Options.First();
        await _pollService.VoteAsync(poll.Id, "user123", new List<int> { option.Id });

        // Act
        var result = await _pollService.HasUserVotedAsync(poll.Id, "user123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserVotesForPollAsync_AfterVoting_ReturnsVotes()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.MultipleChoice, maxVotes: 2);
        var options = poll.Options.Take(2).ToList();
        var optionIds = options.Select(o => o.Id).ToList();
        await _pollService.VoteAsync(poll.Id, "user123", optionIds);

        // Act
        var votes = await _pollService.GetUserVotesForPollAsync(poll.Id, "user123");

        // Assert
        votes.Should().HaveCount(2);
        votes.Should().Contain(optionIds);
    }

    #endregion

    #region Update and Delete Tests

    [Fact]
    public async Task UpdatePollAsync_ValidData_UpdatesPoll()
    {
        // Arrange
        var poll = await CreateTestPoll();
        var newTitle = "Título atualizado";

        // Act
        await _pollService.UpdatePollAsync(poll.Id, newTitle, "Nova descrição", null, null, false, 1);

        // Assert
        var updatedPoll = await _pollService.GetPollAsync(poll.Id);
        updatedPoll!.Title.Should().Be(newTitle);
        updatedPoll.Description.Should().Be("Nova descrição");
    }

    [Fact]
    public async Task ClosePollAsync_ActivePoll_ClosesPoll()
    {
        // Arrange
        var poll = await CreateTestPoll();

        // Act
        await _pollService.ClosePollAsync(poll.Id);

        // Assert
        var closedPoll = await _pollService.GetPollAsync(poll.Id);
        closedPoll!.Status.Should().Be(PollStatus.Closed);
    }

    [Fact]
    public async Task DeletePollAsync_ExistingPoll_DeletesPoll()
    {
        // Arrange
        var poll = await CreateTestPoll();

        // Act
        await _pollService.DeletePollAsync(poll.Id);

        // Assert
        var deletedPoll = await _pollService.GetPollAsync(poll.Id);
        deletedPoll.Should().BeNull();
    }

    #endregion

    #region Results Tests

    [Fact]
    public async Task GetPollResultsAsync_WithVotes_ReturnsCorrectCounts()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);
        var option1 = poll.Options.First();
        var option2 = poll.Options.Last();
        
        await _pollService.VoteAsync(poll.Id, "user1", new List<int> { option1.Id });
        await _pollService.VoteAsync(poll.Id, "user2", new List<int> { option1.Id });
        await _pollService.VoteAsync(poll.Id, "user3", new List<int> { option2.Id });

        // Act
        var results = await _pollService.GetPollResultsAsync(poll.Id);

        // Assert
        results[option1.Id].Should().Be(2);
        results[option2.Id].Should().Be(1);
    }

    [Fact]
    public async Task GetPollResultsAsync_NoVotes_ReturnsZeroCounts()
    {
        // Arrange
        var poll = await CreateTestPollWithOptions(PollType.SingleChoice);

        // Act
        var results = await _pollService.GetPollResultsAsync(poll.Id);

        // Assert
        results.Should().AllSatisfy(kvp => kvp.Value.Should().Be(0));
    }

    #endregion

    #region Helper Methods

    private async Task<Poll> CreateTestPoll(string title = "Test Poll", PollStatus status = PollStatus.Active)
    {
        var poll = Poll.Create(title, "testUser", PollType.SingleChoice);
        poll.SetStatus(status);
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();
        
        return poll;
    }

    private async Task<Poll> CreateTestPollWithOptions(PollType pollType, int maxVotes = 1, bool isAnonymous = false)
    {
        var poll = Poll.Create("Test Poll with Options", "testUser", pollType);
        poll.UpdateDetails("Test Poll with Options", null, null, null, isAnonymous, maxVotes);
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var option1 = PollOption.Create("Option 1", poll.Id);
        var option2 = PollOption.Create("Option 2", poll.Id);
        var option3 = PollOption.Create("Option 3", poll.Id);
        
        _context.PollOptions.AddRange(option1, option2, option3);
        await _context.SaveChangesAsync();

        return poll;
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
