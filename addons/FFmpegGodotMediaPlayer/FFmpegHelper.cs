using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace FFmpegMediaPlayer;

internal static unsafe class FFmpegHelper
{
    public static string AVStringError(int error)
    {
        var bufferSize = ffmpeg.AV_ERROR_MAX_STRING_SIZE;

        var buffer = stackalloc byte[bufferSize];

        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);

        var message = Marshal.PtrToStringAnsi((IntPtr)buffer);

        return message;
    }

    public static string GetChannelLayoutString(AVChannelLayout* chLayout)
    {
        var buffer = new byte[64];

        fixed (byte* bufferPtr = buffer)
        {
            var ret = ffmpeg.av_channel_layout_describe(chLayout, bufferPtr, 64);
            return ret >= 0 ? Marshal.PtrToStringAnsi((IntPtr)bufferPtr) ?? "unknown" : "unknown";
        }
    }
}