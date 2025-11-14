using System;
using System.Collections.Generic;
using FFmpeg.AutoGen.Abstractions;
using Godot;

namespace FFmpegMediaPlayer.godot;

// Audio is not my favorite part, i suck at this :(
public sealed unsafe partial class FFmpegGodotAudioProcess : RefCounted
{
    private readonly FFmpegAudioDecoder _decoder = null;

    private AudioStreamPlayer _player = null;

    private bool PlayerValid => IsInstanceValid(_player) && IsInstanceValid(_player.Stream) && _player.Stream is AudioStreamGenerator;

    private Queue<(Queue<Vector2> samples, double time, float pitch, float speed)> _framesQueue = [];

    private const int FRAMES_QUEUE_SIZE = 5;

    private double _bufferedFrameTime = 0.0;

    public bool IsRunning { get; private set; } = false;

    public bool IsFinished { get; private set; } = false;

    private double _currentFrameTime = 0.0;

    public double Time
    {
        get => GetTime();
        set => SetTime(value);
    }

    private double _duration = 0.0;

    public double Duration => GetDuration();

    private float _pitch = 1.0f;

    public float Pitch
    {
        get => GetPitch();
        set => SetPitch(value);
    }

    private float _speed = 1.0f;

    public float Speed
    {
        get => GetSpeed();
        set => SetSpeed(value);
    }

    private float _volume = 1.0f;

    public float Volume
    {
        get => GetVolume();
        set => SetVolume(value);
    }

    private bool _mute = false;

    public bool Mute
    {
        get => GetMute();
        set => SetMute(value);
    }

    public FFmpegGodotAudioProcess(FFmpegAudioDecoder decoder, AudioStreamPlayer player, float bufferLength)
    {
        _decoder = decoder;

        _duration = _decoder.DurationInSeconds;

        player.Stream = new AudioStreamGenerator
        {
            BufferLength = bufferLength,
            MixRate = _decoder.SampleRate,
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom
        };

        player.Play();

        player.StreamPaused = true;

        _player = player;

        _decoder.OnFilterDrain += OnDrain;
    }

    private void OnDrain((AVFrame frame, double time, float pitch, float speed) f)
    {
        SendFrameToQueue(f.frame, f.time, f.pitch, f.speed);
    }

    public void Update(double clockTime)
    {
        if (!PlayerValid || !IsRunning)
            return;

        while (
            IsRunning
            && _framesQueue.Count < FRAMES_QUEUE_SIZE
            && _decoder.TryGetNextFrame(out var frame, out var frameTime, out var framePitch, out var frameSpeed)
        )
            SendFrameToQueue(frame, frameTime, framePitch, frameSpeed);

        if (!_player.Playing)
            _player.Play();

        if (IsPlaybackValid(out var playback))
        {
            if (!playback.IsPlaying())
                playback.Start();

            while (IsRunning && _framesQueue.TryPeek(out var peekedFrame) && playback.GetFramesAvailable() > 0)
            {
                // Trying to take all samples from this frame and push it to the generator
                if (peekedFrame.samples.TryPeek(out var peekedSample) && playback.PushFrame(peekedSample))
                {
                    var sampleDuration = 1.0 / _decoder.SampleRate;

                    // It just a guess not always corrected
                    _bufferedFrameTime += sampleDuration * peekedFrame.pitch * peekedFrame.speed;

                    peekedFrame.samples.Dequeue();
                }
                // No more samples, remove this frame
                else if (peekedFrame.samples.Count <= 0)
                {
                    _bufferedFrameTime = peekedFrame.time;
                    _framesQueue.Dequeue();
                }
            }
        }

        _currentFrameTime = _bufferedFrameTime - GetGeneratorDataLeftInSeconds() * _pitch * _speed;

        IsFinished = clockTime >= Duration;

        if (IsFinished)
        {
            Stop();
            _currentFrameTime = _duration;
        }
    }

