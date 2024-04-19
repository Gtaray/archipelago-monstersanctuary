using Archipelago.MonsterSanctuary.Client.Behaviors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public class Persistence
    {
        private static Persistence _instance;
        public static Persistence Instance
        {
            get 
            {
                if (_instance == null)
                    _instance = new Persistence();
                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        public const string PERSISTENCE_FILENAME = "archipelago_save_data.json";

        public List<int> ItemsRecieved = new(); // Stores the list of item indexes received from the server
        public List<long> LocationsChecked = new List<long>();  // Includes champion rank up items
        public Dictionary<string, int> CheckCounter = new Dictionary<string, int>();  // does NOT include champion rank up items
        public List<string> ChampionsDefeated = new List<string>(); // Tracks which champions have been defeated
        public List<string> ExploreItems = new List<string>();

        private static bool _loaded = false;

        public static void PrintData()
        {
            Patcher.Logger.LogInfo("Persistence:");
            Patcher.Logger.LogInfo("\tItems Received: " + Instance.ItemsRecieved);
            Patcher.Logger.LogInfo("\tILocations Checked: " + Instance.LocationsChecked.Count());
            Patcher.Logger.LogInfo("\tChampions Defeated: " + Instance.ChampionsDefeated.Count());
        }

        public static bool HasReceivedItem(int index)
        {
            return Instance.ItemsRecieved.Contains(index);
        }

        public static void AddToItemCache(int id)
        {
            if (HasReceivedItem(id)) 
                return;

            Instance.ItemsRecieved.Add(id);
            SaveFile();
        }

        public static void AddAndUpdateCheckedLocations(IEnumerable<long> locationIds)
        {
            foreach (long id in locationIds)
                AddAndUpdateCheckedLocations(id);
        }

        public static void AddAndUpdateCheckedLocations(long locationId)
        {
            if (Instance.LocationsChecked.Contains(locationId))
                return;

            Instance.LocationsChecked.Add(locationId);
            SaveFile();

            // If the item we just recieved is a rank up item, we don't increment the counter
            if (GameData.ChampionRankIds.ContainsValue(locationId))
                return;

            IncrementCheckCounter(locationId);
        }

        public static void RebuildCheckCounter()
        {
            if (!APState.IsConnected)
                return;

            Instance.CheckCounter = new();
            var locations = Instance.LocationsChecked.Except(GameData.ChampionRankIds.Values);

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
            if (!Instance.CheckCounter.ContainsKey(regionName))
                Instance.CheckCounter[regionName] = 0;
            Instance.CheckCounter[regionName] += 1;
        }

        public static void AddChampionDefeated(string scene)
        {
            if (Instance.ChampionsDefeated.Contains(scene)) 
                return;

            Instance.ChampionsDefeated.Add(scene);
            SaveFile();
        }

        public static void DeleteFile()
        {
            Patcher.Logger.LogInfo("DeleteFile()");
            if (File.Exists(PERSISTENCE_FILENAME))
                File.Delete(PERSISTENCE_FILENAME);

            Instance = new Persistence();
        }

        public static void SaveFile()
        {
            // Never save the file before its loaded to avoid overwriting it
            if (!_loaded)
            {
                // If it's not loaded, we check to see if the player has collected anything
                // If they have, we treat the file has having been loaded.
                _loaded = Instance.ItemsRecieved.Any()
                    || Instance.LocationsChecked.Count() > 0
                    || Instance.ChampionsDefeated.Count() > 0
                    || Instance.ExploreItems.Count() > 0;

            }

            Patcher.Logger.LogInfo("SaveFile()");
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                //Persistence.SnapshotExploreItems();
                foreach (var item in Instance.ExploreItems)
                {
                    Patcher.Logger.LogInfo("\t" + item);
                }

                var json = JsonConvert.SerializeObject(Persistence.Instance);
                File.WriteAllText(
                    Path.Combine(rawPath, PERSISTENCE_FILENAME), 
                    json);
            }
        }

        public static void LoadFile()
        {
            _loaded = true;

            Patcher.Logger.LogInfo("LoadFile()");
            if (File.Exists(PERSISTENCE_FILENAME))
            {
                var json = File.ReadAllText(PERSISTENCE_FILENAME);
                Instance = JsonConvert.DeserializeObject<Persistence>(json);

                // No reason to do this here, as GameController doesn't exist
                // when this file is initially loaded.
                //ReloadExploreItems();
                foreach (var item in Instance.ExploreItems)
                {
                    Patcher.Logger.LogInfo("\t" + item);
                }
            }
        }
    }
}
