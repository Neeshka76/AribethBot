using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AribethBot
{
    public class ServiceHandler
    {
        private readonly IServiceProvider services;
        public readonly DiscordSocketClient SocketClient;
        private readonly InteractionService interactions;
        public readonly ILogger Logger;
        private readonly CommandService commands;
        public IConfiguration Config;
        public readonly HttpClient HttpClient;
        
        public ServiceHandler(IServiceProvider services)
        {
            this.services = services;
            SocketClient = this.services.GetRequiredService<DiscordSocketClient>();
            interactions = this.services.GetRequiredService<InteractionService>();
            Logger = this.services.GetRequiredService<ILogger<ServiceHandler>>();
            commands = this.services.GetRequiredService<CommandService>();
            Config = this.services.GetRequiredService<IConfiguration>();
            HttpClient = new HttpClient();
            // process the InteractionCreated payloads to execute Interactions commands
            SocketClient.InteractionCreated += HandleInteraction;
            // process the command execution results 
            interactions.SlashCommandExecuted += SlashCommandExecuted;
            interactions.ContextCommandExecuted += ContextCommandExecuted;
            interactions.ComponentCommandExecuted += ComponentCommandExecuted;
            commands.CommandExecuted += Commands_CommandExecuted;
        }
        
        private async Task Commands_CommandExecuted(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result)
        {
            // if a command isn't found, log that info to console and exit this method
            if (!command.IsSpecified)
            {
                Logger.LogError($"Command failed to execute by [{context.User.Username}] on [{context.Guild.Name}] <-> [{result.ErrorReason}]!");
                // failure scenario, let's let the user know
                await context.Channel.SendMessageAsync($"Sorry, {context.User.Username}... something went wrong -> [{result}]!");
                return;
            }
            
            // log success to the console and exit this method
            if (result.IsSuccess)
            {
                Logger.LogInformation($"Command [{command.Value.Name}] executed by [{context.User.Username}] on [{context.Guild.Name}]");
            }
        }
        
        public async Task InitializeAsync()
        {
            // add the public modules that inherit InteractionModuleBase<T> to the InteractionService
            await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        }
        
        private async Task HandleInteraction(SocketInteraction arg)
        {
            try
            {
                SocketInteractionContext ctx = new SocketInteractionContext(SocketClient, arg);
                switch (arg.Type)
                {
                    case InteractionType.ApplicationCommand:
                        // Slash or context command
                        await interactions.ExecuteCommandAsync(ctx, services);
                        break;
                    
                    case InteractionType.MessageComponent:
                        // Button / select menu
                        await interactions.ExecuteCommandAsync(ctx, services);
                        break;
                    
                    default:
                        Logger.LogWarning("Unhandled interaction type: {Type}", arg.Type);
                        break;
                }
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
        
        private Task ComponentCommandExecuted(ComponentCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
        {
            if (!result.IsSuccess)
            {
                /*switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                Logger.LogError($"Command [{commandInfo.Name}] failed to execute by [{context.User.Username}] on [{context.Guild.Name}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                Logger.LogInformation($"Command [{commandInfo.Name}] executed by [{context.User.Username}] on [{context.Guild.Name}]");
            }
            
            return Task.CompletedTask;
        }
        
        private Task ContextCommandExecuted(ContextCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
        {
            if (!result.IsSuccess)
            {
                /*switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                Logger.LogError($"Command [{commandInfo.Name}] failed to execute by [{context.User.Username}] on [{context.Guild.Name}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                Logger.LogInformation($"Command [{commandInfo.Name}] executed by [{context.User.Username}] on [{context.Guild.Name}]");
            }
            
            return Task.CompletedTask;
        }
        
        private Task SlashCommandExecuted(SlashCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
        {
            if (!result.IsSuccess)
            {
                /*switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute by [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                Logger.LogError($"Command [{commandInfo.Name}] failed to execute by [{context.User.Username}] on [{context.Guild.Name}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                Logger.LogInformation($"Command [{commandInfo.Name}] executed by [{context.User.Username}] on [{context.Guild.Name}]");
            }
            
            return Task.CompletedTask;
        }
    }
}