using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using AribethBot.Database;
using Microsoft.EntityFrameworkCore;

namespace AribethBot.Helpers;

public static class GuildHelper
{
    public static async Task EnsureGuildInDbAsync(SocketGuild? guild, DatabaseContext db, ILogger logger)
    {
        if (guild == null) return;
        // Check if the guild exists in the database
        Guild? dbGuild = await db.Guilds.FindAsync(guild.Id);
        
        if (dbGuild == null)
        {
            // Create default guild entry
            dbGuild = new Guild
            {
                GuildId = guild.Id,
                ChannelDeletedLog = null,
                ChannelEditedLog = null,
                ChannelEntryOutLog = null,
                ChannelBanLog = null,
                ChannelVoiceActivityLog = null
            };
            
            await db.Guilds.AddAsync(dbGuild);
            await db.SaveChangesAsync();
            logger.LogInformation($"Created default database entry for guild {guild.Name} ({guild.Id})");
        }
        else
        {
            logger.LogInformation($"Guild {guild.Name} ({guild.Id}) already exists in the database");
        }
        
        // Ensure default spam triggers exist for this guild
        foreach (SpamType type in Enum.GetValues<SpamType>())
        {
            bool existingTrigger = await db.SpamTriggers
                .AnyAsync(s => s.GuildId == guild.Id && s.Type == type);
            
            if (!existingTrigger)
            {
                SpamTrigger trigger = new SpamTrigger
                {
                    GuildId = guild.Id,
                    Type = type,
                    ActionType = type == SpamType.Bot ? SpamAction.Ban : SpamAction.Timeout,
                    ActionDuration = type == SpamType.Classic ? 10 : null, // default timeout minutes
                    ActionDelete = type == SpamType.Bot
                };
                
                await db.SpamTriggers.AddAsync(trigger);
                await db.SaveChangesAsync();
                
                logger.LogInformation($"Created default '{type}' spam trigger for guild {guild.Name} ({guild.Id})");
            }
            else
            {
                logger.LogInformation($"'{type}' spam trigger already exists for guild {guild.Name} ({guild.Id})");
            }
        }
    }
}