using System;
using System.Linq;
using Godot;

namespace FFmpegMediaPlayer.godot;

#if TOOLS

[Tool]
public partial class FFmpegGodotPlugin : EditorPlugin
{
    private FFmpegGodotImportPlugin _importPlugin = null;

    private FFmpegGodotExportPlugin _exportPlugin = null;

    public override void _EnterTree()
    {
        AddAutoloadSingleton("FFmpegGodotAutoLoad", "res://addons/FFmpegGodotMediaPlayer/godot/FFmpegGodotAutoLoad.cs");

        _importPlugin = new FFmpegGodotImportPlugin();
        AddImportPlugin(_importPlugin);

        _exportPlugin = new FFmpegGodotExportPlugin();
        AddExportPlugin(_exportPlugin);
    }

    public override void _ExitTree()
    {
        RemoveExportPlugin(_exportPlugin);
        _exportPlugin = null;

        RemoveImportPlugin(_importPlugin);
        _importPlugin = null;

        RemoveAutoloadSingleton("FFmpegGodotAutoLoad");
    }
}

public partial class FFmpegGodotImportPlugin : EditorImportPlugin
{
    public override string _GetImporterName()
    {
        return "ffmpeg.godot.media.source";
    }

    public override string _GetVisibleName()
    {
        return "FFmpeg Godot Media Source";
    }

    public override int _GetFormatVersion()
    {
        return 0;
    }

    public override int _GetImportOrder()
    {
        return 0;
    }

    public override float _GetPriority()
    {
        return 1.0f;
    }

    public override string[] _GetRecognizedExtensions()
    {
        return FFmpegStatic.RecognizedVideoExtensions;
    }

    public override string _GetSaveExtension()
    {
        return "res";
    }

    public override string _GetResourceType()
    {
        return "Resource";
    }

    public override int _GetPresetCount()
    {
        return 0;
    }

    public override string _GetPresetName(int presetIndex)
    {
        return "";
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetImportOptions(string path, int presetIndex)
    {
        return [];
    }

    public override Error _Import(string sourceFile, string savePath, Godot.Collections.Dictionary options, Godot.Collections.Array<string> platformVariants, Godot.Collections.Array<string> genFiles)
    {
        if (!FileAccess.FileExists(sourceFile))
            return Error.Failed;

        var source = new FFmpegGodotMediaSource() { Url = sourceFile };

        string filename = $"{savePath}.{_GetSaveExtension()}";

        return ResourceSaver.Save(source, filename);
    }
}

public partial class FFmpegGodotExportPlugin : EditorExportPlugin
{
    public override string _GetName()
    {
        return "FFmpegGodotExportPlugin";
    }

    public override bool _SupportsPlatform(EditorExportPlatform platform)
    {
        if (platform is EditorExportPlatformPC)
            return true;

        return false;
    }

    private void AddSharedLibrary(string sharedLibraryPath, string outputPath)
    {
        foreach (string directory in DirAccess.GetDirectoriesAt(sharedLibraryPath))
        {
            var fullPath = sharedLibraryPath + "/" + directory;

            AddSharedObject(fullPath, [], outputPath);

            FFmpegLogger.Log(this, $"{fullPath} added");
        }

        foreach (string file in DirAccess.GetFilesAt(sharedLibraryPath))
        {
            var fullPath = sharedLibraryPath + "/" + file;

            AddSharedObject(fullPath, [], outputPath);

            FFmpegLogger.Log(this, $"{fullPath} added");
        }
    }

    public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
    {
        FFmpegLogger.Log(this, "Features: [" + string.Join(", ", features) + "]");

        var sharedLibraryPath = "res://addons/FFmpegGodotMediaPlayer/libs/";

        if (features.Contains("windows") && features.Contains("x86_64"))
            AddSharedLibrary(sharedLibraryPath + "win-x64", string.Empty);
        else if (features.Contains("linux") && features.Contains("x86_64"))
            AddSharedLibrary(sharedLibraryPath + "linux-x64", string.Empty);
        else
            FFmpegLogger.LogErr(this, "Current platform is not supported!");
    }

    public override void _ExportFile(string path, string type, string[] features)
    {
        foreach (var extension in FFmpegStatic.RecognizedVideoExtensions)
            if (path.GetExtension().Equals(extension, StringComparison.OrdinalIgnoreCase))
                AddFile(path, FileAccess.GetFileAsBytes(path), false);
    }

    public override void _ExportEnd()
    {
    }
}

#endif