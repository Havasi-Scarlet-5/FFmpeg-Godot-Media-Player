using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.AutoGen.Abstractions;

namespace FFmpegMediaPlayer;

public sealed unsafe class FFmpegVideoDecoder : IDisposable
{
    private FFmpegAVIOHandler _avioHandler = null;

    private AVFormatContext* _pFormatContext = null;

    private readonly int _streamIndex = -1;

    private readonly AVStream* _pStream = null;

    private AVCodecContext* _pCodecContext = null;

    public string CodecName { get; } = string.Empty;

    public bool IsThumbnail { get; } = false;

    public Size Resolution { get; } = Size.Empty;

    public AVPixelFormat PixelFormat { get; } = AVPixelFormat.AV_PIX_FMT_NONE;

    public AVColorSpace ColorSpace { get; } = AVColorSpace.AVCOL_SPC_UNSPECIFIED;

    public double TimeBase { get; } = 0.0;

    public double StartTimeInSeconds { get; } = 0.0;

    public double DurationInSeconds { get; } = 0.0;

    public double FrameRate { get; } = 0.0;

    public long FrameCount { get; } = 0;

    public long BitRate { get; } = 0;

    private AVPacket* _pPacket = null;

    private AVFrame* _pFrame = null;

    private SwsContext* _pSwsContext = null;

    private readonly byte* _convertedFrameBuffer = null;

    private byte_ptr4 _destinationData = default;

    private int4 _destinationLinesize = default;

    public bool Exist { get; } = false;

    private bool _codecFlushed = false;

    private object _seekLock = new();

    private CancellationTokenSource _seekCTS = null;

    public Action<AVFrame> SeekCompleted = null;

    private bool _disposed = false;

    public FFmpegVideoDecoder(FFmpegMediaSource source)
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

