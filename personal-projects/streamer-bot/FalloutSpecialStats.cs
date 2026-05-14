using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public class CPHInline
{
    private static readonly Random r = new Random();

    public bool Execute()
    {
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("user", out string user);
        if (!CPH.TryGetArg("sendChatMessage", out bool sendChat)) sendChat = true;

        TwitchUserInfoEx userInfo;
        SpecialStats specialStats;

        if (CPH.TryGetArg("input0", out string targetUser) && !string.IsNullOrEmpty(targetUser))
        {
            if (IsInternationalName(targetUser.Trim()))
            {
                userInfo = GetUserInfoFromGroup(targetUser.Trim().ToLower());
                specialStats = GetSpecialStats(userInfo.UserId.ToLower());
                if (sendChat)
                    CPH.SendMessage($"@{TwitchName(userInfo.UserLogin, userInfo.UserName)} SPECIAL Stats for today: Strength: {specialStats.Strength}, Perception: {specialStats.Perception}, Endurance: {specialStats.Endurance}, Charisma: {specialStats.Charisma}, Intelligence: {specialStats.Intelligence}, Agility: {specialStats.Agility}, Luck: {specialStats.Luck}.", true, true);
                return true;
            }
            else
            {
                userInfo = CPH.TwitchGetExtendedUserInfoByLogin(targetUser.Trim().ToLower());
                specialStats = GetSpecialStats(userInfo.UserId);
                if (sendChat)
                    CPH.SendMessage($"@{TwitchName(userInfo.UserLogin, userInfo.UserName)} SPECIAL Stats for today: Strength: {specialStats.Strength}, Perception: {specialStats.Perception}, Endurance: {specialStats.Endurance}, Charisma: {specialStats.Charisma}, Intelligence: {specialStats.Intelligence}, Agility: {specialStats.Agility}, Luck: {specialStats.Luck}.", true, true);
                return true;
            }
            return true;
        }

        userInfo = GetUserInfoFromGroup(user);
        specialStats = GetSpecialStats(userId);
        if (sendChat)
            CPH.SendMessage($"@{TwitchName(userInfo.UserLogin, userInfo.UserName)} SPECIAL Stats for today: Strength: {specialStats.Strength}, Perception: {specialStats.Perception}, Endurance: {specialStats.Endurance}, Charisma: {specialStats.Charisma}, Intelligence: {specialStats.Intelligence}, Agility: {specialStats.Agility}, Luck: {specialStats.Luck}.", true, true);
        return true;
    }

    public SpecialStats GetSpecialStats(string userId)
    {
        Dictionary<string, int> stats = CPH.GetTwitchUserVarById<Dictionary<string, int>>(userId, "SpecialStats", false);
        if (stats == null)
        {
            return SetSpecialStats(userId);
        }

        return new SpecialStats
        {
            Strength = stats["Strength"],
            Perception = stats["Perception"],
            Endurance = stats["Endurance"],
            Charisma = stats["Charisma"],
            Intelligence = stats["Intelligence"],
            Agility = stats["Agility"],
            Luck = stats["Luck"]
        };
    }

    public SpecialStats SetSpecialStats(string userId)
    {
        var stats = new[]
        {
            "Strength",
            "Perception",
            "Endurance",
            "Charisma",
            "Intelligence",
            "Agility",
            "Luck"
        };

        var values = DistributeSpecial(stats.Length, 40, 1);

        var specialStats = new SpecialStats
        {
            Strength = values[0],
            Perception = values[1],
            Endurance = values[2],
            Charisma = values[3],
            Intelligence = values[4],
            Agility = values[5],
            Luck = values[6]
        };
        Dictionary<string, int> specialStatsDictionary = new Dictionary<string, int>
        {
            {
                "Strength",
                values[0]
            },
            {
                "Perception",
                values[1]
            },
            {
                "Endurance",
                values[2]
            },
            {
                "Charisma",
                values[3]
            },
            {
                "Intelligence",
                values[4]
            },
            {
                "Agility",
                values[5]
            },
            {
                "Luck",
                values[6]
            }
        };
        CPH.SetTwitchUserVarById(userId, "SpecialStats", specialStatsDictionary, false);
        return specialStats;
    }

    static int[] DistributeSpecial(int statCount, int totalPoints, int minPerStat)
    {
        Random rnd = new Random();
        int[] values = Enumerable.Repeat(minPerStat, statCount).ToArray();
        int pointsLeft = totalPoints - (minPerStat * statCount);
        for (int i = 0; i < pointsLeft; i++)
        {
            values[rnd.Next(statCount)]++; // randomly add 1 point to a stat
        }

        return values;
    }

    public bool IsInternationalName(string UserName) => UserName.Any(ch => ch > 127);
    public string TwitchName(string userName, string displayName)
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

    public TwitchUserInfoEx GetUserInfoFromGroup(string targetDisplayName)
    {
        string groupName = "International Usernames";
        // Store anyone who has a international display name in a group, which we call here to get users in that group.
        List<GroupUser> groupUsers = CPH.UsersInGroup(groupName);
        // Search for the group and find the first user that matches either userId or Display Name.
        // Streamer bot only supports finding users by userLogin and userId and not Display name so this is the only fix I've found.
        GroupUser userInfo = groupUsers.FirstOrDefault(u => u.Id == targetDisplayName || u.Username.Equals(targetDisplayName, StringComparison.OrdinalIgnoreCase));
        // Need a null check if the user can't be found in the group.
        if (userInfo == null)
            return CPH.TwitchGetExtendedUserInfoByLogin(targetDisplayName);
        ;
        return CPH.TwitchGetExtendedUserInfoById(userInfo.Id);
    }
}

public class SpecialStats
{
    public int Strength { get; set; }
    public int Perception { get; set; }
    public int Endurance { get; set; }
    public int Charisma { get; set; }
    public int Intelligence { get; set; }
    public int Agility { get; set; }
    public int Luck { get; set; }
}
