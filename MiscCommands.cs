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
    public class MiscCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        private CommandHandler handler;

        // constructor injection is also a valid way to access the dependecies
        public MiscCommands(CommandHandler handler)
        {
            this.handler = handler;
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
                DateTime dateTime = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
                DateTime clientTime = dateTime + TimeSpan.FromHours(timeZone);
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
            if (year > 3000 || year < 0)
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
