﻿using HarmonyLib;
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

                // Check off the two merchants with restock flags
                ProgressManager.Instance.SetBool("AlchemistShopRestocked", true, true);
                ProgressManager.Instance.SetBool("GoblinTraderRestock", true, true);

                return false;
            }
        }

        [HarmonyPatch(typeof(KeepersIntro), "Start")]
        private class KeepersIntro_Start
        {
            private static bool Prefix(ref KeepersIntro __instance)
            {
                if (!APState.IsConnected)
                    return true;

                // Regardless whether we're auto-selecting familar or not, we set these flags so we can skip the intro
                ProgressManager.Instance.SetBool("FinishedFirstEncounter", true, true);
                ProgressManager.Instance.SetBool("TriggerOnce17", true, true); // Skip post tutorial fight dialog
                ProgressManager.Instance.SetBool("TriggerOnce495", true, true); // Skip post familiar naming dialog
                ProgressManager.Instance.SetBool("TriggerOnce68", true, true); // Skip dialog after hatching first egg
                ProgressManager.Instance.SetBool("TriggerOnce143", true, true); // Skip post second fight dialog
                ProgressManager.Instance.SetBool("TriggerOnce1009", true, true); // Skip post second fight dialog

                ProgressManager.Instance.SetBool("TriggerOnce42700080", true, true); // Vertraag talking in Eternity's End
                ProgressManager.Instance.SetBool("EndOfTimeDoorActivated", true, true); // open the door to eternity's end

                SlotData.StartingFamiliar = 1; // Debugging

                Patcher.Logger.LogWarning(SlotData.StartingFamiliar);
                if (SlotData.StartingFamiliar < 0)
                    return true;

                if (ProgressManager.Instance.GetBool("FamiliarChoiceCompleted"))
                    return true;


                GameObject familiarPrefab = __instance.FamiliarButtons[SlotData.StartingFamiliar].FamiliarPrefab;
                Patcher.Logger.LogInfo("Starter: " + familiarPrefab.name);

                PlayerController.Instance.Monsters.Familiar = PlayerController.Instance.Monsters.AddMonsterByPrefab(familiarPrefab, EShift.Normal);
                PlayerController.Instance.Follower.Monster = PlayerController.Instance.Monsters.Familiar;

                ProgressManager.Instance.SetBool("StartFamiliarChoice");
                ProgressManager.Instance.SetBool("FamiliarChoiceCompleted");
                ProgressManager.Instance.SetBool("TriggerOnce0");
                ProgressManager.Instance.SetBool("TriggerOnce68");
                ProgressManager.Instance.SetBool("TriggerOnce491");

                return false;
            }

            private static void Postfix(KeepersIntro __instance)
            {
                if (!APState.IsConnected)
                    return;

                // If familiar is auto-selected, remove the buttons to select them
                if (SlotData.StartingFamiliar >= 0)
                {
                    foreach (Component familiarButton in __instance.FamiliarButtons)
                        familiarButton.gameObject.SetActive(false);

                    Traverse.Create(__instance).Method("Skip").GetValue();
                }
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

                Traverse.Create(__instance).Method("Skip").GetValue();

                return false;
            }
        }

        [HarmonyPatch(typeof(KeepersIntro), "HideOtherKeepers", new Type[] { typeof(bool) })]
        private class KeepersIntro_HideOtherKeepers
        {
            // To prevent the first encounter from double-spawning, we need to cut out the last section of this method.
            private static bool Prefix(KeepersIntro __instance, bool triggerTweens)
            {
                if (!APState.IsConnected)
                    return true;

                if (SlotData.StartingFamiliar < 0)
                    return true;

                Traverse.Create(__instance).Field("keepersHidden").SetValue(true);
                ProgressManager.Instance.SetBool("IntroPlayed");
                foreach (GameObject keeper in __instance.Keepers)
                    ColorTween.StartTween(keeper, GameDefines.Gray, GameDefines.GrayZeroA, 1f);

                foreach (GameObject familiar in __instance.Familiars)
                {
                    ColorTween.StartTween(familiar, GameDefines.Gray, GameDefines.GrayZeroA, 1f);
                    foreach (ParticleSystem componentsInChild in familiar.transform.GetComponentsInChildren<ParticleSystem>())
                        componentsInChild.Stop();
                }
                if (PlayerController.Instance.Monsters.Familiar.GetComponent<MonsterFamiliar>().FamiliarType == EFamiliar.Wolf)
                {
                    __instance.FamiliarBig.GetComponent<tk2dSprite>().SetSprite("Spectral Wolf");
                    __instance.FamiliarBig.transform.localPosition = Utils.VectorChangeY(__instance.FamiliarBig.transform.localPosition, -140f);
                    __instance.FamiliarBig.transform.localScale = new Vector3(1f, 1f, 1f);
                }
                else if (PlayerController.Instance.Monsters.Familiar.GetComponent<MonsterFamiliar>().FamiliarType == EFamiliar.Lion)
                {
                    __instance.FamiliarBig.GetComponent<tk2dSprite>().SetSprite("Spectral Lion");
                    __instance.FamiliarBig.transform.localPosition = Utils.VectorChangeY(__instance.FamiliarBig.transform.localPosition, -120f);
                    __instance.FamiliarBig.transform.localScale = new Vector3(-1f, 1f, 1f);
                }
                else if (PlayerController.Instance.Monsters.Familiar.GetComponent<MonsterFamiliar>().FamiliarType == EFamiliar.Eagle)
                {
                    __instance.FamiliarBig.GetComponent<tk2dSprite>().SetSprite("Spectral Eagle");
                    __instance.FamiliarBig.transform.localPosition = Utils.VectorChangeY(__instance.FamiliarBig.transform.localPosition, -45f);
                    __instance.FamiliarBig.transform.localScale = new Vector3(1f, 1f, 1f);
                }
                else
                {
                    __instance.FamiliarBig.GetComponent<tk2dSprite>().SetSprite("Spectral Toad");
                    __instance.FamiliarBig.transform.localPosition = Utils.VectorChangeY(__instance.FamiliarBig.transform.localPosition, -120f);
                    __instance.FamiliarBig.transform.localScale = new Vector3(-1f, 1f, 1f);
                }
                __instance.ProtagonistBig.GetComponent<tk2dSprite>().SetSprite(PlayerController.Instance.CharacterGender == ECharacterGender.Male ? "Characters_Male" : "Characters_Female");

                if (triggerTweens)
                {
                    PositionTween.StartTween(__instance.ProtagonistBig, __instance.ProtagonistBig.transform.localPosition, __instance.ProtagonistBig.transform.localPosition + Vector3.left * 220f, 0.5f, PositionTween.Type.Decelerated, 1f);
                    __instance.FamiliarBig.SetActive(true);
                    PositionTween.StartTween(__instance.FamiliarBig, __instance.FamiliarBig.transform.localPosition, __instance.FamiliarBig.transform.localPosition + Vector3.left * 220f, 0.5f, PositionTween.Type.Decelerated, 1.2f);
                    ColorTween.StartTween(__instance.FamiliarBig, GameDefines.Gray, GameDefines.GrayZeroA, 1f, delay: 14f);
                    ColorTween.StartTween(__instance.ProtagonistBig, GameDefines.Gray, GameDefines.GrayZeroA, 1f, delay: 14.5f);
                    ColorTween.StartTween(__instance.BlackLayer, GameDefines.Black, GameDefines.BlackZeroA, 1f, delay: 15f);

                    var namingStarted = Traverse.Create(__instance).Method("NamingStarted");
                    Timer.StartTimer(__instance.gameObject, 14f, new Timer.TimeoutFunction(() => namingStarted.GetValue()));
                }

                return false;
            }
        }

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
                    ProgressManager.Instance.SetBool(name, true);
                    __result = true;
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
                    return false;
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