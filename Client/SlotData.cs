using Newtonsoft.Json;
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
        public static int SpectralFamiliar { get; set; } = -1;
        public static int ExpMultiplier { get; set; } = 1;
        public static bool AlwaysGetEgg { get; set; } = false;
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static bool SkipIntro { get; set; } = false;
        public static bool SkipPlot { get; set; } = false;
        public static bool SkipKeeperBattles { get; set; } = false;
        public static LockedDoorsFlag LockedDoors { get; set; } = 0;

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            Patcher.Logger.LogInfo("SlotData: " + JsonConvert.SerializeObject(slotData));
            //SpectralFamiliar = int.Parse(slotData["starting_familiar"].ToString());
            ExpMultiplier = int.Parse(slotData["exp_multiplier"].ToString());
            AlwaysGetEgg = int.Parse(slotData["monsters_always_drop_egg"].ToString()) == 1;
            switch (int.Parse(slotData["monster_shift_rule"].ToString()))
            {
                case (0):
                    MonsterShiftRule = ShiftFlag.Never;
                    break;
                case (1):
                    MonsterShiftRule = ShiftFlag.Normal;
                    break;
                case (2):
                    MonsterShiftRule = ShiftFlag.Any;
                    break;
            }

            SkipIntro = int.Parse(slotData["skip_intro"].ToString()) == 1;
            SkipPlot = int.Parse(slotData["skip_plot"].ToString()) == 1;
            //SkipKeeperBattles = int.Parse(slotData["skip_battles"].ToString()) == 1;

            switch (int.Parse(slotData["remove_locked_doors"].ToString()))
            {
                case (0):
                    LockedDoors = LockedDoorsFlag.All;
                    break;
                case (1):
                    LockedDoors = LockedDoorsFlag.Minimal;
                    break;
                case (2):
                    LockedDoors = LockedDoorsFlag.None;
                    break;
            }

            var locations = JsonConvert.DeserializeObject<Dictionary<string, string>>(slotData["monster_locations"].ToString());
            foreach (var location in locations)
            {
                GameData.AddMonster(location.Key, location.Value);
            }
        }
    }
}
