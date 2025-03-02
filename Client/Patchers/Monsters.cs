using Archipelago.MonsterSanctuary.Client.AP;
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
    }
}
