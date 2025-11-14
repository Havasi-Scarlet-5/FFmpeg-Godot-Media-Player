using Godot;

namespace FFmpegMediaPlayer.godot;

/// <summary>
/// Can be (video - audio) file or link.
/// </summary>
[GlobalClass, Icon("res://addons/FFmpegGodotMediaPlayer/godot/icons/source.svg")]
public partial class FFmpegGodotMediaSource : Resource
{
    /// <summary>
    /// Url or file path.
    /// </summary>
    [Export]
    public string Url { get; set; } = string.Empty;
}