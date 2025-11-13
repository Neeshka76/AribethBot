using AribethBot.Database;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace AribethBot.Helpers;

public static class JsonToDbMigrator
{
    public static void MigrateGuildsAndSpamTriggers(DatabaseContext db, IConfiguration config, string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"Config file not found at {jsonFilePath}");
        }
        
        // Read JSON
        string jsonContent = File.ReadAllText(jsonFilePath);
        JObject jsonObj = JObject.Parse(jsonContent);
        
        // Load global spam settings
        int classicLimit = jsonObj["nbMessagesSpamTriggerClassic"]?.Value<int>() ?? 10;
        double classicInterval = jsonObj["intervalTimeSpamTriggerClassic"]?.Value<double>() ?? 0.5;
        int botLimit = jsonObj["nbMessagesSpamTriggerBot"]?.Value<int>() ?? 3;
        double botInterval = jsonObj["intervalTimeSpamTriggerBot"]?.Value<double>() ?? 10.0;
        
        // Loop through each guild
        JObject? guildsJson = jsonObj["guilds"] as JObject;
        if (guildsJson == null) return;
        
        foreach (KeyValuePair<string, JToken?> guildPair in guildsJson)
        {
            ulong guildId = ulong.Parse(guildPair.Key);
            JToken? guildJson = guildPair.Value;
            
            // Skip if guild already exists in DB
            if (db.Guilds.Any(g => g.GuildId == guildId)) continue;
            
            // Parse channels
            ulong ParseChannel(string key)
            {
                string raw = guildJson[key]?.ToString() ?? "0";
                return ulong.TryParse(raw, out ulong result) ? result : 0;
            }
            
            Guild guildEntity = new Guild
            {
                GuildId = guildId,
                ChannelDeletedLog = ParseChannel("channelDeletedLog"),
                ChannelEditedLog = ParseChannel("channelEditedLog"),
                ChannelEntryOutLog = ParseChannel("channelEntryOutLog"),
                ChannelBanLog = ParseChannel("channelBanLog"),
                ChannelVoiceActivityLog = ParseChannel("channelVoiceActivityLog")
            };
            
            db.Guilds.Add(guildEntity);
            
            // Add default spam triggers
            db.SpamTriggers.Add(new SpamTrigger
            {
                GuildId = guildId,
                Type = SpamType.Classic,
                NbMessages = classicLimit,
                IntervalTime = classicInterval,
                ActionType = SpamAction.Timeout // default classic action
            });
            
            db.SpamTriggers.Add(new SpamTrigger
            {
                GuildId = guildId,
                Type = SpamType.Bot,
                NbMessages = botLimit,
                IntervalTime = botInterval,
                ActionType = SpamAction.Ban // default bot action
            });
        }
        
        db.SaveChanges();
    }
}