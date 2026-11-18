// NominatimGeocodingService: how many search requests one lookup sends.
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// One lookup is one Nominatim search. Only a failed request (server error, network) is tried once more; "nothing
/// found" is an answer, so it is not asked again. A fake handler answers and counts the requests.
/// </summary>
public class NominatimGeocodingRequestTests
{
    private const string OneTown = """[{"lat":"41.5","lon":"-6.9","display_name":"Mirandela, Portugal","class":"place","type":"town","importance":0.5}]""";

    [Fact]
    public async Task NothingFound_SendsOneRequest()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, "[]");

        var result = await Service(handler).GetCoordinatesAsync("Lugar Que Não Existe");

        result.Should().BeNull();
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task Found_SendsOneRequest()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, OneTown);

        var result = await Service(handler).GetCoordinatesAsync("Mirandela");

        result.Should().Be((41.5, -6.9));
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task AFailedRequest_IsTriedOnceMore()
    {
        var handler = new CountingHandler((HttpStatusCode.ServiceUnavailable, ""), (HttpStatusCode.OK, OneTown));

        var result = await Service(handler).GetCoordinatesAsync("Mirandela");

        result.Should().Be((41.5, -6.9));
        handler.Requests.Should().Be(2);
    }

    [Fact]
    public async Task TwoFailedRequests_GiveUp()
    {
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable, "");

        var result = await Service(handler).GetCoordinatesAsync("Mirandela");

        result.Should().BeNull();
        handler.Requests.Should().Be(2);
    }

    private static NominatimGeocodingService Service(CountingHandler handler)
    {
        var http = new Mock<IHttpClientFactory>();
        http.Setup(f => f.CreateClient("Nominatim")).Returns(() => new HttpClient(handler));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GeocodingRequests_" + Guid.NewGuid()).Options;
        var contexts = new Mock<IDbContextFactory<ApplicationDbContext>>();
        contexts.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(
            options, new Mock<IHttpContextAccessor>().Object, new AuditContext(), new AuditLogAppender()));

        return new NominatimGeocodingService(http.Object, new Mock<ILogger<NominatimGeocodingService>>().Object,
            contexts.Object, new ConfigurationBuilder().Build());
    }

    /// <summary>Answers each request with the next queued response (the last one repeats) and counts them.</summary>
    private sealed class CountingHandler(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        public CountingHandler(HttpStatusCode status, string body) : this((status, body)) { }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = answers[Math.Min(Requests, answers.Length - 1)];
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
