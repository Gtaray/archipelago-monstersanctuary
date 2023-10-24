using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public enum ShiftFlag
    {
        Never = 0,  // Monsters are never shifted
        Normal = 1, // Monsters only shift after sun palace
        Any = 2     // Monsters can be shifted any time
    }

    public class SlotData
    {
        public static int ExpMultiplier { get; set; } = 1;
        public static bool AlwaysGetEgg { get; set; } = false;
        public static ShiftFlag MonsterShiftRule { get; set; } = ShiftFlag.Normal;
        public static bool SkipIntro { get; set; } = false;
        public static bool SkipPlot { get; set; } = false;
        public static bool SkipKeeperBattles { get; set; } = false;

        public static void LoadSlotDataUpdated(Dictionary<string, object> slotData)
        {
            Patcher.Logger.LogInfo("SlotData: " + JsonConvert.SerializeObject(slotData));
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
            SkipKeeperBattles = int.Parse(slotData["skip_battles"].ToString()) == 1;
        }
    }
}
