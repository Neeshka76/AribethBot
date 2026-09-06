using AribethBot.Database;
using AribethBot.Helpers;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Logging;

namespace AribethBot
{
    public class EntryPoint
    {
        // set our fields we assign later
        private IConfiguration _config;
        private DiscordSocketClient _socketClient;
        private DiscordSocketConfig _socketConfig;
        
        private static string _logLevel;
        
        static void Main(string[] args) => new EntryPoint().MainAsync(args.Length != 0 ? args[0] : "").GetAwaiter().GetResult();
        
        private async Task MainAsync(string strLoglevel)
        {
            _logLevel = strLoglevel;
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("Logs/AribethLog.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();
            
            _socketConfig = new DiscordSocketConfig
            {
                MessageCacheSize = 1000,
                GatewayIntents = GatewayIntents.All,
                UseInteractionSnowflakeDate = false,
                LogGatewayIntentWarnings = false,
                AlwaysDownloadUsers = true,
            };
            
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            _config = builder.Build();
            
            // call ConfigureServices to create the ServiceCollection/Provider for passing around the services
            await using ServiceProvider services = ConfigureServices();
            
            // get the client and assign to client 
            // you get the services via GetRequiredService<T>
            _socketClient = services.GetRequiredService<DiscordSocketClient>();
            
            string? token = _config["DiscordToken"];
            
            // Must call this to activate them, need a better way to handle that
            services.GetRequiredService<BotLoggingService>();
            services.GetRequiredService<ServerLogger>();
            services.GetRequiredService<SpamTriggerHandler>();
            services.GetRequiredService<ReactionHandler>();
            services.GetRequiredService<WarningHandlers>();
            await services.GetRequiredService<ServiceHandler>().InitializeAsync();
            
            // this is where we get the Token value from the configuration file, and start the bot
            await _socketClient.LoginAsync(TokenType.Bot, token);
            await _socketClient.StartAsync();
            
            // we get the ServiceHandler class here and call the InitializeAsync method to start things up for the ServiceHandler service
            await Task.Delay(-1);
        }
        
        // this method handles the ServiceCollection creation/configuration, and builds out the service provider we can call on later
        private ServiceProvider ConfigureServices()
        {
            // this returns a ServiceProvider that is used later to call for those services
            // we can add types we have access to here, hence adding the new using statement:
            // using csharpi.Services;
            // the config we build is also added, which comes in handy for setting the command prefix!
            IServiceCollection services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton(_socketConfig)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandService>()
                .AddSingleton<ServiceHandler>()
                .AddSingleton<BotLoggingService>()
                .AddSingleton<ServerLogger>()
                .AddSingleton<ReactionHandler>()
                .AddSingleton<SpamTriggerHandler>()
                .AddSingleton<WarningHandlers>()
                .AddDbContext<DatabaseContext>() // SQLite DB file;
                .AddLogging(configure => configure.AddSerilog()); // Logger
            
            // Register ResourceService (pointing to resources.json)
            string resourcePath = Path.Combine(AppContext.BaseDirectory, "resources.json");
            services.AddSingleton(new ResourceService(resourcePath));
            
            LogLevel minLevel = _logLevel?.ToLower() switch
            {
                "info" => LogLevel.Information,
                "debug" => LogLevel.Debug,
                "error" => LogLevel.Error,
                _ => LogLevel.Error
            };
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = minLevel);
            return services.BuildServiceProvider();
        }
    }
}