using System;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace AribethBot
{
    public class EntryPoint
    {

        // setup our fields we assign later
        private DiscordSocketClient client;
        private DiscordSocketConfig config;
        private InteractionService interactCommands;

        public static void Main(string[] args) => new EntryPoint().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.Guilds,
                UseInteractionSnowflakeDate = false
            };
            // call ConfigureServices to create the ServiceCollection/Provider for passing around the services
            using ServiceProvider services = ConfigureServices();
            // get the client and assign to client 
            // you get the services via GetRequiredService<T>
            client = services.GetRequiredService<DiscordSocketClient>();
            interactCommands = services.GetRequiredService<InteractionService>();
            string? token = Environment.GetEnvironmentVariable("DiscordToken");

            // setup logging and the ready event
            client.Log += LogAsync;
            interactCommands.Log += LogAsync;
            client.Ready += ReadyAsync;

            // this is where we get the Token value from the configuration file, and start the bot
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // we get the CommandHandler class here and call the InitializeAsync method to start things up for the CommandHandler service
            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            // this is where you put the id of the test discord guild

            if (IsDebug())
            {
                Console.WriteLine($"In Debug mode !");
                await ConfigureCommands();
            }
            else
            {
                Console.WriteLine($"In Runtime mode !");
                await ConfigureCommands();
            }
            Console.WriteLine($"Connected as -> [{client.CurrentUser}] :)");
            await client.SetGameAsync("over Neverwinter", type: ActivityType.Watching);
        }

        private async Task<Task> ConfigureCommands()
        {
            Console.WriteLine($"Purging Global Commands");
            await client.Rest.DeleteAllGlobalCommandsAsync();
            IReadOnlyCollection<SocketGuild> guilds = client.Guilds;
            foreach (SocketGuild guild in guilds)
            {
                Console.WriteLine($"Purging Application Commands for {guild.Id}...");
                await guild.DeleteApplicationCommandsAsync();
                await interactCommands.RegisterCommandsToGuildAsync(guild.Id);
                Console.WriteLine($"Adding commands to {guild.Id}...");
            }
            return Task.CompletedTask;
        }


        // this method handles the ServiceCollection creation/configuration, and builds out the service provider we can call on later
        private ServiceProvider ConfigureServices()
        {
            // this returns a ServiceProvider that is used later to call for those services
            // we can add types we have access to here, hence adding the new using statement:
            // using csharpi.Services;
            return new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandHandler>()
                .BuildServiceProvider();
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
