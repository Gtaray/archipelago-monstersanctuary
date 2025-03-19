using Archipelago.MonsterSanctuary.Client.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public class Monsters
    {
        /// <summary>
        /// A dictionary that maps all monsters placed throughout the world to a cached game object and Monster component
        /// This prevents any async weirdness when handling AI or encounter loading
        /// The key is the logical name of the encounter, which is given to the client in the AP slot data upon connecting to an AP server
        /// </summary>
        public static Dictionary<string, Tuple<GameObject, Monster>> MonstersCache = new();

        /// <summary>
        /// Maps monster names from AP to Monster Sanctuary
        /// Only needed for monsters who names have spaces or special characters
        /// </summary>
        public static Dictionary<string, string> MonsterNames = new Dictionary<string, string>();

        /// <summary>
        /// Some monsters, including champions, have visual elements that are not attached to an encounter and are instead treated as NPCs in the world
        /// This dictionary maps a unique ID for each of those NPCs to a monster name
        /// When those NPC actors are loaded to a scene, we can look up if we should swap them out for some other monster.
        /// </summary>
        public static Dictionary<string, string> NPCs = new Dictionary<string, string>();

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
        /// Returns true if a monster replacement exists in the monster cache
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="encounterId"></param>
        /// <param name="monsterIndex"></param>
        /// <returns></returns>
        public static bool IsMonsterCached(string scene, int? encounterId, int monsterIndex)
        {
            if (encounterId == null)
                return false;

            string loc = $"{scene}_{encounterId.Value}_{monsterIndex}";
            return IsMonsterCached(loc);
        }

        /// <summary>
        /// Returns true if a monster replacement exists in the monster cache
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="encounterId"></param>
        /// <param name="monsterIndex"></param>
        /// <returns></returns>
        public static bool IsMonsterCached(string locationId)
        {
            return MonstersCache.ContainsKey(locationId);
        }

        /// <summary>
        /// Returns the monster that is stored in the cache for a given location.
        /// If cache does not contain the location id, returns null
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static GameObject GetReplacementMonster(string scene, int? encounterId, int monsterIndex)
        {
            if (encounterId == null)
                return null;

            string locationId = $"{scene}_{encounterId.Value}_{monsterIndex}";
            return GetReplacementMonster(locationId);
        }

        /// <summary>
        /// Returns the monster that is stored in the cache for a given location.
        /// If cache does not contain the location id, returns null
        /// </summary>
        /// <param name="locationName"></param>
        /// <returns></returns>
        public static GameObject GetReplacementMonster(string locationName)
        {
            if (!IsMonsterCached(locationName))
            {
                Patcher.Logger.LogWarning($"Location '{locationName}' is not in the monster cache");
                return null;
            }

            return MonstersCache[locationName].Item1;
        }

        /// <summary>
        /// Returns true if a given actor within a scene is an NPC monster that needs replacing
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="actorName"></param>
        /// <returns></returns>
        public static bool IsNpc(string scene, string actorName)
        {
            string id = $"{scene}_{actorName}";
            return NPCs.ContainsKey(id);
        }

        /// <summary>
        /// Gets the logical name of the randomized slot where an NPC should pull its data from
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="actorName"></param>
        /// <returns></returns>
        public static string GetNpcLocationName(string scene, string actorName)
        {
            if (!IsNpc(scene, actorName))
                return null;

            string id = $"{scene}_{actorName}";
            return NPCs[id];
        }

        /// <summary>
        /// Empties out data that is supplied by Archipelago. Used primarily to refresh state when connecting to AP
        /// </summary>
        public static void ClearApData()
        {
            MonstersCache.Clear();
        }

        public static Dictionary<string, ExploreAbilityUnlockData> ExploreAbilityUnlockData { get; set; }

        /// <summary>
        /// Gets the items (and quantities) required to use a given monster's ability
        /// </summary>
        /// <param name="monsterName"></param>
        /// <returns></returns>
        public static List<ExploreAbilityUnlockItem> GetItemsRequiredToUseMonstersAbility(string monsterName)
        {
            if (!ExploreAbilityUnlockData.ContainsKey(monsterName))
                return new();

            return ExploreAbilityUnlockData[monsterName].GetRequiredItems();
        }

        public static string GetExploreItemDisplayTextForMonster(string monsterName)
        {
            if (!ExploreAbilityUnlockData.ContainsKey(monsterName))
                return "";

            return ExploreAbilityUnlockData[monsterName].ToDisplayText();
        }

        /// <summary>
        /// Loads all relevant monster data from json files
        /// </summary>
        public static void Load()
        {
            var assembly = Assembly.GetExecutingAssembly();

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

            // Load Explore item data
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.monster_explore_ability_unlocks.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                ExploreAbilityUnlockData = JsonConvert.DeserializeObject<Dictionary<string, ExploreAbilityUnlockData>>(json);
            }
        }
    }

    public class ExploreAbilityUnlockData
    {
        public string MonsterName { get; set; }
        public List<ExploreAbilityUnlockItem> Species { get; set; } = new();
        public List<ExploreAbilityUnlockItem> Ability { get; set; } = new();
        public List<ExploreAbilityUnlockItem> Type { get; set; } = new();
        public List<ExploreAbilityUnlockItem> Progression { get; set; } = new();
        public List<ExploreAbilityUnlockItem> Combo { get; set; } = new();

        public List<ExploreAbilityUnlockItem> GetRequiredItems()
        {
            if (!ApState.IsConnected)
                return new();

            switch (SlotData.LockedExploreAbilities)
            {
                case LockedExploreAbilities.Specie:
                    return Species;
                case LockedExploreAbilities.Ability:
                    return Ability;
                case LockedExploreAbilities.Type:
                    return Type;
                case LockedExploreAbilities.Progression:
                    return Progression;
                case LockedExploreAbilities.Combo:
                    return Combo;
                default:
                    return new();
            }
        }

        public string ToDisplayText()
        {
            if (!ApState.IsConnected)
                return "";

            IEnumerable<string> items;
            switch (SlotData.LockedExploreAbilities)
            {
                case LockedExploreAbilities.Specie:
                    items = Species.Select(i => i.ToDisplayText());
                    break;
                case LockedExploreAbilities.Ability:
                    items = Ability.Select(i => i.ToDisplayText());
                    break;
                case LockedExploreAbilities.Type:
                    items = Type.Select(i => i.ToDisplayText());
                    break;
                case LockedExploreAbilities.Progression:
                    items = Progression.Select(i => i.ToDisplayText());
                    break;
                case LockedExploreAbilities.Combo:
                    items = Combo.Select(i => i.ToDisplayText());
                    break;
                default:
                    items = new List<string>();
                    break;
            }

            if (items.Count() == 0)
                return "";
            else if (items.Count() == 1)
                return items.First();
            else
                return string.Join(", ", items, 0, items.Count() - 1) + ", and " + items.LastOrDefault();
        }
    }

    public class ExploreAbilityUnlockItem
    {
        public string ItemName { get; set; }
        public int Count { get; set; } = 1;

        public string ToDisplayText()
        {
            string formattedItemName = Patcher.FormatItem(ItemName, ItemClassification.Progression);
            return Count == 1
                ? formattedItemName
                : $"{Count} {formattedItemName}";
        }
    }
}
