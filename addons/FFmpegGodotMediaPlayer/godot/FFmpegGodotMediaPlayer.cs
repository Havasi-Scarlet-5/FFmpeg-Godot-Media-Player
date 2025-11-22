using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace FFmpegMediaPlayer.godot;

[GlobalClass, Icon("res://addons/FFmpegGodotMediaPlayer/godot/icons/logo.svg")]
public partial class FFmpegGodotMediaPlayer : Control
{
    private FFmpegGodotMediaSource _source = null;

    [Export]
    public FFmpegGodotMediaSource Source
    {
        get => GetSource();
        set => SetSource(value, LoadAsync);
    }

    [Export]
    public bool LoadAsync = false;

    [ExportCategory("Video")]

    [Export]
    public bool DisableVideo = false;

    private bool _canSkipFrames = true;

    [Export]
    public bool CanSkipFrames
    {
        get => _canSkipFrames;
        set
        {
            _canSkipFrames = value;
            VideoProcess?.SetCanSkipFrames(_canSkipFrames);
        }
    }

    [Export]
    public bool SeekAsync = true;

    public TextureRect TextureRect { get; private set; } = null;

    private TextureRect.StretchModeEnum _stretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

    [Export]
    public TextureRect.StretchModeEnum StretchMode
    {
        get => _stretchMode;
        set
        {
            _stretchMode = value;

            if (TextureRect != null)
                TextureRect.StretchMode = _stretchMode;
        }
    }

    private Color _color = Colors.White;

