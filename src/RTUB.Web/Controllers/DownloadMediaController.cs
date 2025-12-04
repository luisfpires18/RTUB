using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace RTUB.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DownloadMediaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DownloadMediaController> _logger;

    public DownloadMediaController(
        IHttpClientFactory httpClientFactory,
        ILogger<DownloadMediaController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Download([FromQuery] string url, [FromQuery] string filename)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("URL is required");
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            return BadRequest("Filename is required");
        }

        try
        {
            // Fetch file from Cloudflare R2 (or any other URL) server-side
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download file from {Url}. Status: {Status}", url, response.StatusCode);
                return NotFound("File not found");
            }

            // Get content type, default to octet-stream if not specified
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            // Set Content-Disposition header to force download with proper RFC 5987 encoding
            // Use FileNameStar for non-ASCII characters according to RFC 5987
            var contentDisposition = new ContentDispositionHeaderValue("attachment");
            contentDisposition.FileNameStar = filename;
            Response.Headers["Content-Disposition"] = contentDisposition.ToString();

            // Stream file content to client
            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, contentType, filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from {Url}", url);
            return StatusCode(500, "Error downloading file");
        }
    }
}
