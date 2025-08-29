using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AribethBot;

public class ServerLogger
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private readonly DiscordSocketClient socketClient;
    private readonly IConfiguration config;
    private readonly ILogger logger;

    public ServerLogger(IServiceProvider services)
    {
        socketClient = services.GetRequiredService<DiscordSocketClient>();
        config = services.GetRequiredService<IConfiguration>();
        logger = services.GetRequiredService<ILogger<ServerLogger>>();

        // Subscribe events
        socketClient.MessageDeleted += OnMessageDeleted;
        socketClient.MessageUpdated += OnMessageUpdated;
        socketClient.UserBanned += OnUserBanned;
        socketClient.UserUnbanned += OnUserUnbanned;
        socketClient.UserJoined += OnUserJoined;
        socketClient.UserLeft += OnUserLeft;
        socketClient.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
    }

    private async Task EnsureGuildConfigExistsAsync(ulong guildId)
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        // Load and parse the JSON
        JObject jsonObj = JObject.Parse(await File.ReadAllTextAsync(filePath));

        string guildIdStr = guildId.ToString();

        // Ensure "guilds" section exists
        jsonObj["guilds"] ??= new JObject();

        // Ensure this guild section exists
        if (jsonObj["guilds"][guildIdStr] == null)
        {
            jsonObj["guilds"][guildIdStr] = JObject.FromObject(new
            {
                channelDeletedLog = "0",
                channelEditedLog = "0",
                channelEntryOutLog = "0",
                channelBanLog = "0",
                channelVoiceActivityLog = "0"
            });

            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
            logger.LogInformation($"Created default config for guild {socketClient.GetGuild(guildId)?.Name} ({guildId})");
        }
    }

    private async Task<IMessageChannel?> GetLogChannel(ulong guildId, string key)
    {
        // Ensure guild config exists
        await EnsureGuildConfigExistsAsync(guildId);

        // Load the config JSON
        string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        JObject jsonObj = JObject.Parse(await File.ReadAllTextAsync(filePath));

        string guildIdStr = guildId.ToString();
        string? channelIdStr = (string?)jsonObj["guilds"]?[guildIdStr]?[key];

        if (string.IsNullOrEmpty(channelIdStr) || channelIdStr == "0")
        {
            //logger.LogWarning($"No log channel set for {socketClient.GetGuild(guildId)?.Name} ({guildId}) key '{key}'");
            return null;
        }

        if (!ulong.TryParse(channelIdStr, out ulong channelId))
        {
            logger.LogWarning($"Invalid channel ID '{channelIdStr}' for guild {socketClient.GetGuild(guildId)?.Name} ({guildId}), key '{key}'");
            return null;
        }

        IMessageChannel? logChannel = socketClient.GetChannel(channelId) as IMessageChannel;
        if (logChannel == null)
            logger.LogWarning($"Could not resolve channel {channelId} for guild {socketClient.GetGuild(guildId)?.Name} ({guildId}) (key '{key}')");

        return logChannel;
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
        {
            await ResendAttachmentsAsync(userMessage, embed, logChannel);
        }
        else
        {
            await logChannel.SendMessageAsync(embed: embed.Build());
        }
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
        ulong guildId = user.Guild.Id;
        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelEntryOutLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl())
            .WithTitle("Member joined")
            .WithDescription($"{user.Mention} {user.Guild.MemberCount}th to join\nCreated at {user.CreatedAt} ({GetTimeDifference(user.CreatedAt)})")
            .WithColor(Color.Green)
            .WithCurrentTimestamp();

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        ulong guildId = guild.Id;
        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelEntryOutLog");
        if (logChannel == null) return;

        SocketGuildUser? guildUser = guild.GetUser(user.Id);
        string roles = guildUser != null
            ? string.Join("; ", guildUser.Roles.Where(r => !r.IsEveryone).Select(r => r.Mention))
            : "Unknown";
        string joinedAt = guildUser?.JoinedAt?.ToString("g") ?? "Unknown";
        string timeDiff = guildUser?.JoinedAt != null
            ? GetTimeDifference(guildUser.JoinedAt)
            : "Unknown";
        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .WithTitle("Member left")
            .WithDescription($"{user.Mention} joined at {joinedAt} ({timeDiff})\n**Roles:** {roles}")
            .WithColor(Color.Red)
            .WithCurrentTimestamp();
        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserBanned(SocketUser user, SocketGuild guild)
    {
        await Task.Delay(200); // allow audit log to populate
        (IUser? moderator, IAuditLogEntry? entry) = await GetAuditLogResponsible(guild, user.Id, ActionType.Ban);
        ulong guildId = guild.Id;
        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelBanLog");
        if (logChannel == null) return;

        RestBan? ban = await guild.GetBanAsync(user.Id);

        EmbedBuilder? embed = new EmbedBuilder()
            .WithAuthor(user.Username, user.GetAvatarUrl())
            .WithTitle("Ban")
            .WithDescription($"**Offender:** {user.Username} {user.Mention}\n**Reason:** {ban.Reason}")
            .WithColor(Color.Red)
            .WithCurrentTimestamp();

        if (moderator != null)
            embed.Description += $"\n**Responsible moderator:** {moderator.Username} {moderator.Mention}";

        await logChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
    {
        await Task.Delay(200);
        (IUser? moderator, IAuditLogEntry entry) = await GetAuditLogResponsible(guild, user.Id, ActionType.Unban);
        ulong guildId = guild.Id;
        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelBanLog");
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
        ulong guildId = (user as SocketGuildUser)?.Guild.Id ?? 0;
        if (guildId == 0) return;

        IMessageChannel? logChannel = await GetLogChannel(guildId, "channelVoiceActivityLog");
        if (logChannel == null) return;

        EmbedBuilder? embed = new EmbedBuilder().WithAuthor(user.Username, user.GetAvatarUrl()).WithCurrentTimestamp();

        if (before.VoiceChannel == null && after.VoiceChannel != null)
        {
            embed.Title = "Member joined voice channel";
            embed.Description = $"{user.Mention} joined {after.VoiceChannel.Mention}";
            embed.Color = Color.Blue;
            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        else if (before.VoiceChannel != null && after.VoiceChannel == null)
        {
            embed.Title = "Member left voice channel";
            embed.Description = $"{user.Mention} left {before.VoiceChannel.Mention}";
            embed.Color = Color.Red;
            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        else if (before.VoiceChannel != null && after.VoiceChannel != null)
        {
            if (before.VoiceChannel != after.VoiceChannel)
            {
                embed.Title = "Member left voice channel";
                embed.Description = $"{user.Mention} left {before.VoiceChannel.Mention}";
                embed.Color = Color.Red;
                await logChannel.SendMessageAsync(embed: embed.Build());

                embed.Title = "Member joined voice channel";
                embed.Description = $"{user.Mention} joined {after.VoiceChannel.Mention}";
                embed.Color = Color.Blue;
                await logChannel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                if (!before.IsStreaming && after.IsStreaming)
                {
                    embed.Title = "Member started streaming";
                    embed.Description = $"{user.Mention} started streaming in {after.VoiceChannel.Mention}";
                    embed.Color = Color.Purple;
                    await logChannel.SendMessageAsync(embed: embed.Build());
                }
                else if (before.IsStreaming && !after.IsStreaming)
                {
                    embed.Title = "Member stopped streaming";
                    embed.Description = $"{user.Mention} stopped streaming in {after.VoiceChannel.Mention}";
                    embed.Color = Color.DarkPurple;
                    await logChannel.SendMessageAsync(embed: embed.Build());
                }
            }
        }
    }

    private async Task ResendAttachmentsAsync(IUserMessage message, EmbedBuilder embed, IMessageChannel channel)
    {
        using HttpClient client = new HttpClient();
        foreach (IAttachment? att in message.Attachments)
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
        IUser responsible = null;
        IAuditLogEntry foundEntry = null;

        await foreach (IReadOnlyCollection<RestAuditLogEntry>? batch in guild.GetAuditLogsAsync(10, actionType: action))
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