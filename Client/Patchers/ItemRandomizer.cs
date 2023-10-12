using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        #region Helper functions
        private static ConcurrentDictionary<long, GrantItemsAction> _giftActions = new ConcurrentDictionary<long, GrantItemsAction>();

        public static void ReceiveItem(string itemName, long locationId)
        {
            Logger.LogInfo("ReceiveItem(), " + itemName + ", " + locationId);
            // Handle if you send someone else and item and show a message box for that.
            if (itemName == null)
            {
                Logger.LogError("Null item was received");
                return;
            }

            PopupController.PopupDelegate callback = null;
            if (_giftActions.ContainsKey(locationId))
            { 
                callback = () =>
                {
                    _giftActions[locationId].Finish();
                    _giftActions.TryRemove(locationId, out var action);
                };
            }

            var gold = GetGoldQuantity(itemName);
            if (gold > 0)
            {
                GiveGold(gold, true, callback);
                return;
            }

            var quantity = GetQuantityOfItem(ref itemName);
            var newItem = GetItemByName(itemName);

            if (newItem == null)
            {
                // This shouldn't happen. Might need a smarter way to solve this.
                Logger.LogError("No item reference was found with the matching name.");
                return;
            }

            Logger.LogInfo("New Item: " + newItem.Name);

            GiveItem(newItem, quantity, callback);
        }

        static void GiveItem(BaseItem item, int quantity = 1, PopupController.PopupDelegate callback = null, bool showMsg = true)
        {
            if (item != null)
            {
                item = Utils.CheckForCostumeReplacement(item);
                UIController.Instance.PopupController.ShowReceiveItem(item, quantity, true, callback);

                Logger.LogInfo($"Acquired {item.Name}");
                PlayerController.Instance.Inventory.AddItem(item, quantity, 0);
                UIController.Instance.Minimap.UpdateKeys();
            }
        }

        static void GiveGold(int amount, bool showMsg = true, PopupController.PopupDelegate callback = null)
        {
            if (showMsg)
            {
                UIController.Instance.PopupController.ShowMessage(
                    Utils.LOCA("Treasure", ELoca.UI),
                    string.Format(Utils.LOCA("Obtained {0}", ELoca.UI), GameDefines.FormatTextAsGold(amount + " G", false)),
                    callback
                );
            }

            Logger.LogInfo($"Acquired {amount} gold");
            PlayerController.Instance.Gold += amount;
        }

        public static int GetGoldQuantity(string itemName)
        {
            int gold = 0;
            var match = Regex.Match(itemName, "^(\\d+) G");
            if (match.Success)
            {
                var strGold = match.Groups[1].Value;

                // Null check because unit tests call this
                if (Logger != null)
                    Logger.LogWarning("Found gold: " + strGold);

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

                if (Logger != null)
                    Logger.LogWarning("Found quantity group: " + strQuantity);

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
        #endregion

        #region Pathces
        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                if (!APState.IsConnected)
                    return;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                APState.CheckLocation(GameData.GetMappedLocation(locName));

                __instance.Item = null;
                __instance.Gold = 0;
            }
        }

        [HarmonyPatch(typeof(GrantItemsAction), "GrantItem")]
        private class GrantItemsAction_GrantItem
        {
            // This needs to also handle NPCs that give the player eggs, and randomize which egg is given
            [UsedImplicitly]
            private static bool Prefix(ref GrantItemsAction __instance)
            {
                if (!APState.IsConnected)
                    return true;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                var locationId = APState.CheckLocation(GameData.GetMappedLocation(locName));

                _giftActions.TryAdd(locationId, __instance);

                return false;
            }
        }
        #endregion
    }
}
