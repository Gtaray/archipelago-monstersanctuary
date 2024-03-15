using Archipelago.MultiClient.Net.Models;
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
using System.Xml;
using UnityEngine;
using static PopupController;
using static System.Net.Mime.MediaTypeNames;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static ConcurrentDictionary<long, GrantItemsAction> _giftActions = new();
        private static List<string> _monsterArmyRewards = new();

        #region Persistence
        private const string ITEM_CACHE_FILENAME = "archipelago_items_received.json";
        private static int _itemsReceivedIndex = -1;

        public static void DeleteItemCache()
        {
            if (File.Exists(ITEM_CACHE_FILENAME))
                File.Delete(ITEM_CACHE_FILENAME);
            _itemsReceivedIndex = -1;
        }

        public static void AddToItemCache(int id)
        {
            if (_itemsReceivedIndex < id)
            {
                _itemsReceivedIndex = id;
                SaveItemsReceived();
            }
        }

        private static void SaveItemsReceived()
        {
            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_itemsReceivedIndex));
                File.WriteAllBytes(Path.Combine(rawPath, ITEM_CACHE_FILENAME), bytes);
            }

            SaveExploreItemsReceived();
        }

        private static void LoadItemsReceived()
        {
            if (File.Exists(ITEM_CACHE_FILENAME))
            {
                var reader = File.OpenText(ITEM_CACHE_FILENAME);
                var content = reader.ReadToEnd();
                reader.Close();
                _itemsReceivedIndex = JsonConvert.DeserializeObject<int>(content);
            }
        }
        #endregion

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
            if (itemIndex == null && _locations_checked.Contains(locationId))
            {
                return;
            }

            // If this is a shop location and we've already checked it, we updatea the index and move on
            // This is so we receive items we've bought.
            // This runs into the issue where resyncing will not give store items. 
            // We could avoid the need for this entirely if we make it so purchased items aren't given when bought
            // but instead use the normal randomized item rails. That might be better depending on how playtesting goes.
            if (itemIndex != null && GameData.ShopChecks.ContainsValue(locationId) && _locations_checked.Contains(locationId))
            {
                // Have to do this check, becuase otherwise when resyncing this will reset the item received index back to
                // an earlier value when a store item comes up in the list. This ensures that we only update the item cache
                // for store items IF it was just purchased. Resyncing will skip these items entirely.
                AddToItemCache(itemIndex.Value);
                return;
            }

            // We need to do this here so that we know for sure when a check is done the map is updated
            // If the item action is either sent or acquired, we want to update the minimap. If we received the item then its not from us at all.
            if (action != ItemTransferType.Received)
            {
                Patcher.AddAndUpdateCheckedLocations(locationId);
            }   

            // Do not queue a new item if we've already received that item.
            // Do not queue an item if the queue already contains that index.
            if (itemIndex != null && (itemIndex <= _itemsReceivedIndex || _itemQueue.Any(i => i.ItemIndex == itemIndex)))
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

            if (_combatTraps.Contains(itemName))
            {
                _trapQueue.Enqueue(transfer);
            }
            else
            {
                _itemQueue.Enqueue(transfer);
            }
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
                    // For these, we just want to increment the counter and move on. Nothing else.
                    if (nextItem.ItemName == "Champion Defeated")
                    {
                        APState.ChampionsDefeated++;
                        if (APState.ChampionsDefeated >= 27 && SlotData.Goal == CompletionEvent.Champions)
                        {
                            APState.CompleteGame();
                        }
                        return;
                    }

                    Patcher.UI.AddItemToHistory(nextItem);

                    PopupController.PopupDelegate callback = null;
                    if (_giftActions.ContainsKey(nextItem.LocationID))
                    {
                        callback = () =>
                        {
                            _giftActions[nextItem.LocationID].Finish();
                            _giftActions.TryRemove(nextItem.LocationID, out var action);
                        };
                    }
                    if (_monsterArmyRewards.Contains(nextItem.LocationName))
                    {
                        callback = () =>
                        {
                            _monsterArmyRewards.Remove(nextItem.LocationName);

                            // If we've claimed all army rewards, go back and CheckReward again
                            // This will give us the next tier if we've earned it
                            // or it will unlock the MenuList if not.
                            Patcher.Logger.LogInfo("Monster Callback");
                            Patcher.Logger.LogInfo("Number of Rewards left: " + _monsterArmyRewards.Count());
                            if (_monsterArmyRewards.Count() == 0)
                            {
                                Traverse.Create(UIController.Instance.MonsterArmy).Method("CheckReward").GetValue();
                            }
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
                            callback);

                        // We only want to save items to the item cache if we're receiving the item. 
                        // Do not cache items we send to other people
                        AddToItemCache(nextItem.ItemIndex.Value);
                    }
                }
            }

            private static bool CanGiveItem()
            {
                // If we're in the intro, then don't send items
                if (!ProgressManager.Instance.GetBool("FinishedIntro"))
                    return false;

                // If the monster army menu is up, then we want items to be received if and only if 
                // the menu is locked (i.e. we've already donated and are waiting for the donation reward)
                if (UIController.Instance.MonsterArmy.MenuList.IsOpenOrLocked && _monsterArmyRewards.Count() > 0)
                    return UIController.Instance.MonsterArmy.MenuList.IsLocked;

                return GameStateManager.Instance.IsExploring() && !PlayerController.Instance.IsFalling();
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

                if (!showMessage)
                {
                    _giftQueue.Add(GameData.ItemChecks[locName]);
                    return false;
                }

                APState.CheckLocation(GameData.ItemChecks[locName]);
                _giftActions.TryAdd(GameData.ItemChecks[locName], __instance);

                return false;
            }
        }

        [HarmonyPatch(typeof(MonsterArmyMenu), "GrandReward")]
        private class MonsterArmyMenu_GrandReward
        {
            [UsedImplicitly]
            private static bool Prefix(MonsterArmyMenu __instance)
            {
                Patcher.Logger.LogInfo("GrandReward()");
                var rewardData = Traverse.Create(__instance).Method("GetCurrentReward").GetValue<RewardData>();
                int rewardIndex = __instance.Rewards.IndexOf(rewardData);
                int rewardOffset = 0; // Used to handle the endgame rewards

                // This is only the case if the rewardData is not contained in Rewards, 
                // which means that the reward is one of the repeatable endgame rewards.
                if (rewardIndex == -1)
                {
                    rewardOffset = 31;
                    rewardIndex = __instance.EndgameRewards.IndexOf(rewardData);
                }
                if (rewardIndex == -1)
                {
                    Patcher.Logger.LogError($"Monster army reward data was not found for a point threshold of {rewardData.PointsRequired}");
                    return true;
                }

                var locNames = rewardData.Rewards
                    .Select(i => $"KeeperStronghold_MonsterArmy_{rewardOffset + rewardIndex}_{rewardData.Rewards.IndexOf(i)}")
                    .ToList();

                if (!APState.IsConnected)
                {
                    foreach (var name in locNames)
                        APState.OfflineChecks.Add(name);

                    return true;
                }

                List<string> toRemove = new();
                foreach (var locName in locNames)
                {
                    if (!GameData.ItemChecks.ContainsKey(locName))
                    {
                        Patcher.Logger.LogWarning($"Location '{locName}' does not have a location ID assigned to it");
                        toRemove.Add(locName);
                    }
                }

                locNames = locNames.Except(toRemove).ToList();

                foreach (var name in locNames)
                    Patcher.Logger.LogInfo(name);

                if (locNames.Count == 0)
                {
                    return true;
                }

                // Queue up this location so that we know if we're handling monster army rewards
                _monsterArmyRewards.AddRange(locNames);

                APState.CheckLocations(locNames.Select(l => GameData.ItemChecks[l]).ToArray());

                ++ProgressManager.Instance.MonsterArmyRewardsClaimed;
                AchievementsManager.Instance.OnMonsterArmyRewardClaimed();

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

        public static void ReceiveItem(string itemName, string player, ItemClassification classification, bool self, PopupDelegate confirmCallback)
        {
            // Handle if you send someone else and item and show a message box for that.
            if (itemName == null)
            {
                Logger.LogError("Null item was received");
                return;
            }

            if (IsNonCombatTrap(itemName))
            {
                ProcessTrap(itemName, player, self, confirmCallback);
                return;
            }

            var gold = GetGoldQuantity(itemName);
            if (gold > 0)
            {
                GiveGold(gold, player, self, true, confirmCallback);
                return;
            }

            var quantity = GetQuantityOfItem(ref itemName);
            var newItem = GameData.GetItemByName(itemName);

            if (newItem == null)
            {
                // This shouldn't happen. Might need a smarter way to solve this.
                Logger.LogError($"No item reference was found matching the name '{itemName}'.");
                return;
            }

            GiveItem(newItem, player, classification, self, quantity, confirmCallback);
        }        

        #region Give Items
        static void GiveItem(BaseItem item, string player, ItemClassification classification, bool self, int quantity = 1, PopupDelegate callback = null)
        {
            if (item != null)
            {
                item = Utils.CheckForCostumeReplacement(item);

                PopupController.Instance.ShowMessage(
                    Utils.LOCA("Treasure"),
                    FormatItemReceivedMessage(item.GetName(), quantity, player, classification, self),
                    callback);

                PlayerController.Instance.Inventory.AddItem(item, quantity, 0);
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
