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
internal sealed partial class AppendUpdateHandler
    : CompressionHandlerBase,
        IArchiveUpdateCallback,
        ICryptoGetTextPassword2
{
    private readonly IInArchive _existingArchive;

    internal AppendUpdateHandler(
        IInArchive existingArchive,
        uint existingCount,
        IReadOnlyList<(string EntryPath, Stream Data)> newEntries,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken,
        string? password = null
    )
        : base(
            newEntries,
            progress,
            cancellationToken,
            existingCount: existingCount,
            password: password
        )
    {
        _existingArchive = existingArchive;
    }

    protected override int OnGetExistingUpdateItemInfo(
        uint index,
        nint newData,
        nint newProperties,
        nint indexInArchive
    )
    {
        if (newData != nint.Zero)
            Marshal.WriteInt32(newData, 0);
        if (newProperties != nint.Zero)
            Marshal.WriteInt32(newProperties, 0);
        if (indexInArchive != nint.Zero)
            Marshal.WriteInt32(indexInArchive, (int)index);
        return HResult.Ok;
    }

    // `value` is a managed ref into a native-provided buffer (platform-sized PROPVARIANT —
    // 24 bytes on Windows, 16 on POSIX). Forward the raw address to the in-archive's
    // GetProperty(nint) so its write lands directly in that buffer with the correct stride.
    protected override unsafe int OnGetExistingProperty(
        uint index,
        ItemPropId propId,
        ref PropVariant value
    ) =>
        _existingArchive.GetProperty(
            index,
            propId,
            (nint)System.Runtime.CompilerServices.Unsafe.AsPointer(ref value)
        );

    protected override int OnGetExistingStream(uint index, out ISequentialInStream? inStream)
    {
        inStream = null;
        return HResult.Ok;
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

    int ICryptoGetTextPassword2.CryptoGetTextPassword2(
        out int passwordIsDefined,
        out string password
    ) => OnGetPassword(out passwordIsDefined, out password);
}
