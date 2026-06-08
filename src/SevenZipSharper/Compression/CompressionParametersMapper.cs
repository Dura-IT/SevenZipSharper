using System.Collections.Generic;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;

namespace SevenZipSharper.Compression;

internal static class CompressionParametersMapper
{
    // 7-Zip ISetProperties key names
    private const string PropKeyLevel = "x";
    private const string PropKeyMethod = "0"; // codec at position 0 in the filter chain
    private const string PropKeySolid = "s";
    private const string PropKeyDictionarySize = "d";
    private const string PropKeyWordSize = "fb";
    private const string PropKeyThreadCount = "mt";
    private const string PropKeyEncryptHeaders = "he";

    private const string PropValueOn = "on";
    private const string PropValueOff = "off";

    // ISetProperties method name values
    private const string MethodNameLzma = "LZMA";
    private const string MethodNameLzma2 = "LZMA2";
    private const string MethodNameBZip2 = "BZip2";
    private const string MethodNameDeflate = "Deflate";
    private const string MethodNamePpmd = "PPMd";
    private const string MethodNameCopy = "Copy";

    internal static (string[] Names, PropVariant[] Values) ToSetProperties(
        CompressionParameters parameters
    )
    {
        var names = new List<string>();
        var values = new List<PropVariant>();

        void Add(string name, PropVariant value)
        {
            names.Add(name);
            values.Add(value);
        }

        Add(PropKeyLevel, PropVariant.FromUInt32((uint)parameters.Level));
        Add(PropKeyMethod, PropVariant.FromString(ToMethodName(parameters.Method)));
        Add(
            PropKeySolid,
            PropVariant.FromString(parameters.SolidMode ? PropValueOn : PropValueOff)
        );

        if (parameters.DictionarySize.HasValue)
            Add(PropKeyDictionarySize, PropVariant.FromUInt32(parameters.DictionarySize.Value));

        if (parameters.WordSize.HasValue)
            Add(PropKeyWordSize, PropVariant.FromUInt32(parameters.WordSize.Value));

        if (parameters.ThreadCount.HasValue)
            Add(PropKeyThreadCount, PropVariant.FromUInt32((uint)parameters.ThreadCount.Value));

        if (parameters.EncryptHeaders && !string.IsNullOrEmpty(parameters.EncryptionPassword))
            Add(PropKeyEncryptHeaders, PropVariant.FromString(PropValueOn));

        return (names.ToArray(), values.ToArray());
    }

    private static string ToMethodName(CompressionMethod method) =>
        method switch
        {
            CompressionMethod.Lzma => MethodNameLzma,
            CompressionMethod.Lzma2 => MethodNameLzma2,
            CompressionMethod.BZip2 => MethodNameBZip2,
            CompressionMethod.Deflate => MethodNameDeflate,
            CompressionMethod.Ppmd => MethodNamePpmd,
            CompressionMethod.Copy => MethodNameCopy,
            _ => throw new System.ArgumentOutOfRangeException(nameof(method), method, null),
        };
}
