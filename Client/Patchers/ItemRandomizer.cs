using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static PopupController;
using static System.Net.Mime.MediaTypeNames;

namespace Archipelago.MonsterSanctuary.Client
{
    public enum ItemTransferType
    {
        Aquired = 0,
        Received = 1,
        Sent = 2
    }
    public class ItemTransfer
    {
        public string ItemName { get; set; }
        public string PlayerName { get; set; }
        public ItemTransferType Action { get; set; }

        public int? ItemIndex { get; set; }
        public long ItemID { get; set; }
        public int PlayerID { get; set; }
        public long LocationID { get; set; }
        public string LocationName { get; set; }
        public ItemClassification Classification { get; set; }
    }

    public partial class Patcher
    {
        private static ConcurrentDictionary<long, GrantItemsAction> _giftActions = new();

        #region Queue Functions
        // Because both items sent and received use the same pop up system to inform the player
        // we use this item queue to merge item received messages from AP as well as items sent
        // messages from the client.
        private static ConcurrentQueue<ItemTransfer> _itemQueue = new();

        // When skipping dialog that gives the player multiple items, calling CheckLocation back to back that fast
        // can cause problems, especially when finding other players' items. In those cases we batch all item checks
        // until the player is able to move again, then check all of them at once.
        private static List<long> _giftQueue = new();

        public static void QueueItemTransfer(int? itemIndex, long itemId, int playerId, long locationId, ItemClassification classification, ItemTransferType action)
        {
            var itemName = APState.Session.Items.GetItemName(itemId);

            // If item index is null (meaning this is someone else's item), we can only rely on whether the location ID is checked.
            if (itemIndex == null && Persistence.Instance.LocationsChecked.Contains(locationId))
            {
                return;
            }

            // We need to do this here so that we know for sure when a check is done the map is updated
            // If the item action is either sent or acquired, we want to update the minimap. If we received the item then its not from us at all.
            if (action != ItemTransferType.Received)
            {
                Persistence.AddAndUpdateCheckedLocations(locationId);
            }   

            // Do not queue a new item if we've already received that item.
            // Do not queue an item if the queue already contains that index.
            if (itemIndex != null && (Persistence.Instance.ItemsReceived.Contains(itemIndex.Value) || _itemQueue.Any(i => i.ItemIndex == itemIndex)))
            {
                return;
            }

            var transfer = new ItemTransfer()
            {
                ItemIndex = itemIndex,
                ItemID = itemId,
                ItemName = itemName,
                PlayerID = playerId,
                PlayerName = APState.Session.Players.GetPlayerName(playerId),
                LocationID = locationId,
                LocationName = APState.Session.Locations.GetLocationNameFromId(locationId),
                Classification = classification,
                Action = action
            };

            _itemQueue.Enqueue(transfer);
        }
        #endregion

        #region Patches
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

                // If we've built up a list of items in the gift queue,
                // check all of those locations at once and then clear the queue
                if (_giftQueue.Count > 0)
                {
                    APState.CheckLocations(_giftQueue.ToArray());
                    _giftQueue.Clear();
                }

