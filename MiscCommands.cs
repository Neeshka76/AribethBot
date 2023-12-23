using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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

namespace AribethBot
{
    public class MiscCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private readonly DiscordSocketClient client;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        // constructor injection is also a valid way to access the dependecies
        public MiscCommands(CommandHandler handler)
        {
            client = handler.socketClient;
            logger = handler.logger;
            httpClient = handler.httpClient;
        }

        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [SlashCommand("purge", "Purge messages from a channel where the command is executed", runMode: RunMode.Async)]
        public async Task PurgeAsync()
        {
            IEnumerable<IMessage> messages = await Context.Channel.GetMessagesAsync(100).FlattenAsync();
            await RespondAsync($"Executing command in {Context.Channel.Name}", ephemeral: true);
            foreach (IMessage message in messages)
            {
                await message.DeleteAsync();
            }
        }
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [SlashCommand("copy", "copy messages from a channel to where the command is executed", runMode: RunMode.Async)]
        public async Task CopyAsync(int number = 1, string channelToCopy = "https://discord.com/channels/824331584319782982/852773540893687808", string destinationToPasteTo = "https://discord.com/channels/980745782594535484/1187742715589439570")
        {
            ulong[] from = ReturnGuildAndChannelsIDs(channelToCopy);
            ulong[] to = ReturnGuildAndChannelsIDs(destinationToPasteTo);
            DiscordSocketClient clientToGetMessagesFrom = client;
            SocketTextChannel channelToCopyMessagesFrom = clientToGetMessagesFrom.GetGuild(from[0]).GetTextChannel(from[1]);
            DiscordSocketClient clientToPasteMessagesTo = client;
            SocketTextChannel channelToPasteMessagesFrom = clientToPasteMessagesTo.GetGuild(to[0]).GetTextChannel(to[1]);

            IEnumerable<IMessage> messages = await channelToCopyMessagesFrom.GetMessagesAsync(100).FlattenAsync();
            //await RespondAsync($"Number of messages : {messages.Count()}", ephemeral: true);
            //foreach (IMessage message in messages)
            //{
            //    if (messages.First().Attachments.Count <= 0)
            //    {
            //        await channelToPasteMessagesFrom.SendMessageAsync(text: messages.First().Content, isTTS: messages.First().IsTTS);
            //    }
            //    else
            //    {
            //        await channelToPasteMessagesFrom.SendFilesAsync(attachments: (IEnumerable<FileAttachment>)messages.First().Attachments, text: messages.First().Content, isTTS: messages.First//().IsTTS);
            //    }
            //}
            if (messages.ElementAt(number - 1).Attachments.Count <= 0)
            {
                await channelToPasteMessagesFrom.SendMessageAsync(text: messages.ElementAt(number - 1).Content, isTTS: messages.ElementAt(number - 1).IsTTS);
            }
            else
            {
                List<FileAttachment> filestosend = new List<FileAttachment>();
                //foreach (IAttachment attachment in messages.First().Attachments)
                //{
                //    filestosend.Add(new FileAttachment(attachment.Url, attachment.Title, attachment.Description));
                //}
                List<FileStream> streams = new List<FileStream>();
                foreach (IAttachment attachment in messages.ElementAt(number-1).Attachments)
                {
                    if (attachment != null)
                    {
                        string url = attachment.Url.Substring(0, attachment.Url.LastIndexOf('?'));
                        int indexOfSlash = url.LastIndexOf('/') + 1;
                        string fileNameAndExtension = url.Substring(indexOfSlash, url.Length - indexOfSlash);
                        //int indexOfDot = fileNameAndExtension.LastIndexOf(".") + 1;
                        //string fileExtension = fileNameAndExtension.Substring(indexOfDot, fileNameAndExtension.Length - indexOfDot);
                        //string fileName = fileNameAndExtension.Substring(0, indexOfDot - 1);
                        await RespondAsync($"URL : {url}", ephemeral: true);
                        streams.Add(await DownloadAndSave(url, "Download", fileNameAndExtension));
                    }
                    else
                    {
                        await RespondAsync($"URL : attachment null", ephemeral: true);
                    }
                }
                //await channelToPasteMessagesFrom.SendFilesAsync(attachments: filestosend, text: messages.First().Content, isTTS: messages.First().IsTTS);
            }
        }

        async Task<FileStream> DownloadAndSave(string sourceFile, string destinationFolder, string destinationFileName)
        {
            Stream fileStream = await GetFileStream(sourceFile);
            FileStream stream = null;
            if (fileStream != Stream.Null)
            {
                 stream = await SaveStream(fileStream, destinationFolder, destinationFileName);
            }
            return stream;
        }

        async Task<Stream> GetFileStream(string fileUrl)
        {
            try
            {
                Stream fileStream = await httpClient.GetStreamAsync(fileUrl);
                return fileStream;
            }
            catch (Exception ex)
            {
                return Stream.Null;
            }
        }

        async Task<FileStream> SaveStream(Stream fileStream, string destinationFolder, string destinationFileName)
        {
            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            string path = Path.Combine(destinationFolder, destinationFileName);

            using (FileStream outputFileStream = new FileStream(path, FileMode.Create))
            {
                await fileStream.CopyToAsync(outputFileStream);
                return outputFileStream;
            }
        }

