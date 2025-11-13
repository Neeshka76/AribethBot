using AribethBot.Helpers;
using Discord.Interactions;
using Discord.WebSocket;

namespace AribethBot;

[Group("bas", "Commands related to Blade & Sorcery")]
public class BaSCommands : InteractionModuleBase<SocketInteractionContext>
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private readonly ServiceHandler handler;
    private readonly ResourceService resources;
    
    // constructor injection is also a valid way to access the dependencies
    public BaSCommands(ServiceHandler handler, ResourceService resources)
    {
        this.handler = handler;
        this.resources = resources;
    }
    
    // Link WeaponCrafting
    
    [SlashCommand("weaponcraftingtutorial", "Link to the weapon crafting tutorial for B&S")]
    public async Task WeaponCrafting()
    {
        string? link = resources.Get("WeaponCraftingLink") ?? "Link not set.";
        await RespondAsync(link, ephemeral: true);
    }
    
    // Edit Link WeaponCrafting
    [RequireOwner()]
    [SlashCommand("editweaponcraftingtutorial", "Edit the link to the weapon crafting tutorial for B&S")]
    public async Task EditWeaponCrafting([Summary("URL", "URL for the command")] string url)
    {
        resources.Set("WeaponCraftingLink", url);
        await RespondAsync("Weapon crafting link updated!", ephemeral: true);
    }
    
    [SlashCommand("basbible", "Link to the modding wiki for B&S")]
    public async Task BasWiki()
    {
        string? link = resources.Get("WikiLink") ?? "Link not set.";
        await RespondAsync(link, ephemeral: true);
    }
    
    [RequireOwner()]
    [SlashCommand("editbasbible", "Edit the link to the modding wiki for B&S")]
    public async Task EditBasBible([Summary("URL", "URL for the command")] string url)
    {
        resources.Set("WikiLink", url);
        await RespondAsync("Bas Bible link updated!", ephemeral: true);
    }
    
    // Player Log command
    [SlashCommand("log", "Indicate how to get a Player Log")]
    public async Task LogLocation([Summary("User", "User to ping for the command")] SocketUser? user = null)
    {
        SocketUser contextUser = Context.User;
        user ??= contextUser;
        string message = MessageLogBuilder(user, contextUser);
        // Return the player log message text
        await RespondAsync($"{message}");
    }
    
    string MessageLogBuilder(SocketUser user, SocketUser contextUser)
    {
        bool hasPCVR = false;
        bool hasNomad = false;
        foreach (SocketRole role in (user as SocketGuildUser).Roles)
        {
            if (role.Name.Contains("PCVR", StringComparison.CurrentCultureIgnoreCase))
            {
                hasPCVR = true;
            }
            
            if (role.Name.Contains("Nomad", StringComparison.CurrentCultureIgnoreCase))
            {
                hasNomad = true;
            }
        }
        
        string message = "";
        string messageIntro =
            $"**Hi {user.Mention} !**\r\n\r\n" +
            $"Please send your file called **Player.log** (or possibly just **Player**).\r\n";
        string messagePCVR =
            $"# For PCVR \r\n" +
            $"Open the explorer then in the address bar, entering this string into the box that appears, and pressing enter.\r\n\r\n" +
            $"```{resources.Get("PCVRLog")}```\r\n";
        string messageNomad =
            $"# For Nomad \r\n" +
            $"Follow the instructions on this link : <{resources.Get("NomadLog")}>\r\n\r\n";
        string messageOutro =
            $"Drag the file called **Player.Log** (or possibly just **Player**) into this channel on Discord.\r\n\r\n" +
            $"*Command triggered by {contextUser.Mention} with /log @user*";
        if (!hasPCVR && !hasNomad)
        {
            message =
                $"**Hi {user.Mention} !**\r\n\r\n" +
                $"Please select a role (PCVR and/or Nomad) first for the command to work properly !";
        }
        else
        {
            message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
        }
        
        return message;
    }
    
    // Save location command
    [SlashCommand("save", "Indicate how to get to the save folder")]
    public async Task SaveLocation([Summary("User", "User to ping for the command")] SocketUser? user = null)
    {
        SocketUser contextUser = Context.User;
        user ??= contextUser;
        string message = MessageSaveBuilder(user, contextUser);
        // Return the player log message text
        await RespondAsync($"{message}");
    }
    
    string MessageSaveBuilder(SocketUser user, SocketUser contextUser)
    {
        bool hasPCVR = false;
        bool hasNomad = false;
        foreach (SocketRole role in (user as SocketGuildUser).Roles)
        {
            if (role.Name.Contains("PCVR", StringComparison.CurrentCultureIgnoreCase))
            {
                hasPCVR = true;
            }
            
            if (role.Name.Contains("Nomad", StringComparison.CurrentCultureIgnoreCase))
            {
                hasNomad = true;
            }
        }
        
        string message = "";
        string messageIntro =
            $"**Hi {user.Mention} !**\r\n\r\n" +
            $"To find your saves folder, open the explorer then in the address bar, entering this string into the box that appears, and pressing enter.\r\n";
        string messagePCVR =
            $"# For PCVR \r\n" +
            $"```{resources.Get("PCVRSave")}```\r\n";
        string messageNomad =
            $"# For Nomad \r\n" +
            $"```{resources.Get("NomadSave")}```\r\n\r\n";
        string messageOutro =
            $"Deleting the file called **Options.opt** (or possibly just **Options**) will reset all applied settings.  The other files are your characters, which includes their appearance and loadouts..\r\n\r\n" +
            $"*Command triggered by {contextUser.Mention} with /save @user*";
        if (!hasPCVR && !hasNomad)
        {
            message =
                $"**Hi {user.Mention} !**\r\n\r\n" +
                $"Please select a role (PCVR and/or Nomad) first for the command to work properly !";
        }
        else
        {
            message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
        }
        
        return message;
    }
    
    // Crash location command
    [SlashCommand("crash", "Indicate how to get to the crash folder")]
    public async Task CrashLocation([Summary("User", "User to ping for the command")] SocketUser? user = null)
    {
        SocketUser contextUser = Context.User;
        user ??= contextUser;
        string message = MessageCrashBuilder(user, contextUser);
        // Return the player log message text
        await RespondAsync($"{message}");
    }
    
    string MessageCrashBuilder(SocketUser user, SocketUser contextUser)
    {
        bool hasPCVR = false;
        bool hasNomad = false;
        foreach (SocketRole role in (user as SocketGuildUser).Roles)
        {
            if (role.Name.Contains("PCVR", StringComparison.CurrentCultureIgnoreCase))
            {
                hasPCVR = true;
            }
            
            if (role.Name.Contains("Nomad", StringComparison.CurrentCultureIgnoreCase))
            {
                hasNomad = true;
            }
        }
        
        string message = "";
        string messageIntro =
            $"**Hi {user.Mention} !**\r\n\r\n" +
            $"To find your crash folder, open the explorer then in the address bar, entering this string into the box that appears, and pressing enter.\r\n";
        string messagePCVR =
            $"# For PCVR \r\n" +
            $"```{resources.Get("PCVRCrash")}```\r\n";
        string messageNomad =
            $"# For Nomad \r\n" +
            $"```{resources.Get("NomadCrash")}```\r\n\r\n";
        string messageOutro =
            $"Then go inside the most recent one and drag the file called **Player.log** (or possibly just **Player**) ***and*** the file called **crash.dmp** (or possibly just **crash**) into this channel on Discord.\r\n\r\n" +
            $"*Command triggered by {contextUser.Mention} with /crash @user*";
        if (!hasPCVR && !hasNomad)
        {
            message =
                $"**Hi {user.Mention} !**\r\n\r\n" +
                $"Please select a role (PCVR and/or Nomad) first for the command to work properly !";
        }
        else
        {
            message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
        }
        
        return message;
    }
}