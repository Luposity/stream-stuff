using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    public ShoutoutQueue shoutoutQueue = new ShoutoutQueue();
    public TimeSpan shoutoutCooldown = TimeSpan.FromMinutes(60);
    private bool processingQueue = false;
    Version extensionVersion = new Version(1, 1, 3);

    public void Init()
    {
        CPH.RegisterCustomTrigger("Queued Shoutout Sent", "luponium_queuedShoutout", ["Luponium", "Twitch Shoutout Queue"]);
        if (!CPH.GroupExists("Walkon Shoutouts")) CPH.AddGroup("Walkon Shoutouts");
    }

    public bool EnqueueRaid()
    {
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("userName", out string userName);
        if (ShouldSkipUser(userId))
            return false;
        var priority = Priority.Raid;
        shoutoutQueue.Enqueue(userId, userName, priority);
        if (!processingQueue)
            SendShoutout();
        return true;
    }

    public bool EnqueueWalkon()
    {
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("userName", out string userName);
        if (ShouldSkipUser(userId))
            return false;
        var priority = Priority.Default;
        shoutoutQueue.Enqueue(userId, userName, priority);
        if (!processingQueue)
            SendShoutout();
        return true;
    }

    public bool EnqueueCommand()
    {
        CPH.TryGetArg("rawInput", out string rawInput);
        CPH.TryGetArg("msgId", out string replyId);
        var users = rawInput.Replace("@", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim().ToLower()).Distinct().ToList();
        var priority = Priority.Default;
        var usersAdded = new List<string>();
        foreach (var user in users)
        {
            try
            {
                var userInfo = CPH.TwitchGetExtendedUserInfoByLogin(user);
                if (userInfo == null)
                    continue;
                var userId = userInfo.UserId;
                if (ShouldSkipUser(userId))
                    continue;
                shoutoutQueue.Enqueue(userId, userInfo.UserName, priority);
                usersAdded.Add(userInfo.UserName);
            }
            catch
            {
            }
        }

        if (!usersAdded.Any())
            return true;
        var queueCount = shoutoutQueue.Count;
        var addedUsers = string.Join(", ", usersAdded);
        if (usersAdded.Count >= 1 && processingQueue || usersAdded.Count > 1 && !processingQueue)
            CPH.TwitchReplyToMessage($"Shoutout Queue updated, {usersAdded.Count} added, {queueCount} now in queue. (Added: {addedUsers})", replyId, true, true);
        if (!processingQueue)
            SendShoutout();
        return true;
    }

    private async void SendShoutout()
    {
        if (processingQueue)
            return;
        processingQueue = true;
        try
        {
            while (shoutoutQueue.Count > 0)
            {
                string userId = shoutoutQueue.Dequeue().UserId;
                CPH.TwitchSendShoutoutById(userId);
                CallCustomTrigger(userId);
                CPH.SetTwitchUserVarById(userId, "lastShoutout", DateTime.Now, false);
                await Task.Delay(130000);
            }
        }
        finally
        {
            processingQueue = false;
        }
    }

    private void CallCustomTrigger(string userId)
    {
        var userInfo = GetInfoById(userId);
        var Data = new Dictionary<string, object>
        {
            {
                "event",
                "luponium_queuedShoutout"
            },
            {
                "channelUrl",
                $"https://www.twitch.tv/{userInfo.UserLogin}"},
            {
                "extensionVersion",
                extensionVersion
            },
        };

        foreach (var prop in userInfo.GetType().GetProperties())
        {
            var key = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
            data[key] = prop.GetValue(userInfo);
        }

        CPH.TriggerCodeEvent("luponium_queuedShoutout", Data);
    }
    private bool ShouldSkipUser(string userId)
    {
        DateTime lastShoutout = CPH.GetTwitchUserVarById<DateTime>(userId, "lastShoutout", false);

        bool onCooldown = DateTime.Now - lastShoutout < shoutoutCooldown;
        if (onCooldown)
            return true;
        if (shoutoutQueue.ContainsUser(userId))
            return true;
        if (CPH.GroupExists("[Streamers] Deny Shoutout") && CPH.UserIdInGroup(userId, Platform.Twitch, "[Streamers] Deny Shoutout"))
            return true;
        if (userId == CPH.TwitchGetBroadcaster().UserId)
            return true;
        if (userId == CPH.TwitchGetBot().UserId && CPH.TwitchGetBot().UserId != null)
            return true;
        return false;
    }

    public TwitchUserInfoEx GetInfoById(string id)
    {
        TwitchUserInfoEx info = CPH.TwitchGetExtendedUserInfoById(id);
        return info;
    }
}

