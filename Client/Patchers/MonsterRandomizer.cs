﻿using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        /// <summary>
        /// This is specifically used to handle champion spawning, because of the way that we're handling monster replacement
        /// There is an issue where defeating a champion at one location will prevent the champion at its vanilla location from spawning
        /// becuase the game thinks that champion is defeated. So instead of checking if
        /// </summary>
        [HarmonyPatch(typeof(ProgressManager), "WasChampionKilled")]
        private static class ProgressManager_WasChampionKilled
        {
            private static void Postfix(ref bool __result, ProgressManager __instance, Monster champion)
            {
                if (!ApState.IsConnected)
                    return;

                if (!Champions.DoesSceneHaveAChampion(GameController.Instance.CurrentSceneName))
                    return;

                __result = ApData.IsChampionDefeated(GameController.Instance.CurrentSceneName);
            }
        }

        [HarmonyPatch(typeof(SkillManager), "GetChampionPassive")]
        private class SkillManager_GetChampionPassive
        {
            private static bool Prefix(ref SkillManager __instance, ref PassiveChampion __result, ref bool recursive, ref int monsterIndex)
            {
                if (!ApState.IsConnected)
                    return true;

                var monster = __instance.GetComponent<Monster>();

                if (GameStateManager.Instance.IsCombat() && CombatController.Instance.CurrentEncounter.DeterminedEnemies.Count > 1)
                {
                    if (monsterIndex == -1)
                    {
                        monsterIndex = monster.Index;
                    }
                    if (monsterIndex != 1)
                    {
                        __result = null;
                        return false;
                    }
                }
                
                if (!Champions.DoesSceneHaveAChampion(GameController.Instance.CurrentSceneName))
                {
                    return true;
                }

                string originalChampName = Champions.GetOriginalChampionForScene(GameController.Instance.CurrentSceneName);
                if (string.IsNullOrEmpty(originalChampName))
                    return true;

                var champ = Monsters.GetMonsterByName(originalChampName);
                if (champ == null)
                    return true;

                var skillManager = champ.GetComponent<SkillManager>();

                foreach (GameObject gameObject in skillManager.ChampionSkills)
                {
                    PassiveChampion component = gameObject.GetComponent<PassiveChampion>();
                    if (component != null)
                    {
                        __result = component;
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(GameModeManager), "GetReverseReplacement")]
        private class GameModeManager_GetReverseReplacement
        {
            [UsedImplicitly]
            private static bool Prefix(ref Monster __result, ref Monster monster)
            {
                if (!ApState.IsConnected)
                    return true;

                // If the monster we're checking for does not have an encounter, then we 
                // default to the original function
                if (Monsters.IsMonsterCached(GameController.Instance.CurrentSceneName, monster.Encounter?.ID, monster.Index))
                {
                    return true;
                }

                // If this monster is randomized, then we simply return the new monster and treat
                // it as if it were the original
                __result = monster;
                return false;
            }
        }

        /// <summary>
        /// Sets up a monster encounter. We replace the whole function because of the GetReplacementMonster call in the middle that we need 
        /// to modify so it handles our own custom replacement.
        /// </summary>
        [HarmonyPatch(typeof(CombatController), "SetupEncounterConfigEnemies")]
        private class CombatController_SetupEncounterConfigEnemies
        {
            [UsedImplicitly]
            private static bool Prefix(ref List<Monster> __result, CombatController __instance, MonsterEncounter encounter, bool isChampion)
            {
                if (!ApState.IsConnected)
                    return true;

                // Do not randomize the infinity arena
                if (encounter.IsInfinityArena)
                    return true;

                // TODO: This doesn't seem to be changing the level of super champions
                // Need to double check this.
                if (isChampion)
                {
                    encounter.PredefinedMonsters.level = ((PlayerController.Instance.Minimap.CurrentEntry.EncounterLevel != 0)
                        ? PlayerController.Instance.Minimap.CurrentEntry.EncounterLevel
                        : PlayerController.Instance.CurrentSpawnLevel);
                }

                MonsterEncounter.EncounterConfig encounterConfig = encounter.DetermineEnemy();

                // Replace the monsters in encounterConfig. We do this outside of the foreach loop below because if a 1 monster champion fight is replaced with a 3 monster champion fight
                // it breaks things really bad (there's only one monster in encounterConfig.Monster, so only the first replacement is ever applied).
                List<GameObject> replacementMonsters = new();
                for (int i = 0; i < 3; i++)
                {
                    
                    GameObject monsterPrefab = Monsters.GetReplacementMonster(GameController.Instance.CurrentSceneName, encounter.ID, i);
                    if (monsterPrefab != null)
                    {
                        replacementMonsters.Add(monsterPrefab);
                    }
                }
                if (replacementMonsters.Count() == 0)
                {
                    Patcher.Logger.LogError($"Failed to build encounter; no data was found for '{GameController.Instance.CurrentSceneName}_{encounter.ID}'");
                    return true; // Let the original encounter spawn
                }
                encounterConfig.Monster = replacementMonsters.ToArray();

                List<Monster> list = new();
                int num = Mathf.Min(3, PlayerController.Instance.Monsters.Active.Count + PlayerController.Instance.Monsters.Permadead.Count);
                int num2 = 0;
                foreach (GameObject gameObject in encounterConfig.Monster)
                {
                    if (num2 >= num)
                    {
                        break;
                    }

                    // START NEW CODE
                    // Super gross, but we have to do this to access the protected method
                    var setup = (GameObject)(__instance.GetType().GetMethod("SetupEncounterConfigEnemy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { encounter, gameObject }));
                    Monster component = setup.GetComponent<Monster>();
                    // END NEW CODE

                    list.Add(component);
                    if (isChampion)
                    {
                        component.SkillManager.LearnChampionSkills(encounter, encounterConfig.Monster.Length == 1 || num2 == 1);
                    }
                    num2++;
                }

                if (list.Count() == 0)
                {
                    Patcher.Logger.LogWarning($"Ended up with an encounter that has zero monsters. Scene: {GameController.Instance.CurrentSceneName}, ID: {encounter.ID}");
                    return true;
                }

                if (SlotData.MonsterShiftRule != ShiftFlag.Never && (ProgressManager.Instance.GetBool("SanctuaryShifted") || SlotData.MonsterShiftRule == ShiftFlag.Any))
                {
                    if (!isChampion && encounter.IsNormalEncounter)
                    {
                        if (ProgressManager.Instance.GetRecentEncounter(GameController.Instance.CurrentSceneName, encounter.ID, out EncounterShiftData encounterShiftData))
                        {
                            if (list.Count > 0)
                            {
                                list[0].SetShift((EShift)encounterShiftData.Monster1Shift);
                            }
                            if (list.Count > 1)
                            {
                                list[1].SetShift((EShift)encounterShiftData.Monster2Shift);
                            }
                            if (list.Count > 2)
                            {
                                list[2].SetShift((EShift)encounterShiftData.Monster3Shift);
                            }
                        }
                        else
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < 0.25f)
                            {
                                int index = UnityEngine.Random.Range(0, list.Count);
                                bool @bool = ProgressManager.Instance.GetBool("LastMonsterShifted");
                                EShift shift = EShift.Light + Convert.ToInt32(@bool);
                                list[index].SetShift(shift);
                                ProgressManager.Instance.SetBool("LastMonsterShifted", !@bool, true);
                            }

                            ProgressManager.Instance.AddRecentEncounter(
                                GameController.Instance.CurrentSceneName,
                                encounter.ID,
                                (list.Count > 0) ? list[0].Shift : EShift.Normal,
                                (list.Count > 1) ? list[1].Shift : EShift.Normal,
                                (list.Count > 2) ? list[2].Shift : EShift.Normal);
                        }
                    }
                }
                __result = list;
                return false;
            }
        }

        /// <summary>
        /// Used by non-encounter game objects such as champions or decoration mobs
        /// Since we want champions to actually show the sprite of the monster they're randomized to, we do that here.
        /// The reason we have to pull in the entire function is because the GetReplacementMonster call in the middle needs to be swapped out
        /// And there's custom logic for handling decoration mons
        /// </summary>
        [HarmonyPatch(typeof(RandomizerMonsterReplacer), "Awake")]
        private class RandomizerMonsterReplacer_Awake
        {
            [UsedImplicitly]
            private static bool Prefix(RandomizerMonsterReplacer __instance)
            {
                if (!ApState.IsConnected)
                {
                    return true;
                }

                if (GameController.Instance == null)
                {
                    Patcher.Logger.LogWarning("GameController.Instance was null");
                    return true;
                }

                Monster component = __instance.OriginalMonster.GetComponent<Monster>();
                Monster monster = null;

                var npcLocationName = Monsters.GetNpcLocationName(GameController.Instance.CurrentSceneName, __instance.name);
                if (!string.IsNullOrEmpty(npcLocationName))
                {
                    monster = Monsters.GetReplacementMonster(npcLocationName).GetComponent<Monster>();
                }
                else if (__instance.name == "SkorchNPC" && !string.IsNullOrEmpty(SlotData.BexMonster))
                {
                    monster = Monsters.GetMonsterByName(SlotData.BexMonster).GetComponent<Monster>();
                }
                else
                {
                    Patcher.Logger.LogWarning("Didn't find champion data for " + __instance.name);
                }

                if (monster == null)
                {
                    if (GameModeManager.Instance.RandomizerMode || (GameModeManager.Instance.BraveryMode && __instance.isScorch))
                    {
                        component = __instance.OriginalMonster.GetComponent<Monster>();
                        if (GameModeManager.Instance.BraveryMode && __instance.isScorch)
                        {
                            monster = GameModeManager.Instance.BexMonster;
                        }
                        else
                        {
                            monster = GameModeManager.Instance.GetReplacementMonster(component);
                        }
                    }
                    else
                    {
                        monster = component;
                    }
                }

                if (monster != component)
                {
                    SpriteAnimator component2 = __instance.GetComponent<SpriteAnimator>();
                    component2?.Initialize();
                    ParticleSystem[] componentsInChildren = __instance.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        componentsInChildren[i].gameObject.SetActive(false);
                    }
                    JumpingMonster component3 = __instance.GetComponent<JumpingMonster>();
                    if (component3 != null)
                    {
                        component3.enabled = false;
                    }

                    tk2dBaseSprite component4 = monster.GetComponent<tk2dBaseSprite>();
                    MonsterVisuals monsterVisuals = __instance.GetComponent<MonsterVisuals>() ?? __instance.gameObject.AddComponent<MonsterVisuals>();

                    if (__instance.GetComponent<FamiliarNPC>() != null && ProgressManager.Instance.GetBool("SanctuaryShifted") && __instance.GetComponent<FamiliarNPC>().Shift != EShift.Normal)
                    {
                        __instance.GetComponent<MonsterVisuals>().ShiftOverride = __instance.GetComponent<FamiliarNPC>().Shift;
                    }
                    tk2dSpriteAnimator component5 = __instance.GetComponent<tk2dSpriteAnimator>();
                    string text = component5.DefaultClip.name;
                    if (text == "inactive")
                    {
                        text = "idle";
                    }
                    monsterVisuals.Init(monster, component5, false);
                    __instance.GetComponent<tk2dBaseSprite>().SetSprite(component4.Collection, component4.spriteId);
                    component5.Library = monster.GetComponent<tk2dSpriteAnimator>().Library;
                    component5.playAutomatically = false;
                    component5.Play(text);
                    RaycastHit2D raycastHit2D = Utils.CheckForGround(__instance.transform.position, 0f, 256f);
                    if (raycastHit2D.collider != null)
                    {
                        Vector3 vector = raycastHit2D.point;
                        vector.z = __instance.transform.position.z;
                        vector.y += (float)Mathf.RoundToInt(__instance.GetComponent<tk2dBaseSprite>().GetUntrimmedBounds().size.y / 2f);
                        if (monster.IsFlying())
                        {
                            vector.y += monster.FlyingOffset;
                        }
                        __instance.transform.position = vector;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Because we randomize monsters within AP, this method is only used to get the Tanuki monster replacement
        /// </summary>
        [HarmonyPatch(typeof(GameModeManager), "GetReplacementMonster", new Type[] { typeof(GameObject) })]
        private class GameModeManager_GetReplacementMonster
        {
            [UsedImplicitly]
            private static void Postfix(GameModeManager __instance, GameObject monster, ref GameObject __result)
            {
                if (!ApState.IsConnected)
                {
                    return;
                }
                var mon = monster.GetComponent<Monster>();
                
                if (mon.Name == "Tanuki")
                {
                    __result = Monsters.GetMonsterByName(SlotData.TanukiMonster);
                }
                else if (mon.Name == "Skorch")
                {
                    __result = Monsters.GetMonsterByName(SlotData.BexMonster);
                }

                // Might need to add shockhopper cryomancer mon here
            }
        }
    }
}
