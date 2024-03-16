﻿using HarmonyLib
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
        [HarmonyPatch(typeof(CombatController), "WinCombat")]
        private static class MadLordGoal
        {
            [UsedImplicitly]
            private static void Prefix(CombatController __instance)
            {
                if (!APState.IsConnected)
                    return;

                if (__instance.CurrentEncounter.IsKeeperBattle)
                    return;

                if (!__instance.CurrentEncounter.IsChampion)
                    return;

                // if victory condition is to beat the mad lord, check to see if we've done that
                if (SlotData.Goal == CompletionEvent.MadLord && GameController.Instance.CurrentSceneName == "AbandonedTower_Final")
                {
                    APState.CompleteGame();
                }
            }
        }

        [HarmonyPatch(typeof(MonsterManager), "AddMonsterByPrefab")]
        private class CompleteMonsterJournalGoal
        {
            [UsedImplicitly]
            private static void Postfix(bool loadingSaveGame)
            {
                if (!APState.IsConnected)
                    return;

                // If we're loading a save game, don't check any locations
                if (loadingSaveGame)
                    return;

                // If the goal is to complete the monster journal, and we've got 111 monsters, complete the game.
                if (SlotData.Goal == CompletionEvent.Monsterpedia)
                {
                    if (PlayerController.Instance.Monsters.AllMonster.Select(m => m.ID).Distinct().Count() == 111)
                        APState.CompleteGame();
                }
            }
        }

        [HarmonyPatch(typeof(ScriptOwner), "Awake")]
        private class ReuniteMozzieGoal
        {
            static int ID = 777777770;

            [UsedImplicitly]
            private static void Prefix(ScriptOwner __instance)
            {
                if (!APState.IsConnected)
                    return;
                if (SlotData.Goal != CompletionEvent.Mozzie)
                    return;
                if (__instance.name != "VelvetMelodyInteract")
                    return;

                // Start by building up all of the nodes we'll need, then we connect them at the end
                var interactTrigger = __instance.GetNode(43900113);
                var getKeyTrigger = __instance.GetNode(43900164);
                var getMagmaChamberKey = __instance.GetNode(43900165);

                var hasAllMozzies = CreateScriptNode<ItemCondition>();
                hasAllMozzies.Item = GameData.GetItemByName("Mozzie").gameObject;
                hasAllMozzies.CompareValue = SlotData.MozzieSoulPieces;
                hasAllMozzies.Operation = ArithmeticCondition.ArithmeticOperation.GreaterEqual;
                __instance.Nodes.Add(hasAllMozzies);

                var mozzieCutSceneTrigger = CreateScriptNode<TriggerOnceCondition>();
                __instance.Nodes.Add(mozzieCutSceneTrigger);

                var movePlayerAction = __instance.GetNode(43900169);
                var waitAction = __instance.GetNode(43900175);

                var poemDialog = __instance.GetNode(43900114);
                var wishToBattleDialog = __instance.GetNode(43900115);

                var missingMozzieSoulsDialog = CreateScriptNode<DialogueAction>();
                missingMozzieSoulsDialog.Title = "Archipelago";
                missingMozzieSoulsDialog.Text = "You must reunite Mozzie's soul before you can awaken Velvet Melody";

                // Add the hint dialog boxes here. 15 is the max mozzie fragments, so we go with that as the limit
                List<DialogueAction> mozzieLocationHints = new List<DialogueAction>();
                for (int i = 0; i < 15; i++)
                {
                    DialogueAction hint = new DialogueAction()
                    {
                        Title = "Archipelago",
                        Text = "Placeholder hint text that will get replaced"
                    };
                    __instance.Nodes.Add(hint);
                    mozzieLocationHints.Add(hint);
                }

                // Re-assign all the connections
                interactTrigger.Connections.Clear();
                interactTrigger.AddConnection(getKeyTrigger);

                getKeyTrigger.Connections.Clear();
                getKeyTrigger.AddConnection(__instance.GetNode(43900163), 1); // Dialog of getting key
                getKeyTrigger.AddConnection(hasAllMozzies, 2);

                getMagmaChamberKey.Connections.Clear();
                getMagmaChamberKey.AddConnection(hasAllMozzies);

                hasAllMozzies.AddConnection(mozzieCutSceneTrigger, 1); // Does have items
                hasAllMozzies.AddConnection(missingMozzieSoulsDialog, 2); // Does not have items.

                mozzieCutSceneTrigger.AddConnection(movePlayerAction, 1); // Show mozzie cut scene
                mozzieCutSceneTrigger.AddConnection(poemDialog, 2); // Jump ahead to poem

                movePlayerAction.Connections.Clear();
                movePlayerAction.AddConnection(__instance.GetNode(43900170)); // Move player jumps directly to showing mozzie. Skip removing the item from inventory

                waitAction.Connections.Clear();
                waitAction.AddConnection(poemDialog);

                poemDialog.Connections.Clear();
                poemDialog.AddConnection(wishToBattleDialog);

                // Connect the message about souls to the series of hints
                for (int i = 0; i < mozzieLocationHints.Count; i++)
                {
                    var hint = mozzieLocationHints[i];
                    // first hint always connects to the prompt
                    if (i == 0)
                    {
                        missingMozzieSoulsDialog.AddConnection(hint);
                        continue;
                    }

                    // Current hint is connected to the next hint in line.
                    if (i + 1 < mozzieLocationHints.Count)
                    {
                        hint.AddConnection(mozzieLocationHints[i + 1]);
                    }
                }
            }

            private static T CreateScriptNode<T>() where T : ScriptNode, new()
            {
                T result = new T()
                {
                    ID = ID
                };

                ID += 1;

                return result;
            }
        }
    }

    public static class ScriptingExtensions
    {
        public static ScriptNode GetNode(this ScriptOwner owner, int id)
        {
            return owner.Nodes.FirstOrDefault(owner => owner.ID == id);
        }

        public static void AddConnection(this ScriptNode origin, ScriptNode target, int slotIndex = 1)
        {
            origin.GetConnections(slotIndex).Add(target);
        }
    }
}