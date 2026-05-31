using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelegramInterviewBot.Data;
using TelegramInterviewBot.Models;

namespace TelegramInterviewBot.Services;

public interface IQuestionService
{
    Task<AskedQuestion?> GetActiveDailyQuestionAsync(CancellationToken cancellationToken);
    Task<AskedQuestion> CreateDailyQuestionAsync(CancellationToken cancellationToken);
    Task<(int Level, bool IsFrozen)> GetCurrentDifficultyAsync(DateOnly date, CancellationToken cancellationToken);
    Task<(string Question, string ModelSolution)> CreatePracticeQuestionAsync(CancellationToken cancellationToken);
}

public class QuestionService : IQuestionService
{
    private const double SimilarityThreshold = 0.85;
    private readonly BotDbContext _dbContext;
    private readonly ISettingsService _settings;
    private readonly IGeminiClient _gemini;
    private readonly GeminiOptions _geminiOptions;
    private readonly ILogger<QuestionService> _logger;

    public QuestionService(
        BotDbContext dbContext,
        ISettingsService settings,
        IGeminiClient gemini,
        IOptions<GeminiOptions> geminiOptions,
        ILogger<QuestionService> logger)
    {
        _dbContext = dbContext;
        _settings = settings;
        _gemini = gemini;
        _geminiOptions = geminiOptions.Value;
        _logger = logger;
    }

    public async Task<AskedQuestion?> GetActiveDailyQuestionAsync(CancellationToken cancellationToken)
    {
        var activeId = await _settings.GetIntAsync(SettingsKeys.ActiveDailyQuestionId, cancellationToken);
        if (activeId.HasValue)
        {
            var active = await _dbContext.AskedQuestions.FindAsync(new object[] { activeId.Value }, cancellationToken);
            if (active != null)
            {
                return active;
            }
        }

        return await _dbContext.AskedQuestions
            .OrderByDescending(x => x.AskedOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AskedQuestion> CreateDailyQuestionAsync(CancellationToken cancellationToken)
    {
        var cairoDate = TimeZoneHelper.GetCairoDate(DateTimeOffset.UtcNow);
        var (difficulty, _) = await GetCurrentDifficultyAsync(cairoDate, cancellationToken);

        var embeddingsEnabled = _geminiOptions.EnableEmbeddings;
        var recentEmbeddings = new List<byte[]>();

        if (embeddingsEnabled)
        {
            recentEmbeddings = await _dbContext.AskedQuestions
                .OrderByDescending(x => x.AskedOn)
                .Select(x => x.VectorEmbedding)
                .Take(30)
                .ToListAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("Embeddings disabled; skipping semantic deduplication.");
        }

        var attempts = 0;
        string question = string.Empty;
        float[] embedding = Array.Empty<float>();

        do
        {
            attempts++;
            question = await _gemini.GenerateQuestionAsync(difficulty, cancellationToken);
            if (!IsQuestionValid(question))
            {
                _logger.LogInformation("Rejected short or vague question (attempt {Attempt}).", attempts);
                continue;
            }

            if (!embeddingsEnabled)
            {
                embedding = Array.Empty<float>();
                break;
            }

            try
            {
                embedding = await _gemini.GenerateEmbeddingAsync(question, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding failed; skipping deduplication for this run.");
                embeddingsEnabled = false;
                embedding = Array.Empty<float>();
                break;
            }

            if (!EmbeddingHelper.IsTooSimilar(embedding, recentEmbeddings, SimilarityThreshold))
            {
                break;
            }

            _logger.LogInformation("Rejected similar question (attempt {Attempt}).", attempts);
        }
        while (attempts < 8);

        var modelSolution = await GenerateShortModelSolutionAsync(question, cancellationToken);

        var askedQuestion = new AskedQuestion
        {
            QuestionText = question,
            ModelSolution = modelSolution,
            VectorEmbedding = EmbeddingHelper.ToBytes(embedding),
            AskedOn = DateTime.UtcNow
        };

        _dbContext.AskedQuestions.Add(askedQuestion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _settings.SetIntAsync(SettingsKeys.ActiveDailyQuestionId, askedQuestion.Id, cancellationToken);

        return askedQuestion;
    }

    public async Task<(int Level, bool IsFrozen)> GetCurrentDifficultyAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var forced = await _settings.GetIntAsync(SettingsKeys.ForcedDifficultyLevel, cancellationToken);
        if (forced.HasValue)
        {
            return (Math.Clamp(forced.Value, 1, 10), true);
        }

        var anchor = await _settings.GetDateAsync(SettingsKeys.DifficultyCycleAnchor, cancellationToken);
        if (!anchor.HasValue)
        {
            anchor = date;
            await _settings.SetDateAsync(SettingsKeys.DifficultyCycleAnchor, anchor.Value, cancellationToken);
        }

        var days = date.DayNumber - anchor.Value.DayNumber;
        if (days < 0)
        {
            days = 0;
        }

        var baseLevel = 4 + (days % 7);
        var level = baseLevel;

        if (baseLevel > 4 && Random.Shared.NextDouble() < 0.2)
        {
            level = Random.Shared.Next(4, baseLevel);
        }

        return (level, false);
    }

    public async Task<(string Question, string ModelSolution)> CreatePracticeQuestionAsync(CancellationToken cancellationToken)
    {
        var cairoDate = TimeZoneHelper.GetCairoDate(DateTimeOffset.UtcNow);
        var (difficulty, _) = await GetCurrentDifficultyAsync(cairoDate, cancellationToken);

        var question = await GenerateValidQuestionAsync(difficulty, cancellationToken);
        var solution = await GenerateShortModelSolutionAsync(question, cancellationToken);
        return (question, solution);
    }

    private async Task<string> GenerateValidQuestionAsync(int difficulty, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var question = await _gemini.GenerateQuestionAsync(difficulty, cancellationToken);
            if (IsQuestionValid(question))
            {
                return question;
            }
        }

        return await _gemini.GenerateQuestionAsync(difficulty, cancellationToken);
    }

    private static bool IsQuestionValid(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var trimmed = question.Trim();
        if (trimmed.Length < 140)
        {
            return false;
        }

        if (!trimmed.EndsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Regex.IsMatch(trimmed, @"\b(how|why|when|what|which|who)\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(trimmed, @"\b(that|this|it|they|which|who|where)\?\s*$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return true;
    }

    private async Task<string> GenerateShortModelSolutionAsync(string question, CancellationToken cancellationToken)
    {
        var solution = await _gemini.GenerateModelSolutionAsync(question, cancellationToken);
        var attempts = 0;

        while (solution.Length > 1800 && attempts < 2)
        {
            attempts++;
            solution = await _gemini.GenerateModelSolutionAsync(question, cancellationToken);
        }

        if (solution.Length > 1800)
        {
            solution = solution.Substring(0, 1800);
        }

        return solution.Trim();
    }
}
