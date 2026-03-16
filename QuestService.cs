using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;
using Unity.Collections;

namespace DailyQuest.Services;

internal static class QuestService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "quest_config.json");
    private static readonly string PLAYER_FILE = Path.Combine(CONFIG_DIR, "quest_player.json");

    private static readonly object _lock = new();

    private static QuestConfig _config = new();
    private static Dictionary<string, QuestDef> _questsById = new(StringComparer.Ordinal);
    private static List<QuestDef> _enabledQuests = new();
    private static List<QuestDef> _enabledEasyQuests = new();
    private static List<QuestDef> _enabledMediumQuests = new();
    private static List<QuestDef> _enabledHardQuests = new();
    private static Dictionary<string, PlayerQuestState> _players = new();

    private static DateTime _lastDate = DateTime.MinValue.Date;

    private const int SaveIntervalSeconds = 10;
    private static bool _dirty;
    private static DateTime _nextSave = DateTime.MinValue;
    private static Coroutine _tickCoroutine;
    private static bool _initialized;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static void EnsureFilesExist()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            if (!File.Exists(CONFIG_FILE))
            {
                var sample = CreateDefaultConfig();
                File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(sample, JsonOpts));
                Core.Log.LogInfo($"[Quest] Created config: {CONFIG_FILE}");
            }

            if (!File.Exists(PLAYER_FILE))
            {
                File.WriteAllText(PLAYER_FILE, "{}");
                Core.Log.LogInfo($"[Quest] Created player data: {PLAYER_FILE}");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void Initialize()
    {
        lock (_lock)
        {
            EnsureFilesExist();
            LoadConfig_NoLock(createIfMissing: true);
            LoadPlayers_NoLock();

            _lastDate = DateTime.Now.Date;

            if (_tickCoroutine == null)
                _tickCoroutine = Core.StartCoroutine(TickLoop());

            _initialized = true;
        }

        Core.Log.LogInfo("[Quest] QuestService initialized");
    }

    public static void Reload()
    {
        lock (_lock)
        {
            EnsureFilesExist();
            LoadConfig_NoLock(createIfMissing: false);
            _initialized = true;
        }

        Core.Log.LogInfo("[Quest] quest_config.json reloaded");
    }

    public static void EnsureAssignedForToday(ulong sid, string playerName)
    {
        if (sid == 0) return;

        lock (_lock)
        {
            EnsureInitialized_NoLock();

            string today = TodayString();
            var key = sid.ToString();
            bool changed = false;

            if (!_players.TryGetValue(key, out var st) || st == null)
            {
                st = new PlayerQuestState
                {
                    SteamId = sid,
                    Name = playerName ?? "",
                    Date = today,

                    EasyQuestId = "",
                    EasyProgress = 0,
                    EasyClaimed = false,

                    MediumQuestId = "",
                    MediumProgress = 0,
                    MediumClaimed = false,

                    HardQuestId = "",
                    HardProgress = 0,
                    HardClaimed = false
                };
                changed = true;
            }
            else
            {
                var newName = playerName ?? st.Name ?? "";
                if (!string.Equals(st.Name, newName, StringComparison.Ordinal))
                {
                    st.Name = newName;
                    changed = true;
                }
            }

            bool dayChanged = !string.Equals(st.Date, today, StringComparison.Ordinal);
            if (dayChanged)
            {
                st.Date = today;

                st.EasyQuestId = "";
                st.EasyProgress = 0;
                st.EasyClaimed = false;

                st.MediumQuestId = "";
                st.MediumProgress = 0;
                st.MediumClaimed = false;

                st.HardQuestId = "";
                st.HardProgress = 0;
                st.HardClaimed = false;

                changed = true;
            }

            if (string.IsNullOrWhiteSpace(st.EasyQuestId))
            {
                var pool = _enabledEasyQuests.Count > 0 ? _enabledEasyQuests : _enabledQuests;
                st.EasyQuestId = PickQuestId_NoLock(sid, today, "easy", pool);
                st.EasyProgress = 0;
                st.EasyClaimed = false;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(st.MediumQuestId))
            {
                st.MediumQuestId = PickQuestId_NoLock(sid, today, "medium", _enabledMediumQuests);
                st.MediumProgress = 0;
                st.MediumClaimed = false;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(st.HardQuestId))
            {
                st.HardQuestId = PickQuestId_NoLock(sid, today, "hard", _enabledHardQuests);
                st.HardProgress = 0;
                st.HardClaimed = false;
                changed = true;
            }

            if (changed)
            {
                _players[key] = st;
                MarkDirty_NoLock();
            }
        }
    }

    public static string BuildStatusText(ulong sid, string playerName)
    {
        EnsureAssignedForToday(sid, playerName);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
                return "<color=red>No data.</color>";

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            var hardQuest = GetQuestById_NoLock(st.HardQuestId);

            string easyBlock = BuildQuestBlock_NoLock("1", easyQuest, st.EasyProgress, st.EasyClaimed);
            string mediumBlock = BuildQuestBlock_NoLock("2", mediumQuest, st.MediumProgress, st.MediumClaimed);
            string hardBlock = BuildQuestBlock_NoLock("3", hardQuest, st.HardProgress, st.HardClaimed);

            return
                $"<color=yellow>Daily Quests (Reset in {GetNextResetText()})</color>\n" +
                easyBlock + "\n\n" +
                mediumBlock + "\n\n" +
                hardBlock + "\n";
        }
    }

    public static string BuildPlayerStatusTextByName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return "<color=red>Invalid player name.</color>";

        lock (_lock)
        {
            EnsureInitialized_NoLock();

            var st = _players.Values.FirstOrDefault(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.Name) &&
                string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (st == null)
                return $"<color=red>Player not found in quest data.</color>";

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            var hardQuest = GetQuestById_NoLock(st.HardQuestId);

            string easyBlock = BuildAdminQuestLine_NoLock("1", easyQuest, st.EasyProgress, st.EasyClaimed);
            string mediumBlock = BuildAdminQuestLine_NoLock("2", mediumQuest, st.MediumProgress, st.MediumClaimed);
            string hardBlock = BuildAdminQuestLine_NoLock("3", hardQuest, st.HardProgress, st.HardClaimed);

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            bool isToday = string.Equals(st.Date, today, StringComparison.Ordinal);

            string header = $"<color=yellow>Daily Quest: <color=white>{st.Name}</color></color>";
            string dateLine = isToday
                ? $"<color=#87CEFA>Quest Date</color>: {st.Date}"
                : $"<color=#87CEFA>Quest Date</color>: {st.Date} (Outdated)";

            return
                header + "\n" +
                easyBlock + "\n" +
                mediumBlock + "\n" +
                hardBlock + "\n" +
                dateLine;
        }
    }

    public static bool TryClaim(ChatCommandContext ctx, ulong sid, string playerName, Entity characterEntity)
    {
        EnsureAssignedForToday(sid, playerName);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
            {
                ctx.Reply("<color=red>No quest data.</color>");
                return false;
            }

            var userEntity = ctx.Event.SenderUserEntity;
            bool claimedAny = false;
            var claimedNames = new List<string>(3);

            if (TryClaimOne_NoLock(ctx, userEntity, characterEntity, "1", st.EasyQuestId, st.EasyProgress, st.EasyClaimed, out var replyEasy))
            {
                st.EasyClaimed = true;
                ctx.Reply(replyEasy);
                claimedAny = true;

                if (_config.RepairOnClaim)
                {
                    try
                    {
                        Helper.RepairAmulet(characterEntity);
                    }
                    catch (Exception e)
                    {
                        Core.LogException(e);
                    }
                }

                var q = GetQuestById_NoLock(st.EasyQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 1");
            }

            if (TryClaimOne_NoLock(ctx, userEntity, characterEntity, "2", st.MediumQuestId, st.MediumProgress, st.MediumClaimed, out var replyMedium))
            {
                st.MediumClaimed = true;
                ctx.Reply(replyMedium);
                claimedAny = true;

                if (_config.RepairOnClaim)
                {
                    try
                    {
                        Helper.RepairArmor(characterEntity);
                    }
                    catch (Exception e)
                    {
                        Core.LogException(e);
                    }
                }

                var q = GetQuestById_NoLock(st.MediumQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 2");
            }

            if (TryClaimOne_NoLock(ctx, userEntity, characterEntity, "3", st.HardQuestId, st.HardProgress, st.HardClaimed, out var replyHard))
            {
                st.HardClaimed = true;
                ctx.Reply(replyHard);
                claimedAny = true;

                if (_config.RepairOnClaim)
                {
                    try
                    {
                        Helper.RepairWeapon(characterEntity);
                    }
                    catch (Exception e)
                    {
                        Core.LogException(e);
                    }
                }

                var q = GetQuestById_NoLock(st.HardQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 3");
            }

            if (!claimedAny)
                return false;

            _players[key] = st;
            MarkDirty_NoLock();
            ForceSave_NoLock();

            string list = string.Join(", ", claimedNames);
            string msg = $"<color=white>{playerName}</color> completed and claimed rewards for: {list}. Use <color=green>.quest daily</color> to check your quests.";

            var fs = new FixedString512Bytes(msg);
            ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref fs);

            return true;
        }
    }

    public static void OnKilledPrefab(ulong sid, int diedPrefabGuidHash, User user, string playerNameForEnsure = "")
    {
        if (sid == 0 || diedPrefabGuidHash == 0) return;

        EnsureAssignedForToday(sid, playerNameForEnsure);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
                return;

            bool changed = false;

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            if (easyQuest != null &&
                easyQuest.TargetPrefabs != null &&
                easyQuest.TargetPrefabs.Length > 0 &&
                easyQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, easyQuest.RequiredKills);
                if (need > 0 && st.EasyProgress < need)
                {
                    st.EasyProgress++;
                    if (st.EasyProgress > need) st.EasyProgress = need;
                    changed = true;

                    SendQuestToast(user, "1", easyQuest.Name, st.EasyProgress, need, st.EasyProgress >= need);
                }
            }

            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            if (mediumQuest != null &&
                mediumQuest.TargetPrefabs != null &&
                mediumQuest.TargetPrefabs.Length > 0 &&
                mediumQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, mediumQuest.RequiredKills);
                if (need > 0 && st.MediumProgress < need)
                {
                    st.MediumProgress++;
                    if (st.MediumProgress > need) st.MediumProgress = need;
                    changed = true;

                    SendQuestToast(user, "2", mediumQuest.Name, st.MediumProgress, need, st.MediumProgress >= need);
                }
            }

            var hardQuest = GetQuestById_NoLock(st.HardQuestId);
            if (hardQuest != null &&
                hardQuest.TargetPrefabs != null &&
                hardQuest.TargetPrefabs.Length > 0 &&
                hardQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, hardQuest.RequiredKills);
                if (need > 0 && st.HardProgress < need)
                {
                    st.HardProgress++;
                    if (st.HardProgress > need) st.HardProgress = need;
                    changed = true;

                    SendQuestToast(user, "3", hardQuest.Name, st.HardProgress, need, st.HardProgress >= need);
                }
            }

            if (changed)
            {
                _players[key] = st;
                MarkDirty_NoLock();
            }
        }
    }

    private static void EnsureInitialized_NoLock()
    {
        if (_initialized)
            return;

        EnsureFilesExist();
        LoadConfig_NoLock(createIfMissing: true);
        LoadPlayers_NoLock();

        _lastDate = DateTime.Now.Date;
        _initialized = true;

        Core.Log.LogInfo("[Quest] QuestService lazy-initialized");
    }

    private static bool TryClaimOne_NoLock(
        ChatCommandContext ctx,
        Entity userEntity,
        Entity characterEntity,
        string label,
        string questId,
        int progress,
        bool alreadyClaimed,
        out string replyText)
    {
        replyText = "";

        if (string.IsNullOrWhiteSpace(questId))
        {
            ctx.Reply($"<color=yellow>Quest {label} not assigned today.</color>");
            return false;
        }

        var quest = GetQuestById_NoLock(questId);
        if (quest == null)
        {
            ctx.Reply($"<color=red>Quest {label} not found in config.</color>");
            return false;
        }

        int need = Math.Max(0, quest.RequiredKills);
        if (need <= 0)
        {
            ctx.Reply($"<color=red>Quest {label} has invalid requiredKills.</color>");
            return false;
        }

        if (progress < need)
        {
            ctx.Reply($"<color=yellow>Quest {label} not completed yet.</color> Use <color=green>.quest daily</color> to check.");
            return false;
        }

        if (alreadyClaimed)
        {
            ctx.Reply($"<color=yellow>Quest {label} reward already claimed today.</color>");
            return false;
        }

        if (!TryResolveRewardPrefab_NoLock(quest, out var rewardPrefab, out var rewardName))
        {
            ctx.Reply("<color=red>Reward prefab not configured correctly.</color>");
            return false;
        }

        int amount = Math.Max(0, quest.Reward?.Amount ?? 0);
        if (amount <= 0)
        {
            ctx.Reply("<color=red>Reward amount invalid.</color>");
            return false;
        }

        Helper.AddItemToInventory(characterEntity, rewardPrefab, amount);
        replyText = $"<color=green>Quest {label} reward claimed</color> <color=#87CEFA>{amount}x {rewardName}</color>";
        return true;
    }

    private static string BuildQuestBlock_NoLock(string label, QuestDef quest, int progress, bool claimed)
    {
        if (quest == null)
            return $"<color=#87CEFA>Quest {label}</color>: <color=yellow>No quest {label.ToLowerInvariant()} configured.</color>";

        int need = Math.Max(0, quest.RequiredKills);
        int prog = Math.Max(0, progress);
        if (prog > need) prog = need;

        string rewardText = GetRewardDisplay_NoLock(quest);

        bool done = need > 0 && prog >= need;
        string statusText = done
            ? (claimed
                ? "Reward claimed"
                : "Completed! Claim with <color=green>.quest reward</color>")
            : $"Progress {prog}/{need}";

        return
            $"<color=#87CEFA>Quest {label}</color>: {quest.Name} x{need}\n" +
            $"Reward: {rewardText}\n" +
            $"Status: {statusText}";
    }

    private static string BuildAdminQuestLine_NoLock(string label, QuestDef quest, int progress, bool claimed)
    {
        if (quest == null)
            return $"<color=#87CEFA>Quest {label}</color>: <color=yellow>No quest configured.</color>";

        int need = Math.Max(0, quest.RequiredKills);
        int prog = Math.Max(0, progress);
        if (prog > need) prog = need;

        string status = claimed
            ? "(Reward claimed)"
            : prog >= need
                ? "(Completed)"
                : $"({prog}/{need})";

        string questName = string.IsNullOrWhiteSpace(quest.Name) ? $"Quest {label}" : quest.Name;

        return $"<color=#87CEFA>Quest {label}</color>: {questName} {status}";
    }

    private static void SendQuestToast(User user, string label, string questName, int prog, int need, bool done)
    {
        if (user == default || !user.IsConnected)
            return;

        if (need < 0) need = 0;
        if (prog < 0) prog = 0;
        if (prog > need) prog = need;

        string status = done
            ? $"<color=green>{prog}/{need}</color>, Claim: <color=green>.quest reward</color>"
            : $"<color=yellow>{prog}/{need}</color>, Details: <color=green>.quest daily</color>";

        string msg = $"<color=#87CEFA>Quest {label}</color>: <color=white>{questName}</color> {status}";

        var fs = new FixedString512Bytes(msg);
        ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref fs);
    }

    private static void RollDateIfNeeded_NoLock()
    {
        var nowDate = DateTime.Now.Date;
        if (nowDate == _lastDate) return;

        _lastDate = nowDate;
        Core.Log.LogInfo($"[Quest] New day detected: {_lastDate:yyyy-MM-dd}");
    }

    private static string TodayString() => DateTime.Now.ToString("yyyy-MM-dd");

    private static string GetNextResetText()
    {
        var now = DateTime.Now;
        var nextReset = now.Date.AddDays(1);

        var remaining = nextReset - now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        int hours = (int)remaining.TotalHours;
        return $"{hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private static QuestDef GetQuestById_NoLock(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        if (_questsById.TryGetValue(id, out var q))
            return q;

        return _config?.Quests?.FirstOrDefault(x => x != null && string.Equals(x.Id, id, StringComparison.Ordinal));
    }

    private static string PickQuestId_NoLock(ulong sid, string date, string difficulty, List<QuestDef> pool)
    {
        if (pool == null || pool.Count == 0)
            return "";

        int seed = HashCode.Combine(sid, date, difficulty);
        var rnd = new System.Random(seed);
        var pick = pool[rnd.Next(pool.Count)];
        return pick?.Id ?? "";
    }

    private static string GetRewardDisplay_NoLock(QuestDef quest)
    {
        if (quest?.Reward == null) return "None";

        string name = quest.Reward.Name ?? "";
        int amount = quest.Reward.Amount;

        if (string.IsNullOrWhiteSpace(name))
        {
            int prefab = quest.Reward.Prefab;
            if (prefab != 0)
            {
                try { name = new PrefabGUID(prefab).LookupName(); }
                catch { name = prefab.ToString(); }
            }
            else
            {
                name = "Unknown";
            }
        }

        return $"{Math.Max(0, amount)}x {name}";
    }

    private static bool TryResolveRewardPrefab_NoLock(QuestDef quest, out PrefabGUID rewardPrefab, out string rewardName)
    {
        rewardPrefab = new PrefabGUID(0);
        rewardName = "Reward";

        if (quest?.Reward == null) return false;

        int prefabInt = quest.Reward.Prefab;
        if (prefabInt == 0) return false;

        rewardPrefab = new PrefabGUID(prefabInt);

        rewardName = quest.Reward.Name;
        if (string.IsNullOrWhiteSpace(rewardName))
        {
            try { rewardName = rewardPrefab.LookupName(); }
            catch { rewardName = prefabInt.ToString(); }
        }

        return true;
    }

    private static void MarkDirty_NoLock()
    {
        _dirty = true;
        if (_nextSave == DateTime.MinValue)
            _nextSave = DateTime.Now.AddSeconds(SaveIntervalSeconds);
    }

    internal static void Tick()
    {
        lock (_lock)
        {
            RollDateIfNeeded_NoLock();

            if (!_dirty) return;
            if (_nextSave != DateTime.MinValue && DateTime.Now < _nextSave) return;

            SavePlayers_NoLock();
            _dirty = false;
            _nextSave = DateTime.MinValue;
        }
    }

    private static void ForceSave_NoLock()
    {
        SavePlayers_NoLock();
        _dirty = false;
        _nextSave = DateTime.MinValue;
    }

    private static IEnumerator TickLoop()
    {
        while (true)
        {
            try { Tick(); }
            catch (Exception e) { Core.LogException(e); }

            yield return new WaitForSeconds(1f);
        }
    }

    private static void RebuildQuestIndex_NoLock()
    {
        _questsById = new Dictionary<string, QuestDef>(StringComparer.Ordinal);
        _enabledQuests = new List<QuestDef>();
        _enabledEasyQuests = new List<QuestDef>();
        _enabledMediumQuests = new List<QuestDef>();
        _enabledHardQuests = new List<QuestDef>();

        if (_config?.Quests == null)
        {
            Core.Log.LogWarning("[Quest] _config.Quests is null");
            return;
        }

        foreach (var q in _config.Quests)
        {
            if (q == null) continue;

            if (!string.IsNullOrWhiteSpace(q.Id))
                _questsById[q.Id] = q;

            _enabledQuests.Add(q);

            var diff = (q.Difficulty ?? "easy").Trim().ToLowerInvariant();
            if (diff == "hard")
                _enabledHardQuests.Add(q);
            else if (diff == "medium")
                _enabledMediumQuests.Add(q);
            else
                _enabledEasyQuests.Add(q);
        }

        Core.Log.LogInfo($"[Quest] Loaded quests: all {_enabledQuests.Count}, easy {_enabledEasyQuests.Count}, medium {_enabledMediumQuests.Count}, hard {_enabledHardQuests.Count}");
    }

    private static void LoadConfig_NoLock(bool createIfMissing)
    {
        Directory.CreateDirectory(CONFIG_DIR);

        if (!File.Exists(CONFIG_FILE))
        {
            if (!createIfMissing)
            {
                Core.Log.LogWarning($"[Quest] Config not found: {CONFIG_FILE}");
                _config = new QuestConfig();
                RebuildQuestIndex_NoLock();
                return;
            }

            var sample = CreateDefaultConfig();
            File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(sample, JsonOpts));
            Core.Log.LogInfo($"[Quest] Created config: {CONFIG_FILE}");
        }

        try
        {
            var json = File.ReadAllText(CONFIG_FILE);
            _config = JsonSerializer.Deserialize<QuestConfig>(json, JsonOpts) ?? new QuestConfig();
            _config.Quests ??= new List<QuestDef>();
            RebuildQuestIndex_NoLock();
        }
        catch (Exception e)
        {
            Core.LogException(e);
            _config = new QuestConfig();
            RebuildQuestIndex_NoLock();
        }
    }

    private static void LoadPlayers_NoLock()
    {
        try
        {
            if (!File.Exists(PLAYER_FILE))
            {
                _players = new Dictionary<string, PlayerQuestState>();
                return;
            }

            string json = File.ReadAllText(PLAYER_FILE);
            if (string.IsNullOrWhiteSpace(json))
            {
                _players = new Dictionary<string, PlayerQuestState>();
                return;
            }

            _players = JsonSerializer.Deserialize<Dictionary<string, PlayerQuestState>>(json, JsonOpts)
                       ?? new Dictionary<string, PlayerQuestState>();
        }
        catch (Exception e)
        {
            Core.LogException(e);
            _players = new Dictionary<string, PlayerQuestState>();
        }
    }

    private static void SavePlayers_NoLock()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(_players, JsonOpts);
            File.WriteAllBytes(PLAYER_FILE, bytes);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    private static QuestConfig CreateDefaultConfig()
    {
        return new QuestConfig
        {
            RepairOnClaim = false,
            Quests = new List<QuestDef>
            {
                new QuestDef
                {
                    Id = "1_1",
                    Name = "Kill Wolves",
                    Difficulty = "easy",
                    TargetPrefabs = new[] { -1418430647, -1554428547, -578677530, 134039094, -218175217, 572729167, 616274140 },
                    RequiredKills = 10,
                    Reward = new RewardDef
                    {
                        Prefab = 2103989354,
                        Name = "Stygian Shards",
                        Amount = 300
                    }
                },
                new QuestDef
                {
                    Id = "2_1",
                    Name = "Kill Knights",
                    Difficulty = "medium",
                    TargetPrefabs = new[] { -930333806, 794228023 },
                    RequiredKills = 10,
                    Reward = new RewardDef
                    {
                        Prefab = 576389135,
                        Name = "Greater Stygian Shards",
                        Amount = 400
                    }
                },
                new QuestDef
                {
                    Id = "3_1",
                    Name = "Defeat Dracula",
                    Difficulty = "hard",
                    TargetPrefabs = new[] { -327335305 },
                    RequiredKills = 3,
                    Reward = new RewardDef
                    {
                        Prefab = 28358550,
                        Name = "Primal Stygian Shards",
                        Amount = 500
                    }
                }
            }
        };
    }

    private sealed class QuestConfig
    {
        [JsonPropertyName("GearRepairOnClaim")]
        public bool RepairOnClaim { get; set; } = false;

        [JsonPropertyName("Quests")]
        public List<QuestDef> Quests { get; set; } = new();
    }

    private sealed class QuestDef
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Difficulty")]
        public string Difficulty { get; set; } = "easy";

        [JsonPropertyName("TargetPrefabs")]
        public int[] TargetPrefabs { get; set; } = Array.Empty<int>();

        [JsonPropertyName("RequiredKills")]
        public int RequiredKills { get; set; }

        [JsonPropertyName("Reward")]
        public RewardDef Reward { get; set; }
    }

    private sealed class RewardDef
    {
        [JsonPropertyName("Prefab")]
        public int Prefab { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Amount")]
        public int Amount { get; set; }
    }

    private sealed class PlayerQuestState
    {
        [JsonPropertyName("SteamID")]
        public ulong SteamId { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Date")]
        public string Date { get; set; }

        [JsonPropertyName("EasyQuestId")]
        public string EasyQuestId { get; set; }

        [JsonPropertyName("EasyProgress")]
        public int EasyProgress { get; set; }

        [JsonPropertyName("EasyClaimed")]
        public bool EasyClaimed { get; set; }

        [JsonPropertyName("MediumQuestId")]
        public string MediumQuestId { get; set; }

        [JsonPropertyName("MediumProgress")]
        public int MediumProgress { get; set; }

        [JsonPropertyName("MediumClaimed")]
        public bool MediumClaimed { get; set; }

        [JsonPropertyName("HardQuestId")]
        public string HardQuestId { get; set; }

        [JsonPropertyName("HardProgress")]
        public int HardProgress { get; set; }

        [JsonPropertyName("HardClaimed")]
        public bool HardClaimed { get; set; }
    }
}
