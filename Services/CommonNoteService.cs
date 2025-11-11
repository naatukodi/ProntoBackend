using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class CommonNoteService : ICommonNoteService
    {
        private readonly Container _container;
        private readonly ILogger<CommonNoteService> _logger;

        public CommonNoteService(CosmosClient cosmosClient, ILogger<CommonNoteService> logger)
        {
            _logger = logger;

            var database = cosmosClient.GetDatabase("ValuationsDb");
            _container = database.GetContainer("Valuations");
        }

        // ✅ Get all notes
        public async Task<IEnumerable<CommonNoteDto>> GetAllNotesAsync()
        {
            try
            {
                var query = _container.GetItemQueryIterator<CommonNote>(
                    new QueryDefinition(
                        "SELECT * FROM c WHERE c.CompositeKey = @partitionKey AND c.IsActive = true"
                    ).WithParameter("@partitionKey", "CommonNote")
                );

                var notes = new List<CommonNote>();
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    notes.AddRange(response);
                }

                return notes.OrderByDescending(n => n.CreatedDate).Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving all notes");
                throw;
            }
        }

        // ✅ Get notes for a specific entity
        public async Task<IEnumerable<CommonNoteDto>> GetNotesByEntityAsync(string entityType, string entityId)
        {
            try
            {
                _logger.LogInformation("📘 Fetching notes for EntityType={EntityType}, EntityId={EntityId}", entityType, entityId);

                var query = _container.GetItemQueryIterator<CommonNote>(
                    new QueryDefinition(
                        "SELECT * FROM c WHERE c.CompositeKey = @partitionKey AND c.EntityType = @entityType AND c.EntityId = @entityId AND c.IsActive = true"
                    )
                    .WithParameter("@partitionKey", "CommonNote")
                    .WithParameter("@entityType", entityType)
                    .WithParameter("@entityId", entityId)
                );

                var notes = new List<CommonNote>();
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    notes.AddRange(response);
                }

                _logger.LogInformation("✅ Found {Count} notes for EntityType={EntityType}, EntityId={EntityId}", notes.Count, entityType, entityId);

                return notes.OrderByDescending(n => n.CreatedDate).Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving notes for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        // ✅ Get note by ID
        public async Task<CommonNoteDto?> GetNoteByIdAsync(string id)
        {
            try
            {
                // First find the note to get its partition key
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id);

                var iterator = _container.GetItemQueryIterator<CommonNote>(query);
                var response = await iterator.ReadNextAsync();
                var note = response.FirstOrDefault();

                if (note == null)
                    return null;

                var readResponse = await _container.ReadItemAsync<CommonNote>(
                    id, new PartitionKey(note.CompositeKey));

                return MapToDto(readResponse.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Note {Id} not found", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving note {Id}", id);
                throw;
            }
        }

        // ✅ Create a new note
        public async Task<CommonNoteDto> CreateNoteAsync(CreateCommonNoteDto createDto)
        {
            try
            {
                var note = new CommonNote
                {
                    Id = Guid.NewGuid().ToString(),
                    EntityType = string.IsNullOrWhiteSpace(createDto.EntityType)
                        ? "Stakeholder"
                        : createDto.EntityType,
                    EntityId = string.IsNullOrWhiteSpace(createDto.EntityId)
                        ? "UnknownEntity"
                        : createDto.EntityId,
                    Note = createDto.Note,
                    CreatedBy = createDto.CreatedBy,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true,
                    CompositeKey = "CommonNote"
                };

                var response = await _container.CreateItemAsync(note, new PartitionKey(note.CompositeKey));

                _logger.LogInformation("🟢 Note {Id} created successfully for {EntityType}/{EntityId}", note.Id, note.EntityType, note.EntityId);
                return MapToDto(response.Resource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating note");
                throw;
            }
        }

        // ✅ Update a note
        public async Task<CommonNoteDto?> UpdateNoteAsync(string id, UpdateCommonNoteDto updateDto)
        {
            try
            {
                // Find note to get partition key
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id);

                var iterator = _container.GetItemQueryIterator<CommonNote>(query);
                var response = await iterator.ReadNextAsync();
                var note = response.FirstOrDefault();

                if (note == null || !note.IsActive)
                    return null;

                note.Note = updateDto.Note;
                note.ModifiedBy = updateDto.ModifiedBy;
                note.ModifiedDate = DateTime.UtcNow;

                var updated = await _container.ReplaceItemAsync(note, id, new PartitionKey(note.CompositeKey));

                _logger.LogInformation("🟡 Note {Id} updated successfully", id);
                return MapToDto(updated.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Note {Id} not found for update", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating note {Id}", id);
                throw;
            }
        }

        // ✅ Soft-delete a note
        public async Task<bool> DeleteNoteAsync(string id)
        {
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id);

                var iterator = _container.GetItemQueryIterator<CommonNote>(query);
                var response = await iterator.ReadNextAsync();
                var note = response.FirstOrDefault();

                if (note == null || !note.IsActive)
                    return false;

                note.IsActive = false;
                note.ModifiedDate = DateTime.UtcNow;

                await _container.ReplaceItemAsync(note, id, new PartitionKey(note.CompositeKey));

                _logger.LogInformation("🔴 Note {Id} deleted successfully", id);
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Note {Id} not found for deletion", id);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting note {Id}", id);
                throw;
            }
        }

        // ✅ Mapper
        private static CommonNoteDto MapToDto(CommonNote note)
        {
            return new CommonNoteDto
            {
                Id = note.Id,
                EntityType = note.EntityType,
                EntityId = note.EntityId,
                Note = note.Note,
                CreatedDate = note.CreatedDate,
                ModifiedDate = note.ModifiedDate,
                CreatedBy = note.CreatedBy,
                ModifiedBy = note.ModifiedBy
            };
        }
    }
}
