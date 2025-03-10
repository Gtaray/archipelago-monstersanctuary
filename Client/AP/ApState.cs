using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MonsterSanctuary.Client.Persistence;
using Archipelago.MonsterSanctuary.Client.Options;
using System.Security.Cryptography;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public class ApState
    {
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected
        }

        public static int[] AP_VERSION = new int[] { 0, 5, 1 };
        public static ConnectionState State = ConnectionState.Disconnected;
        public static bool IsConnected => State == ConnectionState.Connected;

        public static ArchipelagoSession Session;
        public static bool Authenticated;
        public static bool Completed = false;

        private static DeathLinkService _deathLink;

        public static bool Connect(string hostname, string slotname, string password)
        {
            if (Authenticated)
            {
                return true;
            }

            if (string.IsNullOrEmpty(hostname))
            {
                return false;
            }

            Patcher.Logger.LogInfo($"Connecting to {hostname} as {slotname}...");
            State = ConnectionState.Connecting;

            // Start the archipelago session.
            Session = ArchipelagoSessionFactory.CreateSession(hostname);
            Session.MessageLog.OnMessageReceived += Session_MessageReceived;
            Session.Socket.ErrorReceived += Session_ErrorReceived;
            Session.Socket.SocketClosed += Session_SocketClosed;
            Session.Socket.PacketReceived += Session_PacketReceived;
            Session.Items.ItemReceived += ReceiveItem;

            _deathLink = Session.CreateDeathLinkService();
            _deathLink.OnDeathLinkReceived += (deathLinkObject) => ReceiveDeathLink(deathLinkObject);

            LoginResult loginResult = Session.TryConnectAndLogin(
                "Monster Sanctuary",
                slotname,
                ItemsHandlingFlags.AllItems,
                new Version(AP_VERSION[0], AP_VERSION[1], AP_VERSION[2]),
                password: password);

            if (loginResult is LoginSuccessful loginSuccess)
            {
                Authenticated = true;

                State = ConnectionState.Connected;

                SlotData.LoadSlotData(loginSuccess.SlotData);

                if (SlotData.DeathLink)
                    _deathLink.EnableDeathLink();

                World.LoadMapPins();

                ScoutAllLocations();
            }
            else if (loginResult is LoginFailure loginFailure)
            {
                State = ConnectionState.Disconnected;
                Authenticated = false;
                Patcher.Logger.LogError(string.Join("\n", loginFailure.Errors));
                Session = null;
            }

            return loginResult.Successful;
        }

        private static void ScoutAllLocations()
        {
            // Scout shops and progression item checks
            var allScouts = Locations.GetAllLocations();
            ScoutLocations(allScouts.ToArray(), false, HandleGlobalScoutCallback);
        }

        /// <summary>
        /// Callback method for the global scout that happens when the client connects
        /// Currently only saves information relevant to chest-graphics-matches-contents
        /// </summary>
        /// <param name="packet"></param>
        private static void HandleGlobalScoutCallback(ScoutedItemInfo packet)
        {
            // Instantiate a new random object based on the seed so that trapped chests will always be marked the same.
            var rng = new Random(SlotData.Seed.GetHashCode());

            var flag = (ItemClassification)(int)packet.Flags;
            string locationName = Locations.GetLocationName(packet.LocationId);

            if (flag == ItemClassification.Progression)
            {
                Locations.AddProgressionLocation(locationName);
            }
            else if (flag == ItemClassification.Useful)
            {
                Locations.AddUsefulLocation(locationName);
            }
            else if (flag == ItemClassification.Trap)
            {
                int r = rng.Next(0, 3);
                if (r == 1)
                    Locations.AddProgressionLocation(locationName);
                else if (r == 2)
                    Locations.AddUsefulLocation(locationName);
            }
        }

        static void Session_PacketReceived(ArchipelagoPacketBase packet)
        {
            //Patcher.Logger.LogWarning("Packet Received: " + packet.ToJObject().ToString());
        }
        static void Session_SocketClosed(string reason)
        {
            Patcher.Logger.LogError("Connection to Archipelago lost: " + reason);
            Disconnect();
        }
        static void Session_MessageReceived(LogMessage message)
        {
            Patcher.Logger.LogDebug("Session_MessageReceived");
            Patcher.Logger.LogDebug(message);
        }
        static void Session_ErrorReceived(Exception e, string message)
        {
            Patcher.Logger.LogError("Session_ErrorReceived: " + message);
            if (e != null) Patcher.Logger.LogError(e.ToString());
            InitiateDisconnect();
            Disconnect();
        }

        public static void InitiateDisconnect()
        {
            if (!IsConnected)
                return;

            if (Session != null && Session.Socket != null && Session.Socket.Connected)
            {
                _deathLink.DisableDeathLink();
                Session.Socket.DisconnectAsync();
            }
        }
        public static void Disconnect()
        {
            if (!IsConnected)
                return;

            Authenticated = false;
            State = ConnectionState.Disconnected;
            Session = null;
            ApData.UnloadCurrentFile();
        }

        public static void SendDeathLink()
        {
            if (!IsConnected)
                return;

            if (!SlotData.DeathLink)
                return;

            _deathLink.SendDeathLink(new DeathLink(Session.Players.GetPlayerName(Session.ConnectionInfo.Slot), "lost a combat"));
        }

        /// <summary>
        /// Event handler for receiving a death link notification
        /// Currently does nothing, but may eventually do something.
        /// </summary>
        /// <param name="deathLinkMessage"></param>
        public static void ReceiveDeathLink(DeathLink deathLinkMessage)
        {
            if (!IsConnected)
                return;
        }

        /// <summary>
        /// Event handler for receiving an item from the AP server
        /// This happens outside of the main thread
        /// This event is also raised for every item the player has received when they connecte to the AP server
        /// </summary>
        /// <param name="helper"></param>
        public static void ReceiveItem(ReceivedItemsHelper helper)
        {
            // Don't receive items until we have a loaded data file
            // The only time we would be connected and receive items but not have a data file is when selecting "New Game" from the main menu
            // And in the case of new games, we resync all items when loading the first in-game scene anyway
            if (!ApData.HasApDataFile())
                return;

            int receivedIndex = ApData.GetNextExpectedItemIndex();
            int receivedCount = Session.Items.AllItemsReceived.Count();

            // When we receive items, we want to ensure that we process them in the correct order and never skip an index
            // We do this by only processing items between the currently received index and the number of all received items
            // We effectively ignore the helper parameter, and instead only use this as a trigger to process all un-received items
            for (int i = receivedIndex; i < receivedCount; i++)
            {
                var item = Session.Items.AllItemsReceived[i];
                Items.QueueItemForDelivery(i, item);
            }
        }

        /// <summary>
        /// Sends a message to the AP server to check a location and deliver the item to the appropriate player
        /// </summary>
        /// <param name="locationId"></param>
        public static void CheckLocation(long locationId)
        {
            CheckLocations(locationId);
        }

        /// <summary>
        /// Sends a message to the AP server to check one or more locations and deliver the item to the appropriate player
        /// </summary>
        /// <param name="locationIds"></param>
        public static void CheckLocations(params long[] locationIds)
        {
            if (!IsConnected)
                return;
             
            // Last line of defense to not check places that have already been checked
            var locationsToCheck = locationIds.Except(Session.Locations.AllLocationsChecked);

            Task.Run(() =>
            {
                Session.Locations.CompleteLocationChecksAsync(locationsToCheck.ToArray());
            }).ConfigureAwait(false);

            Task.Run(() => NotifyPlayerOfSentChecks(locationsToCheck.ToArray()));
        }

        /// <summary>
        /// Peeks at what items are at a list of location ids, and then places those results as messages on a notification queue
        /// Used to deliver UI updates to the player to notify them of the items they're finding in their world
        /// </summary>
        /// <param name="locationsToCheck"></param>
        /// <returns></returns>
        private static async Task NotifyPlayerOfSentChecks(long[] locationsToCheck)
        {
            var packet = await Session.Locations.ScoutLocationsAsync(false, locationsToCheck);
            foreach (var scout in packet.Values)
            {
                // We only want to notify the player when they send another player an item
                // Notifying when they receive an item is done elsewhere
                if (scout.Player !=  Session.Players.ActivePlayer)
                    Notifications.QueueItemTransferNotification(scout, ItemTransferType.Sent);
            }
        }

        public static void ScoutLocations(long location, bool scoutAsHint = false, System.Action<ScoutedItemInfo> callback = null)
        {
            ScoutLocations(new long[] { location }, scoutAsHint, callback);
        }

        public static void ScoutLocations(string[] locationNames, bool scoutAsHint = false, System.Action<ScoutedItemInfo> callback = null)
        {
            var locations = locationNames.Select(l => Session.Locations.GetLocationIdFromName("Monster Sanctuary", l));
            ScoutLocations(locations.ToArray(), scoutAsHint, callback);
        }

        public static void ScoutLocations(long[] locations, bool scoutAsHint = false, System.Action<ScoutedItemInfo> callback = null)
        {
            var task = Task.Run(() => ProcessScoutRequest(locations, scoutAsHint, callback));
        }

        private static async Task ProcessScoutRequest(long[] locations, bool asHint, System.Action<ScoutedItemInfo> callback)
        {
            var packets = await Session.Locations.ScoutLocationsAsync(asHint, locations);
            foreach (KeyValuePair<long, ScoutedItemInfo> kvp in packets)
            {
                if (callback != null)
                    callback(kvp.Value);
            }
        }

        /// <summary>
        /// Returns true if the AP server has marked a given location as checked
        /// </summary>
        /// <param name="locationId"></param>
        /// <returns></returns>
        public static bool HasApReceivedLocationCheck(long locationId)
        {
            return Session.Locations.AllLocationsChecked.Contains(locationId);
        }

        /// <summary>
        /// Returns an item name given a game and an item ID
        /// </summary>
        /// <param name="game"></param>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public static string GetItemName(string game, long itemId)
        {
            return Session.Items.GetItemName(itemId, game);
        }

        public static void CompleteGame()
        {
            if (!IsConnected)
                return;

            var statusUpdatePacket = new StatusUpdatePacket
            {
                Status = ArchipelagoClientState.ClientGoal
            };
            Session.Socket.SendPacket(statusUpdatePacket);
            Completed = true;
        }

        public static bool ReadBoolFromDataStorage(string key)
        {
            var value = Session.DataStorage[Scope.Slot, key].To<bool?>();
            return value ?? false;
        }

        public static void SetToDataStorage(string key, DataStorageElement value)
        {
            Session.DataStorage[Scope.Slot, key] = value;
        }
    }
}
