using DailyQuest.Services;
using VampireCommandFramework;

namespace DailyQuest.Commands;

[CommandGroup("questhook", "qh")]
internal static class WebhookCommands
{
    private const string TestMessage = "**[Daily quest]** - Webhook test message from **GGs DailyQuest by Del**.";

    [Command("on", "enabled", description: "Enable DailyQuest webhook sending.", adminOnly: true)]
    public static void On(ChatCommandContext ctx)
    {
        if (WebhookService.SetEnabled(true, out var error))
            ctx.Reply("<color=green>DailyQuest webhook enabled.</color>");
        else
            ctx.Reply($"<color=red>Failed:</color> {error}");
    }

    [Command("off", "disabled", description: "Disable DailyQuest webhook sending.", adminOnly: true)]
    public static void Off(ChatCommandContext ctx)
    {
        if (WebhookService.SetEnabled(false, out var error))
            ctx.Reply("<color=yellow>DailyQuest webhook disabled.</color>");
        else
            ctx.Reply($"<color=red>Failed:</color> {error}");
    }

    [Command("reload", "rl", description: "Reload webhook_config.json", adminOnly: true)]
    public static void Reload(ChatCommandContext ctx)
    {
        if (WebhookService.Reload(out var err))
            ctx.Reply("<color=green>webhook_config.json reloaded.</color>");
        else
            ctx.Reply($"<color=red>Reload failed:</color> {err}");
    }

    [Command("test", "t", description: "Send a test message to Discord", adminOnly: true)]
    public static void Test(ChatCommandContext ctx)
    {
        var (ok, error) = WebhookService.SendAsync(TestMessage).GetAwaiter().GetResult();

        if (ok)
            ctx.Reply("<color=green>DailyQuest webhook test sent.</color>");
        else
            ctx.Reply($"<color=red>Webhook test failed:</color> {error}");
    }
}
