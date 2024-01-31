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
        [HarmonyPatch(typeof(CombatController), "LoseCombat")]
        public static class CombatController_LoseCombat
        {
            private static void Postfix()
            {
                APState.SendDeathLink();
            }
        }

        #region Retreating without smoke bombs
        //[HarmonyPatch(typeof(CombatController), "OnMenuItemSelect")]
        //public static class CombatController_OnMenuItemSelect
        //{
        //    private static bool Prefix(CombatController __instance, MenuListItem item)
        //    {
        //        BaseAction displayable = (BaseAction)item.Displayable;
        //        if (UIController.Instance.CombatUI.Menu.SelectedSwitchAction != null)
        //        {
        //            return true;
        //        }
        //        else
        //        {
        //            // Ignore anything that's not the retreat menu
        //            if (displayable != __instance.DefaultActions[5].GetComponent<BaseAction>())
        //            {
        //                return true;
        //            }

        //            CombatUIController.Instance.Menu.MenuList.SetLocked(true);
        //            PopupController.Instance.ShowRequest(
        //                Utils.LOCA("Retreat"), 
        //                Utils.LOCA("Retreat out of combat?"), 
        //                new PopupController.PopupDelegate(() =>
        //                {
        //                    CombatUIController.Instance.Menu.Close();
        //                    __instance.State.SetState(CombatController.CombatStates.Inactive);
        //                    AnimElement.PlayAnimElement(Prefabs.Instance.SmokeBomb, __instance.PlayerPos + Vector3.up * 16f, false, false);
        //                    Timer.StartTimer(__instance.gameObject, 0.9f, new Timer.TimeoutFunction(() =>
        //                    {
        //                        MethodInfo OnEndCombatStarted = typeof(CombatController).GetMethod("OnEndCombatStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        //                        OnEndCombatStarted.Invoke(__instance, null);
        //                        __instance.WonCombat = false;
        //                        __instance.RetreatedCombat = true;
        //                        CombatUIController.Instance.ComboView.Hide();
        //                        MethodInfo CleanUp = typeof(CombatController).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
        //                        CleanUp.Invoke(__instance, null);
        //                    }));
        //                }), 
        //                new PopupController.PopupDelegate(() =>
        //                {
        //                    CombatUIController.Instance.Menu.MenuList.SetLocked(false);
        //                }), 
        //                true);
        //            return false;
        //        }
        //    }
        //}

        //[HarmonyPatch(typeof(CombatController), "ConfirmedRetreat")]
        //public static class CombatController_ConfirmedRetreat
        //{
        //    private static bool Prefix(CombatController __instance)
        //    {
        //        // If we're using a smoke bomb from the item menu, we do the normal thing
        //        if (CombatUIController.Instance.ItemMenu.IsOpen)
        //        {
        //            return true;
        //        }

        //        // At this point, we know we selected retreat from the combat menu
        //        CombatUIController.Instance.StartMenu.Close();

        //        __instance.State.SetState(CombatController.CombatStates.Inactive);
        //        AnimElement.PlayAnimElement(Prefabs.Instance.SmokeBomb, __instance.PlayerPos + Vector3.up * 16f, false, false);
        //        Timer.StartTimer(__instance.gameObject, 0.9f, new Timer.TimeoutFunction(() =>
        //        {
        //            MethodInfo OnEndCombatStarted = typeof(CombatController).GetMethod("OnEndCombatStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        //            OnEndCombatStarted.Invoke(__instance, null);
        //            __instance.WonCombat = false;
        //            __instance.RetreatedCombat = true;
        //            CombatUIController.Instance.ComboView.Hide();
        //            MethodInfo CleanUp = typeof(CombatController).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
        //            CleanUp.Invoke(__instance, null);
        //        }));
        //        return false;
        //    }
        //}

        //[HarmonyPatch(typeof(CombatController), "RequestRetreat")]
        //public static class CombatController_RequestRetreat
        //{
        //    private static bool Prefix(CombatController __instance)
        //    {
        //        PopupController.Instance.ShowRequest(
        //            Utils.LOCA("Retreat?"), 
        //            Utils.LOCA("Retreat out of combat?"),
        //            new PopupController.PopupDelegate(() =>
        //            {
        //                CombatUIController.Instance.Menu.Close();
        //                __instance.State.SetState(CombatController.CombatStates.Inactive);
        //                AnimElement.PlayAnimElement(Prefabs.Instance.SmokeBomb, __instance.PlayerPos + Vector3.up * 16f, false, false);
        //                Timer.StartTimer(__instance.gameObject, 0.9f, new Timer.TimeoutFunction(() =>
        //                {
        //                    MethodInfo OnEndCombatStarted = typeof(CombatController).GetMethod("OnEndCombatStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        //                    OnEndCombatStarted.Invoke(__instance, null);
        //                    __instance.WonCombat = false;
        //                    __instance.RetreatedCombat = true;
        //                    CombatUIController.Instance.ComboView.Hide();
        //                    MethodInfo CleanUp = typeof(CombatController).GetMethod("CleanUp", BindingFlags.NonPublic | BindingFlags.Instance);
        //                    CleanUp.Invoke(__instance, null);
        //                }));
        //            }),
        //            new PopupController.PopupDelegate(() =>
        //            {
        //                CombatUIController.Instance.Menu.MenuList.SetLocked(false);
        //            }),
        //            true);
        //        return false;
        //    }
        //}

        //[HarmonyPatch(typeof(StartCombatMenu), "Open")]
        //public static class StartCombatMenu_Open
        //{
        //    private static void Postfix(StartCombatMenu __instance)
        //    {
        //        if (CombatController.Instance.CurrentEncounter.EncounterType != EEncounterType.InfinityArena)
        //        {
        //            __instance.BtnRetreat.Text.text = Utils.LOCA("Retreat");
        //            __instance.BtnRetreat.SetDisabled(!CombatController.Instance.CurrentEncounter.CanRetreat);
        //        }
        //    }
        //}
        #endregion
    }
}
