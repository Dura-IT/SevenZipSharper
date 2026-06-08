using System;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Extraction;

// Writes an extracted entry to a new file and owns the underlying FileStream.
[GeneratedComClass]
internal sealed partial class FileEntryStream : ISequentialOutStream, IDisposable
{
    private readonly FileStream _file;

    internal FileEntryStream(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    public int Write(byte[] data, uint size, out uint processedSize)
    {
        try
        {
            _file.Write(data, 0, (int)size);
            processedSize = size;
            return HResult.Ok;
        }
        catch (Exception)
        {
            processedSize = 0;
            return HResult.Fail;
        }
    }

    public void Dispose() => _file.Dispose();
}
