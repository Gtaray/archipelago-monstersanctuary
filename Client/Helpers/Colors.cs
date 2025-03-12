using Archipelago.MonsterSanctuary.Client.AP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Helpers
{
    public class Colors
    {
        public static string Self => "00ffff"; // Cyan
        public static string OtherPlayer => "add8e6"; // Light blue
        public static string ProgressionItem => "ff00ff"; // Magenta
        public static string UsefulItem => "00ff00"; // Lime green
        public static string FillerItem => "ffbb00"; // Orange
        public static string TrapItem => "d02c00"; // Red

        public static string GetItemColor(ItemClassification classification)
        {
            if (classification == ItemClassification.Progression)
            {
                return ProgressionItem;
            }
            else if (classification == ItemClassification.Useful)
            {
                return UsefulItem;
            }
            else if (classification == ItemClassification.Trap)
            {
                return TrapItem;
            }

            return FillerItem;
        }
    }
}
