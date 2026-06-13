using System;
using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using SevenZipSharper;
using SevenZipSharper.Compression;

namespace SevenZipSharper.UnitTests.DependencyInjection;

[TestOf(typeof(SevenZipSharperServiceCollectionExtensions))]
public sealed class SevenZipSharperServiceCollectionExtensionsTests
{
    [Test]
    public void AddSevenZipSharper_RegistersExtractorFactory()
    {
        var services = new Mock<IServiceCollection>();

        services.Object.AddSevenZipSharper();

        services.Verify(s =>
            s.Add(
                It.Is<ServiceDescriptor>(d =>
                    d.ServiceType == typeof(Func<Stream, ArchiveFormat, SevenZipExtractor>)
                    && d.Lifetime == ServiceLifetime.Singleton
                )
            )
        );
    }

    [Test]
    public void AddSevenZipSharper_RegistersCompressorFactory()
    {
        var services = new Mock<IServiceCollection>();

        services.Object.AddSevenZipSharper();

        services.Verify(s =>
            s.Add(
                It.Is<ServiceDescriptor>(d =>
                    d.ServiceType
                        == typeof(Func<ArchiveFormat, CompressionParameters, SevenZipCompressor>)
                    && d.Lifetime == ServiceLifetime.Singleton
                )
            )
        );
    }

    [Test]
    public void AddSevenZipSharper_ReturnsServiceCollection()
    {
        var services = new Mock<IServiceCollection>();

        var result = services.Object.AddSevenZipSharper();

        result.Should().BeSameAs(services.Object);
    }
}
