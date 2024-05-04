using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;
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
        private static List<long> _monsterArmyRewards = new();

        #region Queue Functions
        // Because both items sent and received use the same pop up system to inform the player
        // we use this item queue to merge item received messages from AP as well as items sent
        // messages from the client.
        private static ConcurrentQueue<ItemTransfer> _itemQueue = new();

        // When skipping dialog that gives the player multiple items, calling CheckLocation back to back that fast
        // can cause problems, especially when finding other players' items. In those cases we batch all item checks
        // until the player is able to move again, then check all of them at once.
        private static List<long> _giftQueue = new();

        public static bool ItemQueueHasIndex(int itemIndex)
        {
            return _itemQueue.Any(i => i.ItemIndex == itemIndex);
        }

        public static void QueueItemTransfer(int? itemIndex, long itemId, int playerId, long locationId, ItemClassification classification, ItemTransferType action)
        {
            var itemName = APState.Session.Items.GetItemName(itemId);

            // If this is a shop location and we've already checked it, we update the index and move on
            // This is so we receive items we've bought.
            // This runs into the issue where resyncing will not give store items. 
            // We could avoid the need for this entirely if we make it so purchased items aren't given when bought
            // but instead use the normal randomized item rails. That might be better depending on how playtesting goes.
            if (action == ItemTransferType.Aquired && itemIndex != null && GameData.ShopChecks.ContainsValue(locationId) && Persistence.Instance.LocationsChecked.Contains(locationId))
            {
                // Have to do this check, becuase otherwise when resyncing this will reset the item received index back to
                // an earlier value when a store item comes up in the list. This ensures that we only update the item cache
                // for store items IF it was just purchased. Resyncing will skip these items entirely.
                Persistence.AddToItemCache(itemIndex.Value);
                return;
            }

            // Do not queue a new item if we've already received that item.
            // Do not queue an item if the queue already contains that index.
            if (itemIndex != null && _itemQueue.Any(i => i.ItemIndex == itemIndex))
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

                if (_itemQueue.TryDequeue(out ItemTransfer nextItem))
                {
                    Patcher.Logger.LogInfo("Dequeueing " + nextItem.ItemName);
                    // For these, we simply need to check if our total is 27. We track this when the champion is defeated
                    // before the check is sent to AP.
                    if (nextItem.ItemName == "Champion Defeated")
                    {
                        if (Persistence.Instance.ChampionsDefeated.Count() >= 27 && SlotData.Goal == CompletionEvent.Champions)
                        {
                            APState.CompleteGame();
                        }
                        return;
                    }

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
                    if (_monsterArmyRewards.Contains(nextItem.LocationID))
                    {
                        callback = () =>
                        {
                            _monsterArmyRewards.Remove(nextItem.LocationID);

                            // If we've claimed all army rewards, go back and CheckReward again
                            // This will give us the next tier if we've earned it
                            // or it will unlock the MenuList if not.
                            if (_monsterArmyRewards.Count() == 0)
                            {
                                CheckArmyRewards();
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
                            eggShift,
                            callback);
                    }

                    // Show the item in the tracker, and add it to the persistence file
                    // We do this at the end to make sure the player has actually received the item before 
                    // saving it to the file.
                    Patcher.UI.AddItemToHistory(nextItem);
                    if (nextItem.ItemIndex.HasValue)
                    {
                        Persistence.AddToItemCache(nextItem.ItemIndex.Value);
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
                {
                    return !PopupController.Instance.IsOpen && UIController.Instance.MonsterArmy.MenuList.IsLocked;
                }
                
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

                // Clear the dialog after opening the special chest that would contain the key of power
                if (__instance.ID == 32100074)
                    __instance.Connections.Clear();

                // if the gift comes from one of the blob burg chests that opens up walls
                // then we want to skip the extra scripted components if OpenBlobBurg is set in the slot data
                var isBlobBurgChest = (GameController.Instance.CurrentSceneName == "BlobBurg_Center2" ||
                    GameController.Instance.CurrentSceneName == "BlobBurg_East5" ||
                    GameController.Instance.CurrentSceneName == "BlobBurg_Center3" ||
                    GameController.Instance.CurrentSceneName == "BlobBurg_South2" ||
                    GameController.Instance.CurrentSceneName == "BlobBurg_West2" ||
                    GameController.Instance.CurrentSceneName == "BlobBurg_Champion");
                var openBlobBurg = SlotData.OpenBlobBurg == OpenWorldSetting.Interior || SlotData.OpenBlobBurg == OpenWorldSetting.Full;
                if (openBlobBurg && isBlobBurgChest)
                {
                    __instance.Connections.Clear();
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(MonsterArmyMenu), "GrandReward")]
        private class MonsterArmyMenu_GrandReward
        {
            [UsedImplicitly]
            private static bool Prefix(MonsterArmyMenu __instance)
            {
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

                if (locNames.Count == 0)
                    return true;

                // At this point we know we're supposed to have received a reward, so we can handle these bits
                ++ProgressManager.Instance.MonsterArmyRewardsClaimed;
                AchievementsManager.Instance.OnMonsterArmyRewardClaimed();

                var locIds = locNames.Select(l => GameData.ItemChecks[l]).Except(Persistence.Instance.LocationsChecked);

                // Queue up this location so that we know if we're handling monster army rewards
                _monsterArmyRewards.AddRange(locIds);

                if (locIds.Count() == 0)
                {
                    CheckArmyRewards();
                }
                else
                {
                    APState.CheckLocations(locIds.ToArray());
                }

                return false;
            }
        }
        #endregion

        #region Chest Matches Content
        private static ConcurrentBag<long> _progressionItemChests = new();

        public static void AddProgressionItemChest(NetworkItem packet) 
        {
            var classification = (ItemClassification)(int)packet.Flags;
            if (classification == ItemClassification.Progression)
            {
                _progressionItemChests.Add(packet.Location);
            }
        }

        [HarmonyPatch(typeof(Chest), "Start")]
        private class Chest_Start
        {
            private static void Postfix(Chest __instance)
            {
                if (!APState.IsConnected)
                    return;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                if (!GameData.ItemChecks.ContainsKey(locName))
                    return;

                var locId = GameData.ItemChecks[locName];
                var progression = _progressionItemChests.Contains(locId);

                int baseSprite = 0;
                if (progression)
                {
                    baseSprite = 33;
                }

                __instance.GetComponent<tk2dSprite>().SetSprite(baseSprite);
            }
        }

        [HarmonyPatch(typeof(Chest), "Interact")]
        private class Chest_Interact
        {
            private static void Prefix(Chest __instance)
            {
                if (!APState.IsConnected)
                    return;

                string locName = $"{GameController.Instance.CurrentSceneName}_{__instance.ID}";
                if (!GameData.ItemChecks.ContainsKey(locName))
                    return;

                var locId = GameData.ItemChecks[locName];
                var progression = _progressionItemChests.Contains(locId);

                int baseSprite = 0;
                if (progression)
                {
                    baseSprite = 33;
                }

                var animator = __instance.GetComponent<tk2dSpriteAnimator>();
                var closed = animator.GetClipByName("closed");
                var opening = animator.GetClipByName("opening");
                var open = animator.GetClipByName("open");

                // Closed
                closed.frames[0].spriteId = baseSprite;
                // Opening
                opening.frames[0].spriteId = baseSprite;
                opening.frames[1].spriteId = baseSprite + 1;
                opening.frames[2].spriteId = baseSprite + 2;
                // Open
                open.frames[0].spriteId = baseSprite + 2;
            }
        }
        #endregion

        public static void CheckArmyRewards()
        {
            Traverse.Create(UIController.Instance.MonsterArmy)
                .Method("CheckReward")
                .GetValue();
        }

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

                // Whenever we receive an explore item we have to save it
                if (item is ExploreAbilityItem)
                {
                    Patcher.Logger.LogInfo(item.Name + " is explore ability item");
                    Persistence.Instance.ExploreItems.Add(item.GetName());
                }
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

        public static UnityEngine.Color GetItemColorCode(ItemClassification classification)
        {
            if (classification == ItemClassification.Progression)
            {
                return Colors.ProgressionItemColor;
            }
            else if (classification == ItemClassification.Useful)
            {
                return Colors.UsefulItemColor;
            }
            else if (classification == ItemClassification.Trap)
            {
                return Colors.TrapItemColor;
            }

            return Colors.FillerItemColor;
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
