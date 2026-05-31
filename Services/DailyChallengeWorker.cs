using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TelegramInterviewBot.Data;

namespace TelegramInterviewBot.Services;

public class DailyChallengeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyChallengeWorker> _logger;

    public DailyChallengeWorker(IServiceScopeFactory scopeFactory, ILogger<DailyChallengeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Daily challenge worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily challenge run failed.");
            }

            var nextMidnightUtc = TimeZoneHelper.GetNextCairoMidnightUtc(DateTimeOffset.UtcNow);
            var delay = nextMidnightUtc - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.FromMinutes(1))
            {
                delay = TimeSpan.FromMinutes(1);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var questionService = scope.ServiceProvider.GetRequiredService<IQuestionService>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<ITelegramBroadcastService>();

        var today = TimeZoneHelper.GetCairoDate(DateTimeOffset.UtcNow);
        var lastQuestion = await dbContext.AskedQuestions
            .OrderByDescending(x => x.AskedOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastQuestion != null && TimeZoneHelper.ToCairoDate(lastQuestion.AskedOn) == today)
        {
            return;
        }

        var question = await questionService.CreateDailyQuestionAsync(cancellationToken);
        await broadcaster.BroadcastDailyQuestionAsync(question.QuestionText, cancellationToken);
    }
}