    [Export]
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            VideoProcess?.SetColor(_color);
        }
    }

    private float _hue = 0.0f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float Hue
    {
        get => _hue;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 1.0f), 0.0f, 360.0f);

            _hue = v;

            VideoProcess?.SetHue(_hue);
        }
    }

    private float _saturation = 100.0f;

    [Export(PropertyHint.Range, "0,200,1")]
    public float Saturation
    {
        get => _saturation;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 1.0f), 0.0f, 200.0f);

            _saturation = v;

            VideoProcess?.SetSaturation(_saturation);
        }
    }

    private float _lightness = 50.0f;

    [Export(PropertyHint.Range, "0,100,1")]
    public float Lightness
    {
        get => _lightness;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 1.0f), 0.0f, 100.0f);

            _lightness = v;

            VideoProcess?.SetLightness(_lightness);
        }
    }

    private float _contrast = 0.0f;

    [Export(PropertyHint.Range, "-100,100,1")]
    public float Contrast
    {
        get => _contrast;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 1.0f), -100.0f, 100.0f);

            _contrast = v;

            VideoProcess?.SetContrast(_contrast);
        }
    }

    [ExportSubgroup("Chroma Key")]

    private bool _chromakeyEnable = false;

    [Export]
    public bool ChromaKeyEnable
    {
        get => _chromakeyEnable;
        set
        {
            _chromakeyEnable = value;
            VideoProcess?.SetChromaKeyEnable(_chromakeyEnable);
        }
    }

    private Color _chromaKeyColor = Colors.Green;

    [Export]
    public Color ChromaKeyColor
    {
        get => _chromaKeyColor;
        set
        {
            _chromaKeyColor = value;
            VideoProcess?.SetChromaKeyColor(_chromaKeyColor);
        }
    }

    private float _chromaKeyThreshold = 0.4f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChromaKeyThreshold
    {
        get => _chromaKeyThreshold;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.01f), 0.0f, 1.0f);

            _chromaKeyThreshold = v;

            VideoProcess?.SetChromaKeyThreshold(_chromaKeyThreshold);
        }
    }

    private float _chromaKeySmoothness = 0.1f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChromaKeySmoothness
    {
        get => _chromaKeySmoothness;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.01f), 0.0f, 1.0f);

            _chromaKeySmoothness = v;

            VideoProcess?.SetChromaKeySmoothness(_chromaKeySmoothness);
        }
    }

    [ExportCategory("Audio")]

    [Export]
    public bool DisableAudio = false;

    private float _bufferLength = 0.1f;

    [Export(PropertyHint.Range, "0.01,1,0.001")]
    public float BufferLength
    {
        get => _bufferLength;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.001f), 0.01f, 1.0f);
            _bufferLength = v;
        }
    }

    public AudioStreamPlayer AudioStreamPlayer { get; private set; }

    private float _pitch = 1.0f;

    [Export(PropertyHint.Range, "0.25,2,0.01")]
    public float Pitch
    {
        get => _pitch;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.01f), 0.25f, 2.0f);

            _pitch = v;

            AudioProcess?.SetPitch(_pitch);

            AudioProcess?.SetSpeed(_speed / _pitch);
        }
    }

    private float _volume = 1.0f;

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float Volume
    {
        get => _volume;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.001f), 0.0f, 1.0f);

            _volume = v;

            AudioProcess?.SetVolume(_volume);
        }
    }

    private bool _mute = false;

    [Export]
    public bool Mute
    {
        get => _mute;
        set
        {
            _mute = value;
            AudioProcess?.SetMute(_mute);
        }
    }

    private string _bus = "Master";

    [Export]
    public string Bus
    {
        get => _bus;
        set
        {
            _bus = value;

            if (IsInstanceValid(AudioStreamPlayer))
                AudioStreamPlayer.Bus = _bus;
        }
    }

    [ExportCategory("Playback")]

    [Export]
    public bool AutoPlay = false;

    private float _speed = 1.0f;

    [Export(PropertyHint.Range, "0.25,2,0.01")]
    public float Speed
    {
        get => _speed;
        set
        {
            var v = Mathf.Clamp(Mathf.Snapped(value, 0.01f), 0.25f, 2.0f);

            _speed = v;

            VideoProcess?.SetSpeed(_speed);

            AudioProcess?.SetSpeed(_speed / _pitch);
        }
    }

    [Export]
    public bool Loop = false;

    [ExportCategory("Misc")]

    private bool _debugLog = true;

    [Export]
    public bool DebugLog
    {
        get => _debugLog;
        set
        {
            _debugLog = value;
            FFmpegStatic.DebugLog = _debugLog;
        }
    }

    public FFmpegVideoDecoder VideoDecoder { get; private set; } = null;

    public FFmpegGodotVideoProcess VideoProcess { get; private set; } = null;

    public FFmpegAudioDecoder AudioDecoder { get; private set; } = null;

    public FFmpegGodotAudioProcess AudioProcess { get; private set; } = null;

    public bool IsVideoValid => VideoDecoder != null && !VideoDecoder.IsThumbnail && VideoProcess != null;

    public bool IsAudioValid => AudioDecoder != null && AudioProcess != null;

    public bool Loaded { get; private set; } = false;

    private double _clockTime = 0.0;

    public double ClockTime => Mathf.Clamp(_clockTime, 0.0, Length);

    public double Time => IsVideoValid ? VideoProcess.Time : IsAudioValid ? AudioProcess.Time : 0.0;

    public double Length => IsVideoValid ? VideoProcess.Duration : IsAudioValid ? AudioProcess.Duration : 0.0;

    public bool IsPlaying { get; private set; } = false;

    public bool IsFinished { get; private set; } = false;

    [Signal]
    public delegate void FinishedEventHandler();

    [Signal]
    public delegate void LoadedCompletedEventHandler();

    private double _lastAudioTime = 0.0;

    private CancellationTokenSource _loadCTS;

    public override void _Ready()
    {
        FFmpegStatic.DebugLog = _debugLog;

        if (TextureRect == null)
        {
            TextureRect = new TextureRect
            {
                Texture = new ImageTexture(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = _stretchMode
            };

            AddChild(TextureRect, true, InternalMode.Back);

            TextureRect.SetAnchorsPreset(LayoutPreset.FullRect, true);

            TextureRect.Hide();
        }

        if (AudioStreamPlayer == null)
        {
            AudioStreamPlayer = new AudioStreamPlayer
            {
                VolumeDb = Mathf.LinearToDb(Volume)
            };

            AddChild(AudioStreamPlayer, true, InternalMode.Back);

            AudioStreamPlayer.Bus = Bus;
        }

        if (_source != null)
            SetSource(_source);
    }

    public override void _Process(double delta)
    {
        if (IsPlaying)
        {
            _clockTime += delta * _speed;

            if (IsAudioValid)
            {
                var audioTime = AudioProcess.Time;

                var audioDelta = audioTime - _lastAudioTime;

                if (Mathf.Abs(audioDelta) > 0.0)
                {
                    var absDrift = Mathf.Abs(audioTime - _clockTime);

                    var threshold = 0.05;

                    var outOfSync = absDrift > threshold;

                    if (outOfSync)
                        _clockTime = audioTime;
                }

                _lastAudioTime = audioTime;
            }
        }

        // Delay 100ms to keep synchronize with audio
        VideoProcess?.Update(_clockTime + (IsAudioValid ? -0.1 : 0.0));

        AudioProcess?.Update();

        var videoFinished = !IsVideoValid || VideoProcess.IsFinished;

        var audioFinished = !IsAudioValid || AudioProcess.IsFinished;

        IsFinished = videoFinished && audioFinished;

        if (IsFinished)
        {
            if (Loop)
            {
                Stop();
                Play();
            }
            else
            {
                Pause();
                EmitSignal(SignalName.Finished);
            }
        }
    }

    public override void _ExitTree()
    {
        Close();
    }

    public FFmpegGodotMediaSource GetSource()
    {
        return _source;
    }

    private void LoadDecoders(FFmpegGodotMediaSource source)
    {
        var src = new FFmpegMediaSource() { Url = source.Url };

        if (src.Url.StartsWith("res://"))
            src.Buffer = FileAccess.GetFileAsBytes(src.Url);

        if (!DisableVideo)
            VideoDecoder = new FFmpegVideoDecoder(src);

        if (!DisableAudio)
            AudioDecoder = new FFmpegAudioDecoder(src);
    }

    public async void SetSource(FFmpegGodotMediaSource mediaSource, bool loadAsync = false)
    {
        Close();

        _source = mediaSource;

        if (!IsNodeReady() || _source == null)
            return;

        if (loadAsync)
        {
            _loadCTS = new();

            try
            {
                await Task.Run(() => LoadDecoders(_source), _loadCTS.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _loadCTS?.Dispose();
                _loadCTS = null;
            }
        }
        else
            LoadDecoders(_source);

        if (VideoDecoder?.Exist ?? false)
        {
            VideoProcess = new FFmpegGodotVideoProcess(VideoDecoder, TextureRect);

            VideoProcess?.SetCanSkipFrames(_canSkipFrames);

            VideoProcess?.SetSpeed(_speed);

            VideoProcess?.SetColor(_color);

            VideoProcess?.SetHue(_hue);

            VideoProcess?.SetSaturation(_saturation);

            VideoProcess?.SetLightness(_lightness);

            VideoProcess?.SetContrast(_contrast);

            VideoProcess?.SetChromaKeyEnable(_chromakeyEnable);

            VideoProcess?.SetChromaKeyColor(_chromaKeyColor);

            VideoProcess?.SetChromaKeyThreshold(_chromaKeyThreshold);

            VideoProcess?.SetChromaKeySmoothness(_chromaKeySmoothness);
        }
        else
            CloseVideo();

        if (AudioDecoder?.Exist ?? false)
        {
            AudioProcess = new FFmpegGodotAudioProcess(AudioDecoder, AudioStreamPlayer, _bufferLength);

            AudioProcess?.SetPitch(_pitch);

            AudioProcess?.SetSpeed(_speed / _pitch);

            AudioProcess?.SetVolume(_volume);

            AudioProcess?.SetMute(Mute);
        }
        else
            CloseAudio();

        if (IsVideoValid || IsAudioValid)
        {
            Loaded = true;

            if (loadAsync)
                EmitSignal(SignalName.LoadedCompleted);

            if (AutoPlay)
                Play();
        }
    }

    public void Open(FFmpegGodotMediaSource source)
    {
        SetSource(source, LoadAsync);
    }

    public void Close()
    {
        _loadCTS?.Cancel();

        _loadCTS?.Dispose();

        _loadCTS = null;

        Stop();

        CloseVideo();

        CloseAudio();

        _source = null;

        Loaded = false;
    }

    private void CloseVideo()
    {
        VideoProcess?.Dispose();
        VideoProcess = null;

        VideoDecoder?.Dispose();
        VideoDecoder = null;
    }

    private void CloseAudio()
    {
        AudioProcess?.Dispose();
        AudioProcess = null;

        AudioDecoder?.Dispose();
        AudioDecoder = null;
    }

    public void Play()
    {
        if (IsPlaying)
            return;

        IsFinished = false;

        IsPlaying = true;

        VideoProcess?.Start();

        AudioProcess?.Start();
    }

    public void Pause()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;

        VideoProcess?.Stop();

        AudioProcess?.Stop();
    }

    public void Stop()
    {
        Pause();
        Seek(0.0);
    }

    public void Seek(double time)
    {
        IsFinished = false;

        time = Mathf.Clamp(time, 0.0, Length);

        var seekToTheEnd = time >= Length;

        VideoProcess?.SetTime(seekToTheEnd ? VideoProcess?.Duration ?? 0.0 : time, SeekAsync);

        AudioProcess?.SetTime(seekToTheEnd ? AudioProcess?.Duration ?? 0.0 : time);

        _clockTime = _lastAudioTime = seekToTheEnd ? Mathf.Max(
            VideoProcess?.Duration ?? 0.0,
            AudioProcess?.Duration ?? 0.0
        ) : time;
    }
}