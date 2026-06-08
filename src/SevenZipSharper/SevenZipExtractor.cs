using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Microsoft.Extensions.Logging;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;
using static SevenZipSharper.Extraction.SevenZipExtractorLog;

namespace SevenZipSharper;

/// <summary>
/// Reads and extracts entries from a 7-Zip-compatible archive.
/// </summary>
/// <remarks>
/// Call <see cref="OpenAsync"/> before calling any other method.
/// Dispose when done — the underlying native archive object is released in <see cref="Dispose"/>.
/// </remarks>
public sealed class SevenZipExtractor : IDisposable
{
    private readonly ArchiveFormat _format;
    private readonly ILogger<SevenZipExtractor> _logger;
    private readonly IInArchive _archive;
    private readonly InStreamAdapter _streamAdapter;
    private const string NotOpenedMessage = "Call OpenAsync before listing or extracting.";
    private int _disposed;
    private bool _opened;
    private string? _password;

    /// <summary>
    /// Initializes a new extractor for the given stream.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the archive.</param>
    /// <param name="format">Archive format of the stream.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SevenZipExtractor(Stream stream, ArchiveFormat format, ILogger<SevenZipExtractor> logger)
    {
        NativeLibraryLoader.Register();
        _format = format;
        _logger = logger;
        _streamAdapter = new InStreamAdapter(stream);
        _archive = SevenZipLib.CreateArchiveObject<IInArchive>(
            ArchiveFormatRegistry.GetClassId(format)
        );
    }

    // For unit testing — bypasses native library creation.
    internal SevenZipExtractor(
        Stream stream,
        ArchiveFormat format,
        IInArchive archive,
        ILogger<SevenZipExtractor> logger
    )
    {
        _format = format;
        _logger = logger;
        _streamAdapter = new InStreamAdapter(stream);
        _archive = archive;
    }

