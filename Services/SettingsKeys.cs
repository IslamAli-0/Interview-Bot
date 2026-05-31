namespace TelegramInterviewBot.Services;

public static class SettingsKeys
{
    public const string ActiveDailyQuestionId = "ActiveDailyQuestionId";
    public const string ForcedDifficultyLevel = "ForcedDifficultyLevel";
    public const string DifficultyCycleAnchor = "DifficultyCycleAnchor";

    public static string PendingPracticeQuestion(long telegramId) => $"PendingPracticeQuestion:{telegramId}";
    public static string PendingPracticeSolution(long telegramId) => $"PendingPracticeSolution:{telegramId}";
}
