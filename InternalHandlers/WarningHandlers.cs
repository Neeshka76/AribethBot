using System.Collections.Concurrent;
using System.Globalization;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AribethBot.Database;
using Discord.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AribethBot;

public class WarningHandlers
{
    private readonly DiscordSocketClient socketClient;
    private readonly IConfiguration config;
    private readonly ILogger logger;
    
    public WarningHandlers(IServiceProvider services)
    {
        socketClient = services.GetRequiredService<DiscordSocketClient>();
        socketClient.JoinedGuild += SocketClientOnJoinedGuild;
        socketClient.Ready += SocketClientOnReady ;
        logger = services.GetRequiredService<ILogger<ServiceHandler>>();
        config = services.GetRequiredService<IConfiguration>();
    }
    
    private async Task SocketClientOnReady()
    {
        if (!ulong.TryParse(config["discordIDToWarn"], out ulong ownerId))
            return;
        
        IUser? owner = await socketClient.GetUserAsync(ownerId);
        
        if (owner == null)
            return;
        
        Embed embed = new EmbedBuilder()
            .WithTitle("Bot Back Online")
            .WithColor(Color.Purple)
            .WithCurrentTimestamp()
            .Build();
        
        await owner.SendMessageAsync(embed: embed);
    }
    
    private async Task SocketClientOnJoinedGuild(SocketGuild guild)
    {
        if (!ulong.TryParse(config["discordIDToWarn"], out ulong ownerId))
            return;
        
        IUser? owner = await socketClient.GetUserAsync(ownerId);
        
        if (owner == null)
            return;
        
        SocketGuildUser? guildOwner = guild.Owner;
        
        Embed embed = new EmbedBuilder()
            .WithTitle("Bot Added To New Guild")
            .WithColor(Color.Green)
            .WithThumbnailUrl(guild.IconUrl)
            .AddField("Guild", $"{guild.Name} ({guild.Id})", false)
            .AddField("Members", guild.MemberCount, true)
            .AddField("Owner", $"{guildOwner?.Username} ({guildOwner?.Id})", false)
            .AddField("Verification", guild.VerificationLevel, true)
            .AddField("Created", guild.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), true)
            .WithCurrentTimestamp()
            .Build();
        
        await owner.SendMessageAsync(embed: embed);
    }
}