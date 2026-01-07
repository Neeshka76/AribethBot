using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AribethBot.Database;
using Microsoft.EntityFrameworkCore;
namespace AribethBot;

public class ReactionHandler
{
    private readonly DiscordSocketClient socketClient;
    private readonly DatabaseContext db;
    private readonly ILogger logger;
    
    // Unicode emojis as strings
    List<string> blockedEmojis = ["🫄","🫃","🍆","🍑"];
    
    // For custom emojis, use their name or ID
    // var blockedCustomEmojis = new List<string> { "cake", "123456789012345678" };
    
    public ReactionHandler(IServiceProvider services)
    {
        socketClient = services.GetRequiredService<DiscordSocketClient>();
        db = services.GetRequiredService<DatabaseContext>();
        logger = services.GetRequiredService<ILogger<SpamTriggerHandler>>();
        socketClient.ReactionAdded += HandleReactionAddedAsync;
    }
    
    private async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cacheable,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        // Ignore bot's own reactions
        if (reaction.UserId == socketClient.CurrentUser.Id)
            return;
        
        // Check for unicode emoji
        if (reaction.Emote is Emoji emoji)
        {
            if (blockedEmojis.Contains(emoji.Name))
            {
                IUserMessage? message = await cacheable.GetOrDownloadAsync();
                if (channel.Value is SocketGuildChannel guildChannel)
                    logger.LogInformation(
                        $"Emoji {emoji} reacted by {reaction.User} ({reaction.UserId}) deleted in {message.Channel.Name} " +
                        $"(Guild: {guildChannel.Guild.Name} [{guildChannel.Guild.Id}])."
                        );
                await message.RemoveReactionAsync(emoji, reaction.User.Value);
            }
        }
        
        // Check for custom emoji
        //if (reaction.Emote is Emote customEmote)
        //{
        //    // If you want to block custom emojis by name or ID
        //    if (blockedCustomEmojis.Contains(customEmote.Name) || blockedCustomEmojis.Contains(customEmote.Id.ToString()))
        //    {
        //        IUserMessage? message = await cacheable.GetOrDownloadAsync();
        //        await message.RemoveReactionAsync(customEmote, reaction.User.Value);
        //    }
        //}
    }
    
}