using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using HarmonyLib;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(CombatController), "LoseCombat")]
        public static class CombatController_LoseCombat
        {
            private static void Postfix()
            {
                if (ApState.IsConnected)
                    ApState.SendDeathLink();
            }
        }

        [HarmonyPatch(typeof(CombatController), "WinCombat")]
        private static class CombatController_WinCombat
        {
            [UsedImplicitly]
            private static void Prefix(CombatController __instance)
            {
                if (!ApState.IsConnected)
                    return;

                if (__instance.CurrentEncounter.IsKeeperBattle)
                    return;

                if (!__instance.CurrentEncounter.IsChampion)
                    return;

                // if victory condition is to beat the mad lord, check to see if we've done that
                if (SlotData.Goal == CompletionEvent.MadLord && GameController.Instance.CurrentSceneName == "AbandonedTower_Final")
                {
                    ApState.CompleteGame();
                }

                // We only want to operate on champion encounters
                string locName = $"{GameController.Instance.CurrentSceneName}_Champion";
                var locationId = Champions.GetChampionRankLocationId(locName);
                if (locationId == null)
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return;
                }

                ApData.AddChampionAsDefeated(GameController.Instance.CurrentSceneName);
                ApState.CheckLocation(locationId.Value);
            }
        }

        [HarmonyPatch(typeof(Monster), "GetExpReward")]
        private class Monster_GetExpReward
        {
            [UsedImplicitly]
            private static void Postfix(ref int __result)
            {
                // Only multiply reward if we're in combat
                // This should prevent the game from using this multiplier when scaling enemies.
                if (GameStateManager.Instance.IsCombat())
                {
                    __result = __result * ExpMultiplier.Value;
                }
            }
        }

        /// <summary>
        /// After an encounter, this will add monster eggs to the rare rewards list
        /// which are then given to the player. Controlled by SlotData.AlwaysGetEgg
        /// </summary>
        [HarmonyPatch(typeof(CombatController), "GrantReward")]
        private class CombatController_GrantRewards
        {
            [UsedImplicitly]
            private static void Prefix(CombatController __instance, List<InventoryItem> ___rareRewards)
            {
                if (__instance.CurrentEncounter.EncounterType == EEncounterType.InfinityArena
                    || GameModeManager.Instance.BraveryMode
                    || __instance.CurrentEncounter.IsChampionChallenge
                    || !SlotData.AlwaysGetEgg)
                {
                    return;
                }

                var items = new List<InventoryItem>();
                foreach (Monster enemy in __instance.Enemies)
                {
                    // Get the rare egg reward for this enemy
                    var egg = enemy.RewardsRare
                        .Select(i => i.GetComponent<BaseItem>())
                        .FirstOrDefault(i => i is Egg);

                    if (egg != null)
                    {
                        Patcher.Logger.LogInfo($"Adding Egg: {egg.Name} ({(int)enemy.Shift})");
                        __instance.AddRewardItem(items, egg, 1, (int)enemy.Shift);
                    }
                }

                ___rareRewards.AddRange(items);
            }
        }

        /// <summary>
        /// When giving a reward, this ensures that only one egg of a given monster is ever added
        /// </summary>
        [HarmonyPatch(typeof(CombatController), "AddRewardItem")]
        private class CombatController_AddRewardItem
        {
            [UsedImplicitly]
            private static bool Prefix(List<InventoryItem> items, BaseItem item, int quantity, int variation)
            {
                if (item is not Egg)
                    return true;

                return !items.Any(i => i.Item == item);
            }
        }

        [HarmonyPatch(typeof(PopupController), "ShowRewards")]
        private class PopupController_ShowRewards
        {
            private static void Prefix(
                ref List<InventoryItem> commonItems,
                ref List<InventoryItem> rareItems,
                ref int gold)
            {
                var toRemove = ((gold > 0 ? 1 : 0) + (commonItems == null ? 0 : commonItems.Count) + (rareItems == null ? 0 : rareItems.Count)) - 8;

                // In this case, we don't need to remove anything
                if (toRemove <= 0)
                    return;

                // The rewards screen is limited to 7 items + gold and a continue button
                // so we need to truncate items displayed until we're down to that amount
                while (toRemove > 0)
                {
                    if (commonItems.Count > 0)
                    {
                        // Start by removing common items
                        Patcher.Logger.LogWarning("Too many items in reward screen. Truncating " + commonItems.First().GetName());
                        commonItems.RemoveAt(0);
                    }
                    else if (rareItems.Count > 0)
                    {
                        // if there aren't any common items to remove, remove rare items
                        Patcher.Logger.LogWarning("Too many items in reward screen. Truncating " + commonItems.First().GetName());
                        rareItems.RemoveAt(0);
                    }
                    else
                    {
                        // this should never happen, but just in case, we clear out gold
                        Patcher.Logger.LogWarning("Too many items in reward screen. Truncating gold");
                        gold = 0;
                    }
                    toRemove--;
                }
            }
        }
    }
}
