using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the "ERD document is present but its content is not directly viewable" gap:
/// Gap Analysis only showed the model a resource's URL, never its content, even though the same
/// extraction (<see cref="ResourceDocumentContentExtractor"/>) already exists and is used by the
/// mentor-review feature. <see cref="MetricsController.TryExtractResourceDocumentTextAsync"/> wires
/// the same extractor into Gap Analysis so a student's uploaded .docx/.pdf/etc. is actually read.
/// </summary>
public class GapAnalysisResourceDocumentExtractionTests
{
    private static byte[] BuildMinimalDocx(string bodyText)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text(bodyText)))));
            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetricsController BuildController(Mock<IAzureBlobStorageService> blobMock)
    {
        var config = new ConfigurationBuilder().Build();
        return new MetricsController(
            CreateDb(),
            Mock.Of<ITrelloService>(),
            Mock.Of<IGitHubService>(),
            config,
            NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(),
            blobMock.Object);
    }

    [Fact]
    public async Task ExtractsRealTextFromUploadedDocx()
    {
        var docxBytes = BuildMinimalDocx("High-level ERD: Centres table relates to Coordinators via CentreId (FK). Status is an ENUM: active, closed.");
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(docxBytes) as Stream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "erd.docx"));

        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync(
            "https://skillin.blob.core.windows.net/resources/board/3/erd.docx", "erd", CancellationToken.None);

        Assert.NotNull(text);
        Assert.Contains("Centres table relates to Coordinators", text);
    }

    [Fact]
    public async Task BlobNotConfigured_ReturnsNull()
    {
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(false);
        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync(
            "https://skillin.blob.core.windows.net/resources/board/3/erd.docx", "erd", CancellationToken.None);

        Assert.Null(text);
        blobMock.Verify(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BlobNotFound_ReturnsNull_DoesNotThrow()
    {
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Stream Stream, string ContentType, string FileName)?)null);

        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync(
            "https://skillin.blob.core.windows.net/resources/board/3/missing.docx", "missing", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task InvalidUrl_ReturnsNull_DoesNotThrow()
    {
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync("not-a-url", "erd", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task UnsupportedType_ReturnsNull_NotThrow()
    {
        // Legacy .doc has no extractor in ResourceDocumentContentExtractor — must degrade gracefully.
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(new byte[] { 1, 2, 3, 4 }) as Stream, "application/msword", "old.doc"));

        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync(
            "https://skillin.blob.core.windows.net/resources/board/3/old.doc", "old", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task LongDocument_IsTruncated()
    {
        var longText = new string('x', 10_000);
        var docxBytes = BuildMinimalDocx(longText);
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(docxBytes) as Stream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "big.docx"));

        var controller = BuildController(blobMock);

        var text = await controller.TryExtractResourceDocumentTextAsync(
            "https://skillin.blob.core.windows.net/resources/board/3/big.docx", "big", CancellationToken.None);

        Assert.NotNull(text);
        Assert.True(text!.Length < longText.Length);
        Assert.Contains("truncated", text);
    }
}
