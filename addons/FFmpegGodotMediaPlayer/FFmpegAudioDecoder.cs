using System;
using System.Collections.Generic;
using System.Globalization;
using FFmpeg.AutoGen.Abstractions;

namespace FFmpegMediaPlayer;

public sealed unsafe class FFmpegAudioDecoder : IDisposable
{
    private FFmpegAVIOHandler _avioHandler = null;

    private AVFormatContext* _pFormatContext = null;

    private readonly int _streamIndex = -1;

    private readonly AVStream* _pStream = null;

    private AVCodecContext* _pCodecContext = null;

    public string CodecName { get; } = string.Empty;

    public AVSampleFormat SampleFormat { get; } = AVSampleFormat.AV_SAMPLE_FMT_NONE;

    public string ChannelLayoutName { get; } = string.Empty;

    public double TimeBase { get; } = 0.0;

    public double StartTimeInSeconds { get; } = 0.0;

    public double DurationInSeconds { get; } = 0.0;

    public int SampleRate { get; } = 0;

    public int Channels { get; } = 0;

    public long SampleCount { get; } = 0;

    public int FrameSize { get; } = 0;

    public long BitRate { get; } = 0;

    private AVPacket* _pPacket = null;

    private AVFrame* _pFrame = null;

    private AVFilterGraph* _pFilterGraph = null;

    private AVFilterContext* _pBufferSrcCtx = null;

    private AVFilterContext* _pBufferSinkCtx = null;

    private AVFrame* _pFilteredFrame = null;

    private float _outputPitch = 1.0f;

    private float _lastOutputPitch = 1.0f;

    private float _outputSpeed = 1.0f;

    private float _lastOutputSpeed = 1.0f;

    private double _lastFrameTime = 0.0;

    public Action<(AVFrame frame, double time, float pitch, float speed)> OnFilterDrain = null;

    public bool Exist { get; } = false;

    private bool _codecFlushed = false;

    private object _seekLock = new();

    private object _filterLock = new();

    private bool _disposed = false;

    public FFmpegAudioDecoder(FFmpegMediaSource source)
    {
        try
        {
            var file = source.Url;

            _pFormatContext = ffmpeg.avformat_alloc_context();

            if (_pFormatContext == null)
            {
                FFmpegLogger.LogErr(this, "Failed to alloc context");
                return;
            }

            if (source.Buffer != null && source.Buffer.Length > 0)
            {
                _avioHandler = new FFmpegAVIOHandler(source.Buffer);

                _pFormatContext->pb = _avioHandler.PAVIOContext;

                file = null;

                FFmpegLogger.Log(this, "Load from buffer.");
            }
            else
                FFmpegLogger.Log(this, "Load from file.");

            _pFormatContext->flags |= ffmpeg.AVFMT_FLAG_GENPTS;

            int initResult;

            fixed (AVFormatContext** ppFormatContext = &_pFormatContext)
                initResult = ffmpeg.avformat_open_input(ppFormatContext, file, null, null);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Open file error: ", FFmpegHelper.AVStringError(initResult));

                Dispose();

                return;
            }

            initResult = ffmpeg.avformat_find_stream_info(_pFormatContext, null);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Find stream info error: ", FFmpegHelper.AVStringError(initResult));

                Dispose();

                return;
            }

            AVCodec* pCodec = null;

