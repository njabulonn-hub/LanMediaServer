namespace MediaServer.Server.Utilities;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".mov"] = "video/quicktime",
        [".m4v"] = "video/x-m4v",
        [".mp3"] = "audio/mpeg",
        [".flac"] = "audio/flac",
        [".aac"] = "audio/aac",
        [".wav"] = "audio/wav",
        [".json"] = "application/json",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".m3u8"] = "application/vnd.apple.mpegurl",
        [".ts"] = "video/mp2t"
    };

    public static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrWhiteSpace(ext) && Map.TryGetValue(ext, out var type))
        {
            return type;
        }

        return "application/octet-stream";
    }
}
