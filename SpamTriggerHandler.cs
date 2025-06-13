using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AribethBot
{
    public class SpamTriggerHandler
    {
        private readonly DiscordSocketClient socketClient;
        private readonly IConfiguration config;
        private int messageLimit = 5;
        private double intervalSeconds = 2.0;
        // Tracks each user's messages
        private Dictionary<ulong, UserMessageInfo> userMessages = new Dictionary<ulong, UserMessageInfo>();

        public SpamTriggerHandler(IServiceProvider services)
        {
            socketClient = services.GetRequiredService<DiscordSocketClient>();
            config = services.GetRequiredService<IConfiguration>();
            socketClient.MessageReceived += SocketClientOnMessageReceived;
        }

        private async Task SocketClientOnMessageReceived(SocketMessage rawMessage)
        {
            // ensures we don't process system/other bot messages
            if (!(rawMessage is SocketUserMessage message))
            {
                return;
            }
            if (message.Source != MessageSource.User)
            {
                return;
            }
            ulong userId = message.Author.Id;
            DateTime now = DateTime.UtcNow;
            messageLimit = int.Parse(config["nbMessagesSpamTrigger"]);
            intervalSeconds = double.Parse(config["intervalTimeSpamTrigger"]);
            const int cooldownSeconds = 60;

            // Check if the user is in the list or no
            if (!userMessages.ContainsKey(userId))
            {
                userMessages[userId] = new UserMessageInfo();
            }
            UserMessageInfo userInfo = userMessages[userId];
            userInfo.MessageTimestamps.Add(now);
            //userInfo.DictDateTimeMessages.Add(now, message);
            userInfo.MessageTimestamps.RemoveAll(timestamp => (now - timestamp).TotalSeconds > intervalSeconds);
            if (userInfo.LastSpamDetected.HasValue && (now - userInfo.LastSpamDetected.Value).TotalSeconds < cooldownSeconds)
            {
                return; // On cooldown
            }
            // If reach the message limit, take actions !
            if (userInfo.MessageTimestamps.Count >= messageLimit)
            {
                userInfo.LastSpamDetected = now;
                SocketGuildChannel? guildChannel = message.Channel as SocketGuildChannel;
                // Ban
                await guildChannel.GetUser(userId).BanAsync(1,"Aribeth smited the spammer", RequestOptions.Default);
            }
            await Task.CompletedTask;
        }
    }

    // Supporting class
    public class UserMessageInfo
    {
        public Dictionary<DateTime, SocketUserMessage> DictDateTimeMessages = new Dictionary<DateTime, SocketUserMessage>();
        public List<DateTime> MessageTimestamps { get; set; } = new List<DateTime>();
        public DateTime? LastSpamDetected { get; set; }
    }
}