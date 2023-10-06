using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        #region Helper functions
        public static int GetGoldQuantity(string itemName)
        {
            int gold = 0;
            var match = Regex.Match(itemName, "^(\\d+) G");
            if (match.Success)
            {
                var strGold = match.Groups[1].Value;

                // Null check because unit tests call this
                if (_log != null)
                    _log.LogWarning("Found gold: " + strGold);

                gold = int.Parse(strGold);

            }

            return gold;
        }

        public static int GetQuantityOfItem(ref string name)
        {
            int quantity = 1;
            var match = Regex.Match(name, "^(\\d+)x");

            // Quantity is found
            if (match.Success)
            {
                var strQuantity = match.Groups[1].Value;

                if (_log != null)
                    _log.LogWarning("Found quantity group: " + strQuantity);

                quantity = int.TryParse(strQuantity, out quantity) ? quantity : 1;
                name = Regex.Replace(name, "^\\d+x", "");
            }

            name = name.Trim();
            return quantity;
        }

        static BaseItem GetItemByName(string name)
        {
            // Trim "#x" from the name
            return GameController.Instance.WorldData.Referenceables
                    .Where(x => x?.gameObject.GetComponent<BaseItem>() != null)
                    .Select(x => x.gameObject.GetComponent<BaseItem>())
                    .SingleOrDefault(i => string.Equals(i.GetName(), name, StringComparison.OrdinalIgnoreCase));
        }

        static void GiveItem(BaseItem item, int quantity = 1, PopupController.PopupDelegate callback = null, bool showMsg = true)
        {
            if (showMsg)
                UIController.Instance.PopupController.ShowReceiveItem(item, quantity, true, callback);

            _log.LogInfo($"Acquired {item.Name}");
            PlayerController.Instance.Inventory.AddItem(item, quantity);
        }

        static void GiveGold(int amount, bool showMsg = true)
        {
            if (showMsg)
            {
                UIController.Instance.PopupController.ShowMessage(
                    Utils.LOCA("Treasure", ELoca.UI),
                    string.Format(Utils.LOCA("Obtained {0}", ELoca.UI), GameDefines.FormatTextAsGold(amount + " G", false))
                );
            }

            _log.LogInfo($"Acquired {amount} gold");
            PlayerController.Instance.Gold += amount;
        }
        #endregion

        #region Pathces
        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            private static void PrintReferenceItems()
            {
                var items = GameController.Instance.WorldData.Referenceables
                    .Where(x => x?.gameObject.GetComponent<BaseItem>() != null)
                    .Select(x => x.gameObject.GetComponent<BaseItem>());

                foreach (var item in items)
                {
                    _log.LogInfo(item + ": " + item.Name);
                }
            }

            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                if (!APState.IsConnected)
                    return;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                var itemName = APState.CheckLocation(GetMappedLocation(locName));

                // Set this to null since we'll handle giving the item
                // We do this up here so that if an error occurs, we get no items
                // and it's obvious that something went wrong
                __instance.Item = null;

                // Handle if you send someone else and item and show a message box for that.
                if (itemName == null)
                {
                    _log.LogError("Item name was not found for this location.");
                    return;
                }

                var gold = GetGoldQuantity(itemName);
                if (gold > 0)
                {
                    __instance.Gold = gold;
                    return;
                }

                var quantity = GetQuantityOfItem(ref itemName);
                var newItem = GetItemByName(itemName);

                if (newItem == null)
                {
                    // This shouldn't happen. Might need a smarter way to solve this.
                    _log.LogError("No item reference was found with the matching name.");
                    return;
                }

                _log.LogInfo("New Item: " + newItem.Name);

                GiveItem(newItem, quantity);
            }
        }

        [HarmonyPatch(typeof(GrantItemsAction), "GrantItem")]
        private class GrantItemsAction_GrantItem
        {
            // This needs to also handle NPCs that give the player eggs, and randomize which egg is given
            [UsedImplicitly]
            private static bool Prefix(bool showMessage, ref GrantItemsAction __instance)
            {
                if (!APState.IsConnected)
                    return true;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                var itemName = APState.CheckLocation(GetMappedLocation(locName));

                // Handle if you send someone else and item and show a message box for that.
                if (itemName == null)
                {
                    _log.LogError("Item name was not found for this location.");
                    return false;
                }

                var gold = GetGoldQuantity(itemName);
                if (gold > 0)
                {
                    GiveGold(gold, showMessage);
                    return false;
                }

                var quantity = GetQuantityOfItem(ref itemName);
                var newItem = GetItemByName(itemName);

                if (newItem == null)
                {
                    // This shouldn't happen. Might need a smarter way to solve this.
                    _log.LogError("No item reference was found with the matching name.");
                    return false;
                }

                _log.LogInfo("New Item: " + newItem.Name);

                GiveItem(newItem, quantity, new PopupController.PopupDelegate(__instance.Finish), showMessage);

                // because we can't set __instance.Item to null like we could with chests,
                // we need to outright prevent the original from running
                return false;
            }
        }
        #endregion
    }
}
