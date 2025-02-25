using HarmonyLib;
using JetBrains.Annotations;
using System.Linq;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(GameController), "LoadStartingArea")]
        private class GameController_SkipStartingCinematic
        {
            [UsedImplicitly]
            private static bool Prefix(GameController __instance, bool isNewGamePlus)
            {
                // new game file started, delete old files so we start fresh.
                Patcher.Logger.LogWarning("New Save. Deleting item cache and checked locations");
                Persistence.DeleteFile();
                APState.Resync();

                // if we're not skipping the intro, call the original function
                if (!SlotData.SkipIntro)
                    return true;

                // We have to duplicate the original code here so we can change the current scene name
                __instance.IsStoryMode = true;
                if (!isNewGamePlus)
                {
                    __instance.InitPlayerStartSetup();
                }
                __instance.ChangeType = GameController.SceneChangeType.ToStartScene;
                __instance.CurrentSceneName = "MountainPath_North1";
                SceneManager.LoadScene(__instance.CurrentSceneName, LoadSceneMode.Additive);
                PlayerController.Instance.TimerAvailable = true;


                return false;
            }
        }

        [HarmonyPatch(typeof(KeepersIntro), "ShowKeepers")]
        private class KeepersIntro_ShowKeepers
        {
            [UsedImplicitly]
            private static bool Prefix(KeepersIntro __instance)
            {
                if (!APState.IsConnected)
                    return true;
                if (!SlotData.SkipIntro)
                    return true;

                ProgressManager.Instance.SetBool("FinishedFirstEncounter", true, true);
                ProgressManager.Instance.SetBool("TriggerOnce17", true, true); // Skip post tutorial fight dialog
                ProgressManager.Instance.SetBool("TriggerOnce495", true, true); // Skip post familiar naming dialog
                ProgressManager.Instance.SetBool("TriggerOnce68", true, true); // Skip dialog after hatching first egg
                ProgressManager.Instance.SetBool("TriggerOnce143", true, true); // Skip post second fight dialog
                ProgressManager.Instance.SetBool("TriggerOnce1009", true, true); // Skip post second fight dialog
                var method = __instance.GetType().GetMethod("Skip", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(__instance, null);

                return false;
            }
        }

        //[HarmonyPatch(typeof(KeepersIntro), "Start")]
        //private class KeepersIntro_Update
        //{
        //    private static void Prefix(ref KeepersIntro __instance)
        //    {
        //        SlotData.SpectralFamiliar = 0; // Debugging

        //        Patcher.Logger.LogWarning(SlotData.SpectralFamiliar);
        //        if (SlotData.SpectralFamiliar < 0)
        //            return;

        //        GameObject familiarPrefab = __instance.FamiliarButtons[SlotData.SpectralFamiliar].FamiliarPrefab;
        //        Patcher.Logger.LogInfo("Starter: " + familiarPrefab.name);

        //        PlayerController.Instance.Monsters.AddMonsterByPrefab(familiarPrefab, EShift.Normal);
        //        PlayerController.Instance.Follower.Monster = PlayerController.Instance.Monsters.Familiar;
        //        ProgressManager.Instance.SetBool("FamiliarChoiceCompleted");
        //        __instance.IntroScript.ClearImpulses();
        //    }
        //}

        [HarmonyPatch(typeof(ProgressManager), "GetBool")]
        private class ProgressManager_GetBool
        {
            private static void Postfix(ref bool __result, string name)
            {
                if (!APState.IsConnected)
                    return;

                if (name == "KeyOfPowerGained")
                {
                    __result = PlayerController.Instance.Inventory.Uniques.Any(i => i.GetName() == "Key of Power");
                    ProgressManager.Instance.SetBool(name, __result);
                    return;
                }

                if (name == "MozzieQuestStarted")
                {
                    __result = PlayerController.Instance.Inventory.HasUniqueItem(EUniqueItemId.Mozzie);
                    ProgressManager.Instance.SetBool(name, __result);
                    return;
                }

                if (name == "TrevisanQuestAazerach")
                {
                    __result = PlayerController.Instance.Inventory.HasUniqueItem(EUniqueItemId.Ahrimaaya);
                    ProgressManager.Instance.SetBool("TrevisanQuestAazerach", __result);
                    return;
                }

                if (SlotData.SkipPlot && GameData.Plotless.Contains(name) && __result == false)
                {
                    ProgressManager.Instance.SetBool(name, true);
                    __result = true;
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(ItemCondition), "EvaluateCondition")]
        private class ItemCondition_EvaluateCondition
        {
            private static void Prefix(ItemCondition __instance)
            {
                // if checking how many sanctuary tokens we have, we modify the compare value to be 5
                // This way the cut-scene will only trigger if all 5 sanctuary tokens are already gathered
                if (__instance.ID == 29300015)
                {
                    __instance.CompareValue = 5;
                }
            }
        }

        [HarmonyPatch(typeof(TouchTrigger), "Touch")]
        private class TouchTrigger_Touch
        {
            private static bool Prefix(TouchTrigger __instance)
            {
                if (!APState.IsConnected)
                    return true;

                if (GameController.Instance.CurrentSceneName == "StrongholdDungeon_SummonRoom")
                {
                    // if mad lord is the current goal, then we allow aazerach to be fought before postgame
                    if (SlotData.Goal == CompletionEvent.MadLord)
                    {
                        return ProgressManager.Instance.GetBool("TrevisanQuestAazerach");
                    }
                }
                if (GameController.Instance.CurrentSceneName == "MountainPath_North1" 
                    && __instance.ID == 18)
                {
                    // Skipping intro should disable the touch trigger in the first room
                    return !SlotData.SkipIntro;
                }

                if (GameController.Instance.CurrentSceneName == "MountainPath_North5"
                    && __instance.ID == 86)
                {
                    return !SlotData.SkipPlot;
                }
                if (GameController.Instance.CurrentSceneName == "KeeperStronghold_KeepersTower"
                    && __instance.ID == 578)
                {
                    return !SlotData.SkipPlot;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(BoolSwitchLever), "Interact")]
        private class BoolSwitchLever_Interact
        {
            private static bool Prefix(BoolSwitchLever __instance)
            {
                if (!APState.IsConnected)
                    return true;

                
                // Lowers sun palace water by the first stage
                if (__instance.BoolSwitchName == "SunPalaceWaterSwitch1")
                {
                    if (!ProgressManager.Instance.GetBool("SunPalaceTowerSwitch1Completed"))
                    {
                        ShowPlayerWarningMessage();
                        return false;
                    }
                }
                // Raises the sun palace tower by the second stage
                if (__instance.BoolSwitchName == "SunPalaceTowerSwitch2")
                {
                    if (!ProgressManager.Instance.GetBool("SunPalaceWaterSwitch1Completed"))
                    {
                        ShowPlayerWarningMessage();
                        return false;
                    }
                }
                // Lowers sun palace water by the second stage
                if (__instance.BoolSwitchName == "SunPalaceWaterSwitch2")
                {
                    if (!ProgressManager.Instance.GetBool("SunPalaceTowerSwitch2Completed"))
                    {
                        ShowPlayerWarningMessage();
                        return false;
                    }
                }
                // Raise the sun palace center by its third stage
                if (__instance.BoolSwitchName == "SunPalaceTowerSwitch3")
                {
                    if (!ProgressManager.Instance.GetBool("SunPalaceWaterSwitch2Completed"))
                    {
                        ShowPlayerWarningMessage();
                        return false;
                    }
                }

                return true;
            }

            private static void ShowPlayerWarningMessage()
            {
                PopupController.Instance.ShowMessage("Warning", "This lever doesn't appear to work right now. Looks like you need to do something else first.");
            }
        }
    }
}