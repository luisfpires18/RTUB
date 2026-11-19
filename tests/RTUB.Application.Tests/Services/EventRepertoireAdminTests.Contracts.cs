using FluentAssertions;
using RTUB.Application.DTOs;

namespace RTUB.Application.Tests.Services;

// EventRepertoireAdminTests: Contracts tests.
public partial class EventRepertoireAdminTests
{
    [Fact]
    public void TheRepertoireContracts_CarryNoInternalField()
    {
        typeof(EventRepertoireItemDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Title");
        typeof(EventRepertoireSongDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Title", "Album");
    }
}
