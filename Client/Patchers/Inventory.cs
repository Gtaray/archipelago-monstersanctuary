using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static int REFERENCEABLE_ID_START = 3000;

        [HarmonyPatch(typeof(WorldData), "BuildReferenceablesCache")]
        private static class WorldData_BuildReferenceablesCache
        {
            private static void Prefix(WorldData __instance)
            {
                int idOffset = 0;
                Patcher.Logger.LogInfo("Adding new items");
                foreach (var item in Items.GetNewItems())
                {
                    var id = REFERENCEABLE_ID_START + idOffset;
                    Patcher.Logger.LogInfo($"\tAdding new item: {item.Name} ({id})");

                    var go = new GameObject(item.GameObjectName);
                    go.SetActive(false);

                    var itemComp = go.AddComponent<ArchipelagoItem>();
                    itemComp.ID = id;
                    itemComp.Name = item.Name;
                    itemComp.Description = item.Description;
                    itemComp.Icon = item.Icon;

                    // Add a reference to the component back to the data item, so we have quick access to it later if we need it.
                    item.Item = (BaseItem)itemComp;

                    __instance.Referenceables.Add(itemComp);
                    idOffset += 1;
                }

                // Explore item stuff
                //foreach(var kvp in GameData.ExploreActionUnlockItems)
                //{
                //    var key = kvp.Key;
                //    foreach (var item in kvp.Value)
                //    {
                //        // Check for existing items first.
                //        // We can't update the tooltip here, becuase we don't know which
                //        // tooltip to use. Handle that in SlotData
                //        var existingItem = GameData.GetItemByName(item.Name);
                //        if (existingItem != null)
                //        {
                //            item.Item = existingItem;
                //            continue;
                //        }

                //        var go = new GameObject(item.Name);
                //        go.SetActive(false);

                //        var itemComp = go.AddComponent<ExploreAbilityItem>();
                //        itemComp.Name = item.Name;
                //        itemComp.Tooltip = item.Tooltip;
                //        itemComp.Monsters = item.Monsters;

                //        GameController.Instance.WorldData.Referenceables.Add(itemComp);

                //        item.Item = itemComp;
                //    }
                //}
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "GetListByItemType")]
        private static class InventoryManager_GetListByItemType
        {
            private static void Postfix(InventoryManager __instance, BaseItem item, ref List<InventoryItem> __result)
            {
                if (item is ArchipelagoItem)
                {
                    // TODO: add more cases here on an as-needed basis
                    var apItem = (ArchipelagoItem)item;
                    switch (apItem.Type)
                    {
                        case "unique":
                            __result = __instance.Uniques;
                            break;
                        default:
                            __result = __instance.Uniques;
                            break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "AddItem")]
        private class InventoryManager_AddItem
        {
            [UsedImplicitly]
            private static void Postfix(InventoryManager __instance, BaseItem item)
            {
                if (!ApState.IsConnected)
                    return;

                if (item is not Egg)
                    return;

                var egg = (Egg)item;
                Task.Run(() =>
                {
                    AddAbilityToDataStorage(egg.Monster);
                });
            }
        }
    }
}
