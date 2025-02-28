using Archipelago.MonsterSanctuary.Client.Persistence;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public enum ItemClassification
    {
        Filler = 0,
        Progression = 1,
        Useful = 2,
        Trap = 4
    }

    public class ItemTransfer 
    { 
        public int ItemIndex { get; set; }
        public string ItemName { get; set; }
        public ItemClassification ItemClassification { get; set; }
        public long LocationID { get; set; }
    }

    public class Items
    {
        /// <summary>
        /// A list of locations to check that are batched together when skipping dialog that has multiple checks.
        /// When skipping dialog that gives the player multiple items, calling CheckLocation back to back that fast can cause problems, especially when finding other players' items. In those cases we batch all item checks until the player is able to move again, then check all of them at once.
        /// </summary>
        public static HashSet<long> SkippedGiftChecks = new();

        /// <summary>
        /// Adds a location ID to the list of skipped gift checks
        /// </summary>
        /// <param name="locationId"></param>
        public static void AddSkippedGiftCheck(long locationId) => SkippedGiftChecks.Add(locationId);

        /// <summary>
        /// Clears all skipped gift checks
        /// </summary>
        public static void ClearSkippedGiftChecks() => SkippedGiftChecks.Clear();

        /// <summary>
        /// Gets all skipped gift checks
        /// </summary>
        /// <returns></returns>
        public static List<long> GetSkippedGiftChecks() => SkippedGiftChecks.ToList();

        /// <summary>
        /// Returns true if there are any skipped gift checks
        /// </summary>
        /// <returns></returns>
        public static bool HasSkippedGiftChecks() => SkippedGiftChecks.Count() > 0;

        /// <summary>
        /// A merged queue that handles both items sent to other players, and items received
        /// </summary>
        public static ConcurrentQueue<ItemTransfer> ItemQueue = new();

        /// <summary>
        /// Queues an item to be given to player
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <param name="apItem"></param>
        public static void QueueItemForDelivery(int itemIndex, ItemInfo apItem)
        {
            var name = ApState.Session.Items.GetItemName(apItem.ItemId);
            var classification = (ItemClassification)(int)apItem.Flags;

            QueueItemForDelivery(
                itemIndex, 
                name, 
                classification, 
                apItem.LocationId);
        }

        /// <summary>
        /// Queues an item to be given to the player
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <param name="itemName"></param>
        /// <param name="classification"></param>
        public static void QueueItemForDelivery(
            int itemIndex, 
            string itemName, 
            ItemClassification classification,
            long locationId)
        {
            // Item index should never be null because this is only called for items we receive

            // If we try to queue an item that we've already received, don't
            if (ApData.IsItemReceived(itemIndex))
                return;

            // If we try to queue an item that's already queued, don't
            if (ItemQueue.Any(i => i.ItemIndex == itemIndex))
                return;

            ItemTransfer itemTransfer = new()
            { 
                ItemIndex = itemIndex,
                ItemName = itemName,
                ItemClassification = classification,
                LocationID = locationId
            };

            ItemQueue.Enqueue(itemTransfer);
        }

        /// <summary>
        /// Dequeues the next item in the itme queue
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public static bool TakeNextItem(out ItemTransfer next)
        {
            bool success = ItemQueue.TryDequeue(out ItemTransfer nextItem);
            next = nextItem;
            return success;
        }

        /// <summary>
        /// Fully resyncs all locations this client has checked
        /// </summary>
        public static void ResyncSentItems()
        {
            if (!ApState.IsConnected)
                return;

            var toCheck = ApData.GetCheckedLocationsAsIds()
                .Except(ApState.Session.Locations.AllLocationsChecked);

            ApState.CheckLocations(toCheck.ToArray());
        }

        /// <summary>
        /// Fully resyncs all items that this client has received
        /// </summary>
        public static void ResyncReceivedItems()
        {
            if (!ApState.IsConnected)
                return;

            var itemReceivedIndex = ApData.GetItemsReceived();

            // Start at index 1 because 0 means something special and we should ignore it.
            for (int i = 1; i < ApState.Session.Items.AllItemsReceived.Count(); i++)
            {
                // Only queue up items that haven't already been received
                if (i <= itemReceivedIndex)
                    continue;

                var item = ApState.Session.Items.AllItemsReceived[i];
                QueueItemForDelivery(i, item);
            }
        }

        /// <summary>
        /// Fully resyncs all items this client has received and locations this client has checked
        /// </summary>
        public static void ResyncAllItems()
        {
            ResyncReceivedItems();
            ResyncSentItems();
        }
    }
}