public class ShoutoutQueue
{
    private readonly SortedDictionary<int, List<QueueItem>> _queues = new SortedDictionary<int, List<QueueItem>>();
    public void Enqueue(string userId, string userName, Priority priority)
    {
        int key = (int)priority;
        if (!_queues.ContainsKey(key))
            _queues[key] = new List<QueueItem>();
        _queues[key].Add(new QueueItem(userId, userName, priority));
    }

    public QueueItem Dequeue()
    {
        var first = _queues.Values.FirstOrDefault(q => q.Count > 0);
        if (first == null)
            throw new InvalidOperationException("Queue is empty.");
        var item = first[0];
        first.RemoveAt(0);
        return item;
    }

    public QueueItem DequeueByUserId(string userId)
    {
        foreach (var list in _queues.Values)
        {
            var item = list.FirstOrDefault(x => x.UserId == userId);
            if (item != null)
            {
                list.Remove(item);
                return item;
            }
        }

        return null;
    }

    public bool RemoveUser(string userId)
    {
        return _queues.Values.Any(list =>
        {
            var item = list.FirstOrDefault(x => x.UserId == userId);
            if (item == null)
                return false;
            list.Remove(item);
            return true;
        });
    }

    public bool ContainsUser(string userId)
    {
        return _queues.Values.SelectMany(q => q).Any(x => x.UserId == userId);
    }

    public int Count => _queues.Values.Sum(q => q.Count);

    public bool IsEmpty() => Count == 0;
    private IEnumerable<(QueueItem Item, int Position)> FlattenWithPosition()
    {
        return _queues.Values.SelectMany(q => q).Select((item, index) => (item, index + 1));
    }

    public int? GetPosition(string userId)
    {
        return FlattenWithPosition().Where(x => x.Item.UserId == userId).Select(x => (int?)x.Position).FirstOrDefault();
    }

    public List<string> GetFirstUserIds(int count)
    {
        return _queues.Values.SelectMany(q => q).Take(count).Select(x => x.UserId).ToList();
    }

    public string PeekFirstUserId()
    {
        return _queues.Values.SelectMany(q => q).Select(x => x.UserId).FirstOrDefault();
    }

    public List<(int Position, QueueItem Item)> PeekNextWithPositions(int count)
    {
        return FlattenWithPosition().Take(count).Select(x => (x.Position, x.Item)).ToList();
    }

    public List<QueueItemDto> GetQueueAsFlatList()
    {
        return FlattenWithPosition().Select(x => new QueueItemDto { Position = x.Position, UserId = x.Item.UserId, UserName = x.Item.UserName, Priority = x.Item.Priority.ToString() }).ToList();
    }

    public string ToJson(bool indented = false)
    {
        return JsonConvert.SerializeObject(GetQueueAsFlatList(), indented ? Formatting.Indented : Formatting.None);
    }

    public void Clear() => _queues.Clear();
}

public class QueueItem
{
    public string UserId { get; }
    public string UserName { get; }
    public Priority Priority { get; }

    public QueueItem(string userId, string userName, Priority priority)
    {
        UserId = userId;
        UserName = userName;
        Priority = priority;
    }

    public override string ToString()
    {
        return $"[{UserId}] {UserName}";
    }
}

public enum Priority
{
    Raid = 0,
    Default = 1
}

public class QueueItemDto
{
    public int Position { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Priority { get; set; }
}
