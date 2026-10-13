using FluentAssertions;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The board-folder sanitizer that keeps a board name from leaving docs/{env}/{year}/Logistics/ (moved here in 042 from
/// the removed LogisticsCardService, whose tests covered it through an upload).
/// </summary>
public class LogisticsKanbanServiceTests
{
    [Theory]
    [InlineData("Test Board", "Test Board")]
    [InlineData("Test/Board..Name", "TestBoardName")]
    [InlineData("../../etc/passwd", "etcpasswd")]
    [InlineData(@"..\..\Windows", "Windows")]
    [InlineData("Arraial São João", "Arraial São João")]
    [InlineData("../..", "unnamed")]
    [InlineData("   ", "")]
    public void SanitizePathComponent_KeepsTheBoardNameInsideItsFolder(string boardName, string expected)
    {
        LogisticsKanbanService.SanitizePathComponent(boardName).Should().Be(expected);
    }
}
