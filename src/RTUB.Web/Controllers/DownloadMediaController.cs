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
            // Set both FileName (ASCII-safe fallback for older browsers) and FileNameStar (RFC 5987 for full Unicode support)
            var contentDisposition = new ContentDispositionHeaderValue("attachment");
            
            // Check if filename contains non-ASCII characters
            var hasNonAsciiChars = !filename.All(char.IsAscii);
            
            // Try to set the filename
            try
            {
                if (hasNonAsciiChars)
                {
                    // For non-ASCII filenames, use ASCII-safe fallback in FileName and full filename in FileNameStar
                    // Replace non-ASCII characters with underscores for FileName fallback
                    var asciiSafeFilename = new string(filename.Select(c => char.IsAscii(c) ? c : '_').ToArray());
                    contentDisposition.FileName = asciiSafeFilename;
                    
                    // Set FileNameStar for full Unicode support (RFC 5987)
                    contentDisposition.FileNameStar = filename;
                }
                else
                {
                    // For ASCII-compatible filenames, set FileName directly (no need for FileNameStar per RFC 5987)
                    contentDisposition.FileName = filename;
                }
            }
            catch (ArgumentException ex)
            {
                // If setting FileName fails due to invalid characters, log and fallback to FileNameStar only
                _logger.LogWarning(ex, "Failed to set FileName for {Filename}, using FileNameStar fallback", filename);
                contentDisposition.FileNameStar = filename;
            }
            
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
