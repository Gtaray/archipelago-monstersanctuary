using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Persistence;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Playables;
using static PopupController;
using static System.Collections.Specialized.BitVector32;

namespace Archipelago.MonsterSanctuary.Client
{   
    public partial class Patcher
    {
        // When receiving items as part of a dialog chain, we want the game to pause until we receive this item
        // But since we're receiving items asynchronously, we need a way to keep track of which gift actions are waiting on a response from AP
        // We use this dictionary to do that, so we can quickly test if the location ID (key) of a received check is for the gift action we're waiting on
        private static readonly ConcurrentDictionary<long, GrantItemsAction> _grantItemActions = new();

        // When receiving an egg as a gift during dialog, we need to make sure that the egg the player receives is shifted correctly
        // So we simply store the location ID and intended egg shift here, that way the item receiver an handle it appropriately
        private static readonly ConcurrentDictionary<long, EShift> _eggShifts = new();

        #region Patches
        [HarmonyPatch(typeof(GameController), "Update")]
        private class GameController_Update_Items
        {
            private static void Postfix()
            {
                if (!CanGiveItem())
                    return;

                // If we've built up a list of items in the gift queue,
                // check all of those locations at once and then clear the queue
                if (Items.HasSkippedGiftChecks())
                {
                    ApState.CheckLocations(Items.GetSkippedGiftChecks().ToArray());
                    Items.ClearSkippedGiftChecks();
                }

                if (Items.TakeNextItem(out ItemTransfer nextItem))
                {
                    // If we try to queue an item that we've already received, don't
                    if (ApData.IsItemReceived(nextItem.ItemIndex))
                        return;

                    // Only care about champion items to check if we've hit the goal of having them all defeated
                    if (nextItem.ItemName == "Champion Defeated")
                    {
                        if (ApData.GetNumberOfChampionsDefeated() >= 27 && SlotData.Goal == CompletionEvent.Champions)
                        {
                            ApState.CompleteGame();
                        }
                        return;
                    }

                    EShift eggShift = EShift.Normal;
                    if (_eggShifts.ContainsKey(nextItem.LocationID))
                    {
                        _eggShifts.TryRemove(nextItem.LocationID, out eggShift);
                    }

                    ReceiveItem(nextItem.ItemName, eggShift);

                    // We want to do this immediately after adding the item to the player inventory
                    ApData.SetNextExpectedItemIndex(nextItem.ItemIndex + 1);

                    // We only queue notifications for received/acquired items after the player actually receives it.
                    // Notifications for Sent items are handled when the location is checked
                    // We do this here because the Notification queue should be fire and forget
                    // The queue does not do any filtering or conditional checks for what should be shown. If it's on the queue, it gets shown.
                    Notifications.QueueItemTransferNotification(
                        nextItem.ItemName,
                        nextItem.PlayerID,
                        nextItem.LocationID,
                        nextItem.ItemClassification,
                        nextItem.PlayerID == ApState.Session.Players.ActivePlayer
                            ? ItemTransferType.Acquired
                            : ItemTransferType.Received);
                        
                }
            }

            #region Item Handling
            private static bool CanGiveItem()
            {
                // We can give the player items all the time, since giving the item is distinct from notifying the player that the item was given
                return true;
            }

