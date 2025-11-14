using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FFmpegMediaPlayer;

internal static class FFmpegBinariesHelper
{
    internal static void RegisterFFmpegBinaries()
    {
        var current = Environment.CurrentDirectory;

        var probe = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#if GODOT
            probe = "addons/FFmpegGodotMediaPlayer/libs/win-x64";
#else

#endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
#if GODOT
            probe = "addons/FFmpegGodotMediaPlayer/libs/linux-x64";
#else

#endif
        }
        else
        {
            FFmpegLogger.LogErr(typeof(FFmpegBinariesHelper), "Current platform is not supported!");
            return;
        }

        if (current != null && probe != string.Empty)
        {
            var ffmpegBinaryPath = Path.Combine(current, probe);

            if (Directory.Exists(ffmpegBinaryPath))
            {
                FFmpegLogger.Log(typeof(FFmpegBinariesHelper), $"FFmpeg binaries found in: {ffmpegBinaryPath}");

                foreach (var file in Directory.EnumerateFiles(ffmpegBinaryPath))
                {
                    if (NativeLibrary.TryLoad(file, out _))
                        FFmpegLogger.Log(typeof(FFmpegBinariesHelper), Path.GetFileName(file), " loaded");
                }
            }
        }
        else
            FFmpegLogger.LogErr(typeof(FFmpegBinariesHelper), "Cannot load FFmpeg shared library!");
    }
}