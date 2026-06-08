using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Compression;

[GeneratedComClass]
internal sealed partial class AppendUpdateHandler : IArchiveUpdateCallback
{
    private readonly IInArchive _existingArchive;
    private readonly uint _existingCount;
    private readonly IReadOnlyList<(string EntryPath, Stream Data)> _newEntries;
    private readonly IProgress<CompressionProgress>? _progress;
    private readonly CancellationToken _cancellationToken;

    private ulong _totalBytes;
    private uint _completedEntries;

    internal AppendUpdateHandler(
        IInArchive existingArchive,
        uint existingCount,
        IReadOnlyList<(string EntryPath, Stream Data)> newEntries,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        _existingArchive = existingArchive;
        _existingCount = existingCount;
        _newEntries = newEntries;
        _progress = progress;
        _cancellationToken = cancellationToken;
    }

    int IProgress.SetTotal(ulong total)
    {
        _totalBytes = total;
        return HResult.Ok;
    }

    int IProgress.SetCompleted(nint completeValue)
    {
        if (_cancellationToken.IsCancellationRequested)
            return HResult.Abort;

        if (completeValue == nint.Zero)
            return HResult.Ok;

        var bytesProcessed = (ulong)Marshal.ReadInt64(completeValue);
        string entryPath = string.Empty;
        if (_completedEntries >= _existingCount)
        {
            var newIndex = (int)(_completedEntries - _existingCount);
            if (newIndex < _newEntries.Count)
                entryPath = _newEntries[newIndex].EntryPath;
        }

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

    int IArchiveUpdateCallback.GetUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    )
    {
        if (index < _existingCount)
        {
            if (newData != nint.Zero)
                Marshal.WriteInt32(newData, 0);
            if (newProperties != nint.Zero)
                Marshal.WriteInt32(newProperties, 0);
            if (indexInArchive != nint.Zero)
                Marshal.WriteInt32(indexInArchive, (int)index);
        }
        else
        {
            if (newData != nint.Zero)
                Marshal.WriteInt32(newData, 1);
            if (newProperties != nint.Zero)
                Marshal.WriteInt32(newProperties, 1);
            if (indexInArchive != nint.Zero)
                Marshal.WriteInt32(indexInArchive, unchecked((int)uint.MaxValue));
        }

        return HResult.Ok;
    }

    int IArchiveUpdateCallback.GetProperty(uint index, ItemPropId propId, ref PropVariant value)
    {
        value.Clear();

        if (index < _existingCount)
            return _existingArchive.GetProperty(index, propId, ref value);

        var newIndex = (int)(index - _existingCount);
        if (newIndex >= _newEntries.Count)
            return HResult.InvalidArg;

        var (entryPath, data) = _newEntries[newIndex];

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

    int IArchiveUpdateCallback.GetStream(uint index, out ISequentialInStream? inStream)
    {
        if (index < _existingCount)
        {
            inStream = null;
            return HResult.Ok;
        }

        var newIndex = (int)(index - _existingCount);
        if (newIndex >= _newEntries.Count)
        {
            inStream = null;
            return HResult.InvalidArg;
        }

        var data = _newEntries[newIndex].Data;
        if (data.CanSeek)
            data.Seek(0, SeekOrigin.Begin);
        inStream = new InStreamAdapter(data);
        return HResult.Ok;
    }

    int IArchiveUpdateCallback.SetOperationResult(OperationResult result)
    {
        _completedEntries++;
        return HResult.Ok;
    }
}
