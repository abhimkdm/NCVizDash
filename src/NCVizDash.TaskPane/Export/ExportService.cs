using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Logging;
using NCVizDash.TaskPane.Controls;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace NCVizDash.TaskPane.Export;

/// <summary>
/// Enterprise reporting export: PDF (per widget, via WebView2's native print-to-PDF),
/// PNG (per widget, via WebView2 preview capture), and PowerPoint (one slide per
/// widget, each slide containing that widget's captured PNG as a full-bleed image).
/// <para>
/// Excel Snapshot export (pasting widget images onto a new worksheet) is not
/// implemented here — it needs <c>Microsoft.Office.Interop.Excel</c>, which this
/// project deliberately does not reference (see the Phase 7 "why TaskPane doesn't
/// reference DuckDB" architecture note for the same reasoning applied here). It
/// belongs in <c>NCVizDash.ExcelAddIn</c>, calling back into
/// <see cref="CaptureAllWidgetsAsPngAsync"/> for the image bytes and then using
/// <c>Worksheet.Shapes.AddPicture</c> to place them.
/// </para>
/// </summary>
public sealed class ExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    /// <summary>Exports a single widget to a standalone PDF file.</summary>
    public Task<bool> ExportWidgetToPdfAsync(WidgetCard widget, string filePath) =>
        widget.ExportToPdfAsync(filePath);

    /// <summary>Exports a single widget to a standalone PNG file.</summary>
    public Task<bool> ExportWidgetToPngAsync(WidgetCard widget, string filePath) =>
        widget.ExportToPngAsync(filePath);

    /// <summary>Captures every widget on the canvas as PNG bytes, keyed by widget title (for downstream embedding).</summary>
    public async Task<Dictionary<string, byte[]>> CaptureAllWidgetsAsPngAsync(IReadOnlyList<WidgetCard> widgets)
    {
        var results = new Dictionary<string, byte[]>();

        foreach (var card in widgets)
        {
            var bytes = await card.CapturePngBytesAsync();
            if (bytes is not null)
                results[card.Widget.Title] = bytes;
        }

        return results;
    }

    /// <summary>
    /// Exports every widget on the canvas as a PowerPoint deck — one slide per
    /// widget, each slide title matching the widget title and containing a
    /// full-bleed image of the widget's captured render.
    /// </summary>
    public async Task<bool> ExportDashboardToPptxAsync(string dashboardName, IReadOnlyList<WidgetCard> widgets, string filePath)
    {
        try
        {
            var images = await CaptureAllWidgetsAsPngAsync(widgets);
            if (images.Count == 0)
            {
                _logger.LogWarning("No widget images captured; PPTX export aborted.");
                return false;
            }

            BuildPresentation(dashboardName, images, filePath);
            _logger.LogInformation("Exported {Count} widget(s) to PowerPoint at '{Path}'.", images.Count, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export dashboard to PowerPoint.");
            return false;
        }
    }

    // ── OpenXML PPTX construction ─────────────────────────────────────────────

    private const long SlideWidthEmu = 12192000;  // 16:9 widescreen, EMUs
    private const long SlideHeightEmu = 6858000;

    private static void BuildPresentation(string dashboardName, Dictionary<string, byte[]> images, string filePath)
    {
        using var doc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation);
        var presentationPart = doc.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var slideMasterPart = CreateSlideMasterPart(presentationPart);
        var slideIdList = new SlideIdList();
        uint slideId = 256;

        foreach (var kvp in images)
        {
            var slidePart = CreateSlidePart(presentationPart, slideMasterPart, kvp.Key, kvp.Value);
            var relId = presentationPart.GetIdOfPart(slidePart);
            slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = relId });
        }

        presentationPart.Presentation.Append(
            new SlideMasterIdList(new SlideMasterId { Id = 2147483648, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) }),
            slideIdList,
            new SlideSize { Cx = (Int32Value)(int)SlideWidthEmu, Cy = (Int32Value)(int)SlideHeightEmu },
            new NotesSize { Cx = 6858000, Cy = 9144000 });

        presentationPart.Presentation.Save();
    }

    private static SlideMasterPart CreateSlideMasterPart(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();

        slideLayoutPart.SlideLayout = new SlideLayout(
            new CommonSlideData(new ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup()))))
        { Type = SlideLayoutValues.Blank };

        slideMasterPart.SlideMaster = new SlideMaster(
            new CommonSlideData(new ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.SlideLayoutIdList(new SlideLayoutId
            {
                Id = 2147483649,
                RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart)
            }));

        slideLayoutPart.SlideLayout.Save();
        slideMasterPart.SlideMaster.Save();
        return slideMasterPart;
    }

    private static SlidePart CreateSlidePart(
        PresentationPart presentationPart, SlideMasterPart slideMasterPart, string title, byte[] pngBytes)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var slideLayoutPart = slideMasterPart.SlideLayoutParts.First();
        slidePart.AddPart(slideLayoutPart);

        var imagePart = slidePart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(pngBytes))
            imagePart.FeedData(stream);
        var imageRelId = slidePart.GetIdOfPart(imagePart);

        // Title text box across the top; image fills the remaining slide area.
        var shapeTree = new ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()),
            BuildTitleShape(title),
            BuildPictureShape(imageRelId, 3));

        slidePart.Slide = new Slide(new CommonSlideData(shapeTree));
        slidePart.Slide.Save();
        return slidePart;
    }

    private static P.Shape BuildTitleShape(string title) => new(
        new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = 2, Name = "Title" },
            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
            new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
        new P.ShapeProperties(new A.Transform2D(
            new A.Offset { X = 457200, Y = 274638 },
            new A.Extents { Cx = 11277600, Cy = 900000 })),
        new P.TextBody(
            new A.BodyProperties(),
            new A.ListStyle(),
            new A.Paragraph(new A.Run(new A.Text(title)))));

    private static P.Picture BuildPictureShape(string imageRelId, uint shapeId) => new(
        new P.NonVisualPictureProperties(
            new P.NonVisualDrawingProperties { Id = shapeId, Name = "Chart" },
            new P.NonVisualPictureDrawingProperties(),
            new ApplicationNonVisualDrawingProperties()),
        new P.BlipFill(
            new A.Blip { Embed = imageRelId },
            new A.Stretch(new A.FillRectangle())),
        new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = 457200, Y = 1350000 },
                new A.Extents { Cx = 11277600, Cy = 5200000 }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
}
