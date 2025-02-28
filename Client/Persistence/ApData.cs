using Archipelago.MonsterSanctuary.Client.AP;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Persistence
{
    // SaveGameManager.SaveGameSlot can be used to uniquely identify which save slot we're in, and let us handle multiple save files at once.
    public class ApData
    {
        public static string DataFolder => Path.Combine(Environment.CurrentDirectory, "Archipelago");
        public static string DataFileRegex = @"ap_data_(\d+).json$";

        #region Data Files
        public static Dictionary<int, string> PersistenceFiles;
        public static void InitializePersistenceFiles()
        {
            Patcher.Logger.LogInfo("Initializing persistence files");
            PersistenceFiles = new Dictionary<int, string>();

            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            var files = Directory.EnumerateFiles(DataFolder);
            Regex r = new(DataFileRegex, RegexOptions.IgnoreCase);
            foreach (var fullFilePath in files)
            {
                string file = Path.GetFileName(fullFilePath);
                var match = r.Match(file);
                int.TryParse(match.Groups[0].Value, out int slotid);
                if (match.Success)
                {
                    Patcher.Logger.LogInfo($"\t{file}: Save slot {slotid}");
                    PersistenceFiles.Add(slotid, file);
                }
                else
                {
                    Patcher.Logger.LogWarning($"\tFile '{file}' did not match the pattern for a standard data file.");
                }
            }
        }

        public static ApDataFile CurrentFile { get; set; }

        public static void CreateFileForSaveSlot(int saveSlot)
        {
            Patcher.Logger.LogDebug("Creating file for save slot " + saveSlot);
            if (ApDataExistsForSaveSlot(saveSlot))
            {
                Patcher.Logger.LogDebug("Found an existing file for save slot " + saveSlot + ". Deleting.");
                DeleteFileForSaveSlot(saveSlot);
            }

            var file = new ApDataFile()
            {
                FileName = Path.Combine(DataFolder, $"ap_data_{saveSlot}.json"),
                SaveSlot = saveSlot
            };
            PersistenceFiles.Add(saveSlot, file.FileName);
            file.SaveFile();
        }

        public static bool LoadFileForSaveSlot(int saveSlot)
        {
            Patcher.Logger.LogDebug("Loading file for save slot: " + saveSlot);
            if (!ApDataExistsForSaveSlot(saveSlot))
            {
                Patcher.Logger.LogError("Persistence file not found for save slot " + saveSlot);
                return false;
            }

            if (HasApDataFile())
            {
                CurrentFile.SaveFile();
            }

            CurrentFile = new ApDataFile()
            {
                FileName = PersistenceFiles[saveSlot],
                SaveSlot = saveSlot
            };
            CurrentFile.LoadFile();
            return true;
        }

        public static void UnloadCurrentFile()
        {
            Patcher.Logger.LogDebug("Unloading current AP data file");
            if (!HasApDataFile())
                return;

            CurrentFile.SaveFile();
            CurrentFile = null;
        }

        public static void DeleteFileForSaveSlot(int saveSlot)
        {
            Patcher.Logger.LogDebug("Deleting file for save slot: " + saveSlot);
            if (!ApDataExistsForSaveSlot(saveSlot))
            {
                Patcher.Logger.LogWarning("Tried to delete persistence file for save slot " + saveSlot + ", but that file doesn't exist.");
                return;
            }

            // If our current AP file is what we're deleting
            // then we can delete it from the file
            if (HasApDataFile() && CurrentFile.SaveSlot == saveSlot)
            {
                CurrentFile.DeleteFile();
                CurrentFile = null;
            }

            // If we didn't delete the file above, then we can delete it here
            if (File.Exists(PersistenceFiles[saveSlot]))
            {
                File.Delete(PersistenceFiles[saveSlot]);
            }

            // Finally, remove it from the dictionary.
            PersistenceFiles.Remove(saveSlot);
        }

        public static void SetConnectionDataCurrentFile(string hostname, string slotname, string password)
        {
            Patcher.Logger.LogDebug("Adding connection data for current file");
            if (HasApDataFile())
            {
                CurrentFile.ConnectionInfo.HostName = hostname;
                CurrentFile.ConnectionInfo.SlotName = slotname;
                CurrentFile.ConnectionInfo.Password = password;
                CurrentFile.SaveFile();

            }
        }

        public static bool ApDataExistsForSaveSlot(int saveSlot)
        {
            if (PersistenceFiles == null)
            {
                Patcher.Logger.LogError("PersistenceFiles were null. This shouldn't happen");
            }
            return PersistenceFiles.ContainsKey(saveSlot);
        }

        public static bool HasApDataFile() => CurrentFile != null;
        #endregion

        #region Items Received
        public static int GetItemsReceived()
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to get the Items Received index, except the current AP data file is null.");
                return 0;
            }
            return CurrentFile.ItemsReceived;
        }

        public static bool IsItemReceived(int index)
        {
            return index <= GetItemsReceived();
        }

        public static void SetItemsReceived(int index)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to set the Items Received index, except the current AP data file is null.");
                return;
            }
            if (index > CurrentFile.ItemsReceived)
            {
                CurrentFile.ItemsReceived = index;
                CurrentFile.SaveFile();
            }
        }
        #endregion

        #region Locations Checked
        public static bool HasLocationBeenChecked(string location)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to see if a location has been checked, except the current AP data file is null.");
                return true;
            }
            return CurrentFile.LocationsChecked.Contains(location);
        }

        /// <summary>
        /// Marks a location as having been checked, even if the client is disconnected from Archipelago. Does nothing if no ApData file is loaded
        /// </summary>
        /// <param name="location"></param>
        public static void MarkLocationAsChecked(string location)
        {
            // We want to be able to call this method even if we're offline or in a vanilla save with no AP data
            if (!HasApDataFile())
            {
                return;
            }

            if (!HasLocationBeenChecked(location))
            {
                CurrentFile.LocationsChecked.Add(location);
                CurrentFile.SaveFile();
            }
        }

        public static List<string> GetCheckedLocations()
        {

            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to get checked locations, except the current AP data file is null.");
                return new();
            }

            return CurrentFile.LocationsChecked;
        }

        public static List<long> GetCheckedLocationsAsIds()
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to get checked locations, except the current AP data file is null.");
                return new();
            }

            return GetCheckedLocations()
                .Select(Locations.GetLocationId)
                .Where(l => l.HasValue)
                .Select(l => l.Value)
                .ToList();
        }
        #endregion

        #region Check Counter
        public static void RebuildCheckCounter()
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to rebuild the check counter, except the current AP data file is null.");
                return;
            }

            CurrentFile.CheckCounter = new();
            var locations = ApState.Session.Locations.AllLocationsChecked.Except(Champions.GetChampionRankLocationIds());

            foreach (var location in locations)
                IncrementCheckCounter(location);

            CurrentFile.SaveFile();
        }

        public static void IncrementCheckCounter(long locationId)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to increment the check counter, except the current AP data file is null.");
                return;
            }

            // Add a check to our check counter base don the area
            var name = Locations.GetLocationName(locationId);
            if (!string.IsNullOrEmpty(name))
            {
                var regionName = Locations.GetAreaNameFromLocationName(name);
                if (!IsRegionInCheckCounter(regionName))
                    CurrentFile.CheckCounter[regionName] = 0;

                CurrentFile.CheckCounter[regionName] += 1;
                CurrentFile.SaveFile();
            }
        }

        public static bool IsRegionInCheckCounter(string region)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to check if region is in the check counter, except the current AP data file is null.");
                return false;
            }

            return CurrentFile.CheckCounter.ContainsKey(region);
        }

        public static int GetCheckCounter(string region)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to get the check counter, except the current AP data file is null.");
                return 0;
            }

            if (IsRegionInCheckCounter(region))
                return CurrentFile.CheckCounter[region];
            return 0;
        }
        #endregion

        #region Champions
        public static void AddChampionAsDefeated(string scene)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to mark a champion as defeated, except the current AP data file is null.");
                return;
            }

            if (IsChampionDefeated(scene))
                return;

            CurrentFile.ChampionsDefeated.Add(scene);
            CurrentFile.SaveFile();
        }

        public static int GetNumberOfChampionsDefeated()
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to get the number of champions defeated, except the current AP data file is null.");
                return 0;
            }
            return CurrentFile.ChampionsDefeated.Count();
        }

        public static bool IsChampionDefeated(string scene)
        {
            if (!HasApDataFile())
            {
                Patcher.Logger.LogError("Tried to check if a champion as defeated, except the current AP data file is null.");
                return false;
            }

            return CurrentFile.ChampionsDefeated.Contains(scene);
        }
        #endregion
    }
}
