using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramInterviewBot.Data;
using DataUser = TelegramInterviewBot.Data.User;
using TelegramUser = Telegram.Bot.Types.User;

namespace TelegramInterviewBot.Services;

public interface ITelegramUpdateHandler
{
    Task HandleAsync(Update update, CancellationToken cancellationToken);
}

public class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotDbContext _dbContext;
    private readonly ISettingsService _settings;
    private readonly IQuestionService _questions;
    private readonly IGeminiClient _gemini;
    private readonly IAppState _appState;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        ITelegramBotClient botClient,
        BotDbContext dbContext,
        ISettingsService settings,
        IQuestionService questions,
        IGeminiClient gemini,
        IAppState appState,
        ILogger<TelegramUpdateHandler> logger)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _settings = settings;
        _questions = questions;
        _gemini = gemini;
        _appState = appState;
        _logger = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message?.Text == null)
        {
            return;
        }

        if (update.Message.Chat.Type != ChatType.Private)
        {
            return;
        }

        var messageText = update.Message.Text.Trim();
        if (messageText.Length == 0 || update.Message.From == null)
        {
            return;
        }

        if (messageText.StartsWith("/", StringComparison.Ordinal))
        {
            await HandleCommandAsync(update.Message, messageText, cancellationToken);
            return;
        }

        await HandleAnswerAsync(update.Message, messageText, cancellationToken);
    }

    private async Task HandleCommandAsync(Message message, string messageText, CancellationToken cancellationToken)
    {
        var command = messageText.Split(' ', 2)[0];
        var commandName = command.Split('@')[0].ToLowerInvariant();

        switch (commandName)
        {
            case "/start":
            case "/subscribe":
                await SubscribeAsync(message, cancellationToken);
                break;
            case "/unsubscribe":
                await UnsubscribeAsync(message, cancellationToken);
                break;
            case "/ask":
            case "/test":
                await SendPracticeQuestionAsync(message, cancellationToken);
                break;
            case "/hint":
                await SendHintAsync(message, cancellationToken);
                break;
            case "/profile":
                await SendProfileAsync(message, cancellationToken);
                break;
            case "/help":
                await SendHelpAsync(message, cancellationToken);
                break;
            case "/ping":
                await _botClient.SendMessage(message.Chat.Id, "pong", cancellationToken: cancellationToken);
                break;
            case "/stats":
                await SendStatsAsync(message, cancellationToken);
                break;
            default:
                await SendHelpAsync(message, cancellationToken);
                break;
        }
    }

    private async Task HandleAnswerAsync(Message message, string answerText, CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(message.From!, cancellationToken);

        var pendingQuestion = await _settings.GetAsync(SettingsKeys.PendingPracticeQuestion(user.TelegramId), cancellationToken);
        if (!string.IsNullOrWhiteSpace(pendingQuestion))
        {
            var pendingSolution = await _settings.GetAsync(SettingsKeys.PendingPracticeSolution(user.TelegramId), cancellationToken) ?? string.Empty;
            var evaluation = await _gemini.EvaluateAnswerAsync(pendingQuestion, answerText, pendingSolution, cancellationToken);

            await _settings.RemoveAsync(SettingsKeys.PendingPracticeQuestion(user.TelegramId), cancellationToken);
            await _settings.RemoveAsync(SettingsKeys.PendingPracticeSolution(user.TelegramId), cancellationToken);

            var response = FormatEvaluation(evaluation, pendingSolution);
            await _botClient.SendMessage(message.Chat.Id, response, parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
            return;
        }

        var activeQuestion = await _questions.GetActiveDailyQuestionAsync(cancellationToken);
        if (activeQuestion == null)
        {
            await _botClient.SendMessage(message.Chat.Id, "No active daily question is available yet.", cancellationToken: cancellationToken);
            return;
        }

        var alreadyAnswered = await _dbContext.AnswerHistories
            .AsNoTracking()
            .AnyAsync(x => x.TelegramId == user.TelegramId && x.QuestionId == activeQuestion.Id, cancellationToken);

        if (alreadyAnswered)
        {
            await _botClient.SendMessage(message.Chat.Id, "You already submitted an answer for the current daily challenge.", cancellationToken: cancellationToken);
            return;
        }

        var dailyEvaluation = await _gemini.EvaluateAnswerAsync(activeQuestion.QuestionText, answerText, activeQuestion.ModelSolution, cancellationToken);

        user.Score += dailyEvaluation.Score;
        _dbContext.AnswerHistories.Add(new AnswerHistory
        {
            TelegramId = user.TelegramId,
            QuestionId = activeQuestion.Id,
            GivenAnswer = answerText,
            ScoreReceived = dailyEvaluation.Score
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dailyResponse = FormatEvaluation(dailyEvaluation, activeQuestion.ModelSolution);
        await _botClient.SendMessage(message.Chat.Id, dailyResponse, parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
    }

    private async Task SubscribeAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(message.From!, cancellationToken);
        user.IsSubscribed = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _botClient.SendMessage(message.Chat.Id, "Subscription enabled. You will receive the daily challenge in DM.", cancellationToken: cancellationToken);
    }

    private async Task UnsubscribeAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(message.From!, cancellationToken);
        user.IsSubscribed = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _botClient.SendMessage(message.Chat.Id, "Subscription disabled. Your leaderboard score is preserved.", cancellationToken: cancellationToken);
    }

    private async Task SendPracticeQuestionAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(message.From!, cancellationToken);
        var (question, solution) = await _questions.CreatePracticeQuestionAsync(cancellationToken);

        await _settings.SetAsync(SettingsKeys.PendingPracticeQuestion(user.TelegramId), question, cancellationToken);
        await _settings.SetAsync(SettingsKeys.PendingPracticeSolution(user.TelegramId), solution, cancellationToken);

        await _botClient.SendMessage(message.Chat.Id, question, cancellationToken: cancellationToken);
    }

    private async Task SendHintAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(message.From!, cancellationToken);
        var pendingQuestion = await _settings.GetAsync(SettingsKeys.PendingPracticeQuestion(user.TelegramId), cancellationToken);
        var question = pendingQuestion;

        if (string.IsNullOrWhiteSpace(question))
        {
            var activeQuestion = await _questions.GetActiveDailyQuestionAsync(cancellationToken);
            question = activeQuestion?.QuestionText;
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            await _botClient.SendMessage(message.Chat.Id, "No active question is available for hints yet.", cancellationToken: cancellationToken);
            return;
        }

        var hint = await _gemini.GenerateHintAsync(question, cancellationToken);
        await _botClient.SendMessage(message.Chat.Id, hint, cancellationToken: cancellationToken);
    }

    private async Task SendProfileAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { message.From!.Id }, cancellationToken);
        if (user == null)
        {
            await _botClient.SendMessage(message.Chat.Id, "No profile found. Use /start to register.", cancellationToken: cancellationToken);
            return;
        }

        var attempts = await _dbContext.AnswerHistories
            .AsNoTracking()
            .CountAsync(x => x.TelegramId == user.TelegramId, cancellationToken);

        var response = $"Profile for {user.Name}\nTotal Score: {user.Score}\nChallenges Attempted: {attempts}";
        await _botClient.SendMessage(message.Chat.Id, response, cancellationToken: cancellationToken);
    }

    private Task SendHelpAsync(Message message, CancellationToken cancellationToken)
    {
        var response = string.Join("\n", new[]
        {
            "Available commands:",
            "/start or /subscribe - Enable daily challenges",
            "/unsubscribe - Disable daily challenges",
            "/ask or /test - Get a practice question",
            "/hint - Get a hint for the active question",
            "/profile - View your stats",
            "/stats - View bot stats",
            "/ping - Health check",
            "/help - Show this help"
        });

        return _botClient.SendMessage(message.Chat.Id, response, cancellationToken: cancellationToken);
    }

    private async Task SendStatsAsync(Message message, CancellationToken cancellationToken)
    {
        var uptime = DateTimeOffset.UtcNow - _appState.StartedAtUtc;
        var totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var subscribers = await _dbContext.Users.AsNoTracking().CountAsync(x => x.IsSubscribed, cancellationToken);

        var response = string.Join("\n", new[]
        {
            $"Uptime: {FormatDuration(uptime)}",
            $"Total Users: {totalUsers}",
            $"Subscribers: {subscribers}"
        });

        await _botClient.SendMessage(message.Chat.Id, response, cancellationToken: cancellationToken);
    }

    private async Task<DataUser> EnsureUserAsync(TelegramUser from, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { from.Id }, cancellationToken);
        var displayName = BuildDisplayName(from);

        if (user == null)
        {
            user = new DataUser
            {
                TelegramId = from.Id,
                Name = displayName,
                IsSubscribed = true,
                Score = 0
            };
            _dbContext.Users.Add(user);
        }
        else if (!string.Equals(user.Name, displayName, StringComparison.Ordinal))
        {
            user.Name = displayName;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static string BuildDisplayName(TelegramUser user)
    {
        var parts = new[] { user.FirstName, user.LastName };
        var name = string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username;
        }

        return "Developer";
    }

    private static string FormatEvaluation(Models.AnswerEvaluation evaluation, string modelSolution)
    {
        var score = TelegramMarkdown.Escape(evaluation.Score.ToString(CultureInfo.InvariantCulture));
        var critique = TelegramMarkdown.Escape(evaluation.Critique);
        var solution = TelegramMarkdown.Escape(modelSolution);

        return $"*Score:* {score}/10\n*Critique:* {critique}\n*Model Solution:*\n{solution}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{duration.Seconds}s";
    }
}
