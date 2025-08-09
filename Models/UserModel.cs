// Models/UserModel.cs
namespace Valuation.Api.Models
{
    public class UserModel
    {
        public string UserId { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string RoleId { get; set; } = default!;
        public string Whatsapp { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;

        // New branch/post‑office fields
        public string? Description { get; set; }
        public string BranchType { get; set; } = default!;
        public string ServiceStatus { get; set; } = default!;
        public string Circle { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Division { get; set; } = default!;
        public string Region { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;
        public string Pincode { get; set; } = default!;

        // Additional fields for user management
        public string? ProfilePictureUrl { get; set; }
        public string? Address { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Roles { get; set; } = default!;
        public string? AssignedDistricts { get; set; } = default!;
        public string? AssignedStates { get; set; } = default!;
    }
}
