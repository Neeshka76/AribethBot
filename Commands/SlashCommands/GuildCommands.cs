using System.Text;
using AribethBot.Database;
using AribethBot.Helpers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace AribethBot;
[Group("guild", "Commands related to guilds/servers")]
public class GuildCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseContext db;
    private readonly ILogger<GuildCommands> logger;
    public GuildCommands(DatabaseContext db, ILogger<GuildCommands> logger)
    {
        this.db = db;
        this.logger = logger;
    }
    
    [SlashCommand("setspamaction", "Set the spam action for this guild")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetSpamAction(
        [Summary("type", "classic or bot")] SpamType type,
        [Summary("action", "timeout, kick, ban")] SpamAction actionType,
        [Summary("duration", "duration for timeout in minutes")] int duration = 10,
        [Summary("deleteMessages", "delete messages?")] bool deleteMessages = false)
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
        [Summary("messages", "Number of messages to trigger spam")] int nbMessages,
        [Summary("interval", "Time interval in seconds to count messages")] double intervalSeconds)
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

        SocketGuild guild = Context.Guild;
        SocketGuildUser owner = guild.Owner;

        // --- Owner Info ---
        StringBuilder ownerBuilder = new StringBuilder();
        ownerBuilder.AppendLine($"Name : {owner.Mention}");
        ownerBuilder.AppendLine($"Account created at : {owner.CreatedAt:g}");
        ownerBuilder.AppendLine($"Account joined at : {owner.JoinedAt:g}");

        if (owner.Activities.Count > 0)
        {
            ownerBuilder.AppendLine("Activity:");
            foreach (IActivity? activity in owner.Activities)
            {
                string? line = activity switch
                {
                    SpotifyGame spotify => $"- **{spotify.Type}** to {spotify.Name} : *{spotify.TrackTitle}* by *{spotify.Artists.FirstOrDefault()}* on *{spotify.AlbumTitle}*",
                    CustomStatusGame custom => $"- **{custom.Type}** : {custom.Emote} {custom.State}",
                    RichGame rich => $"- **{rich.Type}** : {rich.Name} {rich.State} {rich.LargeAsset}",
                    StreamingGame streaming => $"- **{streaming.Type}** : {streaming.Name} {streaming.Url}",
                    Game game => $"- **{game.Type}** : {game.Name}",
                    _ => null
                };
                if (line != null) ownerBuilder.AppendLine(line);
            }
        }

        ownerBuilder.AppendLine($"\nRoles ({owner.Roles.Count}):");
        foreach (SocketRole role in owner.Roles.OrderByDescending(r => r.Position))
        {
            if (role.IsEveryone) continue;
            ownerBuilder.Append($"- {role}");
            if (role.IsMentionable) ownerBuilder.Append(" (***@***)");
            ownerBuilder.AppendLine();
        }

        // --- Members ---
        int nbBots = guild.Users.Count(u => u.IsBot);
        string memberStats =
            $"All Members : {guild.MemberCount}\n" +
            $"Bots : {nbBots}\n" +
            $"Humans : {guild.MemberCount - nbBots}";

        // --- Roles ---
        StringBuilder roleBuilder = new StringBuilder();
        roleBuilder.AppendLine($"Number of Roles : {guild.Roles.Count}");
        roleBuilder.AppendLine($"Highest Role : {guild.Roles.OrderByDescending(r => r.Position).First()}");
        roleBuilder.AppendLine($"Most popular Role : {guild.Roles.OrderByDescending(r => r.Members.Count()).First()}");
        roleBuilder.AppendLine("Roles and numbers of members (***Mentionable***):");

        foreach (SocketRole role in guild.Roles.OrderByDescending(r => r.Position))
        {
            if (role.IsEveryone) continue;
            roleBuilder.Append($"- {role} : {role.Members.Count()}");
            if (role.IsMentionable) roleBuilder.Append(" (***@***)");
            roleBuilder.AppendLine();
        }

        // --- Channels ---
        StringBuilder channelBuilder = new StringBuilder();
        channelBuilder.AppendLine($"Number of Channels : {guild.Channels.Count}");
        channelBuilder.AppendLine($"- Text Channels : {guild.TextChannels.Count}");
        channelBuilder.AppendLine($"- Voice Channels : {guild.VoiceChannels.Count}");
        channelBuilder.AppendLine($"- Category Channels : {guild.CategoryChannels.Count}");
        channelBuilder.AppendLine($"- Media Channels : {guild.MediaChannels.Count}");
        channelBuilder.AppendLine($"- Forum Channels : {guild.ForumChannels.Count}");
        channelBuilder.AppendLine($"- Thread Channels : {guild.ThreadChannels.Count}");
        channelBuilder.AppendLine($"- Stage Channels : {guild.StageChannels.Count}");

        AppendChannelInfo(guild.DefaultChannel, "Default Channel");
        AppendChannelInfo(guild.AFKChannel, "AFK Channel");
        AppendChannelInfo(guild.RulesChannel, "Rules Channel");
        AppendChannelInfo(guild.PublicUpdatesChannel, "Public Updates Channel");
        AppendChannelInfo(guild.SystemChannel, "System Channel");
        AppendChannelInfo(guild.SafetyAlertsChannel, "Safety Alerts Channel");
        AppendChannelInfo(guild.WidgetChannel, "Widget Channel");

        // --- Collect all fields ---
        List<(string, string, bool)> fields = new List<(string, string, bool)>
        {
            ("Owner", ownerBuilder.ToString(), false),
            ("Members", memberStats, true),
            ("Roles", roleBuilder.ToString(), false),
            ("Channels", channelBuilder.ToString(), false)
        };

        List<Embed> pages = await BuildEmbedsFromFields(fields, guild.Name);

        await ButtonPaginator.SendPaginatedEmbedsAsync(Context, pages, $"Server Stats for {guild.Name}", ephemeral);
        return;

        void AppendChannelInfo(IChannel? channel, string label)
        {
            if (channel != null) channelBuilder.AppendLine($"{label} : {(channel is ITextChannel text ? text.Mention : channel.Name)}");
        }
    }

    private static Task<List<Embed>> BuildEmbedsFromFields(List<(string Name, string Value, bool Inline)> fields, string title)
    {
        const int MaxEmbedFieldLength = 1024;
        const int MaxEmbedTotalLength = 6000;

        List<Embed> embeds = new List<Embed>();
        EmbedBuilder builder = new EmbedBuilder().WithTitle(title).WithColor(Color.Red).WithCurrentTimestamp();
        int currentLength = 0;

        foreach ((string Name, string Value, bool Inline) field in fields)
        {
            string remaining = field.Value;
            int chunkIndex = 0;

            while (remaining.Length > MaxEmbedFieldLength)
            {
                string chunk = remaining.Substring(0, MaxEmbedFieldLength);
                builder.AddField(field.Name + (chunkIndex > 0 ? $" (cont.)" : ""), chunk, field.Inline);
                embeds.Add(builder.Build());

                remaining = remaining.Substring(MaxEmbedFieldLength);
                builder = new EmbedBuilder().WithTitle(title).WithColor(Color.Red).WithCurrentTimestamp();
                chunkIndex++;
            }

            if (!string.IsNullOrEmpty(remaining))
            {
                builder.AddField(field.Name + (chunkIndex > 0 ? $" (cont.)" : ""), remaining, field.Inline);
                currentLength += remaining.Length;
            }

            if (currentLength <= MaxEmbedTotalLength) continue;
            embeds.Add(builder.Build());
            builder = new EmbedBuilder().WithTitle(title).WithColor(Color.Red).WithCurrentTimestamp();
            currentLength = 0;
        }

        if (builder.Fields.Count > 0)
        {
            embeds.Add(builder.Build());
        }

        return Task.FromResult(embeds);
    }

    [SlashCommand("userstats", "Send stats about the user")]
    public async Task UserStats([Summary("User", "User to ping for the command")] SocketGuildUser? user = null)
    {
        await DeferAsync();
        SocketGuildUser contextUser = Context.User as SocketGuildUser;
        user ??= contextUser;
        EmbedBuilder embedBuilder = new EmbedBuilder();
        //    .WithAuthor(guildUser.ToString(), guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
        //    .WithTitle("Roles")
        //    .WithDescription(roleList)
        //    .WithColor(Color.Green)
        //    .WithCurrentTimestamp();
        string userName = user.Username;
        embedBuilder.WithAuthor(userName);
        embedBuilder.Title = "User Stats";
        embedBuilder.Description = $"Stats of {user.Mention}";
        embedBuilder.WithCurrentTimestamp();
        embedBuilder.Color = Color.Red;
        EmbedFieldBuilder userFieldBuilder = new EmbedFieldBuilder
        {
            Name = "User",
            Value = $"Name : {user.Username}\n" +
                    $"Account created at : {user.CreatedAt}\n" +
                    $"Account joined at : {user.JoinedAt}"
        };
        if (user.Activities.Count > 0)
        {
            userFieldBuilder.Value += $"\n" +
                                      $"Activity :";
            foreach (IActivity activity in user.Activities)
            {
                switch (activity)
                {
                    case SpotifyGame spotifyGame:
                    {
                        userFieldBuilder.Value += $"\n" +
                                                  $"- **{spotifyGame.Type}** to {spotifyGame.Name} : *{spotifyGame.TrackTitle}* by *{spotifyGame.Artists.First()}* on *{spotifyGame.AlbumTitle}*";
                    }
                        break;
                    case CustomStatusGame customStatusGame:
                    {
                        userFieldBuilder.Value += $"\n" +
                                                  $"- **{customStatusGame.Type}** : {customStatusGame.Emote} {customStatusGame.State}";
                    }
                        break;
                    case RichGame richGame:
                    {
                        userFieldBuilder.Value += $"\n" +
                                                  $"- **{richGame.Type}** : {richGame.Name} {richGame.State} {richGame.LargeAsset}";
                    }
                        break;
                    case StreamingGame streamingGame:
                    {
                        userFieldBuilder.Value += $"\n" +
                                                  $"- **{streamingGame.Type}** : {streamingGame.Name} {streamingGame.Url}";
                    }
                        break;
                    case Game game:
                    {
                        userFieldBuilder.Value += $"\n" +
                                                  $"- **{game.Type}** : {game.Name}";
                    }
                        break;
                }
            }

            userFieldBuilder.Value += $"\n" +
                                      $"Roles ({user.Roles.Count}) :" +
                                      $"\n";
            foreach (SocketRole socketRole in user.Roles.OrderByDescending(x => x.Position))
            {
                if (socketRole.IsEveryone) continue;
                userFieldBuilder.Value += $"- {socketRole}";
                if (socketRole.IsMentionable)
                    userFieldBuilder.Value += " (***@***)";
                userFieldBuilder.Value += "\n";
            }
        }

        userFieldBuilder.IsInline = true;
        embedBuilder.AddField(userFieldBuilder);
        await FollowupAsync(embed: embedBuilder.Build());
    }

    //[RequireBotPermission(GuildPermission.BanMembers)]
    //[SlashCommand("ban", "ban users")]
    //public async Task BanMember([Summary("User", "User to ban")] SocketUser user)
    //{
    //    await DeferAsync();
    //    if (user == null)
    //    {
    //        await FollowupAsync("Need a user !");
    //        return;
    //    }
    //    await FollowupAsync($"{user.GlobalName} has been banned !");
    //    await Context.Guild.AddBanAsync(user.Id);
    //    await Task.CompletedTask;
    //}
    //
    //[RequireBotPermission(GuildPermission.BanMembers)]
    //[SlashCommand("banid", "ban users with id")]
    //public async Task BanMember([Summary("UserId", "User to ban")] ulong userId)
    //{
    //    await DeferAsync();
    //    IUser user = await Context.Client.GetUserAsync(userId);
    //    if (user == null)
    //    {
    //        await FollowupAsync($"Unknown user");
    //        return;
    //    }
    //    else
    //    {
    //        await FollowupAsync($"{user.Username} has been banned !");
    //        await Context.Guild.AddBanAsync(userId);
    //    }
    //    await Task.CompletedTask;
    //}

    //[RequireOwner()]
    //[SlashCommand("assignrole", "Assign roles to users")]
    public async Task AssignRole([Summary("Value", "value to start from")] int value)
    {
        await DeferAsync(true);
        _ = GiveRole(value);
        await FollowupAsync("Done !");
    }

    private async Task GiveRole(int value)
    {
        int i = 0;
        foreach (SocketGuildUser user in Context.Guild.Users)
        {
            if (i < value)
            {
                i++;
                continue;
            }

            bool hasPCVR = false;
            bool hasNomad = false;
            bool hasBetaPCVR = false;
            bool hasBetaNomad = false;
            ulong rolePCVR = 1000460998693625986;
            ulong roleNomad = 1000461086648176802;
            ulong roleBetaPCVR = 980767452487106601;
            ulong roleBetaNomad = 1189150798060462121;
            foreach (SocketRole role in user.Roles)
            {
                // PCVR
                if (role.Id == rolePCVR)
                {
                    hasPCVR = true;
                }
                // Nomad
                else if (role.Id == roleNomad)
                {
                    hasNomad = true;
                }
                // Beta PCVR
                else if (role.Id == roleBetaPCVR)
                {
                    hasBetaPCVR = true;
                }
                // Beta Nomad
                else if (role.Id == roleBetaNomad)
                {
                    hasBetaNomad = true;
                }
                else continue;

                switch (hasPCVR)
                {
                    case true when !hasBetaPCVR:
                        await user.AddRoleAsync(roleBetaPCVR);
                        break;
                    case false when hasBetaPCVR:
                        await user.AddRoleAsync(rolePCVR);
                        break;
                }

                if (hasNomad && !hasBetaNomad)
                {
                    await user.AddRoleAsync(roleBetaNomad);
                }

                if (!hasNomad && hasBetaNomad)
                {
                    await user.AddRoleAsync(roleNomad);
                }
            }

            if (i % 10 == 0 || i == (Context.Guild.Users.Count - 1))
                await FollowupAsync($"Done for :{user.DisplayName}; {i} / {Context.Guild.Users.Count}; {(i * 100f / Context.Guild.Users.Count)}%");
            i++;
        }
    }
}