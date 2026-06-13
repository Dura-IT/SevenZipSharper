using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SevenZipSharper.Detection;

internal static class ArchiveFormatDetector
{
    private static readonly (ArchiveFormat Format, int Offset, byte[] Bytes)[] Signatures =
    [
        (ArchiveFormat.Wim, 0, [0x4D, 0x53, 0x57, 0x49, 0x4D, 0x00, 0x00, 0x00]),
        (ArchiveFormat.SevenZip, 0, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]),
        (ArchiveFormat.Xz, 0, [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00]),
        (ArchiveFormat.Cab, 0, [0x4D, 0x53, 0x43, 0x46]),
        (ArchiveFormat.BZip2, 0, [0x42, 0x5A, 0x68]),
        // Lzh: '-lh' at offset 2 (e.g. -lh5-, -lhd-)
        (ArchiveFormat.Lzh, 2, [0x2D, 0x6C, 0x68]),
        (ArchiveFormat.Zip, 0, [0x50, 0x4B]),
        (ArchiveFormat.GZip, 0, [0x1F, 0x8B]),
        (ArchiveFormat.Arj, 0, [0x60, 0xEA]),
        // POSIX/GNU TAR: 'ustar' at offset 257
        (ArchiveFormat.Tar, 257, [0x75, 0x73, 0x74, 0x61, 0x72]),
    ];

    // Covers all signatures above: TAR offset 257 + 5 bytes = 262.
    private const int BufferSize = 262;

    private static readonly Dictionary<string, ArchiveFormat> ExtensionMap = new Dictionary<
        string,
        ArchiveFormat
    >(StringComparer.OrdinalIgnoreCase)
    {
        [".7z"] = ArchiveFormat.SevenZip,
        [".zip"] = ArchiveFormat.Zip,
        [".jar"] = ArchiveFormat.Zip,
        [".epub"] = ArchiveFormat.Zip,
        [".apk"] = ArchiveFormat.Zip,
        [".gz"] = ArchiveFormat.GZip,
        [".tgz"] = ArchiveFormat.GZip,
        [".bz2"] = ArchiveFormat.BZip2,
        [".tbz"] = ArchiveFormat.BZip2,
        [".tbz2"] = ArchiveFormat.BZip2,
        [".tar"] = ArchiveFormat.Tar,
        [".iso"] = ArchiveFormat.Iso,
        [".cab"] = ArchiveFormat.Cab,
        [".arj"] = ArchiveFormat.Arj,
        [".lzh"] = ArchiveFormat.Lzh,
        [".lha"] = ArchiveFormat.Lzh,
        [".xz"] = ArchiveFormat.Xz,
        [".txz"] = ArchiveFormat.Xz,
        [".wim"] = ArchiveFormat.Wim,
        [".swm"] = ArchiveFormat.Wim,
        [".esd"] = ArchiveFormat.Wim,
    };

    internal static ArchiveFormat? FromExtension(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return null;

        return ExtensionMap.TryGetValue(ext, out var format) ? format : null;
    }

    // ISO detection requires reading 32 KB into the stream (CD001 signature at offset 32769)
    // and is not attempted here. Use FromExtension for ISO.
    internal static async Task<ArchiveFormat?> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var originalPosition = stream.CanSeek ? stream.Position : -1L;

        var buffer = new byte[BufferSize];
        var bytesRead = await ReadFullyAsync(stream, buffer, cancellationToken)
            .ConfigureAwait(false);

        if (stream.CanSeek)
            stream.Position = originalPosition;

        if (bytesRead == 0)
            return null;

        return MatchSignature(buffer, bytesRead);
    }

    private static ArchiveFormat? MatchSignature(byte[] buffer, int length)
    {
        var span = buffer.AsSpan(0, length);

        foreach (var (format, offset, bytes) in Signatures)
        {
            if (
                span.Length >= offset + bytes.Length
                && span.Slice(offset, bytes.Length).SequenceEqual(bytes)
            )
                return format;
        }

        return null;
    }

    private static async Task<int> ReadFullyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream
                .ReadAsync(buffer.AsMemory(totalRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }
}
