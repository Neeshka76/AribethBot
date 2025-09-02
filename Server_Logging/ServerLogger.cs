using AribethBot.Database;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AribethBot;

public class ServerLogger
{
    private readonly DiscordSocketClient socketClient;
    private readonly DatabaseContext db;
    private readonly ILogger<ServerLogger> logger;

    public ServerLogger(IServiceProvider services)
    {
        socketClient = services.GetRequiredService<DiscordSocketClient>();
        db = services.GetRequiredService<DatabaseContext>();
        logger = services.GetRequiredService<ILogger<ServerLogger>>();

        // Subscribe to events
        socketClient.MessageDeleted += OnMessageDeleted;
        socketClient.MessageUpdated += OnMessageUpdated;
        socketClient.UserBanned += OnUserBanned;
        socketClient.UserUnbanned += OnUserUnbanned;
        socketClient.UserJoined += OnUserJoined;
        socketClient.UserLeft += OnUserLeft;
        socketClient.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
    }

    private async Task<IMessageChannel?> GetLogChannel(ulong guildId, string key)
    {
        Guild? dbGuild = await db.Guilds.FindAsync(guildId);
        if (dbGuild == null)
        {
            logger.LogWarning($"No database entry found for guild {guildId}, cannot get log channel '{key}'");
            return null;
        }

        ulong? channelId = key switch
        {
            "channelDeletedLog" => dbGuild.ChannelDeletedLog,
            "channelEditedLog" => dbGuild.ChannelEditedLog,
            "channelEntryOutLog" => dbGuild.ChannelEntryOutLog,
            "channelBanLog" => dbGuild.ChannelBanLog,
            "channelVoiceActivityLog" => dbGuild.ChannelVoiceActivityLog,
            _ => null
        };

        if (channelId == null || channelId == 0)
            return null;

        return socketClient.GetChannel(channelId.Value) as IMessageChannel;
    }

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache)
    {
        IMessage? message = await cache.GetOrDownloadAsync();
        if (message is not IUserMessage userMessage || userMessage.Source != MessageSource.User) return;

        ulong guildId = (channelCache.Value as IGuildChannel)?.GuildId ?? 0;
        if (guildId == 0) return;

        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelDeletedLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(userMessage.Author.Username, userMessage.Author.GetAvatarUrl())
            .WithTitle($"Message deleted in <#{userMessage.Channel.Id}>")
            .WithDescription(userMessage.Content)
            .WithColor(Color.Red)
            .WithCurrentTimestamp();

        if (userMessage.Attachments.Count > 0)
            await ResendAttachmentsAsync(userMessage, embed, logChannel);
        else
            await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage updatedMessage, ISocketMessageChannel channel)
    {
        IMessage? oldMessage = await cache.GetOrDownloadAsync();
        if (oldMessage is not IUserMessage userMessage || userMessage.Source != MessageSource.User) return;
        if (updatedMessage.Content == userMessage.Content) return;

        ulong guildId = (channel as IGuildChannel)?.GuildId ?? 0;
        if (guildId == 0) return;

        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelEditedLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(userMessage.Author.Username, userMessage.Author.GetAvatarUrl())
            .WithTitle($"Message updated in <#{userMessage.Channel.Id}>")
            .WithDescription($"**Before:** {userMessage.Content}\n**After:** {updatedMessage.Content}")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserJoined(SocketGuildUser user)
    {
        IMessageChannel? logChannel = await GetLogChannel(user.Guild.Id, "channelEntryOutLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl())
            .WithTitle("Member joined")
            .WithDescription($"{user.Mention} ({user.Guild.MemberCount} members)\nCreated at {user.CreatedAt} ({GetTimeDifference(user.CreatedAt)})")
            .WithColor(Color.Green)
            .WithCurrentTimestamp();

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        IMessageChannel? logChannel = await GetLogChannel(guild.Id, "channelEntryOutLog");
        if (logChannel == null) return;

        SocketGuildUser? guildUser = guild.GetUser(user.Id);
        string roles = guildUser != null ? string.Join("; ", guildUser.Roles.Where(r => !r.IsEveryone).Select(r => r.Mention)) : "Unknown";
        string joinedAt = guildUser?.JoinedAt?.ToString("g") ?? "Unknown";

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .WithTitle("Member left")
            .WithDescription($"{user.Mention} joined at {joinedAt}\nRoles: {roles}")
            .WithColor(Color.Red)
            .WithCurrentTimestamp();

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserBanned(SocketUser user, SocketGuild guild)
    {
        await Task.Delay(200);
        (IUser? moderator, IAuditLogEntry? entry) = await GetAuditLogResponsible(guild, user.Id, ActionType.Ban);

        IMessageChannel? logChannel = await GetLogChannel(guild.Id, "channelBanLog");
        if (logChannel == null) return;

        RestBan? ban = await guild.GetBanAsync(user.Id);

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl())
            .WithTitle("Ban")
            .WithDescription($"**Offender:** {user.Username} {user.Mention}\n**Reason:** {ban?.Reason ?? "No reason"}")
            .WithColor(Color.Red)
            .WithCurrentTimestamp();

        if (moderator != null)
            embed.Description += $"\n**Responsible moderator:** {moderator.Username} {moderator.Mention}";

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
    {
        await Task.Delay(200);
        (IUser? moderator, IAuditLogEntry? entry) = await GetAuditLogResponsible(guild, user.Id, ActionType.Unban);

        IMessageChannel? logChannel = await GetLogChannel(guild.Id, "channelBanLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl())
            .WithTitle("Unban")
            .WithDescription($"**Offender:** {user.Username} {user.Mention}")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        if (moderator != null)
            embed.Description += $"\n**Responsible moderator:** {moderator.Username} {moderator.Mention}";

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser) return;
        ulong guildId = guildUser.Guild.Id;

        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelVoiceActivityLog");
        if (logChannel == null) return;

        // User joins a voice channel
        if (before.VoiceChannel == null && after.VoiceChannel != null)
        {
            EmbedBuilder? embed = new EmbedBuilder()
                .WithAuthor(user.Username, user.GetAvatarUrl())
                .WithTitle("Member joined voice channel")
                .WithDescription($"{user.Mention} joined {after.VoiceChannel.Mention}")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            await logChannel.SendMessageAsync(embed: embed.Build());
            return;
        }

        // User leaves a voice channel
        if (before.VoiceChannel != null && after.VoiceChannel == null)
        {
            EmbedBuilder? embed = new EmbedBuilder()
                .WithAuthor(user.Username, user.GetAvatarUrl())
                .WithTitle("Member left voice channel")
                .WithDescription($"{user.Mention} left {before.VoiceChannel.Mention}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp();

            await logChannel.SendMessageAsync(embed: embed.Build());
            return;
        }

        // User moves between channels
        if (before.VoiceChannel != null && after.VoiceChannel != null && before.VoiceChannel != after.VoiceChannel)
        {
            EmbedBuilder? embed = new EmbedBuilder()
                .WithAuthor(user.Username, user.GetAvatarUrl())
                .WithTitle("Member moved voice channel")
                .WithDescription($"{user.Mention} moved from {before.VoiceChannel.Mention} to {after.VoiceChannel.Mention}")
                .WithColor(Color.Orange)
                .WithCurrentTimestamp();

            await logChannel.SendMessageAsync(embed: embed.Build());
            return;
        }

        // Streaming start/stop within the same channel
        if (before.VoiceChannel == after.VoiceChannel)
        {
            if (!before.IsStreaming && after.IsStreaming)
            {
                EmbedBuilder? embed = new EmbedBuilder()
                    .WithAuthor(user.Username, user.GetAvatarUrl())
                    .WithTitle("Member started streaming")
                    .WithDescription($"{user.Mention} started streaming in {after.VoiceChannel.Mention}")
                    .WithColor(Color.Purple)
                    .WithCurrentTimestamp();

                await logChannel.SendMessageAsync(embed: embed.Build());
            }
            else if (before.IsStreaming && !after.IsStreaming)
            {
                EmbedBuilder? embed = new EmbedBuilder()
                    .WithAuthor(user.Username, user.GetAvatarUrl())
                    .WithTitle("Member stopped streaming")
                    .WithDescription($"{user.Mention} stopped streaming in {after.VoiceChannel.Mention}")
                    .WithColor(Color.DarkPurple)
                    .WithCurrentTimestamp();

                await logChannel.SendMessageAsync(embed: embed.Build());
            }
        }
    }


    private async Task ResendAttachmentsAsync(IUserMessage message, EmbedBuilder embed, IMessageChannel channel)
    {
        using HttpClient client = new HttpClient();
        foreach (IAttachment att in message.Attachments)
        {
            await using Stream stream = await client.GetStreamAsync(att.Url);
            await channel.SendFileAsync(stream, att.Filename, embed: embed.Build());
        }
    }

    private string GetTimeDifference(DateTimeOffset? date)
    {
        if (date == null) return "unknown";
        TimeSpan diff = DateTimeOffset.UtcNow - date.Value;
        return diff.Days > 0 ? $"{diff.Days} days ago" : diff.Hours > 0 ? $"{diff.Hours} hours ago" : $"{diff.Minutes} minutes ago";
    }

    private async Task<(IUser? responsible, IAuditLogEntry? foundEntry)> GetAuditLogResponsible(SocketGuild guild, ulong targetUserId, ActionType action)
    {
        IUser? responsible = null;
        IAuditLogEntry? foundEntry = null;

        await foreach (IReadOnlyCollection<RestAuditLogEntry> batch in guild.GetAuditLogsAsync(10, actionType: action))
        {
            foreach (RestAuditLogEntry entry in batch)
            {
                dynamic data = entry.Data;
                if (data?.Target?.Id != targetUserId) continue;
                foundEntry = entry;
                responsible = entry.User;
                break;
            }

            if (foundEntry != null) break;
        }

        return (responsible, foundEntry);
    }
}