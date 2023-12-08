using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Interactions;
using Newtonsoft.Json;

namespace AribethBot
{
    public class EntryPoint
    {
        private DiscordSocketClient client;
        private DiscordSocketConfig config;
        private CommandHandler handler;
        //private ulong guildId;

        public static void Main(string[] args) => new EntryPoint().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // When working with events that have Cacheable<IMessage, ulong> parameters,
            // you must enable the message cache in your config settings if you plan to
            // use the cached message entity. 
            config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessages
            };
            //guildId = ulong.Parse(Environment.GetEnvironmentVariable("GuildIdNeeshkaModdingServer"));
            client = new DiscordSocketClient();
            client.Log += Log;
            string? token = Environment.GetEnvironmentVariable("DiscordToken");
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // Let's hook the ready event for creating our commands in.
            client.Ready += Client_Ready;
            client.SlashCommandExecuted += SlashCommandHandler;
            client.MessageUpdated += MessageUpdated;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            // If the message was not in the cache, downloading it will result in getting a copy of `after`.
            var message = await before.GetOrDownloadAsync();
            Console.WriteLine($"{message} -> {after}");
            await Task.Delay(-1);
        }

        public async Task Client_Ready()
        {
            handler = new CommandHandler(client, config);
            // Let's build a guild command! We're going to need a guild so lets just put that in a variable.
            SocketGuild guild = client.GetGuild(guildId);
            
            // Next, lets create our slash command builder. This is like the embed builder but for slash commands.
            SlashCommandBuilder guildCommand = new SlashCommandBuilder();
            
            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            guildCommand.WithName("first-command");
            
            // Descriptions can have a max length of 100.
            guildCommand.WithDescription("This is my first guild slash command!");
            
            // Let's do our global command
            SlashCommandBuilder globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("first-global-command");
            globalCommand.WithDescription("This is my first global slash command");
            
            try
            {
                // Now that we have our builder, we can call the CreateApplicationCommandAsync method to make our slash command.
                await guild.CreateApplicationCommandAsync(guildCommand.Build());
            
                // With global commands we don't need the guild.
                await client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the /exception/ to get a visual of where your rroris.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            
                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            await command.RespondAsync($"You executed {command.Data.Name}");
        }
    }
}
