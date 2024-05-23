﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AribethBot
{
    public class CommandHandler
    {
        private readonly IServiceProvider services;
        public readonly DiscordSocketClient socketClient;
        public readonly InteractionService interactions;

        public readonly DiscordSocketConfig socketConfig;
        public readonly ILogger logger;
        public readonly CommandService commands;
        public IConfiguration config;
        public readonly HttpClient httpClient;
        public readonly TriggerHandler triggerHandler;
        public readonly DiscordLogger discordLogger;

        public CommandHandler(IServiceProvider services)
        {
            this.services = services;
            socketClient = this.services.GetRequiredService<DiscordSocketClient>();
            interactions = this.services.GetRequiredService<InteractionService>();
            socketConfig = this.services.GetRequiredService<DiscordSocketConfig>();
            logger = this.services.GetRequiredService<ILogger<CommandHandler>>();
            commands = this.services.GetRequiredService<CommandService>();
            config = this.services.GetRequiredService<IConfiguration>();
            httpClient = new HttpClient();
            triggerHandler = this.services.GetRequiredService<TriggerHandler>();
            discordLogger = this.services.GetRequiredService<DiscordLogger>();
            // process the InteractionCreated payloads to execute Interactions commands
            socketClient.InteractionCreated += HandleInteraction;
            // process the command execution results 
            interactions.SlashCommandExecuted += SlashCommandExecuted;
            interactions.ContextCommandExecuted += ContextCommandExecuted;
            interactions.ComponentCommandExecuted += ComponentCommandExecuted;
            commands.CommandExecuted += Commands_CommandExecuted;
        }

        private bool HasRole(SocketGuildUser guildUser, string roleString)
        {
            foreach (SocketRole role in guildUser.Roles)
            {
                if (roleString == role.Name)
                {
                    return true;
                }
            }
            return false;
        }

        private async Task Commands_CommandExecuted(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result)
        {
            // if a command isn't found, log that info to console and exit this method
            if (!command.IsSpecified)
            {
                logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                // failure scenario, let's let the user know
                await context.Channel.SendMessageAsync($"Sorry, {context.User.Username}... something went wrong -> [{result}]!");
                return;
            }


            // log success to the console and exit this method
            if (result.IsSuccess)
            {
                logger.LogInformation($"Command [{command.Value.Name}] executed for [{context.User.Username}] on [{context.Guild.Name}]");
                return;
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
                // create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
                var ctx = new SocketInteractionContext(socketClient, arg);
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

        private Task ComponentCommandExecuted(ComponentCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
        {
            if (!result.IsSuccess)
            {
                /*switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                logger.LogError($"Command [{commandInfo.Name}] failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                logger.LogInformation($"Command [{commandInfo.Name}] executed for [{context.User.Username}] on [{context.Guild.Name}]");
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
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                logger.LogError($"Command [{commandInfo.Name}] failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                logger.LogInformation($"Command [{commandInfo.Name}] executed for [{context.User.Username}] on [{context.Guild.Name}]");
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
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.UnknownCommand:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.BadArgs:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Exception:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    case InteractionCommandError.Unsuccessful:
                        logger.LogError($"Command failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
                        break;
                    default:
                        break;
                }*/
                logger.LogError($"Command [{commandInfo.Name}] failed to execute for [{context.User.Username}] <-> [{result.ErrorReason}]!");
            }
            else
            {
                logger.LogInformation($"Command [{commandInfo.Name}] executed for [{context.User.Username}] on [{context.Guild.Name}]");
            }
            return Task.CompletedTask;
        }
    }
}
