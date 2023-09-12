using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archipelago.MonsterSanctuary.Client
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class ArchipelagoPlugin : BaseUnityPlugin
    {
        private static ManualLogSource _log;

        private void Awake()
        {
            _log = Logger;

            // Plugin startup logic
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());
        }

        //[HarmonyPatch(typeof(MonsterEncounter), "Start")]
        //private class MonsterEncounterStartPatch
        //{
        //    [UsedImplicitly]
        //    private static void Prefix(ref MonsterEncounter __instance)
        //    {
        //        if (!__instance.IsNormalEncounter)
        //        {
        //            _log.LogDebug("MonsterRandomizer ignore: Not a normal encounter");
        //            return;
        //        }

        //        _log.LogDebug($"Encounter ID: {__instance.ID}");
        //        _log.LogDebug($"Encounter ID: {__instance.name}");
        //    }
        //}

        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                //if (!GameModeManager.Instance.RandomizerMode)
                //{
                //    _log.LogDebug("ChestRandomizer ignore: Not in Randomizer mode");

                //    return;
                //}
                
                _log.LogWarning($"Scene Name: {GameController.Instance.CurrentSceneName}");
                _log.LogWarning($"Chest ID: {__instance.ID}");
            }
        }

        [HarmonyPatch(typeof(Monster), "GetExpReward")]
        private class Monster_GetExpReward
        {
            [UsedImplicitly]
            private static void Postfix(ref int __result)
            {
                //if (_expMultiplier.Value <= 0)
                //{
                //    return;
                //}
                __result = __result * 10;
            }
        }
    }
}