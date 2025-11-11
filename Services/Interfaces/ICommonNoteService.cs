using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface ICommonNoteService
    {
        Task<IEnumerable<CommonNoteDto>> GetAllNotesAsync();
        Task<IEnumerable<CommonNoteDto>> GetNotesByEntityAsync(string entityType, string entityId);
        Task<CommonNoteDto?> GetNoteByIdAsync(string id);
        Task<CommonNoteDto> CreateNoteAsync(CreateCommonNoteDto createDto);
        Task<CommonNoteDto?> UpdateNoteAsync(string id, UpdateCommonNoteDto updateDto);
        Task<bool> DeleteNoteAsync(string id);
    }
}
