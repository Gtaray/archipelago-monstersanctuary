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
        private static string GetCheckString(string region)
        {
            int collected = 0;
            int max = 0;

            if (Persistence.Instance.CheckCounter.ContainsKey(region))
                collected = Persistence.Instance.CheckCounter[region];

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

                var checks = GameData.MapPins[key].Except(Persistence.Instance.LocationsChecked).Count();

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

                    var checks = GameData.MapPins[key].Except(Persistence.Instance.LocationsChecked).Count();
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

        [HarmonyPatch(typeof(MapMenu), "CheckAllAreas")]
        private class MapMenu_CheckAllAreas
        {
            private static void Postfix(MapMenu __instance)
            {
                if (!APState.IsConnected)
                    return;

                __instance.AreaPercentText.maxChars = 150;
                ShowGoalProgress(__instance);
            }
        }

        [HarmonyPatch(typeof(MapMenu), "CheckCurrentArea")]
        private class MapMenu_CheckCurrentArea
        {
            private static void Postfix(MapMenu __instance)
            {
                if (!APState.IsConnected)
                    return;

                __instance.AreaPercentText.maxChars = 150;
                ShowGoalProgress(__instance);
            }
        }

        private static void ShowGoalProgress(MapMenu __instance)
        {
            string goalText = "";
            if (SlotData.Goal == CompletionEvent.MadLord)
                goalText = "Defeat the Mad Lord";
            else if (SlotData.Goal == CompletionEvent.Champions)
                goalText = $"Defeat All Champions - {Persistence.Instance.ChampionsDefeated.Count()} / 27";

            if (APState.Completed)
            {
                // Formats the text to be green
                goalText = FormatItem(goalText, ItemClassification.Useful);
            }

            __instance.AreaPercentText.text = string.Format(
                "{0}\nGoal: {1}",
                __instance.AreaPercentText.text,
                goalText
                );
        }
    }
}