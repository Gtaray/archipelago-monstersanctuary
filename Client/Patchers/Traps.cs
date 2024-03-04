using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PopupController;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        #region Trap Data
        private static List<string> _combatTraps = new()
        {
            "Poison Trap",
            "Shock Trap",
            "Freeze Trap",
            "Ambush!"
        };

        private static bool IsCombatTrap(string itemName)
        {
            return _combatTraps.Contains(itemName);
        }

        private static List<string> _nonCombatTraps = new()
        {
            "Monster Stable Fees",
            "Faulty Equipment",
            "Monster Army Conscription Notice"
        };

        private static bool IsNonCombatTrap(string itemName)
        {
            return _nonCombatTraps.Contains(itemName);
        }

        private static Dictionary<string, string> _trapDescription = new()
        {
            { "Poison Trap", "a {trapName}" },
            { "Shock Trap", "a {trapName}" },
            { "Freeze Trap", "a {trapName}" },
            { "Ambush!", "an {trapName}" },
            { "Monster Stable Fees", "a {trapName} bill for {extra} gold" },
            { "Faulty Equipment", "some {itemName}, damaging your {extra} beyond repair" },
            { "Monster Army Conscription Notice", "an {trapName} for {extra}" },
        };

        private static ConcurrentQueue<ItemTransfer> _trapQueue = new();
        #endregion

        #region Patches
        [HarmonyPatch(typeof(CombatController), "StartPlayerTurn")]
        private class CombatController_StartPlayerTurn
        {

        }
        #endregion

        #region Trap Processing
        private static void ProcessTrap(string trapName, string player, bool self, PopupDelegate confirmCallback)
        {
            string extra = "";

            if (trapName == "Poison Trap")
            {
                PoisonTrap();
            }
            else if (trapName == "Shock Trap")
            {
                ShockTrap();
            }
            else if (trapName == "Freeze Trap")
            {
                FreezeTrap();
            }
            else if (trapName == "Ambush!")
            {
                Ambush();
            }
            else if (trapName == "Monster Stable Fees")
            {
                extra = MonsterStableFees();
            }
            else if (trapName == "Faulty Equipment")
            {
                extra = FaultyEquipment();
            }
            else if (trapName == "Monster Army Conscription")
            {
                extra = MonsterArmyConscription();
            }

            ShowTrapMessage(trapName, extra, player, self, confirmCallback);
        }

        private static void ShowTrapMessage(string trapName, string extra, string player, bool self, PopupController.PopupDelegate callback)
        {
            PopupController.Instance.ShowMessage(
                Utils.LOCA("Trap!"),
                FormatTrapMessage(trapName, extra, player, self),
                callback);
        }

        private static string FormatTrapMessage(string trapName, string extra, string player, bool self)
        {
            trapName = FormatItem(trapName, ItemClassification.Trap);
            string message = _trapDescription[trapName].Replace("{trapName}", trapName);

            if (!string.IsNullOrEmpty(message))
            {
                message = message.Replace("{extra}", FormatItem(extra, ItemClassification.Filler));
            }

            if (self)
            {
                return string.Format("{0} found {1}!",
                    FormatSelf("You"),
                    message);

            }
            else
            {
                return string.Format("{0} sent you {1}!",
                    FormatPlayer(player),
                    message);
            }
        }

        /// <summary>
        /// Returns the amount of gold the player loses
        /// </summary>
        /// <returns></returns>
        private static string MonsterStableFees()
        {
            
            int loss = (int)Math.Round(PlayerController.Instance.Gold * 0.1);
            PlayerController.Instance.Gold -= loss;
            return loss.ToString(); 
        }

        /// <summary>
        /// Returns the name of the equipment that was deleted
        /// </summary>
        /// <returns></returns>
        private static string FaultyEquipment()
        {
            return "";
        }

        /// <summary>
        /// Returns the name of the monster that was perma-deaded
        /// </summary>
        /// <returns></returns>
        private static string MonsterArmyConscription()
        {
            return "";
        }

        private static void PoisonTrap()
        {

        }

        private static void ShockTrap()
        {

        }

        private static void FreezeTrap()
        {

        }

        private static void Ambush()
        {

        }
        #endregion
    }
}
