using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using SevenZipSharper;
using SevenZipSharper.Compression;

namespace SevenZipSharper.Benchmarks;

/// <summary>
/// Compares archive extraction performance against SharpSevenZip.
/// Requires native 7-Zip libraries in runtimes/&lt;RID&gt;/native/ to run.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class ExtractionBenchmarks
{
    [Params(ArchiveFormat.SevenZip, ArchiveFormat.Zip)]
    public ArchiveFormat Format { get; set; }

    private byte[] _archive = null!;
    private MemoryStream _outputBuffer = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var payload = GenerateCompressiblePayload(1024 * 1024);

        using var compressor = new SevenZipCompressor(
            Format,
            CompressionParameters.Default,
            NullLogger<SevenZipCompressor>.Instance
        );

        var ms = new MemoryStream();
        await compressor.CompressAsync(
            new[] { ("payload.bin", (Stream)new MemoryStream(payload)) },
            ms
        );
        _archive = ms.ToArray();
        _outputBuffer = new MemoryStream(capacity: 2 * 1024 * 1024);
    }

    [GlobalCleanup]
    public void Cleanup() => _outputBuffer.Dispose();

    [Benchmark(Description = "SevenZipSharper")]
    public async Task ExtractWithSevenZipSharper()
    {
        _outputBuffer.SetLength(0);
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archive),
            Format,
            NullLogger<SevenZipExtractor>.Instance
        );
        var openResult = await extractor.OpenAsync();
        if (openResult.IsFailed)
            return;
        var entries = await extractor.ListEntriesAsync();
        if (entries.IsFailed)
            return;
        foreach (var entry in entries.Value)
            await extractor.ExtractEntryAsync(entry, _outputBuffer);
    }

    [Benchmark(Description = "SharpSevenZip", Baseline = true)]
    public async Task ExtractWithSharpSevenZip()
    {
        _outputBuffer.SetLength(0);
        global::SharpSevenZip.SharpSevenZipExtractor.SetLibraryPath(NativeLibPath);
        using var extractor = new global::SharpSevenZip.SharpSevenZipExtractor(
            new MemoryStream(_archive)
        );
        await extractor.ExtractFileAsync(0, _outputBuffer);
    }

    internal static string NativeLibPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            BaseRuntimeIdentifier(),
            "native",
            NativeLibName()
        );

    private static string BaseRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}"
            ),
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";
        return $"linux-{arch}";
    }

    internal static byte[] GenerateCompressiblePayload(int size)
    {
        // Repeating ASCII text — high compressibility, realistic for document archives.
        const string pattern =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. "
            + "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. ";
        var patternBytes = System.Text.Encoding.ASCII.GetBytes(pattern);
        var data = new byte[size];
        for (var i = 0; i < data.Length; i++)
            data[i] = patternBytes[i % patternBytes.Length];
        return data;
    }

    private static string NativeLibName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "7z.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "7z.dylib";
        return "7z.so";
    }
}