        private ulong[] ReturnGuildAndChannelsIDs(string link)
        {
            ulong[] ids = new ulong[2];
            string temp = link.Remove(0, "https://discord.com/channels/".Length);
            ulong guildId = ulong.Parse(temp.Split("/", StringSplitOptions.None)[0]);
            ulong channelId = ulong.Parse(temp.Split("/", StringSplitOptions.None)[1]);
            ids[0] = guildId;
            ids[1] = channelId;
            return ids;
        }


        public enum TimestampFormat
        {
            ShortTime,
            LongTime,
            ShortDate,
            LongDate,
            LongDateShortTime,
            LongDateDayWeekShortTime,
            Relative
        }

        enum TypeOfError
        {
            None,
            Year,
            Month,
            Day,
            LeapYear,
            Hour,
            Minute,
            TimeZone
        }

        TypeOfError typeOfError = TypeOfError.None;

        // Timestamp command
        [SlashCommand("timestamp", "Give a discord timestamp")]
        public async Task UnixTimestamp([Summary("Year", "Year : between 1970 & 3000")] int year,
            [Summary("Month", "Month : between 1 & 12")] int month,
            [Summary("Day", "Day : between 1 & 31 (takes in account the month !)")] int day,
            [Summary("Hour", "Hour : between 0 & 23")] int hour,
            [Summary("Minute", "Day : between 0 & 59")] int minute,
            [Summary("TimeZone", "TimeZone : values between -12 & 14 : for example CET is +1; CST is +8;")] int timeZone,
            [Summary("DaylightSaving", "DaylightSaving : True between March and November/October, false for the rest of the year")] bool daylightSaving = false,
            [Summary("Format", "Format : choose between multiple format ")] TimestampFormat timestampFormat = TimestampFormat.LongDateShortTime)
        {
            bool error = ErrorInData(year, month, day, hour, minute, timeZone);
            string message;
            if (error)
            {
                message = $"Error in : {typeOfError}";
                switch (typeOfError)
                {
                    case TypeOfError.Year:
                        message += " : \nMake sure to put a value between 1970 & 3000 for the year";
                        break;
                    case TypeOfError.Month:
                        message += " : \nMake sure to put a value between 1 & 12 for the month";
                        break;
                    case TypeOfError.Day:
                        message += " : \nMake sure to put a value between 1 & 31 for the day";
                        break;
                    case TypeOfError.LeapYear:
                        message += " : \nMake sure to put a value between that is correct for the day of February based on the Leap Year exception (2023 isn't a leap year so it's max 28 for the day; 2024 is so it's max 29 for the day)";
                        break;
                    case TypeOfError.Hour:
                        message += " : \nMake sure to put a value between 0 & 23 for the hour";
                        break;
                    case TypeOfError.Minute:
                        message += " : \nMake sure to put a value between 0 & 59 for the minute";
                        break;
                    case TypeOfError.TimeZone:
                        message += " : \nMake sure to put a values between -12 & 14 for the timezone";
                        break;
                }
            }
            else
            {
                DateTime dateTime = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
                DateTime clientTime = dateTime.AddHours(-timeZone + (daylightSaving ? 1 : 0));
                long unixTime = ((DateTimeOffset)clientTime).ToUnixTimeSeconds();
                char timeStampChar = TimeStampChar(timestampFormat);
                message = $"<t:{unixTime}:{timeStampChar}>";
            }
            await RespondAsync($"{message}");
        }

        char TimeStampChar(TimestampFormat timestampFormat)
        {
            switch (timestampFormat)
            {
                case TimestampFormat.ShortTime:
                    return 't';
                case TimestampFormat.LongTime:
                    return 'T';
                case TimestampFormat.ShortDate:
                    return 'd';
                case TimestampFormat.LongDate:
                    return 'D';
                case TimestampFormat.LongDateShortTime:
                    return 'f';
                case TimestampFormat.LongDateDayWeekShortTime:
                    return 'F';
                case TimestampFormat.Relative:
                    return 'R';
                default:
                    return 'f';
            }
        }

        private bool ErrorInData(int year, int month, int day, int hour, int minute, int timeZone)
        {
            if (year > 3000 || year < 1970)
            {
                typeOfError = TypeOfError.Year;
                return true;
            }
            if (month > 12 || month < 1)
            {
                typeOfError = TypeOfError.Month;
                return true;
            }
            bool isLeapYear = IsLeapYear(year, month);
            if ((day > 31 || day < 1) ||
               ((month % 2 == 0 && month <= 7 || month % 2 == 1 && month > 7) && day > 30))
            {
                typeOfError = TypeOfError.Day;
                return true;
            }
            if (month == 2 && ((!isLeapYear && day > 28) || isLeapYear && day > 29))
            {
                typeOfError = TypeOfError.LeapYear;
                return true;
            }
            if (hour > 23 || hour < 0)
            {
                typeOfError = TypeOfError.Hour;
                return true;
            }
            if (minute > 59 || minute < 0)
            {
                typeOfError = TypeOfError.Minute;
                return true;
            }
            if (timeZone > 14 || timeZone < -12)
            {
                typeOfError = TypeOfError.TimeZone;
                return true;
            }
            return false;
        }

        private bool IsLeapYear(int year, int month)
        {
            if (month == 2 && (year % 4 == 0) && (year % 100 != 0) || ((year % 4 == 0) && (year % 100 == 0) && (year % 400 == 0)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
