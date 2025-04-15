using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Helpers;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using JetBrains.Annotations;
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
        private static void AddMissingRewardsToMonsters()
        {
            var eggs = GameController.Instance.WorldData.Referenceables
                .Where(go => go != null && go.gameObject != null && go.gameObject.HasComponent<Egg>())
                .Select(go => go.GetComponent<Egg>());

            foreach (var egg in eggs)
            {
                var monster = egg.Monster.GetComponent<Monster>();

                // Don't add eggs for evolved monsters. Maybe one day this will be an option.
                if (monster.IsEvolved)
                    continue;

                if (!monster.RewardsRare.Contains(egg.gameObject))
                {
                    monster.RewardsRare.Add(egg.gameObject);
                }

                var filler = GetItemByName("Level Badge");
                while (monster.RewardsRare.Count() < 3)
                {
                    monster.RewardsRare.Add(filler.gameObject);
                }
            }

            var catalysts = GameController.Instance.WorldData.Referenceables
                .Where(go => go != null && go.gameObject != null && go.gameObject.HasComponent<Catalyst>())
                .Select(go => go.GetComponent<Catalyst>());

            foreach (var catalyst in catalysts)
            {
                var monster = catalyst.EvolveMonster.GetComponent<Monster>();

                // Don't add catalysts for unevolved monsters
                if (!monster.IsEvolved)
                    continue;

                if (!monster.RewardsRare.Contains(catalyst.gameObject))
                {
                    monster.RewardsRare.Add(catalyst.gameObject);
                }
            }

            System.Random r = new System.Random();
            var monsters = GameController.Instance.WorldData.Referenceables
                .Where(go => go != null && go.gameObject != null && go.gameObject.HasComponent<Monster>())
                .Select(go => go.GetComponent<Monster>());
            var food = GameController.Instance.WorldData.Referenceables
                .Where(go => go != null && go.gameObject != null && go.gameObject.HasComponent<Food>())
                .Select(go => go.gameObject)
                .ToArray();
            var potion = GetItemByName("Small Potion");
            foreach (var monster in monsters)
            {
                if (monster.RewardsCommon.Count() == 0)
                {
                    monster.RewardsCommon.Add(potion.gameObject);
                }

                while (monster.RewardsCommon.Count() < 3)
                {
                    monster.RewardsCommon.Add(food[r.Next(food.Count())]);
                }
            }
        }

        #region Monsters and abilities to Data Storage
        [HarmonyPatch(typeof(MonsterManager), "AddMonsterByPrefab")]
        private class MonsterManager_AddMonsterByPrefab
        {
            [UsedImplicitly]
            private static void Postfix(GameObject monsterPrefab, bool loadingSaveGame)
            {

                if (!ApState.IsConnected)
                    return;

                // If we're loading a save game, don't check any locations
                if (loadingSaveGame)
                    return;

                Task.Run(() =>
                {
                    AddMonsterToDataStorage(monsterPrefab);
                    AddAbilityToDataStorage(monsterPrefab);
                });
            }
        }

        internal static void AddAbilityToDataStorage(GameObject monsterObj)
        {
            if (!ApState.IsConnected)
                return;

            var monster = monsterObj.GetComponent<Monster>();
            if (monster == null)
            {
                Patcher.Logger.LogWarning($"No monster component found for game object '{monsterObj.name}'");
                return;
            }

            var ability = monster.ExploreAction.GetComponent<ExploreAbility>();
            if (ability == null)
            {
                Patcher.Logger.LogError($"{monster.Name} has a null ExploreAbility component");
                return;
            }

            if (ApState.ReadBoolFromDataStorage(ability.Name) == false)
            {
                ApState.SetToDataStorage(ability.Name, (DataStorageElement)true);
            }
        }

        internal static void AddMonsterToDataStorage(GameObject monsterObj)
        {
            if (!ApState.IsConnected)
                return;

            var monster = monsterObj.GetComponent<Monster>();
            if (monster == null)
            {
                Patcher.Logger.LogWarning($"No monster component found for game object '{monsterObj.name}'");
                return;
            }

            if (ApState.ReadBoolFromDataStorage(monster.Name) == false)
            {
                ApState.SetToDataStorage(monster.Name, (DataStorageElement)true);
            }
        }
        #endregion
    }
}
