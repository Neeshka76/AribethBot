using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AribethBot
{
    public class SpamTriggerHandler
    {
        private readonly DiscordSocketClient socketClient;

        private readonly IConfiguration config;

        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private readonly ILogger logger;
        private int botMessageLimit = 5;
        private double botLapsTimeLimit = 2.0;

        private int classicMessageLimit = 5;
        private double classicLapsTimeLimit = 2.0;

        // Tracks each user's messages
        private Dictionary<ulong, UserMessageInfo> userMessages = new Dictionary<ulong, UserMessageInfo>();

        public SpamTriggerHandler(IServiceProvider services)
        {
            socketClient = services.GetRequiredService<DiscordSocketClient>();
            config = services.GetRequiredService<IConfiguration>();
            logger = services.GetRequiredService<ILogger<SpamTriggerHandler>>();
            socketClient.MessageReceived += SocketClientOnMessageReceived;
        }

        private async Task SocketClientOnMessageReceived(SocketMessage rawMessage)
        {
            // ensures we don't process system/other bot messages
            if (!(rawMessage is SocketUserMessage message) || message.Source != MessageSource.User) return;

            // Load limits from config
            int classicLimit = int.Parse(config["nbMessagesSpamTriggerClassic"]);
            double classicInterval = double.Parse(config["intervalTimeSpamTriggerClassic"]);
            int botLimit = int.Parse(config["nbMessagesSpamTriggerBot"]);
            double botInterval = double.Parse(config["intervalTimeSpamTriggerBot"]);

            logger.LogInformation(
                $"Message by {message.Author} ({message.Author.Id}) in {message.Channel.Name} at {message.Timestamp.LocalDateTime}");

            UserMessageInfo userInfo = userMessages.GetValueOrDefault(message.Author.Id) ?? new UserMessageInfo();
            userMessages[message.Author.Id] = userInfo;

            // Process both classic and bot spam
            await ProcessSpamCheck(message, userInfo,
                (userInfo.ClassicMessageTimestamps, classicLimit, classicInterval, true),
                (userInfo.MessageTimestamps, botLimit, botInterval, false));
        }

        private async Task ProcessSpamCheck(SocketMessage message, UserMessageInfo userInfo,
            params (List<DateTime> timestamps, int limit, double interval, bool isClassic)[] checks)
        {
            DateTime now = DateTime.UtcNow;

            foreach ((List<DateTime> timestamps, int limit, double interval, bool isClassic) in checks)
            {
                timestamps.Add(now);
                if (message.Channel is SocketTextChannel channel)
                    userInfo.ChannelsIdMessages.Add(channel);

                // Remove the messages if the time up compared of the actual time
                timestamps.RemoveAll(t => (now - t).TotalSeconds > interval);

                // If limit has been reached or has duplicates for the channels in bot mode, abort
                if (timestamps.Count < limit) continue;
                if (!isClassic && HasDuplicates(userInfo.ChannelsIdMessages))
                    continue;
                // Ban !
                if (message.Channel is SocketGuildChannel guildChannel)
                {
                    await guildChannel.GetUser(message.Author.Id).BanAsync(1, "Aribeth smited the spammer", RequestOptions.Default);
                    logger.LogInformation($"User {message.Author.Id} banned for spamming.");
                }

                // Clear all tracked messages
                userInfo.Clear();
                break; // No need to check further after ban
            }
        }

        private static bool HasDuplicates<T>(List<T> list)
        {
            HashSet<T> seen = new HashSet<T>();
            foreach (T item in list)
            {
                if (!seen.Add(item))
                    return true;
            }

            return false;
        }
    }


    public class UserMessageInfo
    {
        public List<SocketGuildChannel?> ChannelsIdMessages { get; private set; } = new();
        public List<DateTime> ClassicMessageTimestamps { get; private set; } = new();
        public List<DateTime> MessageTimestamps { get; private set; } = new();

        public void Clear()
        {
            ChannelsIdMessages.Clear();
            ClassicMessageTimestamps.Clear();
            MessageTimestamps.Clear();
        }
    }
}