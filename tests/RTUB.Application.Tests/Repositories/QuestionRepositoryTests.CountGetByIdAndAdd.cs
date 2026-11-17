using FluentAssertions;

namespace RTUB.Application.Tests.Repositories;

// QuestionRepositoryTests: Count Get By Id And Add tests.
public partial class QuestionRepositoryTests
{
    // ── GetCountAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectTotal()
    {
        await SeedQuestionsAsync(15);

        var count = await _repository.GetCountAsync();

        count.Should().Be(15);
    }

    [Fact]
    public async Task GetCountAsync_MatchesPaginationTotalAcrossAllPages()
    {
        await SeedQuestionsAsync(23);
        const int pageSize = 10;

        var count = await _repository.GetCountAsync();
        var page1 = (await _repository.GetAllAsync(1, pageSize)).Count();
        var page2 = (await _repository.GetAllAsync(2, pageSize)).Count();
        var page3 = (await _repository.GetAllAsync(3, pageSize)).Count();

        count.Should().Be(23);
        (page1 + page2 + page3).Should().Be(23);
    }

    [Fact]
    public async Task GetCountAsync_WithStatusFilter_CountsOnlyMatchingStatus()
    {
        var closed = CreateQuestion("Closed");
        closed.Close();
        await SeedAsync(CreateQuestion("Open 1"), CreateQuestion("Open 2"), closed);

        var openCount = await _repository.GetCountAsync(isClosedFilter: false);
        var closedCount = await _repository.GetCountAsync(isClosedFilter: true);

        openCount.Should().Be(2);
        closedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCountAsync_ExcludesSoftDeletedQuestions()
    {
        var active = CreateQuestion("Active");
        var toDelete = CreateQuestion("To delete");
        await SeedAsync(active, toDelete);

        await _repository.SoftDeleteAsync(toDelete.Id);

        var count = await _repository.GetCountAsync();

        count.Should().Be(1);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsQuestion()
    {
        var q = CreateQuestion("Test question");
        await SeedAsync(q);

        var result = await _repository.GetByIdAsync(q.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test question");
    }

    [Fact]
    public async Task GetByIdAsync_WithSoftDeletedQuestion_ReturnsNull()
    {
        var q = CreateQuestion("Deleted question");
        await SeedAsync(q);

        await _repository.SoftDeleteAsync(q.Id);

        var result = await _repository.GetByIdAsync(q.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        result.Should().BeNull();
    }

    // ── AddAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WithExistingUsersAsNavigations_SavesTheQuestionWithoutAddingUsers()
    {
        // QuestionService.CreateAsync sets Author and AssignedMember (loaded elsewhere) for the audit log's names.
        var author = _context.Users.Single(u => u.Id == AuthorId);
        var member = _context.Users.Single(u => u.Id == MemberId);
        _context.ChangeTracker.Clear();
        var usersBefore = _context.Users.Count();
        var q = CreateQuestion("With navigations");
        q.Author = author;
        q.AssignedMember = member;

        await _repository.AddAsync(q);

        q.Id.Should().BeGreaterThan(0);
        (await _repository.GetByIdAsync(q.Id))!.Title.Should().Be("With navigations");
        _context.Users.Count().Should().Be(usersBefore, "the users already exist");
    }

    // ── GetUnansweredQuestionsForNotificationAsync ────────────────────────────

    [Fact]
    public async Task GetUnansweredQuestionsForNotification_ReturnsUnansweredAndInDiscussionNotAwaitingReply()
    {
        var unanswered = CreateQuestion("Unanswered");
        var answered = CreateQuestion("Answered");
        answered.MarkAsAnswered(); // IsAwaitingUserReply = true
        var inDiscussion = CreateQuestion("InDiscussion");
        inDiscussion.MarkAsInDiscussion(); // IsAwaitingUserReply = false
        var closed = CreateQuestion("Closed");
        closed.Close();
        await SeedAsync(unanswered, answered, inDiscussion, closed);

        var result = (await _repository.GetUnansweredQuestionsForNotificationAsync()).ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(q => q.Title == "Unanswered");
        result.Should().Contain(q => q.Title == "InDiscussion");
    }
}
