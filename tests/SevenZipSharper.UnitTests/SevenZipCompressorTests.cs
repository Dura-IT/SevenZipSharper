using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests;

[TestOf(typeof(SevenZipCompressor))]
public class SevenZipCompressorTests
{
    private static SevenZipCompressor CreateCompressor(
        IOutArchive? archive = null,
        CompressionParameters? parameters = null
    )
    {
        archive ??= new FakeOutArchive();
        return new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters ?? CompressionParameters.Default,
            archive,
            NullLogger<SevenZipCompressor>.Instance
        );
    }

    private static (string EntryPath, Stream Data)[] OneEntry() =>
        new[] { ("file.txt", (Stream)new MemoryStream(new byte[] { 1, 2, 3 })) };

    [Test]
    public async Task CompressAsync_CallsUpdateItems_WithCorrectCount()
    {
        var archive = new FakeOutArchive();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        await compressor.CompressAsync(OneEntry(), output);

        archive.LastCount.Should().Be(1u);
    }

    [Test]
    public async Task CompressAsync_ReturnsFail_WhenParametersInvalid()
    {
        var invalidParams = CompressionParameters.Default with { ThreadCount = 0 };
        using var compressor = CreateCompressor(parameters: invalidParams);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_ReturnsFail_WhenUpdateItemsReturnsError()
    {
        var archive = new FakeOutArchive(HResult.NotImplemented);
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_ReturnsOk_WhenUpdateItemsSucceeds()
    {
        var archive = new FakeOutArchive();
        using var compressor = CreateCompressor(archive);
        var output = new MemoryStream();

        var result = await compressor.CompressAsync(OneEntry(), output);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CompressAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        using var compressor = CreateCompressor();
        var output = new MemoryStream();

        await FluentActions
            .Awaiting(() =>
                compressor.CompressAsync(OneEntry(), output, cancellationToken: cts.Token)
            )
            .Should()
            .ThrowAsync<System.OperationCanceledException>();
    }
}

[GeneratedComClass]
internal sealed partial class FakeOutArchive : IOutArchive
{
    private readonly int _hResult;

    internal FakeOutArchive(int hResult = HResult.Ok)
    {
        _hResult = hResult;
    }

    public uint LastCount { get; private set; }

    public int UpdateItems(
        IOutStream outStream,
        uint numItems,
        IArchiveUpdateCallback updateCallback
    )
    {
        LastCount = numItems;
        return _hResult;
    }

    public int GetFileTimeType(out uint type)
    {
        type = 0;
        return HResult.Ok;
    }
}
