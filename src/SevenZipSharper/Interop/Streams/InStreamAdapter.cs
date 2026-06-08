using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams;

[GeneratedComClass]
internal sealed partial class InStreamAdapter : IInStream
{
    private readonly Stream _stream;

    internal InStreamAdapter(Stream stream)
    {
        _stream = stream;
    }

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

    int IInStream.Seek(long offset, uint seekOrigin, nint newPosition)
    {
        if (seekOrigin > 2)
        {
            if (newPosition != nint.Zero)
                Marshal.WriteInt64(newPosition, 0);
            return HResult.InvalidArg;
        }

        try
        {
            var pos = _stream.Seek(offset, (SeekOrigin)seekOrigin);
            if (newPosition != nint.Zero)
                Marshal.WriteInt64(newPosition, pos);
            return HResult.Ok;
        }
        catch (Exception)
        {
            if (newPosition != nint.Zero)
                Marshal.WriteInt64(newPosition, 0);
            return HResult.Fail;
        }
    }
}
