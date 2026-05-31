using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TelegramInterviewBot.Data;

namespace TelegramInterviewBot.Services;

public interface ITelegramBroadcastService
{
    Task BroadcastDailyQuestionAsync(string questionText, CancellationToken cancellationToken);
}

public class TelegramBroadcastService : ITelegramBroadcastService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotDbContext _dbContext;
    private readonly ILogger<TelegramBroadcastService> _logger;

    public TelegramBroadcastService(ITelegramBotClient botClient, BotDbContext dbContext, ILogger<TelegramBroadcastService> logger)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task BroadcastDailyQuestionAsync(string questionText, CancellationToken cancellationToken)
    {
        var chatIds = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsSubscribed)
            .Select(x => x.TelegramId)
            .ToListAsync(cancellationToken);

        var throttle = new SemaphoreSlim(10);
        var tasks = chatIds.Select(async chatId =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                await _botClient.SendMessage(chatId, questionText, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send daily question to {ChatId}", chatId);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
