// HallOfFameEmptyApiTests: test class moved here from MemberAreaApiTests.cs.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// The Hall of Fame on a database with only the seeded Owner and the signed-in member: every record is there, with no
/// winner and no value (the page says "sem dados").
/// </summary>
public class HallOfFameEmptyApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public HallOfFameEmptyApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryRecord_IsThere_EmptyWhenNobodyQualifies()
    {
        var (member, _) = await CookieTestSession.SignInAsync(_factory, $"hof{Guid.NewGuid():N}"[..20], "10.51.0.1");

        var records = (await AreaHttp.Json(member, "/api/hall-of-fame")).GetProperty("categories").EnumerateArray().ToList();

        records.Should().HaveCount(12);
        foreach (var record in records)
        {
            record.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            record.GetProperty("winners").GetArrayLength().Should().Be(0, record.GetProperty("key").GetString());
            record.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }
}
