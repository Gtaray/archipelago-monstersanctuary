using Archipelago.MonsterSanctuary.Client.Persistence;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;
using static System.Collections.Specialized.BitVector32;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public enum ItemTransferType
    {
        /// <summary>
        /// This player found their own item
        /// </summary>
        Acquired = 0,

        /// <summary>
        /// Someone else found this player's item
        /// </summary>
        Received = 1,

        /// <summary>
        /// This player found someone else's item
        /// </summary>
        Sent = 2
    }

    // TODO: Trim this class to only include things that the notifications need
    public class ItemTransferNotification
    {
        public long LocationID { get; set; }
        public int PlayerID { get; set; }
        public string PlayerName { get; set; }
        public string ItemName { get; set; }
        public ItemTransferType Action { get; set; }
        public ItemClassification Classification { get; set; }
    }

    public class Notifications
    {
        public static ConcurrentQueue<ItemTransferNotification> NotificationQueue { get; set; }

        /// <summary>
        /// Adds a new entry to the notification queue for a sent, received, or acquired item
        /// This queue is used to notify the player when an item is sent or received
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <param name="apItem"></param>
        public static void QueueItemTransferNotification(ItemInfo apItem)
        {
            QueueItemTransferNotification(
                    apItem.LocationGame,
                    apItem.ItemId,
                    apItem.Player,
                    apItem.LocationId,
                    (ItemClassification)(int)apItem.Flags,
                    ApState.Session.Players.ActivePlayer == apItem.Player
                        ? ItemTransferType.Acquired
                        : ItemTransferType.Sent);
        }

        /// <summary>
        /// Adds a new entry to the notification queue for a sent, received, or acquired item
        /// This queue is used to notify the player when an item is sent or received
        /// </summary>
        public static void QueueItemTransferNotification(
            string itemGame, 
            long itemId, 
            int playerId, 
            long locationId, 
            ItemClassification classification, 
            ItemTransferType action)
        {
            // Do not queue up a notification if the queue already has a message for the same location ID and player ID
            if (NotificationQueue.Any(n => n.LocationID == locationId && n.PlayerID == playerId))
                return;

            var itemName = ApState.GetItemName(itemGame, itemId);
            var transfer = new ItemTransferNotification()
            {
                ItemName = itemName,
                PlayerID = playerId,
                PlayerName = ApState.Session.Players.GetPlayerName(playerId),
                LocationID = locationId,
                Classification = classification,
                Action = action
            };

            NotificationQueue.Enqueue(transfer);
        }

        /// <summary>
        /// Dequeues the next item in the notification queue
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public static bool TakeNextNotification(out ItemTransferNotification next)
        {
            bool success = NotificationQueue.TryDequeue(out ItemTransferNotification nextItem);
            next = nextItem;
            return success;
        }
    }
}
