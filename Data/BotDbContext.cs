using Microsoft.EntityFrameworkCore;

namespace TelegramInterviewBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AskedQuestion> AskedQuestions => Set<AskedQuestion>();
    public DbSet<AnswerHistory> AnswerHistories => Set<AnswerHistory>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.TelegramId);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.IsSubscribed).HasConversion<int>();
        });

        modelBuilder.Entity<AskedQuestion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuestionText).IsRequired();
            entity.Property(x => x.ModelSolution).IsRequired();
            entity.Property(x => x.VectorEmbedding).IsRequired();
        });

        modelBuilder.Entity<AnswerHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.TelegramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<AskedQuestion>()
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.TelegramId, x.QuestionId }).IsUnique();
            entity.Property(x => x.GivenAnswer).IsRequired();
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Value).IsRequired();
        });
    }
}

public class User
{
    public long TelegramId { get; set; } // Primary Key (Telegram unique user ID)
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsSubscribed { get; set; } = true;
}

public class AskedQuestion
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public byte[] VectorEmbedding { get; set; } = Array.Empty<byte>(); // For concept deduplication
    public string ModelSolution { get; set; } = string.Empty; // Pre-generated optimal answer
    public DateTime AskedOn { get; set; }
}

public class AnswerHistory
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public int QuestionId { get; set; }
    public string GivenAnswer { get; set; } = string.Empty;
    public int ScoreReceived { get; set; }
}

public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}