using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TelegramInterviewBot.Data;

public class BotDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        // 1. Build configuration to read the connection string from appsettings.json
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // 2. Configure DbContextOptions for SQLite
        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        optionsBuilder.UseSqlite(connectionString);

        return new BotDbContext(optionsBuilder.Options);
    }
}