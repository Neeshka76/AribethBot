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

        public static void Main(string[] args)
        {
            if (args.Count() != 0)
            {
                logLevel = args[0];
            }

            Log.Logger = new LoggerConfiguration()
                 .WriteTo.File("Logs/AribethLog.log", rollingInterval: RollingInterval.Day)
                 .WriteTo.Console()
                 .CreateLogger();
            new EntryPoint().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
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
            using ServiceProvider services = ConfigureServices();
            // get the client and assign to client 
            // you get the services via GetRequiredService<T>
            socketClient = services.GetRequiredService<DiscordSocketClient>();
            services.GetRequiredService<LoggingService>();
            services.GetRequiredService<DiscordLogger>();
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
                .AddSingleton<LoggingService>()
                .AddSingleton<DiscordLogger>()
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
