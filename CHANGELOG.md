# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-06-10

Initial pre-release. Native 7-Zip binaries are not yet bundled; the packages are
published as pre-release pending native library compilation for all supported platforms.

### Added

- `SevenZipExtractor` — `OpenAsync`, `ListEntriesAsync`, `ExtractAllAsync`,
  `ExtractEntryAsync`, `ExtractAsync(filter)` with `CancellationToken` and
  `IProgress<ExtractionProgress>` support
- `SevenZipCompressor` — `CompressAsync`, `CompressFilesAsync`, `AppendAsync`,
  `CompressMultiVolumeAsync` with `CancellationToken` and
  `IProgress<CompressionProgress>` support
- `CompressionParameters` — LZMA2 method, compression level, solid mode, dictionary
  size, thread count, encryption (password + header encryption)
- `ArchiveFormat` enum covering 7-Zip, ZIP, gzip, bzip2, XZ, TAR, WIM, CAB, ARJ,
  LZH, CPIO, and ISO (RAR excluded — see README)
- `ArchiveFormatDetector` — format detection from file extension and magic bytes
- `Result<T>` (FluentResults) error handling throughout; exceptions reserved for
  invariant violations
- `ILogger<T>` integration with `LoggerMessage.Define` on all hot paths
- `AddSevenZipSharper()` `IServiceCollection` extension
- NuGet two-package design: `DuraIT.SevenZipSharper` (managed) +
  `DuraIT.SevenZipSharper.Native` (RID assets); single package reference is all
  consumers need
- RID-based native library bundling for `win-x64`, `win-arm64`, `osx-arm64`,
  `osx-x64`, `linux-x64`, `linux-arm64` via `NativeLibrary.SetDllImportResolver`
- GitHub Actions CI: build + test on Ubuntu, Windows, macOS; pack job

[Unreleased]: https://github.com/Dura-IT/SevenZipSharper/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.1.0
