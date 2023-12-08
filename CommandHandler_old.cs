using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Interactions;
using Newtonsoft.Json;

namespace AribethBot
{
    public class CommandHandler_old : InteractionModuleBase<SocketInteractionContext>
    {
        private DiscordSocketClient client;
        private DiscordSocketConfig config;
        private ulong guildId;
        Cryptography cryptography;

        public CommandHandler_old(DiscordSocketClient client, DiscordSocketConfig config)
        {
            this.client = client;
            this.config = config;
            guildId = ulong.Parse(Environment.GetEnvironmentVariable("GuildIdNeeshkaModdingServer"));
            cryptography = new Cryptography();
            
            Task.Run(() =>
            {
                client.SlashCommandExecuted += Client_SlashCommandExecuted;
                InteractionService _interactionService = new InteractionService(client.Rest);
                _ = Command();
                Task.Delay(-1);
            });

        }

        private async Task Client_SlashCommandExecuted(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "list-roles":
                    await HandleListRoleCommand(command);
                    break;
                case "decrypt":

                    break;
                case "encrypt":
                    //EncryptMessage(command);
                    break;

            }
        }

        //private async Task EncryptMessage(SocketSlashCommand command)
        //{
        //    string[] options =
        //    {
        //        command.Data.Options.Where(x => x.Options.First(x=> x.Name == "Shift"))
        //    }
        //    cryptography.Encoder(Enum.TryParse(command.Data.Options.First().Value.ToString(), out Cryptography.Cipher cipher), options[0]);
        //    
        //}

        private async Task HandleListRoleCommand(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.
            var guildUser = (SocketGuildUser)command.Data.Options.First().Value;

            // We remove the everyone role and select the mention of each role.
            var roleList = string.Join(",\n", guildUser.Roles.Where(x => !x.IsEveryone).Select(x => x.Mention));

            var embedBuiler = new EmbedBuilder()
                .WithAuthor(guildUser.ToString(), guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
                .WithTitle("Roles")
                .WithDescription(roleList)
                .WithColor(Color.Green)
                .WithCurrentTimestamp();

            // Now, Let's respond with the embed.
            await command.RespondAsync(embed: embedBuiler.Build(), ephemeral: true);
        }

        [SlashCommand("poll", "Create a poll where people can vote")]
        public async Task PollAsync(string Question, string answer1, string answer2, string answer3 = "", string answer4 = "", string answer5 = "",
            string answer6 = "", string answer7 = "", string answer8 = "")
        {
            var pollBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select an option")
                .WithCustomId("menu-1")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption(answer1, "answer-1")
                .AddOption(answer2, "answer-2");

            if (answer3 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer3, $"answer-3");
            }
            if (answer4 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer4, "answer-4");
            }
            if (answer5 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer5, "answer-5");
            }
            if (answer6 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer6, "answer-6");
            }
            if (answer7 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer7, "answer-7");
            }
            if (answer8 is not "")
            {
                pollBuilder = pollBuilder.AddOption(answer8, "answer-8");
            }

            var builder = new ComponentBuilder()
                .WithSelectMenu(pollBuilder);

            await RespondAsync(Question, components: builder.Build());
        }

        public async Task Command()
        {
            // Let's build a guild command! We're going to need a guild so lets just put that in a variable.
            SocketGuild guild = client.GetGuild(guildId);

            // Next, lets create our slash command builder. This is like the embed builder but for slash commands.
            SlashCommandBuilder guildCommand = new SlashCommandBuilder();

            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            guildCommand.WithName("first-command");

            // Descriptions can have a max length of 100.
            guildCommand.WithDescription("This is my first guild slash command!");

            // Let's do our global command
            SlashCommandBuilder globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("first-global-command");
            globalCommand.WithDescription("This is my first global slash command");

            // List the roles of a user
            SlashCommandBuilder guildCommand2 = new SlashCommandBuilder()
            .WithName("list-roles")
            .WithDescription("Lists all roles of a user.")
            .AddOption("user", ApplicationCommandOptionType.User, "The users whos roles you want to be listed", isRequired: true);

            SlashCommandBuilder encryptCommand = new SlashCommandBuilder()
                .WithName("encrypt")
                .WithDescription("Encrypt a message")
                .AddOption("Cipher", ApplicationCommandOptionType.String, "The type of cipher you want to use", isRequired : true)
                .AddOption("Shift", ApplicationCommandOptionType.Integer, "The shift used for the cipher", isRequired : true);

            try
            {
                // Now that we have our builder, we can call the CreateApplicationCommandAsync method to make our slash command.
                await guild.CreateApplicationCommandAsync(guildCommand.Build());

                // With global commands we don't need the guild.
                await client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
                // command that takes in a user and lists their roles.
                await client.Rest.CreateGuildCommand(guildCommand2.Build(), guildId);

            }
            catch (HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }
    }
}
