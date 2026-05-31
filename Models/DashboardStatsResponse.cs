namespace TelegramInterviewBot.Models;

public record DashboardStatsResponse(
    int ActiveDifficultyLevel,
    bool IsDifficultyFrozen,
    int TotalQuestionsAsked,
    IReadOnlyList<LeaderboardEntry> Leaderboard);
