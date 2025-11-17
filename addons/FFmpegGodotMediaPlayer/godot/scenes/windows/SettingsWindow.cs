using System.Collections.Generic;
using Godot;

namespace FFmpegMediaPlayer.godot.scenes.windows;

public partial class SettingsWindow : Window
{
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
    }

    public void RegisterPlayer(FFmpegGodotMediaPlayer player)
    {
        // Video

        _disableVideoCheckBox.Toggled += toggle => player.DisableVideo = toggle;

        _disableAudioCheckBox.ButtonPressed = player.DisableVideo;

        _canSkipFramesCheckBox.Toggled += toggle => player.CanSkipFrames = toggle;

        _canSkipFramesCheckBox.ButtonPressed = player.CanSkipFrames;

        _seekAsyncCheckBox.Toggled += toggle => player.SeekAsync = toggle;

        _seekAsyncCheckBox.ButtonPressed = player.SeekAsync;

        _stetchCheckBox.Toggled += toggle =>
            player.StretchMode = toggle
            ? TextureRect.StretchModeEnum.Scale
            : TextureRect.StretchModeEnum.KeepAspectCentered;

        _stetchCheckBox.ButtonPressed = player.StretchMode == TextureRect.StretchModeEnum.Scale;

        foreach (var child in GetAllChildren(_colorPicker, true))
        {
            if (child is Slider slider)
                slider.Scrollable = false;

            if (child is Control control && child is not LineEdit)
                control.FocusMode = Control.FocusModeEnum.None;
        }

        foreach (var preset in presetColors)
            _colorPicker.AddPreset(preset);

        _colorPicker.ColorChanged += color => player.Color = color;

        _colorPicker.Color = player.Color;

        _hueSlider.ValueChanged += value =>
        {
            player.Hue = (float)value;
            _hueLabel.Text = $"Hue: {(int)value}";
        };

        _hueSlider.Value = player.Hue;

        _hueResetButton.Pressed += () =>
        {
            var hueDefault = 0.0f;
            _hueSlider.Value = hueDefault;
        };

        _saturationSlider.ValueChanged += value =>
        {
            player.Saturation = (float)value;
            _saturationLabel.Text = $"Saturation: {(int)value}";
        };

        _saturationSlider.Value = player.Saturation;

        _saturationResetButton.Pressed += () =>
        {
            var saturationDefault = 100.0f;
            _saturationSlider.Value = saturationDefault;
        };

        _lightnessSlider.ValueChanged += value =>
        {
            player.Lightness = (float)value;
            _lightnessLabel.Text = $"Lightness: {(int)value}";
        };

        _lightnessSlider.Value = player.Lightness;

        _lightnessResetButton.Pressed += () =>
        {
            var lightnessDefault = 50.0f;
            _lightnessSlider.Value = lightnessDefault;
        };

        _contrastSlider.ValueChanged += value =>
        {
            player.Contrast = (float)value;
            _contrastLabel.Text = $"Contrast: {(int)value}";
        };

        _contrastSlider.Value = player.Contrast;

        _contrastResetButton.Pressed += () =>
        {
            var contrastDefault = 0.0f;
            _contrastSlider.Value = contrastDefault;
        };

        _chromaKeyEnableCheckBox.Toggled += toggle => player.ChromaKeyEnable = toggle;

        _chromaKeyEnableCheckBox.ButtonPressed = player.ChromaKeyEnable;

        foreach (var child in GetAllChildren(_chromaKeyColorPicker, true))
        {
            if (child is Slider slider)
                slider.Scrollable = false;

            if (child is Control control && child is not LineEdit)
                control.FocusMode = Control.FocusModeEnum.None;
        }

        foreach (var preset in presetColors)
            _chromaKeyColorPicker.AddPreset(preset);

        _chromaKeyColorPicker.ColorChanged += color => player.ChromaKeyColor = color;

        _chromaKeyColorPicker.Color = player.ChromaKeyColor;

        _chromaKeyThresholdSlider.ValueChanged += value =>
        {
            player.ChromaKeyThreshold = (float)value;
            _chromaKeyThresholdLabel.Text = $"Chroma Key Threshold: {value:F2}";
        };

        _chromaKeyThresholdSlider.Value = player.ChromaKeyThreshold;

        _chromaKeySmoothnessSlider.ValueChanged += value =>
        {
            player.ChromaKeySmoothness = (float)value;
            _chromaKeySmoothnessLabel.Text = $"Chroma Key Smoothness: {value:F2}";
        };

        _chromaKeySmoothnessSlider.Value = player.ChromaKeySmoothness;

        // Audio

        _disableAudioCheckBox.Toggled += toggle => player.DisableAudio = toggle;

        _disableAudioCheckBox.ButtonPressed = player.DisableAudio;

        _bufferLengthSlider.ValueChanged += value =>
        {
            player.BufferLength = (float)(value / 1000.0);
            _bufferLengthLabel.Text = $"Buffer Length: {(int)value}ms";
        };

        _bufferLengthSlider.Value = (int)(player.BufferLength * 1000.0f);

        _pitchSlider.ValueChanged += value =>
        {
            player.Pitch = (float)value;
            _pitchLabel.Text = $"Pitch: {value:F2}";
        };

        _pitchSlider.Value = player.Pitch;

        _volumeMuteButton.Pressed += () =>
        {
            player.Mute = !player.Mute;
            _volumeIcon.Texture = player.Mute ? _volumeMuteIcon : _volumeNormalIcon;
        };

        _volumeSlider.ValueChanged += value =>
        {
            player.Volume = (float)(value / 100.0f);
            _volumeLabel.Text = $"Volume: {(int)value}";
        };

        _volumeSlider.Value = (int)(player.Volume * 100.0f);

        // Playback

        _speedSlider.ValueChanged += value =>
        {
            player.Speed = (float)value;
            _speedLabel.Text = $"Speed: {(float)value:F2}x";
        };

        _speedSlider.Value = player.Speed;
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