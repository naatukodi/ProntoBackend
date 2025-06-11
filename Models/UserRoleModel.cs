namespace Valuation.Api.Models
{
    public class UserRoleModel
    {
        public string UserId { get; set; }           // Firebase UID or your own identifier
        public string RoleId { get; set; }           // matches RoleModel.RoleId
    }
}
