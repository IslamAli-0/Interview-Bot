using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramInterviewBot.Data;
using TelegramInterviewBot.Models;
using TelegramInterviewBot.Services;

namespace TelegramInterviewBot.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly BotDbContext _dbContext;
    private readonly IQuestionService _questions;

    public DashboardController(BotDbContext dbContext, IQuestionService questions)
    {
        _dbContext = dbContext;
        _questions = questions;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var leaderboard = await _dbContext.Users
            .AsNoTracking()
            .GroupJoin(
                _dbContext.AnswerHistories.AsNoTracking(),
                user => user.TelegramId,
                answer => answer.TelegramId,
                (user, answers) => new { user, AnsweredCount = answers.Count() })
            .OrderByDescending(x => x.user.Score)
            .Select(x => new LeaderboardEntry(
                x.user.TelegramId,
                x.user.Name,
                x.user.Score,
                x.AnsweredCount))
            .ToListAsync(cancellationToken);

        var totalQuestionsAsked = await _dbContext.AskedQuestions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var (level, frozen) = await _questions.GetCurrentDifficultyAsync(
            TimeZoneHelper.GetCairoDate(DateTimeOffset.UtcNow),
            cancellationToken);

        return new DashboardStatsResponse(level, frozen, totalQuestionsAsked, leaderboard);
    }
}
