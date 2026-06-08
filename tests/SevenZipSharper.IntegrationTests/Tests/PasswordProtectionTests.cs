using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace SevenZipSharper.IntegrationTests;

/// <summary>
/// Exercises <see cref="IPasswordProvider"/> — directly verifies the BSTR fix for
/// managed→native <c>out string</c> marshalling on non-Windows platforms.
/// </summary>
[TestFixture]
public class PasswordProtectionTests
{
    private byte[] _archiveBytes = System.Array.Empty<byte>();

    [OneTimeSetUp]
    public void LoadFixture()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SevenZipSharper.IntegrationTests.Fixtures.password-protected.7z";
        using var stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new System.InvalidOperationException(
                $"Embedded resource '{resourceName}' not found."
            );
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _archiveBytes = ms.ToArray();
    }

    [Test]
    public async Task OpenAsync_WithCorrectPassword_Succeeds()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );

        var result = await extractor.OpenAsync(password: "TestPassword123");

        result.IsSuccess.Should().BeTrue("correct password should open the archive");
    }

    [Test]
    public async Task OpenAsync_WithWrongPassword_FailsOrExtractFails()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );

        // 7-Zip may succeed at Open with a wrong password (metadata is not encrypted by default)
        // but extraction will fail with DataError / CrcError.
        var openResult = await extractor.OpenAsync(password: "WrongPassword");
        if (openResult.IsFailed)
        {
            // Archive headers were also encrypted — open correctly fails.
            return;
        }

        using var output = new MemoryStream();
        var entries = (await extractor.ListEntriesAsync()).Value;
        var extractResult = await extractor.ExtractEntryAsync(entries[0], output);

        extractResult
            .IsFailed.Should()
            .BeTrue("extraction with wrong password should produce a CRC / data error");
    }

    [Test]
    public async Task ExtractAsync_WithCorrectPassword_ContentIsReadable()
    {
        using var extractor = new SevenZipExtractor(
            new MemoryStream(_archiveBytes),
            ArchiveFormat.SevenZip,
            NullLogger<SevenZipExtractor>.Instance
        );
        await extractor.OpenAsync(password: "TestPassword123");
        var entries = (await extractor.ListEntriesAsync()).Value;
        using var output = new MemoryStream();

        var result = await extractor.ExtractEntryAsync(entries[0], output);

        result.IsSuccess.Should().BeTrue();
        output.Length.Should().BeGreaterThan(0);
        var text = System.Text.Encoding.UTF8.GetString(output.ToArray());
        text.Should().Contain("SevenZipSharper integration test secret content");
    }
}
