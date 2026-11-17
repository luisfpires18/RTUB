using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

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
