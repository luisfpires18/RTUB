using Bunit;
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Tests.Pages.Base;
using RequestPage = RTUB.Pages.Public.Request;

namespace RTUB.Web.Tests.Pages.Public;

/// <summary>
/// The Blazor /request page after React track 003: same form and messages, but submission goes
/// through the shared <see cref="IPublicRequestService"/> (also behind POST /api/public/requests).
/// </summary>
public class RequestPageTests : PageTestBase
{
    private readonly Mock<IPublicRequestService> _requests;

    public RequestPageTests()
    {
        _requests = SetupService<IPublicRequestService>();
        SetupService<ILabelService>()
            .Setup(l => l.GetLabelByReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((Label?)null);
        SetupUnauthenticated();
    }

    private IRenderedComponent<RequestPage> RenderFilled()
    {
        var cut = Render<RequestPage>();
        cut.Find("#name").Change("Ana");
        cut.Find("#email").Change("ana@example.com");
        cut.Find("#phone").Change("912345678");
        cut.Find("#eventType").Change("Serenata");
        cut.Find("#location").Change("Bragança");
        return cut;
    }

    [Fact]
    public void Submit_GoesThroughTheSharedService_AndShowsSuccess()
    {
        _requests.Setup(r => r.SubmitAsync(It.IsAny<PublicRequestSubmission>()))
            .ReturnsAsync(new Dictionary<string, string[]>());
        var cut = RenderFilled();

        cut.Find("form").Submit();

        _requests.Verify(r => r.SubmitAsync(It.Is<PublicRequestSubmission>(s =>
            s.Name == "Ana" && s.Email == "ana@example.com" && s.Phone == "912345678" &&
            s.EventType == "Serenata" && s.Location == "Bragança" && !s.IsDateRange &&
            s.PreferredDate == DateTime.Today.AddDays(1))), Times.Once);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("O seu pedido foi submetido com sucesso"));
    }

    [Fact]
    public void DateErrorFromTheService_IsShownUnderTheDate_AndNotAsSuccess()
    {
        _requests.Setup(r => r.SubmitAsync(It.IsAny<PublicRequestSubmission>()))
            .ReturnsAsync(new Dictionary<string, string[]> { [nameof(Request.PreferredDate)] = new[] { "A data não pode ser no passado." } });
        var cut = RenderFilled();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("A data não pode ser no passado."));
        cut.Markup.Should().NotContain("submetido com sucesso");
    }

    [Fact]
    public void ServiceFailure_ShowsTheGenericError()
    {
        _requests.Setup(r => r.SubmitAsync(It.IsAny<PublicRequestSubmission>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var cut = RenderFilled();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ocorreu um erro ao submeter o seu pedido"));
        cut.Markup.Should().NotContain("db down");
    }
}
