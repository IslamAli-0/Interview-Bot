using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TelegramInterviewBot.Models;

namespace TelegramInterviewBot.Services;

public interface IGeminiClient
{
    Task<string> GenerateQuestionAsync(int difficulty, CancellationToken cancellationToken);
    Task<string> GenerateModelSolutionAsync(string question, CancellationToken cancellationToken);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
    Task<AnswerEvaluation> EvaluateAnswerAsync(string question, string answer, string modelSolution, CancellationToken cancellationToken);
    Task<string> GenerateHintAsync(string question, CancellationToken cancellationToken);
}

public class GeminiClient : IGeminiClient
{
    private static readonly string[] DefaultGenerationModels =
    {
        "gemini-2.0-flash",
        "gemini-1.5-flash",
        "gemini-2.5-flash"
    };
    private const string DefaultEmbeddingModel = "text-embedding-004";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateQuestionAsync(int difficulty, CancellationToken cancellationToken)
    {
        var prompt = $"Generate 1 practical backend engineering interview question about concepts and principles (APIs, data modeling, concurrency, caching, consistency, security, performance, distributed systems, testing, observability). Include concrete constraints (scale, latency, SLA, throughput, failure modes). Avoid language/framework trivia and avoid code or method signatures; plain English only. Write 1 to 2 sentences with clear context, and end with a single complete question (not a fragment). Target at least 140 characters. Difficulty level: {difficulty}/10. Return only the question text.";
        var response = await GenerateContentAsync(GetGenerationModels(), prompt, maxOutputTokens: 256, temperature: 0.7, cancellationToken);
        return SanitizeQuestion(response);
    }

