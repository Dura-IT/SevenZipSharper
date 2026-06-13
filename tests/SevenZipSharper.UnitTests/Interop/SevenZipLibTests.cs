using System;
using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(SevenZipLib))]
public sealed class SevenZipLibTests
{
    private static IEnumerable<Guid> AllFormatGuids()
    {
        yield return SevenZipLib.SevenZipClassId;
        yield return SevenZipLib.ZipClassId;
        yield return SevenZipLib.BZip2ClassId;
        yield return SevenZipLib.ArjClassId;
        yield return SevenZipLib.LzhClassId;
        yield return SevenZipLib.CabClassId;
        yield return SevenZipLib.IsoClassId;
        yield return SevenZipLib.GZipClassId;
        yield return SevenZipLib.TarClassId;
        yield return SevenZipLib.XzClassId;
        yield return SevenZipLib.WimClassId;
    }

    [Test]
    public void AllFormatGuids_AreUnique()
    {
        AllFormatGuids().Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void AllFormatGuids_ShareExpectedPrefix()
    {
        foreach (var guid in AllFormatGuids())
            guid.ToString().Should().StartWith("23170f69-40c1-278a-1000-0001");
    }
}
