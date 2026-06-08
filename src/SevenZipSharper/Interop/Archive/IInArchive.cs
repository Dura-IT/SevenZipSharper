using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive;

[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600600000")]
internal partial interface IInArchive
{
    [PreserveSig]
    int Open(
        IInStream stream,
        IntPtr maxCheckStartPosition,
        IArchiveOpenCallback? openArchiveCallback
    );

    [PreserveSig]
    int Close();

    [PreserveSig]
    int GetNumberOfItems(out uint numItems);

    [PreserveSig]
    int GetProperty(uint index, ItemPropId propId, ref PropVariant value);

    [PreserveSig]
    int Extract(
        [In, MarshalUsing(CountElementName = "numItems")] uint[]? indices,
        uint numItems,
        int testMode,
        IArchiveExtractCallback extractCallback
    );

    [PreserveSig]
    int GetArchiveProperty(ItemPropId propId, ref PropVariant value);

    [PreserveSig]
    int GetNumberOfProperties(out uint numProps);

    [PreserveSig]
    int GetPropertyInfo(
        uint index,
        [MarshalUsing(typeof(SevenZipBStrMarshaller))] out string? name,
        out uint propId,
        out ushort varType
    );

    [PreserveSig]
    int GetNumberOfArchiveProperties(out uint numProps);

    [PreserveSig]
    int GetArchivePropertyInfo(
        uint index,
        [MarshalUsing(typeof(SevenZipBStrMarshaller))] out string? name,
        out uint propId,
        out ushort varType
    );
}
