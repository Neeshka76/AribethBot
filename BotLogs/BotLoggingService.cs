using Discord;
using Discord.Interactions;
using Discord.Commands;
using Discord.WebSocket;
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

        public BotLoggingService(IServiceProvider services)
        {
            // get the services we need via DI, and assign the fields declared above to them
            client = services.GetRequiredService<DiscordSocketClient>();
            commands = services.GetRequiredService<CommandService>();
            logger = services.GetRequiredService<ILogger<BotLoggingService>>();
            interactCommands = services.GetRequiredService<InteractionService>();
            // hook into these events with the methods provided below
            client.Ready += OnReadyAsync;
            client.Log += OnLogAsync;
            commands.Log += OnLogAsync;
        }

        // this method executes on the bot being connected/ready
        public async Task OnReadyAsync()
        {
            if (IsDebug())
            {
                logger.LogInformation($"In Debug mode !");
                //await PurgeGlobalCommands();
                //await ConfigureLocalCommands();
            }
            else
            {
                logger.LogInformation($"In Runtime mode !");
                //await PurgeLocalCommands();
                await ConfigureGlobalCommands();
            }
            logger.LogInformation($"Connected as -> [{client.CurrentUser}] :)");
            logger.LogInformation($"We are on [{client.Guilds.Count}] servers");
            await client.SetGameAsync("over Neverwinter", type: ActivityType.Watching);
        }

        private async Task<Task> PurgeGlobalCommands()
        {
            IReadOnlyCollection<SocketGuild> guilds = client.Guilds;
            logger.LogInformation($"Purging Global Commands");
            await client.Rest.DeleteAllGlobalCommandsAsync();
            return Task.CompletedTask;
        }

        private async Task<Task> PurgeLocalCommands()
        {
            IReadOnlyCollection<SocketGuild> guilds = client.Guilds;
            foreach (SocketGuild guild in guilds)
            {
                logger.LogInformation($"Purging Application Commands for {guild.Id}...");
                await guild.DeleteApplicationCommandsAsync();
                await Task.Delay(500);
            }
            return Task.CompletedTask;
        }

        private async Task<Task> ConfigureLocalCommands()
        {
            IReadOnlyCollection<SocketGuild> guilds = client.Guilds;
            foreach (SocketGuild guild in guilds)
            {
                logger.LogInformation($"Adding commands to {guild.Id}...");
                await interactCommands.RegisterCommandsToGuildAsync(guild.Id);
                await Task.Delay(500);
            }
            return Task.CompletedTask;
        }

        private async Task<Task> ConfigureGlobalCommands()
        {
            logger.LogInformation($"Adding Global Commands");
            await interactCommands.RegisterCommandsGloballyAsync();
            return Task.CompletedTask;
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