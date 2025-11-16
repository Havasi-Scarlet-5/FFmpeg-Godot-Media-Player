using System.Collections.Generic;
using Godot;

namespace FFmpegMediaPlayer.godot.scenes.windows;

public partial class SettingsWindow : Window
{
    public FFmpegGodotMediaPlayer Player = null;

    [ExportCategory("Video")]

    [Export]
    private CheckBox _disableVideoCheckBox = null;

    [Export]
    private CheckBox _canSkipFramesCheckBox = null;

    [Export]
    private CheckBox _seekAsyncCheckBox = null;

    [Export]
    private CheckBox _stetchCheckBox = null;

    [Export]
    private ColorPicker _colorPicker = null;

    [Export]
    private Label _hueLabel = null;

    [Export]
    private HSlider _hueSlider = null;

    [Export]
    private Button _hueResetButton = null;

    [Export]
    private Label _saturationLabel = null;

    [Export]
    private HSlider _saturationSlider = null;

    [Export]
    private Button _saturationResetButton = null;

    [Export]
    private Label _lightnessLabel = null;

    [Export]
    private HSlider _lightnessSlider = null;

    [Export]
    private Button _lightnessResetButton = null;

    [Export]
    private Label _contrastLabel = null;

    [Export]
    private HSlider _contrastSlider = null;

    [Export]
    private Button _contrastResetButton = null;

    [Export]
    private CheckBox _chromaKeyEnableCheckBox = null;

    [Export]
    private ColorPicker _chromaKeyColorPicker = null;

    [Export]
    private Label _chromaKeyThresholdLabel = null;

    [Export]
    private HSlider _chromaKeyThresholdSlider = null;

    [Export]
    private Label _chromaKeySmoothnessLabel = null;

    [Export]
    private HSlider _chromaKeySmoothnessSlider = null;

    [ExportCategory("Audio")]

    [Export]
    private CheckBox _disableAudioCheckBox = null;

    [Export]
    private Label _bufferLengthLabel = null;

    [Export]
    private Slider _bufferLengthSlider = null;

    [Export]
    private Label _pitchLabel = null;

    [Export]
    private Slider _pitchSlider = null;

    [Export]
    private Label _volumeLabel = null;

    [Export]
    private TextureRect _volumeIcon = null;

    [Export]
    private Texture2D _volumeNormalIcon = null;

    [Export]
    private Texture2D _volumeMuteIcon = null;

    [Export]
    private Button _volumeMuteButton = null;

    [Export]
    private HSlider _volumeSlider = null;

    [ExportCategory("Playback")]

    [Export]
    private Label _speedLabel = null;

    [Export]
    private HSlider _speedSlider = null;

    private Color[] presetColors = [
        Colors.White,
        Colors.Red,
        Colors.Orange,
        Colors.Yellow,
        Colors.Green,
        Colors.Blue,
        Colors.Indigo,
        Colors.Violet
    ];

