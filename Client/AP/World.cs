using Archipelago.MonsterSanctuary.Client.Options;
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
        public static ProgressionFlagSkips ProgressionFlags = new();

        /// <summary>
        /// Returns true if a given progression flag should set to true, overriding the normal behavior of that flag
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool ShouldSetProgressionFlag(string flag) => ProgressionFlags.ShouldSetFlag(flag);

        /// <summary>
        /// Returns true if a given interactable item should be marked as having been interacted with, overriding the normal behavior of that interactable object
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool ShouldInteractableBeActivated(string flag) => ProgressionFlags.ShouldInteractableBeActivated(flag);

        public static bool ShouldTediousDoorBeSkipped(string flag) => ProgressionFlags.ShouldTediousDoorBeSkipped(flag);

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

            // Loads Game flags
            using (Stream stream = assembly.GetManifestResourceStream(
                "Archipelago.MonsterSanctuary.Client.data.progression_flags.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                ProgressionFlags = JsonConvert.DeserializeObject<ProgressionFlagSkips>(json);
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

    public class ProgressionFlagSkips
    {
        public List<string> Plotless = new();
        public List<string> TediousPuzzles = new();
        public OpenWorldCollection BlueCaves = new();
        public OpenWorldCollection StrongholdDungeon = new();
        public OpenWorldCollection SnowyPeaks = new();
        public OpenWorldCollection AncientWoods = new();
        public OpenWorldCollection SunPalace = new();
        public OpenWorldCollection HorizonBeach = new();
        public OpenWorldCollection MagmaChamber = new();
        public OpenWorldCollection BlobBurg = new();
        public OpenWorldCollection ForgottenWorld = new();
        public OpenWorldCollection MysticalWorkshop = new();
        public OpenWorldCollection Underworld = new();
        public OpenWorldCollection AbandonedTower = new();

        public bool ShouldSetFlag(string flag)
        {
            if (!ApState.IsConnected)
                return false;

            if (Plotless.Contains(flag))
                return SlotData.SkipPlot;

            if (BlueCaves.Contains(flag))
                return SlotData.OpenBlueCaves;

            if (StrongholdDungeon.Contains(SlotData.OpenStrongholdDungeon, flag))
                return true;

            if (SnowyPeaks.Contains(flag))
                return SlotData.OpenSnowyPeaks;

            if (SunPalace.Contains(SlotData.OpenSunPalace, flag))
                return true;

            if (AncientWoods.Contains(flag))
                return SlotData.OpenAncientWoods;

            if (HorizonBeach.Contains(SlotData.OpenHorizonBeach, flag))
                return true;

            if (MagmaChamber.Contains(SlotData.OpenMagmaChamber, flag))
                return true;

            if (BlobBurg.Contains(SlotData.OpenBlobBurg, flag))
                return true;

            if (ForgottenWorld.Contains(SlotData.OpenForgottenWorld, flag))
                return true;

            if (MysticalWorkshop.Contains(flag))
                return SlotData.OpenMysticalWorkshop;

            if (Underworld.Contains(SlotData.OpenUnderworld, flag))
                return true;

            if (AbandonedTower.Contains(SlotData.OpenAbandonedTower, flag))
                return true;

            return false;
        }

        public bool ShouldTediousDoorBeSkipped(string flag)
        {
            if (!ApState.IsConnected)
                return false;

            return TediousPuzzles.Contains(flag);
        }

        public bool ShouldInteractableBeActivated(string interactable)
        {
            return ShouldSetFlag(interactable);
        }

        public void PrintDebug()
        {
            Patcher.Logger.LogInfo("Story Skips");
            foreach (var skip in Plotless)
                Patcher.Logger.LogInfo("\t" + skip);

            Patcher.Logger.LogInfo("Blue Caves");
            BlueCaves.Print();

            Patcher.Logger.LogInfo("Stronghold Dungeon");
            StrongholdDungeon.Print();

            Patcher.Logger.LogInfo("Snowy Peaks");
            SnowyPeaks.Print();

            Patcher.Logger.LogInfo("Ancient Woods");
            AncientWoods.Print();

            Patcher.Logger.LogInfo("Sun Palace");
            SunPalace.Print();

            Patcher.Logger.LogInfo("Horizon Beach");
            HorizonBeach.Print();

            Patcher.Logger.LogInfo("Magma Chamber");
            MagmaChamber.Print();

            Patcher.Logger.LogInfo("Blob Burg");
            BlobBurg.Print();

            Patcher.Logger.LogInfo("Forgotten World");
            ForgottenWorld.Print();

            Patcher.Logger.LogInfo("Underworld");
            Underworld.Print();

            Patcher.Logger.LogInfo("Mystical Workshop");
            MysticalWorkshop.Print();

            Patcher.Logger.LogInfo("Abandoned Tower");
            AbandonedTower.Print();
        }
    }

    public class OpenWorldCollection
    {
        public List<string> Entrances = new();
        public List<string> Interior = new();
        public IEnumerable<string> Everything => Entrances.Union(Interior);

        public IEnumerable<string> this[OpenWorldSetting setting]
        {
            get
            {
                return setting == OpenWorldSetting.Full
                    ? Everything
                    : setting == OpenWorldSetting.Entrances
                        ? Entrances
                        : Interior;
            }
        }

        public bool Contains(OpenWorldSetting setting, string key)
        {
            if (setting == OpenWorldSetting.Closed)
                return false;

            return this[setting].Contains(key);
        }

        public bool Contains(string key)
        {
            return Everything.Contains(key);
        }

        public void Print()
        {
            Patcher.Logger.LogInfo("\tEntrances");
            foreach (var item in Entrances)
                Patcher.Logger.LogInfo("\t\t" + item);

            Patcher.Logger.LogInfo("\tInterior");
            foreach (var item in Interior)
                Patcher.Logger.LogInfo("\t\t" + item);
        }
    }
}
