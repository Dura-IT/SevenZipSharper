using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop;

/// <summary>
/// Custom marshaller for null-terminated wchar_t string parameters in 7-Zip COM interface declarations.
/// </summary>
/// <remarks>
/// Use via <c>[MarshalUsing(typeof(SevenZipWideStringMarshaller))]</c> on <c>string</c> parameters
/// in <c>[GeneratedComInterface]</c> declarations where 7-Zip passes a raw <c>const wchar_t*</c>.
/// Do NOT use this for BSTR parameters; use <see cref="SevenZipBStrMarshaller"/> instead.
/// </remarks>
[CustomMarshaller(typeof(string), MarshalMode.Default, typeof(SevenZipWideStringMarshaller))]
internal static unsafe class SevenZipWideStringMarshaller // NOSONAR — unsafe required for wchar_t pointer marshalling
{
    /// <summary>
    /// Converts a managed string to an unmanaged wchar_t pointer.
    /// </summary>
    public static char* ConvertToUnmanaged(string? managed) =>
        managed is null ? null : (char*)SevenZipWideString.Alloc(managed);

    /// <summary>
    /// Converts an unmanaged wchar_t pointer to a managed string.
    /// </summary>
    public static string? ConvertToManaged(char* unmanaged) =>
        SevenZipWideString.Read((nint)unmanaged);

    /// <summary>
    /// Frees an unmanaged wchar_t pointer.
    /// </summary>
    public static void Free(char* unmanaged) => SevenZipWideString.Free((nint)unmanaged);
}
