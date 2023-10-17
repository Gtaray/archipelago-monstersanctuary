using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
                var method = __instance.GetType().GetMethod("Skip", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(__instance, null);

                return false;
            }
        }

        [HarmonyPatch(typeof(ProgressManager), "SetBool")]
        private class ProgressManager_SetBool
        {
            private static void Prefix(string name, bool value)
            {
                Logger.LogInfo($"SetBool(): {name} => {value}");
            }
        }

        [HarmonyPatch(typeof(TouchTrigger), "Touch")]
        private class TouchTrigger_Touch
        {
            private static bool Prefix(TouchTrigger __instance)
            {
                if (!APState.IsConnected)
                    return true;

                if (GameController.Instance.CurrentSceneName == "MountainPath_North1" 
                    && __instance.ID == 18)
                {
                    // Skipping intro should disable the touch trigger in the first room
                    return !SlotData.SkipIntro;
                }

                return true;
            }
        }
    }
}