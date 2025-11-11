using Newtonsoft.Json;
namespace Valuation.Api.Models
{
    public class CommonNote
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CompositeKey { get; set; } // ✅ ADD THIS
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Note { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public string CreatedBy { get; set; }
        public string ModifiedBy { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
