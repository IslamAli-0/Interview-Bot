using Microsoft.Extensions.Options;
using TelegramInterviewBot.Models;

namespace TelegramInterviewBot.Services;

public interface IAdminAuthService
{
    bool IsAuthorized(HttpRequest request);
}

public class AdminAuthService : IAdminAuthService
{
    public const string PasswordHeader = "X-Admin-Password";
    private readonly AdminOptions _options;

    public AdminAuthService(IOptions<AdminOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAuthorized(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            return false;
        }

        if (request.Headers.TryGetValue(PasswordHeader, out var headerValue))
        {
            return string.Equals(headerValue.ToString(), _options.Password, StringComparison.Ordinal);
        }

        if (request.Headers.TryGetValue("Authorization", out var authorization))
        {
            const string bearerPrefix = "Bearer ";
            var value = authorization.ToString();
            if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = value.Substring(bearerPrefix.Length).Trim();
                return string.Equals(token, _options.Password, StringComparison.Ordinal);
            }
        }

        return false;
    }
}
