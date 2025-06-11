using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Valuation.Api.Models;  // ← adjust to your actual models namespace
using QuestPDF.Companion;

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

        [Obsolete]
        public async Task<byte[]> GeneratePdfAsync(ValuationDocument report)
        {
            // 1) Download all photos into memory so we can embed thumbnails
            var photoStreams = new Dictionary<string, byte[]>();
            foreach (var kvp in report.PhotoUrls)
            {
                if (string.IsNullOrEmpty(kvp.Value))
                    continue;

                var response = await _httpClient.GetAsync(kvp.Value);
                response.EnsureSuccessStatusCode();
                photoStreams[kvp.Key] = await response.Content.ReadAsByteArrayAsync();
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
