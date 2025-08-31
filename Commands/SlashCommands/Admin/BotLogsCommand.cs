using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace AribethBot.Admin;

public class BotLogsCommand : InteractionModuleBase<SocketInteractionContext>
{
    private const string LogFolder = "Logs"; // relative to bot executable
    private const int MaxButtons = 8; // max log files to show as buttons

    [SlashCommand("logs", "Download recent log files ephemerally.")]
    public async Task LogsAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!Directory.Exists(LogFolder))
        {
            await FollowupAsync("No log folder found.", ephemeral: true);
            return;
        }

        List<string> logFiles = Directory.GetFiles(LogFolder, "*.log")
            .OrderByDescending(File.GetCreationTime)
            .Take(MaxButtons)
            .ToList();

        if (!logFiles.Any())
        {
            await FollowupAsync("No log files found.", ephemeral: true);
            return;
        }

        // Create buttons for each log file
        ComponentBuilder builder = new ComponentBuilder();
        foreach (string file in logFiles)
        {
            string fileName = Path.GetFileName(file);
            builder.WithButton(fileName, $"log_{fileName}", ButtonStyle.Primary);
        }

        IUserMessage? message = await FollowupAsync("Select a log file to download:", components: builder.Build(), ephemeral: true);

        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id) return;

            if (component.User.Id != Context.User.Id)
            {
                await component.RespondAsync("You cannot use this.", ephemeral: true);
                return;
            }

            string selectedFile = component.Data.CustomId.Replace("log_", "");
            string filePath = Path.Combine(LogFolder, selectedFile);

            if (!File.Exists(filePath))
            {
                await component.RespondAsync($"File `{selectedFile}` not found.", ephemeral: true);
                return;
            }

            await component.RespondWithFileAsync(filePath, ephemeral: true);
        }

        Context.Client.ButtonExecuted += Handler;

        // Auto disable buttons after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.ButtonExecuted -= Handler;
            await message.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        });
    }
}