            _streamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &pCodec, 0);

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

                _pCodecContext->thread_count = Math.Min(Environment.ProcessorCount, pCodec->id == AVCodecID.AV_CODEC_ID_HEVC ? 32 : 16);

                _pCodecContext->thread_type = ffmpeg.FF_THREAD_FRAME | ffmpeg.FF_THREAD_SLICE;

                initResult = ffmpeg.avcodec_open2(_pCodecContext, pCodec, null);

                if (initResult < 0)
                {
                    FFmpegLogger.LogErr(this, "Open codec error: ", FFmpegHelper.AVStringError(initResult));

                    Dispose();

                    return;
                }

                CodecName = ffmpeg.avcodec_get_name(pCodec->id).ToUpper();

                IsThumbnail = IsImageCodec(CodecName);

                Resolution = new Size(_pCodecContext->width, _pCodecContext->height);

                PixelFormat = _pCodecContext->pix_fmt;

                ColorSpace = _pCodecContext->colorspace;

                TimeBase = ffmpeg.av_q2d(_pStream->time_base);

                if (_pStream->start_time != ffmpeg.AV_NOPTS_VALUE)
                {
                    _pStream->start_time = _pStream->start_time;
                    StartTimeInSeconds = 1.0 * _pStream->start_time * TimeBase;
                }
                else if (_pFormatContext->start_time != ffmpeg.AV_NOPTS_VALUE)
                {
                    _pFormatContext->start_time = _pFormatContext->start_time;
                    StartTimeInSeconds = 1.0 * _pFormatContext->start_time / ffmpeg.AV_TIME_BASE;
                }

                if (_pStream->duration != ffmpeg.AV_NOPTS_VALUE)
                {
                    _pStream->duration = _pStream->duration;
                    DurationInSeconds = 1.0 * _pStream->duration * TimeBase;
                }
                else if (_pFormatContext->duration != ffmpeg.AV_NOPTS_VALUE)
                {
                    _pFormatContext->duration = _pFormatContext->duration;
                    DurationInSeconds = 1.0 * _pFormatContext->duration / ffmpeg.AV_TIME_BASE;
                }
                else if (!IsThumbnail)
                    FFmpegLogger.LogErr(this, "Cannot get duration!.");

                FrameRate = ffmpeg.av_q2d(_pStream->avg_frame_rate);

                if (double.IsNaN(FrameRate) || double.IsInfinity(FrameRate) || FrameRate <= 0.0)
                    FrameRate = ffmpeg.av_q2d(_pStream->r_frame_rate);

                if (double.IsNaN(FrameRate) || double.IsInfinity(FrameRate) || FrameRate <= 0.0)
                {
                    var guessFrameRate = ffmpeg.av_guess_frame_rate(_pFormatContext, _pStream, null);
                    FrameRate = ffmpeg.av_q2d(guessFrameRate);
                }

                if (double.IsNaN(FrameRate) || double.IsInfinity(FrameRate) || FrameRate <= 0.0)
                    FrameRate = 0.0;

                if (FrameRate <= 0.0 && !IsThumbnail)
                    FFmpegLogger.LogErr(this, "Cannot get frame rate!.");

                FrameCount = _pStream->nb_frames;

                if (FrameCount <= 0)
                    FrameCount = (long)Math.Round(DurationInSeconds * FrameRate);

                if (FrameCount <= 0 && !IsThumbnail)
                    FFmpegLogger.LogErr(this, "Cannot get frame count!.");

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

                if (PixelFormat != AVPixelFormat.AV_PIX_FMT_YUV420P)
                {
                    _pSwsContext = ffmpeg.sws_getContext(
                        Resolution.Width,
                        Resolution.Height,
                        PixelFormat,
                        Resolution.Width,
                        Resolution.Height,
                        AVPixelFormat.AV_PIX_FMT_YUV420P,
                        ffmpeg.SWS_FAST_BILINEAR,
                        null,
                        null,
                        null
                    );

                    if (_pSwsContext == null)
                    {
                        FFmpegLogger.LogErr(this, "Failed to alloc sws context");

                        Dispose();

                        return;
                    }

                    int bufferSize = ffmpeg.av_image_get_buffer_size(
                        AVPixelFormat.AV_PIX_FMT_YUV420P,
                        Resolution.Width,
                        Resolution.Height,
                        1
                    );

                    if (bufferSize < 0)
                    {
                        FFmpegLogger.LogErr(this, "Failed to get image buffer size");

                        Dispose();

                        return;
                    }

                    _convertedFrameBuffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);

                    if (_convertedFrameBuffer == null)
                    {
                        FFmpegLogger.LogErr(this, "Failed to allocate converted buffer");

                        Dispose();

                        return;
                    }

                    _destinationData = new byte_ptr4();

                    _destinationLinesize = new int4();

                    initResult = ffmpeg.av_image_fill_arrays(
                        ref _destinationData,
                        ref _destinationLinesize,
                        _convertedFrameBuffer,
                        AVPixelFormat.AV_PIX_FMT_YUV420P,
                        Resolution.Width,
                        Resolution.Height,
                        1
                    );

                    if (initResult < 0)
                    {
                        FFmpegLogger.LogErr(this, "Failed to fill image arrays");

                        Dispose();

                        return;
                    }
                }

                var line = "";

                for (int i = 0; i < 100; i++)
                    line += "-";

                FFmpegLogger.Log(this, line);

                FFmpegLogger.Log(this, "Codec Name: ", CodecName);

                FFmpegLogger.Log(this, "Is Thumbnail: ", IsThumbnail);

                FFmpegLogger.Log(this, "Resolution: ", $"{(Resolution != Size.Empty ? Resolution.Width + " x " + Resolution.Height : "UNKNOWN")}");

                FFmpegLogger.Log(this, "Pixel Format Name: ", ffmpeg.av_get_pix_fmt_name(PixelFormat).ToUpper() ?? "UNKNOWN");

                FFmpegLogger.Log(this, "Color Space Name: ", ffmpeg.av_color_space_name(ColorSpace).ToUpper() ?? "UNKNOWN");

                FFmpegLogger.Log(this, "Time Base: ", TimeBase > 0.0 ? TimeBase : "UNKNOWN");

                FFmpegLogger.Log(this, "Start Time In Seconds: ", StartTimeInSeconds);

                FFmpegLogger.Log(this, "Duration In Seconds: ", DurationInSeconds > 0.0 ? DurationInSeconds : "UNKNOWN");

                FFmpegLogger.Log(this, "Frame Rate: ", FrameRate > 0.0 ? FrameRate : "UNKNOWN");

                FFmpegLogger.Log(this, "Frame Count: ", FrameCount > 0 ? FrameCount : "UNKNOWN");

                FFmpegLogger.Log(this, "Bit Rate: ", BitRate > 0 ? BitRate : "UNKNOWN");

                FFmpegLogger.Log(this, line);

                Exist = true;
            }
            else
                FFmpegLogger.Log(this, "There is no video.");
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Initializing error: ", ex.Message);
        }
    }

    private static bool IsImageCodec(string name)
    {
        string[] keywords = [
            "image", "bmp", "png", "jpeg", "jpg", "tiff", "gif", "webp", "exr",
            "dds", "sgi", "pbm", "ppm", "pgm", "qoi", "pcx", "sunrast", "targa",
            "xpm", "xbm", "psd", "svg", "pam", "pgmyuv", "pfm", "txd", "vbn"
        ];

        foreach (var key in keywords)
            if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
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

    public bool TryGetNextFrame(out AVFrame outFrame, out double outFrameTimeInSeconds)
    {
        outFrame = default;

        outFrameTimeInSeconds = 0.0;

        if (!Exist)
            return false;

        lock (_seekLock)
        {
            try
            {
                ffmpeg.av_frame_unref(_pFrame);

                while (true)
                {
                    int receiveResult = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);

                    if (receiveResult >= 0)
                    {
                        if (PixelFormat != AVPixelFormat.AV_PIX_FMT_YUV420P)
                        {
                            int scaleResult = ffmpeg.sws_scale(
                                _pSwsContext,
                                _pFrame->data,
                                _pFrame->linesize,
                                0,
                                _pFrame->height,
                                _destinationData,
                                _destinationLinesize
                            );

                            if (scaleResult < 0)
                            {
                                FFmpegLogger.LogErr(this, $"Failed to scale frame: {FFmpegHelper.AVStringError(scaleResult)}");
                                return false;
                            }

                            _pFrame->data.UpdateFrom(_destinationData);

                            _pFrame->linesize.UpdateFrom(_destinationLinesize);

                            _pFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
                        }

                        outFrame = *_pFrame;

                        if (_pFrame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE)
                            _pFrame->pts = _pFrame->best_effort_timestamp;

                        outFrameTimeInSeconds = _pFrame->pts * TimeBase - StartTimeInSeconds;

                        return true;
                    }
                    else if (receiveResult == ffmpeg.AVERROR_EOF)
                        return false;
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

    public bool TrySeek(double timeInSeconds, CancellationToken cancellationToken = default)
    {
        if (!Exist || IsThumbnail)
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

                AVFrame? lastFrame = null;

                bool foundFrame = false;

                while (!cancellationToken.IsCancellationRequested && TryGetNextFrame(out var frame, out double frameSec))
                {
                    lastFrame = frame;

                    foundFrame = true;

                    if (frameSec >= timeInSeconds)
                        break;
                }

                if (!cancellationToken.IsCancellationRequested && foundFrame && lastFrame.HasValue)
                    SeekCompleted?.Invoke(lastFrame.Value);

                return foundFrame;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                FFmpegLogger.LogErr(this, $"Seeking error: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Unlike audio, in video seeking is a heavy task so async is here for you.
    /// Return the task result when seeking
    /// </summary>
    public Task<bool> TrySeekAsync(double second)
    {
        try
        {
            _seekCTS?.Cancel();

            _seekCTS?.Dispose();

            _seekCTS = new CancellationTokenSource();

            return Task.Run(() =>
                {
                    try
                    {
                        return TrySeek(second, _seekCTS.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                },
                _seekCTS.Token
            );
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Try seek async error: ", ex.Message);
            return Task.FromResult(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Clean up, or we will fucking crash ?
            lock (_seekLock)
            {
                _seekCTS?.Cancel();

                _seekCTS?.Dispose();

                _seekCTS = null;
            }

            if (_convertedFrameBuffer != null)
                ffmpeg.av_free(_convertedFrameBuffer);

            if (_pSwsContext != null)
            {
                ffmpeg.sws_freeContext(_pSwsContext);
                _pSwsContext = null;
            }

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
            FFmpegLogger.LogErr(this, "Disposing error: ", ex.Message);
        }
        finally
        {
            _seekLock = null;
        }
    }
}