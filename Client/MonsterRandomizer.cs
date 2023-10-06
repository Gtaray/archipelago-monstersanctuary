using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        // This dictionary is required to map game room names to AP location ids
        // because champion monsters have a visual element that isn't attached to an encounter id
        private static Dictionary<string, string> _champions = new Dictionary<string, string>();

        // Maps monster names from AP to Monster Sanctuary.
        // Only needed for monsters whose names have spaces or special characters
        private static Dictionary<string, string> _monsters = new Dictionary<string, string>();

        private static Dictionary<string, Tuple<GameObject, Monster>> MonstersCache = new Dictionary<string, Tuple<GameObject, Monster>>();

        public static void AddMonster(string locationId, string monsterName)
        {
            var gameObject = GetMonsterByName(monsterName);
            if (gameObject == null)
            {
                return;
            }
            var monster = gameObject.GetComponent<Monster>();
            if (MonstersCache.ContainsKey(locationId))
                return;

            MonstersCache.Add(locationId, new Tuple<GameObject, Monster>(gameObject, monster));
            // _log.LogInfo($"Adding monster to cache: {locationId}, {monsterName}");
        }

        private static GameObject GetMonsterByName(string name)
        {
            // AP fills empty monster slots with this item. We don't care about it here,
            // and the game will never query for it, so we can safely ignore them
            if (name == "Empty Slot")
                return null;

            if (_monsters.ContainsKey(name))
            {
                name = _monsters[name];
            }

            return GameController.Instance.WorldData.Referenceables
                    .Where(x => x?.gameObject.GetComponent<Monster>() != null)
                    .Select(x => x.gameObject)
                    .SingleOrDefault(mon => string.Equals(mon.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject GetReplacementMonster(string locationId)
        {
            locationId = GetMappedLocation(locationId);
            if (!MonstersCache.ContainsKey(locationId))
            {
                string monster = APState.CheckLocation(locationId);

                if (string.IsNullOrEmpty(monster))
                {
                    _log.LogWarning("Location was not found");
                    return null;
                }

                AddMonster(locationId, monster);
            }

            return MonstersCache[locationId].Item1;
        }

        #region Patches
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

                List<Monster> list = new List<Monster>();
                MonsterEncounter.EncounterConfig encounterConfig = encounter.DetermineEnemy();
                int num = Mathf.Min(3, PlayerController.Instance.Monsters.Active.Count + PlayerController.Instance.Monsters.Permadead.Count);
                int num2 = 0;
                int i = 0;
                foreach (GameObject gameObject in encounterConfig.Monster)
                {
                    if (num2 >= num)
                    {
                        break;
                    }
                    
                    // START NEW CODE
                    GameObject monsterPrefab = GetReplacementMonster($"{GameController.Instance.CurrentSceneName}_{encounter.ID}_{i}");
                    i++;
                    // Super gross, but we have to do this to access the protected method
                    var setup = (GameObject)(__instance.GetType().GetMethod("SetupEncounterConfigEnemy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { encounter, monsterPrefab }));
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
                            list[0].SetShift((EShift)encounterShiftData.Monster1Shift);
                            list[1].SetShift((EShift)encounterShiftData.Monster2Shift);
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
                            ProgressManager.Instance.AddRecentEncounter(GameController.Instance.CurrentSceneName, encounter.ID, list[0].Shift, list[1].Shift, (list.Count > 2) ? list[2].Shift : EShift.Normal);
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
                    APState.CheckLocation(GetMappedLocation($"{GameController.Instance.CurrentSceneName}_Champion"));
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
                    return true;

                if (GameController.Instance == null)
                {
                    return false;
                }

                string champion_id = $"{GameController.Instance.CurrentSceneName}_{__instance.name}";
                Monster component = __instance.OriginalMonster.GetComponent<Monster>();
                Monster monster = null;

                if (_champions.ContainsKey(champion_id))
                {
                    monster = GetReplacementMonster(_champions[champion_id]).GetComponent<Monster>();
                    _log.LogInfo("NPC monster found, replaced with " + monster);
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

        /// <summary>
        /// When a monster encounter spawns in, this function is called prior to Start()
        /// This does a location check for the mons in the encounter, and adds those mons to the cache dictionary
        /// Which is used by another function to actually do the replacement
        /// </summary>
        //[HarmonyPatch(typeof(MonsterEncounter), "Start")]
        //private static class MonsterEncounter_Start
        //{
        //    private static void Prefix(MonsterEncounter __instance)
        //    {
        //        if (__instance == null)
        //        {
        //            return;
        //        }

        //        if (__instance.PredefinedMonsters == null)
        //        {
        //            _log.LogWarning("Monsterencounter '" + __instance + ".PredefinedMonsters' was null");
        //            return;
        //        }

        //        if (__instance.PredefinedMonsters.Monster == null)
        //        {
        //            _log.LogWarning("Monsterencounter '" + __instance + ".PredefinedMonsters.Monster' was null");
        //            return;
        //        }
        //        if (!APState.IsConnected)
        //            return;

        //        if (GameController.Instance == null)
        //            _log.LogWarning("GameController.Instance was null");
        //        if (GameController.Instance.CurrentSceneName == null)
        //            _log.LogWarning("Current Scene Name was null");

        //        // get a list of all of the location names for __instance encounter
        //        string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
        //        List<string> monsterIds = new List<string>();
        //        for (int id = 0; id < __instance.PredefinedMonsters.Monster.Length; id++)
        //        {
        //            monsterIds.Add($"{locName}_{id}");
        //        }

        //        // Check all of the monster 'locations' in __instance encounter
        //        var monsterNames = APState.CheckLocation(GetMappedLocations(monsterIds));

        //        if (monsterNames == null)
        //        {
        //            return;
        //        }

        //        // Store monsters so that we don't have to make calls to AP every time the encounter loads
        //        for (int i = 0; i < monsterNames.Length; i++)
        //        {
        //            AddMonster(monsterIds[i], monsterNames[i]);
        //        }
        //    }
        //}

        /// <summary>
        /// I was running into issues where monsters would spawn in without their encounter
        /// and this was breaking their AI. So here we're just double checking that after being spawned in
        /// they have their encounter set. I suspect the reason for this is some async weirdness when checking
        /// locations, but I don't know for sure.
        /// </summary>
        //[HarmonyPatch(typeof(MonsterEncounter), "SetupEnemies")]
        //private static class MonsterEncounter_SetupEnemies
        //{
        //    private static void Postfix(MonsterEncounter __instance)
        //    {
        //        for (int i = 0; i < __instance.DeterminedEnemies.Count; i++)
        //        {
        //            Monster monster = __instance.DeterminedEnemies[i];
        //            if (monster.Encounter == null)
        //            {
        //                _log.LogWarning($"Encounter was null on {monster.Name}");
        //                monster.Encounter = __instance;
        //            }
        //        }
        //    }
        //}
        #endregion
    }
}
