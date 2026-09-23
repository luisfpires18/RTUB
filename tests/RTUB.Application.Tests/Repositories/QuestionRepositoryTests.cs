using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Tests for QuestionRepository — focuses on server-side pagination and filtering
/// (verifying the fix for the client-side pagination anti-pattern).
/// </summary>
public class QuestionRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly QuestionRepository _repository;

    private const string AuthorId = "author-001";
    private const string MemberId = "member-001";
    private const string OtherMemberId = "member-002";

    public QuestionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        SeedUsersAsync().GetAwaiter().GetResult();
        _repository = new QuestionRepository(_fixture.CreateContextFactory());
    }

    private async Task SeedUsersAsync()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var users = new[]
        {
            new ApplicationUser { Id = AuthorId, UserName = "testauthor", NormalizedUserName = "TESTAUTHOR", Email = "author@test.com", SecurityStamp = Guid.NewGuid().ToString(), FirstName = "Test", LastName = "Author", Nickname = "testauthor" },
            new ApplicationUser { Id = MemberId, UserName = "testmember", NormalizedUserName = "TESTMEMBER", Email = "member@test.com", SecurityStamp = Guid.NewGuid().ToString(), FirstName = "Test", LastName = "Member", Nickname = "testmember" },
            new ApplicationUser { Id = OtherMemberId, UserName = "othermember", NormalizedUserName = "OTHERMEMBER", Email = "other@test.com", SecurityStamp = Guid.NewGuid().ToString(), FirstName = "Other", LastName = "Member", Nickname = "othermember" },
        };
        foreach (var u in users)
            u.PasswordHash = hasher.HashPassword(u, TestSecret.NewPassword());

        // Only seed if not already present (fixture is shared across test methods)
        var existingIds = _context.Users.Select(u => u.Id).ToHashSet();
        foreach (var u in users.Where(u => !existingIds.Contains(u.Id)))
            _context.Users.Add(u);

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Question CreateQuestion(string title, string assignedMemberId = MemberId, DateTime? lastActivityAt = null)
    {
        var q = Question.Create(title, "Content that is at least 10 chars.", AuthorId, Position.Magister, assignedMemberId);
        if (lastActivityAt.HasValue)
            q.LastActivityAt = lastActivityAt.Value;
        return q;
    }

    private async Task SeedAsync(params Question[] questions)
    {
        _context.Questions.AddRange(questions);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task SeedQuestionsAsync(int count, string assignedMemberId = MemberId)
    {
        for (var i = 1; i <= count; i++)
        {
            _context.Questions.Add(CreateQuestion($"Question {i:D3}", assignedMemberId,
                lastActivityAt: DateTime.UtcNow.AddMinutes(-i)));
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    // ── Pagination ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithMoreItemsThanPageSize_ReturnsOnlyPageSize()
    {
        await SeedQuestionsAsync(50);

        var result = await _repository.GetAllAsync(page: 1, pageSize: 10);

        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetAllAsync_Page2_ReturnsSecondPage()
    {
        await SeedQuestionsAsync(25);

        var page1 = (await _repository.GetAllAsync(page: 1, pageSize: 10)).ToList();
        var page2 = (await _repository.GetAllAsync(page: 2, pageSize: 10)).ToList();

        page2.Should().HaveCount(10);
        var page1Ids = page1.Select(q => q.Id).ToHashSet();
        page2.Should().NotContain(q => page1Ids.Contains(q.Id));
    }

    [Fact]
    public async Task GetAllAsync_LastPage_ReturnsRemainingItems()
    {
        await SeedQuestionsAsync(23);

        var lastPage = await _repository.GetAllAsync(page: 3, pageSize: 10);

        lastPage.Should().HaveCount(3); // 23 - 20 = 3
    }

    [Fact]
    public async Task GetAllAsync_PageBeyondTotal_ReturnsEmpty()
    {
        await SeedQuestionsAsync(5);

        var result = await _repository.GetAllAsync(page: 2, pageSize: 10);

        result.Should().BeEmpty();
    }

    // ── Ordering by LastActivityAt ────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_OrdersByLastActivityAtDescending()
    {
        var now = DateTime.UtcNow;
        await SeedAsync(
            CreateQuestion("Oldest", lastActivityAt: now.AddHours(-3)),
            CreateQuestion("Newest", lastActivityAt: now),
            CreateQuestion("Middle", lastActivityAt: now.AddHours(-1)));

        var result = (await _repository.GetAllAsync(page: 1, pageSize: 10)).ToList();

        result[0].Title.Should().Be("Newest");
        result[1].Title.Should().Be("Middle");
        result[2].Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetAllAsync_PaginationPreservesOrderingAcrossPages()
    {
        var now = DateTime.UtcNow;
        for (var i = 1; i <= 15; i++)
            _context.Questions.Add(CreateQuestion($"Q{i:D2}", lastActivityAt: now.AddMinutes(-i)));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page1 = (await _repository.GetAllAsync(page: 1, pageSize: 5)).ToList();
        var page2 = (await _repository.GetAllAsync(page: 2, pageSize: 5)).ToList();
        var page3 = (await _repository.GetAllAsync(page: 3, pageSize: 5)).ToList();

        page1.Select(q => q.Title).Should().BeEquivalentTo(["Q01", "Q02", "Q03", "Q04", "Q05"]);
        page3.Select(q => q.Title).Should().BeEquivalentTo(["Q11", "Q12", "Q13", "Q14", "Q15"]);
    }

    // ── Status Filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithIsClosedFilterTrue_ReturnsOnlyClosedQuestions()
    {
        var open = CreateQuestion("Open question");
        var closed = CreateQuestion("Closed question");
        closed.Close();
        await SeedAsync(open, closed);

        var result = (await _repository.GetAllAsync(1, 10, isClosedFilter: true)).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Closed question");
    }

    [Fact]
    public async Task GetAllAsync_WithIsClosedFilterFalse_ExcludesClosedQuestions()
    {
        var open = CreateQuestion("Open question");
        var closed = CreateQuestion("Closed question");
        closed.Close();
        await SeedAsync(open, closed);

        var result = (await _repository.GetAllAsync(1, 10, isClosedFilter: false)).ToList();

        result.Should().NotContain(q => q.Status == QuestionStatus.Closed);
    }

    [Fact]
    public async Task GetAllAsync_WithNoStatusFilter_ReturnsAllStatuses()
    {
        var open = CreateQuestion("Open");
        var closed = CreateQuestion("Closed");
        closed.Close();
        await SeedAsync(open, closed);

        var result = await _repository.GetAllAsync(1, 10);

        result.Should().HaveCount(2);
    }

    // ── Assigned Member Filter ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithAssignedMemberFilter_ReturnsOnlyMatchingMemberQuestions()
    {
        await SeedAsync(
            CreateQuestion("For member-001", MemberId),
            CreateQuestion("For member-002", OtherMemberId));

        var result = await _repository.GetAllAsync(1, 10, assignedMemberIdFilter: MemberId);

        result.Should().HaveCount(1);
        result.First().AssignedMemberId.Should().Be(MemberId);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithSearchTerm_FiltersByTitle()
    {
        await SeedAsync(
            CreateQuestion("Blazor performance question"),
            CreateQuestion("EF Core migration issue"));

        var result = await _repository.GetAllAsync(1, 10, searchTerm: "blazor");

        result.Should().HaveCount(1);
        result.First().Title.Should().Contain("Blazor");
    }

    [Fact]
    public async Task GetAllAsync_WithSearchTermNoMatch_ReturnsEmpty()
    {
        await SeedAsync(CreateQuestion("Some question title"));

        var result = await _repository.GetAllAsync(1, 10, searchTerm: "zzzznotfound");

        result.Should().BeEmpty();
    }

    // ── Soft Delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ExcludesSoftDeletedQuestions()
    {
        var active = CreateQuestion("Active question");
        var toDelete = CreateQuestion("Deleted question");
        await SeedAsync(active, toDelete);

        await _repository.SoftDeleteAsync(toDelete.Id);

        var result = await _repository.GetAllAsync(1, 10);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Active question");
    }

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

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
