using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Valuation.Api.Models;  // ← adjust to your actual namespace

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
                // ─── Page 1: Pronto Moto Valuation Report ───
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    // Main Heading, centered
                    page.Header()
                        .AlignCenter()
                        .Text("Pronto Moto Valuation Report")
                        .SemiBold()
                        .FontSize(20)
                        .FontColor(Colors.Black);

                    // Stakeholder & Vehicle Details
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(10)
                            .Text("Stakeholder").Bold().FontSize(14);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(130);
                                columns.RelativeColumn();
                            });

                            void addRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold();
                                table.Cell()
                                      .Text(value ?? "-")
                                      .WrapAnywhere();
                            }

                            var st = report.Stakeholder;
                            addRow("Name",               st.Name);
                            addRow("Executive Name",     st.ExecutiveName);
                            addRow("Contact Number",     st.ExecutiveContact);
                            addRow("WhatsApp Number",    st.ExecutiveWhatsapp);
                            addRow("Email",              st.ExecutiveEmail);
                            addRow("Applicant Name",     st.Applicant.Name);
                            addRow("Applicant Contact",  st.Applicant.Contact);
                        });

                        col.Item().PaddingTop(20).PaddingBottom(10)
                            .Text("Vehicle Details").Bold().FontSize(14);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(130);
                                columns.RelativeColumn();
                            });

                            void addVRow(string label, object value)
                            {
                                table.Cell().Text(label).SemiBold();
                                table.Cell()
                                      .Text(value?.ToString() ?? "-")
                                      .WrapAnywhere();
                            }

                            var vd = report.VehicleDetails;
                            addVRow("Registration No.",      vd.RegistrationNumber ?? "-");
                            addVRow("Make",                 vd.Make ?? "-");
                            addVRow("Model",                vd.Model ?? "-");
                            addVRow("Year of Mfg",          vd.YearOfMfg.ToString() ?? "-");
                            addVRow("Body Type",            vd.BodyType ?? "-");
                            addVRow("Chassis No.",          vd.ChassisNumber ?? "-");
                            addVRow("Engine No.",           vd.EngineNumber ?? "-");
                            addVRow("Color",                vd.Colour ?? "-");
                            addVRow("Fuel",                 vd.Fuel ?? "-");
                            addVRow("Owner Name",           vd.OwnerName ?? "-");
                            addVRow("RTO",                  vd.Rto ?? "-");
                            // …add any additional fields you need…
                        });
                    });
                });

                // ─── Page 2: Inspection Details & Quality Control ───
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Inspection Details & Quality Control")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Column(col =>
                    {
                        // Inspection Details
                        col.Item().PaddingBottom(10)
                            .Text("Inspection Details").Bold().FontSize(14);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(130);
                                columns.RelativeColumn();
                            });

                            var ins = report.InspectionDetails;
                            void addIRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold();
                                table.Cell()
                                      .Text(value ?? "-")
                                      .WrapAnywhere();
                            }

                            addIRow("Inspected By",        ins.VehicleInspectedBy);
                            addIRow("Date of Inspection",  ins.DateOfInspection?.ToString("dd MMM yyyy") ?? "-");
                            addIRow("Vehicle Moved",       ins.VehicleMoved == true ? "Yes" : "No");
                            addIRow("VIN Plate",           ins.VinPlate == true ? "Yes" : "No");
                            addIRow("Location",            ins.InspectionLocation ?? "-");
                            addIRow("Odometer",            ins.Odometer?.ToString() ?? "-");
                            addIRow("Engine Started",      ins.EngineStarted == true ? "Yes" : "No");
                            addIRow("Road Worthy",         ins.RoadWorthyCondition == true ? "Yes" : "No");
                            addIRow("Tyre Condition",      ins.OverallTyreCondition ?? "-");
                            addIRow("Engine Condition",    ins.EngineCondition ?? "-");
                            addIRow("Brake Condition",     ins.BrakeSystem ?? "-");
                            // …add more inspection fields if needed…
                        });

                        // spacing between tables
                        col.Item().PaddingTop(20).PaddingBottom(10)
                            .Text("Quality Control").Bold().FontSize(14);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(130);
                                columns.RelativeColumn();
                            });

                            var qc = report.QualityControl;
                            void addQRow(string label, string value)
                            {
                                table.Cell().Text(label).SemiBold();
                                table.Cell()
                                      .Text(value ?? "-")
                                      .WrapAnywhere();
                            }

                            addQRow("Overall Rating",   qc.OverallRating);
                            addQRow("Valuation Amount", $"₹{qc.ValuationAmount}");
                            addQRow("Chassis Punch",    qc.ChassisPunch);
                            addQRow("Remarks",          qc.Remarks ?? "-");
                        });
                    });
                });

                // ─── Page 3: AI Analysis ───
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("AI Analysis")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(10)
                            .Text("Raw AI Output").Bold().FontSize(14);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(130);
                                columns.RelativeColumn();
                            });

                            table.Cell().Text("Content").SemiBold();
                            table.Cell()
                                  .Text(report.ValuationResponse.RawResponse ?? "-")
                                  .FontSize(11)
                                  .WrapAnywhere();

                            void addVRes(string label, object value)
                            {
                                table.Cell().Text(label).SemiBold();
                                table.Cell()
                                      .Text(value?.ToString() ?? "-")
                                      .WrapAnywhere();
                            }

                            addVRes("Low Range (Lacs)",  report.ValuationResponse.LowRange);
                            addVRes("Mid Range (Lacs)",  report.ValuationResponse.MidRange);
                            addVRes("High Range (Lacs)", report.ValuationResponse.HighRange);
                        });
                    });
                });

                // ─── Page 4+: Photos as Thumbnails with Hyperlinks ───
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Photos")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Grid(grid =>
                    {
                        // Two columns of thumbnails
                        grid.Columns(2);

                        foreach (var kvp in photoStreams)
                        {
                            string imageName = kvp.Key;
                            byte[] imageData = kvp.Value;
                            string originalUrl = report.PhotoUrls[imageName]!;

                            // Each cell is a Column containing a linked thumbnail + caption
                            grid.Item().Column(col =>
                            {
                                // 100×100 container, then FitArea for the image
                                col.Item()
                                   .Width(100)
                                   .Height(100)
                                   .Hyperlink(originalUrl)
                                   .Image(imageData)
                                   .FitArea();  // now fits inside 100×100 without conflict

                                // Caption under thumbnail, also a hyperlink
                                col.Item()
                                   .Hyperlink(originalUrl)
                                   .Text(imageName)
                                   .FontSize(10)
                                   .Italic()
                                   .AlignCenter();
                            });
                        }
                    });
                });
            })
            .GeneratePdf();

            return pdfBytes;
        }
    }
}
