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

        var arch = RuntimeInformation.ProcessArchitecture;

        var isX64 = arch == Architecture.X64;

        var extension = string.Empty;

        if (OperatingSystem.IsWindows() && isX64)
        {
            extension = ".dll";

#if GODOT
            probe = "addons/FFmpegGodotMediaPlayer/libs/win-x64";
#else

#endif
        }
        else if (OperatingSystem.IsLinux() && isX64)
        {
            extension = ".so";

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

        if (current != null)
        {
            var ffmpegBinaryPath = probe != string.Empty ? Path.Combine(current, probe) : current;

            if (Directory.Exists(ffmpegBinaryPath))
            {
                FFmpegLogger.Log(typeof(FFmpegBinariesHelper), $"FFmpeg binaries found in: {ffmpegBinaryPath}");

                foreach (var file in Directory.EnumerateFiles(ffmpegBinaryPath))
                {
                    if (
                        extension != string.Empty
                        && file.Contains(extension, StringComparison.OrdinalIgnoreCase)
                        && NativeLibrary.TryLoad(file, out _)
                    )
                        FFmpegLogger.Log(typeof(FFmpegBinariesHelper), file, " loaded");
                }
            }
        }
        else
            FFmpegLogger.LogErr(typeof(FFmpegBinariesHelper), "Cannot load FFmpeg shared library!");
    }
}