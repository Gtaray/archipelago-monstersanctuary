using Archipelago.MultiClient.Net.Helpers;
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
using Team17.Online;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Patcher : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            GameData.Load();

            // Plugin startup logic
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());

            LoadItemsReceived();
            LoadChecksRemaining();
        }

        [HarmonyPatch(typeof(MainMenu))]
        [HarmonyPatch("Start")]
        internal class MainMenu_Start
        {
            [HarmonyPostfix]
            public static void CreateArchipelagoUI()
            {
                var guiObject = new GameObject();
                APState.UI = guiObject.AddComponent<ArchipelagoUI>();
                GameObject.DontDestroyOnLoad(guiObject);

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
    }
}