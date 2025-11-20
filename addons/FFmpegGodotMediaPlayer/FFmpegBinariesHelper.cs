using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;

namespace FFmpegMediaPlayer;

internal static class FFmpegBinariesHelper
{
    internal static void RegisterFFmpegBinaries()
    {
        var current = Environment.CurrentDirectory;

        var probe = string.Empty;

        var arch = RuntimeInformation.ProcessArchitecture;

        if (OperatingSystem.IsWindows() && arch == Architecture.X64)
        {
#if GODOT
            probe = "addons/FFmpegGodotMediaPlayer/libs/win-x64";
#else

#endif
        }
        else if (OperatingSystem.IsLinux() && arch == Architecture.X64)
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

        var ffmpegBinaryPath = probe != string.Empty ? Path.Combine(current, probe) : current;

#if GODOT
        if (!Godot.OS.HasFeature("editor"))
            ffmpegBinaryPath = current;
#endif

        if (Directory.Exists(ffmpegBinaryPath))
        {
            FFmpegLogger.Log(typeof(FFmpegBinariesHelper), $"FFmpeg binaries found in: {ffmpegBinaryPath}");
            DynamicallyLoadedBindings.LibrariesPath = ffmpegBinaryPath;
        }
        else
            FFmpegLogger.LogErr(typeof(FFmpegBinariesHelper), "Cannot load FFmpeg shared library!");
    }
}