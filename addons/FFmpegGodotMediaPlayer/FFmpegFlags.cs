namespace FFmpegMediaPlayer;

/// <summary>
/// Missing Flags from FFmpeg c# binding
/// </summary>
internal static class FFmpegFlags
{
    public const int AV_BUFFERSRC_FLAG_NO_CHECK_FORMAT = 1;

    public const int AV_BUFFERSRC_FLAG_PUSH = 4;

    public const int AV_BUFFERSRC_FLAG_KEEP_REF = 8;

    public const int SEEK_SET = 0;

    public const int SEEK_CUR = 1;

    public const int SEEK_END = 2;
}