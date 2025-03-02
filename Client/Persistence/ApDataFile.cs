using Archipelago.MonsterSanctuary.Client.AP;
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

        /// <summary>
        /// The index that tracks what items have been received. 
        /// This is the next index that the client expects to receive when handling items. 
        /// Indirectly, all items with indexes lower than this number have been received
        /// </summary>
        [JsonProperty("NextExpectedItemIndex")]
        public int NextExpectedItemIndex { get; set; } = 0;

        /// <summary>
        /// A list of all locations within the game that have been checked.
        /// Saves the locations in their string form (e.g. regionname_roomname_chestid) so that this will work even when offline
        /// When connecting, compare this list to the list of ChecksSent to see what checks still need to be sent to AP
        /// </summary>
        [JsonProperty("LocationsChecked")]
        public List<string> LocationsChecked = new();

        /// <summary>
        /// Tracks how many checks in each region have been completed, ignoring the checks for champion rank ups
        /// </summary>
        [JsonProperty("CheckCounter")]
        public Dictionary<string, int> CheckCounter = new Dictionary<string, int>();

        /// <summary>
        /// A list of scene names that tracks which champions have been defeated. If a scene name exists here, it means that scene's champion is defeated
        /// </summary>
        [JsonProperty("ChampionsDefeated")]
        public List<string> ChampionsDefeated = new List<string>();

        public void PrintData()
        {
            Patcher.Logger.LogInfo("Persistence:");
            Patcher.Logger.LogInfo("File Name: " + FileName);
            Patcher.Logger.LogInfo("\tNext Expected Item Index: " + NextExpectedItemIndex);
            Patcher.Logger.LogInfo("\tLocations Checked: " + LocationsChecked.Count());
            Patcher.Logger.LogInfo("\tChampions Defeated: " + ChampionsDefeated.Count());
        }

        #region File Management
        public void DeleteFile()
        {
            Patcher.Logger.LogDebug("Deleting file: " + FileName);
            if (File.Exists(FileName))
                File.Delete(FileName);

            this.NextExpectedItemIndex = new();
            this.CheckCounter = new();
            this.ChampionsDefeated = new();
        }

        public void SaveFile()
        {
            Patcher.Logger.LogDebug("Saving file: " + FileName);
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
            Patcher.Logger.LogDebug("Loading file: " + FileName);
            if (File.Exists(FileName))
            {
                var json = File.ReadAllText(FileName);
                var file = JsonConvert.DeserializeObject<ApDataFile>(json);
                this.ConnectionInfo = file.ConnectionInfo;
                this.NextExpectedItemIndex = file.NextExpectedItemIndex;
                this.LocationsChecked = file.LocationsChecked;
                this.CheckCounter = file.CheckCounter;
                this.ChampionsDefeated = file.ChampionsDefeated;
            }
        }
        #endregion
    }
}
