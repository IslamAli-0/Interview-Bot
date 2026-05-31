namespace TelegramInterviewBot.Models;

public record LeaderboardEntry(long TelegramId, string Name, int Score, int AnsweredCount);
