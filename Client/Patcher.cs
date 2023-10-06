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
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Patcher : BaseUnityPlugin
    {
        public static List<string> MonsterLocations = new List<string>();

        private static ManualLogSource _log;
        private static Dictionary<string, string> _subsections = new Dictionary<string, string>();

        private static string GetMappedLocation(string location)
        {
            if (_subsections.ContainsKey(location))
                return _subsections[location];
            return location;
        }

        private static List<string> GetMappedLocations(List<string> locations)
        {
            return locations.Select(l => GetMappedLocation(l)).ToList();
        }

        private void Awake()
        {
            _log = Logger;

            // Plugin startup logic
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());

            // Load the subsections data into the dictionary
            var assembly = Assembly.GetExecutingAssembly();
            var subsections = "Archipelago.MonsterSanctuary.Client.data.subsections.json";

            using (Stream stream = assembly.GetManifestResourceStream(subsections))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                _subsections = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                _log.LogInfo($"Loaded {_subsections.Count()} subsections");
            }

            // Load champion data into the dictionary
            var champions = "Archipelago.MonsterSanctuary.Client.data.npcs.json";

            using (Stream stream = assembly.GetManifestResourceStream(champions))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                _champions = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                _log.LogInfo($"Loaded {_champions.Count()} npcs");
            }

            // Load monster data into the dictionary. This maps the human-readable names that AP uses to the form that Monster Sanctuary uses
            var monsters = "Archipelago.MonsterSanctuary.Client.data.monsters.json";

            using (Stream stream = assembly.GetManifestResourceStream(monsters))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                _monsters = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                _log.LogInfo($"Loaded {_monsters.Count()} monster names");
            }

            // Load monster data into the dictionary. This maps the human-readable names that AP uses to the form that Monster Sanctuary uses
            using (Stream stream = assembly.GetManifestResourceStream("Archipelago.MonsterSanctuary.Client.data.monster_locations.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                MonsterLocations = JsonConvert.DeserializeObject<List<string>>(json);
                _log.LogInfo($"Loaded {MonsterLocations.Count()} monster locations");
            }
        }

        [HarmonyPatch(typeof(MainMenu))]
        [HarmonyPatch("Start")]
        internal class MainMenu_Start
        {
            [HarmonyPostfix]
            public static void CreateArchipelagoUI()
            {
                _log.LogInfo("Creating Archipelago UI Component");
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
                __result = __result * SlotData.ExpMultiplier;
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

        [HarmonyPatch(typeof(ProgressManager), "GetBool")]
        private class ProgressManager_GetBool
        {
            [UsedImplicitly]
            private static void Postfix(ref bool __result, ProgressManager __instance, string name)
            {
                if (name == "SanctuaryShifted")
                {
                    if (SlotData.MonsterShiftRule == ShiftFlag.Any)
                        __result = true;
                    else if (SlotData.MonsterShiftRule == ShiftFlag.Never)
                        __result = false;
                }
            }
        }
    }
}