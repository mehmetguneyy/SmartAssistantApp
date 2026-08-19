using Microsoft.AspNetCore.Mvc;
using SmartAssistant.API.Models;
using SmartAssistant.API.Services;

namespace SmartAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;

    public AssistantController(IAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("parse-task")]
    public async Task<ActionResult<CreateTaskDto>> ParseTask([FromBody] string naturalLanguageInput)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguageInput))
        {
            return BadRequest("Girdi metni boş olamaz.");
        }

        var parsedDto = await _assistantService.ParseTaskFromTextAsync(naturalLanguageInput);
        return Ok(parsedDto);
    }

    [HttpPost("chat")]
    public async Task<ActionResult<string>> Chat([FromBody] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("Mesaj boş olamaz.");
        }

        var reply = await _assistantService.ChatWithAssistantAsync(message);
        return Ok(new { response = reply });
    }
}