// Services/IPincodeTableService.cs
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IPincodeTableService
    {
        Task<IReadOnlyList<PincodeModel>> GetByPincodeAsync(string pincode);
    }
}
