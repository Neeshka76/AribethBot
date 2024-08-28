using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.ComponentModel.Design;

namespace AribethBot
{
    public class ServerCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private readonly DiscordSocketClient client;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        // constructor injection is also a valid way to access the dependencies
        public ServerCommands(ServiceHandler handler)
        {
            client = handler.socketClient;
            logger = handler.logger;
            httpClient = handler.httpClient;
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

            EmbedFieldBuilder ownerFieldBuilder = new EmbedFieldBuilder();
            ownerFieldBuilder.Name = "Owner";
            ownerFieldBuilder.Value = $"Name : {Context.Guild.Owner.Mention}\n" +
                                      $"Account created at : {Context.Guild.Owner.CreatedAt}\n" +
                                      $"Account joined at : {Context.Guild.Owner.JoinedAt}";
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
                        ownerFieldBuilder.Value += $" (***@***)";
                    ownerFieldBuilder.Value += $"\n";
                }
            }
            ownerFieldBuilder.IsInline = true;
            embedBuilder.AddField(ownerFieldBuilder);
            int nbBots = NbBots(Context.Guild);
            EmbedFieldBuilder serverStatsBuilder = new EmbedFieldBuilder();
            serverStatsBuilder.Name = "Members";
            serverStatsBuilder.Value = $"All Members : {Context.Guild.MemberCount}\n" +
                                       $"Bots : {nbBots}\n" +
                                       $"Humans : {Context.Guild.MemberCount - nbBots}\n";
            serverStatsBuilder.IsInline = true;
            embedBuilder.AddField(serverStatsBuilder);
            EmbedFieldBuilder roleStatsBuilder = new EmbedFieldBuilder();
            roleStatsBuilder.Name = "Roles";
            roleStatsBuilder.Value = $"Number of Roles : {Context.Guild.Roles.Count}\n" +
                                      $"Highest Role : {HighestRole(Context.Guild)}\n" +
                                      $"Most popular Role : {MostPopularMemberRole(Context.Guild)}\n" +
                                      $"Roles and numbers of members (***Mentionable***): \n";
            foreach (SocketRole socketRole in Context.Guild.Roles.OrderByDescending(x => x.Position))
            {
                if (socketRole.IsEveryone) continue;
                roleStatsBuilder.Value += $"- {socketRole} : {socketRole.Members.Count()}";
                if (socketRole.IsMentionable)
                    roleStatsBuilder.Value += $" (***@***)";
                roleStatsBuilder.Value += $"\n";
            }
            roleStatsBuilder.IsInline = true;
            embedBuilder.AddField(roleStatsBuilder);
            EmbedFieldBuilder channelStatsBuilder = new EmbedFieldBuilder();
            channelStatsBuilder.Name = "Channels";
            channelStatsBuilder.Value = $"Number of Channels : {Context.Guild.Channels.Count}\n" +
                                        $"- Text Channels : {Context.Guild.TextChannels.Count}\n" +
                                        $"- Voice Channels : {Context.Guild.VoiceChannels.Count}\n" +
                                        $"- Category Channels : {Context.Guild.CategoryChannels.Count}\n" +
                                        $"- Media Channels : {Context.Guild.MediaChannels.Count}\n" +
                                        $"- Forum Channels : {Context.Guild.ForumChannels.Count}\n" +
                                        $"- Thread Channels : {Context.Guild.ThreadChannels.Count}\n" +
                                        $"- Stage Channels : {Context.Guild.StageChannels.Count}\n";
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
                if (role.Position > rolePos)
                {
                    rolePos = role.Position;
                    roleReturned = role;
                }
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
                if (nbMember > roleNbMembers)
                {
                    roleNbMembers = nbMember;
                    roleReturned = role;
                }
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
            if (user == null)
            {
                user = contextUser;
            }
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

            EmbedFieldBuilder userFieldBuilder = new EmbedFieldBuilder();
            userFieldBuilder.Name = "User";
            userFieldBuilder.Value = $"Name : {user.Username}\n" +
                                      $"Account created at : {user.CreatedAt}\n" +
                                      $"Account joined at : {user.JoinedAt}";
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
                        userFieldBuilder.Value += $" (***@***)";
                    userFieldBuilder.Value += $"\n";
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

                foreach (SocketRole role in (user as SocketGuildUser).Roles)
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

                    if (hasPCVR && !hasBetaPCVR)
                    {
                        await user.AddRoleAsync(roleBetaPCVR);
                    }
                    if (!hasPCVR && hasBetaPCVR)
                    {
                        await user.AddRoleAsync(rolePCVR);
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
                    await FollowupAsync($"Done for :{user.DisplayName}; {i} / {Context.Guild.Users.Count}; {((float)i * 100f / Context.Guild.Users.Count)}%");
                i++;
            }
        }


    }
}
