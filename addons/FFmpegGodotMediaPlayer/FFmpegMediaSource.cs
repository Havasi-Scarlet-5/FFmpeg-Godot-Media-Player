namespace FFmpegMediaPlayer;

/// <summary>
/// Can be (video - audio) file or link.
/// </summary>
public class FFmpegMediaSource
{
    /// <summary>
    /// Url or file path.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// File buffer in bytes.
    /// </summary>
    public byte[] Buffer { get; set; } = [];
}