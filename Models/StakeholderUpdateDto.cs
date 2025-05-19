namespace Valuation.Api.Models;

public class StakeholderUpdateDto
{
    public string ValuationId { get; set; }  // cd8cb1dd-… GUID
    public string Name { get; set; }
    public string ExecutiveName { get; set; }
    public string ExecutiveContact { get; set; }
    public string ExecutiveWhatsapp { get; set; }
    public string ExecutiveEmail { get; set; }
    public string ApplicantName { get; set; }
    public string ApplicantContact { get; set; }
    public string VehicleNumber { get; set; }
    public string VehicleSegment { get; set; }
    public IFormFile? RcFile { get; set; }
    public IFormFile? InsuranceFile { get; set; }
    public IFormFileCollection? OtherFiles { get; set; }
}
