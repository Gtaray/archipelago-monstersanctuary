using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Helpers;
using Archipelago.MonsterSanctuary.Client.Options;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private const int _threeSkillTreePercentageChance = 25;

        private static bool _skillDataInitialized = false;
        private static HashSet<SkillTree> _skillTrees = new();
        private static HashSet<GameObject> _ultimates = new();
        private static HashSet<GameObject> _lightShift = new();
        private static HashSet<GameObject> _darkShift = new();

        private static System.Random _skillTreeRng;

        [HarmonyPatch(typeof(PlayerController), "LoadGame")]
        internal class PlayerController_LoadGame
        {
            private static void Prefix()
            {
                if (!ApState.IsConnected)
                    return;

                InitializeSkillTreeData();
                RandomzeAllMonsterSkillData();
            }
        }

        [HarmonyPatch(typeof(GameController), "InitPlayerStartSetup")]
        internal class GameController_InitPlayerStartSetup
        {
            private static void Prefix()
            {
                if (!ApState.IsConnected)
                    return;

                InitializeSkillTreeData();
                RandomzeAllMonsterSkillData();
            }
        }

        // Only want to do this once, the first time we load the game, and never again
        // This is because its a reference, and needs to remain unchanged to we can reference it for every load.
        public static void InitializeSkillTreeData()
        {
            _skillTreeRng = new System.Random(ApState.Seed.GetHashCode());

            if (_skillDataInitialized)
                return;

            foreach (var obj in GameController.Instance.WorldData.Referenceables)
            {
                if (obj == null || obj.gameObject == null)
                    continue;

                var go = obj.gameObject;
                if (!go.HasComponent<Monster>())
                    continue;

                if (go.HasComponent<SkillTree>())
                {
                    foreach (var skilltree in go.GetComponents<SkillTree>())
                    {
                        _skillTrees.Add(DuplicateComponentType(skilltree));
                    }
                }

                if (go.HasComponent<SkillManager>())
                {
                    var skillManager = go.GetComponent<SkillManager>();
                    skillManager.Ultimates.ForEach(u => _ultimates.Add(u));
                    _darkShift.Add(skillManager.DarkSkill);
                    _lightShift.Add(skillManager.LightSkill);
                }
            }

            _skillDataInitialized = true;
        }

        private static void RandomzeAllMonsterSkillData()
        {
            if (!ApState.IsConnected)
                return;

            var monsters = GameController.Instance.WorldData.Referenceables.Where(go => go != null && go.gameObject != null && go.gameObject.HasComponent<Monster>());
            foreach (var monster in monsters)
            {
                if (SlotData.RandomizeMonsterSkillTress)
                    RandomizeSkillTreesForMonster(monster.gameObject);

                if (SlotData.RandomizeMonsterUltimates)
                    RandomizeUltimatesForMonster(monster.gameObject);

                if (SlotData.RandomizeMonsterShiftSkills)
                    RandomizeShiftSkillsForMonster(monster.gameObject);
            }
        }

        public static void RandomizeSkillTreesForMonster(GameObject monster)
        {
            if (!ApState.IsConnected)
                return;

            if (!monster.HasComponent<Monster>())
                return;

            if (!monster.HasComponent<SkillTree>())
                return;

            // Get rid of old skill tree components
            foreach (var tree in monster.GetComponents<SkillTree>())
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }

            int numberOfTrees = _skillTreeRng.Next(1, 101) <= _threeSkillTreePercentageChance
                ? 3
                : 4;

            var skillManager = monster.GetComponent<SkillManager>();
            var monsterComp = monster.GetComponent<Monster>();
            skillManager.BaseSkills.RemoveAll(s => s.HasComponent<BaseAction>());
            List<SkillTree> skillTreesAdded = new();

            for (int i = 0; i < numberOfTrees; i++)
            {
                var possibleTrees = _skillTrees.Where(tree => !HasBaseSkill(skillManager, tree)).ToList();
                var actualTree = DuplicateComponentType(possibleTrees[_skillTreeRng.Next(possibleTrees.Count())]);

                skillTreesAdded.Add(actualTree);
                CopyComponentToGameObject(actualTree, monster);
            }

            // Randomly pick trees to start off with skill points in.
            var skillCount = monsterComp.IsSpectralFamiliar ? 2 : 1;
            for (int i = 0; i <= skillCount; i++)
            {
                var tree = skillTreesAdded[_skillTreeRng.Next(skillTreesAdded.Count())];

                GameObject skill = null;
                if (tree.Tier1Skills.Any())
                    skill = tree.Tier1Skills[_skillTreeRng.Next(tree.Tier1Skills.Count())];

                else if (tree.Tier2Skills.Any())
                    skill = tree.Tier2Skills[_skillTreeRng.Next(tree.Tier2Skills.Count())];

                if (skill != null)
                {
                    skillManager.BaseSkills.Add(skill);
                    skillTreesAdded.Remove(tree);
                }
            }
        }

        public static void RandomizeUltimatesForMonster(GameObject monster)
        {
            if (!ApState.IsConnected)
                return;

            if (!monster.HasComponent<Monster>())
                return;

            if (!monster.HasComponent<SkillManager>())
                return;

            var skill_manager = monster.GetComponent<SkillManager>();
            skill_manager.Ultimates = new List<GameObject>();

            while (skill_manager.Ultimates.Count() < 3)
            {

                var picks = _ultimates.Except(skill_manager.Ultimates).ToList();
                skill_manager.Ultimates.Add(picks[_skillTreeRng.Next(picks.Count())]);
            }
        }

        public static void RandomizeShiftSkillsForMonster(GameObject monster)
        {
            if (!ApState.IsConnected)
                return;

            if (!monster.HasComponent<Monster>())
                return;

            if (!monster.HasComponent<SkillManager>())
                return;

            var skill_manager = monster.GetComponent<SkillManager>();

            var darkpicks = _darkShift.ToList();
            skill_manager.DarkSkill = darkpicks[_skillTreeRng.Next(darkpicks.Count())];

            var lightpicks = _lightShift.ToList();
            skill_manager.LightSkill = lightpicks[_skillTreeRng.Next(lightpicks.Count())];
        }

        private static bool HasBaseSkill(SkillManager skillManager, SkillTree tree)
        {
            // First check if our base skills collide with the tree's tier 1 skills
            if (skillManager.BaseSkills.Intersect(tree.Tier1Skills).Any())
                return true;
            // Second, if there are no tier 1 skills for this tree, see if our base skills collide with the tree's tier 2 skills
            return !tree.Tier1Skills.Any() && skillManager.BaseSkills.Intersect(tree.Tier2Skills).Any();
        }

        private static T CopyComponentToGameObject<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        private static T DuplicateComponentType<T>(T original) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = (T)Activator.CreateInstance(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }
    }
}
