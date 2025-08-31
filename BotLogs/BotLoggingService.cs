using Discord;
using Discord.Interactions;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AribethBot
{
    public class BotLoggingService
    {
        // declare the fields used later in this class
        private readonly ILogger logger;
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly InteractionService interactCommands;
        private readonly IConfiguration config;

        public BotLoggingService(IServiceProvider services)
        {
            // get the services we need via DI, and assign the fields declared above to them
            client = services.GetRequiredService<DiscordSocketClient>();
            commands = services.GetRequiredService<CommandService>();
            logger = services.GetRequiredService<ILogger<BotLoggingService>>();
            interactCommands = services.GetRequiredService<InteractionService>();
            config = services.GetRequiredService<IConfiguration>();
            // hook into these events with the methods provided below
            client.Ready += OnReadyAsync;
            client.Log += OnLogAsync;
            commands.Log += OnLogAsync;
            
            // when bot joins new guilds
            client.JoinedGuild += OnJoinedGuildAsync;
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

        private async Task<Task> PurgeGlobalCommands()
        {
            logger.LogInformation($"Purging Global Commands");
            await client.Rest.DeleteAllGlobalCommandsAsync();
            return Task.CompletedTask;
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
                case "Critical":
                {
                    logger.LogCritical(logText);
                    break;
                }
                case "Warning":
                {
                    logger.LogWarning(logText);
                    break;
                }
                case "Info":
                {
                    logger.LogInformation(logText);
                    break;
                }
                case "Verbose":
                {
                    logger.LogInformation(logText);
                    break;
                }
                case "Debug":
                {
                    logger.LogDebug(logText);
                    break;
                }
                case "Error":
                {
                    logger.LogError(logText);
                    break;
                }
            }
            return Task.CompletedTask;
        }
        
        private async Task OnJoinedGuildAsync(SocketGuild guild)
        {
            logger.LogInformation($"Joined new guild: {guild.Name} ({guild.Id})");
            await EnsureGuildConfigExists(guild);
        }
        
        private async Task EnsureGuildConfigExists(SocketGuild guild)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
            string json = await File.ReadAllTextAsync(filePath);

            // Use JObject for safe manipulation
            JObject jsonObj = JObject.Parse(json);

            string guildId = guild.Id.ToString();

            // Ensure "guilds" section exists
            jsonObj["guilds"] ??= new JObject();

            // Check if this guild exists
            if (jsonObj["guilds"][guildId] == null)
            {
                // Use JObject.FromObject to safely convert the anonymous type
                JObject guildConfig = JObject.FromObject(new
                {
                    channelDeletedLog = "0",
                    channelEditedLog = "0",
                    channelEntryOutLog = "0",
                    channelBanLog = "0",
                    channelVoiceActivityLog = "0"
                });

                // Assign it directly to the guild key
                jsonObj["guilds"][guildId] = guildConfig;

                // Save changes
                await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                logger.LogInformation($"Created default config for guild {guild.Name} ({guild.Id})");
            }
            else
            {
                logger.LogInformation($"Config already exists for guild {guild.Name} ({guild.Id})");
            }
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