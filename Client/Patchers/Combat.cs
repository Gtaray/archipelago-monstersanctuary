using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Persistence;
using HarmonyLib;
using JetBrains.Annotations;
namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(CombatController), "LoseCombat")]
        public static class CombatController_LoseCombat
        {
            private static void Postfix()
            {
                if (ApState.IsConnected)
                    ApState.SendDeathLink();
            }
        }

        [HarmonyPatch(typeof(CombatController), "WinCombat")]
        private static class CombatController_WinCombat
        {
            [UsedImplicitly]
            private static void Prefix(CombatController __instance)
            {
                if (!ApState.IsConnected)
                    return;

                if (__instance.CurrentEncounter.IsKeeperBattle)
                    return;

                if (!__instance.CurrentEncounter.IsChampion)
                    return;

                // if victory condition is to beat the mad lord, check to see if we've done that
                if (SlotData.Goal == CompletionEvent.MadLord && GameController.Instance.CurrentSceneName == "AbandonedTower_Final")
                {
                    ApState.CompleteGame();
                }

                // We only want to operate on champion encounters
                string locName = $"{GameController.Instance.CurrentSceneName}_Champion";
                var locationId = Champions.GetChampionRankLocationId(locName);
                if (locationId == null)
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return;
                }

                ApData.AddChampionAsDefeated(GameController.Instance.CurrentSceneName);
                ApState.CheckLocation(locationId.Value);
            }
        }
    }
}
