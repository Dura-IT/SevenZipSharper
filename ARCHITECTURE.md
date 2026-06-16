# SevenZipSharper — Architecture Guide

This document is for contributors who need to understand the internals. If you are
a library consumer, the README and public XML docs are the right starting point.

---

## Layer overview

```
SevenZipSharper (public API)
├── SevenZipExtractor       — reads and extracts from archives
├── SevenZipCompressor      — creates and appends to archives
└── ArchiveFormatDetector   — identifies formats by magic bytes

Detection / Extraction / Compression
├── Extraction/             — ExtractionHandler, ArchiveOpenHandler, FileEntryStream
├── Compression/            — CompressionHandler, AppendUpdateHandler, MultiVolumeCompressionHandler
└── Detection/              — ArchiveFormatDetector

Interop (internal — COM boundary)
├── Interop/Archive/        — COM interface definitions (IInArchive, IOutArchive, callbacks…)
├── Interop/Streams/        — ISequentialInStream / IOutStream adapters
├── PropVariant.cs          — platform-aware PROPVARIANT struct
├── SevenZipBStr.cs/.Marshaller.cs   — cross-platform BSTR allocation
├── SevenZipWideString.cs/.Marshaller.cs — cross-platform wchar_t strings
├── NativeLibraryLoader.cs  — resolves and loads the 7-Zip native binary
├── SevenZipLib.cs          — calls CreateObject to instantiate COM objects
└── ArchiveFormatRegistry.cs — maps ArchiveFormat → CLSID

Native binaries (DuraIT.SevenZipSharper.Native NuGet package)
└── runtimes/<rid>/native/  — 7z.dll / 7z.dylib / 7z.so per RID
```

---

## COM interop — source-generated, not runtime-generated

7-Zip exposes its C++ API as COM interfaces. .NET 8+ provides
`[GeneratedComInterface]` and `[GeneratedComClass]` (in `System.Runtime.InteropServices`)
which emit the vtable wiring at compile time via a Roslyn source generator.

**Why source-gen, not `ComImport`?**  
`[ComImport]` is Windows-only and uses the COM runtime. Source-gen COM works
cross-platform and produces pure P/Invoke — no COM runtime required.

**Defining an interface:**

```csharp
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600600000")]
internal partial interface IInArchive
{
    int Open(IInStream stream, nint maxCheckStartPosition, IArchiveOpenCallback openArchiveCallback);
    // …
}
```

The GUID is 7-Zip's fixed CLSID/IID from `IArchive.h`. The source generator
builds a vtable wrapper; `SevenZipLib.CreateObject` calls the native
`CreateObject` export to get back a COM pointer which the generator's marshaller
wraps as the managed interface.

**Implementing a callback:**

```csharp
[GeneratedComClass]
internal sealed partial class CompressionHandler : CompressionHandlerBase,
    IArchiveUpdateCallback,
    IArchiveUpdateCallback2,
    ICryptoGetTextPassword2
{
    // …
}
```

`[GeneratedComClass]` exposes the object via `QueryInterface` for every listed
interface. The native side calls `QueryInterface` on the callback pointer; the
generated wrapper dispatches to the correct managed method.

---

## Native library resolution

`NativeLibraryLoader` (called once, lazily, from `SevenZipLib`) uses
`NativeLibrary.SetDllImportResolver` to intercept the `7z` P/Invoke name and
redirect it to the platform-specific binary bundled in the NuGet package.

Resolution order:
1. `PlatformInfo.BuildRuntimeIdentifier()` → the current OS + architecture
   (e.g. `osx-arm64`, `win-x64`)
2. Walk candidate paths: next to the assembly, then `runtimes/<rid>/native/`
   under the assembly directory
3. `NativeLibrary.Load` the first path that exists

`PlatformInfo` uses `OperatingSystem.Is*()` (not `RuntimeInformation.IsOSPlatform`)
for JIT-friendly branch elimination.

---

## PROPVARIANT — 24 bytes on Windows, 16 bytes on POSIX

This is the most dangerous cross-platform pitfall in the codebase.

Windows `propidlbase.h` PROPVARIANT is **24 bytes** on x64 (outer union must
accommodate `DECIMAL` + counted-array pointer pairs). POSIX 7-Zip's
`CPP/Common/MyWindows.h` defines a minimal PROPVARIANT with only primitive
scalar types — **16 bytes** on x64.

`PropVariant` is declared `[StructLayout(Size = 16)]` to match POSIX. On Windows
a raw 16-byte buffer gets read at the wrong stride by the native side.

**Three patterns are used depending on call direction:**

| Direction | Pattern |
|---|---|
| Send array to native (`ISetProperties.SetProperties`) | Allocate `n × 24` unmanaged buffer on Windows; repack each `PropVariant` into the first 16 bytes of its slot, zeroing the trailing 8. On POSIX pin the managed array. |
| Receive single from native (`IInArchive.GetProperty`) | Change signature to `nint`; stackalloc 24 bytes (worst case); pass pointer; copy first 16 bytes back into a `PropVariant`. |
| Callback — native calls us with `ref PropVariant` | No change needed. Native provides a platform-sized buffer; our 16-byte write lands in bytes 0–15 correctly. |

**Symptom of a new call hitting this:** `E_INVALIDARG` on Windows from any
function taking `const PROPVARIANT *values`, or `AccessViolationException` on
the first managed call after a fill function. The code works on macOS/Linux and
fails only on Windows.

