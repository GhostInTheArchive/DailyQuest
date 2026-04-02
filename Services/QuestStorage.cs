using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DailyQuest.Config;
using DailyQuest.Models;

namespace DailyQuest.Services;

internal static partial class QuestService
{
    public static void EnsureFilesExist()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            if (!File.Exists(CONFIG_FILE))
            {
                File.WriteAllText(CONFIG_FILE, DefaultQuestConfig.QuestConfigJson);
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

            File.WriteAllText(CONFIG_FILE, DefaultQuestConfig.QuestConfigJson);
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
}