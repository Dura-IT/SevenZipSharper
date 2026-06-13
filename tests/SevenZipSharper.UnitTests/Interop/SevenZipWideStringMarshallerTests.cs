using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(SevenZipWideStringMarshaller))]
public sealed unsafe class SevenZipWideStringMarshallerTests
{
    [Test]
    public void ConvertToUnmanaged_NullManaged_ReturnsNullPointer()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(null);

        ((nint)ptr).Should().Be(nint.Zero);
    }

    [Test]
    public void ConvertToManaged_NullPointer_ReturnsNull()
    {
        SevenZipWideStringMarshaller.ConvertToManaged(null).Should().BeNull();
    }

    [Test]
    public void Free_NullPointer_DoesNotThrow()
    {
        var act = () => SevenZipWideStringMarshaller.Free(null);

        act.Should().NotThrow();
    }

    [Test]
    public void ConvertToUnmanaged_Read_RoundTrip_EmptyString()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(string.Empty);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(string.Empty);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_Read_RoundTrip_SingleAsciiChar()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("x");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("x");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_Read_RoundTrip_MultiCharAscii()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("LZMA");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("LZMA");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_Read_RoundTrip_MixedCaseKeyword()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("LZMA2");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("LZMA2");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_Read_RoundTrip_BmpUnicode()
    {
        // U+00E9 (é), U+4E2D (中), U+00FC (ü) — all in BMP, safely representable as wchar_t
        const string value = "é中ü";
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(value);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [TestCase("on")]
    [TestCase("off")]
    [TestCase("e")]
    [TestCase("0")]
    public void ConvertToUnmanaged_Read_RoundTrip_CommonPropertyValues(string value)
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(value);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }
}
