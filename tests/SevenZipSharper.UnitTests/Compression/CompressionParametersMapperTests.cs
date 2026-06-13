using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.UnitTests.Compression;

[TestOf(typeof(CompressionParametersMapper))]
public sealed class CompressionParametersMapperTests
{
    [Test]
    public void ToSetProperties_Default_ProducesExpectedKeys()
    {
        var (names, _) = CompressionParametersMapper.ToSetProperties(CompressionParameters.Default);

        names.Should().Contain("x");
        names.Should().Contain("0");
        names.Should().Contain("s");
        names.Should().NotContain("d");
        names.Should().NotContain("fb");
        names.Should().NotContain("mt");
        names.Should().NotContain("he");
    }

    [Test]
    public void ToSetProperties_Default_LevelIsNormal()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default
        );
        var levelIndex = System.Array.IndexOf(names, "x");

        values[levelIndex].ToUInt32().Should().Be((uint)CompressionLevel.Normal);
    }

    [Test]
    public void ToSetProperties_Default_MethodIsLzma2()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default
        );
        var methodIndex = System.Array.IndexOf(names, "0");

        values[methodIndex].ToStringValue().Should().Be("LZMA2");
    }

    [Test]
    public void ToSetProperties_Default_SolidIsOn()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default
        );
        var solidIndex = System.Array.IndexOf(names, "s");

        values[solidIndex].ToStringValue().Should().Be("on");
    }

    [Test]
    public void ToSetProperties_Store_LevelIsZeroAndMethodIsCopyAndSolidIsOff()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Store
        );

        var levelIndex = System.Array.IndexOf(names, "x");
        var methodIndex = System.Array.IndexOf(names, "0");
        var solidIndex = System.Array.IndexOf(names, "s");

        values[levelIndex].ToUInt32().Should().Be(0u);
        values[methodIndex].ToStringValue().Should().Be("Copy");
        values[solidIndex].ToStringValue().Should().Be("off");
    }

    [Test]
    public void ToSetProperties_AddsDictionaryKey_WhenDictionarySizeSet()
    {
        var p = CompressionParameters.Default with { DictionarySize = 64 * 1024 * 1024u };
        var (names, values) = CompressionParametersMapper.ToSetProperties(p);

        var dictIndex = System.Array.IndexOf(names, "d");
        dictIndex.Should().BeGreaterThanOrEqualTo(0);
        values[dictIndex].ToUInt32().Should().Be(64 * 1024 * 1024u);
    }

    [Test]
    public void ToSetProperties_AddsWordSizeKey_WhenWordSizeSet()
    {
        var p = CompressionParameters.Default with { WordSize = 64 };
        var (names, values) = CompressionParametersMapper.ToSetProperties(p);

        var fbIndex = System.Array.IndexOf(names, "fb");
        fbIndex.Should().BeGreaterThanOrEqualTo(0);
        values[fbIndex].ToUInt32().Should().Be(64u);
    }

    [Test]
    public void ToSetProperties_AddsThreadKey_WhenThreadCountSet()
    {
        var p = CompressionParameters.Default with { ThreadCount = 4 };
        var (names, values) = CompressionParametersMapper.ToSetProperties(p);

        var mtIndex = System.Array.IndexOf(names, "mt");
        mtIndex.Should().BeGreaterThanOrEqualTo(0);
        values[mtIndex].ToUInt32().Should().Be(4u);
    }

    [Test]
    public void ToSetProperties_AddsEncryptHeadersKey_WhenPasswordAndEncryptHeadersSet()
    {
        var p = CompressionParameters.Default with
        {
            EncryptionPassword = "secret",
            EncryptHeaders = true,
        };
        var (names, _) = CompressionParametersMapper.ToSetProperties(p);

        names.Should().Contain("he");
    }

    [Test]
    public void ToSetProperties_DoesNotAddEncryptHeadersKey_WhenPasswordNotSet()
    {
        var p = CompressionParameters.Default with { EncryptHeaders = true };
        var (names, _) = CompressionParametersMapper.ToSetProperties(p);

        names.Should().NotContain("he");
    }

    [Test]
    public void ToSetProperties_NamesAndValuesHaveSameLength()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.MaximumLzma2
        );

        names.Length.Should().Be(values.Length);
    }
}
