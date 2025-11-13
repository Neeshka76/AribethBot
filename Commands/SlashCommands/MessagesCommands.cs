using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace AribethBot;

[Group("messages", "Commands related to messages")]
public class MessageCommands : InteractionModuleBase<SocketInteractionContext>
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private readonly DiscordSocketClient socketClient;
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    
    // constructor injection is also a valid way to access the dependencies
    public MessageCommands(ServiceHandler handler)
    {
        socketClient = handler.SocketClient;
        logger = handler.Logger;
        httpClient = handler.HttpClient;
    }
    
    private async Task PurgeMessagesAsync(
        ITextChannel channel,
        IEnumerable<IMessage> messages,
        int amount = int.MaxValue)
    {
        // Take only the requested number of messages
        List<IMessage> toDelete = messages
            .Where(m => m.Flags != MessageFlags.Ephemeral)
            .Take(amount)
            .ToList();
        
        if (!toDelete.Any())
            return;
        
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        
        // Split messages into bulk deletable and old
        List<IMessage> bulkDeletable = toDelete.Where(m => m.Timestamp > cutoff).ToList();
        List<IMessage> oldMessages = toDelete.Where(m => m.Timestamp <= cutoff).ToList();
        
        if (bulkDeletable.Count > 0)
            await channel.DeleteMessagesAsync(bulkDeletable);
        
        foreach (IMessage msg in oldMessages)
        {
            await msg.DeleteAsync();
            await Task.Delay(200); // prevent rate limits
        }
    }
    
    [RequireOwner]
    [SlashCommand("purgemsgbot", "Delete a number of recent messages sent by the bot", runMode: RunMode.Async)]
    public async Task PurgeBotMessagesAsync(
        [Summary("amount", "Number of bot messages to delete")]
        int amount = 5)
    {
        switch (amount)
        {
            case <= 0:
                await RespondAsync("Please specify a positive number of messages to delete.", ephemeral: true);
                return;
            case > 20:
                amount = 20;
                break;
        }
        
        await RespondAsync($"Purging {amount} bot messages in {Context.Channel.Name}...", ephemeral: true);
        
        ITextChannel channel = (ITextChannel)Context.Channel;
        IEnumerable<IMessage>? messages = await channel.GetMessagesAsync(100).FlattenAsync(); // fetch recent messages
        
        IEnumerable<IMessage> botMessages = messages
            .Where(m => m.Author.Id == Context.Client.CurrentUser.Id)
            .Take(amount);
        
        await PurgeMessagesAsync(channel, botMessages, amount);
        await FollowupAsync($"Deleted {botMessages.Count()} bot messages.", ephemeral: true);
    }
    
    
    //[RequireUserPermission(GuildPermission.ManageMessages)]
    //[RequireBotPermission(GuildPermission.ManageMessages)]
    [RequireOwner]
    [SlashCommand("purgemsg", "Purge all messages from the current channel", runMode: RunMode.Async)]
    public async Task PurgeAsync()
    {
        await RespondAsync($"Purging messages in {Context.Channel.Name}...", ephemeral: true);
        
        ITextChannel channel = (ITextChannel)Context.Channel;
        IEnumerable<IMessage>? messages = await channel.GetMessagesAsync(int.MaxValue).FlattenAsync(); // fetch all messages
        
        await PurgeMessagesAsync(channel, messages);
        await FollowupAsync("Purge complete.", ephemeral: true);
    }
    
    //[RequireUserPermission(GuildPermission.ManageMessages)]
    //[RequireBotPermission(GuildPermission.ManageMessages)]
    [RequireOwner()]
    [SlashCommand("copy", "copy messages from a channel to where the command is executed", runMode: RunMode.Async)]
    public async Task CopyAsync(string channelToCopy = "https://discord.com/channels/824331584319782982/852773540893687808",
        string destinationToPasteTo = "https://discord.com/channels/980745782594535484/1187742715589439570")
    {
        ulong[] from = ReturnGuildAndChannelsIDs(channelToCopy);
        ulong[] to = ReturnGuildAndChannelsIDs(destinationToPasteTo);
        DiscordSocketClient clientToGetMessagesFrom = socketClient;
        SocketTextChannel channelToCopyMessagesFrom = clientToGetMessagesFrom.GetGuild(from[0]).GetTextChannel(from[1]);
        DiscordSocketClient clientToPasteMessagesTo = socketClient;
        SocketTextChannel channelToPasteMessagesFrom = clientToPasteMessagesTo.GetGuild(to[0]).GetTextChannel(to[1]);
        IEnumerable<IMessage> messages = await channelToCopyMessagesFrom.GetMessagesAsync().FlattenAsync();
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
        FileStream outputFileStream = new FileStream(path, FileMode.Create);
        await fileStream.CopyToAsync(outputFileStream);
        return outputFileStream;
    }
    
    private ulong[] ReturnGuildAndChannelsIDs(string link)
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