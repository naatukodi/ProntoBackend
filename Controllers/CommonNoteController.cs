using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommonNoteController : ControllerBase
    {
        private readonly ICommonNoteService _commonNoteService;
        private readonly ILogger<CommonNoteController> _logger;

        public CommonNoteController(ICommonNoteService commonNoteService, ILogger<CommonNoteController> logger)
        {
            _commonNoteService = commonNoteService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommonNoteDto>>> GetAllNotes()
        {
            try
            {
                var notes = await _commonNoteService.GetAllNotesAsync();
                return Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all notes");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("entity/{entityType}/{entityId}")]
        public async Task<ActionResult<IEnumerable<CommonNoteDto>>> GetNotesByEntity(string entityType, string entityId)
        {
            try
            {
                var notes = await _commonNoteService.GetNotesByEntityAsync(entityType, entityId);
                return Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notes for {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommonNoteDto>> GetNoteById(string id)
        {
            try
            {
                var note = await _commonNoteService.GetNoteByIdAsync(id);
                
                if (note == null)
                    return NotFound($"Note with ID {id} not found");

                return Ok(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving note {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<CommonNoteDto>> CreateNote([FromBody] CreateCommonNoteDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var note = await _commonNoteService.CreateNoteAsync(createDto);
                return CreatedAtAction(nameof(GetNoteById), new { id = note.Id }, note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating note");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommonNoteDto>> UpdateNote(string id, [FromBody] UpdateCommonNoteDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var note = await _commonNoteService.UpdateNoteAsync(id, updateDto);
                
                if (note == null)
                    return NotFound($"Note with ID {id} not found");

                return Ok(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating note {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNote(string id)
        {
            try
            {
                var result = await _commonNoteService.DeleteNoteAsync(id);
                
                if (!result)
                    return NotFound($"Note with ID {id} not found");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting note {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