                if (_itemQueue.TryDequeue(out ItemTransfer nextItem))
                {
                    // For these, we just want to increment the counter and move on. Nothing else.
                    if (nextItem.ItemName == "Champion Defeated")
                    {
                        if (Persistence.Instance.ChampionsDefeated.Count() >= 27 && SlotData.Goal == CompletionEvent.Champions)
                        {
                            APState.CompleteGame();
                        }
                        return;
                    }

                    Patcher.UI.AddItemToHistory(nextItem);

                    PopupController.PopupDelegate callback = null;
                    EShift eggShift = EShift.Normal;
                    if (_giftActions.ContainsKey(nextItem.LocationID))
                    {
                        eggShift = _giftActions[nextItem.LocationID].EggShift;

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
                            nextItem.Classification,
                            callback);
                    }
                    else
                    {
                        ReceiveItem(
                            nextItem.ItemName,
                            nextItem.PlayerName,
                            nextItem.Classification,
                            nextItem.Action == ItemTransferType.Aquired,
                            eggShift,
                            callback);

                        // We only want to save items to the item cache if we're receiving the item. 
                        // Do not cache items we send to other people
                        Persistence.AddToItemCache(nextItem.ItemIndex.Value);
                    }
                }
            }

            private static bool CanGiveItem()
            {
                // If we're in the intro, then don't send items
                if (!ProgressManager.Instance.GetBool("FinishedIntro"))
                    return false;
                return GameStateManager.Instance.IsExploring() && PlayerController.Instance.Physics.PhysObject.Collisions.below;
            }
        }

        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";

                // If we're not connected, we add this location to a list of locations that need to be checked once we are connected
                if (!APState.IsConnected)
                {
                    APState.OfflineChecks.Add(locName);
                    return;
                }

                if (!GameData.ItemChecks.ContainsKey(locName))
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return;
                }

                __instance.Item = null;
                __instance.Gold = 0;

                APState.CheckLocation(GameData.ItemChecks[locName]);
            }
        }

        [HarmonyPatch(typeof(GrantItemsAction), "GrantItem")]
        private class GrantItemsAction_GrantItem
        {
            // This needs to also handle NPCs that give the player eggs, and randomize which egg is given
            [UsedImplicitly]
            private static bool Prefix(ref GrantItemsAction __instance, bool showMessage)
            {
                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";

                // If we're not connected, we add this location to a list of locations that need to be checked once we are connected
                if (!APState.IsConnected)
                {
                    APState.OfflineChecks.Add(locName);
                    return true;
                }

                if (!GameData.ItemChecks.ContainsKey(locName))
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return true;
                }

                _giftActions.TryAdd(GameData.ItemChecks[locName], __instance);

                if (!showMessage)
                {
                    _giftQueue.Add(GameData.ItemChecks[locName]);
                }
                else
                {
                    APState.CheckLocation(GameData.ItemChecks[locName]);
                }

                return false;
            }
        }
        #endregion

        public static void SentItem(string item, string player, ItemClassification classification, PopupDelegate confirmCallback)
        {
            var text = string.Format("Sent {0} to {1}", FormatItem(item, classification), FormatPlayer(player));
            PopupController.Instance.ShowMessage(
                    Utils.LOCA("Archipelago"),
                    text, 
                    confirmCallback);
        }

        public static void ReceiveItem(string itemName, string player, ItemClassification classification, bool self, EShift eggShift, PopupDelegate confirmCallback)
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
                Logger.LogError($"No item reference was found matching the name '{itemName}'.");
                return;
            }

            GiveItem(newItem, player, classification, self, quantity, confirmCallback, eggShift);
        }        

        #region Give Items
        static void GiveItem(BaseItem item, string player, ItemClassification classification, bool self, int quantity = 1, PopupDelegate callback = null, EShift eggShift = EShift.Normal)
        {
            if (item != null)
            {
                item = Utils.CheckForCostumeReplacement(item);

                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Treasure"),
                    FormatItemReceivedMessage(item.GetName(), quantity, player, classification, self),
                    callback);

                PlayerController.Instance.Inventory.AddItem(item, quantity, (int) eggShift);
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
            if (name.EndsWith(" Egg"))
                return GetItemByName<Egg>(name);

            return GetItemByName<BaseItem>(name);
        }

        static BaseItem GetItemByName<T>(string name) where T : BaseItem
        {       
            return GameController.Instance.WorldData.Referenceables
                .Where(x => x?.gameObject.GetComponent<T>() != null)
                .Select(x => x.gameObject.GetComponent<T>())
                .SingleOrDefault(i => string.Equals(i.GetName(), name, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatItemReceivedMessage(string item, int quantity, string player, ItemClassification classification, bool self)
        {
            item = FormatItem(item, classification);

            if (self)
            {
                if (quantity > 1)
                    return string.Format("{0} found {1} of your {2}", 
                        FormatSelf("You"),
                        quantity, 
                        item);

                return string.Format("{0} found your {1}", 
                    FormatSelf("You"),
                    item);
            }
            else
            {
                if (quantity > 1)
                    item = string.Format("{0}x {1}", quantity, item);

                return string.Format("Received {0} from {1}", 
                    item, 
                    FormatPlayer(player));
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
            string gold = FormatItem(string.Format("{0} Gold", quantity), ItemClassification.Filler);

            if (self)
                return string.Format("{0} found {1}", 
                    FormatSelf("You"), 
                    gold);
            else
                return string.Format("Received {0} from {1}", 
                    gold, 
                    FormatPlayer(player));
        }
        #endregion

        #region String Formatting / Coloring
        public static string GetItemColor(ItemClassification classification)
        {
            if (classification == ItemClassification.Progression)
            {
                return Colors.ProgressionItem;
            }
            else if (classification == ItemClassification.Useful)
            {
                return Colors.UsefulItem;
            }
            else if (classification == ItemClassification.Trap)
            {
                return Colors.TrapItem;
            }

            return Colors.FillerItem;
        }

        public static string FormatItem(string text, ItemClassification classification)
        {
            text = RemoveProblematicCharacters(text);
            
            return GameDefines.FormatString(GetItemColor(classification), text, true);
        }

        public static string FormatSelf(string text)
        {
            text = RemoveProblematicCharacters(text);
            return GameDefines.FormatString(Colors.Self, text, true);
        }

        public static string FormatPlayer(string text)
        {
            text = RemoveProblematicCharacters(text);
            return GameDefines.FormatString(Colors.OtherPlayer, text, true);
        }

        public static string RemoveProblematicCharacters(string text)
        {
            return text.Replace("<3", "heart")
                .Replace("<", "")
                .Replace(">", "");
        }
        #endregion
    }
}
