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
public class CompressionMatrixTests
{
    private static readonly byte[] CompressibleContent = BuildCompressibleContent();

    private static byte[] BuildCompressibleContent()
    {
        // 64 KB of highly compressible data — LZMA/LZMA2 reduces this to well under 1 KB.
        var data = new byte[64 * 1024];
        Array.Fill(data, (byte)0xAA);
        return data;
    }

    public static IEnumerable<TestCaseData> MatrixCases()
    {
        // 7z × LZMA (multi-char method name — the key regression path for F1)
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("7z_Lzma_Normal");
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma,
            CompressionLevel.Fast,
            (int?)null
        ).SetName("7z_Lzma_Fast");

        // 7z × LZMA2 with non-default ThreadCount
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma2,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("7z_Lzma2_Normal");
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Lzma2,
            CompressionLevel.Fast,
            (int?)2
        ).SetName("7z_Lzma2_Fast_2Threads");

        // 7z × BZip2
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.BZip2,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("7z_BZip2_Normal");

        // 7z × Copy (no compression)
        yield return new TestCaseData(
            ArchiveFormat.SevenZip,
            CompressionMethod.Copy,
            CompressionLevel.Store,
            (int?)null
        ).SetName("7z_Copy_Store");

        // Zip × Deflate
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.Deflate,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("Zip_Deflate_Normal");
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.Deflate,
            CompressionLevel.Fast,
            (int?)null
        ).SetName("Zip_Deflate_Fast");

        // Zip × BZip2
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.BZip2,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("Zip_BZip2_Normal");

        // Zip × Deflate × Store (level 0 stores entries without compression in ZIP)
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.Deflate,
            CompressionLevel.Store,
            (int?)null
        ).SetName("Zip_Deflate_Store");

        // Zip × LZMA2 is remapped to Deflate internally — include to confirm no crash
        yield return new TestCaseData(
            ArchiveFormat.Zip,
            CompressionMethod.Lzma2,
            CompressionLevel.Normal,
            (int?)null
        ).SetName("Zip_Lzma2_RemappedToDeflate");
    }

    [TestCaseSource(nameof(MatrixCases))]
    public async Task Compress_ThenExtract_RoundTripSucceeds(
        ArchiveFormat format,
        CompressionMethod method,
        CompressionLevel level,
        int? threadCount
    )
    {
        var parameters = new CompressionParameters
        {
            Method = method,
            Level = level,
            ThreadCount = threadCount,
        };

        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                format,
                parameters,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("matrix.bin", (Stream)new MemoryStream(CompressibleContent)) };
            var result = await compressor.CompressAsync(entries, archive);
            result
                .IsSuccess.Should()
                .BeTrue(
                    $"{format}/{method}/{level} compression failed: {string.Join("; ", result.Errors)}"
                );
        }

        archive.Position = 0;
        archive
            .Length.Should()
            .BeGreaterThan(0, $"{format}/{method}/{level} archive should not be empty");

        using var extractor = new SevenZipExtractor(
            archive,
            format,
            NullLogger<SevenZipExtractor>.Instance
        );
        var openResult = await extractor.OpenAsync();
        openResult.IsSuccess.Should().BeTrue($"{format}/{method}/{level} open should succeed");

        var entriesResult = await extractor.ListEntriesAsync();
        entriesResult.IsSuccess.Should().BeTrue();
        entriesResult.Value.Should().HaveCount(1);

        using var output = new MemoryStream();
        var extractResult = await extractor.ExtractEntryAsync(entriesResult.Value[0], output);
        extractResult.IsSuccess.Should().BeTrue();
        output
            .ToArray()
            .Should()
            .BeEquivalentTo(
                CompressibleContent,
                $"{format}/{method}/{level} content should round-trip correctly"
            );
    }

    /// <summary>
    /// Regression test for F1 (wchar_t encoding bug in SevenZipWideString).
    /// If the method name "Copy" were passed as UTF-16 instead of UCS-4, 7-Zip would
    /// not recognise it and would fall back to LZMA2, compressing the 64 KB repeating
    /// pattern to well under 1 KB. With the correct UCS-4 encoding, Copy is used and
    /// the archive size reflects the uncompressed input size.
    /// </summary>
    [Test]
    public async Task SevenZip_CopyMethod_ArchiveSizeReflectsNoCompression()
    {
        using var archive = new MemoryStream();
        using (
            var compressor = new SevenZipCompressor(
                ArchiveFormat.SevenZip,
                CompressionParameters.Store,
                NullLogger<SevenZipCompressor>.Instance
            )
        )
        {
            var entries = new[] { ("data.bin", (Stream)new MemoryStream(CompressibleContent)) };
            var result = await compressor.CompressAsync(entries, archive);
            result.IsSuccess.Should().BeTrue();
        }

        // 64 KB of repeating bytes compressed with LZMA2 would be < 512 bytes.
        // With Copy method, archive size must be at least half the input size.
        archive
            .Length.Should()
            .BeGreaterThan(
                CompressibleContent.Length / 2,
                "Copy method should store data without compression; a much smaller archive indicates the method name was not passed correctly (F1 regression)"
            );
    }
}
