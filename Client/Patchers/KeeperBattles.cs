using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Options;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static List<int> _combatsToSkip = new()
        {
            603, // Will in duelist circle
            14200051, // Vallalar in Stronghold Dungeon
            20800047, // Zosimos in Sun Palace
            8500058, // Ostanes in Ancient Woods
            24200047, // Julia in Horizon Beach
            27700018, // Rhazes in magma chamber
            29300053, // Eric in Blue Caves
            29000018, // Leonard in Underworld
            35700015, // Will outside Abandoned Tower
            38300069, // Vallalar and Ostanes in Abandoned Tower
            38800016, // Zosimos in Abandoned Tower
            39700003, // Chimes in Abandoned Tower
            39800029, // Marduk
            2100033, // Old Buran's blob battle
            27900016, // Bex in Magma Chamber
            45100098, // Wanderer in Forgotten World
        };

        [HarmonyPatch(typeof(StartCombatAction), "StartNode")]
        private static class StartCombatAction_StartNode
        {
            private static bool Prefix(StartCombatAction __instance, ref bool ___wonCombat)
            {
                if (!ApState.IsConnected)
                    return true;
                if (!SlotData.SkipBattles)
                    return true;
                if (!_combatsToSkip.Contains(__instance.ID))
                    return true;

                ___wonCombat = true;
                var inst = Traverse.Create(__instance);
                inst.Method("StartCinematic").GetValue();
                __instance.Finish();

                return false;
            }
        }
    }
}
