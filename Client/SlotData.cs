using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft
    .Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Archipelago.MonsterSanctuary.Client
{
    public enum ShiftFlag
    {
        Never = 0,  // Monsters are never shifted
        Normal = 1, // Monsters only shift after sun palace
        Any = 2     // Monsters can be shifted any time
    }

    public enum LockedDoorsFlag
    {
        All = 0,
        Minimal = 1,
        None = 2
    }

    public enum CompletionEvent
    {
        MadLord = 0,
        Champions = 1,
        Monsterpedia = 2,
        Mozzie = 3
    }

    public enum ExploreAbilityLockType
    {
        Off = 0,
        Type = 1,
        Ability = 2,
        Species = 3
    }

    public enum OpenWorldSetting
    {
        Closed = 0,
        Entrances = 1,
        Interior = 2,
        Full = 3
    }

    public class HintData
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("ignore_other_text")]
        public bool IgnoreRemainingText { get; set; }
    }

    public class SlotData
    {
        // UNUSED
        public static bool SkipKeeperBattles { get; set; } = false;
        // END UNUSED

        public static int StartingFamiliar { get; set; } = -1;
        public static CompletionEvent Goal { get; set; } = CompletionEvent.MadLord;
        public static int MozzieSoulPieces { get; set; } = 7;
        public static bool IncludeChaosRelics { get; set; } = false;
        public static int ExpMultiplier { get; set; } = 1;
        public static bool AlwaysGetEgg { get; set; } = false;
        public static bool SkipPlot { get; set; } = false;
        public static bool OpenBlueCaves { get; set; } = false;
        public static OpenWorldSetting OpenStrongholdDungeon{ get; set; } = OpenWorldSetting.Closed;
        public static bool OpenSnowyPeaks { get; set; } = false;
        public static bool OpenAncientWoods { get; set; } = false;
        public static OpenWorldSetting OpenSunPalace { get; set; } = OpenWorldSetting.Closed;
        public static OpenWorldSetting OpenHorizonBeach { get; set; } = OpenWorldSetting.Closed;
        public static OpenWorldSetting OpenMagmaChamber { get; set; } = OpenWorldSetting.Closed;
        public static OpenWorldSetting OpenBlobBurg { get; set; } = OpenWorldSetting.Closed;
        public static OpenWorldSetting OpenForgottenWorld { get; set; } = OpenWorldSetting.Closed;
        public static bool OpenMysticalWorkshop { get; set; } = false;
        public static OpenWorldSetting OpenUnderworld { get; set; } = OpenWorldSetting.Closed;
        public static OpenWorldSetting OpenAbandonedTower { get; set; } = OpenWorldSetting.Closed;
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static LockedDoorsFlag LockedDoors { get; set; } = 0;
        public static bool AddSmokeBombs { get; set; } = false;
        public static int StartingGold { get; set; } = 1;
        public static bool ShopsIgnoreRank { get; set; } = false;
        public static bool DeathLink { get; set; } = false;
        public static string TanukiMonster { get; set; }
        public static string BexMonster{ get; set; }
        public static bool Eggsanity { get; set; }
        public static bool MonsterArmy { get; set; }
        public static ExploreAbilityLockType ExploreAbilityLock { get; set; }

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            var options = GetDictionaryData<object>(slotData, "options");
            Goal = GetEnumData(options, "goal", CompletionEvent.MadLord);
            MozzieSoulPieces = GetIntData(options, "mozzie_pieces", 7);
            IncludeChaosRelics = GetBoolData(options, "include_chaos_relics", false);
            ExpMultiplier = GetIntData(options, "exp_multiplier", 1);
            AlwaysGetEgg = GetBoolData(options, "monsters_always_drop_egg", false);
            SkipPlot = GetBoolData(options, "skip_plot", false);
            OpenBlueCaves = GetBoolData(options, "open_blue_caves", false);
            OpenStrongholdDungeon = GetEnumData(options, "open_stronghold_dungeon", OpenWorldSetting.Closed);
            OpenSnowyPeaks = GetBoolData(options, "open_snowy_peaks", false);
            OpenAncientWoods = GetBoolData(options, "open_ancient_woods", false);
            OpenSunPalace = GetEnumData(options, "open_sun_palace", OpenWorldSetting.Closed);
            OpenHorizonBeach = GetEnumData(options, "open_horizon_beach", OpenWorldSetting.Closed);
            OpenMagmaChamber = GetEnumData(options, "open_magma_chamber", OpenWorldSetting.Closed);
            OpenBlobBurg = GetEnumData(options, "open_blob_burg", OpenWorldSetting.Closed);
            OpenForgottenWorld = GetEnumData(options, "open_forgotten_world", OpenWorldSetting.Closed);
            OpenMysticalWorkshop = GetBoolData(options, "open_mystical_workshop", false);
            OpenUnderworld = GetEnumData(options, "open_underworld", OpenWorldSetting.Closed);
            OpenAbandonedTower = GetEnumData(options, "open_abandoned_tower", OpenWorldSetting.Closed);
            MonsterShiftRule = GetEnumData(options, "monster_shift_rule", ShiftFlag.Normal);
            LockedDoors = GetEnumData(options, "remove_locked_doors", LockedDoorsFlag.All);
            AddSmokeBombs = GetBoolData(options, "add_smoke_bombs", false);
            StartingGold = GetIntData(options, "starting_gold", 1);
            ShopsIgnoreRank = GetBoolData(options, "shops_ignore_rank", false);
            Eggsanity = GetBoolData(options, "eggsanity", false);
            MonsterArmy = GetBoolData(options, "monster_army", false);
            ExploreAbilityLock = GetEnumData(options, "lock_explore_abilities", ExploreAbilityLockType.Off);
            DeathLink = GetBoolData(options, "death_link", false);

            var monsterData = GetDictionaryData<object>(slotData, "monsters");
            BexMonster = GetStringData(monsterData, "bex_monster");
            TanukiMonster = GetStringData(monsterData, "tanuki");

            GameData.MonstersCache = new();
            var monsterLocations = GetDictionaryData<string>(monsterData, "monster_locations");
            foreach (var location in monsterLocations)
                GameData.AddMonster(location.Key, location.Value);

            GameData.ChampionScenes = new();
            GameData.ChampionScenes = GetDictionaryData<string>(monsterData, "champions");

            var itemLocations = GetDictionaryData<Dictionary<string, long>>(slotData, "locations");

            GameData.ShopChecks = itemLocations["shops"];
            GameData.ShopPrices = GetDictionaryData<int>(slotData, "prices");
            GameData.ChampionRankIds = itemLocations["ranks"];

            GameData.ItemChecks = new();
            GameData.NumberOfChecks = new();
            foreach (var locationGroup in itemLocations)
            {
                // Each entry in itemLocations is a key value pair
                // where the key is the area name, and the value is a dictionary of all item checks in that area
                foreach (var check in locationGroup.Value)
                {
                    // Skip over monster army, ranks, and shop stuff
                    if (GameData.ShopChecks.ContainsKey(check.Key))
                        continue;
                    if (GameData.ChampionRankIds.ContainsKey(check.Key))
                        continue;

                    GameData.AddItemCheck(check.Key, check.Value, locationGroup.Key);
                }
            }

            var hints = GetListData<HintData>(slotData, "hints");
            GameData.Hints = new();
            foreach (var hint in hints)
                GameData.AddHint(hint.ID, hint.Text, hint.IgnoreRemainingText);

            Patcher.Logger.LogInfo("Death Link: " + DeathLink);
            Patcher.Logger.LogInfo("Locked Explore Abilities: " + Enum.GetName(typeof(ExploreAbilityLockType), ExploreAbilityLock));
            Patcher.Logger.LogInfo("Eggsanity: " + Eggsanity);
            Patcher.Logger.LogInfo("Exp Multiplier: " + ExpMultiplier);
            Patcher.Logger.LogInfo("Include Chaos Relics: " + IncludeChaosRelics);
            Patcher.Logger.LogInfo("Force Egg Drop: " + AlwaysGetEgg);
            Patcher.Logger.LogInfo("Add Smoke Bombs: " + AddSmokeBombs);
            Patcher.Logger.LogInfo("Starting Gold: " + StartingGold * 100);
            Patcher.Logger.LogInfo("Monster Shift Rule: " + Enum.GetName(typeof(ShiftFlag), MonsterShiftRule));
            Patcher.Logger.LogInfo("Locked Doors: " + Enum.GetName(typeof(LockedDoorsFlag), LockedDoors));
            Patcher.Logger.LogInfo("Skip Plot: " + SkipPlot);
            Patcher.Logger.LogInfo("Open Blue Caves: " + OpenBlueCaves);
            Patcher.Logger.LogInfo("Open Stronghold Dungeon: " + Enum.GetName(typeof(OpenWorldSetting), OpenStrongholdDungeon));
            Patcher.Logger.LogInfo("Open Snowy Peaks: " + OpenSnowyPeaks);
            Patcher.Logger.LogInfo("Open Ancient Woods: " + OpenAncientWoods);
            Patcher.Logger.LogInfo("Open Sun Palace: " + Enum.GetName(typeof(OpenWorldSetting), OpenSunPalace));
            Patcher.Logger.LogInfo("Open Horizon Beach: " + Enum.GetName(typeof(OpenWorldSetting), OpenHorizonBeach));
            Patcher.Logger.LogInfo("Open Magma Chamber: " + Enum.GetName(typeof(OpenWorldSetting), OpenMagmaChamber));
            Patcher.Logger.LogInfo("Open Open Blurg: " + Enum.GetName(typeof(OpenWorldSetting), OpenBlobBurg));
            Patcher.Logger.LogInfo("Open Forgotten World: " + Enum.GetName(typeof(OpenWorldSetting), OpenForgottenWorld));
            Patcher.Logger.LogInfo("Open Underworld: " + Enum.GetName(typeof(OpenWorldSetting), OpenUnderworld));
            Patcher.Logger.LogInfo("Open Mystical Workshop: " + Enum.GetName(typeof(OpenWorldSetting), OpenMysticalWorkshop));
            Patcher.Logger.LogInfo("Open Abandoned Tower: " + Enum.GetName(typeof(OpenWorldSetting), OpenAbandonedTower));
            Patcher.Logger.LogInfo("Randomize Shop Prices: " + (GameData.ShopPrices.Count() > 0));
            Patcher.Logger.LogInfo("Shops Ignore Rank Requirement: " + ShopsIgnoreRank);
            Patcher.Logger.LogInfo("Monster Locations: " + GameData.MonstersCache.Count());
            Patcher.Logger.LogInfo("Champions: " + GameData.ChampionScenes.Count());
            Patcher.Logger.LogInfo("Item Locations: " + GameData.ItemChecks.Count());
            Patcher.Logger.LogInfo("Shop Locations: " + GameData.ShopChecks.Count());
            Patcher.Logger.LogInfo("Hints: " + hints.Count());

            // Lastly, we update tooltips for explore items that are not new items
            Patcher.UpdateExploreItemTooltips();
        }

        private static string GetStringData(Dictionary<string, object> data, string key)
        {
            if (data.ContainsKey(key))
                return data[key].ToString();
            return null;
        }

        private static int GetIntData(Dictionary<string, object> data, string key, int defaultValue)
        {
            var str = GetStringData(data, key);
            if (str == null)
                return defaultValue;
            return int.Parse(str);            
        }

        private static bool GetBoolData(Dictionary<string, object> data, string key, bool defaultValue)
        {
            var value = GetStringData(data, key);
            if (value == null)
                return defaultValue;
            return int.Parse(value) == 1;
        }

        private static T GetEnumData<T>(Dictionary<string, object> data, string key, T defaultValue) where T: Enum
        {
            var intFlag = GetIntData(data, key, -1);
            if (intFlag == -1)
                return defaultValue;

            return (T)(object)intFlag;

        }

        private static Dictionary<string, T> GetDictionaryData<T>(Dictionary<string, object> data, string key)
        {
            if (data[key].ToString() != null)
                return JsonConvert.DeserializeObject<Dictionary<string, T>>(data[key].ToString());

            return new Dictionary<string, T>();
        }

        private static IList<T> GetListData<T>(Dictionary<string, object> data, string key) where T : class
        {
            if (data[key].ToString() != null)
            {
                return JsonConvert.DeserializeObject<List<T>>(data[key].ToString());
            }
            return new List<T>();
        }
    }
}
