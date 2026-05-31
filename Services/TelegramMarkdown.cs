using System.Text;

namespace TelegramInterviewBot.Services;

public static class TelegramMarkdown
{
    private static readonly HashSet<char> EscapeCharacters = new()
    {
        '\\', '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'
    };

    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            if (EscapeCharacters.Contains(ch))
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
