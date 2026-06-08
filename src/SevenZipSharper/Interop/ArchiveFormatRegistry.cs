using System;
using System.Collections.Generic;

namespace SevenZipSharper.Interop;

internal static class ArchiveFormatRegistry
{
    private static readonly Dictionary<ArchiveFormat, Guid> ClassIds = new()
    {
        [ArchiveFormat.SevenZip] = SevenZipLib.SevenZipClassId,
        [ArchiveFormat.Zip] = SevenZipLib.ZipClassId,
        [ArchiveFormat.BZip2] = SevenZipLib.BZip2ClassId,
        [ArchiveFormat.Arj] = SevenZipLib.ArjClassId,
        [ArchiveFormat.Lzh] = SevenZipLib.LzhClassId,
        [ArchiveFormat.Cab] = SevenZipLib.CabClassId,
        [ArchiveFormat.Iso] = SevenZipLib.IsoClassId,
        [ArchiveFormat.GZip] = SevenZipLib.GZipClassId,
        [ArchiveFormat.Tar] = SevenZipLib.TarClassId,
        [ArchiveFormat.Xz] = SevenZipLib.XzClassId,
        [ArchiveFormat.Wim] = SevenZipLib.WimClassId,
    };

    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a recognised value.</exception>
    internal static Guid GetClassId(ArchiveFormat format)
    {
        if (ClassIds.TryGetValue(format, out var classId))
            return classId;

        throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown archive format.");
    }
}
