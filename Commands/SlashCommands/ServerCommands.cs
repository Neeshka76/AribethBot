using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

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
            [Summary("channel_id", "The ID of the channel")] ulong channelId)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
            string json = await File.ReadAllTextAsync(filePath);
            dynamic jsonObj = JsonConvert.DeserializeObject(json);

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

            // Ensure guild config exists
            if (jsonObj["guilds"][guildId] == null)
            {
                jsonObj["guilds"][guildId] = new
                {
                    channelDeletedLog = "0",
                    channelEditedLog = "0",
                    channelEntryOutLog = "0",
                    channelBanLog = "0",
                    channelVoiceActivityLog = "0"
                };
            }

            // Update config with raw channel ID
            jsonObj["guilds"][guildId][configKey] = channelId.ToString();

            string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, output);

            // Reload config
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("config.json", optional: false, reloadOnChange: true);
            config = builder.Build();

            await RespondAsync($"Updated `{type}` log channel to <#{channelId}>");
        }

        

        [SlashCommand("serverstats", "Send stats about the server")]
        public async Task ServerStats()
        {
            await DeferAsync();
            EmbedBuilder embedBuilder = new EmbedBuilder();
            //    .WithAuthor(guildUser.ToString(), guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
            //    .WithTitle("Roles")
            //    .WithDescription(roleList)
            //    .WithColor(Color.Green)
            //    .WithCurrentTimestamp();
            string serverName = Context.Guild.Name;
            embedBuilder.WithAuthor(Context.Guild.Owner.GlobalName);
            embedBuilder.Title = "Server Stats";
            embedBuilder.Description = $"Stats of {serverName}";
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Red;
            EmbedFieldBuilder ownerFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Owner",
                Value = $"Name : {Context.Guild.Owner.Mention}\n" +
                        $"Account created at : {Context.Guild.Owner.CreatedAt}\n" +
                        $"Account joined at : {Context.Guild.Owner.JoinedAt}"
            };
            if (Context.Guild.Owner.Activities.Count > 0)
            {
                ownerFieldBuilder.Value += $"\n" +
                                           $"Activity :";
                foreach (IActivity activity in Context.Guild.Owner.Activities)
                {
                    switch (activity)
                    {
                        case SpotifyGame spotifyGame:
                        {
                            ownerFieldBuilder.Value += $"\n" +
                                                       $"- **{spotifyGame.Type}** to {spotifyGame.Name} : *{spotifyGame.TrackTitle}* by *{spotifyGame.Artists.First()}* on *{spotifyGame.AlbumTitle}*";
                        }
                            break;
                        case CustomStatusGame customStatusGame:
                        {
                            ownerFieldBuilder.Value += $"\n" +
                                                       $"- **{customStatusGame.Type}** : {customStatusGame.Emote} {customStatusGame.State}";
                        }
                            break;
                        case RichGame richGame:
                        {
                            ownerFieldBuilder.Value += $"\n" +
                                                       $"- **{richGame.Type}** : {richGame.Name} {richGame.State} {richGame.LargeAsset}";
                        }
                            break;
                        case StreamingGame streamingGame:
                        {
                            ownerFieldBuilder.Value += $"\n" +
                                                       $"- **{streamingGame.Type}** : {streamingGame.Name} {streamingGame.Url}";
                        }
                            break;
                        case Game game:
                        {
                            ownerFieldBuilder.Value += $"\n" +
                                                       $"- **{game.Type}** : {game.Name}";
                        }
                            break;
                    }
                }
                ownerFieldBuilder.Value += $"\n" +
                                           $"Roles ({Context.Guild.Owner.Roles.Count}) :" +
                                           $"\n";
                foreach (SocketRole socketRole in Context.Guild.Owner.Roles.OrderByDescending(x => x.Position))
                {
                    if (socketRole.IsEveryone) continue;
                    ownerFieldBuilder.Value += $"- {socketRole}";
                    if (socketRole.IsMentionable)
                        ownerFieldBuilder.Value += " (***@***)";
                    ownerFieldBuilder.Value += "\n";
                }
            }
            ownerFieldBuilder.IsInline = true;
            embedBuilder.AddField(ownerFieldBuilder);
            int nbBots = NbBots(Context.Guild);
            EmbedFieldBuilder serverStatsBuilder = new EmbedFieldBuilder
            {
                Name = "Members",
                Value = $"All Members : {Context.Guild.MemberCount}\n" +
                        $"Bots : {nbBots}\n" +
                        $"Humans : {Context.Guild.MemberCount - nbBots}\n",
                IsInline = true
            };
            embedBuilder.AddField(serverStatsBuilder);
            EmbedFieldBuilder roleStatsBuilder = new EmbedFieldBuilder
            {
                Name = "Roles",
                Value = $"Number of Roles : {Context.Guild.Roles.Count}\n" +
                        $"Highest Role : {HighestRole(Context.Guild)}\n" +
                        $"Most popular Role : {MostPopularMemberRole(Context.Guild)}\n" +
                        $"Roles and numbers of members (***Mentionable***): \n"
            };
            foreach (SocketRole socketRole in Context.Guild.Roles.OrderByDescending(x => x.Position))
            {
                if (socketRole.IsEveryone) continue;
                roleStatsBuilder.Value += $"- {socketRole} : {socketRole.Members.Count()}";
                if (socketRole.IsMentionable)
                    roleStatsBuilder.Value += " (***@***)";
                roleStatsBuilder.Value += "\n";
            }
            roleStatsBuilder.IsInline = true;
            embedBuilder.AddField(roleStatsBuilder);
            EmbedFieldBuilder channelStatsBuilder = new EmbedFieldBuilder
            {
                Name = "Channels",
                Value = $"Number of Channels : {Context.Guild.Channels.Count}\n" +
                        $"- Text Channels : {Context.Guild.TextChannels.Count}\n" +
                        $"- Voice Channels : {Context.Guild.VoiceChannels.Count}\n" +
                        $"- Category Channels : {Context.Guild.CategoryChannels.Count}\n" +
                        $"- Media Channels : {Context.Guild.MediaChannels.Count}\n" +
                        $"- Forum Channels : {Context.Guild.ForumChannels.Count}\n" +
                        $"- Thread Channels : {Context.Guild.ThreadChannels.Count}\n" +
                        $"- Stage Channels : {Context.Guild.StageChannels.Count}\n"
            };
            if (Context.Guild.DefaultChannel != null)
                channelStatsBuilder.Value += $"Default Channel : {Context.Guild.DefaultChannel.Mention}\n";
            if (Context.Guild.AFKChannel != null)
                channelStatsBuilder.Value += $"AFK Channel : {Context.Guild.AFKChannel.Mention}\n";
            if (Context.Guild.RulesChannel != null)
                channelStatsBuilder.Value += $"Rules Channel : {Context.Guild.RulesChannel.Mention}\n";
            if (Context.Guild.PublicUpdatesChannel != null)
                channelStatsBuilder.Value += $"Public Updates Channel : {Context.Guild.PublicUpdatesChannel.Mention}\n";
            if (Context.Guild.SystemChannel != null)
                channelStatsBuilder.Value += $"System Channel : {Context.Guild.SystemChannel.Mention}\n";
            if (Context.Guild.SafetyAlertsChannel != null)
                channelStatsBuilder.Value += $"Safety Alerts Channel : {Context.Guild.SafetyAlertsChannel.Name}\n";
            if (Context.Guild.WidgetChannel != null)
                channelStatsBuilder.Value += $"Widget Channel : {Context.Guild.WidgetChannel.Name}\n";
            channelStatsBuilder.IsInline = true;
            embedBuilder.AddField(channelStatsBuilder);
            await FollowupAsync(embed: embedBuilder.Build());
        }

        private SocketRole HighestRole(SocketGuild socketGuild)
        {
            int rolePos = 0;
            SocketRole roleReturned = null;
            foreach (SocketRole role in socketGuild.Roles)
            {
                if (role.Position <= rolePos) continue;
                rolePos = role.Position;
                roleReturned = role;
            }
            return roleReturned;
        }

        private SocketRole MostPopularMemberRole(SocketGuild socketGuild)
        {
            int roleNbMembers = 0;
            SocketRole roleReturned = null;
            foreach (SocketRole role in socketGuild.Roles)
            {
                if (role.IsEveryone) continue;
                int nbMember = role.Members.Count();
                if (nbMember <= roleNbMembers) continue;
                roleNbMembers = nbMember;
                roleReturned = role;
            }
            return roleReturned;
        }

        private int NbBots(SocketGuild socketGuild)
        {
            int nbBots = 0;
            foreach (SocketGuildUser user in socketGuild.Users)
            {
                if (user.IsBot) nbBots++;
            }
            return nbBots;
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