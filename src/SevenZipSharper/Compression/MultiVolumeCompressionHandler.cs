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
internal sealed partial class MultiVolumeCompressionHandler
    : CompressionHandlerBase,
        IArchiveUpdateCallback2
{
    private readonly Func<int, Stream> _volumeStreamFactory;
    private readonly ulong _maxVolumeBytes;

    internal MultiVolumeCompressionHandler(
        IReadOnlyList<(string EntryPath, Stream Data)> entries,
        IProgress<CompressionProgress>? progress,
        Func<int, Stream> volumeStreamFactory,
        ulong maxVolumeBytes,
        CancellationToken cancellationToken
    )
        : base(entries, progress, cancellationToken)
    {
        _volumeStreamFactory = volumeStreamFactory;
        _maxVolumeBytes = maxVolumeBytes;
    }

    int IProgress.SetTotal(ulong total) => OnSetTotal(total);

    int IProgress.SetCompleted(nint completeValue) => OnSetCompleted(completeValue);

    int IArchiveUpdateCallback.GetUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    ) => OnGetUpdateItemInfo(index, newData, newProperties, indexInArchive);

    int IArchiveUpdateCallback.GetProperty(uint index, ItemPropId propId, ref PropVariant value) =>
        OnGetProperty(index, propId, ref value);

    int IArchiveUpdateCallback.GetStream(uint index, out ISequentialInStream? inStream) =>
        OnGetStream(index, out inStream);

    int IArchiveUpdateCallback.SetOperationResult(OperationResult result) =>
        OnSetOperationResult(result);

    int IArchiveUpdateCallback2.GetVolumeSize(uint index, nint size)
    {
        if (size != nint.Zero)
            Marshal.WriteInt64(size, (long)_maxVolumeBytes);
        return HResult.Ok;
    }

    int IArchiveUpdateCallback2.GetVolumeStream(uint index, out IOutStream? volumeStream)
    {
        var stream = _volumeStreamFactory((int)index);
        volumeStream = new OutStreamAdapter(stream);
        return HResult.Ok;
    }
}
