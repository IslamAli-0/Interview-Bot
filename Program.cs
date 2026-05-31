using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramInterviewBot.Data;
using TelegramInterviewBot.Models;
using TelegramInterviewBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

builder.Services.AddDbContext<BotDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString);
});

builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IAppState, AppState>();
builder.Services.AddSingleton<IAdminAuthService, AdminAuthService>();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
    if (string.IsNullOrWhiteSpace(config.BotToken))
    {
        throw new InvalidOperationException("BotConfiguration:BotToken is required.");
    }

    return new TelegramBotClient(config.BotToken);
});

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();
builder.Services.AddScoped<ITelegramBroadcastService, TelegramBroadcastService>();

builder.Services.AddHostedService<DailyChallengeWorker>();
builder.Services.AddHostedService<TelegramWebhookService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseHttpLogging();
app.UseCors("Dashboard");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", (IAppState state) =>
{
    var uptime = DateTimeOffset.UtcNow - state.StartedAtUtc;
    return Results.Ok(new
    {
        status = "ok",
        startedAtUtc = state.StartedAtUtc,
        uptimeSeconds = (int)uptime.TotalSeconds
    });
});
app.MapControllers();

app.Run();
