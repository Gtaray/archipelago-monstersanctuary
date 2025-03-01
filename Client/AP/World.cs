using Archipelago.MonsterSanctuary.Client.Persistence;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public class World
    {
        /// <summary>
        /// A list of story flags that are skipped when Plotless is enabled
        /// Story flags are skipped by flipping their value to true prior to that flag being read
        /// </summary>
        public static List<string> Plotless = new();

        public static bool ShouldSkipStoryFlagForPlotless(string flag) => Plotless.Contains(flag);

        /// <summary>
        /// A list of all locked doors that are considered 'minimal', meaning they will not be removed when the locked doors setting is set to minimal
        /// The values in this list are in the format {scene_name}_{actor_id}
        /// </summary>
        public static List<string> LockedDoors = new();

        /// <summary>
        /// All maps pins that are used to render remaining checks in the mini-map
        /// Key: A string in the format of {scene_name}_{x}_{y}, where x and y are the coordinates of the pin, starting at the lower-left-most cell of the room and increasing upwards and to the right
        /// Value: A list of all AP location names at this particular map cell
        /// </summary>
        public static Dictionary<string, List<long>> MapPins = new();

        public static bool IsLockedDoorMinimal(string doorId)
        {
            return LockedDoors.Contains(doorId);
        }

        /// <summary>
        /// Checks if a given minimap cell within a scene
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool AreThereMapPinsForMinimapCell(string scene, int x, int y)
        {
            string key = $"{scene}_{x}_{y}";
            return MapPins.ContainsKey(key);
        }

        /// <summary>
        /// Gets all map pins for a given cell within a scene
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static List<long> GetMapPinsForMinimapCell(string scene, int x, int y)
        {
            string key = $"{scene}_{x}_{y}";
            if (!AreThereMapPinsForMinimapCell(scene, x, y))
                return new();

            return MapPins[key];
        }

        /// <summary>
        /// Loads all relevant world data from json files
        /// </summary>
        public static void LoadStaticData()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Loads script nodes that are skipped with plotless
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.plotless_flags.json"))
            using (StreamReader reader = new(stream))
            {
                string json = reader.ReadToEnd();
                Plotless = JsonConvert.DeserializeObject<List<string>>(json);
            }

            // Loads script nodes that are skipped with plotless
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.minimal_locked_doors.json"))
            using (StreamReader reader = new(stream))
            {
                string json = reader.ReadToEnd();
                LockedDoors = JsonConvert.DeserializeObject<List<string>>(json);
            }
        }

        /// <summary>
        /// Loads all map pin data. 
        /// Client must be connected to Archipelago for this to work, since it looks up location IDs
        /// </summary>
        public static void LoadMapPins()
        {
            // We can only load this data if we're already connected to AP, since we need location IDs
            if (!ApState.IsConnected)
                return;

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.map_pins.json");
            using StreamReader reader = new(stream);
            Patcher.Logger.LogInfo("Loading minimap pins:");
            string json = reader.ReadToEnd();
            var pins = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

            foreach (var pin in pins)
            {
                MapPins[pin.Key] = new();
                foreach (var location in pin.Value)
                {
                    var id = Locations.GetLocationId(location);
                    if (!id.HasValue)
                    {
                        Patcher.Logger.LogWarning($"\t{location} does not have an id.");
                        continue;
                    }

                    MapPins[pin.Key].Add(id.Value);

                }
            }
            Patcher.Logger.LogInfo($"Loaded {MapPins.Count()} map pins");
        }
    }
}
