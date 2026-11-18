namespace RTUB.Application.Helpers;

/// <summary>
/// What an uploaded file is, read from its first bytes. The Content-Type a browser sends is the caller's word: a file
/// that will be served to other people must also look like what it says.
/// </summary>
public static class MediaSignature
{
    /// <summary>
    /// "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4" (MP4 and QuickTime: the same container) or
    /// "video/webm"; null for anything else, and for a stream that cannot rewind. The stream is left at its start.
    /// </summary>
    public static string? Sniff(Stream content)
    {
        if (!content.CanSeek)
        {
            return null;
        }

        var head = new byte[12]; // a shorter file leaves zeros, which match nothing
        content.Position = 0;
        content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        content.Position = 0;

        return head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF ? "image/jpeg"
            : Ascii(head, 1, "PNG") && head[0] == 0x89 ? "image/png"
            : Ascii(head, 0, "RIFF") && Ascii(head, 8, "WEBP") ? "image/webp"
            : Ascii(head, 0, "GIF8") ? "image/gif"
            // ISO media: a 4-byte size, then the first box. "ftyp" today; QuickTime also wrote the others first.
            : Ascii(head, 4, "ftyp") || Ascii(head, 4, "moov") || Ascii(head, 4, "mdat") || Ascii(head, 4, "wide") || Ascii(head, 4, "free") ? "video/mp4"
            : head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3 ? "video/webm"
            : null;
    }

    private static bool Ascii(byte[] head, int at, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (head[at + i] != text[i])
            {
                return false;
            }
        }

        return true;
    }
}
