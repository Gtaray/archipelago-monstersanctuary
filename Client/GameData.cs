using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public class Hint
    {
        public string Text { get; set; }
        public bool IgnoreRemainingText { get; set; }
    }

    public class Shop
    {
        public ConcurrentDictionary<string, ShopInventoryItem> Inventory { get; set; } = new();

        public void AddItem(string key, ShopInventoryItem item)
        {
            Inventory[key] = item;
        }

        public bool HasItem(string key)
        {
            return Inventory.ContainsKey(key);
        }

        public ShopInventoryItem GetItem(string key)
        {
            return Inventory[key];
        }
    }

    public class ShopInventoryItem
    {
        public bool IsLocal { get; set; }
        public string Player { get; set; }
        public string Name { get; set; }
        public long LocationId { get; set; }
        public ItemClassification Classification { get; set; }
        public int? Price { get; set; }
    }

    public enum ItemClassification
    {
        Filler = 0,
        Progression = 1,
        Useful = 2,
        Trap = 4 
    }

    public class GameData
    {
        #region Monster Location Data
        // Pre-loaded collection of monsters, so that we don't have to worry about async
        // stuff breaking AI/spawning rules during gameplay
        public static Dictionary<string, Tuple<GameObject, Monster>> MonstersCache = new();

        // Maps scenes to the champions that are put in those scenes. Easy way to handle replacing htem.
        public static Dictionary<string, string> ChampionScenes = new();

        // Location ids for the champion rank items
        public static Dictionary<string, long> ChampionRankIds = new();

        // Original champions and their locations
        public static Dictionary<string, string> OriginalChampions = new();

        /// <summary>
        /// Adds a monster to the monster cache
        /// </summary>
        /// <param name="locationId"></param>
        /// <param name="monsterName"></param>
        public static void AddMonster(string locationId, string monsterName)
        {
            var gameObject = GetMonsterByName(monsterName);
            if (gameObject == null)
            {
                Patcher.Logger.LogError("Failed to add monster. Monster was not found. Monster=" + monsterName);
                return;
            }
            var monster = gameObject.GetComponent<Monster>();
            if (MonstersCache.ContainsKey(locationId))
            {
                Patcher.Logger.LogWarning("Duplicate location found");
                return;
            }

            MonstersCache.Add(locationId, new Tuple<GameObject, Monster>(gameObject, monster));
        }

        /// <summary>
        /// Gets a monster game object by the monster's name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject GetMonsterByName(string name)
        {
            if (MonsterNames.ContainsKey(name))
            {
                name = MonsterNames[name];
            }

            return GameController.Instance.WorldData.Referenceables
                    .Where(x => x?.gameObject.GetComponent<Monster>() != null)
                    .Select(x => x.gameObject)
                    .SingleOrDefault(mon => string.Equals(mon.name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the monster that is stored in the cache for a given location.
        /// If cache does not contain the location id, returns null
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static GameObject GetReplacementMonster(string locationId)
        {
            if (MonstersCache.ContainsKey(locationId))
            {
                return MonstersCache[locationId].Item1;
            }

            Patcher.Logger.LogWarning($"Location '{locationId}' is not in the monster cache");
            return null;
        }
        #endregion

        #region Item Location Data
        // Maps the logical <scene>_<object_id> format that the client generates to the location ids that AP cares about
        public static Dictionary<string, long> ItemChecks = new();

        // The number of item checks in each area
        public static Dictionary<string, int> NumberOfChecks = new();

        public static void AddItemCheck(string logicalName, long locationId, string area)
        {
            ItemChecks[logicalName] = locationId;
            if (!NumberOfChecks.ContainsKey(area))
                NumberOfChecks[area] = 0;
            NumberOfChecks[area] += 1;
        }
        #endregion

        #region Shop Location Data
        public static bool Shopsanity => ShopChecks.Count > 0;
        public static Dictionary<string, long> ShopChecks = new();

        // Shopsanity entries
        public static ConcurrentDictionary<string, Shop> Shops = new();
        #endregion

        #region Hints
        public static Dictionary<int, Hint> Hints = new();
        public static void AddHint(int id, string text, bool ignoreRemainingText)
        {
            Hints[id] = new Hint() { Text = text, IgnoreRemainingText = ignoreRemainingText };
        }

        public static string GetHint(int id)
        {
            if (!Hints.ContainsKey(id))
                return null;
            return Hints[id].Text;
        }

        public static bool EndHintDialog(int id)
        {
            if (!Hints.ContainsKey(id))
                return false;
            return Hints[id].IgnoreRemainingText;
        }
        #endregion

        #region Json Files
        // This dictionary is required to map game room names to AP location ids
        // because champion monsters have a visual element that isn't attached to an encounter id
        public static Dictionary<string, string> NPCs = new Dictionary<string, string>();

        // Maps monster names from AP to Monster Sanctuary.
        // Only needed for monsters whose names have spaces or special characters
        public static Dictionary<string, string> MonsterNames = new Dictionary<string, string>();

        // Script Nodes that are skipped with plot less
        public static List<string> Plotless = new();

        // Locked Doors
        public static List<string> LockedDoors = new();

        // Map Pin Locations
        public static Dictionary<string, List<long>> MapPins = new();

        public static void Load()
        {
            // Load the subsections data into the dictionary
            var assembly = Assembly.GetExecutingAssembly();

            // Load monster data into the dictionary. This maps the human-readable names that AP uses to the form that Monster Sanctuary uses
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.monster_names.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                MonsterNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                Patcher.Logger.LogInfo($"Loaded {MonsterNames.Count()} monster names");
            }

            // Load champion data into the dictionary
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.npcs.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                NPCs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                Patcher.Logger.LogInfo($"Loaded {NPCs.Count()} npcs");
            }

            // Loads script nodes that are skipped with plotless
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.plotless_flags.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                Plotless = JsonConvert.DeserializeObject<List<string>>(json);
            }

            // Loads script nodes that are skipped with plotless
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.minimal_locked_doors.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                LockedDoors = JsonConvert.DeserializeObject<List<string>>(json);
            }

            // Loads champion locations into a dictionary. Used to track champions and their default location ids
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.original_champions.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                OriginalChampions = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }
        #endregion

        #region Item Accessors
        public static BaseItem GetItemByName(string name)
        {
            if (name.EndsWith(" Egg"))
                return GetItemByName<Egg>(name);

            return GetItemByName<BaseItem>(name);
        }

        public static BaseItem GetItemByName<T>(string name) where T : BaseItem
        {
            return GameController.Instance.WorldData.Referenceables
                .Where(x => x?.gameObject.GetComponent<T>() != null)
                .Select(x => x.gameObject.GetComponent<T>())
                .SingleOrDefault(i => string.Equals(i.GetName(), name, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region Minimap
        public static void LoadMinimap()
        {
            if (!APState.IsConnected)
                return;

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.map_pins.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                Patcher.Logger.LogInfo("Loading minimap pins:");
                string json = reader.ReadToEnd();
                var pins = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                foreach (var pin in pins)
                {
                    MapPins[pin.Key] = new();
                    foreach (var location in pin.Value)
                    {
                        if (!ItemChecks.ContainsKey(location))
                        {
                            Patcher.Logger.LogWarning($"\t{location} does not have an id.");
                            continue;
                        }

                        MapPins[pin.Key].Add(ItemChecks[location]);

                    }
                }
                Patcher.Logger.LogInfo($"Loaded {MapPins.Count()} map pins");
            }
        }
        #endregion
    }
}
