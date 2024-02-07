using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Patchers
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

                if (SlotData.LockedDoors == LockedDoorsFlag.None 
                    || (SlotData.LockedDoors == LockedDoorsFlag.Minimal && !GameData.LockedDoors.Contains(id)))
                {
                    UnityEngine.Object.Destroy(__instance.gameObject);
                    return false;
                }

                return true;
            }
        }

    }
}
