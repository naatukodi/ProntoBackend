// Define ValuationDocument if it does not exist elsewhere

namespace Valuation.Api.Models;

public class ValuationDocument
{
    public string id { get; set; }
    public Stakeholder Stakeholder { get; set; }
    // Add other properties as needed
    public string CompositeKey { get; set; }
    public string VehicleNumber { get; set; }
    public string ApplicantContact { get; set; }
    public string VehicleSegment { get; set; }
    public List<Document> Documents { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Stakeholder
{
    public string Name { get; set; }
    public string ExecutiveName { get; set; }
    public string ExecutiveContact { get; set; }
    public string ExecutiveWhatsapp { get; set; }
    public string ExecutiveEmail { get; set; }
    public Applicant Applicant { get; set; }
    public List<Document> Documents { get; set; }
}

public class Applicant
{
    public string Name { get; set; }
    public string Contact { get; set; }
}

public class Document
{
    public string Type { get; set; }
    public string FilePath { get; set; }
    public DateTime UploadedAt { get; set; }

}
