using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AribethBot.Database;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using AribethBot.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AribethBot.Admin;

[RequireOwner]
[Group("bot_owner", "Commands related to bot management")]
public class BotOwnerCommands : InteractionModuleBase<SocketInteractionContext>
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private const string LogFolder = "Logs"; // relative to bot executable
    private const int MaxButtons = 8; // max log files to show as buttons
    private const int NumberOfLogToFetch = 31; // number of logs to fetch
    
    // constructor injection is also a valid way to access the dependencies
    public BotOwnerCommands(ServiceHandler handler, IServiceScopeFactory scopeFactory)
    {
        logger = handler.Logger;
        this.scopeFactory = scopeFactory;
    }
    
    [SlashCommand("adminpi", "Show admin actions for the Raspberry Pi.")]
    public async Task AdminPiAsync()
    {
        await DeferAsync(ephemeral: true);
        
        MessageComponent components = new ComponentBuilder()
            .WithButton("Stats", "adminpi_stats")
            .WithButton("Restart Bot", "adminpi_restart")
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
                    await RunPiUpdate(component);
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
    
    private async Task RunPiUpdate(SocketMessageComponent component)
    {
        await component.ModifyOriginalResponseAsync(msg => { msg.Content = "Starting Raspberry Pi update..."; });
        
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"sudo apt update && sudo DEBIAN_FRONTEND=noninteractive apt upgrade -y\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };
        StringBuilder recentOutput = new StringBuilder();
        HashSet<string> processedPackages = new HashSet<string>();
        int totalPackages = 0;
        DateTime lastEdit = DateTime.MinValue;
        
        async Task UpdateDiscordMessage(string latestLine)
        {
            // Throttle edits
            if ((DateTime.UtcNow - lastEdit).TotalSeconds < 3)
                return;
            lastEdit = DateTime.UtcNow;
            try
            {
                string trimmedOutput = TrimOutput(recentOutput.ToString());
                
                await component.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content =
                        $"Updating Raspberry Pi...\n\n" +
                        $"Processed Packages: {processedPackages.Count}" +
                        (totalPackages > 0 ? $"/{totalPackages}" : "") +
                        $"\n\n" +
                        $"Latest Activity:\n" +
                        $"```bash\n{latestLine}\n```\n\n" +
                        $"Recent Output:\n" +
                        $"```bash\n{trimmedOutput}\n```";
                });
            }
            catch
            {
                // ignored
            }
        }
        
        process.OutputDataReceived += async (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            string line = e.Data.Trim();
            recentOutput.AppendLine(line);
            // Keep buffer reasonable
            if (recentOutput.Length > 5000)
            {
                recentOutput.Remove(0, 2000);
            }
            
            // Detect total package count
            // Example:
            // 83 upgraded, 0 newly installed...
            Match totalMatch = Regex.Match(line, @"(\d+)\s+upgraded");
            
            if (totalMatch.Success)
            {
                totalPackages = int.Parse(totalMatch.Groups[1].Value);
            }
            
            // Detect processed packages
            // Example:
            // Unpacking libc6:arm64 ...
            // Setting up libc6:arm64 ...
            Match packageMatch = Regex.Match(line, @"^(Unpacking|Setting up)\s+([^\s:]+)");
            if (packageMatch.Success)
            {
                string packageName = packageMatch.Groups[2].Value;
                processedPackages.Add(packageName);
            }
            
            await UpdateDiscordMessage(line);
        };
        
        process.ErrorDataReceived += async (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            string line = $"[ERR] {e.Data.Trim()}";
            recentOutput.AppendLine(line);
            if (recentOutput.Length > 5000)
            {
                recentOutput.Remove(0, 2000);
            }
            
            await UpdateDiscordMessage(line);
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        string finalMessage =
            process.ExitCode == 0
                ? "Raspberry Pi update completed successfully."
                : $"Raspberry Pi update failed with exit code {process.ExitCode}.";
        try
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Content =
                    $"{finalMessage}\n\n" +
                    $"Processed Packages: {processedPackages.Count}" +
                    (totalPackages > 0 ? $"/{totalPackages}" : "");
            });
        }
        catch
        {
            // ignored
        }
    }
    
    private string TrimOutput(string text, int maxLength = 1200)
    {
        return text.Length <= maxLength ? text : text[^maxLength..];
    }
    
    private string GetCpuTemp()
    {
        try
        {
            string tempRaw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
            if (int.TryParse(tempRaw, out int millidegrees))
                return $"{millidegrees / 1000.0:F1} °C";
        }
        catch
        {
            // ignored
        }
        
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
        catch
        {
            // ignored
        }
        
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
            builder.AddField(name, value);
            
            if (builder.Fields.Sum(f => f.Value.ToString().Length) <= 4000) continue;
            embeds.Add(builder.Build());
            builder = new EmbedBuilder().WithTitle(title).WithColor(color);
        }
        
        if (builder.Fields.Count > 0)
            embeds.Add(builder.Build());
        
        return embeds;
    }
    
    [SlashCommand("botlogs", "Download recent log files ephemerally.")]
    public async Task BotLogsAsync()
    {
        await DeferAsync(ephemeral: true);
        
        if (!Directory.Exists(LogFolder))
        {
            await FollowupAsync("No log folder found.", ephemeral: true);
            return;
        }
        
        List<string> logFiles = Directory.GetFiles(LogFolder, "*.log")
            .OrderByDescending(File.GetCreationTime)
            .Take(NumberOfLogToFetch)
            .ToList();
        
        if (!logFiles.Any())
        {
            await FollowupAsync("No log files found.", ephemeral: true);
            return;
        }
        
        int currentPage = 0;
        int totalPages = (int)Math.Ceiling(logFiles.Count / (double)MaxButtons);
        
        // Build buttons for a specific page
        MessageComponent BuildButtons(int page)
        {
            ComponentBuilder builder = new ComponentBuilder();
            
            foreach (string file in logFiles.Skip(page * MaxButtons).Take(MaxButtons))
            {
                string fileName = Path.GetFileName(file);
                string buttonId = $"log_{Uri.EscapeDataString(fileName)}"; // Safe ID
                builder.WithButton(fileName, buttonId, ButtonStyle.Primary);
            }
            
            // Pagination buttons
            builder.WithButton("⏮️ Prev", "logs_prev", disabled: page == 0);
            builder.WithButton("⏭️ Next", "logs_next", disabled: page == totalPages - 1);
            
            return builder.Build();
        }
        
        IUserMessage message = await FollowupAsync(
            $"Showing logs page {currentPage + 1}/{totalPages}:",
            components: BuildButtons(currentPage),
            ephemeral: true
        );
        
        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id) return;
            if (component.User.Id != Context.User.Id)
            {
                await component.FollowupAsync("You cannot use this.", ephemeral: true);
                return;
            }
            
            try
            {
                switch (component.Data.CustomId)
                {
                    case "logs_prev":
                        currentPage = Math.Max(currentPage - 1, 0);
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = $"Showing logs page {currentPage + 1}/{totalPages}:";
                            msg.Components = BuildButtons(currentPage);
                        });
                        return;
                    
                    case "logs_next":
                        currentPage = Math.Min(currentPage + 1, totalPages - 1);
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = $"Showing logs page {currentPage + 1}/{totalPages}:";
                            msg.Components = BuildButtons(currentPage);
                        });
                        return;
                    
                    default:
                        // File button
                        string selectedFile = Uri.UnescapeDataString(component.Data.CustomId.Replace("log_", ""));
                        string filePath = Path.Combine(LogFolder, selectedFile);
                        
                        if (!File.Exists(filePath))
                        {
                            await component.FollowupAsync($"File `{selectedFile}` not found.", ephemeral: true);
                            return;
                        }
                        
                        // Update the same ephemeral message with the file sent
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = $"Downloading `{selectedFile}`...";
                            msg.Components = BuildButtons(currentPage);
                        });
                        
                        await component.FollowupWithFileAsync(filePath, ephemeral: true);
                        return;
                }
            }
            catch
            {
                // Ignore individual errors
            }
        }
        
        Context.Client.ButtonExecuted += Handler;
        
        // Auto-disable buttons after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.ButtonExecuted -= Handler;
            await message.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        });
    }
    
    [SlashCommand("listspamsettings", "List all spam trigger settings for servers the bot is in")]
    public async Task ListSpamSettings()
    {
        await DeferAsync(ephemeral: true);
        IReadOnlyCollection<SocketGuild>? guilds = Context.Client.Guilds;
        if (!guilds.Any())
        {
            await FollowupAsync("The bot is not in any guilds.", ephemeral: true);
            return;
        }
        
        // Build buttons for each guild
        ComponentBuilder componentBuilder = new ComponentBuilder();
        foreach (SocketGuild guild in guilds.Take(25)) // Discord limits to 25 components per message
        {
            componentBuilder.WithButton(
                label: guild.Name.Length > 80 ? guild.Name.Substring(0, 77) + "..." : guild.Name, // Discord button label limit is 80 chars
                customId: $"spamsettings_{guild.Id}",
                style: ButtonStyle.Primary
            );
        }
        
        // Send initial message with buttons
        IUserMessage message = await FollowupAsync(
            text: "Select a guild to view its spam trigger settings:",
            components: componentBuilder.Build(),
            ephemeral: true // Make the response visible only to the user
        );
        
        // Attach button handler
        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id) return;
            
            if (component.User.Id != Context.User.Id)
            {
                await component.RespondAsync("You cannot use these buttons.", ephemeral: true);
                return;
            }
            
            if (!component.Data.CustomId.StartsWith("spamsettings_"))
            {
                await component.RespondAsync("Invalid button.", ephemeral: true);
                return;
            }
            
            try
            {
                string guildIdStr = component.Data.CustomId.Replace("spamsettings_", "");
                if (!ulong.TryParse(guildIdStr, out ulong guildId))
                {
                    await component.RespondAsync("Invalid guild ID.", ephemeral: true);
                    logger.LogWarning($"Button [spamsettings] failed: Invalid guild ID [{guildIdStr}] by [{component.User.Username}] on [{Context.Guild?.Name ?? "DM"}]");
                    return;
                }
                
                SocketGuild? guild = Context.Client.GetGuild(guildId);
                if (guild == null)
                {
                    await component.RespondAsync("Guild not found or bot is no longer in it.", ephemeral: true);
                    logger.LogWarning($"Button [spamsettings] failed: Guild not found [{guildId}] by [{component.User.Username}] on [{Context.Guild?.Name ?? "DM"}]");
                    return;
                }
                
                using (IServiceScope scope = scopeFactory.CreateScope())
                {
                    DatabaseContext databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                    List<SpamTrigger> triggers = await databaseContext.SpamTriggers
                        .Where(t => t.GuildId == guild.Id)
                        .ToListAsync();
                    
                    EmbedBuilder embed = new EmbedBuilder()
                        .WithTitle($"Spam Trigger Settings for {guild.Name}")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp();
                    
                    if (!triggers.Any())
                    {
                        embed.WithDescription("No spam trigger settings configured for this guild.");
                    }
                    else
                    {
                        StringBuilder guildInfo = new StringBuilder();
                        foreach (SpamTrigger trigger in triggers)
                        {
                            string actionDuration = trigger.ActionType == SpamAction.Timeout && trigger.ActionDuration.HasValue
                                ? $"{trigger.ActionDuration} min"
                                : "N/A";
                            string deleteMessages = trigger.ActionDelete ? "Yes" : "No";
                            guildInfo.AppendLine($"**Type:** {trigger.Type}");
                            guildInfo.AppendLine($"  - **Action:** {trigger.ActionType}");
                            guildInfo.AppendLine($"  - **Duration:** {actionDuration}");
                            guildInfo.AppendLine($"  - **Delete Messages:** {deleteMessages}");
                            guildInfo.AppendLine($"  - **Messages:** {trigger.NbMessages}");
                            guildInfo.AppendLine($"  - **Interval:** {trigger.IntervalTime:F1} sec");
                        }
                        
                        embed.WithDescription(guildInfo.ToString());
                    }
                    
                    await component.RespondAsync(embed: embed.Build(), ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Button [spamsettings] failed by [{component.User.Username}] on [{Context.Guild?.Name ?? "DM"}] - GuildId: {component.Data.CustomId}");
                await component.RespondAsync("An error occurred while retrieving the settings. Please try again later.", ephemeral: true);
            }
        }
        
        // Attach the handler
        Context.Client.ButtonExecuted += Handler;
        
        // Auto-remove handler after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.ButtonExecuted -= Handler;
            
            ComponentBuilder disabledComponents = new ComponentBuilder();
            foreach (IMessageComponentBuilder? component in componentBuilder.ActionRows.SelectMany(r => r.Components))
            {
                if (component is ButtonComponent button)
                {
                    disabledComponents.WithButton(
                        label: button.Label,
                        customId: button.CustomId,
                        style: button.Style,
                        disabled: true
                    );
                }
            }
            
            await message.ModifyAsync(m => m.Components = disabledComponents.Build());
        });
    }
    
    [SlashCommand("guildstats", "View stats for a guild the bot is in")]
    public async Task GuildStats()
    {
        await DeferAsync(ephemeral: true);
        IReadOnlyCollection<SocketGuild> guilds = Context.Client.Guilds;
        if (!guilds.Any())
        {
            await FollowupAsync("The bot is not in any guilds.", ephemeral: true);
            return;
        }
        
        // Build select menu
        SelectMenuBuilder menu = new SelectMenuBuilder()
            .WithCustomId("guildstats_select")
            .WithPlaceholder("Select a guild");
        
        foreach (SocketGuild guild in guilds.Take(25))
        {
            menu.AddOption(label: guild.Name, value: guild.Id.ToString(), description: $"{guild.MemberCount} members");
        }
        
        ComponentBuilder components = new ComponentBuilder()
            .WithSelectMenu(menu);
        IUserMessage message = await FollowupAsync(text: "Select a guild to view stats:", components: components.Build(), ephemeral: true);
        
        // Handler
        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id)
                return;
            
            if (component.User.Id != Context.User.Id)
            {
                await component.RespondAsync("You cannot use this menu.", ephemeral: true);
                return;
            }
            
            if (component.Data.CustomId != "guildstats_select")
                return;
            try
            {
                string selectedValue = component.Data.Values.First();
                ulong guildId = ulong.Parse(selectedValue);
                SocketGuild? selectedGuild = Context.Client.GetGuild(guildId);
                if (selectedGuild == null)
                {
                    await component.RespondAsync("Guild not found.", ephemeral: true);
                    return;
                }
                
                // Build embeds from helper
                List<Embed> pages =
                    await GuildHelper.BuildServerStatsEmbeds(selectedGuild);
                await component.DeferAsync(ephemeral: true);
                
                // Send paginator
                await ButtonPaginator.SendPaginatedEmbedsAsync(Context, pages, $"Server Stats: {selectedGuild.Name}", true);
            }
            catch (Exception ex)
            {
                await component.RespondAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
        
        // Register handler
        Context.Client.SelectMenuExecuted += Handler;
        
        // Cleanup after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            Context.Client.SelectMenuExecuted -= Handler;
            try
            {
                await message.ModifyAsync(msg => { msg.Components = new ComponentBuilder().Build(); });
            }
            catch
            {
                // ignored
            }
        });
    }
    
    [SlashCommand("leaveguild", "Make the bot leave a guild")]
    public async Task LeaveGuild()
    {
        await DeferAsync(ephemeral: true);
        
        IReadOnlyCollection<SocketGuild> guilds = Context.Client.Guilds;
        
        if (!guilds.Any())
        {
            await FollowupAsync("The bot is not in any guilds.", ephemeral: true);
            return;
        }
        
        SelectMenuBuilder menu = new SelectMenuBuilder()
            .WithCustomId("leaveguild_select")
            .WithPlaceholder("Select a guild");
        
        foreach (SocketGuild guild in guilds.Take(25))
        {
            menu.AddOption(
                label: guild.Name,
                value: guild.Id.ToString(),
                description: $"{guild.MemberCount} members"
            );
        }
        
        ComponentBuilder components = new ComponentBuilder()
            .WithSelectMenu(menu);
        
        IUserMessage message = await FollowupAsync(
            text: "Select a guild to leave:",
            components: components.Build(),
            ephemeral: true
        );
        
        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id)
                return;
            
            if (component.User.Id != Context.User.Id)
            {
                await component.RespondAsync("You cannot use this menu.", ephemeral: true);
                return;
            }
            
            if (component.Data.CustomId != "leaveguild_select")
                return;
            
            try
            {
                string selectedValue = component.Data.Values.First();
                
                ulong guildId = ulong.Parse(selectedValue);
                
                SocketGuild? selectedGuild = Context.Client.GetGuild(guildId);
                
                if (selectedGuild == null)
                {
                    await component.RespondAsync("Guild not found.", ephemeral: true);
                    return;
                }
                
                string guildName = selectedGuild.Name;
                
                // Confirmation buttons
                ComponentBuilder confirmBuilder = new ComponentBuilder()
                    .WithButton(
                        label: "Confirm Leave",
                        customId: $"confirm_leave_{guildId}",
                        style: ButtonStyle.Danger
                    )
                    .WithButton(
                        label: "Cancel",
                        customId: "cancel_leave",
                        style: ButtonStyle.Secondary
                    );
                
                await component.UpdateAsync(msg =>
                {
                    msg.Content = $"Are you sure you want the bot to leave:\n**{guildName}** ({guildId})";
                    msg.Components = confirmBuilder.Build();
                });
            }
            catch (Exception ex)
            {
                await component.RespondAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
        
        async Task ConfirmHandler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id)
                return;
            
            if (component.User.Id != Context.User.Id)
            {
                await component.RespondAsync("You cannot use this.", ephemeral: true);
                return;
            }
            
            try
            {
                if (component.Data.CustomId == "cancel_leave")
                {
                    await component.UpdateAsync(msg =>
                    {
                        msg.Content = "Cancelled.";
                        msg.Components = new ComponentBuilder().Build();
                    });
                    
                    return;
                }
                
                if (!component.Data.CustomId.StartsWith("confirm_leave_"))
                    return;
                
                string guildIdStr = component.Data.CustomId.Replace("confirm_leave_", "");
                
                ulong guildId = ulong.Parse(guildIdStr);
                
                SocketGuild? guild = Context.Client.GetGuild(guildId);
                
                if (guild == null)
                {
                    await component.UpdateAsync(msg =>
                    {
                        msg.Content = "Guild no longer exists.";
                        msg.Components = new ComponentBuilder().Build();
                    });
                    
                    return;
                }
                
                string guildName = guild.Name;
                logger.LogWarning($"Bot leaving guild [{guildName}] ({guild.Id}) by [{Context.User.Username}]");
                await component.UpdateAsync(msg =>
                {
                    msg.Content = $"Leaving guild: **{guildName}** ({guild.Id})";
                    msg.Components = new ComponentBuilder().Build();
                });
                
                await guild.LeaveAsync();
            }
            catch (Exception ex)
            {
                await component.RespondAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
        
        Context.Client.SelectMenuExecuted += Handler;
        Context.Client.ButtonExecuted += ConfirmHandler;
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            
            Context.Client.SelectMenuExecuted -= Handler;
            Context.Client.ButtonExecuted -= ConfirmHandler;
            
            try
            {
                await message.ModifyAsync(msg => { msg.Components = new ComponentBuilder().Build(); });
            }
            catch
            {
                // ignored
            }
        });
    }
}