using Microsoft.AspNetCore.Mvc;
using TelegramInterviewBot.Services;

namespace TelegramInterviewBot.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    private readonly IAdminAuthService _adminAuth;

    public SettingsController(ISettingsService settings, IAdminAuthService adminAuth)
    {
        _settings = settings;
        _adminAuth = adminAuth;
    }

    [HttpPost("level/{newLevel:int}")]
    public async Task<IActionResult> SetLevel(int newLevel, CancellationToken cancellationToken)
    {
        if (!_adminAuth.IsAuthorized(Request))
        {
            return Unauthorized();
        }

        if (newLevel < 1 || newLevel > 10)
        {
            return BadRequest("Difficulty level must be between 1 and 10.");
        }

        await _settings.SetIntAsync(SettingsKeys.ForcedDifficultyLevel, newLevel, cancellationToken);
        return NoContent();
    }

    [HttpDelete("level")]
    public async Task<IActionResult> ClearLevel(CancellationToken cancellationToken)
    {
        if (!_adminAuth.IsAuthorized(Request))
        {
            return Unauthorized();
        }

        await _settings.RemoveAsync(SettingsKeys.ForcedDifficultyLevel, cancellationToken);
        return NoContent();
    }
}
