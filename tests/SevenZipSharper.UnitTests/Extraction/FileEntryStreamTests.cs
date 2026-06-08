using System;
using System.IO;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Extraction;

[TestOf(typeof(FileEntryStream))]
public class FileEntryStreamTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Write_WritesDataToFile_AndReturnsOk()
    {
        var path = Path.Combine(_tempDir, "out.bin");
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new FileEntryStream(path);

        var hr = stream.Write(data, (uint)data.Length, out var processedSize);

        hr.Should().Be(HResult.Ok);
        processedSize.Should().Be((uint)data.Length);
        stream.Dispose();
        File.ReadAllBytes(path).Should().Equal(data);
    }

    [Test]
    public void Write_ReturnsFail_AndZeroProcessedSize_WhenStreamIsDisposed()
    {
        var path = Path.Combine(_tempDir, "disposed.bin");
        var stream = new FileEntryStream(path);
        stream.Dispose();

        var hr = stream.Write(new byte[] { 1 }, 1, out var processedSize);

        hr.Should().Be(HResult.Fail);
        processedSize.Should().Be(0u);
    }

    [Test]
    public void Constructor_CreatesNestedDirectories()
    {
        var nested = Path.Combine(_tempDir, "a", "b", "file.txt");

        using var stream = new FileEntryStream(nested);

        Directory.Exists(Path.Combine(_tempDir, "a", "b")).Should().BeTrue();
    }

    [Test]
    public void Dispose_ClosesFileStream_AllowingAnotherWriterToOpen()
    {
        var path = Path.Combine(_tempDir, "reopen.bin");
        var stream = new FileEntryStream(path);
        stream.Dispose();

        var act = () =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        };

        act.Should().NotThrow<IOException>();
    }
}
