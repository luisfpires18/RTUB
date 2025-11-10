using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Integration tests for API endpoints
/// </summary>
public class ApiEndpointsTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public ApiEndpointsTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Images API Tests







    #endregion

    #region API Response Headers Tests

    // Images API tests removed - ImagesController was deleted as part of R2 migration
    // Images are now served directly from Cloudflare R2

    #endregion

    #region Concurrent API Requests Tests



    #endregion

    #region Error Handling Tests




    #endregion
}
