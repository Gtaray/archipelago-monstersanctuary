﻿using Archipelago.MonsterSanctuary.Client.AP;
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

namespace Archipelago.MonsterSanctuary.Client.Options
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
        Champions = 1
    }

    public enum OpenWorldSetting
    {
        Closed = 0,
        Entrances = 1,
        Interior = 2,
        Full = 3
    }

    public enum EquipmentAutoScaler
    {
        Never = 0,
        Level = 1,
        Rank = 2,
        Map = 3
    }

    public enum LockedExploreAbilities
    {
        Off = 0,
        Type = 1,
        Ability = 2,
        Specie = 3,
        Progression = 4,
        Combo = 5,
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
        public static int SpectralFamiliar { get; set; } = -1;
        public static bool SkipKeeperBattles { get; set; } = false;
        // END UNUSED

        public static string Version { get; set; }

        public static CompletionEvent Goal { get; set; } = CompletionEvent.MadLord;
        public static bool DeathLink { get; set; } = false;
        public static int ChampionsNeededToGetKeyOfPower { get; set; } = 0;

        public static bool SkipPlot { get; set; } = false;
        public static bool SkipBattles { get; set; } = false;
        //public static bool SolveTediousPuzzles { get; set; } = true;
        public static LockedDoorsFlag LockedDoors { get; set; } = 0;
        public static bool OpenBlueCaves { get; set; } = false;
        public static OpenWorldSetting OpenStrongholdDungeon { get; set; } = OpenWorldSetting.Closed;
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

        public static bool AlwaysGetEgg { get; set; } = false;
        public static bool AlwaysGetCatalyst { get; set; } = false;
        public static string TanukiMonster { get; set; }
        public static string BexMonster { get; set; }

        public static LockedExploreAbilities LockedExploreAbilities { get; set; }
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static bool RandomizeMonsterSkillTress { get; set; } = false;
        public static bool RandomizeMonsterUltimates { get; set; } = false;
        public static bool RandomizeMonsterShiftSkills { get; set; } = false;

        public static bool AddSmokeBombs { get; set; }
        public static int StartingGold { get; set; }
        public static bool IncludeChaosRelics { get; set; }
        public static EquipmentAutoScaler AutoScaleEquipment { get; set; }

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            var options = GetDictionaryData<object>(slotData, "options");

            Version = GetStringData(slotData, "version");
            Patcher.Logger.LogInfo("AP World Version: " + Version);

            Goal = GetEnumData(options, "goal", CompletionEvent.MadLord);
            Patcher.Logger.LogInfo("Goal: " + Enum.GetName(typeof(CompletionEvent), Goal));

            ChampionsNeededToGetKeyOfPower = GetIntData(options, "key_of_power_champion_unlock", 0);
            Patcher.Logger.LogInfo("Champions Needed for Key of Power: " + ChampionsNeededToGetKeyOfPower);

            SkipPlot = GetBoolData(options, "skip_plot", false);
            Patcher.Logger.LogInfo("Skip Plot: " + SkipPlot);

            SkipBattles = GetBoolData(options, "skip_keeper_battles", false);
            Patcher.Logger.LogInfo("Skip Keeper Battles: " + SkipBattles);

            LockedDoors = GetEnumData(options, "remove_locked_doors", LockedDoorsFlag.All);
            Patcher.Logger.LogInfo("Locked Doors: " + Enum.GetName(typeof(LockedDoorsFlag), LockedDoors));

            OpenBlueCaves = GetBoolData(options, "open_blue_caves", false);
            Patcher.Logger.LogInfo("Open Blue Caves: " + OpenBlueCaves);

            OpenStrongholdDungeon = GetEnumData(options, "open_stronghold_dungeon", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Stronghold Dungeon: " + Enum.GetName(typeof(OpenWorldSetting), OpenStrongholdDungeon));

            OpenSnowyPeaks = GetBoolData(options, "open_snowy_peaks", false);
            Patcher.Logger.LogInfo("Open Snowy Peaks: " + OpenSnowyPeaks);

            OpenAncientWoods = GetBoolData(options, "open_ancient_woods", false);
            Patcher.Logger.LogInfo("Open Ancient Woods: " + OpenAncientWoods);

            OpenSunPalace = GetEnumData(options, "open_sun_palace", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Sun Palace: " + Enum.GetName(typeof(OpenWorldSetting), OpenSunPalace));

            OpenHorizonBeach = GetEnumData(options, "open_horizon_beach", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Horizon Beach: " + Enum.GetName(typeof(OpenWorldSetting), OpenHorizonBeach));

            OpenMagmaChamber = GetEnumData(options, "open_magma_chamber", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Magma Chamber: " + Enum.GetName(typeof(OpenWorldSetting), OpenMagmaChamber));

            OpenBlobBurg = GetEnumData(options, "open_blob_burg", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Open Blurg: " + Enum.GetName(typeof(OpenWorldSetting), OpenBlobBurg));

            OpenForgottenWorld = GetEnumData(options, "open_forgotten_world", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Forgotten World: " + Enum.GetName(typeof(OpenWorldSetting), OpenForgottenWorld));

            OpenMysticalWorkshop = GetBoolData(options, "open_mystical_workshop", false);
            Patcher.Logger.LogInfo("Open Mystical Workshop: " + Enum.GetName(typeof(OpenWorldSetting), OpenMysticalWorkshop));

            OpenUnderworld = GetEnumData(options, "open_underworld", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Underworld: " + Enum.GetName(typeof(OpenWorldSetting), OpenUnderworld));

            OpenAbandonedTower = GetEnumData(options, "open_abandoned_tower", OpenWorldSetting.Closed);
            Patcher.Logger.LogInfo("Open Abandoned Tower: " + Enum.GetName(typeof(OpenWorldSetting), OpenAbandonedTower));

            AlwaysGetEgg = GetBoolData(options, "monsters_always_drop_egg", false);
            Patcher.Logger.LogInfo("Always Drop Egg: " + AlwaysGetEgg);

            AlwaysGetCatalyst = GetBoolData(options, "monsters_always_drop_catalyst", false);
            Patcher.Logger.LogInfo("Always Drop Catalyst: " + AlwaysGetCatalyst);

            MonsterShiftRule = GetEnumData(options, "monster_shift_rule", ShiftFlag.Normal);
            Patcher.Logger.LogInfo("Monster Shift Rule: " + Enum.GetName(typeof(ShiftFlag), MonsterShiftRule));

            RandomizeMonsterSkillTress = GetBoolData(options, "randomize_monster_skill_trees", false);
            Patcher.Logger.LogInfo("Randomize Skill Trees: " +  RandomizeMonsterSkillTress);

            RandomizeMonsterUltimates = GetBoolData(options, "randomize_monster_ultimates", false);
            Patcher.Logger.LogInfo("Randomize Ultimates: " + RandomizeMonsterUltimates);

            RandomizeMonsterShiftSkills = GetBoolData(options, "randomize_monster_shift_skills", false);
            Patcher.Logger.LogInfo("Randomize Shift Skills: " + RandomizeMonsterShiftSkills);

            LockedExploreAbilities = GetEnumData(options, "lock_explore_abilities", LockedExploreAbilities.Off);
            Patcher.Logger.LogInfo("Locked Explore Abilities: " + Enum.GetName(typeof(LockedExploreAbilities), LockedExploreAbilities));

            AutoScaleEquipment = GetEnumData(options, "automatically_scale_equipment", EquipmentAutoScaler.Never);
            Patcher.Logger.LogInfo("Automatically Scale Equipment: " +  Enum.GetName(typeof(EquipmentAutoScaler), AutoScaleEquipment));

            IncludeChaosRelics = GetBoolData(options, "include_chaos_relics", false);
            Patcher.Logger.LogInfo("Include Chaos Relics: " + IncludeChaosRelics);

            AddSmokeBombs = GetBoolData(options, "add_smoke_bombs", false);
            Patcher.Logger.LogInfo("Add Smoke Bombs: " + AddSmokeBombs);

            StartingGold = GetIntData(options, "starting_gold", 1);
            Patcher.Logger.LogInfo("Starting Gold: " + StartingGold * 100);

            var monsterData = GetDictionaryData<object>(slotData, "monsters");
            TanukiMonster = GetStringData(monsterData, "tanuki");
            BexMonster = GetStringData(monsterData, "bex_monster");

            Monsters.ClearApData();
            var monsterLocations = GetDictionaryData<string>(monsterData, "monster_locations");
            foreach (var location in monsterLocations)
                Monsters.AddMonster(location.Key, location.Value);

            Champions.ClearApData();
            foreach (var kvp in GetDictionaryData<string>(monsterData, "champions"))
            {
                Champions.AddChampionScene(kvp.Key, kvp.Value);
            }

            Locations.KeyOfPowerUnlockLocation = GetLongData(slotData, "key_of_power_champion_unlock", 0);

            var itemLocations = GetDictionaryData<Dictionary<string, long>>(slotData, "locations");

            //  Have to do this first so we have a list of all rank item ids before we process the rest of the items
            foreach (var item in itemLocations["ranks"])
            {
                Champions.AddChampionRank(item.Key, item.Value);
            }

            Locations.ClearApData();
            foreach (var locationGroup in itemLocations)
            {
                // Each entry in itemLocations is a key value pair
                // where the key is the area name, and the value is a dictionary of all item checks in that area
                foreach (var check in locationGroup.Value)
                {
                    if (!Champions.IsLocationAChampionRank(check.Key))
                        Locations.AddLocation(check.Key, check.Value, locationGroup.Key);
                }
            }

            var hints = GetListData<HintData>(slotData, "hints");
            Hints.ClearApData();
            foreach (var hint in hints)
                Hints.AddHint(hint.ID, hint.Text, hint.IgnoreRemainingText);

            Patcher.Logger.LogInfo("Force Egg Drop: " + AlwaysGetEgg);
            Patcher.Logger.LogInfo("Monster Shift Rule: " + Enum.GetName(typeof(ShiftFlag), MonsterShiftRule));
            Patcher.Logger.LogInfo("Locked Doors: " + Enum.GetName(typeof(LockedDoorsFlag), LockedDoors));
            Patcher.Logger.LogInfo("Skip Plot: " + SkipPlot);
            Patcher.Logger.LogInfo("Monster Locations: " + Monsters.MonstersCache.Count());
            Patcher.Logger.LogInfo("Champions: " + Champions.ReplacedChampions.Count());
            Patcher.Logger.LogInfo("Item Locations: " + Locations.IdToName.Count());
            Patcher.Logger.LogInfo("Hints: " + hints.Count());
        }

        private static string GetStringData(Dictionary<string, object> data, string key)
        {
            if (data.ContainsKey(key))
                return data[key].ToString();
            return null;
        }

        private static long GetLongData(Dictionary<string, object> data, string key, long defaultValue)
        {
            var str = GetStringData(data, key);
            if (str == null)
                return defaultValue;
            return long.Parse(str);
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
            if (data.ContainsKey(key) && data[key] is bool)
                return (bool)data[key];

            var value = GetStringData(data, key);
            if (value == null)
                return defaultValue;
            return int.Parse(value) == 1;
        }

        private static T GetEnumData<T>(Dictionary<string, object> data, string key, T defaultValue) where T : Enum
        {
            var intFlag = GetIntData(data, key, -1);
            if (intFlag == -1)
                return defaultValue;

            return (T)(object)intFlag;

        }

        private static Dictionary<string, T> GetDictionaryData<T>(Dictionary<string, object> data, string key)
        {
            if (data.ContainsKey(key) && data[key].ToString() != null)
                return JsonConvert.DeserializeObject<Dictionary<string, T>>(data[key].ToString());

            return new Dictionary<string, T>();
        }

        private static IList<T> GetListData<T>(Dictionary<string, object> data, string key) where T : class
        {
            if (data.ContainsKey(key) && data[key].ToString() != null)
            {
                return JsonConvert.DeserializeObject<List<T>>(data[key].ToString());
            }
            return new List<T>();
        }
    }
}
