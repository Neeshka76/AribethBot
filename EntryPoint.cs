using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Logging;

namespace AribethBot
{
    public class EntryPoint
    {
        // setup our fields we assign later
        private IConfiguration config;
        private DiscordSocketClient socketClient;
        private DiscordSocketConfig socketConfig;
        
        private static string logLevel;

        static void Main(string[] args) => new EntryPoint().MainAsync(args.Length != 0 ? args[0] : "").GetAwaiter().GetResult();

        private async Task MainAsync(string strLoglevel)
        {
            logLevel = strLoglevel;
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("Logs/AribethLog.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();
            
            socketConfig = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.All,
                UseInteractionSnowflakeDate = false,
                LogGatewayIntentWarnings = false,
                AlwaysDownloadUsers = true,
            };
            
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            config = builder.Build();
            // call ConfigureServices to create the ServiceCollection/Provider for passing around the services
            await using ServiceProvider services = ConfigureServices();
            // get the client and assign to client 
            // you get the services via GetRequiredService<T>
            socketClient = services.GetRequiredService<DiscordSocketClient>();
            // Must call this to activate them, need a better way to handle that
            services.GetRequiredService<BotLoggingService>();
            services.GetRequiredService<ServerLogger>();
            services.GetRequiredService<SpamTriggerHandler>();
            string? token = config["DiscordToken"];

            // this is where we get the Token value from the configuration file, and start the bot
            await socketClient.LoginAsync(TokenType.Bot, token);
            await socketClient.StartAsync();

            // we get the ServiceHandler class here and call the InitializeAsync method to start things up for the ServiceHandler service
            await services.GetRequiredService<ServiceHandler>().InitializeAsync();
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
                .AddSingleton(config)
                .AddSingleton(socketConfig)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandService>()
                .AddSingleton<ServiceHandler>()
                .AddSingleton<BotLoggingService>()
                .AddSingleton<ServerLogger>()
                .AddSingleton<SpamTriggerHandler>()
                .AddLogging(configure => configure.AddSerilog());
            if (!string.IsNullOrEmpty(logLevel))
            {
                switch (logLevel.ToLower())
                {
                    case "info":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
                        break;
                    }
                    case "error":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                    }
                    case "debug":
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
                        break;
                    }
                    default:
                    {
                        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                        break;
                    }
                }
            }
            else
            {
                services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
            }
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            return serviceProvider;
        }
    }
}