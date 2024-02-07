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
                // new game file started, delete old files so we start fresh.
                Logger.LogWarning("New Save. Deleting item cache and checked locations");
                DeleteItemCache();
                DeleteLocationsChecked();
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
                if (APState.IsConnected && SlotData.SkipPlot && GameData.Plotless.Contains(name) && __result == false)
                {
                    ProgressManager.Instance.SetBool(name, true);
                    __result = true;
                }
            }
        }

        //[HarmonyPatch(typeof(ProgressManager), "SetBool")]
        //private class ProgressManager_SetBool
        //{
        //    private static void Prefix(string name, bool value)
        //    {
        //        Logger.LogInfo($"SetBool(): {name} => {value}");
        //    }
        //}

        private static bool SkipAction(ScriptNode scriptNode)
        {
            return true;
            //if (!APState.IsConnected)
            //    return true;
            //if (!SlotData.SkipPlot)
            //    return true;

            //Logger.LogWarning("Room in dict? " + GameData.PlotlessScriptNodes.ContainsKey(GameController.Instance.CurrentSceneName));
            //if (!GameData.PlotlessScriptNodes.ContainsKey(GameController.Instance.CurrentSceneName))
            //    return true;

            //Logger.LogWarning("Skipping script node '" + scriptNode.ID + "'? " + GameData.PlotlessScriptNodes[GameController.Instance.CurrentSceneName].Contains(scriptNode.ID));
            //bool skip = GameData.PlotlessScriptNodes[GameController.Instance.CurrentSceneName].Contains(scriptNode.ID);
            //if (skip)
            //    scriptNode.Finish();

            //return !skip;
        }

        [HarmonyPatch(typeof(DialogueAction), "StartNode")]
        private class DialogueAction_StartNode
        {
            private static bool Prefix(DialogueAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(PositionFollowerAction), "StartNode")]
        private class PositionFollowerAction_StartNode
        {
            private static bool Prefix(PositionFollowerAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(MovePlayerAction), "StartNode")]
        private class MovePlayerAction_StartNode
        {
            private static bool Prefix(MovePlayerAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(UpgradeAction), "StartNode")]
        private class UpgradeAction_StartNode
        {
            private static bool Prefix(UpgradeAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(SetVisibleAction), "StartNode")]
        private class SetVisibleAction_StartNode
        {
            private static bool Prefix(UpgradeAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(MoveAction), "StartNode")]
        private class MoveAction_StartNode
        {
            private static bool Prefix(UpgradeAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(SetDirectionAction), "StartNode")]
        private class SetDirectionAction_StartNode
        {
            private static bool Prefix(UpgradeAction __instance)
            {
                return SkipAction(__instance);
            }
        }

        [HarmonyPatch(typeof(WaitAction), "StartNode")]
        private class WaitAction_StartNode
        {
            private static bool Prefix(UpgradeAction __instance)
            {
                return SkipAction(__instance);
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