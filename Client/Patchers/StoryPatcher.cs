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
        private class GameController_LoadStartingArea
        {
            [UsedImplicitly]
            private static bool Prefix(GameController __instance, bool isNewGamePlus)
            {
                string startingScene = "MountainPath_Intro";

                if (APState.IsConnected)
                {
                    // new game file started, delete old files so we start fresh.
                    // This could cause problems, deleting only if we're connected
                    // but the other option is to delete even when not connected, and that will break
                    // if someone wants to do a normal run while also doing a rando.
                    Logger.LogWarning("New Save. Deleting item cache and checked locations");
                    DeleteItemCache();
                    DeleteLocationsChecked();
                    APState.Resync();

                    // if we're not skipping the intro, call the original function
                    if (!SlotData.SkipIntro)
                        return true;

                    startingScene = "MountainPath_North1";
                }

                // We have to duplicate the original code here so we can change the current scene name
                __instance.IsStoryMode = true;
                if (!isNewGamePlus)
                {
                    __instance.InitPlayerStartSetup();
                }

                __instance.ChangeType = GameController.SceneChangeType.ToStartScene;
                __instance.CurrentSceneName = startingScene;
                SceneManager.LoadScene(__instance.CurrentSceneName, LoadSceneMode.Additive);
                PlayerController.Instance.TimerAvailable = true;
                // End of original code

                // Do this after InitPlayerStartSetup()
                if (SlotData.AddSmokeBombs)
                    PlayerController.Instance.Inventory.AddItem(GameData.GetItemByName("Smoke Bomb"), 50, 0);
                PlayerController.Instance.Gold = SlotData.StartingGold * 100;

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
                        ProgressManager.Instance.SetBool("TrevisanQuestAazerach", true, true);
                    }
                    return true;
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
    }
}