using Archipelago.MonsterSanctuary.Client.AP;
using HarmonyLib;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(DialogueAction), "StartNode")]
        private class DialogueAction_StartNode
        {
            private static void Prefix(DialogueAction __instance)
            {
                if (!ApState.IsConnected)
                    return;

                // If the player loses the duel with will in the duel circle, end early
                if (__instance.ID == 699 || __instance.ID == 6800021)
                {
                    __instance.Connections.Clear();
                }

                var hint = Hints.GetHintText(__instance.ID);
                if (string.IsNullOrEmpty(hint))
                    return;
                __instance.Text = hint;

                // This might be too extreme and cause things to break
                if (Hints.ShouldEndHintDialog(__instance.ID))
                    __instance.Connections.Clear();
            }
        }
    }
}
