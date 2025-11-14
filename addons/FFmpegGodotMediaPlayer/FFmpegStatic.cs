namespace FFmpegMediaPlayer;

internal static class FFmpegStatic
{
    public static readonly string[] RecognizedVideoExtensions = ["mp4", "webm", "mpg", "mpeg", "mkv", "avi", "mov", "wmv", "ogv"];

    public static readonly string[] RecognizedAudioExtensions = ["mp3", "ogg", "wav", "flac"];

    public static bool DebugLog = true;
}