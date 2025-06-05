using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Valuation.Api.Models;  // ← adjust to your actual models namespace

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

            // 2) Build the PDF document
            byte[] pdfBytes = QuestPDF.Fluent.Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    // ————————————————————————————————————————————————————————
                    // HEADER BAR (Thin Dark Gray strip with company name + contact)
                    // ————————————————————————————————————————————————————————
                    page.Header()
                        .Background(Colors.LightBlue.Darken1)
                        .Height(30)
                        .Row(r =>
                        {
                            r.RelativeColumn().Stack(stack =>
                            {
                                stack.Item()
                                     .AlignLeft()
                                     .Text("PRONTO MOTO SERVICES")
                                     .FontColor(Colors.White)
                                     .FontSize(12)
                                     .SemiBold();
                            });
                            r.RelativeColumn().Stack(stack =>
                            {
                                stack.Item()
                                     .AlignRight()
                                     .Text("www.prontomoto.in | connect@prontomoto.in | +91 9885755567")
                                     .FontColor(Colors.White)
                                     .FontSize(10);
                            });
                        });

                    // ————————————————————————————————————————————————————————
                    // MAIN BODY: Title + All Sections
                    // ————————————————————————————————————————————————————————
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // 2a) Title centered
                        col.Item().AlignCenter()
                            .Text("PRONTO MOTO VALUATION REPORT")
                            .FontSize(18)
                            .SemiBold();

                        col.Item().PaddingBottom(10);

                        // 2b) “Basic Metadata” Section (two columns per row)
                        col.Item().Table(table =>
                        {
                            // Two columns: label (constant) + value (relative)
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                            });

                            void addCell(string text, bool isLabel = false)
                            {
                                if (isLabel)
                                    table.Cell().Text(text).SemiBold().FontSize(11);
                                else
                                    table.Cell().Text(text ?? "-").FontSize(11).WrapAnywhere();
                            }

                            // Row 1: TYPE OF VAL, DATE | REPORT REQUESTED BY BRANCH
                            addCell("TYPE OF VAL");
                            addCell(report.QualityControl?.OverallRating ?? "-"); // or appropriate field
                            addCell("REPORT REQUESTED BY BRANCH");
                            addCell(report.Stakeholder?.Name ?? "-");

                            // Row 2: INSPECTION DATE 📆 | REF NO
                            addCell("INSPECTION DATE");
                            addCell(report.InspectionDetails?.DateOfInspection?.ToString("dd-MM-yyyy") ?? "-");
                            addCell("REF NO");
                            addCell(report.CompositeKey);

                            // Row 3: INSPECTION LOCATION | REGN NO
                            addCell("INSPECTION LOCATION");
                            addCell(report.InspectionDetails?.InspectionLocation ?? "-");
                            addCell("REGN NO");
                            addCell(report.VehicleDetails?.RegistrationNumber ?? "-");

                            // Row 4: REGISTERED OWNER | APPLICANT NAME
                            addCell("REGISTERED OWNER");
                            addCell(report.VehicleDetails?.OwnerName ?? "-");
                            addCell("APPLICANT NAME");
                            addCell(report.Stakeholder?.Applicant?.Name ?? "-");

                            // Row 5: VEHICLE CATEGORY (SEGMENT) | MAKE
                            addCell("VEHICLE CATEGORY");
                            addCell(report.VehicleDetails?.CategoryCode ?? "-");
                            addCell("MAKE");
                            addCell(report.VehicleDetails?.Make ?? "-");

                            // Row 6: MODEL | CHASSIS NO
                            addCell("MODEL");
                            addCell(report.VehicleDetails?.Model ?? "-");
                            addCell("CHASSIS NO");
                            addCell(report.VehicleDetails?.ChassisNumber ?? "-");

                            // Row 7: ENGINE NO | YEAR OF MFG
                            addCell("ENGINE NO");
                            addCell(report.VehicleDetails?.EngineNumber ?? "-");
                            addCell("YEAR OF MFG");
                            addCell(report.VehicleDetails?.YearOfMfg.ToString() ?? "-");

                            // Row 8: REGISTRATION DATE | CLASS OF VEHICLE
                            var regDate = report.VehicleDetails?.DateOfRegistration?.ToString("dd-MM-yyyy");
                            addCell("REGISTRATION DATE");
                            addCell(regDate);
                            addCell("CLASS OF VEHICLE");
                            addCell(report.VehicleDetails?.ClassOfVehicle ?? "-");

                            // Row 9: BODY TYPE | OWNER SR NO
                            addCell("BODY TYPE");
                            addCell(report.VehicleDetails?.BodyType ?? "-");
                            addCell("OWNER SR NO");
                            addCell(report.VehicleDetails?.OwnerSerialNo ?? "-");
                        });

                        col.Item().PaddingVertical(10);

                        // 2c) “Document Details” Section Header (light gray background)
                        col.Item().Background(Colors.LightBlue.Lighten3).Padding(5)
                            .Text("DOCUMENT DETAILS").SemiBold().FontSize(12);

                        col.Item().PaddingBottom(5);

                        // Document Details Table (two columns per row)
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                            });

                            void addDocRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold().FontSize(11);
                                table.Cell().Text(value ?? "-").FontSize(11).WrapAnywhere();
                            }

                            // Example rows—adjust to your JSON structure:
                            addDocRow("DOCUMENT REQUESTED BY", report.Stakeholder?.ExecutiveName ?? "-");
                            addDocRow("CHASSIS PUNCH", report.QualityControl?.ChassisPunch ?? "-");
                            addDocRow("ORIGINAL", report.QualityControl?.Remarks ?? "N/A");
                            addDocRow("RC VALIDITY", report.VehicleDetails.InsuranceValidUpTo?.ToString("dd-MM-yyyy") ?? "-");
                            addDocRow("HYPOTHECATION", (bool)report.VehicleDetails.Hypothecation ? "Yes" : "No");
                            addDocRow("FUEL", report.VehicleDetails.Fuel ?? "-");
                            addDocRow("ODO METER", report.VehicleDetails.Odometer?.ToString() ?? "-");
                            addDocRow("COLOUR", report.VehicleDetails.Colour ?? "-");
                            addDocRow("PERMIT NO", report.VehicleDetails.PermitNo ?? "-");
                            addDocRow("POLICY NO", report.VehicleDetails.InsurancePolicyNo ?? "-");
                            addDocRow("PERMIT VALID UP TO", report.VehicleDetails.PermitValidUpTo?.ToString("dd-MM-yyyy") ?? "-");
                            addDocRow("INSURANCE VALID UP TO", report.VehicleDetails.InsuranceValidUpTo?.ToString("dd-MM-yyyy") ?? "-");
                            addDocRow("IDV", report.VehicleDetails?.IDV?.ToString() ?? "-");
                            addDocRow("FITNESS VALID UP TO", report.VehicleDetails?.FitnessValidTo?.ToString("dd-MM-yyyy") ?? "-");
                        });

                        col.Item().PaddingVertical(10);

                        // 2d) “Inspection Details & Quality Control” Combined Section
                        col.Item().Background(Colors.LightBlue.Lighten3).Padding(5)
                            .Text("INSPECTION DETAILS & QUALITY CONTROL").SemiBold().FontSize(12);

                        col.Item().PaddingBottom(5);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                            });

                            // Inspection Detail rows
                            var ins = report.InspectionDetails;
                            void addIRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold().FontSize(11);
                                table.Cell().Text(value ?? "-").FontSize(11).WrapAnywhere();
                            }

                            addIRow("VEHICLE INSPECTED BY", ins.VehicleInspectedBy);
                            addIRow("DATE OF INSPECTION", ins.DateOfInspection?.ToString("dd-MM-yyyy"));
                            addIRow("LOCATION", ins.InspectionLocation);
                            addIRow("VEHICLE MOVED", ins.VehicleMoved == true ? "Yes" : "No");
                            addIRow("ENGINE STARTED", ins.EngineStarted == true ? "Yes" : "No");
                            addIRow("ODOMETER", ins.Odometer?.ToString());
                            addIRow("VIN PLATE", ins.VinPlate == true ? "Yes" : "No");
                            addIRow("BODY TYPE", ins.BodyType);
                            addIRow("TYRE CONDITION", ins.OverallTyreCondition);
                            addIRow("ENGINE CONDITION", ins.EngineCondition);
                            addIRow("BRAKE SYSTEM", ins.BrakeSystem);
                            // …other inspection rows…

                            // Quality Control rows
                            var qc = report.QualityControl;
                            void addQRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold().FontSize(11);
                                table.Cell().Text(value ?? "-").FontSize(11).WrapAnywhere();
                            }

                            addQRow("OVERALL RATING", qc.OverallRating);
                            addQRow("VALUATION AMOUNT", $"₹{qc.ValuationAmount}");
                            addQRow("CHASSIS PUNCH", qc.ChassisPunch);
                            addQRow("REMARKS", qc.Remarks);
                        });

                        col.Item().PaddingVertical(10);

                        // 2e) “AI Analysis” Section
                        col.Item().Background(Colors.LightBlue.Lighten3).Padding(5)
                            .Text("AI ANALYSIS").SemiBold().FontSize(12);

                        col.Item().PaddingBottom(5);

                        col.Item().Column(c =>
                        {
                            // Full‐width raw AI output
                            c.Item().Text(report.ValuationResponse.RawResponse ?? "-")
                                .FontSize(11)
                                .WrapAnywhere();

                            c.Item().PaddingVertical(10);

                            // Summary table for numeric ranges
                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(140);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(140);
                                    columns.RelativeColumn();
                                });

                                void addVRes(string label, object value)
                                {
                                    table.Cell().Text(label).SemiBold().FontSize(11);
                                    table.Cell().Text(value?.ToString() ?? "-").FontSize(11).WrapAnywhere();
                                }

                                addVRes("LOW RANGE (Lacs)", report.ValuationResponse.LowRange);
                                addVRes("MID RANGE (Lacs)", report.ValuationResponse.MidRange);
                                addVRes("HIGH RANGE (Lacs)", report.ValuationResponse.HighRange);
                            });
                        });

                        col.Item().PaddingVertical(10);

                        // 2f) “Photos” Section (Thumbnails, three per row)
                        col.Item().Background(Colors.LightBlue.Lighten3).Padding(5)
                            .Text("PHOTOS").SemiBold().FontSize(12);

                        col.Item().PaddingBottom(5);

                        col.Item().Grid(grid =>
                        {
                            grid.Columns(3); // three columns per row
                            grid.Spacing(10);

                            foreach (var kvp in photoStreams)
                            {
                                string imageName = kvp.Key;
                                byte[] imageData = kvp.Value;
                                string originalUrl = report.PhotoUrls[imageName]!;

                                grid.Item().Column(c =>
                                {
                                    // 80×80 thumbnail container
                                    c.Item()
                                     .Width(80)
                                     .Height(80)
                                     .Hyperlink(originalUrl)
                                     .Image(imageData)
                                     .FitArea();

                                    // Caption below thumbnail
                                    c.Item()
                                     .Hyperlink(originalUrl)
                                     .Text(imageName)
                                     .FontSize(9)
                                     .Italic()
                                     .AlignCenter();
                                });
                            }
                        });

                        col.Item().PaddingVertical(10);

                        // 2g) Disclaimer Section (small italic text in light gray box)
                        col.Item().Background(Colors.LightBlue.Darken3).Padding(8)
                            .Text(@"
                        DISCLAIMER:
                        We are not responsible for verifying the authenticity of associated documents. Vehicle odometer readings are not verified for accuracy. Valuation amounts are professional opinions based on market methodology and are issued without prejudice.                
                        ")
                            .FontSize(8)
                            .FontColor(Colors.White)  // ← use FontColor instead of Color()
                            .Italic();
                    });

                    // ————————————————————————————————————————————————————————
                    // FOOTER BAR (Thin Dark Gray strip with address + contact)
                    // ————————————————————————————————————————————————————————
                    page.Footer()
                        .Background(Colors.LightBlue.Darken1)
                        .Height(40)
                        .Row(r =>
                        {
                            r.RelativeColumn().AlignLeft().Text("Registered Address: F-1, 2-216/A, Vakalapudi, Kakinada, East Godavari Dist, Andhra Pradesh – 533005")
                                .FontColor(Colors.White)
                                .FontSize(9);
                            r.RelativeColumn().AlignRight().Text("www.prontomoto.in|+91 9885755567")
                                .FontColor(Colors.White)
                                .FontSize(9);
                        });
                });
            })
            .GeneratePdf();

            string fileName = $"{report.VehicleDetails?.RegistrationNumber}-VR.pdf";

            return pdfBytes;
        }
    }
}
