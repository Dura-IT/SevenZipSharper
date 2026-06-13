# SevenZipSharper

[![NuGet](https://img.shields.io/nuget/v/DuraIT.SevenZipSharper.svg)](https://www.nuget.org/packages/DuraIT.SevenZipSharper/)
[![CI](https://github.com/Dura-IT/SevenZipSharper/actions/workflows/ci.yml/badge.svg)](https://github.com/Dura-IT/SevenZipSharper/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=Dura-IT_SevenZipSharper&metric=alert_status)](https://sonarcloud.io/summary/overall?id=Dura-IT_SevenZipSharper)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Dura-IT_SevenZipSharper&metric=coverage)](https://sonarcloud.io/summary/overall?id=Dura-IT_SevenZipSharper)

A modern, cross-platform .NET 10 library for reading and writing 7-Zip archives. Provides full LZMA/LZMA2 compression parameter control, `Task`/`CancellationToken`-based async API, `IProgress<T>` reporting, and `Result<T>` error handling — with no system 7-Zip installation required.

---

## History

The .NET / 7-Zip wrapper ecosystem has a long lineage:

1. **Original CodePlex project** (circa 2007) — Targeted .NET Framework 2.0 and Windows. Built around the 7-Zip COM interface via P/Invoke, exposing `SevenZipExtractor`, `SevenZipCompressor`, and `SevenZipSfx` with event-based progress.

2. **[SevenZipSharp](https://github.com/tomap/SevenZipSharp)** — Migrated the project to GitHub when CodePlex shut down.

3. **[SevenZipSharp](https://github.com/squid-box/SevenZipSharp)** — Updated targets to .NET Standard 2.0, .NET Framework 4.7.2, and .NET Core 3.1. Cleaned up tests and added CI. Released as version 1.6.2 and archived in April 2024.

4. **[SharpSevenZip](https://github.com/JeremyAnsel/SharpSevenZip)** — A fork of SevenZipSharp that extended targets to .NET Framework 4.8, .NET 6, and .NET 8, with benchmarks and code improvements.

**SevenZipSharper** starts from the low-level COM/P-Invoke declarations in [SevenZipSharp](https://github.com/squid-box/SevenZipSharp) — the part that required the most tedious vtable and marshaling research — and builds a fully rewritten public API on top, designed for the modern .NET ecosystem.

> **Attribution:** The COM/P-Invoke declarations for `IInArchive`, `IOutArchive`, `IArchiveUpdateCallback`, `IArchiveOpenCallback`, `IProgress`, the property ID enums, and the `CreateObject` binding are derived from [SevenZipSharp](https://github.com/squid-box/SevenZipSharp). Thank you for doing the tedious vtable and marshaling research.

---

## Why SevenZipSharper

| | SevenZipSharp | SharpSevenZip | **SevenZipSharper** |
|---|---|---|---|
| **Status** | Archived (2024) | Active | Active |
| **Target frameworks** | .NET Std 2.0 / FX 4.7.2 / Core 3.1 | .NET Std 2.0 / FX 4.8 / .NET 6–8 | .NET 8 + .NET 10 |
| **Async model** | Events + callbacks | Events + callbacks | `Task` / `CancellationToken` |
| **Progress reporting** | Events | Events | `IProgress<T>` |
| **Error handling** | Exceptions only | Exceptions only | `Result<T>` (FluentResults) |
| **Native library** | Caller must supply path | Caller must supply path | Bundled RID assets — zero config |
| **Cross-platform** | Windows-centric | Windows-centric | Windows, macOS, Linux |
| **Nullable** | Not enforced | Partial | Fully enabled |
| **DI / logging** | None | None | `ILogger<T>` / Serilog |

Both existing libraries share the original API shape and require the caller to locate and supply a `7z.dll`. SevenZipSharper ships the correct native binary for your platform as a NuGet RID asset — the same pattern used by SkiaSharp and SQLitePCLRaw — so it works out of the box on Windows, macOS, and Linux with no system dependencies or path configuration.

---

## Framework & Platform Coverage

### Target frameworks

| | SevenZipSharp | SharpSevenZip | **SevenZipSharper** |
|---|---|---|---|
| .NET Framework 4.x | 4.7.2 | 4.8 | — |
| .NET Standard 2.0 | Yes | Yes | — |
| .NET Core 3.1 | Yes | — | — |
| .NET 6 | — | Yes | — |
| .NET 8 | — | Yes | **Yes** |
| .NET 10 | — | — | **Yes** |

**.NET Framework and .NET Standard are not supported.** The COM interop layer requires `[GeneratedComInterface]`/`[GeneratedComClass]` (.NET 7+) and native library resolution requires `NativeLibrary.SetDllImportResolver` (.NET Core 3.0+). Neither API exists on .NET Framework or any version of .NET Standard.

### Native library delivery

| | SevenZipSharp | SharpSevenZip | **SevenZipSharper** |
|---|---|---|---|
| Windows x64 | Caller supplies `7z.dll` | Caller supplies `7z.dll` | **Bundled** |
| Windows Arm64 | — | — | **Bundled** |
| macOS Arm64 | — | — | **Bundled** |
| macOS x64 | — | — | **Bundled** |
| Linux x64 | — | — | **Bundled** |
| Linux Arm64 | — | — | **Bundled** |

Native 7-Zip libraries are bundled as RID-specific NuGet assets under `runtimes/<RID>/native/`. .NET resolves the correct binary automatically at runtime — no system 7-Zip installation required.

---

## Supported Formats

| Format | Read | Write |
|--------|------|-------|
| 7-Zip (`.7z`) | Yes | Yes |
| ZIP (`.zip`, `.jar`, `.epub`, `.apk`) | Yes | Yes |
| gzip (`.gz`, `.tgz`) | Yes | Yes |
| bzip2 (`.bz2`, `.tbz2`) | Yes | Yes |
| XZ (`.xz`, `.txz`) | Yes | Yes |
| TAR (`.tar`) | Yes | Yes |
| WIM (`.wim`) | Yes | Yes |
| CAB (`.cab`) | Yes | — |
| ARJ (`.arj`) | Yes | — |
| LZH (`.lzh`, `.lha`) | Yes | — |
| ISO (`.iso`) | Yes | — |

**RAR is not supported.** The unRAR source code carries a redistribution restriction that is incompatible with SevenZipSharper's LGPL licence. If you need RAR extraction, use a dedicated unRAR library alongside this one.

---

## Installation

```
dotnet add package DuraIT.SevenZipSharper --version 0.1.0
```

`DuraIT.SevenZipSharper` has a direct dependency on `DuraIT.SevenZipSharper.Native`, so a single package reference is all you need — the native 7-Zip binaries for your platform are pulled in automatically.

> Native 7-Zip binaries are not yet bundled. The packages are published pre-release pending native library compilation for all supported platforms.

---

## Quick Start

### Extraction

```csharp
using SevenZipSharper;

using var stream = File.OpenRead("archive.7z");
using var extractor = new SevenZipExtractor(stream, ArchiveFormat.SevenZip, logger);

var openResult = await extractor.OpenAsync();
if (openResult.IsFailed)
    return openResult.ToResult();

// List all entries
var entriesResult = await extractor.ListEntriesAsync();

// Extract everything to a directory
await extractor.ExtractAllAsync("/path/to/output");

// Extract a single entry to a stream
await extractor.ExtractEntryAsync(entriesResult.Value[0], outputStream);

// Extract with a filter
await extractor.ExtractAsync(e => e.Path.EndsWith(".txt"), "/path/to/output");
```

### Compression

```csharp
using var compressor = new SevenZipCompressor(
    ArchiveFormat.SevenZip,
    CompressionParameters.Default,
    logger
);

var entries = new[]
{
    ("docs/readme.md", (Stream)File.OpenRead("readme.md")),
    ("src/main.cs",    (Stream)File.OpenRead("main.cs")),
};

await compressor.CompressAsync(entries, File.Create("archive.7z"));
```

### Progress reporting

```csharp
var progress = new Progress<ExtractionProgress>(p =>
    Console.WriteLine($"{p.EntryPath} — {p.EntryIndex + 1}/{p.TotalEntries}"));

await extractor.ExtractAllAsync("/path/to/output", progress: progress);
```

```csharp
var progress = new Progress<CompressionProgress>(p =>
    Console.WriteLine($"{p.EntryPath} — {p.BytesProcessed}/{p.TotalBytes} bytes"));

await compressor.CompressAsync(entries, outputStream, progress: progress);
```

### Password-protected archives

```csharp
// Extraction — pass the password to OpenAsync
var openResult = await extractor.OpenAsync(password: "secret");

// Compression — set EncryptionPassword (and optionally EncryptHeaders) in parameters
var parameters = CompressionParameters.Default with
{
    EncryptionPassword = "secret",
    EncryptHeaders = true,
};
using var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, parameters, logger);
await compressor.CompressAsync(entries, outputStream);
```

### Detecting an unknown archive format

When you don't know the format up front — accepting user-supplied files, sniffing an upload, etc. — use `ArchiveFormatDetector` to identify it before constructing the extractor.

```csharp
using SevenZipSharper.Detection;

await using var fs = File.OpenRead(path);

// Try magic-byte sniffing first (works for archives without an extension);
// fall back to the extension when the bytes are inconclusive (e.g. ISO).
var format = await ArchiveFormatDetector.FromStreamAsync(fs)
             ?? ArchiveFormatDetector.FromExtension(path);

if (format is null)
    throw new InvalidOperationException("Unrecognised archive format.");

using var extractor = new SevenZipExtractor(fs, format.Value, logger);
await extractor.OpenAsync();
```

`FromStreamAsync` reads up to 262 bytes and restores the original position on seekable streams. Both methods return `null` for unknown formats rather than throwing; they throw `ArgumentNullException` / `ArgumentException` for invalid arguments.

### Dependency injection

```csharp
// Registers the native library resolver; SevenZipExtractor and SevenZipCompressor
// are still constructed directly (they require per-instance Stream and format args).
services.AddSevenZipSharper();
```

---

## Rebuilding native libraries

The bundled `7z.dll` / `7z.dylib` / `7z.so` binaries are built from the official [7-Zip source](https://github.com/ip7z/7zip) and stored under `src/SevenZipSharper.Native/runtimes/`. To rebuild them after a 7-Zip version bump:

1. Update `scripts/7zip-version` with the new four-digit version (e.g. `2409`).
2. Compute SHA256 hashes of the new source tarball and Windows installers, and update `scripts/7zip-sha256`.
3. Run the appropriate fetch script:
   - **macOS / Linux:** `bash scripts/fetch-natives.sh`
   - **Windows:** `.\scripts\fetch-natives.ps1`
4. Commit the updated binaries and hash file; the `build-natives.yml` workflow builds all six RID targets on GitHub Actions.

---

## Benchmarks

```
dotnet run -c Release --project benchmarks/SevenZipSharper.Benchmarks -- --filter *
```

Benchmarks compare extraction and compression performance against [SharpSevenZip](https://github.com/JeremyAnsel/SharpSevenZip) on a 1 MB payload across formats and compression levels, run on both .NET 8 and .NET 10 to expose any TFM-dependent perf gap.

### Results (Apple M5 Pro, macOS Arm64, BenchmarkDotNet 0.15.8)

**Compression** — mean over multiple iterations, lower is better:

| Level   | SevenZipSharper / net10 | SevenZipSharper / net8 | SharpSevenZip / net10 | SharpSevenZip / net8 |
|---------|------------------------:|-----------------------:|----------------------:|---------------------:|
| Fastest |              **1.85 ms** |             **1.82 ms** |              1.69 ms |              1.72 ms |
| Normal  |              **3.18 ms** |             **3.16 ms** |              3.12 ms |              3.13 ms |
| Ultra   |              **3.06 ms** |             **3.12 ms** |              2.97 ms |              2.98 ms |

**Extraction** — mean over multiple iterations, lower is better:

| Format | SevenZipSharper / net10 | SevenZipSharper / net8 | SharpSevenZip / net10 | SharpSevenZip / net8 |
|--------|------------------------:|-----------------------:|----------------------:|---------------------:|
| 7z     |               **256 µs** |              **256 µs** |              256 µs |              315 µs |
| Zip    |               **243 µs** |              **249 µs** |              220 µs |              262 µs |

### Reading the numbers

- **Compression** is dominated by the LZMA codec running inside the native `7z` library. COM marshalling is a small fraction of total time, so the differences between libraries and TFMs are small and mostly within noise.
- **Extraction** is where the COM-interop story shows up. SevenZipSharper sits at the same ~256 µs on both net8 and net10 — we're at the performance floor for this workload, where the codec dominates and there's nothing left for the runtime to speed up. SharpSevenZip pays an extra ~60 µs of COM overhead on net8 (315 µs) and closes that gap on net10 (256 µs), matching us.
- **Why SharpSevenZip moves and we don't:** SharpSevenZip uses legacy `[ComImport]` interfaces because its lineage targets .NET Framework and .NET Standard, where modern interop attributes aren't available. With `[ComImport]`, every COM call goes through the CLR's runtime-generated Runtime Callable Wrapper and built-in marshaller — and the .NET team has heavily optimised that path in net9 and net10. SevenZipSharper uses `[GeneratedComInterface]` / `[GeneratedComClass]` (net8+ only), where the marshalling code is emitted at compile time. That generated code was already at the floor on net8; the runtime improvements don't apply to it because we don't call into the runtime marshaller. The result: we've been at net10's perf level since net8.
- **Allocations:** SevenZipSharper allocates ~90–110× more than SharpSevenZip per operation. Most of this is the trade we make for ergonomic async APIs (`Result<T>`, `Task`, `IProgress<T>`). For most callers this is invisible; for tight loops processing thousands of small archives, it's a measurable cost worth knowing.
- **TFM choice:** for this library on this hardware, pick whichever TFM your downstream stack uses — performance won't be the deciding factor.

Results on x64 hardware and Linux/Windows may differ — the relative ordering should be similar but absolute timings will shift with codec performance.

---

## License

SevenZipSharper is licensed under the **GNU Lesser General Public License v3.0 or later** (LGPL-3.0-or-later).

See [LICENSE](LICENSE) for details.
