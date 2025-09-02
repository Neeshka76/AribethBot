using System.ComponentModel.DataAnnotations;

namespace AribethBot.Database;

public class Guild
{
    [Key] public ulong GuildId { get; set; } // Discord guild ID
    public ulong? ChannelDeletedLog { get; set; }
    public ulong? ChannelEditedLog { get; set; }
    public ulong? ChannelEntryOutLog { get; set; }
    public ulong? ChannelBanLog { get; set; }
    public ulong? ChannelVoiceActivityLog { get; set; }

    public List<SpamTrigger> SpamTriggers { get; set; } = new();
}

public enum SpamType
{
    Classic,
    Bot
}

public enum SpamAction
{
    Timeout,
    Kick,
    Ban,
    NoAction,
}

public class SpamTrigger
{
    public ulong GuildId { get; set; } // Foreign key
    public SpamType Type { get; set; } // classic or bot
    public int NbMessages { get; set; }
    public double IntervalTime { get; set; }
    public SpamAction ActionType { get; set; } // timeout, kick or ban
    public int? ActionDuration { get; set; }
    public bool ActionDelete { get; set; }

    public Guild Guild { get; set; }
}