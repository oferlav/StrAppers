using System.Text;
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
/// Regression tests for the generic Data Assessment Engine's Resources sensor
/// (<see cref="MetricsController.AppendAssessmentResourcesAsync"/>): previously, any metric with
/// UseResources enabled only ever saw a resource's name + URL — never its content — because only
/// GapAnalysis wired in <see cref="MetricsController.TryExtractResourceDocumentTextAsync"/>. The rule
/// is: if a metric's Resources sensor is on, uploaded document content must be extracted into the
/// prompt, the same way GapAnalysis already does it.
/// </summary>
public class AssessmentEngineResourceExtractionTests
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

    private static MetricsController BuildController(ApplicationDbContext db, Mock<IAzureBlobStorageService> blobMock)
    {
        var config = new ConfigurationBuilder().Build();
        return new MetricsController(
            db,
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
    public async Task NonImageResource_EmbedsExtractedDocumentContent()
    {
        var db = CreateDb();
        db.Resources.Add(new Resource
        {
            BoardId = "board-1", StudentId = 65, Name = "PRD_Ran",
            Url = "https://skillin.blob.core.windows.net/resources/board-1/65/PRD_Ran.docx",
            IsFigma = false, SprintNumber = 1,
        });
        await db.SaveChangesAsync();

        var docxBytes = BuildMinimalDocx("Donation intake flow: donor calls, staff logs details, driver dispatched by zone.");
        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(docxBytes) as Stream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "PRD_Ran.docx"));

        var controller = BuildController(db, blobMock);
        var sb = new StringBuilder();

        await controller.AppendAssessmentResourcesAsync(sb, "board-1", 65, 1, CancellationToken.None);

        var output = sb.ToString();
        Assert.Contains("document content extracted on server", output);
        Assert.Contains("Donation intake flow: donor calls, staff logs details, driver dispatched by zone.", output);
    }

    [Fact]
    public async Task NonImageResource_WhenExtractionFails_KeepsUrlOnly_DoesNotPenaliseNote()
    {
        var db = CreateDb();
        db.Resources.Add(new Resource
        {
            BoardId = "board-1", StudentId = 65, Name = "Scanned_Spec",
            Url = "https://skillin.blob.core.windows.net/resources/board-1/65/Scanned_Spec.pdf",
            IsFigma = false, SprintNumber = 1,
        });
        await db.SaveChangesAsync();

        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);
        blobMock.Setup(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Stream Stream, string ContentType, string FileName)?)null); // blob not found → extraction fails

        var controller = BuildController(db, blobMock);
        var sb = new StringBuilder();

        await controller.AppendAssessmentResourcesAsync(sb, "board-1", 65, 1, CancellationToken.None);

        var output = sb.ToString();
        Assert.Contains("Scanned_Spec.pdf".Replace(".pdf", ""), output); // resource name still shown
        Assert.Contains("content could not be extracted for review", output);
        Assert.Contains("do not penalise the student", output);
        Assert.DoesNotContain("document content extracted on server", output);
    }

    [Fact]
    public async Task ImageResource_IsNotSentToExtraction_ShowsLinkOnly()
    {
        var db = CreateDb();
        db.Resources.Add(new Resource
        {
            BoardId = "board-1", StudentId = 65, Name = "Mockup",
            Url = "https://skillin.blob.core.windows.net/resources/board-1/65/mockup.png",
            IsFigma = false, SprintNumber = 1,
        });
        await db.SaveChangesAsync();

        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);

        var controller = BuildController(db, blobMock);
        var sb = new StringBuilder();

        await controller.AppendAssessmentResourcesAsync(sb, "board-1", 65, 1, CancellationToken.None);

        Assert.Contains("[Resource] **Mockup**: https://skillin.blob.core.windows.net/resources/board-1/65/mockup.png", sb.ToString());
        blobMock.Verify(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FigmaResource_IsNotSentToExtraction_ShowsLinkOnly()
    {
        var db = CreateDb();
        db.Resources.Add(new Resource
        {
            BoardId = "board-1", StudentId = 65, Name = "Design File",
            Url = "https://figma.com/file/abc123",
            IsFigma = true, SprintNumber = 1,
        });
        await db.SaveChangesAsync();

        var blobMock = new Mock<IAzureBlobStorageService>();
        blobMock.SetupGet(b => b.IsConfigured).Returns(true);

        var controller = BuildController(db, blobMock);
        var sb = new StringBuilder();

        await controller.AppendAssessmentResourcesAsync(sb, "board-1", 65, 1, CancellationToken.None);

        Assert.Contains("[Figma] **Design File**: https://figma.com/file/abc123", sb.ToString());
        blobMock.Verify(b => b.OpenBlobReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoResources_EmitsNoneSentinel()
    {
        var db = CreateDb();
        var blobMock = new Mock<IAzureBlobStorageService>();
        var controller = BuildController(db, blobMock);
        var sb = new StringBuilder();

        await controller.AppendAssessmentResourcesAsync(sb, "board-1", 65, 1, CancellationToken.None);

        Assert.Contains("_(none for this sprint)_", sb.ToString());
    }
}
