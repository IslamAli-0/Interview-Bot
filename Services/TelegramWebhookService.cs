using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramInterviewBot.Models;

namespace TelegramInterviewBot.Services;

public class TelegramWebhookService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _config;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(ITelegramBotClient botClient, IOptions<BotConfiguration> options, ILogger<TelegramWebhookService> logger)
    {
        _botClient = botClient;
        _config = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.HostAddress))
        {
            _logger.LogWarning("BotConfiguration:HostAddress is empty. Skipping webhook registration.");
            return;
        }

        var url = _config.HostAddress.TrimEnd('/') + "/api/telegram/webhook";
        await _botClient.SetWebhook(url, allowedUpdates: new[] { UpdateType.Message }, dropPendingUpdates: true, cancellationToken: cancellationToken);
        _logger.LogInformation("Telegram webhook set to {WebhookUrl}", url);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.DeleteWebhook(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Telegram webhook.");
        }
    }
}
