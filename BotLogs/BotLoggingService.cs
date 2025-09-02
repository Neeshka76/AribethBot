using AribethBot.Database;
using AribethBot.Helpers;
using Discord;
using Discord.Interactions;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AribethBot
{
    public class BotLoggingService
    {
        // declare the fields used later in this class
        private readonly ILogger logger;
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly InteractionService interactCommands;
        private readonly DatabaseContext db;
        private readonly IServiceProvider services;

        public BotLoggingService(IServiceProvider services)
        {
            this.services = services;
            // get the services we need via DI, and assign the fields declared above to them
            client = services.GetRequiredService<DiscordSocketClient>();
            commands = services.GetRequiredService<CommandService>();
            logger = services.GetRequiredService<ILogger<BotLoggingService>>();
            interactCommands = services.GetRequiredService<InteractionService>();
            db = services.GetRequiredService<DatabaseContext>();
            // hook into these events with the methods provided below
            client.Ready += OnReadyAsync;
            client.JoinedGuild += OnJoinedGuildAsync;
            client.Log += OnLogAsync;
            commands.Log += OnLogAsync;
        }

        // this method executes on the bot being connected/ready
        public async Task OnReadyAsync()
        {
            logger.LogInformation($"Connected as -> [{client.CurrentUser}] :)");
            logger.LogInformation($"We are on [{client.Guilds.Count}] servers");
            foreach (SocketGuild guild in client.Guilds)
            {
                logger.LogInformation($"\t- {guild.Name} ({guild.Id})");
            }
            
            // Initialize DB AFTER bot is ready
            await InitializeDatabaseAsync();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    if (IsDebug())
                    {
                        logger.LogInformation("In Debug mode!");
                        await PurgeGlobalCommands();    // remove global commands
                        await RegisterGuildCommands(); // register per-guild
                    }
                    else
                    {
                        logger.LogInformation("In Runtime mode!");
                        await PurgeGlobalCommands();     // always ensure globals are gone
                        await RegisterGuildCommands();  // only guild commands
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while configuring commands in background.");
                }
            });
            
            await client.SetGameAsync("over Neverwinter", type: ActivityType.Watching);
        }
        
        private async Task OnJoinedGuildAsync(SocketGuild guild)
        {
            // Ensure newly joined guild is in DB
            using IServiceScope scope = services.CreateScope();
            DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            await GuildHelper.EnsureGuildInDbAsync(guild, db, logger);
            logger.LogInformation($"Joined new guild: {guild.Name} ({guild.Id})");
        }
        
        private async Task InitializeDatabaseAsync()
        {
            // Apply automatic migrations for the DB
            using IServiceScope scope = services.CreateScope();
            DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            logger.LogInformation("Checking for pending migrations...");
            IEnumerable<string> pendingMigrations = await db.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("No pending migrations found. Database is up to date.");
            }

            foreach (SocketGuild guild in client.Guilds)
            {
                await GuildHelper.EnsureGuildInDbAsync(guild, db, logger);
            }
        }

        private async Task PurgeGlobalCommands()
        {
            logger.LogInformation($"Purging Global Commands");
            await client.Rest.DeleteAllGlobalCommandsAsync();
        }

        private async Task RegisterGuildCommands()
        {
            foreach (SocketGuild guild in client.Guilds)
            {
                logger.LogInformation($"Registering commands to {guild.Name} ({guild.Id})...");
                await interactCommands.RegisterCommandsToGuildAsync(guild.Id);
                await Task.Delay(500); // Avoid rate limit...
            }
        }

        // this method switches out the severity level from Discord.Net's API, and logs appropriately
        public Task OnLogAsync(LogMessage msg)
        {
            string logText = $"{msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
            switch (msg.Severity.ToString())
            {
                case "Critical": logger.LogCritical(logText); break;
                case "Warning": logger.LogWarning(logText); break;
                case "Info": logger.LogInformation(logText); break;
                case "Verbose": logger.LogInformation(logText); break;
                case "Debug": logger.LogDebug(logText); break;
                case "Error": logger.LogError(logText); break;
            }
            return Task.CompletedTask;
        }

        static bool IsDebug()
        {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
}