namespace TelegramInterviewBot.Services;

public static class TimeZoneHelper
{
    public static TimeZoneInfo GetCairoTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.Utc;
    }

    public static DateOnly GetCairoDate(DateTimeOffset utcNow)
    {
        var tz = GetCairoTimeZone();
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static DateTimeOffset GetNextCairoMidnightUtc(DateTimeOffset utcNow)
    {
        var tz = GetCairoTimeZone();
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var nextLocalMidnight = local.Date.AddDays(1);
        var nextLocalOffset = new DateTimeOffset(nextLocalMidnight, tz.GetUtcOffset(nextLocalMidnight));
        return nextLocalOffset.ToUniversalTime();
    }

    public static DateOnly ToCairoDate(DateTime utcDateTime)
    {
        var tz = GetCairoTimeZone();
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return DateOnly.FromDateTime(local);
    }
}
