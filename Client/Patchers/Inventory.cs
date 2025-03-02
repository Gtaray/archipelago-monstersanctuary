using Archipelago.MonsterSanctuary.Client.AP;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(InventoryManager), "AddItem")]
        private class InventoryManager_AddItem
        {
            [UsedImplicitly]
            private static void Postfix(InventoryManager __instance, BaseItem item)
            {
                if (!ApState.IsConnected)
                    return;

                if (item is not Egg)
                    return;

                var egg = (Egg)item;
                Task.Run(() =>
                {
                    AddAbilityToDataStorage(egg.Monster);
                });
            }
        }
    }
}
