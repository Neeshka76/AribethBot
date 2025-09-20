using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AribethBot.Database;
using Microsoft.EntityFrameworkCore;

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

            // 🚀 Offload to background so we don't block gateway
            _ = Task.Run(async () =>
            {
                try
                {
                    // Load guild-specific spam triggers asynchronously
                    List<SpamTrigger> triggers = await db.SpamTriggers
                        .Where(t => t.GuildId == guildChannel.Guild.Id)
                        .ToListAsync();

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
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error while processing spam triggers for {guildChannel.Guild.Name} ({guildChannel.Guild.Id})");
                }
            });
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

        // ignore nonsensical thresholds
        if (trigger.NbMessages <= 1)
            return;

        bool spamDetected = trigger.Type == SpamType.Bot
            ? tracker.ActiveChannelCount >= trigger.NbMessages
            : tracker.TotalMessages >= trigger.NbMessages;

        if (!spamDetected) return;
        if (message.Channel is not SocketGuildChannel guildChannel) return;

        SocketGuild guild = guildChannel.Guild;
        SocketGuildUser user = guild.GetUser(message.Author.Id);
        if (user == null) return;

        // Do the moderation first
        await ApplyActionAsync(user, trigger);

        int deletedCount = 0;
        if (trigger.ActionDelete)
        {
            // Small grace period for "just sent" messages
            await Task.Delay(1000);
            deletedCount = await DeleteTrackedMessagesAsync(tracker, user);
        }

        // Logging (after both action + optional deletes)
        LogAction(user, guild, trigger, deletedCount);

        tracker.Clear();
    }

    private async Task ApplyActionAsync(SocketGuildUser user, SpamTrigger trigger)
    {
        int duration = trigger.ActionDuration ?? 10;

        switch (trigger.ActionType)
        {
            case SpamAction.Ban:
                await user.BanAsync(1, $"Aribeth smited the spammer");
                break;

            case SpamAction.Kick:
                await user.KickAsync($"Aribeth blessed the guild and removed the spammer");
                break;

            case SpamAction.Timeout:
                await user.SetTimeOutAsync(
                    TimeSpan.FromMinutes(duration),
                    new RequestOptions { AuditLogReason = $"Aribeth protected the guild" }
                );
                break;

            case SpamAction.NoAction:
                break;
        }
    }

    private async Task<int> DeleteTrackedMessagesAsync(MessageTracker tracker, SocketGuildUser user)
    {
        List<SocketUserMessage> messagesToDelete = tracker.GetMessagesForSpam()
            .Where(m => m.Author.Id == user.Id)
            .ToList();

        if (!messagesToDelete.Any())
            return 0;


        int deletedCount = 0;
        // Group messages by channel (BulkDelete works per-channel)
        IEnumerable<IGrouping<ulong, SocketUserMessage>> channelGroups = messagesToDelete.GroupBy(m => m.Channel.Id);
        foreach (IGrouping<ulong, SocketUserMessage> group in channelGroups)
        {
            if (socketClient.GetChannel(group.Key) is not ITextChannel textChannel)
                continue;

            // Only recent (< 14 days) can be bulk-deleted
            List<SocketUserMessage> recentMessages = group
                .Where(m => (DateTimeOffset.UtcNow - m.Timestamp).TotalDays < 14)
                .ToList();

            if (recentMessages.Count > 1)
            {
                try
                {
                    await textChannel.DeleteMessagesAsync(recentMessages);
                    deletedCount += recentMessages.Count;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Bulk delete failed in {textChannel.Name} ({textChannel.Id})");
                    // fallback to individual deletes
                    foreach (SocketUserMessage msg in recentMessages)
                    {
                        try
                        {
                            await msg.DeleteAsync();
                            deletedCount++;
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }
                }
            }
            else
            {
                // Just 1 message or too old → delete individually
                foreach (SocketUserMessage msg in group)
                {
                    try
                    {
                        await msg.DeleteAsync();
                        deletedCount++;
                    }
                    catch
                    {
                        /* ignore */
                    }
                }
            }
        }

        return deletedCount;
    }

    private void LogAction(SocketGuildUser user, SocketGuild guild, SpamTrigger trigger, int deletedCount)
    {
        string deleteText = trigger.ActionDelete && trigger.ActionType != SpamAction.NoAction ? $" and deleted {deletedCount} message(s)" : "";

        switch (trigger.ActionType)
        {
            case SpamAction.Ban:
                logger.LogInformation($"User {user.Username} ({user.Id}) banned from {guild.Name} ({guild.Id}) for spamming{deleteText}.");
                break;

            case SpamAction.Kick:
                logger.LogInformation($"User {user.Username} ({user.Id}) kicked from {guild.Name} ({guild.Id}) for spamming{deleteText}.");
                break;

            case SpamAction.Timeout:
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