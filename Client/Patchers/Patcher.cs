﻿using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Team17.Online;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Patcher : BaseUnityPlugin
    {
        public static ManualLogSource Logger;
        public static ArchipelagoUI UI;

        private void Awake()
        {
            Logger = base.Logger;

            GameData.Load();

            // Plugin startup logic
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());

            LoadItemsReceived();
            LoadLocationsChecked();
        }

        [HarmonyPatch(typeof(MainMenu))]
        [HarmonyPatch("Start")]
        internal class MainMenu_Start
        {
            [HarmonyPostfix]
            public static void CreateArchipelagoUI()
            {
                if (Patcher.UI != null)
                    return;

                var guiObject = new GameObject("Archipelago UI");
                Patcher.UI = guiObject.AddComponent<ArchipelagoUI>();

                var rawPath = Environment.CurrentDirectory;
                var lastConnection = ArchipelagoConnection.LoadFromFile(rawPath + "/archipelago_last_connection.json");
                if (lastConnection != null)
                {
                    APState.ConnectionInfo = lastConnection;
                }
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
                    __result = __result * SlotData.ExpMultiplier;
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
            private static void Prefix(CombatController __instance)
            {
                if(__instance.CurrentEncounter.EncounterType == EEncounterType.InfinityArena 
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
                        __instance.AddRewardItem(items, egg, 1, (int)enemy.Shift);
                }

                var rareField = Traverse.Create(__instance).Field("rareRewards");
                var rareRewards = rareField.GetValue<List<InventoryItem>>();
                rareRewards.AddRange(items);
                rareField.SetValue(rareRewards);
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
                foreach (InventoryItem inventoryItem in items) 
                {
                    // Only ever add one copy of an egg
                    if (inventoryItem.Item == item && item is Egg)
                        return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "AddItem")]
        private class InventoryManager_AddItem 
        {
            [UsedImplicitly]
            private static void Postfix(InventoryManager __instance, BaseItem item) 
            {
                if (!APState.IsConnected)
                    return;

                if (item is not Egg)
                    return;

                var egg = (Egg)item;
                AddAbilityToDataStorage(egg.Monster);
            }
        }

        [HarmonyPatch(typeof(MonsterManager), "AddMonsterByPrefab")]
        private class MonsterManager_AddMonsterByPrefab
        {
            [UsedImplicitly]
            private static void Postfix(GameObject monsterPrefab, bool loadingSaveGame)
            {
                if (!APState.IsConnected)
                    return;

                // If we're loading a save game, don't check any locations
                if (loadingSaveGame)
                    return;

                // If the goal is to complete the monster journal, and we've got 111 monsters, complete the game.
                if (SlotData.Goal == CompletionEvent.Monsterpedia)
                {
                    if (PlayerController.Instance.Monsters.AllMonster.Select(m => m.ID).Distinct().Count() == 111)
                        APState.CompleteGame();
                }

                if (SlotData.Eggsanity)
                {
                    var monster = monsterPrefab.GetComponent<Monster>();
                    var location_name = $"eggsanity_{monster.Name.Replace(" ", "_").Replace("'", "").ToLower()}";

                    APState.CheckLocation(GameData.ItemChecks[location_name]);
                }

                AddMonsterToDataStorage(monsterPrefab);
                    return;

                // If we're loading a save game, don't check any locations
                if (loadingSaveGame)
                    return;

                AddMonsterToDataStorage(monsterPrefab);
                AddAbilityToDataStorage(monsterPrefab);
            }
        }

        private static void AddAbilityToDataStorage(GameObject monsterObj)
        {
            if (!APState.IsConnected)
                return;

            var monster = monsterObj.GetComponent<Monster>();
            if (monster == null)
            {
                Patcher.Logger.LogWarning($"No monster component found for game object '{monsterObj.name}'");
                return;
            }

            var ability = monster.ExploreAction.GetComponent<ExploreAbility>();
            if (ability == null)
            {
                Patcher.Logger.LogError($"{monster.Name} has a null ExploreAbility component");
                return;
            }

            if (APState.ReadBoolFromDataStorage(ability.Name) == false)
            {
                APState.SetToDataStorage(ability.Name, (DataStorageElement)true);
            }
        }

        private static void AddMonsterToDataStorage(GameObject monsterObj)
        {
            if (!APState.IsConnected)
                return;

            var monster = monsterObj.GetComponent<Monster>();
            if (monster == null)
            {
                Patcher.Logger.LogWarning($"No monster component found for game object '{monsterObj.name}'");
                return;
            }

            if (APState.ReadBoolFromDataStorage(monster.Name) == false)
            {
                APState.SetToDataStorage(monster.Name, (DataStorageElement)true);
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