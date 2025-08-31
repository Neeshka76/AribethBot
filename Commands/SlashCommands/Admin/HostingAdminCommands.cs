using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using AribethBot.Helpers;
using Microsoft.Extensions.Logging;

namespace AribethBot.Admin;

[RequireOwner]
public class HostingAdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private readonly ILogger logger;

    // constructor injection is also a valid way to access the dependencies
    public HostingAdminCommands(ServiceHandler handler)
    {
        logger = handler.Logger;
    }
    
    [SlashCommand("adminpi", "Show admin actions for the Raspberry Pi.")]
    public async Task AdminPiAsync()
    {
        await DeferAsync(ephemeral: true);

        MessageComponent components = new ComponentBuilder()
            .WithButton("Stats", "adminpi_stats", ButtonStyle.Primary)
            .WithButton("Restart Bot", "adminpi_restart", ButtonStyle.Primary)
            .WithButton("Reboot Pi", "adminpi_reboot", ButtonStyle.Danger)
            .WithButton("Update Pi", "adminpi_update", ButtonStyle.Secondary)
            .Build();

        IUserMessage message = await FollowupAsync("Choose an action:", components: components, ephemeral: true);

        // Hook button clicks
        Task Handler(SocketMessageComponent component) => HandleButtonClick(component, message);

        Context.Client.ButtonExecuted += Handler;

        // Auto-remove handler after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.ButtonExecuted -= Handler;
            await message.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        });
    }

    private async Task HandleButtonClick(SocketMessageComponent component, IUserMessage message)
    {
        if (component.Message.Id != message.Id) return;

        if (component.User.Id != Context.User.Id)
        {
            await component.RespondAsync("You cannot use this.", ephemeral: true);
            return;
        }

        string commandName = component.Data.CustomId;
        string user = component.User.Username;
        string guild = Context.Guild?.Name ?? "DM";

        try
        {
            switch (commandName)
            {
                case "adminpi_stats":
                    await component.DeferAsync(ephemeral: true);
                    List<Embed> pages = BuildStatsEmbeds();
                    await ButtonPaginator.SendPaginatedEmbedsAsync(Context, pages, "Raspberry Pi Stats", true);
                    break;

                case "adminpi_restart":
                    await component.RespondAsync("Restarting bot...", ephemeral: true);
                    RunCommand("sudo systemctl restart AribethBot");
                    break;

                case "adminpi_reboot":
                    await component.RespondAsync("Rebooting Raspberry Pi...", ephemeral: true);
                    RunCommand("sudo reboot");
                    break;

                case "adminpi_update":
                    await component.RespondAsync("Updating Raspberry Pi...", ephemeral: true);
                    RunCommand("sudo apt update && sudo apt upgrade -y");
                    break;

                default:
                    await component.RespondAsync("Unknown button.", ephemeral: true);
                    logger.LogWarning($"Unknown button [{commandName}] pressed by [{user}] on [{guild}]");
                    return;
            }

            // Log execution for every valid button
            logger.LogInformation($"Command [{commandName}] executed by [{user}] on [{guild}]");
        }
        catch (Exception ex)
        {
            await component.RespondAsync($"Error: {ex.Message}", ephemeral: true);
            logger.LogError(ex, $"Button [{commandName}] failed execution by [{user}] on [{guild}]");
        }
    }

    private string RunCommand(string cmd)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{cmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    private string GetCpuTemp()
    {
        try
        {
            string tempRaw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
            if (int.TryParse(tempRaw, out int millidegrees))
                return $"{millidegrees / 1000.0:F1} °C";
        }
        catch { }
        return "N/A";
    }

    private string GetCpuUsage()
    {
        try
        {
            string result = RunCommand("top -bn1 | grep '%Cpu' | awk '{print $2+$4}'");
            if (double.TryParse(result.Trim().Replace(',', '.'), out double usage))
                return $"{usage:F1}%";
        }
        catch { }
        return "N/A";
    }

    private List<Embed> BuildStatsEmbeds()
    {
        string uptime = RunCommand("uptime -p").Trim();
        string load = RunCommand("uptime").Split("load average:").Last().Trim();
        string cpuTemp = GetCpuTemp();
        string cpuUsage = GetCpuUsage();

        // Memory
        string memoryRaw = RunCommand("free -h");
        string[] memLines = memoryRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        StringBuilder memoryFormatted = new StringBuilder();
        if (memLines.Length >= 2)
        {
            string[] memValues = memLines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            memoryFormatted.AppendLine($"Total      : {memValues[1]}");
            memoryFormatted.AppendLine($"Used       : {memValues[2]}");
            memoryFormatted.AppendLine($"Free       : {memValues[3]}");
            memoryFormatted.AppendLine($"Shared     : {memValues[4]}");
            memoryFormatted.AppendLine($"Buff/Cache : {memValues[5]}");
            memoryFormatted.AppendLine($"Available  : {memValues[6]}");
        }
        if (memLines.Length >= 3)
        {
            string[] swapValues = memLines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            memoryFormatted.AppendLine($"Swap Total : {swapValues[1]}");
            memoryFormatted.AppendLine($"Swap Used  : {swapValues[2]}");
            memoryFormatted.AppendLine($"Swap Free  : {swapValues[3]}");
        }

        // Disk
        string diskRaw = RunCommand("df -h /");
        string[] diskLines = diskRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        StringBuilder diskFormatted = new StringBuilder();
        if (diskLines.Length >= 2)
        {
            string[] diskValues = diskLines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            diskFormatted.AppendLine($"Filesystem : {diskValues[0]}");
            diskFormatted.AppendLine($"Size       : {diskValues[1]}");
            diskFormatted.AppendLine($"Used       : {diskValues[2]}");
            diskFormatted.AppendLine($"Available  : {diskValues[3]}");
            diskFormatted.AppendLine($"Use%       : {diskValues[4]}");
            diskFormatted.AppendLine($"Mounted on : {diskValues[5]}");
        }

        // Top processes
        string topRaw = RunCommand("ps -eo pid,comm,%cpu,%mem --sort=-%cpu | head -n 4").Trim();
        string topFormatted = FormatTopProcesses(topRaw);

        // Build fields
        List<(string name, string value)> fields = new List<(string name, string value)>
        {
            ("Uptime & Load", $"```---------- Uptime & Load ----------\nUptime: {uptime}\nLoad Avg: {load}\nCPU Temp: {cpuTemp}\nCPU Usage: {cpuUsage}```"),
            ("Memory Usage", $"```---------- Memory Usage ----------\n{memoryFormatted}```"),
            ("Disk Usage (/)", $"```---------- Disk Usage (/) ----------\n{diskFormatted}```"),
            ("Top Processes", $"```---------- Top Processes ----------\n{topFormatted}```")
        };

        return SplitFieldsToEmbeds(fields, "Raspberry Pi Status", Color.DarkGreen);
    }

    private string FormatTopProcesses(string topRaw)
    {
        string[] lines = topRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return "";

        List<string[]> rows = new List<string[]>();
        foreach (string line in lines)
        {
            string[] cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            rows.Add(cols);
        }

        int colCount = rows.Max(r => r.Length);
        int[] colWidths = new int[colCount];
        for (int i = 0; i < colCount; i++)
        {
            colWidths[i] = rows.Max(r => i < r.Length ? r[i].Length : 0);
        }

        StringBuilder sb = new StringBuilder();
        foreach (string[] row in rows)
        {
            for (int i = 0; i < colCount; i++)
            {
                string value = i < row.Length ? row[i] : "";
                sb.Append(i < 2 ? value.PadRight(colWidths[i] + 1) : value.PadLeft(colWidths[i] + 1));
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private List<Embed> SplitFieldsToEmbeds(List<(string name, string value)> fields, string title, Color color)
    {
        List<Embed> embeds = new List<Embed>();
        EmbedBuilder builder = new EmbedBuilder().WithTitle(title).WithColor(color);

        foreach ((string name, string value) in fields)
        {
            builder.AddField(name, value, false);

            if (builder.Fields.Sum(f => f.Value.ToString().Length) <= 4000) continue;
            embeds.Add(builder.Build());
            builder = new EmbedBuilder().WithTitle(title).WithColor(color);
        }

        if (builder.Fields.Count > 0)
            embeds.Add(builder.Build());

        return embeds;
    }
}
