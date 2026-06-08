using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop;

internal static partial class SevenZipLib
{
    [LibraryImport(NativeLibraryLoader.LibraryName, EntryPoint = "CreateObject")]
    private static partial int CreateObject(
        in Guid classId,
        in Guid interfaceId,
        out nint outObject
    );

    internal static unsafe T CreateArchiveObject<T>(Guid classId) // NOSONAR — unsafe required for COM vtable pointer cast
        where T : class
    {
        var interfaceId = typeof(T).GUID;
        var hr = CreateObject(in classId, in interfaceId, out var ptr);

        if (hr != HResult.Ok || ptr == 0)
            throw new InvalidOperationException(
                $"Failed to create 7-Zip archive object (HRESULT: 0x{hr:X8})."
            );

        return ComInterfaceMarshaller<T>.ConvertToManaged((void*)ptr)
            ?? throw new InvalidOperationException("CreateObject returned null.");
    }

    // COM class IDs for 7-Zip archive handler objects.
    // Pattern: 23170F69-40C1-278A-1000-000110{formatId}0000
    internal static readonly Guid SevenZipClassId = new("23170F69-40C1-278A-1000-000110070000");
    internal static readonly Guid ZipClassId = new("23170F69-40C1-278A-1000-000110010000");
    internal static readonly Guid BZip2ClassId = new("23170F69-40C1-278A-1000-000110020000");
    internal static readonly Guid ArjClassId = new("23170F69-40C1-278A-1000-000110040000");
    internal static readonly Guid LzhClassId = new("23170F69-40C1-278A-1000-000110060000");
    internal static readonly Guid CabClassId = new("23170F69-40C1-278A-1000-000110080000");
    internal static readonly Guid IsoClassId = new("23170F69-40C1-278A-1000-0001100E0000");
    internal static readonly Guid GZipClassId = new("23170F69-40C1-278A-1000-000110EF0000");
    internal static readonly Guid TarClassId = new("23170F69-40C1-278A-1000-000110EE0000");
    internal static readonly Guid XzClassId = new("23170F69-40C1-278A-1000-000110F80000");
    internal static readonly Guid WimClassId = new("23170F69-40C1-278A-1000-000110E60000");
}
