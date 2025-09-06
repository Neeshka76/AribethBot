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


    [SlashCommand("manage_users", "View and manage special user categories")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ManageUsersCommand()
    {
        await DeferAsync(ephemeral: true);

        // Main buttons to select user category
        MessageComponent categoryButtons = new ComponentBuilder()
            .WithButton("Pending Users", "select_pending")
            .WithButton("Users Without Roles", "select_no_roles", ButtonStyle.Secondary)
            .Build();

        IUserMessage mainMessage = await FollowupAsync(
            "Select which type of users to manage:",
            components: categoryButtons,
            ephemeral: true
        );

        async Task CategoryHandler(SocketMessageComponent categoryEvent)
        {
            if (categoryEvent.Message.Id != mainMessage.Id) return;
            if (categoryEvent.User.Id != Context.User.Id)
            {
                await categoryEvent.RespondAsync("You cannot use this menu.", ephemeral: true);
                return;
            }

            List<SocketGuildUser> targetUsers;
            string title, reasonPrefix;

            switch (categoryEvent.Data.CustomId)
            {
                case "select_pending":
                    targetUsers = Context.Guild.Users.Where(u => (bool)u.IsPending).ToList();
                    title = "Pending Users";
                    reasonPrefix = "Pending screening";
                    break;

                case "select_no_roles":
                    targetUsers = Context.Guild.Users.Where(u => u.Roles.Count == 1).ToList();
                    title = "Users Without Roles";
                    reasonPrefix = "No roles";
                    break;

                default:
                    return;
            }

            if (!targetUsers.Any())
            {
                await categoryEvent.UpdateAsync(msg =>
                {
                    msg.Content = $"No users found for {title}.";
                    msg.Components = new ComponentBuilder().Build();
                });
                return;
            }

            // Build paginated embeds for display
            List<(string Name, string Value, bool Inline)> fields = new();
            int chunkSize = 10;
            for (int i = 0; i < targetUsers.Count; i += chunkSize)
            {
                IEnumerable<string> chunk = targetUsers.Skip(i).Take(chunkSize).Select(u => u.Mention);
                fields.Add(($"Users {i + 1}-{i + chunk.Count()}", string.Join("\n", chunk), false));
            }

            List<Embed> pages = await BuildEmbedsFromFields(fields, title);
            int currentPage = 0;

            Embed AddPageFooter(Embed embed)
            {
                EmbedBuilder builder = new EmbedBuilder()
                    .WithTitle(embed.Title)
                    .WithDescription(embed.Description)
                    .WithColor(embed.Color ?? Color.Default)
                    .WithFooter($"Page {currentPage + 1}/{pages.Count}");

                if (embed.Timestamp.HasValue) builder.WithTimestamp(embed.Timestamp.Value);
                if (embed.Author != null) builder.WithAuthor(embed.Author.Value.Name, embed.Author.Value.IconUrl, embed.Author.Value.Url);
                foreach (EmbedField field in embed.Fields) builder.AddField(field.Name, field.Value, field.Inline);

                return builder.Build();
            }

            // Buttons for pagination and actions
            MessageComponent BuildComponents()
            {
                return new ComponentBuilder()
                    .WithButton("⏮️ Prev", "paginator_prev", disabled: currentPage == 0)
                    .WithButton("⏭️ Next", "paginator_next", disabled: currentPage == pages.Count - 1)
                    .WithButton("Ban All", "ban_all", ButtonStyle.Danger)
                    .WithButton("Kick All", "kick_all")
                    .Build();
            }

            await categoryEvent.UpdateAsync(msg =>
            {
                msg.Content = null;
                msg.Embed = AddPageFooter(pages[currentPage]);
                msg.Components = BuildComponents();
            });

            async Task ActionHandler(SocketMessageComponent actionEvent)
            {
                if (actionEvent.Message.Id != mainMessage.Id) return;
                if (actionEvent.User.Id != Context.User.Id)
                {
                    await actionEvent.RespondAsync("You cannot control this paginator.", ephemeral: true);
                    return;
                }

                switch (actionEvent.Data.CustomId)
                {
                    case "paginator_prev":
                        currentPage = Math.Max(currentPage - 1, 0);
                        break;
                    case "paginator_next":
                        currentPage = Math.Min(currentPage + 1, pages.Count - 1);
                        break;

                    case "ban_all":
                    case "kick_all":
                        
                        // Defer immediately
                        await actionEvent.DeferAsync(ephemeral: true);
                        // Create new ephemeral message for processing progress
                        RestFollowupMessage progressMessage = await actionEvent.FollowupAsync(
                            $"Starting {(actionEvent.Data.CustomId == "ban_all" ? "ban" : "kick")} for {targetUsers.Count} users...",
                            ephemeral: true
                        );

                        int total = targetUsers.Count;
                        int processed = 0;
                        int batchSize = 5;

                        foreach (SocketGuildUser user in targetUsers)
                        {
                            try
                            {
                                if (actionEvent.Data.CustomId == "ban_all")
                                    await Context.Guild.AddBanAsync(user, 0, $"{reasonPrefix} ban");
                                else
                                    await user.KickAsync($"{reasonPrefix} kick");
                            }
                            catch
                            {
                                // ignore individual errors
                            }

                            processed++;

                            if (processed % batchSize == 0 || processed == total)
                            {
                                double percent = (processed * 100.0) / total;
                                await progressMessage.ModifyAsync(msg =>
                                    msg.Content = $"Processing {(actionEvent.Data.CustomId == "ban_all" ? "ban" : "kick")}: {processed}/{total} users ({percent:0.0}%)");
                            }

                            await Task.Delay(1000); // optional delay
                        }

                        await progressMessage.ModifyAsync(msg =>
                            msg.Content = $"All {total} users have been {(actionEvent.Data.CustomId == "ban_all" ? "banned" : "kicked")}!");
                        return;
                }

                // Update pagination embeds
                await actionEvent.UpdateAsync(msg =>
                {
                    msg.Embed = AddPageFooter(pages[currentPage]);
                    msg.Components = BuildComponents();
                });
            }

            Context.Client.ButtonExecuted += ActionHandler;

            // Auto-remove handler after 5 minutes
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                Context.Client.ButtonExecuted -= ActionHandler;
            });
        }

        Context.Client.ButtonExecuted += CategoryHandler;

        // Auto-remove main handler after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.ButtonExecuted -= CategoryHandler;
            await mainMessage.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        });
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
        const int MaxEmbedTotalLength = 6000;

        List<Embed> embeds = new List<Embed>();
        EmbedBuilder builder = new EmbedBuilder().WithTitle(title).WithColor(Color.Red).WithCurrentTimestamp();
        int currentLength = 0;

        foreach ((string Name, string Value, bool Inline) field in fields)
        {
            int fieldLength = field.Name.Length + field.Value.Length;

            // If adding this field would exceed the total embed limit, start a new embed
            if (currentLength + fieldLength > MaxEmbedTotalLength)
            {
                embeds.Add(builder.Build());
                builder = new EmbedBuilder().WithTitle(title).WithColor(Color.Red).WithCurrentTimestamp();
                currentLength = 0;
            }

            builder.AddField(field.Name, field.Value, field.Inline);
            currentLength += fieldLength;
        }

        if (builder.Fields.Count > 0)
            embeds.Add(builder.Build());

        return Task.FromResult(embeds);
    }

    [SlashCommand("userstats", "Send stats about the user")]
    public async Task UserStats([Summary("User", "User to ping for the command")] SocketGuildUser? user = null)
    {
        await DeferAsync();
        SocketGuildUser contextUser = Context.User as SocketGuildUser;
        user ??= contextUser;
        EmbedBuilder embedBuilder = new EmbedBuilder();
        string userName = user.Username;
        embedBuilder.WithAuthor(userName);
        embedBuilder.Title = "User Stats";
        embedBuilder.Description = $"Stats of {user.Mention}";
        embedBuilder.WithCurrentTimestamp();
        embedBuilder.Color = Color.Green;
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
        }

        List<SocketRole> roles = user.Roles
            .Where(r => !r.IsEveryone) // exclude @everyone
            .OrderByDescending(r => r.Position)
            .ToList();

        userFieldBuilder.Value += $"\n" +
                                  $"Roles ({roles.Count}) :" +
                                  $"\n";
        foreach (SocketRole socketRole in roles)
        {
            if (socketRole.IsEveryone) continue;
            userFieldBuilder.Value += $"- {socketRole}";
            if (socketRole.IsMentionable)
                userFieldBuilder.Value += " (***@***)";
            userFieldBuilder.Value += "\n";
        }

        userFieldBuilder.IsInline = true;
        embedBuilder.AddField(userFieldBuilder);
        await FollowupAsync(embed: embedBuilder.Build());
    }
}