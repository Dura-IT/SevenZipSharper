# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-06-15

### Added

- Password-protected compression for the 7z and Zip formats via the existing
  `CompressionParameters.EncryptionPassword` property. 7z uses AES-256
  natively; Zip emits `em=AES` (WinZip AES, extra field `0x9901`, strength 3)
  so encrypted Zip archives use AES-256 instead of the legacy weak ZipCrypto.
  `EncryptHeaders` (7z only) likewise becomes functional.
- Append paths (`AppendAsync`) handle encrypted archives, including
  header-encrypted 7z files where the existing archive must be opened with
  the password before new entries can be appended.
- Format-level fail-fast validation: setting `EncryptionPassword` on a format
  that does not support encryption (Tar, GZip, BZip2, Xz) returns
  `Result.Fail` instead of silently producing an unencrypted archive.
  `EncryptHeaders` on any non-7z format does the same.
- README: Format × method compatibility matrix derived from
  `FormatFallbackBehaviorTests` (the spec lives in the tests).
- `<example>` XML doc blocks on the public `SevenZipExtractor` and
  `SevenZipCompressor` methods drive IntelliSense.

### Changed

- `ArchiveFormatDetector` is now public with input validation
  (`ArgumentNullException.ThrowIfNull`, `stream.CanRead`) and an
  `<example>` block.

### Fixed

- Sonar security hotspots cleared: workflow steps now reference
  `$SONAR_TOKEN` via the `env:` block instead of `${{ secrets… }}` in the
  `run:` body, and `scripts/fetch-natives.sh` pins curl to HTTPS
  (`--proto '=https'`) to refuse plaintext redirects.

## [0.1.1] - 2026-06-13

### Added

- `scripts/release.sh <version>` — pre-flights semver, branch, clean tree,
  tag clash, and the matching natives GitHub release, then bumps both
  csprojs, commits, tags, and pushes.

### Changed

- CI/CD reorganised so the native 7-Zip binaries live as a GitHub release
  asset keyed by `scripts/7zip-version` (consumed by `ci.yml` and
  `publish.yml`). Managed-side CI no longer rebuilds natives every run.
- `ci.yml` split into a multi-OS compile matrix and a single Ubuntu
  test+sonar job, so integration tests actually run under Sonar coverage
  (previously silently skipped on Linux/Windows).
- Trusted Publishing for NuGet via the `NuGet/login` action.

### Fixed

- README performance framing — explicit, honest comparison against
  SharpSevenZip with benchmark methodology notes.

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

[Unreleased]: https://github.com/Dura-IT/SevenZipSharper/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.2.0
[0.1.1]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.1.1
[0.1.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.1.0
