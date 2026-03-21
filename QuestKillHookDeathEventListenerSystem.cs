using System;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Unity.Collections;
using Unity.Entities;
using DailyQuest.Services;

namespace DailyQuest.Patches;

[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
internal static class QuestKillHookDeathEventListenerSystem
{
    private static bool TryResolvePlayerFromKiller(Entity killer, EntityManager em, out Entity playerChar, out Entity userEnt, out ulong sid)
    {
        playerChar = Entity.Null;
        userEnt = Entity.Null;
        sid = 0;

        if (killer == Entity.Null || !em.Exists(killer))
            return false;

        Entity cur = killer;
        for (int i = 0; i < 12; i++)
        {
            if (cur == Entity.Null || !em.Exists(cur))
                return false;

            if (cur.Has<PlayerCharacter>())
            {
                playerChar = cur;
                var pc = cur.Read<PlayerCharacter>();
                userEnt = pc.UserEntity;

                if (userEnt != Entity.Null && em.Exists(userEnt) && userEnt.Has<User>())
                {
                    sid = userEnt.Read<User>().PlatformId;
                    return sid != 0;
                }
                return false;
            }

            if (cur.Has<EntityOwner>())
            {
                var owner = cur.Read<EntityOwner>().Owner;
                if (owner == Entity.Null || owner == cur)
                    return false;

                cur = owner;
                continue;
            }

            return false;
        }

        return false;
    }

    [HarmonyPrefix]
    private static void Prefix(DeathEventListenerSystem __instance)
    {
        try
        {
            var em = __instance.EntityManager;

            /*
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<DeathEvent>());
            if (q.CalculateEntityCount() == 0)
                return;
            */

            // Use existing system query when an equivalent is available.
            var events = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);

            try
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var ev = events[i];

                    var died = ev.Died;
                    if (died == Entity.Null || !__instance.Exists(died))
                        continue;

                    if (died.Has<PlayerCharacter>())
                        continue;

                    if (!TryResolvePlayerFromKiller(ev.Killer, em, out _, out var userEnt, out var sid))
                        continue;

                    var diedPrefab = Helper.GetPrefabGUID(died);
                    if (diedPrefab.GuidHash == 0)
                        continue;

                    if (!userEnt.Has<User>())
                        continue;

                    var user = userEnt.Read<User>();
                    QuestService.OnKilledPrefab(sid, diedPrefab.GuidHash, user, user.CharacterName.ToString());
                }
            }
            finally
            {
                if (events.IsCreated) events.Dispose();
                QuestService.Tick();
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}
