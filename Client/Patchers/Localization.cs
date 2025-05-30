using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static bool _forceEnglishLanguage = false;
        public static void SetForcedEnglishLanguage(bool force) => _forceEnglishLanguage = force;

        [HarmonyPatch(typeof(OptionsManager), "GetLanguage")]
        private class OptionsManager_GetLanguage
        {
            private static void Postfix(OptionsManager __instance, ref ELanguage __result)
            {
                __result = _forceEnglishLanguage ? ELanguage.English : __result;
            }
        }
    }
}