    public override void _Ready()
    {
        CloseRequested += Hide;

        if (GetChild(0) is TabContainer tabContainer)
            foreach (var child in GetAllChildren(tabContainer, true))
            {
                if (child is TabBar tabBar)
                    tabBar.FocusMode = Control.FocusModeEnum.None;
            }

        // Video

        _disableVideoCheckBox.Toggled += toggle => Player.DisableVideo = toggle;

        _canSkipFramesCheckBox.Toggled += toggle => Player.CanSkipFrames = toggle;

        _seekAsyncCheckBox.Toggled += toggle => Player.SeekAsync = toggle;

        _stetchCheckBox.Toggled += toggle =>
            Player.StretchMode = toggle
            ? TextureRect.StretchModeEnum.Scale
            : TextureRect.StretchModeEnum.KeepAspectCentered;

        foreach (var child in GetAllChildren(_colorPicker, true))
        {
            if (child is Slider slider)
                slider.Scrollable = false;

            if (child is Control control && child is not LineEdit)
                control.FocusMode = Control.FocusModeEnum.None;
        }

        foreach (var preset in presetColors)
            _colorPicker.AddPreset(preset);

        _colorPicker.ColorChanged += color => Player.Color = color;

        _hueSlider.ValueChanged += value =>
        {
            Player.Hue = (float)value;
            _hueLabel.Text = $"Hue: {(int)value}";
        };

        _hueResetButton.Pressed += () =>
        {
            var hueDefault = 0.0f;
            _hueSlider.SetValue(hueDefault);
        };

        _saturationSlider.ValueChanged += value =>
        {
            Player.Saturation = (float)value;
            _saturationLabel.Text = $"Saturation: {(int)value}";
        };

        _saturationResetButton.Pressed += () =>
        {
            var saturationDefault = 100.0f;
            _saturationSlider.SetValue(saturationDefault);
        };

        _lightnessSlider.ValueChanged += value =>
        {
            Player.Lightness = (float)value;
            _lightnessLabel.Text = $"Lightness: {(int)value}";
        };

        _lightnessResetButton.Pressed += () =>
        {
            var lightnessDefault = 50.0f;
            _lightnessSlider.SetValue(lightnessDefault);
        };

        _contrastSlider.ValueChanged += value =>
        {
            Player.Contrast = (float)value;
            _contrastLabel.Text = $"Contrast: {(int)value}";
        };

        _contrastResetButton.Pressed += () =>
        {
            var contrastDefault = 0.0f;
            _contrastSlider.SetValue(contrastDefault);
        };

        _chromaKeyEnableCheckBox.Toggled += toggle => Player.ChromaKeyEnable = toggle;

        foreach (var child in GetAllChildren(_chromaKeyColorPicker, true))
        {
            if (child is Slider slider)
                slider.Scrollable = false;

            if (child is Control control && child is not LineEdit)
                control.FocusMode = Control.FocusModeEnum.None;
        }

        foreach (var preset in presetColors)
            _chromaKeyColorPicker.AddPreset(preset);

        _chromaKeyColorPicker.ColorChanged += color => Player.ChromaKeyColor = color;

        _chromaKeyThresholdSlider.ValueChanged += value =>
        {
            Player.ChromaKeyThreshold = (float)value;
            _chromaKeyThresholdLabel.Text = $"Chroma Key Threshold: {value:F2}";
        };

        _chromaKeySmoothnessSlider.ValueChanged += value =>
        {
            Player.ChromaKeySmoothness = (float)value;
            _chromaKeySmoothnessLabel.Text = $"Chroma Key Smoothness: {value:F2}";
        };

        // Audio

        _disableAudioCheckBox.Toggled += toggle => Player.DisableAudio = toggle;

        _bufferLengthSlider.ValueChanged += value =>
        {
            Player.BufferLength = (float)(value / 1000.0);
            _bufferLengthLabel.Text = $"Buffer Length: {(int)value}ms";
        };

        _pitchSlider.ValueChanged += value =>
        {
            Player.Pitch = (float)value;
            _pitchLabel.Text = $"Pitch: {value:F2}";
        };

        _volumeMuteButton.Pressed += () =>
        {
            Player.Mute = !Player.Mute;
            _volumeIcon.Texture = Player.Mute ? _volumeMuteIcon : _volumeNormalIcon;
        };

        _volumeSlider.ValueChanged += value =>
        {
            Player.Volume = (float)value / 100.0f;
            _volumeLabel.Text = $"Volume: {(int)value}";
        };

        // Playback

        _speedSlider.ValueChanged += value =>
        {
            Player.Speed = (float)value;
            _speedLabel.Text = $"Speed: {(float)value:F2}x";
        };
    }

    public static List<Node> GetAllChildren(Node root, bool includeInternal = false)
    {
        var result = new List<Node>();

        var stack = new Stack<Node>();

        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            foreach (Node child in current.GetChildren(includeInternal))
            {
                result.Add(child);
                stack.Push(child);
            }
        }

        return result;
    }
}