using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        #region Locking Abilities
        /// <summary>
        ///  Returns a dictionary of all ExploreAbilityItems in the players inventory
        ///  Key = item name, Value = item quantity
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, int> GetOwnedExploreAbilityItems()
        {
            return PlayerController.Instance.Inventory.Uniques
                .Where(u => u.Item is ExploreAbilityItem || u.Item.Name == "Ahrimaaya")
                .ToDictionary(i => i.Item.Name, i => i.Quantity);
        }

        private static bool IsMonsterAbilityAvailable(string monsterName)
        {
            var inventory = GetOwnedExploreAbilityItems();
            var items = Monsters.GetItemsRequiredToUseMonstersAbility(monsterName);

            // For each item that's required, test that the player has the item in their inventory in the correct quantity
            // If either test fails, return false.
            // If no tests fail, return true
            foreach (var item in items)
            {
                if (!inventory.ContainsKey(item.ItemName))
                    return false;
                if (inventory[item.ItemName] < item.Count)
                    return false;
            }

            return true;
        }

        private static int GetExploreItemInventoryCount(string itemName)
        {
            var items = GetOwnedExploreAbilityItems();
            if (!items.ContainsKey(itemName))
                return 0;
            return items[itemName];
        }

        [HarmonyPatch(typeof(PlayerFollower), "CanUseAction")]
        private static class PlayerFollower_CanUseAction
        {
            private static void Postfix(PlayerFollower __instance, ref bool __result)
            {
                if (!ApState.IsConnected)
                    return;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return;

                // If the player would otherwise be able to use this action, we add one final check
                // if they already couldn't use the action, don't suddenly let them.
                if (__result)
                    __result = IsMonsterAbilityAvailable(__instance.Monster.OriginalMonsterName);
            }
        }

        [HarmonyPatch(typeof(InvisiblePlatform), "Update")]
        private static class InvisiblePlatform_Update
        {
            private static bool Prefix(InvisiblePlatform __instance)
            {
                if (!ApState.IsConnected)
                    return true;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return true;

                var ability = PlayerController.Instance.Follower.Monster.ExploreAction.GetComponent<ExploreAbility>();
                if (ability is not SecretVisionAbility)
                    return true;

                // Rather than trying to control the state of the invisible platform, we're simply going to suppress
                // the platforms updates. This effectively means it'll never turn on, and avoids having to make a reflection
                // call in an update loop
                return IsMonsterAbilityAvailable(PlayerController.Instance.Follower.Monster.OriginalMonsterName);
            }
        }

        [HarmonyPatch(typeof(DarkRoomLightManager), "LightenArea")]
        private static class DarkRoomLightManager_LightenArea
        {
            private static bool Prefix(DarkRoomLightManager __instance, Vector3 position)
            {
                if (!ApState.IsConnected)
                    return true;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return true;

                //  Only disable the follower's light source, nothing else.
                if (position != PlayerController.Instance.Follower.transform.position)
                    return true;

                return IsMonsterAbilityAvailable(PlayerController.Instance.Follower.Monster.OriginalMonsterName);
            }
        }
        #endregion

        #region Monster Selector Menu
        [HarmonyPatch(typeof(MonsterSelector), "UpdateDisabledStatus")]
        private class MonsterSelector_UpdateDisabledStatus
        {
            private static void Postfix(MonsterSelector __instance, MonsterSelectorView monsterView)
            {
                if (!ApState.IsConnected)
                    return;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return;

                if (__instance.CurrentSelectType != MonsterSelector.MonsterSelectType.SelectFollower)
                    return;

                bool avail = IsMonsterAbilityAvailable(monsterView.Monster.OriginalMonsterName);
                monsterView.SetDisabled(!avail);
            }
        }

        [HarmonyPatch(typeof(MonsterSelector), "OnItemSelected")]
        private class MonsterSelector_ShowMonsters
        {
            private static bool Prefix(MonsterSelector __instance, MenuListItem item)
            {
                if (!ApState.IsConnected)
                    return true;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return true;

                if (__instance.CurrentSelectType != MonsterSelector.MonsterSelectType.SelectFollower)
                    return true;

                var msv = item.GetComponent<MonsterSelectorView>();
                if (!msv.IsDisabled)
                    return true;

                var itemText = GetExploreItemDisplayTextForMonster(msv.Monster.OriginalMonsterName);

                SFXController.Instance.PlaySFX(SFXController.Instance.SFXMenuCancel);
                __instance.MenuList.SetLocked(true);
                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Can't use ability"),
                    Utils.LOCA($"You cannot have this monster follow you until you acquire {itemText}"),
                    new PopupController.PopupDelegate(() => __instance.MenuList.SetLocked(false)));

                return false;
            }
        }
        [HarmonyPatch(typeof(FollowerTooltip), "Open")]
        private class FollowerTooltip_Open
        {
            private static void Postfix(FollowerTooltip __instance, Monster monster)
            {
                if (!ApState.IsConnected)
                    return;

                if (SlotData.LockedExploreAbilities == LockedExploreAbilities.Off)
                    return;

                string reqText = GetExploreItemDisplayTextForMonster(monster.OriginalMonsterName);

                __instance.Description.text = $"- Requires: {reqText}\n{__instance.Description.text}";
            }
        }

        private static string GetExploreItemDisplayTextForMonster(string monsterName)
        {
            List<string> itemNames = new();
            var items = Monsters.GetItemsRequiredToUseMonstersAbility(monsterName);
            foreach (var item in items)
            {
                int has = GetExploreItemInventoryCount(item.ItemName);
                int needed = item.Count;
                ItemClassification color = has >= needed
                    ? ItemClassification.Useful
                    : ItemClassification.Trap;

                itemNames.Add(FormatItem($"{has}/{needed} {item.ItemName}", color));
            }

            if (itemNames.Count() == 0)
            {
                Patcher.Logger.LogError($"could not find the item that unlocks {monsterName}'s explore ability");
                return "";
            }

            if (itemNames.Count() == 1)
                return itemNames.First();
            return string.Join(", ", itemNames.ToArray(), 0, itemNames.Count() - 1) + ", and " + itemNames.LastOrDefault();
        }
        #endregion
    }
}
