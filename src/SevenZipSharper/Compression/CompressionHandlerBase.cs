using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Compression;

internal abstract class CompressionHandlerBase
{
    private readonly IReadOnlyList<(string EntryPath, Stream Data)> _entries;
    private readonly IProgress<CompressionProgress>? _progress;
    private readonly CancellationToken _cancellationToken;
    private readonly bool _ownsEntryStreams;

    private ulong _totalBytes;
    private int _completedEntries;
    private Stream? _activeEntryStream;

    protected CompressionHandlerBase(
        IReadOnlyList<(string EntryPath, Stream Data)> entries,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken,
        bool ownsEntryStreams = false
    )
    {
        _entries = entries;
        _progress = progress;
        _cancellationToken = cancellationToken;
        _ownsEntryStreams = ownsEntryStreams;
    }

    protected int OnSetTotal(ulong total)
    {
        _totalBytes = total;
        return HResult.Ok;
    }

    protected int OnSetCompleted(nint completeValue)
    {
        if (_cancellationToken.IsCancellationRequested)
            return HResult.Abort;

        if (completeValue == nint.Zero)
            return HResult.Ok;

        var bytesProcessed = (ulong)Marshal.ReadInt64(completeValue);
        var entryPath =
            _completedEntries < _entries.Count
                ? _entries[_completedEntries].EntryPath
                : string.Empty;

        _progress?.Report(
            new CompressionProgress
            {
                EntryPath = entryPath,
                BytesProcessed = bytesProcessed,
                TotalBytes = _totalBytes,
            }
        );

        return HResult.Ok;
    }

    protected static int OnGetUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    )
    {
        if (newData != nint.Zero)
            Marshal.WriteInt32(newData, 1);
        if (newProperties != nint.Zero)
            Marshal.WriteInt32(newProperties, 1);
        if (indexInArchive != nint.Zero)
            Marshal.WriteInt32(indexInArchive, unchecked((int)uint.MaxValue));
        return HResult.Ok;
    }

    protected int OnGetProperty(uint index, ItemPropId propId, ref PropVariant value)
    {
        if (index >= (uint)_entries.Count)
            return HResult.InvalidArg;

        var (entryPath, data) = _entries[(int)index];

        value.Clear();
        value = propId switch
        {
            ItemPropId.Path => PropVariant.FromString(entryPath),
            ItemPropId.Size => data.CanSeek
                ? PropVariant.FromUInt64((ulong)data.Length)
                : new PropVariant(),
            ItemPropId.IsDirectory => PropVariant.FromBoolean(false),
            ItemPropId.IsAnti => PropVariant.FromBoolean(false),
            _ => new PropVariant(),
        };

        return HResult.Ok;
    }

    protected int OnGetStream(uint index, out ISequentialInStream? inStream)
    {
        _activeEntryStream?.Dispose();
        _activeEntryStream = null;
        inStream = null;

        if (index >= (uint)_entries.Count)
            return HResult.InvalidArg;

        var data = _entries[(int)index].Data;
        if (data.CanSeek)
            data.Seek(0, SeekOrigin.Begin);
        inStream = new InStreamAdapter(data);

        if (_ownsEntryStreams)
            _activeEntryStream = data;

        return HResult.Ok;
    }

    protected int OnSetOperationResult(OperationResult result)
    {
        _completedEntries++;
        return HResult.Ok;
    }
}
