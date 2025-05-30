using Discord;
using Discord.WebSocket;

namespace AribethBot
{
    public class TriggerCommands
    {
        DiscordSocketClient client;
        SocketUserMessage message;
        SocketGuildUser user;
        string[] listToFilter =
        [
            "jujutsu kaisen",
            "jjk"
        ];

        public TriggerCommands(DiscordSocketClient client, SocketUserMessage message, SocketGuildUser user)
        {
            this.client = client;
            this.message = message;
            this.user = user;
            foreach (string filter in listToFilter)
            {
                string selection = SearchCommand(filter);
                if (string.IsNullOrEmpty(selection)) continue;
                _ = RunResponse(selection);
            }
        }

        private async Task NoCommands()
        {
            await Task.CompletedTask;
        }

        private async Task RunResponse(string selection)
        {
            switch (selection)
            {
                case "jjk":
                    await JJKTrigger();
                    break;
                case "jujutsu kaisen":
                    await JJKTrigger();
                    break;
                default:
                    await NoCommands();
                    break;
            }
            await Task.CompletedTask;
        }

        private async Task JJKTrigger()
        {
            await message.ReplyAsync($"NO ! Enough with this !");
            IEmote[] emotes =
            {
                new Emoji("\U0001F1F3"),
                new Emoji("\U0001F1F4"),
                new Emoji("\U00002757")
            };
            await message.AddReactionsAsync(emotes);
        }

        private string SearchCommand(string research)
        {
            if (research == "")
                return research;
            if (message.ToString().ToLower().Contains(research))
            {
                return research;
            }
            return "";
        }
    }
}