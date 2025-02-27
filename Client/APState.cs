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

namespace Archipelago.MonsterSanctuary.Client
{
    public class APState
    {
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected
        }

        // These are cached when we connect simply so we have easy access to them
        // Mostly for double checking that when we load a save file, we're connected to the right AP server
        public static string HostName { get; set; }
        public static string SlotName { get; set; }

        public static int[] AP_VERSION = new int[] { 0, 5, 1 };
        public static ConnectionState State = ConnectionState.Disconnected;
        public static bool IsConnected => State == ConnectionState.Connected;

        public static ArchipelagoSession Session;
        public static bool Authenticated;
        public static bool Completed = false;
        public static HashSet<string> OfflineChecks = new HashSet<string>(); // Keeps track of locations that were checked while offline
        public static HashSet<long> CheckedLocations = new HashSet<long>(); // Keeps track of checked locations for the current session. Does not persist

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

            APState.HostName = hostname;
            APState.SlotName = slotname;

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

                GameData.LoadMinimap();
                ApData.RebuildCheckCounter();

                // If the player opened chests while not connected, this get those items upon connection
                if (OfflineChecks.Count() > 0)
                {
                    Patcher.Logger.LogInfo("Number of Locations Checked While Offline: " + OfflineChecks.Count());
                    var ids = OfflineChecks.Select(loc => GameData.ItemChecks[loc]).ToArray();
                    CheckLocations(ids);
                }

                Resync();
            }
            else if (loginResult is LoginFailure loginFailure)
            {
                State = ConnectionState.Disconnected;
                Authenticated = false;
                Patcher.Logger.LogError(String.Join("\n", loginFailure.Errors));
                Session = null;
            }

            return loginResult.Successful;
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
        }

        public static void InitiateDisconnect()
        {
            if (!APState.IsConnected)
                return;

            if (Session != null && Session.Socket != null && Session.Socket.Connected)
            {
                _deathLink.DisableDeathLink();
                Session.Socket.DisconnectAsync();
            }
        }
        public static void Disconnect()
        {
            if (!APState.IsConnected)
                return;

            Authenticated = false;
            State = ConnectionState.Disconnected;
            Session = null;
            ApData.UnloadCurrentFile();
        }

        public static void SendDeathLink()
        {
            if (!APState.IsConnected)
                return;

            if (!SlotData.DeathLink)
                return;

            _deathLink.SendDeathLink(new DeathLink(Session.Players.GetPlayerName(Session.ConnectionInfo.Slot), "lost a combat"));
        }

        public static void ReceiveDeathLink(DeathLink deathLinkMessage)
        {
            if (!APState.IsConnected)
                return;

            // Nothing to do here
        }

        public static void Resync()
        {
            if (!APState.IsConnected)
                return;

            for (int i = 1; i < Session.Items.AllItemsReceived.Count();  i++)
            {
                var item = Session.Items.AllItemsReceived[i];

                var action = Session.ConnectionInfo.Slot == item.Player
                    ? ItemTransferType.Aquired // We found our own item
                    : ItemTransferType.Received; // Someone else found our item
                var classification = (ItemClassification)((int)item.Flags);
                Patcher.QueueItemTransfer(i, item.ItemGame, item.ItemId, item.Player, item.LocationId, classification, action);
            }
        }
        
        public static void ReceiveItem(ReceivedItemsHelper helper)
        {
            // I guess we're supposed to ignore index 0, as that's special and means something else.
            if (helper.Index == 0)
                return;

            var item = helper.DequeueItem();
            var name = helper.GetItemName(item.ItemId);
            var action = Session.ConnectionInfo.Slot == item.Player
                ? ItemTransferType.Aquired // We found our own item
                : ItemTransferType.Received; // Someone else found our item
            var classification = (ItemClassification)((int)item.Flags);

            Patcher.QueueItemTransfer(helper.Index, item.ItemGame, item.ItemId, item.Player, item.LocationId, classification, action);
        }

        public static void CheckLocations(params long[] locationIds)
        {
            foreach (var locationId in locationIds)
            {
                CheckedLocations.Add(locationId);
            };

            if (!APState.IsConnected)
                return;

            var locationsToCheck = CheckedLocations.Except(Session.Locations.AllLocationsChecked);
            Task.Run(() =>
            {
                Session.Locations.CompleteLocationChecksAsync(
                    locationsToCheck.ToArray());
            }).ConfigureAwait(false);

            Task.Run(() => ScoutLocations(locationsToCheck.ToArray()));
        }

        public static void CheckLocation(long locationId)
        {
            CheckLocations(locationId);
        }

        private static async Task ScoutLocations(long[] locationsToCheck)
        {
            // First we go through and 
            var packet = await Session.Locations.ScoutLocationsAsync(false, locationsToCheck);
            foreach (var scout in packet.Values)
            {
                if (Session.ConnectionInfo.Slot == scout.Player)
                {
                    continue;
                }
                var classification = (ItemClassification)((int)scout.Flags);
                // This needs to work without an index (because sent items never have an index.
                Patcher.QueueItemTransfer(null, scout.ItemGame, scout.ItemId, scout.Player, scout.LocationId, classification, ItemTransferType.Sent);
            }
        }

        public static void CompleteGame()
        {
            if (!APState.IsConnected)
                return;

            var statusUpdatePacket = new StatusUpdatePacket();
            statusUpdatePacket.Status = ArchipelagoClientState.ClientGoal;
            Session.Socket.SendPacket(statusUpdatePacket);
            Completed = true;
        }

        public static bool ReadBoolFromDataStorage(string key)
        {
            var value = Session.DataStorage[Scope.Slot, key].To<bool?>();
            return value.HasValue ? value.Value : false;
        }

        public static void SetToDataStorage(string key, DataStorageElement value)
        {
            Session.DataStorage[Scope.Slot, key] = value;
        }
    }
}
