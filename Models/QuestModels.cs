using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DailyQuest.Models;

internal sealed class QuestConfig
{
    [JsonPropertyName("GearRepairOnClaim")]
    public bool RepairOnClaim { get; set; } = false;

    [JsonPropertyName("Quests")]
    public List<QuestDef> Quests { get; set; } = new();
}

internal sealed class QuestDef
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

internal sealed class RewardDef
{
    [JsonPropertyName("Prefab")]
    public int Prefab { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Amount")]
    public int Amount { get; set; }
}

internal sealed class PlayerQuestState
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
