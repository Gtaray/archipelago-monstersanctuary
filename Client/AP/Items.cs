using Archipelago.MonsterSanctuary.Client.Behaviors;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public int PlayerID { get; set; }
        public ItemClassification ItemClassification { get; set; }
        public long LocationID { get; set; }
    }

    public class Items
    {
        #region Skipped Gift Checks
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
        #endregion

        #region Item Queue
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
                apItem.Player,
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
            int playerId,
            ItemClassification classification,
            long locationId)
        {
            // Special note, we enqueue items that the player has already received
            // We do this so that the recieved message can place on the queue irrespective of whether the player has loaded their save (an AP data file is set)
            // We just need to make sure to filter out those items in the handler

            // If we try to queue an item that's already queued, don't
            if (ItemQueue.Any(i => i.ItemIndex == itemIndex))
                return;

            ItemTransfer itemTransfer = new()
            { 
                ItemIndex = itemIndex,
                ItemName = itemName,
                PlayerID = playerId,
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

            // This method runs when an AP connection is established, and when creating a new file, that can happen before save file is selected and an AP data file is created
            // So we just need to bail early when that happens
            if (!ApData.HasApDataFile())
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

            // We shouldn't be receiving checks unless we have a data file loaded
            // and are ready to handle the responses
            if (!ApData.HasApDataFile())
                return;

            var itemReceivedIndex = ApData.GetNextExpectedItemIndex();

            // Start at index 1 because 0 means something special and we should ignore it.
            for (int i = 0; i < ApState.Session.Items.AllItemsReceived.Count(); i++)
            {
                // Only queue up items that haven't already been received
                if (i < itemReceivedIndex)
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
        #endregion

        #region New Items
        public static List<string> ItemIcons = new();
        public static List<NewItem> NewItems = new();

        public static IEnumerable<NewItem> GetNewItems() => NewItems;


        public static void Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Archipelago.MonsterSanctuary.Client.data.item_icons.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                ItemIcons = JsonConvert.DeserializeObject<List<string>>(json);
            }

            using (Stream stream = assembly.GetManifestResourceStream("Archipelago.MonsterSanctuary.Client.data.new_items.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                NewItems = JsonConvert.DeserializeObject<List<NewItem>>(json);
                Patcher.Logger.LogInfo($"Loaded {NewItems.Count()} new items");
            }
        }
        #endregion
    }

    public class NewItem
    {
        public BaseItem Item { get; set; }

        public string GameObjectName { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = "SOMEONE IS A DOOFUS AND FORGOT TO PUT IN A DESCRIPTION";
        public string Icon { get; set; }
        public string Type { get; set; }
        public ItemClassification Classification { get; set; }
        public TrapTrigger TrapTrigger { get; set; }
    }
}
