using System.Text;
using AribethBot.Helpers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AribethBot
{
    public class ServerCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private IConfiguration config;
        private ServiceHandler handler;

        // constructor injection is also a valid way to access the dependencies
        public ServerCommands(ServiceHandler handler)
        {
            config = handler.Config;
            this.handler = handler;
        }

        [RequireOwner]
        [SlashCommand("classicspamtrignbmsg", "Edit the number of messages for classic spam detection")]
        public async Task EditClassicSpamTriggerNbMessages([Summary("NbMessages", "value for the number of messages")] int nbMessages)
        {
            string json = await File.ReadAllTextAsync(AppContext.BaseDirectory + "config.json");
            dynamic jsonObj = JsonConvert.DeserializeObject(json);
            string oldValue = jsonObj["nbMessagesSpamTriggerClassic"];
            jsonObj["nbMessagesSpamTriggerClassic"] = nbMessages;
            string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            await File.WriteAllTextAsync(AppContext.BaseDirectory + "config.json", output);
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            config = builder.Build();
            handler.Config = config;
            await RespondAsync($"Number of messages for the ClassicSpamTrigger is updated ! ({oldValue} -> {nbMessages})");
        }

        [RequireOwner]
        [SlashCommand("classicspamtriginttime", "Edit the time interval for classic spam detection")]
        public async Task EditClassicSpamTriggerIntervalTime([Summary("IntervalTime", "value for the time interval")] double timeInterval)
        {
            string json = await File.ReadAllTextAsync(AppContext.BaseDirectory + "config.json");
            dynamic jsonObj = JsonConvert.DeserializeObject(json);
            string oldValue = jsonObj["intervalTimeSpamTriggerClassic"];
            jsonObj["intervalTimeSpamTriggerClassic"] = timeInterval.ToString();
            string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            await File.WriteAllTextAsync(AppContext.BaseDirectory + "config.json", output);
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            config = builder.Build();
            handler.Config = config;
            await RespondAsync($"Time interval for the ClassicSpamTrigger is updated ! ({oldValue} -> {timeInterval})");
        }

        [RequireOwner]
        [SlashCommand("botspamtrignbmsg", "Edit the number of messages for bot spam detection")]
        public async Task EditBotSpamTriggerNbMessages([Summary("NbMessages", "value for the number of messages")] int nbMessages)
        {
            string json = await File.ReadAllTextAsync(AppContext.BaseDirectory + "config.json");
            dynamic jsonObj = JsonConvert.DeserializeObject(json);
            string oldValue = jsonObj["nbMessagesSpamTriggerBot"];
            jsonObj["nbMessagesSpamTriggerBot"] = nbMessages;
            string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            await File.WriteAllTextAsync(AppContext.BaseDirectory + "config.json", output);
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            config = builder.Build();
            handler.Config = config;
            await RespondAsync($"Number of messages for the BotSpamTrigger is updated ! ({oldValue} -> {nbMessages})");
        }

        [RequireOwner]
        [SlashCommand("botspamtriginttime", "Edit the time interval for bot spam detection")]
        public async Task EditBotSpamTriggerIntervalTime([Summary("IntervalTime", "value for the time interval")] double timeInterval)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
            string json = await File.ReadAllTextAsync(filePath);
            dynamic jsonObj = JsonConvert.DeserializeObject(json);
            string oldValue = jsonObj["intervalTimeSpamTriggerBot"];
            jsonObj["intervalTimeSpamTriggerBot"] = timeInterval.ToString();
            string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            await File.WriteAllTextAsync(AppContext.BaseDirectory + "config.json", output);
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            config = builder.Build();
            handler.Config = config;
            await RespondAsync($"Time interval for the BotSpamTrigger is updated ! ({oldValue} -> {timeInterval})");
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
            [Summary("channel_id", "The ID of the channel (0 for not logging)")]
            string channelId)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
            string json = await File.ReadAllTextAsync(filePath);

            // Parse as JObject (safe, no dynamic)
            JObject jsonObj = JObject.Parse(json);

            string guildId = Context.Guild.Id.ToString();

            // Map enum to config key
            string? configKey = type switch
            {
                LogChannelType.Deleted => "channelDeletedLog",
                LogChannelType.Edited => "channelEditedLog",
                LogChannelType.EntryOut => "channelEntryOutLog",
                LogChannelType.Ban => "channelBanLog",
                LogChannelType.Voice => "channelVoiceActivityLog",
                _ => null
            };

            if (configKey == null)
            {
                await RespondAsync("Invalid log type.");
                return;
            }

            // Ensure the "guilds" section exists
            jsonObj["guilds"] ??= new JObject();

            // Ensure this guild exists
            jsonObj["guilds"][guildId] ??= JObject.FromObject(new
            {
                channelDeletedLog = "0",
                channelEditedLog = "0",
                channelEntryOutLog = "0",
                channelBanLog = "0",
                channelVoiceActivityLog = "0"
            });

            // Update the channel ID
            jsonObj["guilds"][guildId][configKey] = channelId;

            // Save back to file
            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));

            await RespondAsync($"Updated `{type}` log channel to <#{channelId}> for this guild.");
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
}