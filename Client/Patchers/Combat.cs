using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
                List<long> toCheck = new()
                {
                    locationId.Value
                };

                // If defeating champions should unlock the key of power, we check if enough champions have been defeated
                // and if so, add the key of power location to the check
                if (Locations.ChampionsUnlockKeyOfPower())
                {
                    if (ApData.GetNumberOfChampionsDefeated() >= SlotData.ChampionsNeededToGetKeyOfPower)
                    {
                        toCheck.Add(Locations.KeyOfPowerUnlockLocation);
                    }
                }

                ApState.CheckLocations(toCheck.ToArray());
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
                if (__instance.CurrentEncounter.EncounterType == EEncounterType.InfinityArena || __instance.CurrentEncounter.IsChampionChallenge)
                {
                    return;
                }

                var items = new List<InventoryItem>();

                if (SlotData.AlwaysGeCatalyst)
                {
                    foreach (Monster enemy in __instance.Enemies)
                    {
                        Catalyst c = enemy.RewardsRare
                            .Select(i => i.GetComponent<Catalyst>())
                            .FirstOrDefault(i => i is Catalyst);

                        if (c != null)
                        {
                            __instance.AddRewardItem(items, c, 1);
                        }
                    }
                }

                if (!GameModeManager.Instance.BraveryMode && SlotData.AlwaysGetEgg)
                {
                    foreach (Monster enemy in __instance.Enemies)
                    {
                        // Get the rare egg reward for this enemy
                        var egg = enemy.RewardsRare
                            .Select(i => i.GetComponent<BaseItem>())
                            .FirstOrDefault(i => i is Egg);

                        if (egg != null)
                        {
                            __instance.AddRewardItem(items, egg, 1, (int)enemy.Shift);
                        }
                    }
                }

                ___rareRewards.AddRange(items);
            }
        }

        /// <summary>
        /// When giving a reward, this ensures that only one egg or catalyst of a given monster is ever added
        /// </summary>
        [HarmonyPatch(typeof(CombatController), "AddRewardItem")]
        private class CombatController_AddRewardItem
        {
            [UsedImplicitly]
            private static bool Prefix(List<InventoryItem> items, BaseItem item, int quantity, int variation)
            {
                if (item is Catalyst)
                    return !items.Any(i => i.Item == item);

                else if (item is Egg)
                    return !items.Any(i => i.Item == item);

                return true;
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

        #region Traps
        private static IEnumerable<InventoryItem> GetTrapsInInventory(TrapTrigger trigger)
        {
            return PlayerController.Instance.Inventory.Uniques
                .Where(i => i.Item is TrapItem)
                .Where(i => ((TrapItem)i.Item).Trigger == trigger)
                .OrderBy(i => ((TrapItem)i.Item).GetPriority());
        }

        private static void ApplyPoisonTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Poison, null);
            }
        }

        private static void ApplyShockTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Shock, null);
            }
        }

        private static void ApplyBurnTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Burn, null);
            }
        }

        private static void ApplyFreezeTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Chill, null);
            }
        }

        private static void ApplyBlindTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddSpecialBuff(source, BuffManager.ESpecialBuff.Blind);
                monster.BuffManager.AddSpecialBuff(source, BuffManager.ESpecialBuff.Blind);
                monster.BuffManager.AddSpecialBuff(source, BuffManager.ESpecialBuff.Blind);
                monster.BuffManager.AddSpecialBuff(source, BuffManager.ESpecialBuff.Blind);
                monster.BuffManager.AddSpecialBuff(source, BuffManager.ESpecialBuff.Blind);
            }
        }

        private static void ApplyAmbushTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.Enemies)
            {
                // Apply sidekick and a 30% shield to all enemies
                monster.BuffManager.AddBuff(new BuffSourceChain(source), BuffManager.BuffType.Sidekick, true);
                monster.AddShield(source, (int)(monster.MaxHealth * 0.3));
            }
        }

        private static void ApplyDeathTrap()
        {
            var monsters = CombatController.Instance.PlayerMonsters.Where(m => !m.IsDead()).ToList();
            if (!monsters.Any())
                return;

            System.Random r = new System.Random();
            var monster = monsters[r.Next(monsters.Count())];

            monster.ModifyHealth(-99999);
            monster.CheckDeath();
        }

        private static void SendTrapUseNotification(string itemName)
        {
            ItemTransferNotification notification = new()
            {
                ItemName = itemName,
                Action = ItemTransferType.TrapUsed,
            };

            Patcher.UI.AddItemToHistory(notification);
        }

        private static TrapItem _activeTrap;

        [HarmonyPatch(typeof(CombatController), "StartPlayerTurn")]
        private class CombatController_StartPlayerTurn
        {
            private static void Postfix()
            {
                var traps = GetTrapsInInventory(TrapTrigger.PlayerTurnStart);
                if (traps.Count() == 0)
                    return;

                _activeTrap = traps.First().Item as TrapItem;

                if (_activeTrap.Name == "Shock Trap")
                    ApplyShockTrap();
                else if (_activeTrap.Name == "Freeze Trap")
                    ApplyFreezeTrap();
                else if (_activeTrap.Name == "Burn Trap")
                    ApplyBurnTrap();
                else if (_activeTrap.Name == "Poison Trap")
                    ApplyPoisonTrap();
                else if (_activeTrap.Name == "Flash-Bang Trap")
                    ApplyBlindTrap();
                else if (_activeTrap.Name == "Death Trap")
                    ApplyDeathTrap();

                SendTrapUseNotification(_activeTrap.Name);

                PlayerController.Instance.Inventory.RemoveItem(_activeTrap);
                _activeTrap = null;
            }
        }

        [HarmonyPatch(typeof(CombatController), "StartEnemyTurn")]
        private class CombatController_StartEnemyTurn
        {
            private static void Postfix()
            {
                var traps = GetTrapsInInventory(TrapTrigger.EnemyTurnStart);
                if (traps.Count() == 0)
                    return;

                // There are currently no traps that use the enemy turn trigger
            }
        }

        [HarmonyPatch(typeof(CombatController), "StartFirstCombatTurn")]
        private class CombatController_StartFirstCombatTurn
        {
            private static void Postfix()
            {
                var traps = GetTrapsInInventory(TrapTrigger.CombatStart);
                if (traps.Count() == 0)
                    return;

                _activeTrap = traps.First().Item as TrapItem;

                if (_activeTrap.Name == "Ambush Trap")
                    ApplyAmbushTrap();

                PlayerController.Instance.Inventory.RemoveItem(_activeTrap);
                _activeTrap = null;
            }
        }

        [HarmonyPatch(typeof(SkillManager), "CanApplyDebuffOrNegativeStack")]
        private class SkillManager_CanApplyDebuffOrNegativeStack
        {
            private static bool Prefix(ref bool __result)
            {
                if (_activeTrap != null)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SkillManager), "OnApplyDebuffToEnemy")]
        private class SkillManager_OnApplyDebuffToEnemy
        {
            private static bool Prefix(BuffManager __instance, BaseAction action)
            {
                if (action == null && _activeTrap != null)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(BuffManager), "HasDebuffMasterySlot")]
        private class BuffManager_HasDebuffMasterySlot
        {
            private static bool Prefix(BuffManager __instance, ref bool __result, Type applyingSkill)
            {
                // Traps should always apply the debuff as if it's mastery
                if (_activeTrap != null)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
        #endregion
    }
}
