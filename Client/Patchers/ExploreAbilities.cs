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
        #region Persistence
        private const string EXPLORE_ITEMS_FILENAME = "archipelago_explore_items.json";

        public static void DeleteExploreItemCache()
        {
            if (File.Exists(EXPLORE_ITEMS_FILENAME))
                File.Delete(EXPLORE_ITEMS_FILENAME);
        }

        private static void SaveExploreItemsReceived()
        {
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var items = PlayerController.Instance.Inventory.Uniques
                    .Where(i => i.Item is ExploreAbilityItem)
                    .Select(i => i.GetName());

                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(items));
                File.WriteAllBytes(Path.Combine(rawPath, EXPLORE_ITEMS_FILENAME), bytes);
            }
        }

        private static void LoadExploreItemsReceived()
        {
            if (File.Exists(EXPLORE_ITEMS_FILENAME))
            {
                Patcher.Logger.LogInfo("LoadExploreItemsReceived()");
                var reader = File.OpenText(EXPLORE_ITEMS_FILENAME);
                var content = reader.ReadToEnd();
                reader.Close();
                List<string> items = JsonConvert.DeserializeObject<List<string>>(content);

                foreach (var itemName in items)
                {
                    Patcher.Logger.LogInfo("\tLoaded item: " + itemName);
                    var item = GameData.GetItemByName<ExploreAbilityItem>(itemName);

                    if (item == null)
                    {
                        Patcher.Logger.LogWarning("\tItem not found in World Data");
                        continue;
                    }

                    // Don't add the same item twice
                    if (PlayerController.Instance.Inventory.Uniques.Any(i => i.GetName() == item.GetName()))
                        continue;

                    PlayerController.Instance.Inventory.AddItem(item);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "SaveGame")]
        private static class InventoryManager_SaveGame
        {
            private static void Postfix(SaveGameData saveGameData)
            {
                SaveExploreItemsReceived();

                // Go through the save game inventory and remove any new items.
                // We'll add them back to the player when they connect and resync
                saveGameData.Inventory.RemoveAll(i => i.Item is ExploreAbilityItem);
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "LoadGame")]
        private static class InventoryManager_LoadGame
        {
            private static void Postfix()
            {
                LoadExploreItemsReceived();
            }
        }
        #endregion

        #region Data Initialization
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
                            itemComp.Monsters = item.Monsters;

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
        #endregion

        private static IEnumerable<string> GetAvailableMonsterAbilities()
        {
            var monsters = PlayerController.Instance.Inventory.Uniques
                .Where(i => i.Item is ExploreAbilityItem)
                .Select(i => i.Item as ExploreAbilityItem)
                .SelectMany(i => i.Monsters)
                .ToList();

            if (PlayerController.Instance.Inventory.HasUniqueItem(EUniqueItemId.Ahrimaaya))
            {
                var ahrimaaya = GameData.ExploreActionUnlockItems[SlotData.ExploreAbilityLock].First(i => i.Name == "Ahrimaaya");
                monsters.AddRange(ahrimaaya.Monsters);
            }

            return monsters;
        }

        [HarmonyPatch(typeof(PlayerFollower), "CanUseAction")]
        private static class PlayerFollower_CanUseAction
        {
            private static void Postfix(PlayerFollower __instance, ref bool __result)
            {
                if (!APState.IsConnected)
                    return;

                if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                    return;

                __result = GetAvailableMonsterAbilities().Contains(__instance.Monster.Name);
                Patcher.Logger.LogInfo($"{__instance.Monster.Name} - CanBeUsed(): " + __result);
            }
        }

        [HarmonyPatch(typeof(InvisiblePlatform), "Update")]
        private static class InvisiblePlatform_Update
        {
            private static bool Prefix(InvisiblePlatform __instance)
            {
                if (!APState.IsConnected)
                    return true;

                if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                    return true;

                var ability = PlayerController.Instance.Follower.Monster.ExploreAction.GetComponent<ExploreAbility>();
                if (ability is not SecretVisionAbility)
                    return true;

                // Rather than trying to control the state of the invisible platform, we're simply going to suppress
                // the platforms updates. This effectively means it'll never turn on, and avoids having to make a reflection
                // call in an update loop
                return GetAvailableMonsterAbilities().Contains(PlayerController.Instance.Follower.Monster.GetName());
            }
        }

        [HarmonyPatch(typeof(DarkRoomLightManager), "LightenArea")]
        private static class DarkRoomLightManager_LightenArea
        {
            private static bool Prefix(DarkRoomLightManager __instance, Vector3 position)
            {
                if (!APState.IsConnected)
                    return true;

                if (SlotData.ExploreAbilityLock == ExploreAbilityLockType.Off)
                    return true;

                //  Only disable the follower's light source, nothing else.
                if (position != PlayerController.Instance.Follower.transform.position)
                    return true;

                return GetAvailableMonsterAbilities().Contains(PlayerController.Instance.Follower.Monster.GetName());
            }
        }
    }
}
