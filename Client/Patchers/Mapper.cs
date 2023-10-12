using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.UIElements.StyleEnums;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(MonsterEncounter), "Start")]
        private static class MonsterEncounter_Start_Mapper
        {
            private static void Postfix(ref MonsterEncounter __instance)
            {
#if DEBUG
                if (__instance == null)
                {
                    return;
                }

                if (__instance.PredefinedMonsters == null)
                {
                    Logger.LogWarning("Monsterencounter '" + __instance + ".PredefinedMonsters' was null");
                    return;
                }

                if (__instance.PredefinedMonsters.Monster == null)
                {
                    Logger.LogWarning("Monsterencounter '" + __instance + ".PredefinedMonsters.Monster' was null");
                    return;
                }

                // Get the default data so we can print it
                APState.UI.AddMonster(__instance.ID, string.Join(", ", __instance.PredefinedMonsters.Monster.Select(m => m.name)));
#endif
            }

            [HarmonyPatch(typeof(Chest), "Start")]
            private class Chest_Start_Mapper
            {
                private static void Postfix(ref Chest __instance)
                {
#if DEBUG
                    // Get the default data so we can print it
                    if (__instance == null)
                        return;
                    if (__instance.Gold > 0)
                        APState.UI.AddChest(__instance.ID, $"{__instance.Gold} G");
                    else if (__instance.Item == null)
                        APState.UI.AddChest(__instance.ID, "EMPTY");
                    else
                        APState.UI.AddChest(__instance.ID, __instance.Quantity > 1
                            ? $"{__instance.Quantity}x {__instance.Item.name}"
                            : __instance.Item.name);
#endif
                }
            }

            [HarmonyPatch(typeof(ScriptOwner), "Awake")]
            private class SriptOwner_Awake_Mapper
            {
                private static void Postfix(ref ScriptOwner __instance)
                {
#if DEBUG
                    if (GameController.Instance == null)
                    {
                        return; 
                    }

                    foreach (var node in __instance.Nodes)
                    {
                        if (node is GrantItemsAction)
                        {
                            var id = node.ID;
                            var grantNode = (GrantItemsAction)node;
                            var item = grantNode.Item;
                            if (item != null)
                            {
                                var comp = item.GetComponent<BaseItem>();
                                APState.UI.AddGift(id, grantNode.Quantity > 1
                                    ? $"{grantNode.Quantity}x {comp.Name}"
                                    : comp.Name);
                            }
                        }
                    }
#endif
                }
            }
        }
    }
}
