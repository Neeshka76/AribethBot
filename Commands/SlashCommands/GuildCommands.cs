using System.Text;
using AribethBot.Database;
using AribethBot.Helpers;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AribethBot;

[Group("guild", "Commands related to guilds/servers")]
public class GuildCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseContext db;
    private readonly ILogger<GuildCommands> logger;
    private readonly IServiceProvider services;
    
    public GuildCommands(DatabaseContext db, ILogger<GuildCommands> logger, IServiceProvider services)
    {
        this.db = db;
        this.logger = logger;
        this.services = services;
    }
    
    [SlashCommand("setspamaction", "Set the spam action for this guild")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetSpamAction(
        [Summary("type", "classic or bot")] SpamType type,
        [Summary("action", "timeout, kick, ban")]
        SpamAction actionType,
        [Summary("deleteMessages", "delete messages?")]
        bool deleteMessages = false,
        [Summary("duration", "duration for timeout in minutes")]
        int? duration = 10)
    {
        ulong guildId = Context.Guild.Id;
        SpamTrigger? trigger = await db.SpamTriggers.FindAsync(guildId, type);
        
        // Capture old values
        string oldAction = trigger?.ActionType.ToString() ?? "not set";
        string oldDuration = trigger?.ActionDuration > 0 ? $"{trigger.ActionDuration} min" : "N/A";
        string oldDelete = trigger?.ActionDelete == true ? "Yes" : "No";
        
        if (trigger == null)
        {
            trigger = new SpamTrigger
            {
                GuildId = guildId,
                Type = type
            };
            await db.SpamTriggers.AddAsync(trigger);
        }
        
        trigger.ActionType = actionType;
        trigger.ActionDuration = duration;
        trigger.ActionDelete = deleteMessages;
        
        await db.SaveChangesAsync();
        
        // New values
        string newAction = trigger.ActionType.ToString();
        string newDuration = trigger.ActionType == SpamAction.Timeout ? $"{trigger.ActionDuration} min" : "N/A";
        string newDelete = trigger.ActionDelete ? "Yes" : "No";
        
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Spam Action Updated ({type})")
            .WithColor(Color.Blue)
            .AddField("Old Values", $"**Action:** {oldAction}\n**Duration:** {oldDuration}\n**Delete Msgs:** {oldDelete}", true)
            .AddField("New Values", $"**Action:** {newAction}\n**Duration:** {newDuration}\n**Delete Msgs:** {newDelete}", true)
            .WithCurrentTimestamp();
        
        await RespondAsync(embed: embed.Build());
    }
    
    [SlashCommand("setspamtrigger", "Set spam trigger threshold for this guild")]
    public async Task SetSpamTrigger(
        [Summary("type", "classic or bot")] SpamType type,
        [Summary("messages", "Number of messages to trigger spam")]
        int nbMessages,
        [Summary("interval", "Time interval in seconds to count messages")]
        double intervalSeconds)
    {
        ulong guildId = Context.Guild.Id;
        
        SpamTrigger? trigger = await db.SpamTriggers.FindAsync(guildId, type);
        
        if (trigger == null)
        {
            trigger = new SpamTrigger
            {
                GuildId = guildId,
                Type = type,
                ActionType = type == SpamType.Bot ? SpamAction.Ban : SpamAction.Timeout,
                ActionDuration = type == SpamType.Classic ? 10 : null, // default
                ActionDelete = false
            };
            await db.SpamTriggers.AddAsync(trigger);
        }
        
        // Save old values
        int oldNb = trigger.NbMessages;
        double oldInterval = trigger.IntervalTime;
        
        // Update values
        trigger.NbMessages = nbMessages;
        trigger.IntervalTime = intervalSeconds;
        
        await db.SaveChangesAsync();
        
        // Reply embed
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Spam Trigger Updated ({type})")
            .WithColor(Color.Blue)
            .AddField("Old Values", $"**Messages:** {oldNb}\n**Interval:** {oldInterval:F1} sec", true)
            .AddField("New Values", $"**Messages:** {trigger.NbMessages}\n**Interval:** {trigger.IntervalTime:F1} sec", true)
            .WithCurrentTimestamp();
        
        await RespondAsync(embed: embed.Build());
    }
    
    public enum LogChannelType
    {
        Deleted,
        Edited,
        EntryOut,
        Ban,
        Voice
    }
    
    [RequireUserPermission(GuildPermission.Administrator)]
    [SlashCommand("setlogchannel", "Set a log channel for this guild")]
    public async Task SetLogChannel(
        [Summary("type", "The type of log")] LogChannelType type,
        [Summary("channel", "The channel (pick from menu, or leave empty to disable)")]
        SocketTextChannel? channel = null)
    {
        ulong guildId = Context.Guild.Id;
        Guild? dbGuild = await db.Guilds.FindAsync(guildId);
        
        if (dbGuild == null)
        {
            // Ensure the guild exists in DB
            dbGuild = new Guild { GuildId = guildId };
            await db.Guilds.AddAsync(dbGuild);
            await db.SaveChangesAsync();
        }
        
        switch (type)
        {
            case LogChannelType.Deleted:
                dbGuild.ChannelDeletedLog = channel?.Id;
                break;
            case LogChannelType.Edited:
                dbGuild.ChannelEditedLog = channel?.Id;
                break;
            case LogChannelType.EntryOut:
                dbGuild.ChannelEntryOutLog = channel?.Id;
                break;
            case LogChannelType.Ban:
                dbGuild.ChannelBanLog = channel?.Id;
                break;
            case LogChannelType.Voice:
                dbGuild.ChannelVoiceActivityLog = channel?.Id;
                break;
        }
        
        await db.SaveChangesAsync();
        // Refresh cache in ServerLogger
        ServerLogger loggerService = services.GetRequiredService<ServerLogger>();
        await loggerService.RefreshGuildCacheAsync(dbGuild);
        string response = channel == null
            ? $"Disabled `{type}` logging for this guild."
            : $"Updated `{type}` log channel to {channel.Mention} for this guild.";
        await RespondAsync(response);
    }
    
    [SlashCommand("serverstats", "Send stats about the server")]
    public async Task ServerStats(
        [Summary("ephemeral", "Set to false to make the message visible to everyone (default is ephemeral at true)")]
        bool ephemeral = true)
    {
        await DeferAsync(ephemeral);
        List<Embed> pages = await GuildHelper.BuildServerStatsEmbeds(Context.Guild);
        await ButtonPaginator.SendPaginatedEmbedsAsync(Context, pages, $"Server Stats for {Context.Guild.Name}", ephemeral);
    }
    
    
    [SlashCommand("userinfos", "Send infos about the user")]
    public async Task UserInfos(
        [Summary("User", "User to ping for the command")]
        SocketGuildUser? user = null,
        [Summary("ephemeral", "Set to false to make the message visible to everyone (default is ephemeral at true)")]
        bool ephemeral = true)
    {
        await DeferAsync(ephemeral: ephemeral);
        user ??= Context.User as SocketGuildUser;
        if (user != null)
        {
            List<Embed> pages = await GuildHelper.BuildUserInfoEmbeds(user, Context.Guild);
            // --- Send paginated embeds ---
            await ButtonPaginator.SendPaginatedEmbedsAsync(Context, pages, $"User Stats: {user.Mention}", ephemeral);
        }
    }
}