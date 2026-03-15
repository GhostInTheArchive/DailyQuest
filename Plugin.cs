using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;
using System.Collections;
using DailyQuest.Services;

namespace DailyQuest;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    internal static Harmony Harmony;
    internal static ManualLogSource PluginLog;
    public static ManualLogSource LogInstance { get; private set; }

    public override void Load()
    {
        if (Application.productName != "VRisingServer")
            return;

        PluginLog = Log;
        LogInstance = Log;

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        try
        {
            QuestService.EnsureFilesExist();
            Log.LogInfo("DailyQuest config files ensured.");
        }
        catch (System.Exception e)
        {
            Log.LogError($"Failed to create DailyQuest files: {e}");
        }

        Harmony = new Harmony("dailyquest");
        Harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        CommandRegistry.RegisterAll();

        Core.StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        while (!Core.IsServerReady())
            yield return null;

        try
        {
            Core.InitializeAfterLoaded();
            Log.LogInfo("DailyQuest initialized.");
        }
        catch (System.Exception e)
        {
            Log.LogError($"DailyQuest initialization failed: {e}");
        }
    }

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        Harmony?.UnpatchSelf();
        return true;
    }
}