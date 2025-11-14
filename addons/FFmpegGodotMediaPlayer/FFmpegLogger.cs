using System.Diagnostics;
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

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=green][{className}, {lineNumber}] {what}[/color]");
#else

#endif
    }

    public static void Log(object caller, params object[] what)
    {
        if (!FFmpegStatic.DebugLog)
            return;

        string className = FormatClassName(caller.GetType().Name);

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=green][{className}, {lineNumber}] ", string.Join(string.Empty, what), "[/color]");
#else

#endif
    }

    public static void LogWarn(object caller, string what)
    {
        string className = FormatClassName(caller.GetType().Name);

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=yellow][{className}, {lineNumber}] {what}[/color]");
#else

#endif
    }

    public static void LogWarn(object caller, params object[] what)
    {
        string className = FormatClassName(caller.GetType().Name);

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=yellow][{className}, {lineNumber}] ", string.Join(string.Empty, what), "[/color]");
#else

#endif
    }

    public static void LogErr(object caller, string what)
    {
        string className = FormatClassName(caller.GetType().Name);

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=red][{className}, {lineNumber}] {what}[/color]");
#else

#endif
    }

    public static void LogErr(object caller, params object[] what)
    {
        string className = FormatClassName(caller.GetType().Name);

        var stackFrame = new StackFrame(1, true);

        var lineNumber = stackFrame.GetFileLineNumber();

#if GODOT
        Godot.GD.PrintRich($"[color=red][{className}, {lineNumber}] ", string.Join(string.Empty, what), "[/color]");
#else

#endif
    }
}