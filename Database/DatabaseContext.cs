using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AribethBot.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> optionsBuilderOptions) : DbContext
{
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<SpamTrigger> SpamTriggers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        SqliteConnectionStringBuilder connectionStringBuilder = new() { DataSource = "AribethBot.db" };
        SqliteConnection connection = new(connectionStringBuilder.ToString());
        optionsBuilder
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Convert ulong → long for SQLite
        modelBuilder.Entity<Guild>()
            .Property(g => g.GuildId)
            .HasConversion<long>();

        modelBuilder.Entity<Guild>()
            .Property(g => g.ChannelDeletedLog)
            .HasConversion<long?>();

        modelBuilder.Entity<Guild>()
            .Property(g => g.ChannelEditedLog)
            .HasConversion<long?>();

        modelBuilder.Entity<Guild>()
            .Property(g => g.ChannelEntryOutLog)
            .HasConversion<long?>();

        modelBuilder.Entity<Guild>()
            .Property(g => g.ChannelBanLog)
            .HasConversion<long?>();

        modelBuilder.Entity<Guild>()
            .Property(g => g.ChannelVoiceActivityLog)
            .HasConversion<long?>();

        // SpamTrigger foreign key
        modelBuilder.Entity<SpamTrigger>()
            .HasKey(s => new { s.GuildId, s.Type });

        modelBuilder.Entity<SpamTrigger>()
            .HasOne(s => s.Guild)
            .WithMany(g => g.SpamTriggers)
            .HasForeignKey(s => s.GuildId);
        
        // Enum conversions
        modelBuilder.Entity<SpamTrigger>()
            .Property(s => s.Type)
            .HasConversion<string>();

        modelBuilder.Entity<SpamTrigger>()
            .Property(s => s.ActionType)
            .HasConversion<string>();
    }
}