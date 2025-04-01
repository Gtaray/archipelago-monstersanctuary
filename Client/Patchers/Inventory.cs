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
            private static T CreateNewApItem<T>(NewItem item, int id) where T : ArchipelagoItem
            {
                var go = new GameObject($"AP {item.GameObjectName}");
                go.SetActive(false);

                var itemComp = go.AddComponent<T>();
                itemComp.ID = id;
                itemComp.Name = item.Name;
                itemComp.Description = item.Description;
                itemComp.Icon = item.Icon;
                itemComp.Type = item.Type;

                item.Item = itemComp;

                return itemComp;
            }

            private static void Prefix(WorldData __instance)
            {
                int idOffset = 0;
                Patcher.Logger.LogInfo("Adding new items");
                foreach (var item in Items.GetNewItems())
                {
                    int id = REFERENCEABLE_ID_START + idOffset;
                    Patcher.Logger.LogInfo($"\tAdding new item: {item.Name} ({id})");

                    ArchipelagoItem component = null;
                    if (item.Type == "exploreitem")
                        component = CreateNewApItem<ExploreAbilityItem>(item, id);
                    else
                        component = CreateNewApItem<ArchipelagoItem>(item, id);

                    __instance.Referenceables.Add(component);
                    idOffset += 1;
                }
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
                        case "exploreitem":
                        case "trap":
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
