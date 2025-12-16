using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Valuation.Api.Models;  // ← adjust to your actual models namespace
using QuestPDF.Companion;
using SkiaSharp; // ✅ REQUIRED FOR WATERMARKING

namespace Valuation.Api.Services
{
    public interface IFinalReportPdfService
    {
        Task<byte[]> GeneratePdfAsync(ValuationDocument report);
    }

    public class FinalReportPdfService : IFinalReportPdfService
    {
        private readonly HttpClient _httpClient;

        public FinalReportPdfService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// ✅ Helper method to draw a semi-transparent black bar with text (Date | Location)
        /// on the bottom of the image.
        /// </summary>
        private byte[] AddWatermark(byte[] imageBytes, DateTime? date, string location)
        {
            // If no metadata is provided, return the original image
            if (date == null && string.IsNullOrEmpty(location)) return imageBytes;

            try
            {
                using var bitmap = SKBitmap.Decode(imageBytes);
                if (bitmap == null) return imageBytes; // Return original if decode fails

                using var canvas = new SKCanvas(bitmap);

                // 1. Draw Semi-Transparent Black Bar at Bottom
                int barHeight = (int)(bitmap.Height * 0.08); // Bar is 8% of image height
                if (barHeight < 40) barHeight = 40; // Enforce minimum height

                var footerRect = new SKRect(0, bitmap.Height - barHeight, bitmap.Width, bitmap.Height);
                var barPaint = new SKPaint
                {
                    Color = new SKColor(0, 0, 0, 160), // Black with ~60% Opacity
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(footerRect, barPaint);

                // 2. Prepare Text
                var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                    TextSize = (float)(barHeight * 0.5) // Text size is half of bar height
                };

                // Format: "15-Dec-2025 14:30 | Kakinada, AP"
                string dateStr = date.HasValue ? date.Value.ToString("dd-MMM-yyyy HH:mm") : "";
                string locStr = !string.IsNullOrEmpty(location) ? location : "";
                
                // Construct string joining non-empty parts
                string watermarkText = $"{dateStr} | {locStr}".Trim('|', ' ');

                // 3. Draw Text (Centered vertically in the bar, Left aligned with padding)
                float x = 20; 
                float y = bitmap.Height - (barHeight / 2) + (textPaint.TextSize / 2.5f);
                
                canvas.DrawText(watermarkText, x, y, textPaint);

                // 4. Encode back to bytes
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80); // 80% Quality
                return data.ToArray();
            }
            catch
            {
                // If any error occurs (e.g. font missing, corrupt image), return original
                return imageBytes;
            }
        }

