using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    /// <summary>
    /// A collection of easily accessible shortcuts for handling item locations and names
    /// </summary>
    public class Locations
    {
        /// <summary>
        /// Maps the logical <scene>_<object_id> format that the client generates to the location ids that AP cares about
        /// </summary>
        public static Dictionary<string, long> NameToId = new();

        /// <summary>
        /// Maps the AP location ID to the logical <scene>_<object_id> format
        /// </summary>
        public static Dictionary<long, string> IdToName = new();

        /// <summary>
        /// The number of checks in each region, indexed by region name
        /// </summary>
        public static Dictionary<string, int> NumberOfChecks = new();

        /// <summary>
        /// Returns a list of the location IDs for all checks
        /// </summary>
        /// <returns></returns>
        public static List<long> GetAllLocations()
        {
            return NameToId.Values.ToList();
        }

        /// <summary>
        /// Adds a location to the game data for easy access
        /// </summary>
        /// <param name="logicalName"></param>
        /// <param name="locationId"></param>
        public static void AddLocation(string logicalName, long locationId, string area)
        {
            NameToId[logicalName] = locationId;
            IdToName[locationId] = logicalName;

            if (!NumberOfChecks.ContainsKey(area))
                NumberOfChecks[area] = 0;
            NumberOfChecks[area] += 1;
        }

        /// <summary>
        /// Gets an AP Location ID from a given logical name
        /// </summary>
        /// <param name="locationName"></param>
        /// <returns>AP location ID (or null if the location is not mapped)</returns>
        public static long? GetLocationId(string locationName)
        {
            if (string.IsNullOrEmpty(locationName))
                return null;
            if (!NameToId.ContainsKey(locationName))
                return null;
            return NameToId[locationName];
        }

        /// <summary>
        /// Gets a location's logical name from a given AP Location ID
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns>Locations logical name</returns>
        public static string GetLocationName(long locationId)
        {
            if (!IdToName.ContainsKey(locationId))
                return null;
            return IdToName[locationId];
        }

        /// <summary>
        /// Returns true if a given name exists
        /// </summary>
        /// <param name="locationName"></param>
        /// <returns></returns>
        public static bool DoesLocationExist(string locationName)
        {
            return NameToId.ContainsKey(locationName);
        }

        /// <summary>
        /// Returns true if a given location ID exists
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static bool DoesLocationExist(long locationId)
        {
            return IdToName.ContainsKey(locationId);
        }

        /// <summary>
        /// Gets the area/region from a given logical name.
        /// </summary>
        /// <param name="locationName"></param>
        /// <returns>Region name of a given scene</returns>
        public static string GetAreaNameFromLocationName(string locationName)
        {
            if (string.IsNullOrEmpty(locationName))
                return null;

            return locationName
                .Replace(" ", "")
                .Split('_')
                .First();
        }

        /// <summary>
        /// Gets the number of checks for a specific region
        /// </summary>
        /// <param name="regionName"></param>
        /// <returns></returns>
        public static int GetNumberOfChecksForRegion(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
                return 0;
            if (NumberOfChecks.ContainsKey(regionName))
                return NumberOfChecks[regionName];
            return 0;
        }

        #region Chest Graphics Match Contents
        public static HashSet<string> ProgressionLocations { get; set; } = new();
        public static HashSet<string> UsefulLocations { get; set; } = new();

        /// <summary>
        /// Adds a location to the list of locations where progression items are
        /// </summary>
        /// <param name="location"></param>
        public static void AddProgressionLocation(string location)
        {
            if (ProgressionLocations.Contains(location))
                return;
            ProgressionLocations.Add(location);
        }

        /// <summary>
        /// Returns true if a given location name has a progression item
        /// </summary>
        /// <returns></returns>
        public static bool IsLocationProgression(string location)
        {
            return ProgressionLocations.Contains(location);
        }

        /// <summary>
        /// Adds a location to the list of locations where useful items are
        /// </summary>
        /// <param name="location"></param>
        public static void AddUsefulLocation(string location)
        {
            if (UsefulLocations.Contains(location))
                return;
            UsefulLocations.Add(location);
        }

        /// <summary>
        /// Returns true if a given location name has a useful item
        /// </summary>
        /// <returns></returns>
        public static bool IsLocationUseful(string location)
        {
            return UsefulLocations.Contains(location);
        }
        #endregion

        /// <summary>
        /// Empties out data that is supplied by Archipelago. Used primarily to refresh state when connecting to AP
        /// </summary>
        public static void ClearApData()
        {
            NameToId.Clear();
            IdToName.Clear();
            NumberOfChecks.Clear();

            ProgressionLocations.Clear();
            UsefulLocations.Clear();
        }
    }
}
