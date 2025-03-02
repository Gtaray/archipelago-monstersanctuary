using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public  class Champions
    {
        /// <summary>
        /// Original champions indexed by the scenes they're in
        /// </summary>
        public static Dictionary<string, string> OriginalChampions = new();

        /// <summary>
        /// Maps scenes to the champions that are put in those scenes. Easy way to handle replacing them.
        /// </summary>
        public static Dictionary<string, string> ReplacedChampions = new();

        /// <summary>
        /// Location ids for the champion rank items
        /// </summary>
        public static Dictionary<string, long> ChampionRankIds = new();

        public static string GetOriginalChampionForScene(string scene)
        {
            if (OriginalChampions.ContainsKey(scene))
                return OriginalChampions[scene];
            return null;
        }

        /// <summary>
        /// Adds a champion scene, mapping a scene to a champion name
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="championName"></param>
        public static void AddChampionScene(string scene, string championName)
        {
            if (!ReplacedChampions.ContainsKey(scene))
                ReplacedChampions.Add(scene, championName);
        }

        /// <summary>
        /// Returns true if a given scene has a champion in it
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static bool DoesSceneHaveAChampion(string scene)
        {
            return ReplacedChampions.ContainsKey(scene);
        }

        /// <summary>
        /// Adds a champion rank, mapping logical location name to AP location ID
        /// </summary>
        /// <param name="locationName"></param>
        /// <param name="locationId"></param>
        public static void AddChampionRank(string locationName, long locationId)
        {
            if (IsLocationAChampionRank(locationName))
                return;

            ChampionRankIds.Add(locationName, locationId);
        }

        /// <summary>
        /// Checks a location name to see if it is a champion rank check
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static bool IsLocationAChampionRank(string locationName)
        {
            return ChampionRankIds.ContainsKey(locationName);
        }

        public static long? GetChampionRankLocationId(string locationName)
        {
            if (IsLocationAChampionRank(locationName))
                return ChampionRankIds[locationName];
            return null;
        }

        /// <summary>
        /// Gets all AP location IDs for champion ranks
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<long> GetChampionRankLocationIds()
        {
            return ChampionRankIds.Values.ToList();
        }

        /// <summary>
        /// Empties out data that is supplied by Archipelago. Used primarily to refresh state when connecting to AP
        /// </summary>
        public static void ClearApData()
        {
            ReplacedChampions.Clear();
            ChampionRankIds.Clear();
        }

        /// <summary>
        /// Loads champion data from json files
        /// </summary>
        public static void Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.original_champions.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                OriginalChampions = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }
    }
}
