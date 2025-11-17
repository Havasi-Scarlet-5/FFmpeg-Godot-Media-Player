using System;
using FFmpegMediaPlayer.godot.scenes.windows;
using Godot;

namespace FFmpegMediaPlayer.godot.scenes.test;

partial class FFmpegGodotMediaPlayerTest : Control
{
    [Export]
    private FFmpegGodotMediaPlayer _videoPlayer = null;

    [Export]
    private Label _debugLabel = null;

    [Export]
    private Label _timeLabel = null;

    [Export]
    private Label lengthLabel = null;

    [Export]
    private HSlider _timeSlider = null;

    [Export]
    private Button _playbackButton = null;

    [Export]
    private Texture2D _playIcon = null;

    [Export]
    private Texture2D _pauseIcon = null;

    [Export]
    private Button _stopButton = null;

    [Export]
    private Button _loopButton = null;

    [Export]
    private Button _controllerButton = null;

    [Export]
    private Button _fullScreenButton = null;

    [Export]
    private Texture2D _fullScreenOnIcon = null;

    [Export]
    private Texture2D _fullScreenOffIcon = null;

    [Export]
    private FileDialog _openFileDialog = null;

    [Export]
    private SettingsWindow _settingsWindow = null;

    [Export]
    private VBoxContainer _ui = null;

    [Export]
    private MenuButton _fileMenuButton = null;

    private double _showTimeout = 0.0;

    private bool _newVideoLoaded = false;

    private bool _isPlaying = false;

    private DisplayServer.WindowMode _lastWindowMode = DisplayServer.WindowMode.Windowed;

    private Vector2 _lastMousePos = Vector2.Zero;

    public override void _Ready()
    {
        if (OS.GetName() == "Android")
            OS.RequestPermissions();

        _fullScreenButton.Pressed += ToggleFullScreen;

        _openFileDialog.AddFilter("*.mp4, *.webm, *.mpg, *.mpeg, *.mkv, *.avi, *.mov, *.wmv, *.ogv", "Supported Video Files");

        _openFileDialog.AddFilter("*.mp3, *.ogg, *.wav, *.flac", "Supported Audio Files");

        _openFileDialog.FileSelected += path =>
        {
            _newVideoLoaded = true;

            _videoPlayer.Open(new FFmpegGodotMediaSource() { Url = path });

            if (_isPlaying && !_videoPlayer.AutoPlay)
                _videoPlayer.Play();
            else if (_videoPlayer.AutoPlay)
                _isPlaying = true;

            if (_videoPlayer.Loaded)
                GetWindow().Title = path.GetFile().GetBaseName();
        };

        GetViewport().GetWindow().FilesDropped += file =>
        {
            _newVideoLoaded = true;

            _videoPlayer.Open(new FFmpegGodotMediaSource() { Url = file[0] });

            if (_isPlaying && !_videoPlayer.AutoPlay)
                _videoPlayer.Play();
            else if (_videoPlayer.AutoPlay)
                _isPlaying = true;

            if (_videoPlayer.Loaded)
                GetWindow().Title = file[0].GetFile().GetBaseName();
        };

        _timeSlider.DragStarted += _videoPlayer.Pause;

        _timeSlider.ValueChanged += value =>
        {
            if (_newVideoLoaded)
            {
                _newVideoLoaded = false;
                return;
            }

            _videoPlayer.Seek(value);
        };

        _timeSlider.DragEnded += valueChanged =>
        {
            if (_isPlaying)
                _videoPlayer.Play();
        };

        _playbackButton.Pressed += () =>
        {
            _isPlaying = !_isPlaying;

            if (_isPlaying)
                _videoPlayer.Play();
            else
                _videoPlayer.Pause();
        };

        _stopButton.Pressed += () =>
        {
            _isPlaying = false;
            _videoPlayer.Stop();
        };

        _loopButton.Toggled += toggle =>
        {
            _videoPlayer.Loop = toggle;
        };

        _settingsWindow.RegisterPlayer(_videoPlayer);

        _controllerButton.Pressed += () =>
        {
            if (_settingsWindow.Visible)
            {
                _settingsWindow.Hide();
                return;
            }

            var mousePos = (Vector2I)GetViewport().GetMousePosition();

            var windowSize = _settingsWindow.Size;

            _settingsWindow.Show();

            _settingsWindow.SetPosition(new Vector2I(mousePos.X - windowSize.X, mousePos.Y - windowSize.Y - 32));
        };

        _fileMenuButton.GetPopup().IdPressed += id =>
        {
            switch (id)
            {
                case 0: // Open
                    _openFileDialog.PopupCentered();
                    break;
                case 1: // Close
                    _videoPlayer.Close();
                    GetWindow().Title = (string)ProjectSettings.GetSetting("application/config/name");
                    break;
                case 2: // Exit
                    GetTree().Quit();
                    break;
            }
        };

        if (_videoPlayer.Source != null && _videoPlayer.AutoPlay)
            _isPlaying = true;
    }

