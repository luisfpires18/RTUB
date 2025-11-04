using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Enums;

/// <summary>
/// Unit tests for RequestStatus enum
/// Tests status workflow logic
/// </summary>
public class RequestStatusTests
{
    [Fact]
    public void RequestStatus_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[]
        {
            RequestStatus.Pending,
            RequestStatus.Analysing,
            RequestStatus.Confirmed,
            RequestStatus.Rejected
        };

        // Act
        var actualValues = Enum.GetValues<RequestStatus>();

        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData("Pending", RequestStatus.Pending)]
    [InlineData("Analysing", RequestStatus.Analysing)]
    [InlineData("Confirmed", RequestStatus.Confirmed)]
    [InlineData("Rejected", RequestStatus.Rejected)]
    public void RequestStatus_CanParse(string value, RequestStatus expected)
    {
        // Act
        var result = Enum.Parse<RequestStatus>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RequestStatus_WorkflowOrder_IsCorrect()
    {
        // This test documents the expected workflow
        // Pending -> Analysing -> Confirmed/Rejected

        // Assert
        ((int)RequestStatus.Pending).Should().BeLessThan((int)RequestStatus.Analysing);
        ((int)RequestStatus.Analysing).Should().BeLessThan((int)RequestStatus.Confirmed);
        ((int)RequestStatus.Analysing).Should().BeLessThan((int)RequestStatus.Rejected);
    }

    [Fact]
    public void RequestStatus_Count_IsFour()
    {
        // Act
        var count = Enum.GetValues<RequestStatus>().Length;

        // Assert
        count.Should().Be(4);
    }
}
