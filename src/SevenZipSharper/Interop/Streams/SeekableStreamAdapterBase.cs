using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop.Streams;

internal abstract class SeekableStreamAdapterBase
{
    protected readonly Stream _stream;

    protected SeekableStreamAdapterBase(Stream stream)
    {
        _stream = stream;
    }

    protected int SeekStream(long offset, uint seekOrigin, nint newPosition)
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
