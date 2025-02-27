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
            Regex r = new Regex(DataFileRegex, RegexOptions.IgnoreCase);
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

            if (CurrentFile != null)
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
            if (CurrentFile == null)
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
            if (CurrentFile != null && CurrentFile.SaveSlot == saveSlot)
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
            if (CurrentFile != null)
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


        public static void AddChampionDefeated(string scene)
        {
            if (CurrentFile == null)
                return;

            CurrentFile.AddChampionDefeated(scene);
        }

        public static int GetNumberOfChampionsDefeated()
        {
            if (CurrentFile == null)
                return 0;
            return CurrentFile.ChampionsDefeated.Count();
        }

        public static bool IsCampionDefeated(string scene)
        {
            if (CurrentFile == null)
                return false;
            return CurrentFile.ChampionsDefeated.Contains(scene);
        }


        public static void AddToItemCache(int id)
        {
            if (CurrentFile == null)
                return;

            CurrentFile.AddToItemCache(id);
        }

        public static bool IsItemReceived(long itemId)
        {
            if (!APState.IsConnected)
                return true;
            if (CurrentFile == null)
                return true;

            return CurrentFile.LocationsChecked.Contains(itemId);
        }


        public static void AddAndUpdateCheckedLocations(long locationId)
        {
            if (CurrentFile == null)
                return;

            CurrentFile.AddAndUpdateCheckedLocations(locationId);
        }


        public static List<long> GetLocationsChecked()
        {
            if (CurrentFile == null)
                return new();

            return CurrentFile.LocationsChecked;
        }

        public static bool IsLocationChecked(long locationId)
        {
            if (!APState.IsConnected)
                return true;
            if (CurrentFile == null)
                return true;

            return CurrentFile.LocationsChecked.Contains(locationId);
        }


        public static void RebuildCheckCounter()
        {
            if (!APState.IsConnected)
                return;

            if (CurrentFile == null)
                return;

            CurrentFile.RebuildCheckCounter();
        }

        public static void IncrementCheckCounter(long locationId)
        {
            if (!APState.IsConnected)
                return;

            if (CurrentFile == null)
                return;

            CurrentFile.IncrementCheckCounter(locationId);
        }

        public static bool IsRegionInCheckCounter(string region)
        {
            if (!APState.IsConnected)
                return false;

            if (CurrentFile == null)
                return false;

            return CurrentFile.CheckCounter.ContainsKey(region);
        }

        public static int GetCheckCounter(string region)
        {
            if (!APState.IsConnected)
                return 0;

            if (CurrentFile == null)
                return 0;

            if (IsRegionInCheckCounter(region))
                return CurrentFile.CheckCounter[region];
            return 0;
        }
    }
}