    public Task<string> GenerateModelSolutionAsync(string question, CancellationToken cancellationToken)
    {
        var prompt = "Provide the optimal answer to the following interview question. Include brief explanation and backend engineering reasoning. If you include code, keep it short and language-agnostic or minimal C#. Keep the response under 1800 characters. Return plain text only, no markdown.\n\nQuestion:\n" + question;
        return GenerateContentAsync(GetGenerationModels(), prompt, maxOutputTokens: 768, temperature: 0.5, cancellationToken);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (!_options.EnableEmbeddings)
        {
            return Array.Empty<float>();
        }

        EnsureApiKey();

        var request = new
        {
            content = new
            {
                parts = new[] { new { text } }
            }
        };

        var primaryModel = string.IsNullOrWhiteSpace(_options.EmbeddingModel)
            ? DefaultEmbeddingModel
            : _options.EmbeddingModel;

        var models = new[] { primaryModel, DefaultEmbeddingModel, "embedding-001" }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var attempts = new List<(string Version, string Model)>();
        foreach (var model in models)
        {
            attempts.Add(("v1beta", model));
            attempts.Add(("v1", model));
        }

        foreach (var (version, model) in attempts)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{version}/models/{model}:embedContent?key={_options.ApiKey}",
                request,
                cancellationToken);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ParseEmbedding(payload);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Embedding model not found: {Version}/{Model}.", version, model);
                continue;
            }

            _logger.LogError("Gemini embed error: {Status} {Payload}", response.StatusCode, payload);
            throw new InvalidOperationException("Gemini embedding request failed.");
        }

        throw new InvalidOperationException("No supported Gemini embedding model was found for this API key.");
    }

    private static float[] ParseEmbedding(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("embedding", out var embedding) ||
            !embedding.TryGetProperty("values", out var values))
        {
            throw new InvalidOperationException("Gemini embedding response missing values.");
        }

        var result = new float[values.GetArrayLength()];
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            result[index++] = value.GetSingle();
        }

        return result;
    }

    public async Task<AnswerEvaluation> EvaluateAnswerAsync(string question, string answer, string modelSolution, CancellationToken cancellationToken)
    {
        var prompt = "You are a strict senior backend engineer. Evaluate the candidate answer for correctness, safety, and edge cases. Return exactly two lines in plain text:\nSCORE: X out of 10\nCRITIQUE: one or two sentences\nIf the answer is empty, irrelevant, or unsafe, score 0.\n\nQuestion:\n" + question + "\n\nModel Solution:\n" + modelSolution + "\n\nCandidate Answer:\n" + answer;
        var response = await GenerateContentAsync(GetGenerationModels(), prompt, maxOutputTokens: 256, temperature: 0.3, cancellationToken);
        return ParseEvaluation(response);
    }

    public Task<string> GenerateHintAsync(string question, CancellationToken cancellationToken)
    {
        var prompt = "Provide a short conceptual hint for the following question. One or two sentences, no code, no direct answer. Return plain text only.\n\nQuestion:\n" + question;
        return GenerateContentAsync(GetGenerationModels(), prompt, maxOutputTokens: 128, temperature: 0.6, cancellationToken);
    }

    private async Task<string> GenerateContentAsync(
        IReadOnlyList<string> models,
        string prompt,
        int maxOutputTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        EnsureApiKey();

        var modelList = models.Count > 0 ? models : DefaultGenerationModels;

        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature,
                maxOutputTokens
            }
        };

        HttpStatusCode? lastStatus = null;
        string? lastPayload = null;

        foreach (var model in modelList)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/{model}:generateContent?key={_options.ApiKey}",
                request,
                cancellationToken);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var text = ExtractContentText(payload);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Gemini returned empty response.");
                }

                return text.Trim();
            }

            lastStatus = response.StatusCode;
            lastPayload = payload;

            if (IsAuthError(response.StatusCode, payload))
            {
                _logger.LogError("Gemini auth error: {Status} {Payload}", response.StatusCode, payload);
                throw new InvalidOperationException("Gemini API key is invalid or revoked.");
            }

            if (ShouldFallback(response.StatusCode, payload))
            {
                _logger.LogWarning("Gemini model {Model} unavailable or rate-limited. Trying fallback.", model);
                continue;
            }

            _logger.LogError("Gemini generate error: {Status} {Payload}", response.StatusCode, payload);
            throw new InvalidOperationException("Gemini generate request failed.");
        }

        _logger.LogError("Gemini generate error after model fallbacks: {Status} {Payload}", lastStatus, lastPayload);
        throw new InvalidOperationException("Gemini generate request failed.");
    }

    private IReadOnlyList<string> GetGenerationModels()
    {
        var models = _options.GenerationModels
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return models.Length > 0 ? models : DefaultGenerationModels;
    }

    private static bool IsAuthError(HttpStatusCode status, string payload)
    {
        if (status == HttpStatusCode.Forbidden || status == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        return payload.Contains("api key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldFallback(HttpStatusCode status, string payload)
    {
        if (status == HttpStatusCode.NotFound || status == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (payload.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (status == HttpStatusCode.BadRequest &&
            (payload.Contains("model", StringComparison.OrdinalIgnoreCase) ||
             payload.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
             payload.Contains("unsupported", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string SanitizeQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace("```", string.Empty).Replace("`", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^\s*(question|q)\s*[:\-]\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^\s*\d+[\).\-]\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        const int maxLength = 800;
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength).TrimEnd();
        }

        if (!cleaned.EndsWith("?", StringComparison.Ordinal))
        {
            cleaned = cleaned.TrimEnd('.', '!', ';', ':') + "?";
        }

        return cleaned;
    }

    private static string ExtractContentText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var content = candidates[0].GetProperty("content");
        if (!content.TryGetProperty("parts", out var parts))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static AnswerEvaluation ParseEvaluation(string response)
    {
        var match = Regex.Match(response, @"score\s*[:\-]?\s*(\d{1,2})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(response, @"(\d{1,2})\s*/\s*10", RegexOptions.IgnoreCase);
        }

        var score = 0;
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
        {
            score = Math.Clamp(parsed, 0, 10);
        }

        var critiqueMatch = Regex.Match(response, @"critique\s*:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var critique = critiqueMatch.Success ? critiqueMatch.Groups[1].Value.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(critique))
        {
            critique = Regex.Replace(response, @"(?im)^\s*score\s*[:\-]?.*$", string.Empty).Trim();
        }

        critique = Regex.Replace(critique, @"(?im)^\s*critique\s*[:\-]?\s*", string.Empty).Trim();

        if (critique.Length < 8)
        {
            critique = BuildDefaultCritique(score);
        }

        return new AnswerEvaluation(score, critique);
    }

    private static string BuildDefaultCritique(int score)
    {
        if (score <= 3)
        {
            return "Answer is incomplete and misses core backend concerns and edge cases.";
        }

        if (score <= 7)
        {
            return "Partially correct but missing key details, trade-offs, or safety considerations.";
        }

        return "Mostly correct, but could be more precise about trade-offs and edge cases.";
    }

    private void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Gemini ApiKey is not configured.");
        }
    }
}
