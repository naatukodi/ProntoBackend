namespace Valuation.Api.Models
{
    public class CommonNoteDto
    {
        public string Id { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Note { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string CreatedBy { get; set; }
        public string ModifiedBy { get; set; }
    }

    public class CreateCommonNoteDto
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Note { get; set; }
        public string CreatedBy { get; set; }
    }

    public class UpdateCommonNoteDto
    {
        public string Note { get; set; }
        public string ModifiedBy { get; set; }
    }
}
