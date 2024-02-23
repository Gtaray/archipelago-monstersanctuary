using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private static List<GameObject> _modified_items = new();

        public static void ClearModifiedShopItems()
        {
            foreach (var item in _modified_items)
            {
                // For foreign items, we just destroy the item
                if (item.GetComponent<ForeignItem>() != null)
                {
                    Destroy(item);
                }
                // For local items, we just destroy the randomized shop item component
                else 
                {
                    Destroy(item.GetComponent<RandomizedShopItem>());
                }                
            }
            _modified_items.Clear();
        }

        public static void AddShopItem(NetworkItem packet)
        {
            var locationName = APState.Session.Locations.GetLocationNameFromId(packet.Location);
            var shopName = DetermineShopFromLocationName(locationName);
            var oldItemName = locationName.Split('-').Last().Trim();
            var newItemName = APState.Session.Items.GetItemName(packet.Item);

            var invItem = new ShopInventoryItem()
            {
                Player = APState.Session.Players.GetPlayerName(packet.Player),
                IsLocal = packet.Player == APState.Session.ConnectionInfo.Slot,
                Name = newItemName,
                LocationId = packet.Location,
                Classification = (ItemClassification)((int)packet.Flags)
            };

            Patcher.Logger.LogInfo($"Added item to {shopName}:");
            Patcher.Logger.LogInfo($"\t{oldItemName} -> {invItem.Name} ({Enum.GetName(typeof(ItemClassification), invItem.Classification)})");

            if (shopName == null)
            {
                Patcher.Logger.LogError($"Shop name '{shopName}' is not recognized");
                return;
            }

            // Something about interacting with these dictionaries from within a background task
            // causes the whole for-loop to break
            if (!GameData.Shops.ContainsKey(shopName))
            {
                GameData.Shops[shopName] = new Shop();
            }

            GameData.Shops[shopName].AddItem(oldItemName, invItem);
        }

        // This probably won't work, because we can't return a game object for 
        // items from other games.
        [HarmonyPatch(typeof(TradeMenu), "UpdateMenuList")]
        private class TradeMenu_UpdateMenuList
        {
            private static bool Prefix(TradeMenu __instance)
            {
                if (!APState.IsConnected)
                {
                    return true;
                }

                // First thing's first, clear the previous list
                __instance.PagedMenuList.Clear();
                ClearModifiedShopItems(); // We need to make sure this gets cleared out every time a shop is opened

                // Don't fuck with selling
                var sell = Traverse.Create(__instance).Field("sell").GetValue<bool>();
                Patcher.Logger.LogInfo("sell: " + sell);
                if (sell)
                {
                    Patcher.Logger.LogInfo("Selling");
                    return true;
                }

                // Don't mess with the PVP coupon trader
                var inventories = __instance.CurrentTrader.GetComponents<TradeInventory>();
                if (inventories.Any(i => i.CouponTrader))
                {
                    return true;
                }

                // First determine which shop this is
                string shop = DetermineShop(__instance.CurrentTrader);
                if (shop == null || !GameData.Shops.ContainsKey(shop))
                {
                    Patcher.Logger.LogError($"Could not determine shop inventory in room {GameController.Instance.CurrentSceneName}");
                    return true;
                }

                // Start by getting a list of all items the shop would normally sell.
                List<GameObject> shopInventory = BuildBaseInventory(inventories);
                shopInventory = SwapInventory(shopInventory, shop);

                foreach (var item in shopInventory)
                {
                    var rsi = item.GetComponent<RandomizedShopItem>();
                    var local = item.GetComponent<ForeignItem>() != null;

                    // Ignore foreign items that have already been checked
                    if (!local && _locations_checked.Contains(rsi.LocationId))
                    {
                        continue;
                    }

                    _modified_items.Add(item);
                    __instance.PagedMenuList.AddDisplayable(item.GetComponent<BaseItem>());
                }

                return false;
            }

            private static List<GameObject> BuildBaseInventory(TradeInventory[] inventories)
            {
                List<GameObject> shopInventory = new List<GameObject>();
                foreach (var inventory in inventories.Reverse())
                {
                    // If this inventory is available, move on
                    if (!inventory.IsAvailable())
                    {
                        continue;
                    }

                    foreach (GameObject tradeItem in inventory.GetTradeItems())
                    {
                        shopInventory.Add(tradeItem);
                    }
                }

                return shopInventory;
            }

            private static List<GameObject> SwapInventory(List<GameObject> shopInventory, string shop)
            {
                List<GameObject> newInventory = new List<GameObject>();
                var randomizedInventory = GameData.Shops[shop];

                // Go through the inventory and swap items with the randomized one
                foreach (var shopItem in shopInventory)
                {
                    var baseItem = shopItem.GetComponent<BaseItem>();

                    // Sanity check to make sure the item we're replacing is actually in the shop
                    if (!randomizedInventory.HasItem(baseItem.GetName()))
                    {
                        Patcher.Logger.LogError($"Randomized inventory for shop '{shop}' does not contain the source item '{baseItem.GetName()}'");
                        continue;
                    }

                    var randomizedItem = randomizedInventory.GetItem(baseItem.GetName());

                    GameObject newItem = randomizedItem.IsLocal
                        ? GameData.GetItemByName<BaseItem>(randomizedItem.Name).gameObject
                        : Instantiate(baseItem.gameObject);

                    if (!randomizedItem.IsLocal)
                    {
                        Destroy(newItem.GetComponent<BaseItem>()); // Destroy the original item component

                        // Add the new component. Make sure to pull data from tradeItem or baseItem from here on out
                        var item = newItem.AddComponent<ForeignItem>();
                        item.Classification = randomizedItem.Classification;
                        item.Player = randomizedItem.Player;
                        item.Name = randomizedItem.Name;
                    }

                    newItem.GetComponent<BaseItem>().Price = baseItem.Price;
                    var rsi = newItem.AddComponent<RandomizedShopItem>();
                    rsi.LocationId = randomizedItem.LocationId;

                    newInventory.Add(newItem);
                }

                return newInventory;
            }
        }

        [HarmonyPatch(typeof(TradePopup), "Open")]
        public class TradePopup_Open 
        { 
            private static void Prefix(BaseItem item, ref int maxQuantity)
            {
                if (!APState.IsConnected)
                {
                    return;
                }

                // Can only ever buy a single foreign item
                if (item is ForeignItem)
                {
                    maxQuantity = 1;
                }
            }
        }

        [HarmonyPatch(typeof(TradePopup), "OnItemSelected")]
        public class TradePopup_OnItemSelected
        {
            // This is specifically to prevent giving a foreign item to the player
            // and to handle eggs (which the base game doesn't handle if it's not randomizer or NG+)
            // Actually doing the location check will occur in the postfix
            private static bool Prefix(TradePopup __instance, MenuListItem item)
            {
                if (!APState.IsConnected)
                {
                    return true;
                }

                // Don't muck with canceling out of the menu
                if (item != __instance.ConfirmItem)
                    return true;

                // Don't muck with selling
                if (Traverse.Create(__instance).Field("Sell").GetValue<bool>())
                    return true;

                // Don't muck with coupon trader
                if (UIController.Instance.TradeMenu.CouponTrader)
                    return true;

                var baseItem = Traverse.Create(__instance).Field("Item").GetValue<BaseItem>();
                if (baseItem == null)
                    return true;

                // If the player buys an egg, mark off that it's been encountered
                if (baseItem is Egg)
                {
                    Egg egg = (Egg)baseItem;
                    ProgressManager.Instance.EncounteredMonster(egg.Monster.GetComponent<Monster>());
                }

                // Don't muck with anything that's not a foreign item
                if (baseItem is not ForeignItem)
                    return true;

                PlayerController.Instance.Gold -= Traverse.Create(__instance).Method("GetItemPrice").GetValue<int>();

                // If this was bought from a shop with limited inventory, make sure to mark that it was bought
                foreach (TradeInventory component in UIController.Instance.TradeMenu.CurrentTrader.GetComponents<TradeInventory>())
                {
                    if (component.IsAvailable() && component.OnItemSold(baseItem.gameObject))
                        break;
                }
                SFXController.Instance.PlaySFX(SFXController.Instance.SFXBuySell, 0.7f);
                __instance.Close();
                return false;
            }

            private static void Postfix(TradePopup __instance, MenuListItem item)
            {
                if (!APState.IsConnected)
                {
                    return;
                }

                // Don't muck with canceling out of the menu
                if (item != __instance.ConfirmItem)
                    return;

                // Don't muck with selling
                if (Traverse.Create(__instance).Field("Sell").GetValue<bool>())
                    return;

                // Don't muck with coupon trader
                if (UIController.Instance.TradeMenu.CouponTrader)
                    return;

                var baseItem = Traverse.Create(__instance).Field("Item").GetValue<BaseItem>();
                if (baseItem == null)
                    return;

                var rsi = baseItem.GetComponent<RandomizedShopItem>();
                if (rsi == null)
                    return;

                var foreignItem = baseItem.GetComponent<ForeignItem>();

                var isLocal = foreignItem != null;
                Patcher.Logger.LogInfo($"{baseItem.GetName()} was purchased. Check location {rsi.LocationId}");

                // We mark off this location as checked immediately so that when we get the checked location callback
                // we don't queue up an item transfer to happen. We don't want a message box pop up in this case.
                // We want to manually add to our item history list
                Patcher.AddAndUpdateCheckedLocations(rsi.LocationId);
                Patcher.UI.AddItemToHistory(new ItemTransfer()
                {
                    PlayerName = isLocal ? "" : foreignItem.Player, // Only need a name if we're sending an item
                    ItemName = baseItem.GetName(),
                    Action = isLocal ? ItemTransferType.Aquired : ItemTransferType.Sent
                });
                APState.CheckLocation(rsi.LocationId);
            }
        }

        private static string DetermineShopFromLocationName(string locationName)
        {

            if (locationName.StartsWith("Treasure Hunter"))
            {
                return "TreasureHunter";
            }
            else if (locationName.StartsWith("Equipment Merchant"))
            {
                return "EquipmentMerchant";
            }
            else if (locationName.StartsWith("Food Merchant"))
            {
                return "FoodMerchant";
            }
            else if (locationName.StartsWith("Consumable Merchant"))
            {
                return "ConsumableMerchant";
            }
            else if (locationName.StartsWith("Traveling Merchant"))
            {
                return "TravelingMerchant";
            }
            else if (locationName.StartsWith("Rhazes"))
            {
                return "Rhazes";
            }
            else if (locationName.StartsWith("Goblin Trader"))
            {
                return "GoblinTrader";
            }
            else if (locationName.StartsWith("Golem Merchant"))
            {
                return "GolemMerchant";
            }

            return null;
        }

        private static string DetermineShop(GameObject trader)
        {

            if (GameController.Instance.CurrentSceneName == "MountainPath_Center3")
            {
                return "TreasureHunter";
            }
            else if (GameController.Instance.CurrentSceneName == "KeeperStronghold_Shops")
            {
                if (trader.name == "MerchantEquipment")
                    return "EquipmentMerchant";
                else if (trader.name == "MerchantFood")
                    return "FoodMerchant";
                else if (trader.name == "MerchantItems")
                    return "ConsumableMerchant";
            }
            else if (GameController.Instance.CurrentSceneName == "AncientWoods_TreeOfEvolution")
            {
                return "TravelingMerchant";
            }
            else if (GameController.Instance.CurrentSceneName == "MagmaChamber_AlchemistLab")
            {
                return "Rhazes";
            }
            else if (GameController.Instance.CurrentSceneName == "MagmaChamber_GoblinTrader")
            {
                return "GoblinTrader";
            }
            else if (GameController.Instance.CurrentSceneName == "MysticalWorkshop_GolemMerchant")
            {
                return "GolemMerchant";
            }

            return null;
        }
    }
}
