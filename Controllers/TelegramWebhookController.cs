using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using TelegramInterviewBot.Services;

namespace TelegramInterviewBot.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramUpdateHandler _updateHandler;

    public TelegramWebhookController(ITelegramUpdateHandler updateHandler)
    {
        _updateHandler = updateHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
    {
        await _updateHandler.HandleAsync(update, cancellationToken);
        return Ok();
    }
}
