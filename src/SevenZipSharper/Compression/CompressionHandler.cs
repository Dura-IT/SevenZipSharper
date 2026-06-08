using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Compression;

[GeneratedComClass]
internal sealed partial class CompressionHandler : CompressionHandlerBase, IArchiveUpdateCallback
{
    internal CompressionHandler(
        IReadOnlyList<(string EntryPath, Stream Data)> entries,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken,
        bool ownsEntryStreams = false
    )
        : base(entries, progress, cancellationToken, ownsEntryStreams) { }

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
}
