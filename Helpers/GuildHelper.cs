using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using AribethBot.Database;
using Discord;
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
    
    public static async Task<List<Embed>> BuildServerStatsEmbeds(SocketGuild guild)
    {
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
        
        return await BuildEmbedsFromFields(fields, guild.Name);
        
        void AppendChannelInfo(IChannel? channel, string label)
        {
            if (channel != null) channelBuilder.AppendLine($"{label} : {(channel is ITextChannel text ? text.Mention : channel.Name)}");
        }
    }
    
    public static async Task<List<Embed>> BuildUserInfoEmbeds(
        SocketGuildUser user,
        SocketGuild guild)
    {
        SocketGuildUser contextUser = user;
        user ??= contextUser;
        
        string userMention = user.Mention;
        string userName = user.Username;
        string avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
        
        // --- Base Info ---
        StringBuilder baseBuilder = new StringBuilder();
        baseBuilder.AppendLine($"Mention: {userMention}");
        baseBuilder.AppendLine($"Name : {userName}");
        baseBuilder.AppendLine($"Account created at : {user.CreatedAt:g}");
        baseBuilder.AppendLine($"Account joined at : {user.JoinedAt:g}");
        baseBuilder.AppendLine($"Id : {user.Id}");
        baseBuilder.AppendLine($"Discriminator : {user.Discriminator}");
        baseBuilder.AppendLine($"Hierarchy in server : {GetHierarchyDisplay(user, guild)}");
        
        if (user.Activities.Count > 0)
        {
            baseBuilder.AppendLine("\nActivity:");
            foreach (IActivity activity in user.Activities)
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
                if (!string.IsNullOrEmpty(line)) baseBuilder.AppendLine(line);
            }
        }
        
        // --- Roles ---
        List<SocketRole> userRoles = user.Roles
            .Where(r => !r.IsEveryone)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        StringBuilder roleBuilder = new StringBuilder();
        roleBuilder.AppendLine($"Total Roles: {userRoles.Count}");
        foreach (SocketRole role in userRoles)
        {
            roleBuilder.Append($"- {role}");
            if (role.IsMentionable) roleBuilder.Append(" (***@***)");
            roleBuilder.AppendLine();
        }
        
        // --- Collect fields ---
        List<(string Name, string Value, bool Inline)> fields = new()
        {
            ("User Info", baseBuilder.ToString(), false),
            ("Roles", roleBuilder.ToString(), false)
        };
        
        // --- Build embeds ---
        List<Embed> pages = await BuildEmbedsFromFields(fields, userName);
        
        // --- Add avatar & mention to first embed ---
        for (int i = 0; i < pages.Count; i++)
        {
            Embed oldEmbed = pages[i];
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(oldEmbed.Title)
                .WithDescription(oldEmbed.Description)
                .WithFooter(oldEmbed.Footer?.Text, oldEmbed.Footer?.IconUrl)
                .WithColor(new Color(0, 255, 255)) // cyan
                .WithThumbnailUrl(avatarUrl)
                .WithAuthor(userName, avatarUrl, avatarUrl);
            
            // Copy fields
            foreach (EmbedField field in oldEmbed.Fields)
                builder.AddField(field.Name, field.Value, field.Inline);
            
            pages[i] = builder.Build(); // replace the old embed
        }

        return pages;
    }

    public static async Task<List<Embed>> BuildEmbedsFromFields(
        List<(string Name, string Value, bool Inline)> fields,
        string title)
    {
        const int MaxField = 1024;

        List<Embed> embeds = new();

        EmbedBuilder builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        foreach ((string name, string value, bool inline) in fields)
        {
            if (value.Length <= MaxField)
            {
                builder.AddField(name, value, inline);
                continue;
            }

            IEnumerable<string> chunks = ChunkString(value, MaxField);

            int index = 1;

            foreach (string chunk in chunks)
            {
                builder.AddField(
                    $"{name} ({index++})",
                    chunk,
                    inline);
            }
        }

        embeds.Add(builder.Build());

        return await Task.FromResult(embeds);
    }
    
    private static string GetHierarchyDisplay(SocketGuildUser user, SocketGuild guild)
    {
        // Exclude @everyone
        List<SocketRole> rolesOrdered = guild.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        int totalRoles = rolesOrdered.Count;
        
        // Owner special case
        if (guild.OwnerId == user.Id)
        {
            return $"Owner (0/{totalRoles})";
        }
        
        // Get user roles (excluding @everyone)
        List<SocketRole> userRoles = user.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        // Roleless = bottom
        if (!userRoles.Any())
        {
            return $"No Roles ({totalRoles}/{totalRoles})";
        }
        
        // Highest role
        SocketRole highestRole = userRoles.First();
        int index = rolesOrdered.IndexOf(highestRole) + 1; // 1-based index
        
        return $"{highestRole.Name} ({index}/{totalRoles})";
    }
    
    private static int GetHierarchyIndex(SocketGuildUser user, SocketGuild guild)
    {
        // Exclude @everyone role
        List<SocketRole> rolesOrdered = guild.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        int totalRoles = rolesOrdered.Count;
        
        if (guild.OwnerId == user.Id)
        {
            return 0; // Owner always above
        }
        
        List<SocketRole> userRoles = user.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        if (!userRoles.Any()) return totalRoles; // Roleless = bottom
        SocketRole highestRole = userRoles.First();
        return rolesOrdered.IndexOf(highestRole) + 1; // 1-based index
    }
    
    private static string GetHierarchyRoleMention(SocketGuildUser user, SocketGuild guild)
    {
        // Owner always first
        if (guild.OwnerId == user.Id)
        {
            return "Owner";
        }
        
        // Exclude @everyone
        List<SocketRole> userRoles = user.Roles
            .Where(r => r.Id != guild.Id)
            .OrderByDescending(r => r.Position)
            .ToList();
        
        if (!userRoles.Any())
        {
            return "No Roles";
        }
        
        // Return highest role as a clickable mention
        SocketRole highestRole = userRoles.First();
        return highestRole.Mention;
    }
    
    //private static Task<List<Embed>> BuildEmbedsFromFields(List<(string Name, string Value, bool Inline)> fields, string title)
    //{
    //    const int MaxEmbedFieldLength = 1024;
    //    const int MaxEmbedTotalLength = 6000;
    //    
    //    List<Embed> embeds = new List<Embed>();
    //    
    //    foreach ((string Name, string Value, bool Inline) field in fields)
    //    {
    //        List<Embed> categoryEmbeds = new List<Embed>();
    //        EmbedBuilder builder = new EmbedBuilder()
    //            .WithTitle(title)
    //            .WithColor(new Color(0, 255, 255))
    //            .WithCurrentTimestamp();
    //        
    //        int currentLength = 0;
    //        string[] lines = field.Value.Split('\n');
    //        StringBuilder chunkBuilder = new StringBuilder();
    //        int chunkIndex = 0;
    //        
    //        foreach (string line in lines)
    //        {
    //            if (chunkBuilder.Length + line.Length + 1 > MaxEmbedFieldLength)
    //            {
    //                builder.AddField(
    //                    field.Name + (chunkIndex > 0 ? $" (cont.)" : ""),
    //                    chunkBuilder.ToString(),
    //                    field.Inline
    //                );
    //                currentLength += chunkBuilder.Length;
    //                
    //                if (currentLength > MaxEmbedTotalLength)
    //                {
    //                    categoryEmbeds.Add(builder.Build());
    //                    builder = new EmbedBuilder()
    //                        .WithTitle(title)
    //                        .WithColor(new Color(0, 255, 255))
    //                        .WithCurrentTimestamp();
    //                    currentLength = 0;
    //                }
    //                
    //                chunkBuilder.Clear();
    //                chunkIndex++;
    //            }
    //            
    //            if (chunkBuilder.Length > 0)
    //                chunkBuilder.AppendLine();
    //            chunkBuilder.Append(line);
    //        }
    //        
    //        if (chunkBuilder.Length > 0)
    //        {
    //            builder.AddField(
    //                field.Name + (chunkIndex > 0 ? $" (cont.)" : ""),
    //                chunkBuilder.ToString(),
    //                field.Inline
    //            );
    //            currentLength += chunkBuilder.Length;
    //            
    //            if (currentLength > MaxEmbedTotalLength)
    //            {
    //                categoryEmbeds.Add(builder.Build());
    //                builder = new EmbedBuilder()
    //                    .WithTitle(title)
    //                    .WithColor(new Color(0, 255, 255))
    //                    .WithCurrentTimestamp();
    //            }
    //        }
    //        
    //        if (builder.Fields.Count > 0)
    //            categoryEmbeds.Add(builder.Build());
    //        
    //        // If the category has multiple pages, add numbering in title
    //        if (categoryEmbeds.Count > 1)
    //        {
    //            for (int i = 0; i < categoryEmbeds.Count; i++)
    //            {
    //                EmbedBuilder numbered = categoryEmbeds[i].ToEmbedBuilder();
    //                numbered.Title = $"{title} - {field.Name} (page {i + 1}/{categoryEmbeds.Count})";
    //                categoryEmbeds[i] = numbered.Build();
    //            }
    //        }
    //        else if (categoryEmbeds.Count == 1)
    //        {
    //            EmbedBuilder single = categoryEmbeds[0].ToEmbedBuilder();
    //            single.Title = $"{title} - {field.Name}";
    //            categoryEmbeds[0] = single.Build();
    //        }
    //        
    //        embeds.AddRange(categoryEmbeds);
    //    }
    //    
    //    // Add global page numbers in footer
    //    for (int i = 0; i < embeds.Count; i++)
    //    {
    //        EmbedBuilder numbered = embeds[i].ToEmbedBuilder();
    //        numbered.Footer = new EmbedFooterBuilder { Text = $"Page {i + 1}/{embeds.Count}" };
    //        embeds[i] = numbered.Build();
    //    }
    //    
    //    return Task.FromResult(embeds);
    //}
    

    private static IEnumerable<string> ChunkString(
        string str,
        int maxLength)
    {
        for (int i = 0; i < str.Length; i += maxLength)
        {
            yield return str.Substring(
                i,
                Math.Min(maxLength, str.Length - i));
        }
    }
    
}