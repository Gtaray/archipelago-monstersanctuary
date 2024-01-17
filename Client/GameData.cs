using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public class GameData
    {
        // List of monster locations, so we know which locations to cache when we connect
        public static List<string> MonsterLocations = new List<string>();

        // Pre-loaded collection of monsters, so that we don't have to worry about async
        // stuff breaking AI/spawning rules during gameplay
        public static Dictionary<string, Tuple<GameObject, Monster>> MonstersCache = new Dictionary<string, Tuple<GameObject, Monster>>();

        // Maps the location that this mod generates to a subsection of a region within AP
        // This way the mod doesn't have to know about the logical sub-regions within AP
        public static Dictionary<string, string> Subsections = new Dictionary<string, string>();

        // This dictionary is required to map game room names to AP location ids
        // because champion monsters have a visual element that isn't attached to an encounter id
        public static Dictionary<string, string> NPCs = new Dictionary<string, string>();

        // Maps monster names from AP to Monster Sanctuary.
        // Only needed for monsters whose names have spaces or special characters
        public static Dictionary<string, string> Monsters = new Dictionary<string, string>();

        // Maps a champion name to their default location.
        // This is used to track which champions have been defeated, since the default method doesn't work
        // with randomized monsters
        public static Dictionary<string, string> ChampionLocations = new Dictionary<string, string>();

        // The number of chest/gift locations in each area
        public static Dictionary<string, int> NumberOfChecks = new Dictionary<string, int>();

        // Script Nodes that are skipped with plot less
        public static List<string> Plotless = new();

        // Locked Doors
        public static List<string> LockedDoors = new();

        public static void Load()
        {
            // Load the subsections data into the dictionary
            var assembly = Assembly.GetExecutingAssembly();
            
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.subsections.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                Subsections = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                Patcher.Logger.LogInfo($"Loaded {Subsections.Count()} subsections");
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

            // Load monster data into the dictionary. This maps the human-readable names that AP uses to the form that Monster Sanctuary uses
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.monsters.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                Monsters = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                Patcher.Logger.LogInfo($"Loaded {Monsters.Count()} monster names");
            }

            // Load monster data into the dictionary. This maps the human-readable names that AP uses to the form that Monster Sanctuary uses
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.monster_locations.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                MonsterLocations = JsonConvert.DeserializeObject<List<string>>(json);
                Patcher.Logger.LogInfo($"Loaded {MonsterLocations.Count()} monster locations");
            }

            // Loads champion locations into a dictionary. Used to track champions and their default location ids
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.champion_locations.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                ChampionLocations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                Patcher.Logger.LogInfo($"Loaded {ChampionLocations.Count()} champion locations");
            }

            // Loads chest/gift counts for each region
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.number_of_checks.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                NumberOfChecks = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                Patcher.Logger.LogInfo($"Loaded {NumberOfChecks.Sum(kvp => kvp.Value)} chest/gift locations");
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
        }
        
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
        private static GameObject GetMonsterByName(string name)
        {
            // AP fills empty monster slots with this item. We don't care about it here,
            // and the game will never query for it, so we can safely ignore them
            if (name == "Empty Slot")
                return null;

            if (Monsters.ContainsKey(name))
            {
                name = Monsters[name];
            }

            return GameController.Instance.WorldData.Referenceables
                    .Where(x => x?.gameObject.GetComponent<Monster>() != null)
                    .Select(x => x.gameObject)
                    .SingleOrDefault(mon => string.Equals(mon.name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Given a champion monster's name, return the replacement for that champion
        /// </summary>
        /// <param name="championName"></param>
        /// <returns></returns>
        public static Monster GetReplacementChampion(string championName)
        {
            string location = null;
            if (ChampionLocations.ContainsKey(championName))
                location = ChampionLocations[championName];

            // If for some reason the location is not found, just bail
            if (location == null)
            {
                // Patcher.Logger.LogError($"Champion location for {championName} was not found.");
                return null;
            }

            // Because champion monsters could exist in either slot 1 (for figths with 3 monsters)
            // or in slot 0 (for fights with 1 monster), we need to check which one this is
            GameObject monsterObject = GetReplacementMonster(location + "_0");
            if (monsterObject == null)
                monsterObject = GetReplacementMonster(location + "_1");

            if (monsterObject == null)
            {
                // Patcher.Logger.LogError($"Could not get replacement monster for {championName} at {location}");
                return null;
            }

            Monster monster = monsterObject.GetComponent<Monster>();

            if (monster == null)
            {
                // Patcher.Logger.LogError($"Monster component for {championName} was not found.");
                return null;
            }

            return monster;
        }

        /// <summary>
        /// Returns the monster that is stored in the cache for a given location.
        /// If cache does not contain the location id, returns null
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static GameObject GetReplacementMonster(string locationId)
        {
            locationId = GetMappedLocation(locationId);

            if (MonstersCache.ContainsKey(locationId))
            {
                return MonstersCache[locationId].Item1;
            }

            // Patcher.Logger.LogWarning($"Location '{locationId}' is not in the monster cache");
            return null;
        }

        /// <summary>
        ///  Given the scene name for a champion encounter, return the original champion from that location
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public static GameObject GetReverseChampionReplacement(string region)
        {
            var kvp = ChampionLocations.FirstOrDefault(kvp => kvp.Value.Contains(region));
            if (kvp.Key == null)
                return null;
            return GetMonsterByName(kvp.Key);
        }

        /// <summary>
        /// Given a location, returns the mapped location that includes sub-region data (if present)
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static string GetMappedLocation(string location)
        {
            if (Subsections.ContainsKey(location))
                return Subsections[location];
            return location;
        }

        /// <summary>
        /// Returns the mapped location names for a given list of locations
        /// </summary>
        /// <param name="locations"></param>
        /// <returns></returns>
        public static List<string> GetMappedLocations(List<string> locations)
        {
            return locations.Select(l => GetMappedLocation(l)).ToList();
        }
    }
}
