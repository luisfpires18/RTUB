using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The files the gallery accepts. A gallery file is public-read, so the list is short (no SVG, no HTML), the first
/// bytes must agree with the declared type (<see cref="MediaSignature"/>), and the stored name takes its extension
/// from the type, never from the name the browser sent.
/// </summary>
public static class GalleryUploadTypes
{
    public const string Refused = "Formato de ficheiro não suportado. São aceites fotos JPEG, PNG, WebP ou GIF e vídeos MP4, MOV ou WebM.";

    /// <param name="Signature">What <see cref="MediaSignature.Sniff"/> must answer for the file's bytes.</param>
    public sealed record Accepted(MediaType Type, string Signature, string Extension);

    private static readonly Dictionary<string, Accepted> ByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = new(MediaType.Image, "image/jpeg", ".jpg"),
        ["image/png"] = new(MediaType.Image, "image/png", ".png"),
        ["image/webp"] = new(MediaType.Image, "image/webp", ".webp"),
        ["image/gif"] = new(MediaType.Image, "image/gif", ".gif"),
        ["video/mp4"] = new(MediaType.Video, "video/mp4", ".mp4"),
        ["video/quicktime"] = new(MediaType.Video, "video/mp4", ".mov"), // the same container as MP4
        ["video/webm"] = new(MediaType.Video, "video/webm", ".webm"),
    };

    /// <summary>The accepted type for this declared Content-Type and these bytes; null when the upload is refused.</summary>
    public static Accepted? Find(string? contentType, Stream content) =>
        ByContentType.GetValueOrDefault(contentType ?? string.Empty) is { } accepted && MediaSignature.Sniff(content) == accepted.Signature
            ? accepted
            : null;
}
