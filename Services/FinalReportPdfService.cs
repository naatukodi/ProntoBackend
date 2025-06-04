using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Valuation.Api.Models;  // <- adjust to match your actual models namespace

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
            // 1) Download all photos into memory
            // var photoStreams = new Dictionary<string, byte[]>();
            // foreach (var kvp in report.PhotoUrls)
            // {
            //     if (string.IsNullOrEmpty(kvp.Value))
            //         continue;

            //     var response = await _httpClient.GetAsync(kvp.Value);
            //     response.EnsureSuccessStatusCode();
            //     photoStreams[kvp.Key] = await response.Content.ReadAsByteArrayAsync();
            // }

            // 2) Build a document with one Page(...) per section
            byte[] pdfBytes = QuestPDF.Fluent.Document.Create(doc =>
            {
                // --- Page 1: Stakeholder & Vehicle Details ---
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    // Header
                    page.Header()
                        .Text($"Final Report: {report.id}")
                        .SemiBold()
                        .FontSize(18)
                        .FontColor(Colors.Black);

                    // Content: Stakeholder + Vehicle Details in a vertical stack
                    page.Content().Column(col =>
                    {
                        // Stakeholder Section
                        col.Item().Text("Stakeholder").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(120);
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
                            addRow("Name", st.Name);
                            addRow("Executive", st.ExecutiveName);
                            addRow("Contact", st.ExecutiveContact);
                            addRow("WhatsApp", st.ExecutiveWhatsapp);
                            addRow("Email", st.ExecutiveEmail);
                            addRow("Applicant", st.Applicant.Name);
                            addRow("Applicant Contact", st.Applicant.Contact);
                        });

                        col.Item().PaddingRight(10); // small gap

                        // Vehicle Details Section
                        col.Item().Text("Vehicle Details").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(120);
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
                            addVRow("Registration No.", vd.RegistrationNumber?? "-");
                            addVRow("Make", vd.Make?? "-");
                            addVRow("Model", vd.Model?? "-");
                            addVRow("Year of Mfg", vd.YearOfMfg.ToString()?? "-");
                            addVRow("Body Type", vd.BodyType?? "-");
                            addVRow("Chassis No.", vd.ChassisNumber?? "-");
                            addVRow("Engine No.", vd.EngineNumber?? "-");
                            addVRow("Color", vd.Colour?? "-");
                            addVRow("Fuel", vd.Fuel?? "-");
                            addVRow("Owner", vd.OwnerName?? "-");
                            addVRow("RTO", vd.Rto?? "-");
                            // …add more fields as needed…
                        });
                    });
                });

                // --- Page 2: Inspection Details ---
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Inspection Details")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
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

                        addIRow("Inspected By", ins.VehicleInspectedBy);
                        addIRow("Date", ins.DateOfInspection?.ToString("dd MMM yyyy") ?? "-");
                        addIRow("Location", ins.InspectionLocation?? "-");
                        addIRow("Odometer", ins.Odometer?.ToString() ?? "-");
                        addIRow("Engine Started", ins.EngineStarted == true ? "Yes" : "No");
                        addIRow("Road Worthy", ins.RoadWorthyCondition == true ? "Yes" : "No");
                        addIRow("Tyre Condition", ins.OverallTyreCondition?? "-");
                        addIRow("Engine Condition", ins.EngineCondition?? "-");
                        addIRow("Brake Condition", ins.BrakeSystem?? "-");
                        // …add more inspection fields as needed…
                    });
                });

                // --- Page 3: Quality Control ---
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Quality Control")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
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

                        addQRow("Overall Rating", qc.OverallRating);
                        addQRow("Valuation Amount", $"₹{qc.ValuationAmount}");
                        addQRow("Chassis Punch", qc.ChassisPunch);
                        addQRow("Remarks", qc.Remarks);
                    });
                });

                // --- Page 4: Valuation Response ---
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Valuation Response")
                        .SemiBold()
                        .FontSize(16);

                    page.Content().Column(col =>
                    {
                        // Raw Response in a two-column table so it wraps
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(120);
                                columns.RelativeColumn();
                            });

                            table.Cell().Text("Raw Response").SemiBold();
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

                            addVRes("Low Range (Lacs)", report.ValuationResponse.LowRange);
                            addVRes("Mid Range (Lacs)", report.ValuationResponse.MidRange);
                            addVRes("High Range (Lacs)", report.ValuationResponse.HighRange);
                        });
                    });
                });

                // --- Page 5+: Photos (one or more pages) ---
                // If there are many photos, we can group them two per row or one per row.
                // Below, we simply list them one after another. QuestPDF will push to subsequent pages if needed.
            //     doc.Page(page =>
            //     {
            //         page.Size(PageSizes.A4);
            //         page.Margin(30);
            //         page.DefaultTextStyle(x => x.FontSize(12));

            //         page.Header()
            //             .Text("Photos")
            //             .SemiBold()
            //             .FontSize(16);

            //         page.Content().Column(col =>
            //         {
            //             foreach (var kvp in photoStreams)
            //             {
            //                 // Show each image scaled to fit page width
            //                 col.Item()
            //                    .Image(kvp.Value)
            //                    .FitWidth();

            //                 col.Item()
            //                    .Text(kvp.Key)
            //                    .FontSize(10)
            //                    .Italic();
            //             }
            //         });
            //     });
             })
            .GeneratePdf();

            return pdfBytes;
        }
    }
}
