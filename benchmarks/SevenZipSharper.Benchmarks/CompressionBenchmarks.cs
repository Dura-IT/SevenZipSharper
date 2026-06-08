using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using SevenZipSharper;
using SevenZipSharper.Compression;

namespace SevenZipSharper.Benchmarks;

/// <summary>
/// Compares archive compression performance against SharpSevenZip.
/// Requires native 7-Zip libraries in runtimes/&lt;RID&gt;/native/ to run.
/// </summary>
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

    [GlobalSetup]
    public void Setup() => _outputBuffer = new MemoryStream(capacity: 2 * 1024 * 1024);

    [GlobalCleanup]
    public void Cleanup() => _outputBuffer.Dispose();

    [Benchmark(Description = "SevenZipSharper")]
    public async Task CompressWithSevenZipSharper()
    {
        _outputBuffer.SetLength(0);
        var parameters = CompressionParameters.Default with { Level = Level };
        using var compressor = new SevenZipCompressor(
            ArchiveFormat.SevenZip,
            parameters,
            NullLogger<SevenZipCompressor>.Instance
        );
        await compressor.CompressAsync(
            new[] { ("payload.bin", (Stream)new MemoryStream(TestPayload)) },
            _outputBuffer
        );
    }

    [Benchmark(Description = "SharpSevenZip", Baseline = true)]
    public Task CompressWithSharpSevenZip()
    {
        _outputBuffer.SetLength(0);
        return Task.Run(() =>
        {
            global::SharpSevenZip.SharpSevenZipCompressor.SetLibraryPath(
                ExtractionBenchmarks.NativeLibPath
            );
            var compressor = new global::SharpSevenZip.SharpSevenZipCompressor
            {
                CompressionLevel = (global::SharpSevenZip.CompressionLevel)(int)Level,
            };
            compressor.CompressStream(new MemoryStream(TestPayload), _outputBuffer);
        });
    }
}
