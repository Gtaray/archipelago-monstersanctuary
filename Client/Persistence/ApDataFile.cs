using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Persistence
{
    public class ApDataFile
    {
        [JsonIgnore]
        public string FileName { get; set; }

        [JsonIgnore]
        public int SaveSlot { get; set; }

        [JsonProperty("ConnectionInfo")]
        public ArchipelagoConnectionInfo ConnectionInfo { get; set; } = new();

        [JsonProperty("ItemsReceived")]
        public List<int> ItemsReceived = new List<int>();

        [JsonProperty("LocationsChecked")]
        public List<long> LocationsChecked = new List<long>();  // Includes champion rank up items

        [JsonProperty("CheckCounter")]
        public Dictionary<string, int> CheckCounter = new Dictionary<string, int>();  // does NOT include champion rank up items

        [JsonProperty("ChampionsDefeated")]
        public List<string> ChampionsDefeated = new List<string>(); // Tracks which champions have been defeated

        public void PrintData()
        {
            Patcher.Logger.LogInfo("Persistence:");
            Patcher.Logger.LogInfo("File Name: " + FileName);
            Patcher.Logger.LogInfo("\tItems Received: " + ItemsReceived.Count());
            Patcher.Logger.LogInfo("\tILocations Checked: " + LocationsChecked.Count());
            Patcher.Logger.LogInfo("\tChampions Defeated: " + ChampionsDefeated.Count());
        }

        public void AddToItemCache(int id)
        {
            if (ItemsReceived.Contains(id))
                return;

            ItemsReceived.Add(id);
            SaveFile();
        }

        public void AddAndUpdateCheckedLocations(long locationId)
        {
            if (LocationsChecked.Contains(locationId))
                return;

            LocationsChecked.Add(locationId);
            SaveFile();

            // If the item we just recieved is a rank up item, we don't increment the counter
            if (GameData.ChampionRankIds.ContainsValue(locationId))
                return;

            IncrementCheckCounter(locationId);
        }

        public void RebuildCheckCounter()
        {
            if (!APState.IsConnected)
                return;

            CheckCounter = new();
            var locations = LocationsChecked.Except(GameData.ChampionRankIds.Values);

            foreach (var location in locations)
                IncrementCheckCounter(location);
        }

        public void IncrementCheckCounter(long locationId)
        {
            if (!APState.IsConnected)
                return;

            // Add a check to our check counter base don the area
            var locationName = APState.Session.Locations.GetLocationNameFromId(locationId);
            var regionName = locationName.Replace(" ", "").Split('-').First();
            if (!CheckCounter.ContainsKey(regionName))
                CheckCounter[regionName] = 0;
            CheckCounter[regionName] += 1;
        }

        public void AddChampionDefeated(string scene)
        {
            if (ChampionsDefeated.Contains(scene))
                return;

            ChampionsDefeated.Add(scene);
            SaveFile();
        }

        #region File Management
        public void DeleteFile()
        {
            Patcher.Logger.LogInfo("Deleting file: " + FileName);
            if (File.Exists(FileName))
                File.Delete(FileName);

            this.ItemsReceived = new();
            this.CheckCounter = new();
            this.LocationsChecked = new();
            this.ChampionsDefeated = new();
        }

        public void SaveFile()
        {
            Patcher.Logger.LogInfo("Saving file: " + FileName);
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var json = JsonConvert.SerializeObject(this);
                File.WriteAllText(
                    Path.Combine(rawPath, FileName),
                    json);
            }
        }

        public void LoadFile()
        {
            Patcher.Logger.LogInfo("Loading file: " + FileName);
            if (File.Exists(FileName))
            {
                var json = File.ReadAllText(FileName);
                var file = JsonConvert.DeserializeObject<ApDataFile>(json);
                this.ConnectionInfo = file.ConnectionInfo;
                this.ItemsReceived = file.ItemsReceived;
                this.CheckCounter = file.CheckCounter;
                this.LocationsChecked = file.LocationsChecked;
                this.ChampionsDefeated = file.ChampionsDefeated;
            }
        }
        #endregion
    }
}