---

## BSTR and wchar_t — cross-platform string marshalling

C# `string` is 2-byte UTF-16. 7-Zip uses two wide-character string types:

### BSTR

Windows BSTR (COM standard): 4-byte length prefix + 2-byte UTF-16 chars.  
POSIX 7-Zip emulates BSTR: 4-byte length prefix + **`wchar_t`-sized** chars
(4 bytes each on POSIX).

`SevenZipBStr.cs` branches on `OperatingSystem.IsWindows()`:
- Windows → `Marshal.StringToBSTR` / `Marshal.FreeBSTR`
- POSIX → manual `CoTaskMemAlloc`, 4-byte length header, 4-byte chars

`SevenZipBStrMarshaller` is the custom marshaller used in `[GeneratedComInterface]`
signatures wherever 7-Zip passes a BSTR.

### wchar_t strings (non-BSTR)

POSIX `wchar_t` is 4 bytes (UCS-4 / UTF-32). Windows `wchar_t` is 2 bytes
(UTF-16). Naively passing a C# `string` as `wchar_t*` on POSIX corrupts every
other character — the silent data corruption looks like "method name not
recognised" or "wrong compression codec selected."

`SevenZipWideString.cs` allocates platform-aware: UTF-16 on Windows, UTF-32
on POSIX. `SevenZipWideStringMarshaller` is the custom marshaller for callback
directions where native passes a `wchar_t*` back to managed code.

**Where it matters:** `ApplyParametersTo` (compression method names),
`IArchiveOpenVolumeCallback.GetStream` (volume filenames).

---

## Nullable out parameters — marshal as `nint`

Some 7-Zip callbacks declare `ref` or `out` value-type parameters that may
arrive as `null` (meaning "not needed"). C# `ref T` requires non-null; a null
pointer causes `AccessViolationException` or SIGABRT.

Pattern: declare as `nint`, null-guard before dereferencing:

```csharp
void SetCompleted(nint completedPtr)
{
    if (completedPtr == 0) return;
    ulong value = *(ulong*)completedPtr;
}
```

Applies to: `IProgress.SetCompleted`, `IArchiveUpdateCallback.GetUpdateItemInfo`,
`IArchiveUpdateCallback2.GetVolumeSize`.

---

## Password / encryption — ICryptoGetTextPassword2

The encoder queries `ICryptoGetTextPassword2`
(GUID `23170F69-40C1-278A-0000-000500110000`) on the update callback via
`QueryInterface`. This is the **compression-side** password interface; do not
confuse it with `ICryptoSetPassword` (the decoder interface, different GUID).

To wire encryption: list `ICryptoGetTextPassword2` in the `[GeneratedComClass]`
interface list — `QueryInterface` is handled automatically by the source
generator. `CompressionHandlerBase.OnGetPassword` implements the shared logic.

Format-specific details:
- **7z**: `ICryptoGetTextPassword2` alone enables AES-256. Add `he=on` via
  `ISetProperties` to also encrypt headers (filenames, sizes, timestamps).
- **Zip**: defaults to weak ZipCrypto. Emit `em=AES` via `ISetProperties` to
  force AES-256. Header encryption is not supported by the Zip format.
- **Tar / GZip / BZip2 / Xz**: no encryption support — fail fast at the
  wrapper level; never silently produce an unencrypted archive.

---

## Archive format registry

`ArchiveFormatRegistry` maps `ArchiveFormat` enum values to the 7-Zip COM CLSIDs
defined in `ArchiveClassIds`. All CLSIDs share the prefix
`23170f69-40c1-278a-1000-0001xxxxxxxx` where the last four bytes identify the
format handler.

`SevenZipLib.CreateObject(classId, interfaceId)` calls the native `CreateObject`
export, which instantiates the C++ handler and returns a COM pointer.

---

## Adding a new archive format

1. Add an entry to `ArchiveFormat` enum.
2. Add the CLSID to `ArchiveClassIds` (from `IArchive.h` in the 7-Zip source).
3. Register it in `ArchiveFormatRegistry.BuildMap`.
4. Add the magic-byte signature to `ArchiveFormatDetector` if detection is needed.
5. Document encryption support (or lack of it) in `CompressionParameters.Validate`.
6. Add integration tests covering compress + extract round-trip.
7. Update the format × method compatibility matrix in README.

---

## Error handling contract

| Situation | Pattern |
|---|---|
| Expected failures (IO, bad archive, wrong password) | `Result<T>` via FluentResults — never throw from public API methods |
| Invariant violations / programming errors | `throw` (e.g. `ArgumentNullException`, `ObjectDisposedException`) |
| HRESULT failures from native | Checked against `HResult` constants; converted to `Result.Fail` with a descriptive message |

---

## Testing strategy

- **Unit tests** (`SevenZipSharper.UnitTests`): mock the native boundary via
  internal constructors that accept fake COM objects. No native binary needed.
  This is the only test project that measures code coverage (via Sonar).
- **Integration tests** (`SevenZipSharper.IntegrationTests`): run against the
  real native binary on all six RIDs (linux-x64, linux-arm64, win-x64,
  win-arm64, osx-arm64, osx-x64). These catch cross-platform interop regressions
  that unit tests cannot.
