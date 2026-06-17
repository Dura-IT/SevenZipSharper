using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using SevenZipSharper;
using SevenZipSharper.Compression;

namespace SevenZipSharper.Benchmarks;

/// <summary>
/// Compares archive compression performance against SharpSevenZip across matching compression
/// levels and TFMs. Requires native 7-Zip libraries in runtimes/&lt;RID&gt;/native/ to run.
/// </summary>
/// <remarks>
/// Both compressor instances are created once in <see cref="GlobalSetupAttribute"/> and reused
/// across iterations — both libraries support re-invocation, matching real-world usage of a
/// long-lived compressor. Per-iteration allocations (the MemoryStream wrapping the test payload)
/// are excluded from the measurement window via <see cref="IterationSetupAttribute"/>; only the
/// compress call itself is timed.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class CompressionBenchmarks
{
    [Params(CompressionLevel.Fastest, CompressionLevel.Normal, CompressionLevel.Ultra)]
    public CompressionLevel Level { get; set; }

    private static readonly byte[] TestPayload = ExtractionBenchmarks.GenerateCompressiblePayload(
        1024 * 1024
    );

    private MemoryStream _outputBuffer = null!;
    private MemoryStream _payloadStream = null!;
    private SevenZipCompressor _compressor = null!;
    private global::SharpSevenZip.SharpSevenZipCompressor _sharpCompressor = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _outputBuffer = new MemoryStream(capacity: 2 * 1024 * 1024);

        var parameters = CompressionParameters.Default with { Level = Level };
        _compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters,
            NullLogger<SevenZipCompressor>.Instance
        );

        global::SharpSevenZip.SharpSevenZipCompressor.SetLibraryPath(
            ExtractionBenchmarks.NativeLibPath
        );
        _sharpCompressor = new global::SharpSevenZip.SharpSevenZipCompressor
        {
            CompressionLevel = MapLevel(Level),
        };
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _outputBuffer.SetLength(0);
        _payloadStream = new MemoryStream(TestPayload, writable: false);
    }

    [IterationCleanup]
    public void IterationCleanup() => _payloadStream.Dispose();

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _compressor.Dispose();
        _outputBuffer.Dispose();
    }

    [Benchmark(Description = "SevenZipSharper")]
    public Task CompressWithSevenZipSharper() =>
        _compressor.CompressAsync(new[] { ("payload.bin", (Stream)_payloadStream) }, _outputBuffer);

    [Benchmark(Description = "SharpSevenZip", Baseline = true)]
    public void CompressWithSharpSevenZip() =>
        _sharpCompressor.CompressStream(_payloadStream, _outputBuffer);

    /// <summary>
    /// Maps <see cref="CompressionLevel"/> (Store=0, Fastest=1, Fast=3, Normal=5, Maximum=7, Ultra=9)
    /// to <see cref="global::SharpSevenZip.CompressionLevel"/> (None=0, Fast=1, Low=2, Normal=3, High=4, Ultra=5)
    /// so both libraries do equivalent work at each measured level.
    /// </summary>
    private static global::SharpSevenZip.CompressionLevel MapLevel(CompressionLevel level) =>
        level switch
        {
            CompressionLevel.Store => global::SharpSevenZip.CompressionLevel.None,
            CompressionLevel.Fastest => global::SharpSevenZip.CompressionLevel.Fast,
            CompressionLevel.Fast => global::SharpSevenZip.CompressionLevel.Low,
            CompressionLevel.Normal => global::SharpSevenZip.CompressionLevel.Normal,
            CompressionLevel.Maximum => global::SharpSevenZip.CompressionLevel.High,
            CompressionLevel.Ultra => global::SharpSevenZip.CompressionLevel.Ultra,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown level"),
        };
}
