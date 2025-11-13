using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AribethBot.Database;

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<DatabaseContext> optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        optionsBuilder.UseSqlite("Data Source=AribethBot.db"); // same as your runtime DB
        
        return new DatabaseContext(optionsBuilder.Options);
    }
}