            _streamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &pCodec, 0);

            if (_streamIndex != ffmpeg.AVERROR_STREAM_NOT_FOUND)
            {
                _pStream = _pFormatContext->streams[_streamIndex];

                _pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec);

                if (_pCodecContext == null)
                {
                    FFmpegLogger.LogErr(this, "Failed to alloc codec context");

                    Dispose();

                    return;
                }

                initResult = ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pStream->codecpar);

                if (initResult < 0)
                {
                    FFmpegLogger.LogErr(this, "Copy codec parameters to codec context error: ", FFmpegHelper.AVStringError(initResult));

                    Dispose();

                    return;
                }

                initResult = ffmpeg.avcodec_open2(_pCodecContext, pCodec, null);

                if (initResult < 0)
                {
                    FFmpegLogger.LogErr(this, "Open codec error: ", FFmpegHelper.AVStringError(initResult));

                    Dispose();

                    return;
                }

                CodecName = ffmpeg.avcodec_get_name(pCodec->id).ToUpper();

                SampleFormat = _pCodecContext->sample_fmt;

                ChannelLayoutName = FFmpegHelper.GetChannelLayoutString(&_pCodecContext->ch_layout).ToUpper();

                TimeBase = ffmpeg.av_q2d(_pStream->time_base);

                if (_pStream->start_time != ffmpeg.AV_NOPTS_VALUE)
                    StartTimeInSeconds = 1.0 * _pStream->start_time * TimeBase;
                else if (_pFormatContext->start_time != ffmpeg.AV_NOPTS_VALUE)
                    StartTimeInSeconds = 1.0 * _pFormatContext->start_time / ffmpeg.AV_TIME_BASE;

                if (_pStream->duration != ffmpeg.AV_NOPTS_VALUE)
                    DurationInSeconds = 1.0 * _pStream->duration * TimeBase;
                else if (_pFormatContext->duration != ffmpeg.AV_NOPTS_VALUE)
                    DurationInSeconds = 1.0 * _pFormatContext->duration / ffmpeg.AV_TIME_BASE;
                else
                    FFmpegLogger.LogErr(this, "Cannot get duration!.");

                SampleRate = _pCodecContext->sample_rate;

                Channels = _pCodecContext->ch_layout.nb_channels;

                SampleCount = _pStream->nb_frames * _pStream->codecpar->frame_size;

                if (SampleCount <= 0)
                    SampleCount = (long)Math.Round(DurationInSeconds * SampleRate);

                if (SampleCount <= 0)
                    FFmpegLogger.LogErr(this, "Cannot get sample count!.");

                FrameSize = _pCodecContext->frame_size;

                BitRate = _pCodecContext->bit_rate;

                _pPacket = ffmpeg.av_packet_alloc();

                if (_pPacket == null)
                {
                    FFmpegLogger.LogErr(this, "Failed to alloc packet");

                    Dispose();

                    return;
                }

                _pFrame = ffmpeg.av_frame_alloc();

                if (_pFrame == null)
                {
                    FFmpegLogger.LogErr(this, "Failed to alloc frame");

                    Dispose();

                    return;
                }

                UpdateFilter();

                _pFilteredFrame = ffmpeg.av_frame_alloc();

                if (_pFilteredFrame == null)
                {
                    FFmpegLogger.LogErr(this, "Failed to alloc filtered frame");

                    Dispose();

                    return;
                }

                var line = "";

                for (int i = 0; i < 100; i++)
                    line += "-";

                FFmpegLogger.Log(this, line);

                FFmpegLogger.Log(this, "Codec Name: ", CodecName);

                FFmpegLogger.Log(this, "Sample Format Name: ", ffmpeg.av_get_sample_fmt_name(SampleFormat).ToUpper() ?? "UNKNOWN");

                FFmpegLogger.Log(this, "Channel Layout Name: ", ChannelLayoutName);

                FFmpegLogger.Log(this, "Time Base: ", TimeBase > 0.0 ? TimeBase : "UNKNOWN");

                FFmpegLogger.Log(this, "Start Time In Seconds: ", StartTimeInSeconds);

                FFmpegLogger.Log(this, "Duration In Seconds: ", DurationInSeconds > 0.0 ? DurationInSeconds : "UNKNOWN");

                FFmpegLogger.Log(this, "Sample Rate: ", SampleRate > 0 ? SampleRate : "UNKNOWN");

                FFmpegLogger.Log(this, "Channels: ", Channels > 0 ? Channels : "UNKNOWN");

                FFmpegLogger.Log(this, "Sample Count: ", SampleCount > 0 ? SampleCount : "UNKNOWN");

                FFmpegLogger.Log(this, "Frame Size: ", FrameSize > 0 ? FrameSize : "UNKNOWN");

                FFmpegLogger.Log(this, "Bit Rate: ", BitRate > 0 ? BitRate : "UNKNOWN");

                FFmpegLogger.Log(this, line);

                Exist = true;
            }
            else
                FFmpegLogger.Log(this, "There is no audio.");
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Initializing error: ", ex.Message);
        }
    }

    private void InitFilter(string filters)
    {
        AVFilterInOut* pOutputs = null;

        AVFilterInOut* pInputs = null;

        try
        {
            if (_pCodecContext == null)
            {
                FFmpegLogger.LogErr(this, "Codec context is null!");
                return;
            }

            _pFilterGraph = ffmpeg.avfilter_graph_alloc();

            if (_pFilterGraph == null)
            {
                FFmpegLogger.LogErr(this, "Allocate filter graph error");
                return;
            }

            int initResult;

            string args =
                $"time_base={_pCodecContext->time_base.num}/{_pCodecContext->time_base.den}:" +
                $"sample_rate={_pCodecContext->sample_rate}:" +
                $"sample_fmt={ffmpeg.av_get_sample_fmt_name(_pCodecContext->sample_fmt)}:" +
                $"channel_layout={FFmpegHelper.GetChannelLayoutString(&_pCodecContext->ch_layout)}";

            AVFilter* pABuffer = ffmpeg.avfilter_get_by_name("abuffer");

            fixed (AVFilterContext** ppBufferSrcCtx = &_pBufferSrcCtx)
                initResult = ffmpeg.avfilter_graph_create_filter(ppBufferSrcCtx, pABuffer, "in", args, null, _pFilterGraph);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Creating buffer source error: " + FFmpegHelper.AVStringError(initResult));
                return;
            }

            AVFilter* pABufferSink = ffmpeg.avfilter_get_by_name("abuffersink");

            fixed (AVFilterContext** ppBufferSinkCtx = &_pBufferSinkCtx)
                initResult = ffmpeg.avfilter_graph_create_filter(ppBufferSinkCtx, pABufferSink, "out", null, null, _pFilterGraph);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Creating buffer sink error: " + FFmpegHelper.AVStringError(initResult));
                return;
            }

            pOutputs = ffmpeg.avfilter_inout_alloc();

            if (pOutputs == null)
            {
                FFmpegLogger.LogErr(this, "Allocate outputs filter error");
                return;
            }

            pOutputs->name = ffmpeg.av_strdup("in");
            pOutputs->filter_ctx = _pBufferSrcCtx;
            pOutputs->pad_idx = 0;
            pOutputs->next = null;

            pInputs = ffmpeg.avfilter_inout_alloc();

            if (pOutputs == null)
            {
                FFmpegLogger.LogErr(this, "Allocate inputs filter error");
                return;
            }

            pInputs->name = ffmpeg.av_strdup("out");
            pInputs->filter_ctx = _pBufferSinkCtx;
            pInputs->pad_idx = 0;
            pInputs->next = null;

            initResult = ffmpeg.avfilter_graph_parse_ptr(_pFilterGraph, filters, &pInputs, &pOutputs, null);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Parsing filter graph error: " + FFmpegHelper.AVStringError(initResult));
                return;
            }

            initResult = ffmpeg.avfilter_graph_config(_pFilterGraph, null);

            if (initResult < 0)
            {
                FFmpegLogger.LogErr(this, "Configuring filter graph error: " + FFmpegHelper.AVStringError(initResult));
                return;
            }
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Initializing filter error: " + ex.Message);
        }
        finally
        {
            if (pInputs != null)
                ffmpeg.avfilter_inout_free(&pInputs);

            if (pOutputs != null)
                ffmpeg.avfilter_inout_free(&pOutputs);
        }
    }

    private void FlushFilter()
    {
        try
        {
            if (_pBufferSrcCtx != null)
            {
                int flushResult = ffmpeg.av_buffersrc_add_frame_flags(_pBufferSrcCtx, null, FFmpegFlags.AV_BUFFERSRC_FLAG_PUSH);

                if (flushResult < 0)
                    FFmpegLogger.LogErr(this, "Flush audio filter error: " + FFmpegHelper.AVStringError(flushResult));
                else
                {
                    int drainResult;

                    while (true)
                    {
                        drainResult = ffmpeg.av_buffersink_get_frame(_pBufferSinkCtx, _pFilteredFrame);

                        if (drainResult < 0)
                            break;

                        if (_pFilteredFrame->nb_samples > 0)
                        {
                            // It just a guess not always corrected
                            _lastFrameTime += 1.0 * _pFilteredFrame->nb_samples / SampleRate * _lastOutputPitch * _lastOutputSpeed;
                            OnFilterDrain?.Invoke((*_pFilteredFrame, _lastFrameTime, _lastOutputPitch, _lastOutputSpeed));
                        }

                        ffmpeg.av_frame_unref(_pFilteredFrame);
                    }

                    if (drainResult < 0 && drainResult != ffmpeg.AVERROR(ffmpeg.EAGAIN) && drainResult != ffmpeg.AVERROR_EOF)
                        FFmpegLogger.LogErr(this, "Error draining filter sink: " + FFmpegHelper.AVStringError(drainResult));
                }
            }
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Flushing filter error: " + ex.Message);
        }
    }

    private void DisposeFilter()
    {
        try
        {
            if (_pBufferSinkCtx != null)
            {
                ffmpeg.avfilter_free(_pBufferSinkCtx);
                _pBufferSinkCtx = null;
            }

            if (_pBufferSrcCtx != null)
            {
                ffmpeg.avfilter_free(_pBufferSrcCtx);
                _pBufferSrcCtx = null;
            }

            if (_pFilterGraph != null)
            {
                fixed (AVFilterGraph** ppFilterGraph = &_pFilterGraph)
                    ffmpeg.avfilter_graph_free(ppFilterGraph);

                _pFilterGraph = null;
            }
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Disposing filter error: " + ex.Message);
        }
    }

    private static string GetOutputPitchFilters(int sampleRate, float pitch)
    {
        int newRate = (int)(sampleRate * pitch);
        return $"asetrate={newRate},aresample={sampleRate}";
    }

    private static string GetOutputSpeedFilters(float speed)
    {
        List<string> filters = [];

        float remaining = speed;

        while (remaining < 0.5f)
        {
            filters.Add("atempo=0.5");
            remaining /= 0.5f;
        }

        while (remaining > 2.0f)
        {
            filters.Add("atempo=2");
            remaining /= 2.0f;
        }

        filters.Add($"atempo={remaining.ToString(CultureInfo.InvariantCulture)}");

        return string.Join(",", filters);
    }

    private static string GetOutputFormatFilters()
    {
        return $"aformat=sample_fmts=flt:channel_layouts=stereo";
    }

    private void UpdateFilter()
    {
        lock (_filterLock)
        {
            FlushFilter();

            DisposeFilter();

            string filters = $"{GetOutputPitchFilters(SampleRate, _outputPitch)},{GetOutputSpeedFilters(_outputSpeed)},{GetOutputFormatFilters()}";

            InitFilter(filters);
        }
    }

    public void SetOutputPitch(float pitch)
    {
        if (_outputPitch == pitch)
            return;

        _lastOutputPitch = _outputPitch;

        _outputPitch = pitch;

        UpdateFilter();
    }

    public void SetOutputSpeed(float speed)
    {
        if (_outputSpeed == speed)
            return;

        _lastOutputSpeed = _outputSpeed;

        _outputSpeed = speed;

        UpdateFilter();
    }

    private bool TryGetNextPacket()
    {
        try
        {
            int readResult = ffmpeg.av_read_frame(_pFormatContext, _pPacket);

            if (readResult >= 0)
            {
                bool success = true;

                if (_pPacket->stream_index == _streamIndex)
                {
                    int sendResult = ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket);

                    if (sendResult < 0 && sendResult != ffmpeg.AVERROR_INVALIDDATA && sendResult != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    {
                        FFmpegLogger.LogErr(this, $"Sending packet error: {FFmpegHelper.AVStringError(sendResult)}");
                        success = false;
                    }
                }

                ffmpeg.av_packet_unref(_pPacket);

                return success;
            }
            else if (readResult == ffmpeg.AVERROR_EOF)
            {
                int flushResult = ffmpeg.avcodec_send_packet(_pCodecContext, null);

                if (flushResult < 0 && flushResult != ffmpeg.AVERROR_EOF)
                {
                    FFmpegLogger.LogErr(this, $"Flushing packet error: {FFmpegHelper.AVStringError(flushResult)}");
                    return false;
                }

                return true;
            }

            FFmpegLogger.LogErr(this, $"Reading frame error: {FFmpegHelper.AVStringError(readResult)}");

            return false;
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, $"Try get next packet error: {ex.Message}");
            return false;
        }
    }

    public bool TryGetNextFrame(out AVFrame outFrame, out double outFrameTimeInSeconds, out float outFramePitch, out float outFrameSpeed)
    {
        outFrame = default;

        outFrameTimeInSeconds = 0.0;

        outFramePitch = 0.0f;

        outFrameSpeed = 0.0f;

        if (!Exist)
            return false;

        lock (_seekLock)
            lock (_filterLock)
            {
                try
                {
                    ffmpeg.av_frame_unref(_pFrame);

                    ffmpeg.av_frame_unref(_pFilteredFrame);

                    while (true)
                    {
                        int receiveResult = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);

                        if (receiveResult >= 0)
                        {
                            int filterResult = ffmpeg.av_buffersrc_add_frame_flags(
                                _pBufferSrcCtx,
                                _pFrame,
                                FFmpegFlags.AV_BUFFERSRC_FLAG_KEEP_REF | FFmpegFlags.AV_BUFFERSRC_FLAG_NO_CHECK_FORMAT
                            );

                            if (filterResult < 0)
                            {
                                FFmpegLogger.LogErr(this, $"Add frame to filter error: {FFmpegHelper.AVStringError(filterResult)}");
                                return false;
                            }

                            filterResult = ffmpeg.av_buffersink_get_frame(_pBufferSinkCtx, _pFilteredFrame);

                            if (filterResult < 0)
                                return false;

                            outFrame = *_pFilteredFrame;

                            if (_pFilteredFrame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE)
                                _pFilteredFrame->pts = _pFilteredFrame->best_effort_timestamp;

                            outFrameTimeInSeconds = _pFilteredFrame->pts * TimeBase - StartTimeInSeconds;

                            _lastFrameTime = outFrameTimeInSeconds;

                            outFramePitch = _outputPitch;

                            outFrameSpeed = _outputSpeed;

                            return true;
                        }
                        else if (receiveResult == ffmpeg.AVERROR_EOF)
                        {
                            int filterResult = ffmpeg.av_buffersrc_add_frame_flags(_pBufferSrcCtx, null, FFmpegFlags.AV_BUFFERSRC_FLAG_PUSH);

                            if (filterResult >= 0)
                            {
                                filterResult = ffmpeg.av_buffersink_get_frame(_pBufferSinkCtx, _pFilteredFrame);

                                if (filterResult >= 0)
                                {
                                    outFrame = *_pFilteredFrame;

                                    outFrameTimeInSeconds = DurationInSeconds - StartTimeInSeconds;

                                    _lastFrameTime = outFrameTimeInSeconds;

                                    outFramePitch = _outputPitch;

                                    outFrameSpeed = _outputSpeed;

                                    return true;
                                }
                            }

                            return false;
                        }
                        else if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                        {
                            if (!_codecFlushed && !TryGetNextPacket())
                            {
                                _codecFlushed = true;
                                continue;
                            }
                            else if (_codecFlushed)
                                return false;

                            continue;
                        }

                        FFmpegLogger.LogErr(this, $"Receiving frame error: {FFmpegHelper.AVStringError(receiveResult)}");

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    FFmpegLogger.LogErr(this, $"Try get next frame error: {ex.Message}");
                    return false;
                }
            }
    }

    public bool TrySeek(double timeInSeconds)
    {
        if (!Exist)
            return false;

        lock (_seekLock)
        {
            try
            {
                ffmpeg.avcodec_flush_buffers(_pCodecContext);

                _codecFlushed = false;

                var targetSec = Math.Clamp(timeInSeconds + StartTimeInSeconds, 0.0, DurationInSeconds);

                long ts = (long)(targetSec / TimeBase);

                var flags = ffmpeg.AVSEEK_FLAG_BACKWARD;

                int seekResult = ffmpeg.av_seek_frame(_pFormatContext, _streamIndex, ts, flags);

                if (seekResult < 0)
                    seekResult = ffmpeg.avformat_seek_file(_pFormatContext, _streamIndex, long.MinValue, ts, long.MaxValue, flags);

                if (seekResult < 0)
                {
                    FFmpegLogger.LogErr(this, $"Seeking error: {FFmpegHelper.AVStringError(seekResult)}");
                    return false;
                }

                UpdateFilter();

                while (TryGetNextFrame(out var frame, out var frameSec, out _, out _))
                    if (frameSec >= timeInSeconds)
                        break;

                return true;
            }
            catch (Exception ex)
            {
                FFmpegLogger.LogErr(this, $"Seeking error: {ex.Message}");
                return false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (_pFilteredFrame != null)
            {
                fixed (AVFrame** ppFilteredFrame = &_pFilteredFrame)
                    ffmpeg.av_frame_free(ppFilteredFrame);

                _pFilteredFrame = null;
            }

            DisposeFilter();

            if (_pFrame != null)
            {
                fixed (AVFrame** ppFrame = &_pFrame)
                    ffmpeg.av_frame_free(ppFrame);

                _pFrame = null;
            }

            if (_pPacket != null)
            {
                fixed (AVPacket** ppPacket = &_pPacket)
                    ffmpeg.av_packet_free(ppPacket);

                _pPacket = null;
            }

            if (_pCodecContext != null)
            {
                fixed (AVCodecContext** ppCodecContext = &_pCodecContext)
                    ffmpeg.avcodec_free_context(ppCodecContext);

                _pCodecContext = null;
            }

            if (_pFormatContext != null)
            {
                fixed (AVFormatContext** ppFormatContext = &_pFormatContext)
                    ffmpeg.avformat_close_input(ppFormatContext);

                ffmpeg.avformat_free_context(_pFormatContext);

                _pFormatContext = null;
            }

            _avioHandler?.Dispose();

            _avioHandler = null;
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Disposing error: " + ex.Message);
        }
        finally
        {
            _filterLock = null;
            _seekLock = null;
        }
    }
}