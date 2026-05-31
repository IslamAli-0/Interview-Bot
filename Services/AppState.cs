namespace TelegramInterviewBot.Services;

public interface IAppState
{
    DateTimeOffset StartedAtUtc { get; }
}

public class AppState : IAppState
{
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
}
