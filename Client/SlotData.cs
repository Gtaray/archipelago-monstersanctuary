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
        Champions = 1
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

        public static CompletionEvent Goal { get; set; } = CompletionEvent.MadLord;
        public static int ExpMultiplier { get; set; } = 1;
        public static bool AlwaysGetEgg { get; set; } = false;
        public static bool SkipIntro { get; set; } = false;
        public static bool SkipPlot { get; set; } = false;
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static LockedDoorsFlag LockedDoors { get; set; } = 0;
        public static bool DeathLink { get; set; } = false;
        public static string TanukiMonster { get; set; }
        public static string BexMonster{ get; set; }

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            var options = GetDictionaryData<object>(slotData, "options");
            Goal = GetEnumData(options, "goal", CompletionEvent.MadLord);
            ExpMultiplier = GetIntData(options, "exp_multiplier", 1);
            AlwaysGetEgg = GetBoolData(options, "monsters_always_drop_egg", false);
            SkipIntro = GetBoolData(options, "skip_intro", false);
            SkipPlot = GetBoolData(options, "skip_plot", false);
            MonsterShiftRule = GetEnumData(options, "monster_shift_rule", ShiftFlag.Normal);
            LockedDoors = GetEnumData(options, "remove_locked_doors", LockedDoorsFlag.All);
            DeathLink = GetBoolData(options, "death_link", false);

            var monsterData = GetDictionaryData<object>(slotData, "monsters");
            TanukiMonster = GetStringData(monsterData, "tanuki");
            BexMonster = GetStringData(monsterData, "bex_monster");

            GameData.MonstersCache = new();
            var monsterLocations = GetDictionaryData<string>(monsterData, "monster_locations");
            foreach (var location in monsterLocations)
                GameData.AddMonster(location.Key, location.Value);

            GameData.ChampionScenes = new();
            GameData.ChampionScenes = GetDictionaryData<string>(monsterData, "champions");

            var itemLocations = GetDictionaryData<Dictionary<string, long>>(slotData, "locations");

            //  Have to do this first so we have a list of all rank item ids before we process the rest of the items
            GameData.ChampionRankIds = new();
            foreach (var item in itemLocations["ranks"])
            {
                GameData.ChampionRankIds.Add(item.Key, item.Value);
            }

            GameData.ItemChecks = new();
            GameData.NumberOfChecks = new();
            foreach (var locationGroup in itemLocations)
            {
                // Each entry in itemLocations is a key value pair
                // where the key is the area name, and the value is a dictionary of all item checks in that area
                foreach (var check in locationGroup.Value)
                {
                    if (!GameData.ChampionRankIds.ContainsKey(check.Key))
                        GameData.AddItemCheck(check.Key, check.Value, locationGroup.Key);
                }
            }

            var hints = GetListData<HintData>(slotData, "hints");
            GameData.Hints = new();
            foreach (var hint in hints)
                GameData.AddHint(hint.ID, hint.Text, hint.IgnoreRemainingText);

            Patcher.Logger.LogInfo("Death Link: " + DeathLink);
            Patcher.Logger.LogInfo("Exp Multiplier: " + ExpMultiplier);
            Patcher.Logger.LogInfo("Force Egg Drop: " + AlwaysGetEgg);
            Patcher.Logger.LogInfo("Monster Shift Rule: " + Enum.GetName(typeof(ShiftFlag), MonsterShiftRule));
            Patcher.Logger.LogInfo("Locked Doors: " + Enum.GetName(typeof(LockedDoorsFlag), LockedDoors));
            Patcher.Logger.LogInfo("Skip Intro: " + SkipIntro);
            Patcher.Logger.LogInfo("Skip Plot: " + SkipPlot);
            Patcher.Logger.LogInfo("Monster Locations: " + GameData.MonstersCache.Count());
            Patcher.Logger.LogInfo("Champions: " + GameData.ChampionScenes.Count());
            Patcher.Logger.LogInfo("Item Locations: " + GameData.ItemChecks.Count());
            Patcher.Logger.LogInfo("Hints: " + hints.Count());
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
