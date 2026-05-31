# Telegram Interview Master Bot

A hybrid Telegram bot and local Web API that runs a daily technical interview loop for backend developers focused on C# and .NET.

## What it does
- Sends a daily challenge to subscribed users in private Telegram chats
- Grades replies as a strict senior engineer and returns feedback plus a model solution
- Tracks scores and history in a local SQLite database
- Exposes a lightweight admin dashboard API for stats and difficulty control

## Docs
- System architecture: docs/SystemArchitecture.md

## Quickstart
1. Install the .NET SDK that matches the project target framework.
2. Set values in appsettings.json (or appsettings.Development.json for local overrides).
3. Run the app:

```
dotnet run
```

The app applies EF Core migrations automatically on startup.

## Database and migrations
- Restore local tools: `dotnet tool restore`
- Add a migration: `dotnet tool run dotnet-ef migrations add <Name>`
- Apply migrations: `dotnet tool run dotnet-ef database update`

## Configuration
appsettings.json:
- BotConfiguration: BotToken, HostAddress
- Admin: Password (used for dashboard admin actions)
- Gemini: ApiKey, EnableEmbeddings, EmbeddingModel
- ConnectionStrings: DefaultConnection (SQLite file path)

If you see 404 errors for embeddings, set `Gemini:EnableEmbeddings` to `false` to skip semantic deduplication.

## API Endpoints
- GET /api/dashboard/stats
- POST /api/settings/level/{newLevel}
- DELETE /api/settings/level
- POST /api/admin/verify
- POST /api/admin/broadcast-test
- DELETE /api/admin/leaderboard
- GET /health

Admin endpoints require the header `X-Admin-Password` with the configured password.

## Telegram Commands
- /start, /subscribe, /unsubscribe
- /ask, /test, /hint
- /profile, /help, /ping, /stats

## Development Notes
- Current target framework is net10.0 (see TelegramInterviewBot.csproj).
- The architecture spec targets .NET 8; align the target framework as needed.
