using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private const string LOCATIONS_CHECKED_FILENAME = "archipelago_locations_checked.json";
        private static List<long> _locations_checked = new List<long>();  // Includes champion rank up items
        private static Dictionary<string, int> _check_counter = new Dictionary<string, int>();  // does NOT include champion rank up items

        public static void AddAndUpdateCheckedLocations(long locationId)
        {
            if (_locations_checked.Contains(locationId))
            {
                return;
            }

            _locations_checked.Add(locationId);
            SaveLocationsChecked();

            // If the item we just recieved is a rank up item, we don't increment the counter
            if (GameData.ChampionRankIds.ContainsValue(locationId))
                return;

            IncrementCheckCounter(locationId);

            // Update the minimap with the new map pins
            UIController.Instance.Minimap.UpdateMinimap();
        }

        public static void DeleteLocationsChecked()
        {
            if (File.Exists(LOCATIONS_CHECKED_FILENAME))
                File.Delete(LOCATIONS_CHECKED_FILENAME);
            _locations_checked = new();
            _check_counter = new();
        }

        public static void SaveLocationsChecked()
        {
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_locations_checked));
                File.WriteAllBytes(Path.Combine(rawPath, LOCATIONS_CHECKED_FILENAME), bytes);
            }
        }

        public static void LoadLocationsChecked()
        {
            if (File.Exists(LOCATIONS_CHECKED_FILENAME))
            {
                var reader = File.OpenText(LOCATIONS_CHECKED_FILENAME);
                var content = reader.ReadToEnd();
                reader.Close();
                _locations_checked = JsonConvert.DeserializeObject<List<long>>(content);

                RebuildCheckCounter();                
            }
        }

        public static void RebuildCheckCounter()
        {
            if (!APState.IsConnected)
                return;

            _check_counter = new();
            var locations = _locations_checked.Except(GameData.ChampionRankIds.Values);

            foreach (var location in locations)
                IncrementCheckCounter(location);
        }

        public static void IncrementCheckCounter(long locationId)
        {
            if (!APState.IsConnected)
                return;

            // Add a check to our check counter base don the area
            var locationName = APState.Session.Locations.GetLocationNameFromId(locationId);
            var regionName = locationName.Replace(" ", "").Split('-').First();
            if (!_check_counter.ContainsKey(regionName))
                _check_counter[regionName] = 0;
            _check_counter[regionName] += 1;
        }

        private static string GetCheckString(string region)
        {
            int collected = 0;
            int max = 0;

            if (_check_counter.ContainsKey(region))
                collected = _check_counter[region];

            if (GameData.NumberOfChecks.ContainsKey(region))
                max = GameData.NumberOfChecks[region];

            return string.Format("{0,2} / {1,-2}", collected, max);
        }

        [HarmonyPatch(typeof(MinimapView), "Start")]
        private class MinimapView_Start
        {
            private static void Prefix(MinimapView __instance)
            {
                var keytext = __instance.gameObject.transform.Find("KeyAmount");
                var chesttext = GameObject.Instantiate(keytext);
                chesttext.name = "ChestAmount";
                chesttext.transform.localPosition = new Vector3(-13, 21, -0.5f);
                chesttext.transform.SetParent(__instance.gameObject.transform);
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Show")]
        private class MinimapView_Show
        {
            private static void Postfix(MinimapView __instance)
            {
                var chesttext = __instance.gameObject.transform.Find("ChestAmount");
                chesttext.transform.localPosition = new Vector3(-13, 21, -0.5f);
            }
        }

        [HarmonyPatch(typeof(MinimapView), "Hide")]
        private class MinimapView_Hide
        {
            private static void Postfix(MinimapView __instance)
            {
                var chesttext = __instance.gameObject.transform.Find("ChestAmount");
                chesttext.transform.localPosition = new Vector3(-13, 21, -0.5f);
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

        //[HarmonyPatch(typeof(MinimapView), "UpdateMinimap")]
        //private class MinimapView_UpdateMinimap
        //{
        //    static bool updatingPins = false;

        //    private static bool Prefix()
        //    {
        //        // If we're already updating the pins, do not do any other updates
        //        if (updatingPins)
        //            return false;

        //        updatingPins = true;
        //        Patcher.UpdateMapPins();
        //        updatingPins = false;

        //        return true;
        //    }
        //}

        [HarmonyPatch(typeof(MinimapTileView), "DisplayTile")]
        private class MinimapTileView_DisplayTile
        {
            [UsedImplicitly]
            private static void Prefix(MinimapTileView __instance, MinimapEntry entry, int posX, int posY)
            {
                if (!APState.IsConnected)
                    return;

                string key = $"{entry.MapData.SceneName}_{posX}_{posY}";

                // If there are no checks at this spot, bail
                if (!GameData.MapPins.ContainsKey(key))
                    return;

                var tileIndex = entry.GetIndex(posX, posY);
                if (tileIndex == -1)
                {
                    return;
                }

                var checks = GameData.MapPins[key].Except(_locations_checked).Count();

                // If all map pins for this tile are contained within the _itemcache, then we can delete the marker
                if (checks == 0)
                {
                    entry.DeleteMinimapMarker(tileIndex);
                    __instance.SetMarker(0);
                    return;
                }

                string name = checks == 1
                    ? "1 Check"
                    : $"{checks} Checks";

                entry.SetMinimapMarker(tileIndex, 3, name);
            }
        }

        [HarmonyPatch(typeof(MapMenu), "InitMapTiles")]
        private class MapMenu_InitMapTiles
        {
            private static void Postfix(MapMenu __instance)
            {
                if (!APState.IsConnected)
                    return;

                var tiles = Traverse.Create(UIController.Instance.IngameMenu.Map).Field("tiles").GetValue<List<MinimapTileView>>();

                foreach (var tile in tiles)
                {
                    if (tile.MinimapEntry.MapData.IsIndoor)
                        continue;

                    var index = tile.MinimapEntry.GetTileViewIndex(tile);
                    int posX = index % (int)tile.MinimapEntry.MapData.Size.x;
                    int posY = index / (int)tile.MinimapEntry.MapData.Size.x;

                    string key = $"{tile.MinimapEntry.MapData.SceneName}_{posX}_{posY}";

                    if (!GameData.MapPins.ContainsKey(key))
                        continue;

                    var checks = GameData.MapPins[key].Except(_locations_checked).Count();
                    if (checks == 0)
                    {
                        tile.MinimapEntry.DeleteMinimapMarker(tile.MinimapEntry.GetTileViewIndex(tile));
                        tile.SetMarker(0);
                    }
                    else
                    {
                        string name = checks == 1
                            ? "1 Check"
                            : $"{checks} Checks";

                        tile.MinimapEntry.SetMinimapMarker(tile.MinimapEntry.GetTileViewIndex(tile), 3, name);
                        tile.SetMarker(3);
                    }
                }
            }
        }
    }
}