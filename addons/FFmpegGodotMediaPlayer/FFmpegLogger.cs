using System.Text.RegularExpressions;

namespace FFmpegMediaPlayer;

internal static partial class FFmpegLogger
{
    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex FFmpegLoggerRegex();

    private static string FormatClassName(string name)
    {
        return FFmpegLoggerRegex().Replace(name, " ");
    }

    public static void Log(object caller, string what)
    {
        if (!FFmpegStatic.DebugLog)
            return;

        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=green][{className}] {what}[/color]");
#else

#endif
    }

    public static void Log(object caller, params object[] what)
    {
        if (!FFmpegStatic.DebugLog)
            return;

        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=green][{className}] ", string.Join("", what), "[/color]");
#else

#endif
    }

    public static void LogWarn(object caller, string what)
    {
        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=yellow][{className}] {what}[/color]");
#else

#endif
    }

    public static void LogWarn(object caller, params object[] what)
    {
        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=yellow][{className}] ", string.Join("", what), "[/color]");
#else

#endif
    }

    public static void LogErr(object caller, string what)
    {
        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=red][{className}] {what}[/color]");
#else

#endif
    }

    public static void LogErr(object caller, params object[] what)
    {
        string className = FormatClassName(caller.GetType().Name);

#if GODOT
        Godot.GD.PrintRich($"[color=red][{className}] ", string.Join("", what), "[/color]");
#else

#endif
    }
}