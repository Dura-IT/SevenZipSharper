using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper;
using SevenZipSharper.Compression;

namespace SevenZipSharper.UnitTests.Compression;

[TestOf(typeof(CompressionParametersMapper))]
public sealed class CompressionParametersMapperTests
{
    [Test]
    public void ToSetProperties_Default_ProducesExpectedKeys()
    {
        var (names, _) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.SevenZip
        );

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
            CompressionParameters.Default,
            ArchiveFormat.SevenZip
        );
        var levelIndex = System.Array.IndexOf(names, "x");

        values[levelIndex].ToUInt32().Should().Be((uint)CompressionLevel.Normal);
    }

    [Test]
    public void ToSetProperties_Default_MethodIsLzma2()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.SevenZip
        );
        var methodIndex = System.Array.IndexOf(names, "0");

        values[methodIndex].ToStringValue().Should().Be("LZMA2");
    }

    [Test]
    public void ToSetProperties_Default_SolidIsOn()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.SevenZip
        );
        var solidIndex = System.Array.IndexOf(names, "s");

        values[solidIndex].ToStringValue().Should().Be("on");
    }

    [Test]
    public void ToSetProperties_Store_LevelIsZeroAndMethodIsCopyAndSolidIsOff()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Store,
            ArchiveFormat.SevenZip
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
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            p,
            ArchiveFormat.SevenZip
        );

        var dictIndex = System.Array.IndexOf(names, "d");
        dictIndex.Should().BeGreaterThanOrEqualTo(0);
        values[dictIndex].ToUInt32().Should().Be(64 * 1024 * 1024u);
    }

    [Test]
    public void ToSetProperties_AddsWordSizeKey_WhenWordSizeSet()
    {
        var p = CompressionParameters.Default with { WordSize = 64 };
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            p,
            ArchiveFormat.SevenZip
        );

        var fbIndex = System.Array.IndexOf(names, "fb");
        fbIndex.Should().BeGreaterThanOrEqualTo(0);
        values[fbIndex].ToUInt32().Should().Be(64u);
    }

    [Test]
    public void ToSetProperties_AddsThreadKey_WhenThreadCountSet()
    {
        var p = CompressionParameters.Default with { ThreadCount = 4 };
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            p,
            ArchiveFormat.SevenZip
        );

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
        var (names, _) = CompressionParametersMapper.ToSetProperties(p, ArchiveFormat.SevenZip);

        names.Should().Contain("he");
    }

    [Test]
    public void ToSetProperties_DoesNotAddEncryptHeadersKey_WhenPasswordNotSet()
    {
        var p = CompressionParameters.Default with { EncryptHeaders = true };
        var (names, _) = CompressionParametersMapper.ToSetProperties(p, ArchiveFormat.SevenZip);

        names.Should().NotContain("he");
    }

    [Test]
    public void ToSetProperties_NamesAndValuesHaveSameLength()
    {
        var (names, values) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.MaximumLzma2,
            ArchiveFormat.SevenZip
        );

        names.Length.Should().Be(values.Length);
    }

    [Test]
    public void ToSetProperties_ZipWithPassword_EmitsEmAes256()
    {
        var p = CompressionParameters.Default with { EncryptionPassword = "secret" };
        var (names, values) = CompressionParametersMapper.ToSetProperties(p, ArchiveFormat.Zip);

        var emIndex = System.Array.IndexOf(names, "em");
        emIndex.Should().BeGreaterThanOrEqualTo(0);
        values[emIndex].ToStringValue().Should().Be("AES");
    }

    [Test]
    public void ToSetProperties_SevenZipWithPassword_DoesNotEmitEm()
    {
        var p = CompressionParameters.Default with { EncryptionPassword = "secret" };
        var (names, _) = CompressionParametersMapper.ToSetProperties(p, ArchiveFormat.SevenZip);

        names.Should().NotContain("em");
    }

    [Test]
    public void ToSetProperties_ZipWithoutPassword_DoesNotEmitEm()
    {
        var (names, _) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.Zip
        );

        names.Should().NotContain("em");
    }

    [Test]
    public void ToSetProperties_Zip_DoesNotEmitSolidMode()
    {
        var (names, _) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.Zip
        );

        names.Should().NotContain("s");
    }

    [Test]
    public void ToSetProperties_SevenZip_EmitsSolidMode()
    {
        var (names, _) = CompressionParametersMapper.ToSetProperties(
            CompressionParameters.Default,
            ArchiveFormat.SevenZip
        );

        names.Should().Contain("s");
    }

    [Test]
    public void ToSetProperties_UnknownCompressionMethod_ThrowsArgumentOutOfRange()
    {
        var parameters = CompressionParameters.Default with { Method = (CompressionMethod)999 };

        var act = () =>
            CompressionParametersMapper.ToSetProperties(parameters, ArchiveFormat.SevenZip);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ToSetProperties_SingleCodecFormats_DoNotEmitMethodProperty()
    {
        foreach (var format in new[] { ArchiveFormat.GZip, ArchiveFormat.BZip2, ArchiveFormat.Tar })
        {
            var (names, _) = CompressionParametersMapper.ToSetProperties(
                CompressionParameters.Default,
                format
            );

            names
                .Should()
                .NotContain("0", $"{format} has a fixed codec and rejects the method property");
        }
    }

    [Test]
    public void ToSetProperties_SevenZipAndZip_EmitMethodProperty()
    {
        foreach (var format in new[] { ArchiveFormat.SevenZip, ArchiveFormat.Zip })
        {
            var (names, _) = CompressionParametersMapper.ToSetProperties(
                CompressionParameters.Default,
                format
            );

            names.Should().Contain("0", $"{format} supports codec selection");
        }
    }
}
