using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Patchers
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(Chest), "Start")]
        private class Chest_Start
        {
            private static void Postfix(Chest __instance)
            {
                //if (!ApState.IsConnected)
                //    return;

                //string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                //if (!GameData.ItemChecks.ContainsKey(locName))
                //    return;

                //var locId = GameData.ItemChecks[locName];
                //var progression = _progressionItemChests.Contains(locId);

                //int baseSprite = 0;
                //if (progression)
                //{
                //    baseSprite = 33;
                //}

                //__instance.GetComponent<tk2dSprite>().SetSprite(baseSprite);
            }
        }

        [HarmonyPatch(typeof(Chest), "Interact")]
        private class Chest_Interact
        {
            private static void Prefix(Chest __instance)
            {
                //if (!ApState.IsConnected)
                //    return;

                //string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                //if (!GameData.ItemChecks.ContainsKey(locName))
                //    return;

                //var locId = GameData.ItemChecks[locName];
                //var progression = _progressionItemChests.Contains(locId);

                //int baseSprite = 0;
                //if (progression)
                //{
                //    baseSprite = 33;
                //}

                //var animator = __instance.GetComponent<tk2dSpriteAnimator>();
                //var closed = animator.GetClipByName("closed");
                //var opening = animator.GetClipByName("opening");
                //var open = animator.GetClipByName("open");

                //// Closed
                //closed.frames[0].spriteId = baseSprite;
                //// Opening
                //opening.frames[0].spriteId = baseSprite;
                //opening.frames[1].spriteId = baseSprite + 1;
                //opening.frames[2].spriteId = baseSprite + 2;
                //// Open
                //open.frames[0].spriteId = baseSprite + 2;
            }
        }
    }
}
