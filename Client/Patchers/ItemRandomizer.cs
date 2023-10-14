using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using static PopupController;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static ConcurrentDictionary<long, GrantItemsAction> _giftActions = new();

        #region Queue Fucntions
        // Because both items sent and received use the same pop up system to inform the player
        // we use this item queue to merge item received messages from AP as well as items sent
        // messages from the client.
        private static ConcurrentQueue<ItemTransfer> _itemQueue = new();

        public static void QueueItemTransfer(long itemId, int playerId, long locationId, ItemTransferType action)
        {
            var transfer = new ItemTransfer()
            {
                ItemID = itemId,
                ItemName = APState.Session.Items.GetItemName(itemId),
                PlayerID = playerId,
                PlayerName = APState.Session.Players.GetPlayerName(playerId),
                LocationID = locationId,
                Action = action
            };

            _itemQueue.Enqueue(transfer);
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

        [HarmonyPatch(typeof(GameController), "Update")]
        private class GameController_Update
        {
            private static void Postfix()
            {
                // Showing a pop up takes the game out of the isExploring state so we
                // should be guaranteed to never give an item until the pop up
                // is closed.
                if (!CanGiveItem())
                    return;

                if (_itemQueue.Count() == 0)
                    return;

                if (_itemQueue.TryDequeue(out ItemTransfer nextItem))
                {
                    PopupController.PopupDelegate callback = null;
                    if (_giftActions.ContainsKey(nextItem.LocationID))
                    {
                        callback = () =>
                        {
                            _giftActions[nextItem.LocationID].Finish();
                            _giftActions.TryRemove(nextItem.LocationID, out var action);
                        };
                    }

                    if (nextItem.Action == ItemTransferType.Sent)
                    {
                        SentItem(
                            nextItem.ItemName, 
                            nextItem.PlayerName, 
                            callback);
                        return;
                    } 
                    else 
                    {
                        ReceiveItem(
                            nextItem.ItemName,
                            nextItem.PlayerName,
                            nextItem.Action == ItemTransferType.Aquired,
                            callback);
                    }
                }
            }

            private static bool CanGiveItem()
            {
                return GameStateManager.Instance.IsExploring();
            }
        }
        #endregion

        public static void SentItem(string item, string player, PopupDelegate confirmCallback)
        {
            item = GameDefines.FormatTextAsHighlight(item);
            player = GameDefines.FormatTextAsHighlight(player);

            PopupController.Instance.ShowMessage(
                    Utils.LOCA("Archipelago"),
                    string.Format("Sent {0} to {1}", item, player), 
                    confirmCallback);
        }

        public static void ReceiveItem(string itemName, string player, bool self, PopupDelegate confirmCallback)
        {
            // Handle if you send someone else and item and show a message box for that.
            if (itemName == null)
            {
                Logger.LogError("Null item was received");
                return;
            }

            var gold = GetGoldQuantity(itemName);
            if (gold > 0)
            {
                GiveGold(gold, player, self, true, confirmCallback);
                return;
            }

            var quantity = GetQuantityOfItem(ref itemName);
            var newItem = GetItemByName(itemName);

            if (newItem == null)
            {
                // This shouldn't happen. Might need a smarter way to solve this.
                Logger.LogError($"No item reference was found with the matching name '{itemName}'.");
                return;
            }

            GiveItem(newItem, player, self, quantity, confirmCallback);
        }        

        #region Give Items
        static void GiveItem(BaseItem item, string player, bool self, int quantity = 1, PopupDelegate callback = null)
        {
            if (item != null)
            {
                item = Utils.CheckForCostumeReplacement(item);

                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Treasure"),
                    FormatItemReceivedMessage(item.GetName(), quantity, player, self),
                    callback);

                PlayerController.Instance.Inventory.AddItem(item, quantity, 0);
                UIController.Instance.Minimap.UpdateKeys();
            }
        }

        public static int GetQuantityOfItem(ref string name)
        {
            int quantity = 1;
            var match = Regex.Match(name, "^(\\d+)x");

            // Quantity is found
            if (match.Success)
            {
                var strQuantity = match.Groups[1].Value;

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

        private static string FormatItemReceivedMessage(string item, int quantity, string player, bool self)
        {
            item = GameDefines.FormatTextAsHighlight(item);
            player = GameDefines.FormatTextAsHighlight(player);

            if (self)
            {
                if (quantity > 1)
                    return string.Format("You found {0} of your {1}", quantity, item);

                return string.Format("You found your {0}", item);
            }
            else
            {
                if (quantity > 1)
                    item = string.Format("{0}x {1}", quantity, item);
                return string.Format("Received {0} from {1}", item, player);
            }
        }
        #endregion

        #region Give Gold
        static void GiveGold(int amount, string player, bool self, bool showMsg = true, PopupController.PopupDelegate callback = null)
        {
            if (showMsg)
            {
                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Treasure"),
                    FormatGoldReceivedMessage(amount, player, self),
                    callback);
                UIController.Instance.PopupController.ShowMessage(
                    Utils.LOCA("Treasure", ELoca.UI),
                    string.Format(Utils.LOCA("Obtained {0}", ELoca.UI), GameDefines.FormatTextAsGold(amount + " G", false)),
                    callback
                );
            }

            PlayerController.Instance.Gold += amount;
        }

        public static int GetGoldQuantity(string itemName)
        {
            int gold = 0;
            var match = Regex.Match(itemName, "^(\\d+) G");
            if (match.Success)
            {
                var strGold = match.Groups[1].Value;

                gold = int.Parse(strGold);

            }

            return gold;
        }

        private static string FormatGoldReceivedMessage(int quantity, string player, bool self)
        {

            string gold = GameDefines.FormatTextAsHighlight(string.Format("{0} Gold", quantity));
            player = GameDefines.FormatTextAsHighlight(player);

            if (self)
                return string.Format("You found your {0}", gold);
            else
                return string.Format("Received {0} from {1}", gold, player);
        }
        #endregion
    }
}
