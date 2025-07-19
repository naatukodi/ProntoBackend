namespace Valuation.Api.Models
{
    public class RoleModel
    {
        public string RoleId { get; set; }           // e.g. "CanViewTest"
        public string Name { get; set; }             // human name
        public string Description { get; set; }      // what it allows
        public List<string> Permissions { get; set; } // e.g. ["CanViewTest", "CanEditTest"]
        public List<string> Users { get; set; }      // user IDs assigned to this role
        public DateTime CreatedAt { get; set; }      // when it was created
        public DateTime? UpdatedAt { get; set; }     // when it was last updated
        
    }
}
