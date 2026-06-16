using System;
using System.IO;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams;

[GeneratedComClass]
internal sealed partial class InStreamAdapter : SeekableStreamAdapterBase, IInStream
{
    internal InStreamAdapter(Stream stream)
        : base(stream) { }

    int ISequentialInStream.Read(byte[] data, uint size, out uint processedSize)
    {
        if (size > int.MaxValue)
        {
            processedSize = 0;
            return HResult.InvalidArg;
        }

        try
        {
            processedSize = (uint)_stream.Read(data, 0, (int)size);
            return HResult.Ok;
        }
        catch (Exception)
        {
            processedSize = 0;
            return HResult.Fail;
        }
    }

    int IInStream.Seek(long offset, uint seekOrigin, nint newPosition) =>
        SeekStream(offset, seekOrigin, newPosition);
}
