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
            if (rawMessage is not SocketUserMessage message || message.Source != MessageSource.User) return;

            // Load limits from config
            int classicLimit = int.Parse(config["nbMessagesSpamTriggerClassic"]);
            double classicInterval = double.Parse(config["intervalTimeSpamTriggerClassic"]);
            int botLimit = int.Parse(config["nbMessagesSpamTriggerBot"]);
            double botInterval = double.Parse(config["intervalTimeSpamTriggerBot"]);

            logger.LogInformation(
                $"Message written by {rawMessage.Author} ({rawMessage.Author.Id}) in {rawMessage.Channel.Name} at {rawMessage.Timestamp.LocalDateTime:dddd dd MMMM yyyy HH:mm:ss:fff}.");

            UserMessageInfo userInfo = userMessages.GetValueOrDefault(message.Author.Id) ?? new UserMessageInfo();
            userMessages[message.Author.Id] = userInfo;

            // Process both classic and bot spam
            await ProcessSpamCheck(message, userInfo,
                (userInfo.ClassicMessages, classicLimit, classicInterval, false),
                (userInfo.BotMessages, botLimit, botInterval, true));
        }

        private async Task ProcessSpamCheck(SocketMessage message, UserMessageInfo userInfo,
            params (MessageTracker tracker, int limit, double interval, bool isBot)[] checks)
        {
            DateTime now = DateTime.UtcNow;

            foreach ((MessageTracker tracker, int limit, double interval, bool isBot) in checks)
            {
                tracker.AddMessage(message.Channel.Id, now);
                tracker.Cleanup(now, interval);

                bool spamDetected = isBot
                    ? tracker.ActiveChannelCount >= limit // classic = messages in same channel
                    : tracker.TotalMessages >= limit; // bot = messages across multiple channels

                if (!spamDetected) continue;

                if (message.Channel is SocketGuildChannel guildChannel)
                {
                    await guildChannel.GetUser(message.Author.Id)
                        .BanAsync(1, "Aribeth smited the spammer", RequestOptions.Default);
                    logger.LogInformation($"User {message.Author} ({message.Author.Id}) banned for spamming.");
                }

                userInfo.Clear();
                break;
            }
        }

        public class UserMessageInfo
        {
            public MessageTracker ClassicMessages { get; private set; } = new();
            public MessageTracker BotMessages { get; private set; } = new();

            public void Clear()
            {
                ClassicMessages.Clear();
                BotMessages.Clear();
            }
        }

        public class MessageTracker
        {
            private readonly Dictionary<ulong, List<DateTime>> messagesPerChannel = new();

            public void AddMessage(ulong channelId, DateTime timestamp)
            {
                if (!messagesPerChannel.ContainsKey(channelId))
                    messagesPerChannel[channelId] = new List<DateTime>();
                messagesPerChannel[channelId].Add(timestamp);
            }

            public void Cleanup(DateTime now, double interval)
            {
                foreach (var list in messagesPerChannel.Values)
                    list.RemoveAll(t => (now - t).TotalSeconds > interval);
            }

            public int TotalMessages => messagesPerChannel.Values.Sum(list => list.Count);
            public int ActiveChannelCount => messagesPerChannel.Count(kv => kv.Value.Count > 0);

            public void Clear() => messagesPerChannel.Clear();
        }
    }
}