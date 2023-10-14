using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        #region Patches
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
                string monsterName = champion.GetName();
                var monster = GameData.GetReplacementChampion(monsterName);

                for (int i = 0; i < __instance.ChampionScores.Count; i++)
                {
                    if (__instance.ChampionScores[i].ChampionId == monster.ID)
                    {
                        __result = true;
                        return;
                    }
                }
                
                // If we got down here, then we didn't find any scores for the randomized champ
                // which means we haven't fought it yet.
                __result = false;
            }

            [HarmonyPatch(typeof(SkillManager), "GetChampionPassive")]
            private class SkillManager_GetChampionPassive
            {
                [UsedImplicitly]
                private static void Postfix(ref PassiveChampion __result, SkillManager __instance, bool recursive)
                {
                    if (recursive)
                        return;
                    if (GameController.Instance == null)
                        return;
                    if (GameController.Instance.CurrentSceneName == null)
                        return;

                    var go = GameData.GetReverseChampionReplacement(GameController.Instance.CurrentSceneName);
                    if (go == null)
                        return;

                    var monster = go.GetComponent<Monster>();
                    __result = go
                        .GetComponent<SkillManager>()
                        .GetChampionPassive(true, monster.Index);
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
                    if (!APState.IsConnected)
                        return true;

                    Logger.LogInfo("SetupEncounterConfigEnemies()");

                    encounter.VariableLevel = true; // We force this to true so that super champions aren't locked to level 42
                    MonsterEncounter.EncounterConfig encounterConfig = encounter.DetermineEnemy();

                    // Replace the monsters in encounterConfig. We do this outside of the foreach loop below because if a 1 monster champion fight is replaced with a 3 monster champion fight
                    // it breaks things really bad (there's only one monster in encounterConfig.Monster, so only the first replacement is ever applied).
                    List<GameObject> replacementMonsters = new List<GameObject>();
                    for (int i = 0; i < 3; i++)
                    {
                        GameObject monsterPrefab = GameData.GetReplacementMonster($"{GameController.Instance.CurrentSceneName}_{encounter.ID}_{i}");
                        if (monsterPrefab != null)
                        {
                            replacementMonsters.Add(monsterPrefab);
                        }
                    }
                    if (replacementMonsters.Count() == 0)
                    {
                        Logger.LogError($"Failed to build encounter; no data was found for '{GameController.Instance.CurrentSceneName}_{encounter.ID}'");
                        return true; // Let the original encounter spawn
                    }
                    encounterConfig.Monster = replacementMonsters.ToArray();

                    List<Monster> list = new List<Monster>();
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

                    // Eventually want this to include rando settings so things can be shifted any time
                    if (ProgressManager.Instance.GetBool("SanctuaryShifted"))
                    {
                        if (!isChampion && encounter.IsNormalEncounter)
                        {
                            EncounterShiftData encounterShiftData;
                            if (ProgressManager.Instance.GetRecentEncounter(GameController.Instance.CurrentSceneName, encounter.ID, out encounterShiftData))
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

                        // Infinity arena stuff. This will probably remain untested for a long long while, and could possibly break things.
                        else if (encounter.IsInfinityArena)
                        {
                            if (encounter.PredefinedMonsters.level >= 160)
                            {
                                using (List<Monster>.Enumerator enumerator = list.GetEnumerator())
                                {
                                    while (enumerator.MoveNext())
                                    {
                                        Monster monster2 = enumerator.Current;
                                        __instance.GetType().GetMethod("SetupInfinityArenaMonsterShift", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { monster2 });
                                    }
                                    __result = list;
                                    return false;
                                }
                            }
                            if (encounter.PredefinedMonsters.level >= 130)
                            {
                                int num3 = UnityEngine.Random.Range(0, 3);
                                for (int j = 0; j < 3; j++)
                                {
                                    if (j != num3)
                                    {
                                        __instance.GetType().GetMethod("SetupInfinityArenaMonsterShift", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { list[j] });
                                    }
                                }
                            }
                            else if (encounter.PredefinedMonsters.level >= 70)
                            {
                                int index2 = UnityEngine.Random.Range(0, 3);
                                __instance.GetType().GetMethod("SetupInfinityArenaMonsterShift", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { list[index2] });
                            }
                        }
                    }
                    __result = list;
                    return false;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            [HarmonyPatch(typeof(CombatController), "WinCombat")]
            private static class CombatController_WinCombat
            {
                [UsedImplicitly]
                private static void Prefix(CombatController __instance)
                {
                    if (!APState.IsConnected)
                        return;

                    // We only want to operate on champion encounters
                    if (__instance.CurrentEncounter.IsChampion)
                    {
                        APState.CheckLocation(GameData.GetMappedLocation($"{GameController.Instance.CurrentSceneName}_Champion"));
                    }
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
                    if (!APState.IsConnected)
                    {
                        Logger.LogInfo("Not connected");
                        return true;
                    }

                    if (GameController.Instance == null)
                    {
                        Logger.LogWarning("GameController.Instance was null");
                        return true;
                    }

                    string champion_id = $"{GameController.Instance.CurrentSceneName}_{__instance.name}";
                    Monster component = __instance.OriginalMonster.GetComponent<Monster>();
                    Monster monster = null;


                    if (GameData.NPCs.ContainsKey(champion_id))
                    {
                        monster = GameData.GetReplacementMonster(GameData.NPCs[champion_id]).GetComponent<Monster>();
                    }
                    else
                    {
                        Logger.LogWarning("Didn't find champion data for " + __instance.name);
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
                        if (component2 != null)
                        {
                            component2.Initialize();
                        }
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
                        MonsterVisuals monsterVisuals = __instance.GetComponent<MonsterVisuals>();
                        if (monsterVisuals == null)
                        {
                            monsterVisuals = __instance.gameObject.AddComponent<MonsterVisuals>();
                        }
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
            #endregion
        }
    }
}
