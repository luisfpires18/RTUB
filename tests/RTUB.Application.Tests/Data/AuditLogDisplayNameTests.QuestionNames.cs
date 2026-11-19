using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

// AuditLogDisplayNameTests: Question Names tests.
public partial class AuditLogDisplayNameTests
{
    [Fact]
    public async Task Question_Created_HasTitleDisplayName()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var author = new ApplicationUser
        {
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            Nickname = "Jeans"
        };
        _context.Users.Add(author);

        var assignedMember = new ApplicationUser
        {
            UserName = "malelo",
            Email = "malelo@example.com",
            FirstName = "Male",
            LastName = "Lo",
            Nickname = "Malelo"
        };
        _context.Users.Add(assignedMember);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create Question with navigation properties loaded
        var question = Question.Create(
            "Test Question Title",
            "This is a test question content",
            author.Id,
            Position.Magister,
            assignedMember.Id);
        question.Author = author;
        question.AssignedMember = assignedMember;
        _context.Questions.Add(question);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "Question" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().Be("Test Question Title", "should show the question title");
    }

    [Fact]
    public async Task Question_Created_ResolvesUserIdsToNicknames_InChangesJson()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var author = new ApplicationUser
        {
            UserName = "author_user",
            Email = "author@example.com",
            FirstName = "Author",
            LastName = "User",
            Nickname = "AuthorNick"
        };
        _context.Users.Add(author);

        var assignedMember = new ApplicationUser
        {
            UserName = "assigned_user",
            Email = "assigned@example.com",
            FirstName = "Assigned",
            LastName = "User",
            Nickname = "AssignedNick"
        };
        _context.Users.Add(assignedMember);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create Question with navigation properties loaded
        var question = Question.Create(
            "Another Test Title",
            "This is a test question content",
            author.Id,
            Position.ViceMagister,
            assignedMember.Id);
        question.Author = author;
        question.AssignedMember = assignedMember;
        _context.Questions.Add(question);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "Question" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.Changes.Should().NotBeNull();
        // The AuthorId and AssignedMemberId should be resolved to nicknames
        auditLog.Changes.Should().Contain("AuthorNick", "AuthorId should be resolved to nickname");
        auditLog.Changes.Should().Contain("AssignedNick", "AssignedMemberId should be resolved to nickname");
        // Should NOT contain the raw user IDs
        auditLog.Changes.Should().NotContain(author.Id, "AuthorId should not contain raw user ID");
        auditLog.Changes.Should().NotContain(assignedMember.Id, "AssignedMemberId should not contain raw user ID");
    }
}
