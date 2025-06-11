namespace Valuation.Api.Models
{
    public class RoleModel
    {
        public string RoleId { get; set; }           // e.g. "CanViewTest"
        public string Name { get; set; }             // human name
        public string Description { get; set; }      // what it allows
    }
}
