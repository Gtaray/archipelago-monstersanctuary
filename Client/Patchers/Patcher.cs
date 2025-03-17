using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using Archipelago.MultiClient.Net.Models;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using garfieldbanks.MonsterSanctuary.ModsMenu;
using garfieldbanks.MonsterSanctuary.ModsMenu.Extensions;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Archipelago.MonsterSanctuary.Client
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Patcher : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;
        public static ArchipelagoUI UI;
        public static string ClientVersion = "v#.#.#";

        private static ConfigEntry<bool> ShowNotificationFiller;
        private static ConfigEntry<bool> ShowNotificationUseful;
        private static ConfigEntry<bool> ShowNotificationTrap;
        private static ConfigEntry<bool> ShowNotificationProgression;
        private static ConfigEntry<int> ExpMultiplier;
        private static ConfigEntry<bool> EnableWarpingHome;

        private void Awake()
        {
            Logger = base.Logger;

            ClientVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            Patcher.Logger.LogInfo("AP CLIENT V" + ClientVersion);

            // OPTIONS
            ShowNotificationFiller = Config.Bind("Archipelago", "Notification Filler", false, "Show pop-up notifications for filler items");
            ShowNotificationUseful = Config.Bind("Archipelago", "Notification Useful", false, "Show pop-up notifications for useful items");
            ShowNotificationTrap = Config.Bind("Archipelago", "Notification Trap", true, "Show pop-up notifications for trap items");
            ShowNotificationProgression = Config.Bind("Archipelago", "Notification Progression", true, "Show pop-up notifications for progression items");
            ExpMultiplier = Config.Bind("Archipelago", "Exp Multiplier", 1, "Multiplier for experienced gained");
            EnableWarpingHome = Config.Bind("Archipelago", "Warp to Home", true, "If enabled, Warp to Start menu item returns you to the Keeper's Stronhold instead once it is explored.");

            if (ExpMultiplier.Value < 0)
            {
                ExpMultiplier.Value = 1;
            }

            ModList.RegisterOptionsEvt += (_, _) =>
            {
                ModList.TryAddOption(
                    "AP Client",
                    "Notifications: Filler",
                    () => ShowNotificationFiller.Value ? "Yes" : "No",
                    onValueChangeFunc: _ => ShowNotificationFiller.Value = !ShowNotificationFiller.Value,
                    setDefaultValueFunc: () => ShowNotificationFiller.Value = false);

                ModList.TryAddOption(
                    "AP Client",
                    "Notifications: Useful",
                    () => ShowNotificationUseful.Value ? "Yes" : "No",
                    onValueChangeFunc: _ => ShowNotificationUseful.Value = !ShowNotificationUseful.Value,
                    setDefaultValueFunc: () => ShowNotificationUseful.Value = false);

                ModList.TryAddOption(
                   "AP Client",
                   "Notifications: Traps",
                   () => ShowNotificationTrap.Value ? "Yes" : "No",
                   onValueChangeFunc: _ => ShowNotificationTrap.Value = !ShowNotificationTrap.Value,
                   setDefaultValueFunc: () => ShowNotificationTrap.Value = true);

                ModList.TryAddOption(
                   "AP Client",
                   "Notifications: Progression",
                   () => ShowNotificationProgression.Value ? "Yes" : "No",
                   onValueChangeFunc: _ => ShowNotificationProgression.Value = !ShowNotificationProgression.Value,
                   setDefaultValueFunc: () => ShowNotificationProgression.Value = true);

                ModList.TryAddOption(
                    "AP Client",
                    "Warp to Stronghold",
                    () => EnableWarpingHome.Value ? "Enabled" : "Disabled",
                    onValueChangeFunc: _ => EnableWarpingHome.Value = !EnableWarpingHome.Value,
                    setDefaultValueFunc: () => EnableWarpingHome.Value = true);

                ModList.TryAddOption(
                    "AP Client",
                    "Exp Multiplier",
                    () => $"{ExpMultiplier.Value}x Exp",
                    direction => ExpMultiplier.Value = (ExpMultiplier.Value + direction).Clamp(1, 10),
                    possibleValuesFunc: () => ModList.CreateOptionsIntRange(1, 10),
                    onValueSelectFunc: newValue => ExpMultiplier.Value = int.Parse(newValue.Replace("x Exp", "")),
                    setDefaultValueFunc: () => ExpMultiplier.Value = 1);
            };


            Champions.Load();
            Monsters.Load();
            Items.Load();
            World.LoadStaticData();

            // Plugin startup logic
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());
            SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>(this.OnSceneLoaded);

            ApData.InitializePersistenceFiles();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= new UnityAction<Scene, LoadSceneMode>(this.OnSceneLoaded);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var parts = scene.name.Split('_');

            // Unless the parts are exactly as expected, don't do anything
            if (parts.Length != 2)
                return;

            if (!ApState.IsConnected)
                return;

            Task.Run(() =>
            {
                ApState.SetToDataStorage("CurrentArea", parts[0]);
            });
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
            }
        }

        [HarmonyPatch(typeof(PlayerController), "LoadGame")]
        internal class PlayerController_LoadGame
        {
            private static void Prefix()
            {
                if (!ApState.IsConnected)
                    return;

                RandomzeAllMonsterSkillData();
                AddMissingRewardsToMonsters();
            }
        }

        [HarmonyPatch(typeof(GameController), "InitPlayerStartSetup")]
        internal class GameController_InitPlayerStartSetup
        {
            private static void Prefix()
            {
                if (!ApState.IsConnected)
                    return;

                RandomzeAllMonsterSkillData();
                AddMissingRewardsToMonsters();
            }
        }
    }
}