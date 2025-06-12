using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AribethBot
{
    public class ServerLogger
    {
        private readonly DiscordSocketClient socketClient;
        public readonly InteractionService Interactions;
        public readonly DiscordSocketConfig SocketConfig;
        public readonly ILogger Logger;
        public readonly CommandService Commands;
        private IConfiguration Config;
        private readonly string? channelDeletedLog;
        private readonly string? channelEditedLog;
        private readonly string? channelEntryOutLog;
        private readonly string? channelBanLog;
        private readonly string? channelVoiceActivityLog;
        private SocketTextChannel channelEdited;
        private SocketTextChannel channelDeleted;
        private SocketTextChannel channelEntryOut;
        private SocketTextChannel channelBan;
        private SocketTextChannel channelVoiceActivity;

        public ServerLogger(IServiceProvider services)
        {
            socketClient = services.GetRequiredService<DiscordSocketClient>();
            Interactions = services.GetRequiredService<InteractionService>();
            SocketConfig = services.GetRequiredService<DiscordSocketConfig>();
            Logger = services.GetRequiredService<ILogger<CommandsHandler>>();
            Commands = services.GetRequiredService<CommandService>();
            Config = services.GetRequiredService<IConfiguration>();
            channelDeletedLog = Config["channelDeletedLog"];
            channelEditedLog = Config["channelEditedLog"];
            channelEntryOutLog = Config["channelEntryOutLog"];
            channelBanLog = Config["channelBanLog"];
            channelVoiceActivityLog = Config["channelVoiceActivityLog"];
            // process the messages 
            socketClient.MessageUpdated += SocketClient_MessageUpdated;
            socketClient.MessageDeleted += SocketClient_MessageDeleted;
            socketClient.UserBanned += SocketClient_UserBanned;
            socketClient.UserUnbanned += SocketClient_UserUnbanned;
            socketClient.UserJoined += SocketClient_UserJoined;
            socketClient.UserLeft += SocketClient_UserLeft;
            socketClient.UserVoiceStateUpdated += SocketClient_UserVoiceStateUpdated;
        }

        private async Task SocketClient_UserVoiceStateUpdated(SocketUser user, SocketVoiceState voiceStateBefore, SocketVoiceState voiceStateAfter)
        {
            ulong[] channelUserJoined = ReturnGuildAndChannelsIDs(channelVoiceActivityLog);
            channelVoiceActivity = socketClient.GetGuild(channelUserJoined[0]).GetTextChannel(channelUserJoined[1]);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.WithCurrentTimestamp();
            // Joined a channel and wasn't inside one channel
            if (voiceStateBefore.VoiceChannel == null && voiceStateAfter.VoiceChannel != null)
            {
                embedBuilder.Title = $"Member joined voice channel";
                embedBuilder.Description = $"{user.Mention} joined {voiceStateAfter.VoiceChannel.Mention}";
                embedBuilder.Color = Color.Blue;
                await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
            }

            // Left and was in one channel
            if (voiceStateBefore.VoiceChannel != null && voiceStateAfter.VoiceChannel == null)
            {
                embedBuilder.Title = $"Member left voice channel";
                embedBuilder.Description = $"{user.Mention} left {voiceStateBefore.VoiceChannel.Mention}";
                embedBuilder.Color = Color.Red;
                await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
            }

            // Switch from a channel to another channel
            if (voiceStateBefore.VoiceChannel != null && voiceStateAfter.VoiceChannel != null && voiceStateBefore.VoiceChannel != voiceStateAfter.VoiceChannel)
            {
                embedBuilder.Title = $"Member left voice channel";
                embedBuilder.Description = $"{user.Mention} left {voiceStateBefore.VoiceChannel.Mention}";
                embedBuilder.Color = Color.Red;
                await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
                embedBuilder.Title = $"Member joined voice channel";
                embedBuilder.Description = $"{user.Mention} joined {voiceStateAfter.VoiceChannel.Mention}";
                embedBuilder.Color = Color.Blue;
                await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
            }
            if (voiceStateBefore.VoiceChannel != null && voiceStateAfter.VoiceChannel != null && voiceStateBefore.VoiceChannel == voiceStateAfter.VoiceChannel)
            {
                // Started a streaming
                if (!voiceStateBefore.IsStreaming && voiceStateAfter.IsStreaming)
                {
                    embedBuilder.Title = $"Member started a streaming in voice channel";
                    embedBuilder.Description = $"{user.Mention} started a streaming {voiceStateAfter.VoiceChannel.Mention}";
                    embedBuilder.Color = Color.Purple;
                    await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
                }

                // Ended a streaming
                if (voiceStateBefore.IsStreaming && !voiceStateAfter.IsStreaming)
                {
                    embedBuilder.Title = $"Member ended a streaming in voice channel";
                    embedBuilder.Description = $"{user.Mention} ended a streaming {voiceStateAfter.VoiceChannel.Mention}";
                    embedBuilder.Color = Color.Orange;
                    await channelVoiceActivity.SendMessageAsync(embed: embedBuilder.Build());
                }
            }
        }

        private async Task SocketClient_UserLeft(SocketGuild guild, SocketUser user)
        {
            ulong[] channelUserJoined = ReturnGuildAndChannelsIDs(channelEntryOutLog);
            channelEntryOut = socketClient.GetGuild(channelUserJoined[0]).GetTextChannel(channelUserJoined[1]);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.Title = $"Member left";
            SocketGuildUser guildUser = user as SocketGuildUser;
            string roles = "";
            int i = 0;
            foreach (SocketRole role in guildUser.Roles)
            {
                if (role.IsEveryone)
                    continue;
                if (i == 0)
                {
                    roles += role.Mention;
                }
                else
                {
                    roles += "; " + role.Mention;
                }
                i++;
            }
            embedBuilder.Description = $"{user.Mention} joined {guildUser.JoinedAt} ({ReturnDateTimeOffsetDifference(guildUser.JoinedAt)}) \n" +
                                       $"**Roles : ** {roles}";
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Red;
            await channelEntryOut.SendMessageAsync(embed: embedBuilder.Build());
        }

        private string ReturnDateTimeOffsetDifference(DateTimeOffset? startDate)
        {
            DateTimeOffset endDate = DateTimeOffset.Now;
            DateTimeOffset actualStartDate = startDate ?? new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            if (startDate == null)
                return "";
            // Calculate the difference in years, months, days, hours, minutes and seconds
            int years = endDate.Year - actualStartDate.Year;
            int months = endDate.Month - actualStartDate.Month;
            int days = endDate.Day - actualStartDate.Day;
            int hours = endDate.Hour - actualStartDate.Hour;
            int minutes = endDate.Minute - actualStartDate.Minute;
            int seconds = endDate.Second - actualStartDate.Second;
            if (seconds < 0)
            {
                minutes--;
                seconds += 60;
            }
            if (minutes < 0)
            {
                hours--;
                minutes += 60;
            }
            if (hours < 0)
            {
                days--;
                hours += 24;
            }
            if (days < 0)
            {
                months--;
                days += DateTime.DaysInMonth(actualStartDate.Year, (actualStartDate.Month + months) % 12);
            }
            if (months < 0)
            {
                years--;
                months += 12;
            }
            string dateStrToReturn = "";
            if (years != 0)
                dateStrToReturn += years + $" year{(years == 1 ? "" : "s")},";
            if (months != 0)
                dateStrToReturn += " " + months + $" month{(months == 1 ? "" : "s")},";
            if (days != 0)
                dateStrToReturn += " " + days + $" day{(days == 1 ? "" : "s")},";
            if (hours != 0)
                dateStrToReturn += " " + hours + $" hour{(hours == 1 ? "" : "s")},";
            if (minutes != 0)
                dateStrToReturn += " " + minutes + $" minute{(minutes == 1 ? "" : "s")},";
            if (seconds != 0)
                dateStrToReturn += " " + seconds + $" second{(seconds == 1 ? "" : "s")}";
            if (dateStrToReturn.EndsWith(','))
                dateStrToReturn = dateStrToReturn.Substring(0, dateStrToReturn.Length - 1);
            if (dateStrToReturn.StartsWith(" "))
                dateStrToReturn = dateStrToReturn.Substring(1);
            return dateStrToReturn;
        }

        private async Task SocketClient_UserJoined(SocketGuildUser guildUser)
        {
            ulong[] channelUserJoined = ReturnGuildAndChannelsIDs(channelEntryOutLog);
            channelEntryOut = socketClient.GetGuild(channelUserJoined[0]).GetTextChannel(channelUserJoined[1]);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(guildUser.Username, guildUser.GetAvatarUrl());
            embedBuilder.Title = $"Member joined";
            embedBuilder.Description = $"{guildUser.Mention} {guildUser.Guild.MemberCount}th to join\n" +
                                       $"created at {guildUser.CreatedAt} ({ReturnDateTimeOffsetDifference(guildUser.CreatedAt)})";
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Green;
            await channelEntryOut.SendMessageAsync(embed: embedBuilder.Build());
        }

        private async Task SocketClient_UserUnbanned(SocketUser user, SocketGuild guild)
        {
            ulong[] channelUserBan = ReturnGuildAndChannelsIDs(channelBanLog);
            channelBan = socketClient.GetGuild(channelUserBan[0]).GetTextChannel(channelUserBan[1]);
            RestBan restBan = await guild.GetBanAsync(user.Id);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.Title = $"Unban";
            embedBuilder.Description =
                $"**Offender : **{user.Username} {user.Mention}"; /*\n" +
                $"**Reason : ** {restBan.Reason}"; \n" +
                $"**Responsible moderator : {restBan.}**";*/
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Blue;
            await channelBan.SendMessageAsync(embed: embedBuilder.Build());
        }

        private async Task SocketClient_UserBanned(SocketUser user, SocketGuild guild)
        {
            ulong[] channelUserBan = ReturnGuildAndChannelsIDs(channelBanLog);
            channelBan = socketClient.GetGuild(channelUserBan[0]).GetTextChannel(channelUserBan[1]);
            RestBan restBan = await guild.GetBanAsync(user.Id);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.Title = $"Ban";
            embedBuilder.Description =
                $"**Offender : **{user.Username} {user.Mention}\n" +
                $"**Reason : ** {restBan.Reason}"; /* \n" +
                $"**Responsible moderator : {restBan.}**";*/
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Red;
            await channelBan.SendMessageAsync(embed: embedBuilder.Build());
        }

        private async Task SocketClient_MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            ulong[] channelDeletedParse = ReturnGuildAndChannelsIDs(channelDeletedLog);
            channelDeleted = socketClient.GetGuild(channelDeletedParse[0]).GetTextChannel(channelDeletedParse[1]);
            IMessage oldMessage = message.GetOrDownloadAsync().Result;
            // ensures we don't process system/other bot messages
            if (oldMessage is not SocketUserMessage userMessage)
            {
                return;
            }
            if (oldMessage.Source != MessageSource.User)
            {
                return;
            }
            SocketUser user = userMessage.Author;
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.Title = $"Message deleted in <#{userMessage.Channel.Id}>";
            embedBuilder.Description = $"{userMessage.Content}";
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Red;
            if (userMessage.Attachments.Count != 0)
                await ResendAttachmentsAsync(userMessage, embedBuilder, channelDeleted);
            else
                await channelDeleted.SendMessageAsync(embed: embedBuilder.Build());
        }

        private async Task ResendAttachmentsAsync(IMessage originalMessage, EmbedBuilder embedBuilder, IMessageChannel targetChannel)
        {
            using HttpClient httpClient = new HttpClient();
            string message = "";
            int i = 0;
            foreach (IAttachment? attachment in originalMessage.Attachments)
            {
                i++;
                message += attachment.Url;
                if (i != originalMessage.Attachments.Count)
                    message += "\n";
            }
            await targetChannel.SendMessageAsync(text: message, embed: embedBuilder.Build());
        }

        private async Task SocketClient_MessageUpdated(Cacheable<IMessage, ulong> message, SocketMessage updatedMessage, ISocketMessageChannel channel)
        {
            ulong[] channelEditedParse = ReturnGuildAndChannelsIDs(channelEditedLog);
            channelEdited = socketClient.GetGuild(channelEditedParse[0]).GetTextChannel(channelEditedParse[1]);
            IMessage oldMessage = message.GetOrDownloadAsync().Result;
            // ensures we don't process system/other bot messages
            if (oldMessage is not SocketUserMessage userMessage)
            {
                return;
            }
            if (oldMessage.Source != MessageSource.User)
            {
                return;
            }

            // Ensure that the message isn't the same (cause by embedded)
            if (updatedMessage.Content == userMessage.Content)
                return;
            SocketUser user = userMessage.Author;
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
            embedBuilder.Title = $"Message updated in <#{userMessage.Channel.Id}>";
            embedBuilder.Description = $"**Before : **\n{userMessage.Content}\n" +
                                       $"**After : **\n{updatedMessage.Content}";
            embedBuilder.WithCurrentTimestamp();
            embedBuilder.Color = Color.Blue;
            await channelEdited.SendMessageAsync(embed: embedBuilder.Build());
        }

        private ulong[] ReturnGuildAndChannelsIDs(string? link)
        {
            ulong[] ids = new ulong[2];
            string temp = link.Remove(0, "https://discord.com/channels/".Length);
            ulong guildId = ulong.Parse(temp.Split("/")[0]);
            ulong channelId = ulong.Parse(temp.Split("/")[1]);
            ids[0] = guildId;
            ids[1] = channelId;
            return ids;
        }
    }
}