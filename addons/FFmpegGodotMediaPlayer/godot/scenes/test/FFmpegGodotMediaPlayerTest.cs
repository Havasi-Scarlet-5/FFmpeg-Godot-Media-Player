using System;
using FFmpegMediaPlayer.godot.scenes.windows;
using Godot;

namespace FFmpegMediaPlayer.godot.scenes.test;

partial class FFmpegGodotMediaPlayerTest : Control
{
    [Export]
    private FFmpegGodotMediaPlayer _mediaPlayer = null;

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

    private bool _newMediaFileLoaded = false;

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

        // You can load from (res://) by using this:
        // var source = GD.Load<FFmpegGodotMediaSource>("res://video.mp4");
        // GD.Load can only loaded with (res://) path
        // For both (file system) and (res://) path use this:
        // var source = new FFmpegGodotMediaSource() { Url = "C:/video.mp4" };
        // Then _mediaPlayer.Source = source;
        // Or can be using with _mediaPlayer.Open(source), _mediaPlayer.SetSource(source) as well

        _openFileDialog.FileSelected += path =>
        {
            _newMediaFileLoaded = true;
            _mediaPlayer.Open(new FFmpegGodotMediaSource() { Url = path });
        };

        GetViewport().GetWindow().FilesDropped += file =>
        {
            _newMediaFileLoaded = true;
            _mediaPlayer.Open(new FFmpegGodotMediaSource() { Url = file[0] });
        };

        // If LoadAsync is enable then LoadCompleted must be used
        _mediaPlayer.OnLoadedCompleted += player =>
        {
            if (_isPlaying && !player.AutoPlay)
                player.Play();
            else if (player.AutoPlay)
                _isPlaying = true;

            GetWindow().Title = player.Source.Url.GetFile().GetBaseName();
        };

        // Called when media is playing finished, never called when loop is enable
        _mediaPlayer.OnFinished += player =>
        {
        };

        // Called when media is closed
        _mediaPlayer.OnClosed += player =>
        {
            GetWindow().Title = (string)ProjectSettings.GetSetting("application/config/name");
        };

        _timeSlider.DragStarted += _mediaPlayer.Pause;

        _timeSlider.ValueChanged += value =>
        {
            // There is some fucking bug with this slider :/
            if (_newMediaFileLoaded)
            {
                _newMediaFileLoaded = false;
                return;
            }

            // Changed media position in seconds
            _mediaPlayer.Seek(value);
        };

        _timeSlider.DragEnded += valueChanged =>
        {
            if (_isPlaying)
                _mediaPlayer.Play();
        };

        _playbackButton.Pressed += () =>
        {
            _isPlaying = !_isPlaying;

            if (_isPlaying)
                _mediaPlayer.Play();
            else
                _mediaPlayer.Pause();
        };

        _stopButton.Pressed += () =>
        {
            _isPlaying = false;
            _mediaPlayer.Stop();
        };

        _loopButton.Toggled += toggle =>
        {
            _mediaPlayer.Loop = toggle;
        };

        _settingsWindow.RegisterPlayer(_mediaPlayer);

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
                    // Close the media
                    _mediaPlayer.Close();
                    break;
                case 2: // Exit
                    GetTree().Quit();
                    break;
            }
        };

        if (_mediaPlayer.Source != null && _mediaPlayer.AutoPlay)
            _isPlaying = true;
    }

    public override void _Process(double delta)
    {
        _timeLabel.Text = TimeSpan.FromSeconds(_mediaPlayer.Time).ToString("mm\\:ss\\:fff");

        lengthLabel.Text = TimeSpan.FromSeconds(_mediaPlayer.Length).ToString("mm\\:ss\\:fff");

        _timeSlider.MaxValue = _mediaPlayer.Length;

        _timeSlider.SetValueNoSignal(_mediaPlayer.Time);

        _playbackButton.Icon = _mediaPlayer.IsPlaying ? _pauseIcon : _playIcon;

        var videoTime = _mediaPlayer.VideoProcess?.Time ?? 0.0;

        var videoLength = _mediaPlayer.VideoProcess?.Duration ?? 0.0;

        var audioTime = _mediaPlayer.AudioProcess?.Time ?? 0.0;

        var audioLength = _mediaPlayer.AudioProcess?.Duration ?? 0.0;

        var difference = (_mediaPlayer.IsVideoValid && _mediaPlayer.IsAudioValid) ? (videoTime - audioTime) : 0.0;

        _debugLabel.Text =
            $"Video Time | Length: {videoTime:F3} | {videoLength:F3}"
            + $"\nAudio Time | Length: {audioTime:F3} | {audioLength:F3}"
            + $"\n(Video {(difference == 0.0 ? "=" : difference > 0 ? ">" : "<")} Audio): {Mathf.Abs(difference):F3}"
            + $"\nPlaying: {_mediaPlayer.IsPlaying}"
            + $"\nFinished: {_mediaPlayer.IsFinished}"
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