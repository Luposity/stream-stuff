using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private string DateToday = String.Empty;
    private static readonly Random r = new Random();
    public bool Execute()
    {
        if (String.IsNullOrEmpty(DateToday) || DateToday != DateTime.Today.ToString("MM/dd")) DateToday = DateTime.Today.ToString("MM/dd");
        CPH.TryGetArg("badges", out List<Twitch.Common.Models.Badge> badges);
        if (badges != null && badges.Any(b => b.Name == "bot-badge")) return HandleBotAccount();

        CPH.TryGetArg("userId", out string UserId);
        CPH.TryGetArg("firstMessage", out bool firstMessage);
        CPH.TryGetArg("returningChatter", out bool returningChatter);
        CPH.TryGetArg("rawInput", out string rawInput);

        if (CPH.UserIdInGroup(UserId, Platform.Twitch, "[Twitch Bots]")) return false;

        TwitchUserInfoEx UserInfo = CPH.TwitchGetExtendedUserInfoById(UserId);

        if (!rawInput.StartsWith("!!"))
            PlayWalkonSound(UserId, UserInfo);

        string birthday = CPH.GetTwitchUserVarById<string>(UserId, "birthday", true);

        if (!string.IsNullOrEmpty(birthday) && birthday == DateToday) UserBirthday();

        if (UserInfo.IsFollowing)
        {
            if (CPH.ObsIsConnected() && !CPH.ObsIsStreaming()) return true;
            FollowerAnniversary(UserInfo);

            if (UserInfo.IsSubscribed) IncrementUserCounterById(UserId, "points", 300, true);
            else if (badges != null && badges.Any(b => b.Name == "turbo")) IncrementUserCounterById(UserId, "points", 250, true);
            else IncrementUserCounterById(UserId, "points", 150, true);
        }

        return true;
    }
    private bool FollowerAnniversary(TwitchUserInfoEx UserInfo)
    {
        if (!CPH.TryGetArg("followDate", out string inputDateString)) return false;

        var followDate = DateTimeOffset.Parse(inputDateString);
        var now = DateTimeOffset.UtcNow;

        int targetDay = Math.Min(followDate.Day, DateTime.DaysInMonth(now.Year, now.Month));

        if (now.Day != targetDay) return false;

        int totalMonths = (now.Year - followDate.Year) * 12 + (now.Month - followDate.Month);

        if (totalMonths <= 0) return false;

        if (totalMonths % 6 != 0) return false;

        var result = TimeAgo(followDate, DateTimeOffset.UtcNow);

        if (totalMonths % 12 == 0)
        {
            int years = totalMonths / 12;
            CPH.SendMessage($"Today marks {years} year{(years > 1 ? "s" : "")} of @{UserInfo.UserName} following!",true, true);
        }
        else
        {
            CPH.SendMessage($"Today marks {totalMonths} months of @{UserInfo.UserName} following!",true, true);
        }
        return true;
    }
    private bool UserBirthday()
    {
        return true;
    }
    public bool PlayWalkonSound(string UserId, TwitchUserInfoEx UserInfo)
    {
        CPH.TryGetArg("firstMessage", out bool firstMessage);
        CPH.TryGetArg("returningChatter", out bool returningChatter);
        var flags = new WalkOnFlags
        {
            GoldenCookieAward = CPH.UserIdInGroup(UserId, Platform.Twitch, "[Twitch] Golden Cookie Award"),

            IsModerator = UserInfo.IsModerator,
            IsVIP = UserInfo.IsVip,
            IsTwitchPartner = UserInfo.IsPartner,
            ReturningChatter = returningChatter,
            FirstMessage = firstMessage
        };
        if (flags.AnyTrue())
        {
            WalkOnOverlay(UserInfo.UserName, UserInfo.UserLogin);

            if (PlaySoundFromFolder(UserId)) return true;
            else { PlaySoundFromFolder("default"); return true; }
        }

        return false;
    }
    private bool HandleBotAccount()
    {
        CPH.TryGetArg("userId", out string UserId);
        if (CPH.UserIdInGroup(UserId, Platform.Twitch, "[Twitch Bots]")) return false;
        CPH.AddUserIdToGroup(UserId, Platform.Twitch, "[Twitch Bots]");
        return true;
    }
    public long IncrementUserCounterById(string userId, string varName, long value = 1, bool persited = true)
    {
        CPH.IncrementOrCreateTwitchUsersVarById([userId], varName, value, persited);
        return CPH.GetTwitchUserVarById<long>(userId, varName, persited);
    }

    private bool WalkOnOverlay(string displayName, string userLogin)
    {
        var payload = new
        {
            eventName = "overlay.walkon",
            userDisplayName = displayName,
            userLoginName = userLogin,
            message = $"{WalkOnOverlayMessage(displayName, userLogin)}"
        };
        string json = JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);
        return true;
    }
    private string WalkOnOverlayMessage(string displayName, string userLogin)
    {
        int hour = DateTime.Now.Hour;
        string timeOfDay;
        switch (hour)
        {
            case int h when (h >= 0 && h < 12):
                timeOfDay = "morning";
                break;
            case int h when (h >= 12 && h < 17):
                timeOfDay = "afternoon";
                break;
            case int h when (h >= 17 && h < 0):
                timeOfDay = "evening";
                break;
            default:
                timeOfDay = "afternoon";
                break;
        }

        List<string> message = new()
        {
            $"Good {timeOfDay}, {TwitchName(userLogin, displayName)}!",
            $"Hey there {TwitchName(userLogin, displayName)}!",
            $"Greetings {TwitchName(userLogin, displayName)}!",
            $"A wild {TwitchName(userLogin, displayName)} has arrived!"
        };

        int index = r.Next(0, message.Count);

        return message[index];
    }
    private string TwitchName(string userName, string displayName)
    {
        if (IsInternationalName(displayName))
        {

            return $"{displayName} ({userName})";
        }
        else
        {
            return $"{displayName}";
        }
    }
    public static string TimeAgo(DateTimeOffset from, DateTimeOffset to)
    {
        if (from > to)
            (from, to) = (to, from);
        int years = 0, months = 0;
        while (from.AddYears(1) <= to)
        {
            from = from.AddYears(1);
            years++;
        }

        while (from.AddMonths(1) <= to)
        {
            from = from.AddMonths(1);
            months++;
        }

        var span = to - from;
        int days = span.Days;
        int hours = span.Hours;
        int minutes = span.Minutes;
        int seconds = span.Seconds;
        if (years > 0)
            return $"{years} years, {months} months, {days} days";
        if (months > 0)
            return $"{months} months, {days} days";
        if (days > 0)
            return $"{days} days ago";
        return $"Today!";
    }
    private bool IsInternationalName(string UserName) => UserName.Any(ch => ch > 127);
    public string FolderPath(string userId) => Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Walk-On", $"{userId}");
    public bool PlaySoundFromFolder(string userId) => CPH.PlaySoundFromFolder(FolderPath(userId), 1.00F, true) > 0.0;
    public string TTSName(string userName)
    {
		string TTSName = Regex.Replace(userName, "[^a-zA-Z0-9_]", "");
		TTSName = TTSName.Replace("_", " ");
		TTSName = Regex.Replace(TTSName, @"\s+", " ").Trim();
		return TTSName;
    }

    public bool CreateWalkOnPath(string UserId, string UserName)
	{
		string FolderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Walk-On", $"{UserId}");
		
		string FileName = $"{UserName}.txt";
		string FilePath = Path.Combine(FolderPath, FileName);

		if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

		if (!File.Exists(FilePath))
        {
			File.Create(FilePath);
        }
        return true;
	}
}

class WalkOnFlags
{
    public bool IsModerator;
    public bool IsVIP;
    public bool IsTwitchPartner;

    public bool GoldenCookieAward;

    public bool ReturningChatter;
    public bool FirstMessage;
    public bool AnyTrue()
    {
        return IsModerator
            || IsVIP
            || IsTwitchPartner
            || GoldenCookieAward
            || FirstMessage
            || ReturningChatter;
    }
}

public static class ListExtensions
{
    public static bool ListContains(this List<string> list, string value, StringComparison comparison)
    {
        return list.Any(s => s != null && s.Equals(value, comparison));
    }
}