        [Obsolete]
        public async Task<byte[]> GeneratePdfAsync(ValuationDocument report)
        {
            // 1) Download all photos into memory AND apply Watermarks
            var photoStreams = new Dictionary<string, byte[]>();
            
            foreach (var kvp in report.PhotoUrls)
            {
                if (string.IsNullOrEmpty(kvp.Value))
                    continue;

                try 
                {
                    var response = await _httpClient.GetAsync(kvp.Value);
                    response.EnsureSuccessStatusCode();
                    var rawBytes = await response.Content.ReadAsByteArrayAsync();

                    // ✅ CHECK METADATA & APPLY WATERMARK
                    DateTime? docDate = null;
                    string docLoc = null;

                    // Check if metadata exists for this specific photo key (e.g., "FrontLeftSide")
                    if (report.PhotoMetadata != null && report.PhotoMetadata.ContainsKey(kvp.Key))
                    {
                        var meta = report.PhotoMetadata[kvp.Key];
                        docDate = meta.CapturedDate;
                        docLoc = meta.LocationText;
                    }

                    // Process the image (Overlay text on image)
                    var finalBytes = AddWatermark(rawBytes, docDate, docLoc);

                    // Store processed image
                    photoStreams[kvp.Key] = finalBytes;
                }
                catch
                {
                    // If download fails, skip image
                    continue;
                }
            }

            // 2) Build the PDF document instance
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(10);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial").FontColor(Colors.Black));

                    // ————————————————————————————————————————————————————————
                    // HEADER SECTION
                    // ————————————————————————————————————————————————————————
                    page.Header().Column(headerCol =>
                    {
                        // Logo + Title + Contact row
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeColumn(2)
                               .AlignCenter()
                               .Text("Vehicle Valuation Report - Retail")
                               .FontSize(24)
                               .Bold();

                            row.RelativeColumn(1)
                               .AlignRight()
                               .Text("www.prontomoto.in\n+91 9885755567")
                               .FontSize(10);
                        });

                        // Divider below header
                        headerCol.Item()
                            .PaddingVertical(5)
                            .LineHorizontal(2)
                            .LineColor(Colors.Black);
                    });

                    // ————————————————————————————————————————————————————————
                    // MAIN BODY
                    // ————————————————————————————————————————————————————————
                    page.Content().PaddingVertical(5).Column(col =>
                    {
                        // 2a) MAIN SUMMARY SECTION
                        col.Item().Row(row =>
                        {
                            // Left-aligned summary
                            row.RelativeColumn().Stack(stack =>
                            {
                                stack.Item().Text($"Vehicle No: {report.VehicleDetails?.RegistrationNumber ?? "-"}")
                                    .FontSize(18).Bold();
                                stack.Item().Text($"Valuation No: {report.CompositeKey}");
                                stack.Item().Text($"Date: {report.InspectionDetails?.DateOfInspection?.ToString("dd-MM-yyyy")}");
                                stack.Item().Text("Report Type: Retail");
                            });

                            // Right-aligned summary
                            row.RelativeColumn().Stack(stack =>
                            {
                                stack.Item().Text($"Stakeholder: {report.Stakeholder?.Name}");
                                stack.Item().Text($"Executive: {report.Stakeholder?.ExecutiveName}");
                                stack.Item().Text($"Inspector: {report.InspectionDetails?.VehicleInspectedBy}");
                            });
                        });

                        // Divider
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2b) PRIMARY VEHICLE DETAILS BLOCK
                        col.Item().PaddingTop(5).Text("Primary Vehicle Details").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(140);
                                cols.RelativeColumn();
                            });

                            void AddRow(string label, string value)
                            {
                                table.Cell().Text(label).Bold();
                                table.Cell().Text(value ?? "-");
                            }

                            var v = report.VehicleDetails;
                            AddRow("Make", v?.Make);
                            AddRow("Model", v?.Model);
                            AddRow("Year of Manufacture", v?.YearOfMfg?.ToString("MM-yyyy"));
                            AddRow("Body Type", v?.BodyType);
                            AddRow("Chassis Number", v?.ChassisNumber);
                            AddRow("Engine Number", v?.EngineNumber);
                            AddRow("Inspection Date", report.InspectionDetails?.DateOfInspection?.ToString("dd-MM-yyyy"));
                            AddRow("Location", report.InspectionDetails?.InspectionLocation);
                            AddRow("Colour", v?.Colour);
                            AddRow("Transmission", "Manual");
                            AddRow("Engine Condition", report.InspectionDetails?.EngineCondition);
                            AddRow("Fuel Type", v?.Fuel);
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2c) VALUATION & RATING SECTION
                        col.Item().PaddingTop(5).Text("Valuation & Rating").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(140);
                                cols.RelativeColumn();
                            });

                            table.Cell().Text("Valuation Amount").Bold();
                            table.Cell().Text($"₹ {report.QualityControl?.ValuationAmount:N0}");

                            table.Cell().Text("Overall Rating").Bold();
                            table.Cell().Text(report.QualityControl?.OverallRating ?? "-");
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2d) OWNER & HYPOTHECATION SECTION
                        col.Item().PaddingTop(5).Text("Owner & Hypothecation").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(140);
                                cols.RelativeColumn();
                            });

                            var vdet = report.VehicleDetails;
                            table.Cell().Text("Owner Name").Bold();
                            table.Cell().Text(vdet?.OwnerName ?? "-");
                            table.Cell().Text("Address").Bold();
                            table.Cell().Text(vdet?.PresentAddress ?? "-");
                            table.Cell().Text("Hypothecation").Bold();
                            table.Cell().Text(vdet?.Hypothecation.ToString() ?? "-");
                            table.Cell().Text("Insurer").Bold();
                            table.Cell().Text(vdet?.Insurer ?? "-");
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2e) ADDITIONAL VEHICLE DETAILS
                        col.Item().PaddingTop(5).Text("Additional Vehicle Details").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(140);
                                cols.RelativeColumn();
                            });

                            void AddRow2(string label, string value)
                            {
                                table.Cell().Text(label).Bold();
                                table.Cell().Text(value ?? "-");
                            }

                            AddRow2("Insurance Validity", report.VehicleDetails?.InsuranceValidUpTo?.ToString("dd-MM-yyyy"));
                            AddRow2("Registration Date", report.VehicleDetails?.DateOfRegistration?.ToString("dd-MM-yyyy"));
                            AddRow2("Fitness Validity", report.VehicleDetails?.FitnessValidTo?.ToString("dd-MM-yyyy"));
                            AddRow2("Vehicle Type", report.VehicleDetails?.ClassOfVehicle);
                            AddRow2("Engine CC", report.VehicleDetails?.EngineCC?.ToString());
                            AddRow2("Kilometers", report.VehicleDetails?.Odometer?.ToString());
                            AddRow2("Contact Number", report.Stakeholder?.ExecutiveContact);
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2f) REMARKS / NOTES SECTION
                        col.Item().PaddingTop(5).Text("Remarks / Notes").Bold();
                        col.Item().Text(report.QualityControl?.Remarks ?? "-");
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                        // 2g) PHOTOS & MEDIA SECTION
                        col.Item().PaddingTop(5).Text("Photos").Bold();
                        col.Item().Grid(grid =>
                        {
                            grid.Columns(3);
                            grid.Spacing(10);

                            foreach (var kvp in photoStreams)
                            {
                                var imageName = kvp.Key;
                                var imageData = kvp.Value;

                                grid.Item().Column(imgCol =>
                                {
                                    imgCol.Item()
                                          .Width(80)
                                          .Height(80)
                                          .Image(imageData)
                                          .FitArea();

                                    imgCol.Item()
                                          .Text(imageName)
                                          .FontSize(9)
                                          .Italic()
                                          .AlignCenter();
                                });
                            }
                        });
                    });

                    // ————————————————————————————————————————————————————————
                    // FOOTER SECTION
                    // ————————————————————————————————————————————————————————
                    page.Footer().PaddingTop(5).Column(footer =>
                    {
                        footer.Item().LineHorizontal(1).LineColor(Colors.Black);
                        footer.Item().Text($"Report Generated: {DateTime.Now:dd-MM-yyyy HH:mm}").FontSize(10);
                        footer.Item().Text("PRONTO MOTO SERVICES, F-1, 2-216/A, Vakalapudi, Kakinada, AP – 533005")
                              .FontSize(10);
                    });
                });
            });

            // 3) Show a live preview in Companion before generating the bytes
            document.ShowInCompanion();

            // 4) Generate PDF bytes
            byte[] pdfBytes = document.GeneratePdf();

            return pdfBytes;
        }
    }
}