            public static void ReceiveItem(string itemName, EShift eggShift)
            {
                if (itemName == null)
                {
                    Logger.LogError("Null item was received");
                    return;
                }

                var gold = GetGoldQuantity(itemName);
                if (gold > 0)
                {
                    GiveGoldToPlayer(gold);
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

                AddItemToPlayerInventory(ref newItem, quantity, eggShift);
            }

            static void GiveGoldToPlayer(int amount)
            {
                PlayerController.Instance.Gold += amount;
            }

            static void AddItemToPlayerInventory(ref BaseItem item, int quantity = 1, EShift eggShift = EShift.Normal)
            {
                if (item != null)
                {
                    item = Utils.CheckForCostumeReplacement(item);
                    PlayerController.Instance.Inventory.AddItem(item, quantity, (int)eggShift);
                }
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
            #endregion
        }

        [HarmonyPatch(typeof(GameController), "Update")]
        private class GameController_Update_Notifications
        {
            private static void Postfix()
            {
                // Showing a pop up takes the game out of the isExploring state so we
                // should be guaranteed to never give an item until the pop up is closed.
                // Notably, this needs to return true when waiting on a gift action response
                if (!CanNotifyPlayerOfItemTransfer())
                    return;

                if (Notifications.TakeNextNotification(out ItemTransferNotification nextItem))
                {
                    // Ignore champion rank up item notifications
                    if (nextItem.ItemName == "Champion Defeated")
                        return;

                    NotifyPlayer(nextItem);
                }
            }

            #region Notifications
            private static bool CanNotifyPlayerOfItemTransfer()
            {
                // Don't notify if we're in the intro
                if (!ProgressManager.Instance.GetBool("FinishedIntro"))
                    return false;
                // Only notify if we're exploring and grounded
                return GameStateManager.Instance.IsExploring() && PlayerController.Instance.Physics.PhysObject.Collisions.below;
            }

            private static void NotifyPlayer(ItemTransferNotification notification)
            {
                Patcher.UI.AddItemToHistory(notification);

                // For gift actions specifically, we need to make sure we handle the callback of the popup
                PopupController.PopupDelegate callback = null;
                if (_grantItemActions.ContainsKey(notification.LocationID))
                {
                    callback = () =>
                    {
                        _grantItemActions.TryRemove(notification.LocationID, out var action);
                        action.Finish();
                    };
                }

                string msg = "";
                string itemName = notification.ItemName;
                bool selfFound = notification.Action == ItemTransferType.Acquired;
                int gold = GetGoldQuantity(itemName);

                if (gold > 0)
                {
                    msg = FormatGoldReceivedMessage(gold, notification.PlayerName, selfFound);
                }
                else
                {
                    switch (notification.Action)
                    {
                        case ItemTransferType.Sent:
                            msg = FormatItemSentMessage(
                                notification.ItemName,
                                notification.PlayerName,
                                notification.Classification);
                            break;
                        default:
                            int quantity = GetQuantityOfItem(ref itemName);
                            msg = FormatItemReceivedMessage(
                                itemName,
                                quantity,
                                notification.PlayerName,
                                notification.Classification,
                                selfFound);
                            break;
                    }
                }

                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Archipelago"),
                    msg,
                    callback);
            }

            private static string FormatItemSentMessage(string itemName, string playerName, ItemClassification classification)
            {
                return string.Format("Sent {0} to {1}",
                    FormatItem(itemName,
                    classification),
                    FormatPlayer(playerName));
            }

            private static string FormatItemReceivedMessage(string item, int quantity, string player, ItemClassification classification, bool self)
            {
                string updatedItem = FormatItem(item, classification);

                if (self)
                {
                    if (quantity > 1)
                        return string.Format("{0} found {1} of your {2}",
                            FormatSelf("You"),
                            quantity,
                            updatedItem);

                    return string.Format("{0} found your {1}",
                        FormatSelf("You"),
                        updatedItem);
                }
                else
                {
                    if (quantity > 1)
                        updatedItem = string.Format("{0}x {1}", quantity, updatedItem);

                    return string.Format("Received {0} from {1}",
                        updatedItem,
                        FormatPlayer(player));
                }
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

            public static string FormatSelf(string text)
            {
                var newtext = RemoveProblematicCharacters(text);
                return GameDefines.FormatString(Colors.Self, newtext, true);
            }

            public static string FormatPlayer(string text)
            {
                var newtext = RemoveProblematicCharacters(text);
                return GameDefines.FormatString(Colors.OtherPlayer, newtext, true);
            }
            #endregion
        }

        [HarmonyPatch(typeof(Chest), "OpenChest")]
        private class Chest_OpenChest
        {
            [UsedImplicitly]
            private static void Prefix(ref Chest __instance)
            {
                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";

                // Mark this location as checked the instant we interact with it, even if we're offline
                ApData.MarkLocationAsChecked(locName);

                // If we're not connected, but we have an AP Data file, we want to empty the chest so the player doesn't receive the normal item
                if (!ApState.IsConnected)
                {
                    if (ApData.HasApDataFile())
                    {
                        EmptyChest(__instance);
                    }
                }

                if (!Locations.DoesLocationExist(locName))
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return;
                }

                // Empty the chest so it doesn't give the player anything
                EmptyChest(__instance);

                // We're guaranteed to have a location ID at this point, we can safely use .Value
                ApState.CheckLocation(Locations.GetLocationId(locName).Value);
            }

            private static void EmptyChest(Chest __instance)
            {
                __instance.Item = null;
                __instance.Gold = 0;
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

                // Mark this location as opened the instant we interact with it, even if we're offline
                ApData.MarkLocationAsChecked(locName);

                // If we're not connected, then one of two things happens:
                // There's no AP Data File associated with this file slot, in which case we let the normal code take over
                // If there IS an AP Data File associated with this file slot (and it is loaded), we do skip this event and move on (without giving the player anything)
                if (!ApState.IsConnected)
                {
                    if (ApData.HasApDataFile())
                    {
                        __instance.Finish();
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                
                if (!Locations.DoesLocationExist(locName))
                {
                    Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                    return true;
                }

                long locationId = Locations.GetLocationId(locName).Value;

                // We save this Action in the dictionary, indexed by its location ID
                // that way when the player is notified on receiving the item, we can 
                // continue this dialog
                _grantItemActions.TryAdd(locationId, __instance);

                // If this action would give an egg that is Light or Dark shifted, we save that so we can handle it when the player receives the item
                if (__instance.EggShift != EShift.Normal)
                    _eggShifts.TryAdd(locationId, __instance.EggShift);

                // If the player is skipping this dialog,
                // then we want to batch all skipped gift actions together
                if (!showMessage)
                {
                    Items.AddSkippedGiftCheck(locationId);
                }
                else
                {
                    ApState.CheckLocation(locationId);
                }

                return false;
            }
        }
        #endregion       

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

        public static string FormatItem(string text, ItemClassification classification)
        {
            text = RemoveProblematicCharacters(text);

            return GameDefines.FormatString(Colors.GetItemColor(classification), text, true);
        }

        public static string RemoveProblematicCharacters(string text)
        {
            return text.Replace("<3", "heart")
                .Replace("<", "")
                .Replace(">", "");
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
    }
}
