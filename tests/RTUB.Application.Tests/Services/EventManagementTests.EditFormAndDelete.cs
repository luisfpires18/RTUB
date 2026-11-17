using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventManagementTests: Edit Form And Delete tests.
public partial class EventManagementTests
{
    // ---------- edit ----------

    [Fact]
    public async Task EditForm_LoadsTheStoredValues()
    {
        var single = AddEvent("Serenata", new DateTime(2031, 5, 3, 21, 30, 0), description: "nota", imageUrl: "https://pub-test.r2.dev/a.webp");
        var multi = AddEvent("Festival", new DateTime(2031, 6, 1), endDate: new DateTime(2031, 6, 3), type: EventType.Festival);

        var one = (await _service.GetEventForEditAsync(single, Owner)).Value!;
        var many = (await _service.GetEventForEditAsync(multi, Admin)).Value!;

        one.Should().BeEquivalentTo(new EventEditDto(single, "Serenata", "2031-05-03", "21:30", null, "Bragança", "Atuacao", "nota", true,
            "https://pub-test.r2.dev/a.webp"));
        many.Time.Should().BeNull();
        many.EndDate.Should().Be("2031-06-03");
        many.Type.Should().Be("Festival");
        (await _service.GetEventForEditAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
    }

    // ---------- delete ----------

    [Fact]
    public async Task Delete_GoesThroughTheExistingService()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.DeleteEventAsync(id, Owner)).Status.Should().Be(EventResultStatus.Ok);

        _events.Verify(s => s.DeleteEventAsync(id, default), Times.Once);
        (await _service.GetEventAsync(id, Owner)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_IsRefused_WhenNerbaOrdersPointAtTheEvent()
    {
        var id = AddEvent("NERBA", _future, type: EventType.Nerba);
        using (var db = _db.CreateContext())
        {
            db.NerbaOrders.Add(new NerbaOrder { Item = "Bifanas", Stock = 1, PricePerUnit = 2, EventId = id, ReportId = 1 });
            db.SaveChanges();
        }

        (await _service.DeleteEventAsync(id, Admin)).Status.Should().Be(EventResultStatus.InUse);
        _events.Verify(s => s.DeleteEventAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Delete_OfAMissingEvent_IsNotFound()
    {
        (await _service.DeleteEventAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public void ManagementContracts_CarryNoInternalField()
    {
        typeof(EventEditDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "Name", "Date", "Time", "EndDate", "Location", "Type", "Description", "HasImage", "ImageUrl", "MembersOnly");
        typeof(EventInput).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Name", "Date", "Time", "EndDate", "Location", "Type", "Description", "MembersOnly");
    }
}
