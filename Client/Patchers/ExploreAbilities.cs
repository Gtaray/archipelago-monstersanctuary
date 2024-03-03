using Archipelago.MonsterSanctuary.Client.Behaviors;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        public static void UpdateExploreItemTooltips()
        {
            if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                return;

            foreach (var item in GameData.ExploreActionUnlockItems[SlotData.ExploreAbilityLock])
            {
                if (item.Item is ExploreAbilityItem)
                {
                    var explore = (ExploreAbilityItem)item.Item;
                    explore.Tooltip = item.Tooltip;
                    continue;
                }
                if (item.Item is not UniqueItem)
                    continue;

                var unique = (UniqueItem)item.Item;
                unique.Description = item.Tooltip;
            }
        }

        [HarmonyPatch(typeof(GameController), "Start")]
        private static class GameController_Start
        {
            private static void Postfix(WorldData __instance)
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Archipelago.MonsterSanctuary.Client.data.explore_action_items.json"))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    GameData.ExploreActionUnlockItems = JsonConvert.DeserializeObject<Dictionary<ExploreAbilityLockType, List<ExploreActionUnlockItem>>>(json);

                    foreach (var kvp in GameData.ExploreActionUnlockItems)
                    {
                        var key = kvp.Key;
                        foreach (var item in kvp.Value)
                        {
                            // Check for existing items first.
                            // We can't update the tooltip here, becuase we don't know which
                            // tooltip to use. Handle that in SlotData
                            var existingItem = GameData.GetItemByName(item.Name);
                            if (existingItem != null)
                            {
                                item.Item = existingItem;
                                continue;
                            }

                            var go = new GameObject(item.Name);
                            go.SetActive(false);

                            var itemComp = go.AddComponent<ExploreAbilityItem>();
                            itemComp.Name = item.Name;
                            itemComp.Tooltip = item.Tooltip;

                            GameController.Instance.WorldData.Referenceables.Add(itemComp);

                            item.Item = itemComp;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "GetListByItemType")]
        private static class InventoryManager_GetListByItemType
        {
            private static void Postfix(InventoryManager __instance, BaseItem item, ref List<InventoryItem> __result)
            {
                if (item is ExploreAbilityItem)
                    __result = __instance.Uniques;
            }
        }

        [HarmonyPatch(typeof(PlayerFollower), "CanUseAction")]
        private static class PlayerFollower_CanUseAction
        {
            private static void Postfix(PlayerFollower __instance, ref bool __result)
            {
                //if (!APState.IsConnected)
                //    return;

                //if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                //    return;

                Patcher.Logger.LogInfo($"{__instance.Monster.Name} - CanBeUsed()");
                __result = false;
            }
        }

        [HarmonyPatch(typeof(InvisiblePlatform), "Update")]
        private static class InvisiblePlatform_Update
        {
            private static bool Prefix(InvisiblePlatform __instance)
            {
                //if (!APState.IsConnected)
                //    return true;

                //if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                //    return true;

                var ability = PlayerController.Instance.Follower.Monster.ExploreAction.GetComponent<ExploreAbility>();
                if (ability is not SecretVisionAbility)
                    return true;

                // Rather than trying to control the state of the invisible platform, we're simply going to suppress
                // the platforms updates. This effectively means it'll never turn on, and avoids having to make a reflection
                // call in an update loop.
                return false;
            }
        }

        [HarmonyPatch(typeof(DarkRoomLightManager), "LightenArea")]
        private static class DarkRoomLightManager_LightenArea
        {
            private static bool Prefix(DarkRoomLightManager __instance, Vector3 position)
            {
                //if (!APState.IsConnected)
                //    return true;

                //if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                //    return true;

                //  Only disable the follower's light source, nothing else.
                if (position != PlayerController.Instance.Follower.transform.position)
                    return true;

                return false;
            }
        }
    }
}
