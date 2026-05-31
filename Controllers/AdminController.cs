using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramInterviewBot.Data;
using TelegramInterviewBot.Services;

namespace TelegramInterviewBot.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminAuthService _adminAuth;
    private readonly BotDbContext _dbContext;
    private readonly ITelegramBroadcastService _broadcast;

    public AdminController(
        IAdminAuthService adminAuth,
        BotDbContext dbContext,
        ITelegramBroadcastService broadcast)
    {
        _adminAuth = adminAuth;
        _dbContext = dbContext;
        _broadcast = broadcast;
    }

    [HttpPost("verify")]
    public IActionResult Verify()
    {
        return _adminAuth.IsAuthorized(Request) ? NoContent() : Unauthorized();
    }

    [HttpDelete("leaderboard")]
    public async Task<IActionResult> ClearLeaderboard(CancellationToken cancellationToken)
    {
        if (!_adminAuth.IsAuthorized(Request))
        {
            return Unauthorized();
        }

        await _dbContext.AnswerHistories.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Users.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Score, 0),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("broadcast-test")]
    public async Task<IActionResult> BroadcastTest(CancellationToken cancellationToken)
    {
        if (!_adminAuth.IsAuthorized(Request))
        {
            return Unauthorized();
        }

        var subscriberCount = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsSubscribed)
            .CountAsync(cancellationToken);

        if (subscriberCount == 0)
        {
            return Ok(new { sent = 0 });
        }

        var message = $"Test broadcast from admin at {DateTimeOffset.UtcNow:O}.";
        await _broadcast.BroadcastDailyQuestionAsync(message, cancellationToken);

        return Ok(new { sent = subscriberCount });
    }
}
