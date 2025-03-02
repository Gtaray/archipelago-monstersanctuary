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

        private static ConfigEntry<bool> ShowPopupNotifications;
        private static ConfigEntry<int> ExpMultiplier;

        private void Awake()
        {
            Logger = base.Logger;

            // OPTIONS
            ShowPopupNotifications = Config.Bind("Archipelago", "Show popup notifications", true, "Show popup notifications for sending and receiving items");
            ExpMultiplier = Config.Bind("Archipelago", "Exp Multiplier", 1, "Multiplier for experienced gained");

            if (ExpMultiplier.Value < 0)
            {
                ExpMultiplier.Value = 1;
            }

            ModList.RegisterOptionsEvt += (_, _) =>
            {
                ModList.TryAddOption(
                    "AP Client",
                    "Show Popup Notifications",
                    () => ShowPopupNotifications.Value ? "Enabled" : "Disabled",
                    _ => ShowPopupNotifications.Value = !ShowPopupNotifications.Value,
                    setDefaultValueFunc: () => ShowPopupNotifications.Value = true);

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
    }
}