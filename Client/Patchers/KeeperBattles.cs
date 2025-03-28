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
        [HarmonyPatch(typeof(StartCombatAction), "StartNode")]
        private static class StartCombatAction_StartNode
        {
            private static bool Prefix(StartCombatAction __instance, ref bool ___wonCombat)
            {
                if (!ApState.IsConnected)
                    return true;
                if (!SlotData.SkipBattles)
                    return true;

                if (GameController.Instance.CurrentSceneName == "KeeperStronghold_EndOfTime")
                    return true;

                if (__instance.MonsterEncounter.name == "BlobEncounter")
                    return true;
                if (__instance.MonsterEncounter.name == "MechGolemEncounter")
                    return true;

                ___wonCombat = true;
                var inst = Traverse.Create(__instance);
                inst.Method("StartCinematic").GetValue();
                __instance.Finish();
                //__instance.Skip();

                return false;
            }
        }
    }
}
