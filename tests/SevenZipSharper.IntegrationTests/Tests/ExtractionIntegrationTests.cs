using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests;

[TestFixture]
[TestOf(typeof(SevenZipExtractor))]
public sealed class ExtractionIntegrationTests
{
    private string _tempDir = string.Empty;
    private byte[] _archiveBytes = Array.Empty<byte>();

    private static readonly byte[] EntryContent = System.Text.Encoding.UTF8.GetBytes(
        "Hello from SevenZipSharper integration tests"
    );

    [OneTimeSetUp]
    public async Task CreateArchive()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"szs_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var entries = new[] { ("test/hello.txt", (Stream)new MemoryStream(EntryContent)) };
        using var output = new MemoryStream();
        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            CompressionParameters.Default,
            NullLogger<SevenZipCompressor>.Instance
        );
        var result = await compressor.CompressAsync(entries, output);
        result
            .IsSuccess.Should()
            .BeTrue(
                $"fixture archive creation must succeed, errors: {string.Join("; ", result.Errors)}"
            );
        _archiveBytes = output.ToArray();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task OpenAsync_ValidArchive_ReturnsSuccessWithArchiveInfo()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );

        var result = await extractor.OpenAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be(ArchiveFormat.SevenZip);
    }

    [Test]
    public async Task ListEntriesAsync_AfterOpen_ReturnsEntries()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();

        var result = await extractor.ListEntriesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Path.Should().Be("test/hello.txt");
        result.Value[0].Size.Should().Be((ulong)EntryContent.Length);
    }

    [Test]
    public async Task ExtractAllAsync_AfterOpen_WritesFilesWithCorrectContent()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();
        var outDir = Path.Combine(_tempDir, "extractAll");

        var result = await extractor.ExtractAllAsync(outDir);

        result.IsSuccess.Should().BeTrue();
        var extracted = Path.Combine(outDir, "test", "hello.txt");
        File.Exists(extracted).Should().BeTrue();
        (await File.ReadAllBytesAsync(extracted)).Should().BeEquivalentTo(EntryContent);
    }

    [Test]
    public async Task ExtractEntryAsync_AfterOpen_WritesCorrectContent()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync();
        var entries = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();

        var result = await extractor.ExtractEntryAsync(entries[0], output);

        result.IsSuccess.Should().BeTrue();
        output.ToArray().Should().BeEquivalentTo(EntryContent);
    }
}
