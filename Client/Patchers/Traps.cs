using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Experimental.UIElements.StyleEnums;
using static BuffManager;
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
            { "Faulty Equipment", "some {trapName}, damaging {extra} beyond repair" },
            { "Monster Army Conscription Notice", "a {trapName} for {extra}" },
        };

        private static ItemTransfer _currentTrap = null;
        private static ConcurrentQueue<ItemTransfer> _trapQueue = new();
        #endregion

        #region Patches
        [HarmonyPatch(typeof(CombatController), "StartPlayerTurn")]
        private class CombatController_StartPlayerTurn
        {
            private static void Postfix()
            {
                if (_trapQueue.Count == 0)
                    return;

                while (_trapQueue.TryDequeue(out ItemTransfer trap))
                {
                    _currentTrap = trap;

                    Patcher.UI.AddItemToHistory(_currentTrap);

                    ProcessTrap(
                        _currentTrap.ItemName,
                        _currentTrap.PlayerName,
                        _currentTrap.Action == ItemTransferType.Aquired,
                        null);

                    // Save the trap to the cache so we don't get it again.
                    AddToItemCache(_currentTrap.ItemIndex.Value);
                }

                _currentTrap = null;
            }
        }

        [HarmonyPatch(typeof(CombatController), "StartEnemyTurn")]
        private class CombatController_StartEnemyTurn
        {
            private static void Postfix()
            {

            }
        }

        [HarmonyPatch(typeof(SkillManager), "CanApplyDebuffOrNegativeStack")]
        private class SkillManager_CanApplyDebuffOrNegativeStack
        {
            private static bool Prefix(ref bool __result)
            {
                if (_currentTrap != null)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SkillManager), "OnApplyDebuffToEnemy")]
        private class SkillManager_OnApplyDebuffToEnemy
        {
            private static bool Prefix(BuffManager __instance, BaseAction action)
            {
                if (action == null && _currentTrap != null)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(BuffManager), "HasDebuffMasterySlot")]
        private class BuffManager_HasDebuffMasterySlot
        {
            private static bool Prefix(BuffManager __instance, ref bool __result, Type applyingSkill)
            {
                // Traps should always apply the debuff as if it's mastery
                if (_currentTrap != null)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
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
            else if (trapName == "Monster Army Conscription Notice")
            {
                extra = MonsterArmyConscription();
            }

            ShowTrapMessage(trapName, extra, player, self, confirmCallback);
        }

        private static void ShowTrapMessage(string trapName, string extra, string player, bool self, PopupController.PopupDelegate callback)
        {
            if (_nonCombatTraps.Contains(trapName))
            {
                // Non-Combat traps only show the message box
                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Trap!"),
                    FormatTrapMessage(trapName, extra, player, self),
                    callback);
            }
        }

        private static string FormatTrapMessage(string trapName, string extra, string player, bool self)
        {
            string message = _trapDescription[trapName]
                .Replace("{trapName}", FormatItem(trapName, ItemClassification.Trap));

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
            Random rand = new Random();

            // Get a list of monsters who have equipment
            var monsters = PlayerController.Instance.Monsters.Active
                .Where(m => m.Equipment.EquipmentCount > 0)
                .ToList();

            // if for some reason the player has no monsters with equipment, then they got lucky
            if (monsters.Count == 0)
            {
                return "no items";
            }

            var mIndex = rand.Next(monsters.Count);
            var monster = monsters[mIndex];

            // Convert the array of equipment (including empty slots) to a list of only possible options
            var possibleEquipment = monster.Equipment.Equipment.Where(m => m != null).ToList();
            if (possibleEquipment.Count == 0)
            {
                return "not items";
            }
            // A bit wacky, but we randomly select one of the possible equipment pieces, then get its index in the 
            // monster's equipment array. Then we use that to get the piece of equipment.
            var equipment = possibleEquipment[rand.Next(possibleEquipment.Count())];
            var eIndex = Array.IndexOf(monster.Equipment.Equipment, equipment);


            // Unequipping will pass the item back to the player's inventory, so we can't use that.
            // Instead we just set the equip slot to null, which will vanish the item
            monster.Equipment.Equip(null, (EquipmentManager.EquipmentSlot)eIndex);
            return $"{monster.GetName()}'s {equipment.GetName()}";
        }

        /// <summary>
        /// Returns the name of the monster that was perma-deaded
        /// </summary>
        /// <returns></returns>
        private static string MonsterArmyConscription()
        {
            // Do not conscript if the player is already down to 6 or fewer monsters
            if (PlayerController.Instance.Monsters.AllMonster.Count() <= 6)
            {
                return "someone else's monster";
            }

            // Don't select the familiar to be conscripted
            var choices = PlayerController.Instance.Monsters.AllMonster
                .Where(m => !m.IsSpectralFamiliar)
                .ToList();

            Random rand = new Random();
            var index = rand.Next(choices.Count());
            var monster = choices[index];

            PlayerController.Instance.Monsters.MonsterPermanentlyDies(monster);

            return monster.GetName();
        }

        private static void PoisonTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Poison, null);
            }
        }

        private static void ShockTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Shock, null);
            }
        }

        private static void FreezeTrap()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.PlayerMonsters)
            {
                monster.BuffManager.AddDebuff(source, BuffManager.DebuffType.Chill, null);
            }
        }

        private static void Ambush()
        {
            var source = CombatController.Instance.Enemies.FirstOrDefault(m => !m.IsDead());
            foreach (var monster in CombatController.Instance.Enemies)
            {
                // Apply sidekick and a 30% shield to all enemies
                monster.BuffManager.AddBuff(new BuffSourceChain(source), BuffManager.BuffType.Sidekick, true);
                monster.AddShield(source, (int)(monster.MaxHealth * 0.3));
            }
        }
        #endregion
    }
}
