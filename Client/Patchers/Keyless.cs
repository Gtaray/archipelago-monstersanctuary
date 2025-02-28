using Archipelago.MonsterSanctuary.Client.AP;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using UnityEngine;

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
                if (!ApState.IsConnected)
                    return true;

                string id = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                bool isMinimal = World.IsLockedDoorMinimal(id);

                Patcher.Logger.LogDebug("CheckIfDoorWasOpen");
                Patcher.Logger.LogDebug("Flag: " + Enum.GetName(typeof(LockedDoorsFlag), SlotData.LockedDoors));
                Patcher.Logger.LogDebug("Is minimal door: " + isMinimal);

                // Remove the locked door if either ALL locked doors are removed, or there's only minimal doors and this isn't a minimal door
                if (SlotData.LockedDoors == LockedDoorsFlag.None 
                    || (SlotData.LockedDoors == LockedDoorsFlag.Minimal && !isMinimal))
                {
                    GameObject.Destroy(__instance.gameObject);
                    return false;
                }

                return true;
            }
        }
    }
}
