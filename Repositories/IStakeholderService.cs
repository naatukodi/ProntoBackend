// Services/IStakeholderService.cs
namespace Valuation.Api.Services
{
    public interface IStakeholderService
    {
        Task UpdateAsync(StakeholderUpdateDto dto);
    }
}
