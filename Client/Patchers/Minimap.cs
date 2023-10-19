using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private const string CHECKS_REMAINING_FILENAME = "archipelago_checks.json";
        private static Dictionary<string, int> _checks_remaining = new Dictionary<string, int>();

        public static void AddAndUpdateChecksRemaining(string locationName)
        {
            string region = locationName.Split('_').First();
            if (!_checks_remaining.ContainsKey(region))
                _checks_remaining[region] = 0;
            _checks_remaining[region] += 1;

            SaveChecksRemaining();
        }

        public static void SaveChecksRemaining()
        {
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_checks_remaining));
                File.WriteAllBytes(Path.Combine(rawPath, CHECKS_REMAINING_FILENAME), bytes);
            }
        }

        public static void LoadChecksRemaining()
        {
            if (File.Exists(CHECKS_REMAINING_FILENAME))
            {
                var reader = File.OpenText(CHECKS_REMAINING_FILENAME);
                var content = reader.ReadToEnd();
                reader.Close();
                _checks_remaining = JsonConvert.DeserializeObject<Dictionary<string, int>>(content);
            }
        }

        private static string GetCheckString(string region)
        {
            int collected = 0;
            int max = 0;
            if (_checks_remaining.ContainsKey(region))
                collected = _checks_remaining[region];

            if (GameData.NumberOfChecks.ContainsKey(region))
                max = GameData.NumberOfChecks[region];

            return string.Format("{0,2}/{1,-2}", collected, max);
        }

        [HarmonyPatch(typeof(GameController), "LoadStartingArea")]
        private class GameController_ClearChecksRemainingOnNewGame
        {
            [UsedImplicitly]
            private static void Prefix()
            {
                // Clear the item persistence cache when a new file is created
                Logger.LogWarning("New Save. Deleting checks remaining");
                _checks_remaining.Clear();
                SaveChecksRemaining();
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Start")]
        private class MinimapView_Start
        {
            private static void Prefix(MinimapView __instance)
            {
                var keytext = __instance.gameObject.transform.Find("KeyAmount");
                var chesttext = GameObject.Instantiate(keytext);
                chesttext.name = "ChestAmount";
                chesttext.transform.localPosition = new Vector3(-13, 21, 0);
                chesttext.transform.SetParent(__instance.gameObject.transform);
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Show")]
        private class MinimapView_Show
        {
            private static void Postfix(MinimapView __instance)
            {
                var chesttext = __instance.gameObject.transform.Find("ChestAmount");
                chesttext.transform.localPosition = new Vector3(-13, 21, 0);
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Hide")]
        private class MinimapView_Hide
        {
            private static void Postfix(MinimapView __instance)
            {
                var chesttext = __instance.gameObject.transform.Find("ChestAmount");
                chesttext.transform.localPosition = new Vector3(-13, 21, 0);
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Update")]
        private class MinimapView_Update
        {
            static tk2dTextMesh mesh;

            private static void Postfix(MinimapView __instance)
            {
                if (!APState.IsConnected)
                    return;

                if (GameController.Instance == null)
                    return;

                if (PlayerController.Instance.Minimap.CurrentEntry == null)
                    return;

                var scene = GameController.Instance.CurrentSceneName;
                var parts = scene.Split('_');

                if (parts.Length <= 1)
                    return;

                if (mesh == null)
                {
                    var chesttext = __instance.gameObject.transform.Find("ChestAmount");
                    mesh = chesttext.gameObject.GetComponent<tk2dTextMesh>();
                }
                
                if (mesh != null)
                {
                    var text = GetCheckString(parts[0]);
                    mesh.text = text;
                }
            }
        }

        [HarmonyPatch(typeof(MapMenu), "Awake")]
        private class MapMenu_Awake
        {
            [UsedImplicitly]
            private static void Postfix(MapMenu __instance)
            {
                //var go = new GameObject("Locations");
                //go.transform.SetParent(__instance.gameObject.transform);

                //var ap = __instance.gameObject.transform.Find("AreaPercent");
                //var aptext = ap.transform.Find("Text");
                //var apbg = ap.transform.Find("Background");

                //var mountainpath_bg = GameObject.Instantiate(apbg);
            }
        }
    }
}
