using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(CombatController), "LoseCombat")]
        public static class CombatController_LoseCombat
        {
            private static void Postfix()
            {
                if (APState.IsConnected)
                    APState.SendDeathLink();
            }
        }

        [HarmonyPatch(typeof(CombatController), "WinCombat")]
        private static class CombatController_WinCombat
        {
            [UsedImplicitly]
            private static void Prefix(CombatController __instance)
            {
                if (!APState.IsConnected)
                    return;

                if (__instance.CurrentEncounter.IsKeeperBattle)
                    return;

                if (!__instance.CurrentEncounter.IsChampion)
                    return;

                // Right now, the champions in End of Time don't do anything
                // Also this prevents them from progressing the All Champions goal
                if (GameController.Instance.CurrentSceneName == "KeeperStronghold_EndOfTime")
                    return;

                // We only want to operate on champion encounters
                string locName = $"{GameController.Instance.CurrentSceneName}_Champion";
                if (!GameData.ChampionRankIds.ContainsKey(locName))
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return;
                }

                Persistence.AddChampionDefeated(GameController.Instance.CurrentSceneName);
                APState.CheckLocation(GameData.ChampionRankIds[locName]);
            }
        }
    }
}
