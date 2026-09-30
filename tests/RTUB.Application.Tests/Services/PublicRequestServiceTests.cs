using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The public request submission (React track 003): the rules and side effects the retired Blazor
/// /request page had inline - annotations, date rules, create, optional range, one RTUB email.
/// </summary>
public class PublicRequestServiceTests
{
    private readonly Mock<IRequestService> _requests = new();
    private readonly Mock<IEmailNotificationService> _email = new();
    private readonly PublicRequestService _service;

    public PublicRequestServiceTests()
    {
        _requests
            .Setup(r => r.CreateRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Request { Id = 42, CreatedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc) });
        _service = new PublicRequestService(_requests.Object, new RequestValidationService(), _email.Object);
    }

    private static PublicRequestSubmission Valid(
        DateTime? date = null, bool range = false, DateTime? end = null, string email = "ana@example.com", string? message = "Olá") =>
        new("Ana", email, "912345678", "Serenata", date ?? DateTime.Today.AddDays(10), range, end, "Bragança", message);

    [Fact]
    public async Task ValidRequest_IsCreatedAndEmailedOnce()
    {
        var errors = await _service.SubmitAsync(Valid());

        errors.Should().BeEmpty();
        _requests.Verify(r => r.CreateRequestAsync("Ana", "ana@example.com", "912345678", "Serenata",
            DateTime.Today.AddDays(10), "Bragança", "Olá"), Times.Once);
        _requests.Verify(r => r.SetRequestDateRangeAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        _email.Verify(e => e.SendNewRequestNotificationAsync(42, "Ana", "ana@example.com", "912345678", "Serenata",
            DateTime.Today.AddDays(10), null, "Bragança", "Olá", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task DateRange_IsStoredOnTheCreatedRequest()
    {
        var end = DateTime.Today.AddDays(12);

        (await _service.SubmitAsync(Valid(range: true, end: end))).Should().BeEmpty();

        _requests.Verify(r => r.SetRequestDateRangeAsync(42, end), Times.Once);
    }

    [Fact]
    public async Task MissingMessage_IsStoredAsEmpty_BecauseTheColumnIsNotNull()
    {
        await _service.SubmitAsync(Valid(message: null));

        _requests.Verify(r => r.CreateRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), string.Empty), Times.Once);
    }

    [Theory]
    [InlineData("not-an-email", "Email")]
    [InlineData("", "Email")]
    public async Task InvalidField_IsReportedAndNothingHappens(string email, string field)
    {
        var errors = await _service.SubmitAsync(Valid(email: email));

        errors.Should().ContainKey(field);
        NothingHappened();
    }

    [Fact]
    public async Task PastDate_UsesTheExistingMessage()
    {
        var errors = await _service.SubmitAsync(Valid(date: DateTime.Today.AddDays(-1)));

        errors[nameof(Request.PreferredDate)].Should().Equal("A data não pode ser no passado.");
        NothingHappened();
    }

    [Fact]
    public async Task RangeWithoutEnd_IsRejected()
    {
        var errors = await _service.SubmitAsync(Valid(range: true, end: null));

        errors[nameof(Request.PreferredEndDate)].Should().Equal("A data de fim é obrigatória para intervalo de datas.");
        NothingHappened();
    }

    [Fact]
    public async Task EndBeforeStart_IsRejected()
    {
        var errors = await _service.SubmitAsync(Valid(date: DateTime.Today.AddDays(10), range: true, end: DateTime.Today.AddDays(5)));

        errors[nameof(Request.PreferredEndDate)].Should().Equal("A data de fim deve ser posterior ou igual à data de início.");
        NothingHappened();
    }

    [Fact]
    public async Task FieldAndDateErrors_ComeBackTogether()
    {
        var errors = await _service.SubmitAsync(Valid(email: "x", date: DateTime.Today.AddDays(-3)));

        errors.Keys.Should().Contain(new[] { "Email", nameof(Request.PreferredDate) });
    }

    [Fact]
    public async Task StorageFailure_PropagatesAndSendsNoEmail()
    {
        _requests
            .Setup(r => r.CreateRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await _service.Invoking(s => s.SubmitAsync(Valid())).Should().ThrowAsync<InvalidOperationException>();
        _email.VerifyNoOtherCalls();
    }

    private void NothingHappened()
    {
        _requests.VerifyNoOtherCalls();
        _email.VerifyNoOtherCalls();
    }
}
