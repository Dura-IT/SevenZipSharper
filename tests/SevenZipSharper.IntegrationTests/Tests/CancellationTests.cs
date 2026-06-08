using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

[TestFixture]
public class CancellationTests
{
    private byte[] _archiveBytes = Array.Empty<byte>();

    [OneTimeSetUp]
    public async Task CreateArchive()
    {
        var content = new byte[128 * 1024];
        new Random(0).NextBytes(content);
        var entries = new[] { ("large.bin", (Stream)new MemoryStream(content)) };

        using var archive = new MemoryStream();
        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            CompressionParameters.Default,
            NullLogger<SevenZipCompressor>.Instance
        );
        await compressor.CompressAsync(entries, archive);
        _archiveBytes = archive.ToArray();
    }

    [Test]
    public async Task ExtractAllAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();

        var outDir = Path.Combine(Path.GetTempPath(), $"szs_cancel_{Guid.NewGuid():N}");
        try
        {
            await FluentActions
                .Awaiting(() => extractor.ExtractAllAsync(outDir, cancellationToken: cts.Token))
                .Should()
                .ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    [Test]
    public async Task ListEntriesAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();

        await FluentActions
            .Awaiting(() => extractor.ListEntriesAsync(cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }
}
