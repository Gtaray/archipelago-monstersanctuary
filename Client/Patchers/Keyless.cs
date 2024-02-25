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
        [HarmonyPatch(typeof(Door), "CheckIfDoorWasOpened")]
        private class Door_CheckIfDoorWasOpened
        {
            [UsedImplicitly]
            private static bool Prefix(ref Door __instance)
            {
                if (!APState.IsConnected)
                    return true;

                string id = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";

                Patcher.Logger.LogInfo("CheckIfDoorWasOpen");
                Patcher.Logger.LogInfo("Flag: " + Enum.GetName(typeof(LockedDoorsFlag), SlotData.LockedDoors));
                Patcher.Logger.LogInfo("Is minimal door: " + GameData.LockedDoors.Contains(id));
                if (SlotData.LockedDoors == LockedDoorsFlag.None 
                    || (SlotData.LockedDoors == LockedDoorsFlag.Minimal && !GameData.LockedDoors.Contains(id)))
                {
                    Destroy(__instance.gameObject);
                    return false;
                }

                return true;
            }
        }

    }
}
