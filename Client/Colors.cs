using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    internal class Colors
    {
        public static string Self => "00ffff"; // Cyan
        public static string OtherPlayer => "add8e6"; // Light blue
        public static string ProgressionItem => "ff00ff"; // Magenta
        public static string UsefulItem => "00ff00"; // Lime green
        public static string FillerItem => "ffbb00"; // Orange
        public static string TrapItem => "d02c00"; // Red

        public static Color ProgressionItemColor => new Color(1, 0, 1); // Magenta
        public static Color UsefulItemColor => new Color(0, 1, 0); // Lime Green
        public static Color FillerItemColor => new Color(1, 0.733f, 0); // Orange
        public static Color TrapItemColor => new Color(0.816f, 0.173f, 0); // Red
    }
}
