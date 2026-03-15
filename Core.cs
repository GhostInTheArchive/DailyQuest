using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DailyQuest.Services;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using Unity.Entities;
using UnityEngine;

namespace DailyQuest;

internal static class Core
{
    private static World _server;
    private static MonoBehaviour monoBehaviour;
    private static bool _hasInitialized = false;

    public const int MAX_REPLY_LENGTH = 509;

    public static World Server
    {
        get
        {
            _server ??= GetWorld("Server");
            return _server;
        }
    }

    public static EntityManager EntityManager => Server.EntityManager;
    public static GameDataSystem GameDataSystem => Server.GetExistingSystemManaged<GameDataSystem>();
    public static PrefabCollectionSystem PrefabCollectionSystem { get; internal set; }
    public static PrefabCollectionSystem PrefabCollection => Server.GetExistingSystemManaged<PrefabCollectionSystem>();
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static ManualLogSource Log => Plugin.PluginLog;

    public static void LogException(System.Exception e, [CallerMemberName] string caller = null)
    {
        Log.LogError($"Failure in {caller}\nMessage: {e.Message} Inner:{e.InnerException?.Message}\n\nStack: {e.StackTrace}\nInner Stack: {e.InnerException?.StackTrace}");
    }

    internal static bool IsServerReady()
    {
        _server ??= GetWorld("Server");
        if (_server == null) return false;

        var prefabCollection = _server.GetExistingSystemManaged<PrefabCollectionSystem>();
        return prefabCollection != null;
    }

    internal static void InitializeAfterLoaded()
    {
        if (_hasInitialized) return;
        if (!IsServerReady())
        {
            Log.LogWarning("Server world is not ready yet.");
            return;
        }

        PrefabCollectionSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
        ServerScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();

        QuestService.Initialize();

        _hasInitialized = true;
        Log.LogInfo($"{nameof(InitializeAfterLoaded)} completed");
    }

    private static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name)
                return world;
        }

        return null;
    }

    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("DailyQuest");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            Object.DontDestroyOnLoad(go);
        }

        return monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }

    public static void StopCoroutine(Coroutine coroutine)
    {
        if (monoBehaviour == null) return;
        monoBehaviour.StopCoroutine(coroutine);
    }
}