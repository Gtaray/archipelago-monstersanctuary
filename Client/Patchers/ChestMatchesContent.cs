using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(Chest), "Start")]
        private class Chest_Start
        {
            private static void Postfix(Chest __instance)
            {
                if (!ApState.IsConnected)
                    return;

                __instance.gameObject.AddComponent<ChestGraphicsMatchContent>();
            }
        }

        [HarmonyPatch(typeof(Chest), "Interact")]
        private class Chest_Interact
        {
            private static void Prefix(Chest __instance)
            {
                if (!ApState.IsConnected)
                    return;

                var animator = __instance.GetComponent<tk2dSpriteAnimator>();
                var comp = __instance.gameObject.GetComponent<ChestGraphicsMatchContent>();
                if (comp == null)
                    return;

                int spriteId = (int)comp.Color;
                var closed = animator.GetClipByName("closed");
                var opening = animator.GetClipByName("opening");
                var open = animator.GetClipByName("open");

                // Closed
                closed.frames[0].spriteId = spriteId;

                // Opening
                opening.frames[0].spriteId = spriteId;
                opening.frames[1].spriteId = spriteId + 1;
                opening.frames[2].spriteId = spriteId + 2;

                // Open
                open.frames[0].spriteId = spriteId + 2;
            }
        }
    }
}
