using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.ComponentModel.Design;

namespace AribethBot
{
    public class DebugCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private readonly DiscordSocketClient client;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        // constructor injection is also a valid way to access the dependencies
        public DebugCommands(CommandHandler handler)
        {
            client = handler.socketClient;
            logger = handler.logger;
            httpClient = handler.httpClient;
        }
        //[RequireUserPermission(GuildPermission.ManageMessages)]
        //[SlashCommand("debuglog", "Retrieve the last log of the bot")]
        public async Task DebugLog()
        {
            await DeferAsync();
            string pathOfFolder = "Logs";
            string mostRecentLog = TryGetMostRecentFromList(pathOfFolder);
            if (mostRecentLog == "")
            {
                await FollowupAsync("Cannot get log, something wrong happened !");
            }
            else
            {
                logger.LogInformation(mostRecentLog, LogLevel.Debug);
                if (File.Exists(mostRecentLog))
                {
                    FileInfo fileInfo = new FileInfo(mostRecentLog);
                    logger.LogInformation($"'{fileInfo.FullName}' : '{fileInfo.Name}'", LogLevel.Debug);
                    await FollowupWithFileAsync(fileInfo.FullName, fileInfo.Name);
                }
                else
                {
                    await FollowupAsync($"Failed to grab the file {mostRecentLog}");
                }

            }
        }

        private string TryGetMostRecentFromList(string pathToFiles)
        {
            HashSet<string> listPath = Directory.EnumerateFiles(pathToFiles, "AribethLog*.log", SearchOption.TopDirectoryOnly).ToHashSet();
            string mostRecent = "";
            DateTime mostRecentDate = DateTime.MinValue;
            if (listPath.Count > 0)
            {
                mostRecent = listPath.First();
                FileInfo fileInfo = new FileInfo(mostRecent);
                mostRecentDate = fileInfo.LastWriteTime;
                foreach (string path in listPath)
                {
                    if (TryGetMostRecent(path, mostRecentDate, out string outputRecent, out DateTime outputRecentDate))
                    {
                        mostRecent = outputRecent;
                        mostRecentDate = outputRecentDate;
                    }
                }
            }
            return mostRecent;
        }

        private bool TryGetMostRecent(string filepath, DateTime dateTime, out string mostRecent, out DateTime mostRecentDate)
        {
            if (!File.Exists(filepath))
            {
                mostRecent = "";
                mostRecentDate = DateTime.MinValue;
                return false;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(filepath);
                if (fileInfo.LastWriteTime > dateTime)
                {
                    mostRecent = filepath;
                    mostRecentDate = fileInfo.LastWriteTime;
                    return true;
                }
                else
                {
                    mostRecent = "";
                    mostRecentDate = DateTime.MinValue;
                    return false;
                }
            }
        }

    }
}
