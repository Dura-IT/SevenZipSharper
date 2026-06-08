using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams;

[GeneratedComClass]
internal sealed partial class OutStreamAdapter : IOutStream
{
    private readonly Stream _stream;

    internal OutStreamAdapter(Stream stream)
    {
        _stream = stream;
    }

    int ISequentialOutStream.Write(byte[] data, uint size, out uint processedSize)
    {
        if (size > int.MaxValue)
        {
            processedSize = 0;
            return HResult.InvalidArg;
        }

        try
        {
            _stream.Write(data, 0, (int)size);
            processedSize = size;
            return HResult.Ok;
        }
        catch (Exception)
        {
            processedSize = 0;
            return HResult.Fail;
        }
    }

    int IOutStream.Seek(long offset, uint seekOrigin, nint newPosition)
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

    int IOutStream.SetSize(ulong newSize)
    {
        try
        {
            _stream.SetLength((long)newSize);
            return HResult.Ok;
        }
        catch (Exception)
        {
            return HResult.Fail;
        }
    }
}
