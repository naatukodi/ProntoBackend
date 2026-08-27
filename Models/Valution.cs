using System;
using System.Collections.Generic;

namespace Valuation.Api.Models
{
    public class ValuationDocument
    {
        public string id { get; set; } = string.Empty;

        // Which company the case belongs to: "vehga" or "pronto". Stamped once at
        // creation from the caller's brand and never changed afterwards — every case
        // list is scoped by it, so flipping it would move a case between companies.
        // Null on documents created before multi-brand shipped, i.e. Vehga.
        public string? Brand { get; set; }

        // Assigned by ProntoPDFGeneration on first PDF generation and persisted, so
        // the QR code and blob path stay stable for the life of the report.
        public string? ReferenceNumber { get; set; }

        public Stakeholder? Stakeholder { get; set; }
        public string? CompositeKey { get; set; }
        public string? VehicleNumber { get; set; }
        public string? ApplicantContact { get; set; }
        public string? VehicleSegment { get; set; }
        public List<Document>? Documents { get; set; }
        public VehicleDetailsDto? VehicleDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public InspectionDetails? InspectionDetails { get; set; }
        public QualityControl? QualityControl { get; set; }

        // What the photo reader found, kept so opening a case does not pay for the
        // same reading twice. Re-read only when the photo set itself changes.
        public QcAiAuditRecord? QcAiAudit { get; set; }
        public ValuationResponse? ValuationResponse { get; set; }
        
        public Dictionary<string, string> PhotoUrls { get; set; } = new();
        public Dictionary<string, string> VideoUrls { get; set; } = new();
        public Dictionary<string, PhotoMetadata> PhotoMetadata { get; set; } = new();
        
        // ✅ NEW: Array for dynamic custom images
        public List<SavedCustomPhoto> CustomPhotos { get; set; } = new();

        // Keys (from PhotoUrls) QC chose to include in the PDF gallery page.
        // Null/empty means "not set" — PDF generator falls back to including everything available.
        public List<string>? SelectedGalleryPhotos { get; set; }

        public List<WorkflowStep>? Workflow { get; set; }
        public string? Status { get; set; } = "Open";
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? AssignedToRole { get; set; }
        public string? RedFlag { get; set; } 
        public string? Remarks { get; set; }
        public string? PaymentStatus { get; set; } 
        public string? PaymentReference { get; set; } 
        public DateTime? PaymentDate { get; set; } 
        public string? PaymentMethod { get; set; } 
        public string? PaymentAmount { get; set; } 
        public string? CompletedByPhoneNumber { get; set; }
        public string? CompletedByEmail { get; set; }
        public string? CompletedByWhatsapp { get; set; }
        public decimal? FinalValuationAmount { get; set; }
    }
}