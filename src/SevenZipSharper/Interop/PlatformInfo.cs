using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop;

internal static class PlatformInfo
{
    internal static string GetRuntimeIdentifier() =>
        BuildRuntimeIdentifier(GetCurrentOS(), RuntimeInformation.ProcessArchitecture);

    internal static string GetLibraryFileName() => BuildLibraryFileName(GetCurrentOS());

    internal static string BuildRuntimeIdentifier(OSPlatform os, Architecture architecture)
    {
        var archSegment = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported processor architecture: {architecture}."
            ),
        };

        if (os == OSPlatform.Windows)
            return $"win-{archSegment}";
        if (os == OSPlatform.OSX)
            return $"osx-{archSegment}";
        if (os == OSPlatform.Linux)
            return $"linux-{archSegment}";

        throw new PlatformNotSupportedException($"Unsupported operating system: {os}.");
    }

    internal static string BuildLibraryFileName(OSPlatform os)
    {
        if (os == OSPlatform.Windows)
            return "7z.dll";
        if (os == OSPlatform.OSX)
            return "7z.dylib";
        if (os == OSPlatform.Linux)
            return "7z.so";

        throw new PlatformNotSupportedException($"Unsupported operating system: {os}.");
    }

    private static OSPlatform GetCurrentOS()
    {
        if (OperatingSystem.IsWindows())
            return OSPlatform.Windows;
        if (OperatingSystem.IsMacOS())
            return OSPlatform.OSX;
        if (OperatingSystem.IsLinux())
            return OSPlatform.Linux;

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }
}
