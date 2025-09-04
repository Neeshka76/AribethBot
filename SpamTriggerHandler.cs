using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AribethBot.Database;

namespace AribethBot;

public class SpamTriggerHandler
{
    private readonly DiscordSocketClient socketClient;
    private readonly DatabaseContext db;
    private readonly ILogger logger;

    private readonly Dictionary<ulong, UserMessageInfo> userMessages = new();

    public SpamTriggerHandler(IServiceProvider services)
    {
        socketClient = services.GetRequiredService<DiscordSocketClient>();
        db = services.GetRequiredService<DatabaseContext>();
        logger = services.GetRequiredService<ILogger<SpamTriggerHandler>>();

        socketClient.MessageReceived += SocketClientOnMessageReceived;
    }

    private async Task SocketClientOnMessageReceived(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message || message.Source != MessageSource.User)
            return;

        if (rawMessage.Channel is SocketGuildChannel guildChannel)
        {
            logger.LogInformation(
                $"Message written by {message.Author} ({message.Author.Id}) in {rawMessage.Channel.Name} " +
                $"(Guild: {guildChannel.Guild.Name} [{guildChannel.Guild.Id}]) at {rawMessage.Timestamp.LocalDateTime:dddd dd MMMM yyyy HH:mm:ss:fff}."
            );

            UserMessageInfo userInfo = userMessages.GetValueOrDefault(message.Author.Id) ?? new UserMessageInfo();
            userMessages[message.Author.Id] = userInfo;

            // Load guild-specific spam triggers
            List<SpamTrigger> triggers = db.SpamTriggers
                .Where(t => t.GuildId == guildChannel.Guild.Id)
                .ToList();

            foreach (SpamTrigger trigger in triggers)
            {
                switch (trigger.Type)
                {
                    case SpamType.Classic:
                        await ProcessSpamCheck(message, userInfo.ClassicMessages, trigger);
                        break;
                    case SpamType.Bot:
                        await ProcessSpamCheck(message, userInfo.BotMessages, trigger);
                        break;
                }
            }
        }
        else
        {
            logger.LogInformation(
                $"Message written by {message.Author} ({message.Author.Id}) in {rawMessage.Channel.Name} (DM / Group Chat) at {rawMessage.Timestamp.LocalDateTime:dddd dd MMMM yyyy HH:mm:ss:fff}."
            );
        }
    }

    private async Task ProcessSpamCheck(SocketUserMessage message, MessageTracker tracker, SpamTrigger trigger)
    {
        tracker.AddMessage(message); // store actual message
        tracker.Cleanup(trigger.IntervalTime);

        bool spamDetected = trigger.Type == SpamType.Bot
            ? tracker.ActiveChannelCount >= trigger.NbMessages
            : tracker.TotalMessages >= trigger.NbMessages;

        if (!spamDetected) return;
        if (message.Channel is not SocketGuildChannel guildChannel) return;
        SocketGuildUser user = guildChannel.Guild.GetUser(message.Author.Id);
        if (user != null)
            await ApplyActionAsync(user, guildChannel.Guild, guildChannel, trigger, tracker);

        tracker.Clear();
    }

    private async Task ApplyActionAsync(SocketGuildUser user, SocketGuild guild, SocketGuildChannel channel, SpamTrigger trigger, MessageTracker tracker)
    {
        int duration = trigger.ActionDuration ?? 10;
        int deletedCount = 0;

        if (trigger.ActionDelete && tracker != null)
        {
            List<SocketUserMessage> messagesToDelete = tracker.GetMessagesForSpam()
                .Where(m => m.Author.Id == user.Id)
                .ToList();

            foreach (SocketUserMessage msg in messagesToDelete)
            {
                await msg.DeleteAsync();
                deletedCount++;
            }
        }

        string deleteText = trigger.ActionDelete ? $" and deleted {deletedCount} message(s)" : "";

        switch (trigger.ActionType)
        {
            case SpamAction.Ban:
                await user.BanAsync(1, $"Aribeth smited the spammer{(trigger.ActionDelete ? " and healed the guild" : "")}");
                logger.LogInformation($"User {user.Username} ({user.Id}) banned from {guild.Name} ({guild.Id}) for spamming{deleteText}.");
                break;

            case SpamAction.Kick:
                await user.KickAsync($"Aribeth blessed the guild and removed the spammer{(trigger.ActionDelete ? " and healed the guild" : "")}");
                logger.LogInformation($"User {user.Username} ({user.Id}) kicked from {guild.Name} ({guild.Id}) for spamming{deleteText}.");
                break;

            case SpamAction.Timeout:
                await user.SetTimeOutAsync(TimeSpan.FromMinutes(duration),
                    new RequestOptions { AuditLogReason = $"Aribeth protected{(trigger.ActionDelete ? " and healed the guild" : " the guild")}" });
                logger.LogInformation($"User {user.Username} ({user.Id}) timed out in {guild.Name} ({guild.Id}) for spamming{deleteText}.");
                break;

            case SpamAction.NoAction:
                logger.LogInformation($"Aribeth saw a troublemaker {user.Username} ({user.Id}) in {guild.Name}, but she'll allow it.");
                break;

            default:
                logger.LogWarning($"Aribeth is confused and doesn't know how to act '{trigger.ActionType}' for user {user.Username} ({user.Id}) in {guild.Name} ({guild.Id}).");
                break;
        }
    }

    public class UserMessageInfo
    {
        public MessageTracker ClassicMessages { get; } = new();
        public MessageTracker BotMessages { get; } = new();

        public void Clear()
        {
            ClassicMessages.Clear();
            BotMessages.Clear();
        }
    }

    public class MessageTracker
    {
        private readonly Dictionary<ulong, List<SocketUserMessage>> messagesPerChannel = new();

        public void AddMessage(SocketUserMessage message)
        {
            if (!messagesPerChannel.ContainsKey(message.Channel.Id))
                messagesPerChannel[message.Channel.Id] = new List<SocketUserMessage>();
            messagesPerChannel[message.Channel.Id].Add(message);
        }

        public void Cleanup(double intervalSeconds)
        {
            DateTime now = DateTime.UtcNow;
            foreach (List<SocketUserMessage> list in messagesPerChannel.Values)
                list.RemoveAll(m => (now - m.Timestamp.UtcDateTime).TotalSeconds > intervalSeconds);
        }

        public List<SocketUserMessage> GetMessagesForSpam() =>
            messagesPerChannel.Values.SelectMany(l => l).ToList();

        public int TotalMessages => messagesPerChannel.Values.Sum(l => l.Count);
        public int ActiveChannelCount => messagesPerChannel.Count(kv => kv.Value.Count > 0);

        public void Clear() => messagesPerChannel.Clear();
    }
}