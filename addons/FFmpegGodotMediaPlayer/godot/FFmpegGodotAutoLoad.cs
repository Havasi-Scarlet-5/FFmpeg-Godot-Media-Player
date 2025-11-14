using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using Godot;

namespace FFmpegMediaPlayer.godot;

internal sealed partial class FFmpegGodotAutoLoad : Node
{
    public static ResourcePreloader Preloader { get; private set; } = null;

    public override void _Ready()
    {
        InitializeFFmpegLibrary();

        Preloader = new ResourcePreloader();

        PreloadShaders();
    }

    public static void InitializeFFmpegLibrary()
    {
        FFmpegBinariesHelper.RegisterFFmpegBinaries();
        DynamicallyLoadedBindings.Initialize();
    }

    private static void PreloadShaders()
    {
        Preloader.AddResource("YUVToRGB", ResourceLoader.Load("res://addons/FFmpegGodotMediaPlayer/godot/shaders/yuv_to_rgb.gdshader"));
    }
}