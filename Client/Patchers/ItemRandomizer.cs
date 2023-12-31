﻿using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using static PopupController;
using static System.Collections.Specialized.BitVector32;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static ConcurrentDictionary<long, GrantItemsAction> _giftActions = new();

        #region Persistence
        private const string ITEM_CACHE_FILENAME = "archipelago_items.json";
        private static HashSet<long> _itemCache = new HashSet<long>();

        private static void SaveItemsReceived()
        {
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_itemCache));
                File.WriteAllBytes(Path.Combine(rawPath, ITEM_CACHE_FILENAME), bytes);
            }
        }

        private static void LoadItemsReceived()
        {
            if (File.Exists(ITEM_CACHE_FILENAME))
            {
                var reader = File.OpenText(ITEM_CACHE_FILENAME);
                var content = reader.ReadToEnd();
                reader.Close();
                _itemCache = JsonConvert.DeserializeObject<HashSet<long>>(content);
            }
        }
        #endregion

        #region Queue Fucntions
        // Because both items sent and received use the same pop up system to inform the player
        // we use this item queue to merge item received messages from AP as well as items sent
        // messages from the client.
        private static ConcurrentQueue<ItemTransfer> _itemQueue = new();

        public static void QueueItemTransfer(long itemId, int playerId, long locationId, ItemTransferType action)
        {
            // Do not queue a new item if we've already received that item.
            if (_itemCache.Contains(locationId))
                return;

            var itemName = APState.Session.Items.GetItemName(itemId);

            // We don't care about these, they're just flags
            if (itemName == "Champion Defeated")
                return;

            // Don't queue an item if the queue already contains that item id.
            if (_itemQueue.Any(i => i.ItemID == itemId))
                return;

            var transfer = new ItemTransfer()
            {
                ItemID = itemId,
                ItemName = itemName,
                PlayerID = playerId,
                PlayerName = APState.Session.Players.GetPlayerName(playerId),
                LocationID = locationId,
                LocationName = APState.Session.Locations.GetLocationNameFromId(locationId),
                Action = action
            };

            _itemQueue.Enqueue(transfer);
        }
        #endregion

        #region Pathces
        [HarmonyPatch(typeof(GameController), "LoadStartingArea")]
        private class GameController_ClearItemCacheOnNewGame
        {
            [UsedImplicitly]
            private static void Prefix()
            {
                // Clear the item persistence cache when a new file is created
                Logger.LogWarning("New Save. Deleting item cache");
                _itemCache.Clear();
                SaveItemsReceived();
            }
        }

        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                if (!APState.IsConnected)
                    return;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                var locationId = APState.CheckLocation(GameData.GetMappedLocation(locName));
                if (locationId < 0)
                    return;

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
                if (locationId < 0)
                    return true;

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
                {
                    // if the item queue is empty and we have gift actions that need solving
                    // we simply complete them so we can move on
                    if (_giftActions.Count() > 0)
                    {
                        var kvp = _giftActions.First();
                        kvp.Value.Finish();
                        _giftActions.TryRemove(kvp.Key, out var action);
                    }
                    return;
                }

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
                    } 
                    else 
                    {
                        ReceiveItem(
                            nextItem.ItemName,
                            nextItem.PlayerName,
                            nextItem.Action == ItemTransferType.Aquired,
                            callback);
                    }

                    _itemCache.Add(nextItem.LocationID);
                    SaveItemsReceived();

                    AddAndUpdateChecksRemaining(nextItem.LocationName);

                    // If we're reached the end of the item queue,
                    // resync with the server to make sure we've gotten everything
                    if (_itemQueue.Count() == 0)
                    {
                        APState.Resync();
                    }
                }
            }

            private static bool CanGiveItem()
            {
                // If we're in the intro, then don't send items
                if (!ProgressManager.Instance.GetBool("FinishedIntro"))
                    return false;
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
