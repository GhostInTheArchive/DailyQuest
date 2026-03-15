using DailyQuest.Services;
using Unity.Entities;
using VampireCommandFramework;

namespace DailyQuest.Commands;

[CommandGroup("quest")]
internal static class QuestCommands
{

    [Command("info", shortHand: "i", description: "Show daily quest status for a specific player.", adminOnly: true)]
    public static void ShowPlayerQuest(ChatCommandContext ctx, string playerName)
    {
        var text = QuestService.BuildPlayerStatusTextByName(playerName);
        ctx.Reply(text);
    }

    [Command("daily", shortHand: "d", description: "Show your daily quests and progress.", adminOnly: false)]
    public static void Show(ChatCommandContext ctx)
    {
        var user = ctx.Event.User;
        if (user == default || !user.IsConnected)
            return;

        ulong sid = user.PlatformId;
        string name = user.CharacterName.ToString();

        var text = QuestService.BuildStatusText(sid, name);
        ctx.Reply(text);
    }

    [Command("reward", shortHand: "rw", description: "Claim all completed daily quest rewards.", adminOnly: false)]
    public static void Claim(ChatCommandContext ctx)
    {
        var user = ctx.Event.User;
        if (user == default || !user.IsConnected)
            return;

        ulong sid = user.PlatformId;
        string name = user.CharacterName.ToString();

        Entity character = ctx.Event.SenderCharacterEntity;
        if (character == Entity.Null)
        {
            ctx.Reply("<color=red>Character entity not found.</color>");
            return;
        }

        QuestService.TryClaim(ctx, sid, name, character);
    }

    [Command("reload", shortHand: "rl", description: "Reload quest_config.json", adminOnly: true)]
    public static void Reload(ChatCommandContext ctx)
    {
        QuestService.Reload();
        ctx.Reply("<color=green>Reloaded quest_config.json</color>");
    }
}