    /// <summary>
    /// Creates a new extractor, returning <c>Result.Fail</c> if the native library cannot be loaded.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the archive.</param>
    /// <param name="format">Archive format of the stream.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>A successful result containing the extractor, or a failed result with the error message.</returns>
    public static Result<SevenZipExtractor> Create(
        Stream stream,
        ArchiveFormat format,
        ILogger<SevenZipExtractor> logger
    )
    {
        try
        {
            return Result.Ok(new SevenZipExtractor(stream, format, logger));
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Opens the archive and reads its top-level metadata.
    /// </summary>
    /// <param name="password">Password for encrypted archives; <see langword="null"/> for unprotected archives.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result containing archive metadata, or a failed result if the archive could not be opened.</returns>
    /// <remarks>Must be called before <see cref="ListEntriesAsync"/>, <see cref="ExtractAllAsync"/>, <see cref="ExtractEntryAsync"/>, or any <c>ExtractAsync</c> overload.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result<ArchiveInfo>> OpenAsync(
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return await Task.Run(
                () =>
                {
                    _password = password;
                    var handler = new ArchiveOpenHandler(password);
                    var hr = _archive.Open(_streamAdapter, IntPtr.Zero, handler);
                    if (hr != HResult.Ok)
                    {
                        ArchiveOpenFailed(_logger, _format, hr);
                        return Result.Fail<ArchiveInfo>(
                            $"Failed to open archive (HRESULT: 0x{hr:X8})."
                        );
                    }

                    var info = ReadArchiveInfo();
                    _opened = true;
                    ArchiveOpened(_logger, _format);
                    return Result.Ok(info);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns metadata for every entry in the archive.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result containing the entry list, or a failed result if listing fails.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result<IReadOnlyList<ArchiveEntry>>> ListEntriesAsync(
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_opened)
            return Result.Fail<IReadOnlyList<ArchiveEntry>>(NotOpenedMessage);
        return await Task.Run(
                () =>
                {
                    var hr = _archive.GetNumberOfItems(out var count);
                    if (hr != HResult.Ok)
                    {
                        ListEntriesFailed(_logger, _format, hr);
                        return Result.Fail<IReadOnlyList<ArchiveEntry>>(
                            $"Failed to list archive entries (HRESULT: 0x{hr:X8})."
                        );
                    }

                    if (count > (uint)Array.MaxLength)
                        return Result.Fail<IReadOnlyList<ArchiveEntry>>(
                            $"Archive reports {count} entries, which exceeds the supported maximum."
                        );

                    var entries = new List<ArchiveEntry>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        entries.Add(ReadEntry(i));
                    }

                    return Result.Ok<IReadOnlyList<ArchiveEntry>>(entries);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts all entries in the archive to the specified output directory.
    /// </summary>
    /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
    /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result on completion, or a failed result if extraction fails or any entry has errors.</returns>
    /// <remarks>Entries whose paths resolve outside <paramref name="outputPath"/> (zip-slip) are silently skipped.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result> ExtractAllAsync(
        string outputPath,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_opened)
            return Result.Fail(NotOpenedMessage);
        return await Task.Run(
                () =>
                {
                    var hr = _archive.GetNumberOfItems(out var count);
                    if (hr != HResult.Ok)
                        return Result.Fail($"Failed to get item count (HRESULT: 0x{hr:X8}).");

                    var handler = new ExtractionHandler(
                        CreateFileEntryProvider(outputPath),
                        progress,
                        (int)count,
                        cancellationToken,
                        _password
                    );

                    hr = _archive.Extract(null, uint.MaxValue, 0, handler);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                    {
                        ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                        return Result.Fail(
                            $"Extraction had entry errors: {handler.LastEntryError}."
                        );
                    }

                    if (hr == HResult.Ok)
                    {
                        ExtractAllCompleted(_logger, _format, count);
                        return Result.Ok();
                    }

                    ExtractAllFailed(_logger, _format, hr);
                    return Result.Fail($"Extraction failed (HRESULT: 0x{hr:X8}).");
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a single archive entry to the provided stream.
    /// </summary>
    /// <param name="entry">The entry to extract; must belong to this archive.</param>
    /// <param name="outputStream">Writable stream that receives the decompressed data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result on completion, or a failed result if extraction fails or the entry has errors.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result> ExtractEntryAsync(
        ArchiveEntry entry,
        Stream outputStream,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_opened)
            return Result.Fail(NotOpenedMessage);
        return await Task.Run(
                () =>
                {
                    var indices = new uint[] { (uint)entry.Index };
                    var handler = new ExtractionHandler(
                        CreateSingleEntryProvider(entry, outputStream),
                        null,
                        1,
                        cancellationToken,
                        _password
                    );

                    var hr = _archive.Extract(indices, 1, 0, handler);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                    {
                        ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                        return Result.Fail(
                            $"Entry extraction had errors: {handler.LastEntryError}."
                        );
                    }

                    if (hr == HResult.Ok)
                    {
                        ExtractEntryCompleted(_logger, entry.Path);
                        return Result.Ok();
                    }

                    ExtractEntryFailed(_logger, entry.Path, hr);
                    return Result.Fail($"Entry extraction failed (HRESULT: 0x{hr:X8}).");
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts entries that match <paramref name="filter"/> to the specified output directory.
    /// </summary>
    /// <param name="filter">Predicate applied to each entry; only matching entries are extracted.</param>
    /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
    /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result on completion, or a failed result if extraction fails or any matched entry has errors.</returns>
    /// <remarks>Calls <see cref="ListEntriesAsync"/> internally. Use the <see cref="ExtractAsync(IReadOnlyList{ArchiveEntry},Func{ArchiveEntry,bool},string,IProgress{ExtractionProgress}?,CancellationToken)"/> overload to avoid the extra round-trip when you already have the entry list.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result> ExtractAsync(
        Func<ArchiveEntry, bool> filter,
        string outputPath,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_opened)
            return Result.Fail(NotOpenedMessage);
        var entriesResult = await ListEntriesAsync(cancellationToken).ConfigureAwait(false);
        if (entriesResult.IsFailed)
            return entriesResult.ToResult();
        return await ExtractAsync(
                entriesResult.Value,
                filter,
                outputPath,
                progress,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts entries that match <paramref name="filter"/> from a pre-built entry list to the specified output directory.
    /// </summary>
    /// <param name="entries">Entry list previously returned by <see cref="ListEntriesAsync"/>.</param>
    /// <param name="filter">Predicate applied to each entry; only matching entries are extracted.</param>
    /// <param name="outputPath">Directory to write extracted files into; created if it does not exist.</param>
    /// <param name="progress">Optional progress sink; receives a snapshot after each block of bytes is processed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful result on completion, or a failed result if extraction fails or any matched entry has errors.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the extractor has been disposed.</exception>
    public async Task<Result> ExtractAsync(
        IReadOnlyList<ArchiveEntry> entries,
        Func<ArchiveEntry, bool> filter,
        string outputPath,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_opened)
            return Result.Fail(NotOpenedMessage);
        return await Task.Run(
                () =>
                {
                    var indices = entries.Where(filter).Select(e => (uint)e.Index).ToArray();

                    if (indices.Length == 0)
                        return Result.Ok();

                    var handler = new ExtractionHandler(
                        CreateFileEntryProvider(outputPath),
                        progress,
                        indices.Length,
                        cancellationToken,
                        _password
                    );

                    var hr = _archive.Extract(indices, (uint)indices.Length, 0, handler);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (hr == HResult.Ok && handler.LastEntryError != OperationResult.Ok)
                    {
                        ExtractionHadEntryErrors(_logger, _format, handler.LastEntryError);
                        return Result.Fail(
                            $"Filtered extraction had entry errors: {handler.LastEntryError}."
                        );
                    }

                    if (hr == HResult.Ok)
                        return Result.Ok();

                    ExtractFilteredFailed(_logger, _format, hr);
                    return Result.Fail($"Filtered extraction failed (HRESULT: 0x{hr:X8}).");
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the underlying native archive object.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var closeHr = _archive.Close();
        if (closeHr != HResult.Ok)
            ArchiveCloseFailed(_logger, _format, closeHr);
    }

    private ArchiveInfo ReadArchiveInfo() =>
        new ArchiveInfo
        {
            Format = _format,
            IsSolid = ReadBoolArchiveProp(ItemPropId.Solid) ?? false,
            IsEncrypted = ReadBoolArchiveProp(ItemPropId.Encrypted) ?? false,
            Comment = ReadStringArchiveProp(ItemPropId.Comment),
            PhysicalSize = ReadUInt64ArchiveProp(ItemPropId.PhysicalSize) ?? 0,
            VolumeCount = (int)(ReadUInt32ArchiveProp(ItemPropId.NumVolumes) ?? 1),
        };

    private ArchiveEntry ReadEntry(uint index) =>
        new ArchiveEntry
        {
            Index = (int)index,
            Path = ReadStringProp(index, ItemPropId.Path) ?? string.Empty,
            Size = ReadUInt64Prop(index, ItemPropId.Size) ?? 0,
            PackedSize = ReadUInt64Prop(index, ItemPropId.PackedSize) ?? 0,
            Crc = ReadUInt32Prop(index, ItemPropId.Crc) ?? 0,
            IsDirectory = ReadBoolProp(index, ItemPropId.IsDirectory) ?? false,
            IsEncrypted = ReadBoolProp(index, ItemPropId.Encrypted) ?? false,
            LastWriteTime = ReadDateTimeProp(index, ItemPropId.LastWriteTime),
            CreationTime = ReadDateTimeProp(index, ItemPropId.CreationTime),
            LastAccessTime = ReadDateTimeProp(index, ItemPropId.LastAccessTime),
            Attributes = ReadUInt32Prop(index, ItemPropId.Attributes),
        };

    private bool? ReadBoolProp(uint index, ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetProperty(index, propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToBoolean() : null;
        prop.Clear();
        return value;
    }

    private string? ReadStringProp(uint index, ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetProperty(index, propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToStringValue() : null;
        prop.Clear();
        return value;
    }

    private ulong? ReadUInt64Prop(uint index, ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetProperty(index, propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToUInt64() : null;
        prop.Clear();
        return value;
    }

    private uint? ReadUInt32Prop(uint index, ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetProperty(index, propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToUInt32() : null;
        prop.Clear();
        return value;
    }

    private DateTime? ReadDateTimeProp(uint index, ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetProperty(index, propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToDateTime() : null;
        prop.Clear();
        return value;
    }

    private bool? ReadBoolArchiveProp(ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetArchiveProperty(propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToBoolean() : null;
        prop.Clear();
        return value;
    }

    private string? ReadStringArchiveProp(ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetArchiveProperty(propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToStringValue() : null;
        prop.Clear();
        return value;
    }

    private ulong? ReadUInt64ArchiveProp(ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetArchiveProperty(propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToUInt64() : null;
        prop.Clear();
        return value;
    }

    private uint? ReadUInt32ArchiveProp(ItemPropId propId)
    {
        var prop = new PropVariant();
        var hr = _archive.GetArchiveProperty(propId, ref prop);
        var value = hr == HResult.Ok ? prop.ToUInt32() : null;
        prop.Clear();
        return value;
    }

    private Func<uint, (ISequentialOutStream? Stream, string EntryPath)> CreateFileEntryProvider(
        string outputPath
    )
    {
        var canonicalOutput =
            Path.GetFullPath(outputPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return index =>
        {
            var path = ReadStringProp(index, ItemPropId.Path) ?? string.Empty;
            var isDir = ReadBoolProp(index, ItemPropId.IsDirectory) ?? false;

            if (string.IsNullOrEmpty(path))
                return (null, path);

            var fullPath = Path.GetFullPath(Path.Combine(outputPath, path));

            // Guard against path traversal (zip slip): skip entries that resolve outside the output directory.
            if (!fullPath.StartsWith(canonicalOutput, StringComparison.Ordinal))
                return (null, path);

            if (isDir)
            {
                Directory.CreateDirectory(fullPath);
                return (null, path);
            }

            return (new FileEntryStream(fullPath), path);
        };
    }

    private static Func<
        uint,
        (ISequentialOutStream? Stream, string EntryPath)
    > CreateSingleEntryProvider(ArchiveEntry entry, Stream outputStream)
    {
        var adapter = new OutStreamAdapter(outputStream);
        return index =>
            (index == (uint)entry.Index ? (ISequentialOutStream?)adapter : null, entry.Path);
    }
}
