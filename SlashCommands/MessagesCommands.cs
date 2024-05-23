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
    public class MessageCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private readonly DiscordSocketClient socketClient;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        // constructor injection is also a valid way to access the dependencies
        public MessageCommands(CommandHandler handler)
        {
            socketClient = handler.socketClient;
            logger = handler.logger;
            httpClient = handler.httpClient;
        }

        //[RequireUserPermission(GuildPermission.ManageMessages)]
        //[RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireOwner()]
        [SlashCommand("purge", "Purge messages from a channel where the command is executed", runMode: RunMode.Async)]
        public async Task PurgeAsync()
        {
            await RespondAsync($"Executing command in {Context.Channel.Name}", ephemeral: true);

            IEnumerable<IMessage> messages = await Context.Channel.GetMessagesAsync(100).FlattenAsync();
            if(messages.Count() <= 0)
            {
                await Task.CompletedTask;
                return;
            }
            List<IMessage> messagesBefore = new List<IMessage>();
            foreach (IMessage message in messages)
            {
                if ((DateTime.UtcNow - message.Timestamp).Days < 14)
                {
                    messagesBefore.Add(message);
                }
            }
            // Delete bulk
            if (messagesBefore.Count > 0)
            {
                logger.LogInformation($"BulkDelete : {messagesBefore.Count}");
                await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messagesBefore);
                await Task.Delay(1000);
            }
            else
            {
                logger.LogWarning($"Bots cannot bulk delete messages older than 2 weeks.");
            }
            if(messagesBefore.Count == messages.Count())
            {
                await Task.CompletedTask;
                return;
            }
            messages = await Context.Channel.GetMessagesAsync(100).FlattenAsync();
            if (messages.Count() > 0)
            {
                logger.LogInformation($"Still {messages.Count()} to delete");
                foreach (IMessage message in messages)
                {
                    // Ephemeral messages cannot be deleted if they are dismissed
                    if (message.Flags == MessageFlags.Ephemeral)
                        continue;
                    await message.DeleteAsync();
                    await Task.Delay(100);
                }
            }
        }
        //[RequireUserPermission(GuildPermission.ManageMessages)]
        //[RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireOwner()]
        [SlashCommand("copy", "copy messages from a channel to where the command is executed", runMode: RunMode.Async)]
        public async Task CopyAsync(string channelToCopy = "https://discord.com/channels/824331584319782982/852773540893687808", string destinationToPasteTo = "https://discord.com/channels/980745782594535484/1187742715589439570")
        {
            ulong[] from = ReturnGuildAndChannelsIDs(channelToCopy);
            ulong[] to = ReturnGuildAndChannelsIDs(destinationToPasteTo);
            DiscordSocketClient clientToGetMessagesFrom = socketClient;
            SocketTextChannel channelToCopyMessagesFrom = clientToGetMessagesFrom.GetGuild(from[0]).GetTextChannel(from[1]);
            DiscordSocketClient clientToPasteMessagesTo = socketClient;
            SocketTextChannel channelToPasteMessagesFrom = clientToPasteMessagesTo.GetGuild(to[0]).GetTextChannel(to[1]);
            IEnumerable<IMessage> messages = await channelToCopyMessagesFrom.GetMessagesAsync(100).FlattenAsync();
            await DeferAsync(ephemeral: true);
            foreach (IMessage message in messages.Reverse())
            {
                if (message.Attachments.Count <= 0)
                {
                    await channelToPasteMessagesFrom.SendMessageAsync(text: message.Content, isTTS: message.IsTTS);
                }
                else
                {
                    List<FileAttachment> fileAttachments = new List<FileAttachment>();
                    foreach (IAttachment attachment in message.Attachments)
                    {
                        if (attachment != null)
                        {
                            string url = attachment.Url.Substring(0, attachment.Url.LastIndexOf('?'));
                            int indexOfSlash = url.LastIndexOf('/') + 1;
                            string fileNameAndExtension = url.Substring(indexOfSlash, url.Length - indexOfSlash);
                            //FileStream fileStream = await DownloadAndSave(url, "Download", fileNameAndExtension);
                            Stream fileStream = await GetFileStream(url);
                            FileAttachment fileAttachment = new FileAttachment(fileStream, fileNameAndExtension);
                            fileAttachments.Add(fileAttachment);
                        }
                        else
                        {
                            await FollowupAsync($"URL : attachment null", ephemeral: true);
                        }
                    }
                    await channelToPasteMessagesFrom.SendFilesAsync(fileAttachments, text: message.Content, isTTS: message.IsTTS);
                }
            }
        }

        async Task<FileStream> DownloadAndSave(string sourceFile, string destinationFolder, string destinationFileName)
        {
            Stream fileStream = await GetFileStream(sourceFile);
            FileStream stream = null;
            if (fileStream != Stream.Null)
            {
                stream = await SaveStream(fileStream, destinationFolder, destinationFileName);
            }
            return stream;
        }

        async Task<Stream> GetFileStream(string fileUrl)
        {
            try
            {
                Stream fileStream = await httpClient.GetStreamAsync(fileUrl);
                return fileStream;
            }
            catch (Exception ex)
            {
                return Stream.Null;
            }
        }

        async Task<FileStream> SaveStream(Stream fileStream, string destinationFolder, string destinationFileName)
        {
            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            string path = Path.Combine(destinationFolder, destinationFileName);

            using (FileStream outputFileStream = new FileStream(path, FileMode.Create))
            {
                await fileStream.CopyToAsync(outputFileStream);
                return outputFileStream;
            }
        }

        private ulong[] ReturnGuildAndChannelsIDs(string link)
        {
            ulong[] ids = new ulong[2];
            string temp = link.Remove(0, "https://discord.com/channels/".Length);
            ulong guildId = ulong.Parse(temp.Split("/", StringSplitOptions.None)[0]);
            ulong channelId = ulong.Parse(temp.Split("/", StringSplitOptions.None)[1]);
            ids[0] = guildId;
            ids[1] = channelId;
            return ids;
        }
    }
}
