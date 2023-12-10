using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AribethBot
{
    // Context is the "environnement" of where the command is executed
    public class BaSCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private CommandHandler handler;

        // constructor injection is also a valid way to access the dependecies
        public BaSCommands(CommandHandler handler)
        {
            this.handler = handler;
        }

        // Link WeaponCrafting
        [SlashCommand("weaponcraftingtutorial", "Link to the weapon crafting tutorial for B&S")]
        public async Task WeaponCrafting()
        {
            string link = "https://youtu.be/-yjZAnniklM";
            await RespondAsync(link);
        }

        // Player Log command
        [SlashCommand("log", "Indicate how to get a Player Log")]
        public async Task Log([Summary("User", "User to ping for the command")] SocketUser? user = null)
        {
            SocketUser contextUser = Context.User;
            if (user == null)
            {
                user = contextUser;
            }
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
                if (role.Name.Contains("PCVR"))
                {
                    hasPCVR = true;
                }
                if (role.Name.Contains("Nomad"))
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
                $"```%userprofile%\\AppData\\LocalLow\\WarpFrog\\BladeAndSorcery\\```\r\n";
            string messageNomad =
                $"# For Nomad \r\n" +
                "Follow the instructions on this link : <https://warpfrog.notion.site/Retrieve-your-game-log-file-cc3c723074be479a8284569055c5afcf>\r\n\r\n";
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
                if (hasPCVR & !hasNomad)
                {
                    //embedBuilder.Color = Color.Blue;
                }
                else if (!hasPCVR & hasNomad)
                {
                    //embedBuilder.Color = Color.Red;
                }
                else
                {
                    //embedBuilder.Color = Color.Gold;
                }
                message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
            }
            return message;
        }

        // Save location command
        [SlashCommand("savelocation", "Indicate how to get to the save folder")]
        public async Task SaveLocation([Summary("User", "User to ping for the command")] SocketUser? user = null)
        {
            SocketUser contextUser = Context.User;
            if (user == null)
            {
                user = contextUser;
            }
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
                if (role.Name.Contains("PCVR"))
                {
                    hasPCVR = true;
                }
                if (role.Name.Contains("Nomad"))
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
                $"```%userprofile%\\Documents\\My Games\\BladeAndSorcery\\Saves\\```\r\n";
            string messageNomad =
                $"# For Nomad \r\n" +
                "```This PC\\Quest 2\\Internal shared storage\\Android\\data\\com.Warpfrog.BladeAndSorcery\\files\\Saves```\r\n\r\n";
            string messageOutro =
            $"Deleting the file called **Options.opt** (or possibly just **Options**) will reset all applied settings.  The other files are your characters, which includes their appearance and loadouts..\r\n\r\n" +
            $"*Command triggered by {contextUser.Mention} with /savelocation @user*";

            if (!hasPCVR && !hasNomad)
            {
                message =
                    $"**Hi {user.Mention} !**\r\n\r\n" +
                    $"Please select a role (PCVR and/or Nomad) first for the command to work properly !";
            }
            else
            {
                if (hasPCVR & !hasNomad)
                {
                    //embedBuilder.Color = Color.Blue;
                }
                else if (!hasPCVR & hasNomad)
                {
                    //embedBuilder.Color = Color.Red;
                }
                else
                {
                    //embedBuilder.Color = Color.Gold;
                }
                message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
            }
            return message;
        }

        // Crash location command
        [SlashCommand("crashlocation", "Indicate how to get to the crash folder")]
        public async Task CrashLocation([Summary("User", "User to ping for the command")] SocketUser? user = null)
        {
            SocketUser contextUser = Context.User;
            if (user == null)
            {
                user = contextUser;
            }
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
                if (role.Name.Contains("PCVR"))
                {
                    hasPCVR = true;
                }
                if (role.Name.Contains("Nomad"))
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
                $"```%temp%\\WarpFrog\\BladeAndSorcery\\Crashes```\r\n";
            string messageNomad =
                $"# For Nomad \r\n" +
                "```This PC\\Quest 2\\Internal shared storage\\Android\\data\\com.Warpfrog.BladeAndSorcery\\files\\Crashes```\r\n\r\n";
            string messageOutro =
            $"Then go inside the most recent one and drag the file called **Player.log** (or possibly just **Player**) ***and*** the file called **crash.dmp** (or possibly just **crash**) into this channel on Discord.\r\n\r\n" +
            $"*Command triggered by {contextUser.Mention} with /crashlocation @user*";

            if (!hasPCVR && !hasNomad)
            {
                message =
                    $"**Hi {user.Mention} !**\r\n\r\n" +
                    $"Please select a role (PCVR and/or Nomad) first for the command to work properly !";
            }
            else
            {
                if (hasPCVR & !hasNomad)
                {
                    
                }
                else if (!hasPCVR & hasNomad)
                {
                    
                }
                else
                {
                    
                }
                message = messageIntro + (hasPCVR ? messagePCVR : "") + (hasNomad ? messageNomad : "") + messageOutro;
            }
            return message;
        }
    }
}
