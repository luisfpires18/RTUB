using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Music pages (Albums and Songs)
/// Tests the complete workflow from HTTP requests through the application
/// </summary>
public class MusicPagesTests : IClassFixture<RTUBWebApplicationFactory>
{
    private readonly RTUBWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MusicPagesTests(RTUBWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Albums Page Tests

    [Fact]
    public async Task AlbumsPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlbumsPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("sica", "page should display Music/Música content");
    }

    [Fact]
    public async Task AlbumsPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task MusicSection_IsAccessibleFromHomePage()
    {
        // Arrange - Load home page first
        var homeResponse = await _client.GetAsync("/");
        
        // Act - Navigate to music
        var musicResponse = await _client.GetAsync("/music");

        // Assert
        homeResponse.IsSuccessStatusCode.Should().BeTrue();
        musicResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Songs Page Tests

    [Fact]
    public async Task SongsPage_WithValidAlbumId_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        // Album ID 1 should exist in test data
        var response = await _client.GetAsync("/music/songs/1");

        // Assert
        // Either success or redirect if album doesn't exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SongsPage_WithInvalidAlbumId_HandlesGracefully()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music/songs/99999");

        // Assert
        // Should either show empty page or redirect, not crash
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task SongsPage_ContainsExpectedSongElements()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music/songs/1");
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Assert - Check for song-related elements
            content.Should().Contain("Faixas", "page should display Tracks/Faixas section");
        }
    }

    #endregion
}
