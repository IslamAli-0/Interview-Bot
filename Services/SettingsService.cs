using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TelegramInterviewBot.Data;

namespace TelegramInterviewBot.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);
    Task<int?> GetIntAsync(string key, CancellationToken cancellationToken);
    Task<DateOnly?> GetDateAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, string value, CancellationToken cancellationToken);
    Task SetIntAsync(string key, int value, CancellationToken cancellationToken);
    Task SetDateAsync(string key, DateOnly value, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public class SettingsService : ISettingsService
{
    private readonly BotDbContext _dbContext;

    public SettingsService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        return setting?.Value;
    }

    public async Task<int?> GetIntAsync(string key, CancellationToken cancellationToken)
    {
        var value = await GetAsync(key, cancellationToken);
        if (value == null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public async Task<DateOnly?> GetDateAsync(string key, CancellationToken cancellationToken)
    {
        var value = await GetAsync(key, cancellationToken);
        if (value == null)
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    public Task SetIntAsync(string key, int value, CancellationToken cancellationToken)
    {
        return SetAsync(key, value.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    public Task SetDateAsync(string key, DateOnly value, CancellationToken cancellationToken)
    {
        return SetAsync(key, value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting == null)
        {
            setting = new Setting { Key = key, Value = value };
            _dbContext.Settings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting == null)
        {
            return;
        }

        _dbContext.Settings.Remove(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
