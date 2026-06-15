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
    private readonly uint _existingCount;
    private readonly string? _password;

    private ulong _totalBytes;
    private int _completedEntries;
    private Stream? _activeEntryStream;

    protected CompressionHandlerBase(
        IReadOnlyList<(string EntryPath, Stream Data)> entries,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken,
        bool ownsEntryStreams = false,
        uint existingCount = 0,
        string? password = null
    )
    {
        _entries = entries;
        _progress = progress;
        _cancellationToken = cancellationToken;
        _ownsEntryStreams = ownsEntryStreams;
        _existingCount = existingCount;
        _password = password;
    }

    protected virtual int OnGetExistingUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    ) => HResult.Ok;

    protected virtual int OnGetExistingProperty(
        uint index,
        ItemPropId propId,
        ref PropVariant value
    ) => HResult.Ok;

    protected virtual int OnGetExistingStream(uint index, out ISequentialInStream? inStream)
    {
        inStream = null;
        return HResult.Ok;
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
        var newIndex = _completedEntries - (int)_existingCount;
        var entryPath =
            newIndex >= 0 && newIndex < _entries.Count
                ? _entries[newIndex].EntryPath
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

    protected int OnGetUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    )
    {
        if (index < _existingCount)
            return OnGetExistingUpdateItemInfo(index, newData, newProperties, indexInArchive);

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
        value.Clear();

        if (index < _existingCount)
            return OnGetExistingProperty(index, propId, ref value);

        var adjustedIndex = (int)(index - _existingCount);
        if (adjustedIndex >= _entries.Count)
            return HResult.InvalidArg;

        var (entryPath, data) = _entries[adjustedIndex];

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

        if (index < _existingCount)
            return OnGetExistingStream(index, out inStream);

        var adjustedIndex = (int)(index - _existingCount);
        if (adjustedIndex >= _entries.Count)
            return HResult.InvalidArg;

        var data = _entries[adjustedIndex].Data;
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

    protected int OnGetPassword(out int passwordIsDefined, out string password)
    {
        if (_password is null)
        {
            passwordIsDefined = 0;
            password = string.Empty;
            return HResult.Ok;
        }

        passwordIsDefined = 1;
        password = _password;
        return HResult.Ok;
    }
}
