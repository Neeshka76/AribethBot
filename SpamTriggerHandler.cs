using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AribethBot
{
    public class SpamTriggerHandler
    {
        private readonly IServiceProvider services;
        private readonly DiscordSocketClient socketClient;
        private readonly InteractionService interactions;
        private readonly IConfiguration config;

        public SpamTriggerHandler(IServiceProvider services)
        {
            this.services = services;
            socketClient = this.services.GetRequiredService<DiscordSocketClient>();
            interactions = this.services.GetRequiredService<InteractionService>();
            config = this.services.GetRequiredService<IConfiguration>();
            // process the InteractionCreated payloads to execute Interactions commands
            socketClient.InteractionCreated += HandleInteraction;
            // process the messages 
            socketClient.MessageReceived += Client_MessageReceived;
            socketClient.PresenceUpdated += SocketClient_PresenceUpdated;
        }

        private async Task HandleInteraction(SocketInteraction arg)
        {
            try
            {
                // create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
                SocketInteractionContext ctx = new SocketInteractionContext(socketClient, arg);
                await interactions.ExecuteCommandAsync(ctx, services);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                // if a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
                // response, or at least let the user know that something went wrong during the command execution.
                if (arg.Type == InteractionType.ApplicationCommand)
                {
                    await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
                }
            }
        }

        private async Task Client_MessageReceived(SocketMessage rawMessage)
        {
            // ensures we don't process system/other bot messages
            if (!(rawMessage is SocketUserMessage message))
            {
                return;
            }
            if (message.Source != MessageSource.User)
            {
                return;
            }

            // sets the argument position away from the prefix we set
            int argPos = 0;

            // get prefix from the configuration file
            char prefix = char.Parse(config["Prefix"]);

            // determine if the message has a valid prefix, and adjust argPos based on prefix
            if (!(message.HasMentionPrefix(socketClient.CurrentUser, ref argPos) || message.HasCharPrefix(prefix, ref argPos)))
            {
                // execute command if one is found that matches
                return;
            }
            await Task.CompletedTask;
        }

        private async Task SocketClient_PresenceUpdated(SocketUser user, SocketPresence presenceBefore, SocketPresence presenceAfter)
        {
            if (presenceBefore == null) return;
            if (presenceAfter == null) return;
            SocketGuildUser guildUser = user as SocketGuildUser;
            if (guildUser == null) return;
            await Task.CompletedTask;
        }
    }
}