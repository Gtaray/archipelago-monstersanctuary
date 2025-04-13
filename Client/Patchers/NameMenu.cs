using Archipelago.MonsterSanctuary.Client.AP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SaveGameMenu;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        /// <summary>
        /// Add new input type to max characters allowed check
        /// </summary>
        [HarmonyPatch(typeof(NameMenu), "MaxCharactersAllowed", MethodType.Getter)]
        public static class NameMenu_MaxCharactersAllowed
        {
            public static bool Prefix(NameMenu __instance, ref int __result)
            {
                var nameType = (NameMenu.ENameType)Traverse.Create(UIController.Instance.NameMenu)
                    .Field("nameType")
                    .GetValue();
                
                __result = nameType == NameMenu.ENameType.MapMarker ? 20 : __instance.MaxCharacters;
                __result = (int)nameType switch
                {
                    (int)NameMenu.ENameType.JoinCode => 4,
                    (int)NameMenu.ENameType.SeedCode => 6,
                    // check for our new input type of 7
                    7 => __instance.MaxCharacters,
                    _ => __result
                };
                return false;
            }
        }
        
        /// <summary>
        /// Also disable confirmation if we're using the new input type
        /// </summary>
        [HarmonyPatch(typeof(NameMenu), "Confirm")]
        public static class NameMenu_Confirm
        {
            public static bool Prefix(NameMenu __instance)
            {
                var nameType = (NameMenu.ENameType)Traverse.Create(UIController.Instance.NameMenu)
                    .Field("nameType")
                    .GetValue();
                __instance.DirectKeyboardSupportHandler.Deactivate();
                // If it's not the new menu type, just run the original code
                if ((int)nameType != 7) return true;
                AccessTools.Method(typeof(NameMenu), "ConfirmNaming")?.Invoke(__instance, []);
                return false;
            }
        }
    }
}
