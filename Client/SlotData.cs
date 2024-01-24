using Newtonsoft
    .Json;
using Newtonsoft.Json.Linq;
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

    public class SlotData
    {
        // UNUSED
        public static int SpectralFamiliar { get; set; } = -1;
        public static bool SkipKeeperBattles { get; set; } = false;
        // END UNUSED

        public static int ExpMultiplier { get; set; } = 1;
        public static bool AlwaysGetEgg { get; set; } = false;
        public static bool SkipIntro { get; set; } = false;
        public static bool SkipPlot { get; set; } = false;
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static LockedDoorsFlag LockedDoors { get; set; } = 0;
        public static string Tanuki_Monster { get; set; }
        public static string Bex_Monster{ get; set; }
        public static string Caretaker_Monster{ get; set; }

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            ExpMultiplier = GetIntData(slotData, "exp_multiplier", 1);
            AlwaysGetEgg = GetBoolData(slotData, "monsters_always_drop_egg", false);
            SkipIntro = GetBoolData(slotData, "skip_intro", false);
            SkipPlot = GetBoolData(slotData, "skip_plot", false);
            MonsterShiftRule = GetShiftFlagData(slotData, "monster_shift_rule");
            LockedDoors = GetLockedDoorsData(slotData, "remove_locked_doors");
            Tanuki_Monster = GetStringData(slotData, "tanuki");
            Bex_Monster = GetStringData(slotData, "bex_monster");

            var locations = GetDictionaryData(slotData, "monster_locations");
            foreach (var location in locations)
            {
                GameData.AddMonster(location.Key, location.Value);
            }

            Patcher.Logger.LogInfo("Exp Multiplier: " + ExpMultiplier);
            Patcher.Logger.LogInfo("Force Egg Drop: " + AlwaysGetEgg);
            Patcher.Logger.LogInfo("Monster Shift Rule: " + Enum.GetName(typeof(ShiftFlag), MonsterShiftRule));
            Patcher.Logger.LogInfo("Locked Doors: " + Enum.GetName(typeof(LockedDoorsFlag), MonsterShiftRule));
            Patcher.Logger.LogInfo("Skip Intro: " + SkipIntro);
            Patcher.Logger.LogInfo("Skip Plot: " + SkipPlot);
            Patcher.Logger.LogInfo("Monster Locations: " + locations.Count());
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

        private static ShiftFlag GetShiftFlagData(Dictionary<string, object> data, string key)
        {
            var strFlag = GetStringData(data, key);
            if (strFlag == null)
                return ShiftFlag.Normal;

            var intFlag = int.Parse(strFlag);

            switch (intFlag)
            {
                case (0):
                    return ShiftFlag.Never;
                case (1):
                    return ShiftFlag.Normal;
                case (2):
                    return ShiftFlag.Any;
            }

            return ShiftFlag.Normal;
        }

        private static LockedDoorsFlag GetLockedDoorsData(Dictionary<string, object> data, string key)
        {
            var strFlag = GetStringData(data, key);
            if (strFlag == null)
                return LockedDoorsFlag.All;

            var intFlag = int.Parse(strFlag);

            switch (intFlag)
            {
                case (0):
                    return LockedDoorsFlag.All;
                case (1):
                    return LockedDoorsFlag.Minimal;
                case (2):
                    return LockedDoorsFlag.None;
            }

            return LockedDoorsFlag.All;
        }

        private static Dictionary<string, string> GetDictionaryData(Dictionary<string, object> data, string key)
        {
            if (data[key].ToString() != null)
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(data[key].ToString());

            return new Dictionary<string, string>();
        }
    }
}