    private void SendFrameToQueue(AVFrame frame, double time, float pitch, float speed)
    {
        try
        {
            var sampleFormat = (AVSampleFormat)frame.format;

            if (sampleFormat == AVSampleFormat.AV_SAMPLE_FMT_FLT)
            {
                var samples = new ReadOnlySpan<Vector2>(frame.data[0], frame.nb_samples);
                _framesQueue.Enqueue((new Queue<Vector2>(samples.ToArray()), time, pitch, speed));
            }
            else
                FFmpegLogger.LogErr(this, "Unsupported sample format: ", sampleFormat);
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Error sending frame to queue: ", ex.Message);
        }
    }

    public void Start()
    {
        if (IsRunning)
            return;

        IsFinished = false;

        IsRunning = true;

        if (PlayerValid)
            _player.StreamPaused = false;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        if (PlayerValid)
            _player.StreamPaused = true;
    }

    public double GetTime()
    {
        return Mathf.Clamp(_currentFrameTime, 0.0, _duration);
    }

    public void SetTime(double time)
    {
        IsFinished = false;

        time = Mathf.Clamp(time, 0.0, _duration);

        _currentFrameTime = _bufferedFrameTime = time;

        _decoder?.TrySeek(_currentFrameTime);

        ClearAudioBuffer();
    }

    public double GetDuration()
    {
        return _duration;
    }

    public float GetPitch()
    {
        return _pitch;
    }

    public void SetPitch(float pitch)
    {
        if (_pitch == pitch)
            return;

        _pitch = pitch;

        _decoder?.SetOutputPitch(_pitch);
    }

    public float GetSpeed()
    {
        return _speed;
    }

    public void SetSpeed(float speed)
    {
        if (_speed == speed)
            return;

        _speed = speed;

        _decoder?.SetOutputSpeed(_speed);
    }

    public float GetVolume()
    {
        return _volume;
    }

    public void SetVolume(float volume)
    {
        _volume = volume;

        if (PlayerValid && !_mute)
            _player.VolumeLinear = _volume;
    }

    public bool GetMute()
    {
        return _mute;
    }

    public void SetMute(bool mute)
    {
        if (PlayerValid)
        {
            if (mute)
                _player.VolumeLinear = 0.0f;
            else
                _player.VolumeLinear = _volume;
        }

        _mute = mute;
    }

    public bool IsPlaybackValid(out AudioStreamGeneratorPlayback playback)
    {
        if (PlayerValid && _player.Playing && _player.GetStreamPlayback() is AudioStreamGeneratorPlayback playbackInstance)
        {
            playback = playbackInstance;
            return true;
        }
        else
            playback = null;

        return false;
    }

    private void ClearAudioBuffer()
    {
        _framesQueue?.Clear();

        // Force the playback to exist so we can clear the buffer at any time
        if (!_player.Playing)
            _player.Play();

        // Clear the playback buffer
        if (IsPlaybackValid(out var playback))
        {
            // Stop the playback first or we getting error
            playback.Stop();

            playback.ClearBuffer();

            playback.Start();
        }
    }

    private static uint NearestShift(uint p_number)
    {
        uint i = 31;

        do
        {
            i--;

            if ((p_number & ((uint)1 << (int)i)) != 0)
                return i + 1;
        }
        while (i != 0);

        return 0;
    }

    // Me who waiting for this feature be implemented..., it been a fucking year!
    // https://github.com/godotengine/godot/pull/99512
    /// <summary>
    /// Return the amount of internal buffer that has not been used through the generator playback in seconds.
    /// </summary>
    public double GetGeneratorDataLeftInSeconds()
    {
        if (PlayerValid && IsPlaybackValid(out var playback))
        {
            var streamGenerator = _player.Stream as AudioStreamGenerator;

            var targetMixRate = streamGenerator.MixRate;

            var targetBufferLength = streamGenerator.BufferLength;

            var targetBufferSize = (uint)(targetMixRate * targetBufferLength);

            var bufferSize = 1U << (int)NearestShift(targetBufferSize);

            var bufferSpaceLeft = playback.GetFramesAvailable();

            var bufferDataLeft = bufferSize - bufferSpaceLeft - 1;

            return 1.0 * bufferDataLeft / targetMixRate;
        }

        return 0.0;
    }

    public new void Dispose()
    {
        Stop();

        ClearAudioBuffer();

        _framesQueue = null;

        _player?.Stop();

        _player?.SetStream(null);

        _player = null;

        _decoder.OnFilterDrain -= OnDrain;

        base.Dispose();
    }
}