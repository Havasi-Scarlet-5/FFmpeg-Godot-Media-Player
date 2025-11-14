using System;
using System.Buffers;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace FFmpegMediaPlayer;

public sealed unsafe class FFmpegAVIOHandler : IDisposable
{
    private readonly ReadOnlyMemory<byte> _source = null;

    private MemoryHandle _pinned = default;

    private long _position = 0;

    private bool _disposed = false;

    private readonly avio_alloc_context_read_packet _read = null;

    private readonly avio_alloc_context_seek _seek = null;

    public AVIOContext* PAVIOContext = null;

    private GCHandle _handle = default;

    public FFmpegAVIOHandler(ReadOnlyMemory<byte> source)
    {
        try
        {
            if (source.Length == 0)
            {
                FFmpegLogger.LogErr(this, "Buffer is empty!");

                Dispose();

                return;
            }

            _source = source;

            _pinned = _source.Pin();

            _position = 0;

            _read = ReadPacket;

            _seek = Seek;

            int bufferSize = Math.Min(Math.Max(4096, _source.Length / 10), 64 * 1024);

            byte* buf = (byte*)ffmpeg.av_malloc((ulong)bufferSize);

            if (buf == null)
            {
                FFmpegLogger.LogErr(this, "Failed to allocate FFmpeg buffer");

                Dispose();

                return;
            }

            _handle = GCHandle.Alloc(this, GCHandleType.Normal);

            PAVIOContext = ffmpeg.avio_alloc_context(
                buf,
                bufferSize,
                0,
                GCHandle.ToIntPtr(_handle).ToPointer(),
                _read,
                null,
                _seek
            );

            if (PAVIOContext == null)
            {
                FFmpegLogger.LogErr(this, "Failed to allocate AVIOContext");

                ffmpeg.av_free(buf);

                _handle.Free();

                Dispose();
            }
        }
        catch (Exception ex)
        {
            FFmpegLogger.LogErr(this, "Initializing error: ", ex.Message);
        }
    }

    private int ReadPacket(void* opaque, byte* dst, int size)
    {
        if (_disposed || dst == null || size <= 0)
            return ffmpeg.AVERROR_EOF;

        long remain = _source.Length - _position;

        if (remain <= 0)
            return ffmpeg.AVERROR_EOF;

        int toCopy = (int)Math.Min(size, remain);

        byte* srcPtr = (byte*)_pinned.Pointer + _position;

        Buffer.MemoryCopy(srcPtr, dst, size, toCopy);

        _position += toCopy;

        return toCopy;
    }

    private long Seek(void* opaque, long offset, int whence)
    {
        if (_disposed)
            return -1;

        switch (whence)
        {
            case FFmpegFlags.SEEK_SET:
                _position = offset;
                break;

            case FFmpegFlags.SEEK_CUR:
                _position += offset;
                break;

            case FFmpegFlags.SEEK_END:
                _position = _source.Length + offset;
                break;

            case ffmpeg.AVSEEK_SIZE:
                return _source.Length;

            default:
                return -1;
        }

        _position = Math.Max(0, Math.Min(_position, _source.Length));

        return _position;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (PAVIOContext != null)
        {
            fixed (AVIOContext** ppAVIOContext = &PAVIOContext)
                ffmpeg.avio_context_free(ppAVIOContext);

            PAVIOContext = null;
        }

        if (_handle.IsAllocated)
            _handle.Free();

        _pinned.Dispose();
    }
}