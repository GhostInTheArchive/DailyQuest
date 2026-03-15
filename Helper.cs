using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using ProjectM.Scripting;
using ProjectM.Shared;

namespace DailyQuest;

internal static partial class Helper
{
    public static PrefabGUID GetPrefabGUID(Entity entity)
    {
        var entityManager = Core.EntityManager;
        try
        {
            return entityManager.GetComponentData<PrefabGUID>(entity);
        }
        catch
        {
            return new PrefabGUID(0);
        }
    }

    public static Entity AddItemToInventory(Entity recipient, PrefabGUID guid, int amount)
    {
        try
        {
            var serverGameManager = Core.Server.GetExistingSystemManaged<ServerScriptMapper>()._ServerGameManager;
            var inventoryResponse = serverGameManager.TryAddInventoryItem(recipient, guid, amount);
            return inventoryResponse.NewEntity;
        }
        catch (System.Exception e)
        {
            Core.LogException(e);
        }
        return new Entity();
    }

    private static readonly HashSet<int> SoulShards = new()
    {
        666638454,
        -1581189572,
        -1260254082,
        -21943750,
        1286615355
    };

    private static void HandleEquipment(Entity itemEntity, bool repair)
    {
        if (itemEntity == Entity.Null) return;
        if (!itemEntity.Has<Durability>()) return;

        var durability = itemEntity.Read<Durability>();
        durability.Value = repair ? durability.MaxDurability : 0;
        itemEntity.Write(durability);
    }

    public static void RepairArmor(Entity character, bool repair = true)
    {
        if (!character.Has<Equipment>()) return;

        var equipment = character.Read<Equipment>();
        HandleEquipment(equipment.ArmorChestSlot.SlotEntity.GetEntityOnServer(), repair);
        HandleEquipment(equipment.ArmorGlovesSlot.SlotEntity.GetEntityOnServer(), repair);
        HandleEquipment(equipment.ArmorLegsSlot.SlotEntity.GetEntityOnServer(), repair);
        HandleEquipment(equipment.ArmorFootgearSlot.SlotEntity.GetEntityOnServer(), repair);
    }

    public static void RepairAmulet(Entity character, bool repair = true)
    {
        if (!character.Has<Equipment>()) return;

        var equipment = character.Read<Equipment>();
        var grimoire = equipment.GrimoireSlot.SlotEntity.GetEntityOnServer();

        if (grimoire == Entity.Null || !grimoire.Has<PrefabGUID>()) return;

        var prefab = grimoire.Read<PrefabGUID>();
        if (SoulShards.Contains(prefab.GuidHash)) return;

        HandleEquipment(grimoire, repair);
    }

    public static void RepairWeapon(Entity character, bool repair = true)
    {
        if (!InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, character, out var inventory)) return;
        if (!Core.ServerGameManager.TryGetBuffer<InventoryBuffer>(inventory, out var buffer)) return;

        for (int i = 0; i < 8 && i < buffer.Length; i++)
        {
            var entry = buffer[i];
            var itemEntity = entry.ItemEntity.GetEntityOnServer();

            if (itemEntity == Entity.Null) continue;
            if (!itemEntity.Has<Durability>()) continue;
            if (!itemEntity.Has<EquippableData>()) continue;

            var equipData = itemEntity.Read<EquippableData>();
            if (equipData.EquipmentType != EquipmentType.Weapon) continue;

            HandleEquipment(itemEntity, repair);
        }
    }
}