    public override void _Process(double delta)
    {
        _timeLabel.Text = TimeSpan.FromSeconds(_videoPlayer.Time).ToString("mm\\:ss\\:fff");

        lengthLabel.Text = TimeSpan.FromSeconds(_videoPlayer.Length).ToString("mm\\:ss\\:fff");

        _timeSlider.MaxValue = _videoPlayer.Length;

        _timeSlider.SetValueNoSignal(_videoPlayer.Time);

        _playbackButton.Icon = _videoPlayer.IsPlaying ? _pauseIcon : _playIcon;

        var videoTime = _videoPlayer.VideoProcess?.Time ?? 0.0;

        var videoLength = _videoPlayer.VideoProcess?.Duration ?? 0.0;

        var audioTime = _videoPlayer.AudioProcess?.Time ?? 0.0;

        var audioLength = _videoPlayer.AudioProcess?.Duration ?? 0.0;

        var difference = (_videoPlayer.IsVideoValid && _videoPlayer.IsAudioValid) ? (videoTime - audioTime) : 0.0;

        _debugLabel.Text =
            $"Video Time | Length: {videoTime:F3} | {videoLength:F3}"
            + $"\nAudio Time | Length: {audioTime:F3} | {audioLength:F3}"
            + $"\n(Video {(difference == 0.0 ? "=" : difference > 0 ? ">" : "<")} Audio): {Mathf.Abs(difference):F3}"
            + $"\nPlaying: {_videoPlayer.IsPlaying}"
            + $"\nFinished: {_videoPlayer.IsFinished}"
        ;

        if (_showTimeout <= 0.0)
        {
            _ui.Modulate -= Color.FromHsv(0.0f, 0.0f, 0.0f, (float)delta);
            DisplayServer.MouseSetMode(DisplayServer.MouseMode.Hidden);
        }
        else if (!_settingsWindow.Visible)
            _showTimeout -= delta;

        var mousePos = GetGlobalMousePosition();

        if (_lastMousePos != mousePos)
        {
            ShowUI();
            _lastMousePos = mousePos;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey inputEventKey && inputEventKey.IsPressed() && !inputEventKey.IsEcho())
        {
            switch (inputEventKey.PhysicalKeycode)
            {
                case Key.F11:
                    ToggleFullScreen();
                    break;
            }
        }

        ShowUI();
    }

    private void ShowUI()
    {
        _showTimeout = 3.0;

        _ui.Modulate = Colors.White;

        DisplayServer.MouseSetMode(DisplayServer.MouseMode.Visible);
    }

    private void ToggleFullScreen()
    {
        if (Engine.IsEmbeddedInEditor())
            return;

        var mode = DisplayServer.WindowGetMode();

        if (mode == DisplayServer.WindowMode.Windowed || mode == DisplayServer.WindowMode.Maximized)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            _fullScreenButton.Icon = _fullScreenOffIcon;
        }
        else
        {
            DisplayServer.WindowSetMode(_lastWindowMode);
            _fullScreenButton.Icon = _fullScreenOnIcon;
        }

        _lastWindowMode = mode;
    }
}