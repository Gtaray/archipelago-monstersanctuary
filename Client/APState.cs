using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Helpers;
using System.Xml.Linq;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Models;
using static System.Collections.Specialized.BitVector32;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using System.Net;
using System.Data.SqlTypes;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Team17.Online;
using UnityEngine.Networking.Match;

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

        public static int[] AP_VERSION = new int[] { 0, 4, 5 };
        public static string ModVersion = "1.1.4";
        public static ConnectionState State = ConnectionState.Disconnected;
        public static bool IsConnected => State == ConnectionState.Connected;

        public static ArchipelagoConnectionInfo ConnectionInfo = new ArchipelagoConnectionInfo();
        public static ArchipelagoSession Session;
        public static bool Authenticated;
        public static bool Completed = false;
        public static HashSet<string> OfflineChecks = new HashSet<string>(); // Keeps track of locations that were checked while offline
        public static HashSet<long> CheckedLocations = new HashSet<long>(); // Keeps track of checked locations for the current session. Does not persist

        private static DeathLinkService _deathLink;

        #region Connecting
        public static bool Connect()
        {
            if (Authenticated)
            {
                return true;
            }

            if (string.IsNullOrEmpty(ConnectionInfo.host_name))
            {
                return false;
            }

            Patcher.Logger.LogInfo($"Connecting to {ConnectionInfo.host_name} as {ConnectionInfo.slot_name}...");
            State = ConnectionState.Connecting;

            // Start the archipelago session.
            Session = ArchipelagoSessionFactory.CreateSession(ConnectionInfo.host_name);
            Session.MessageLog.OnMessageReceived += Session_MessageReceived;
            Session.Socket.ErrorReceived += Session_ErrorReceived;
            Session.Socket.SocketClosed += Session_SocketClosed;
            Session.Socket.PacketReceived += Session_PacketReceived;
            Session.Items.ItemReceived += ReceiveItem;

            _deathLink = Session.CreateDeathLinkService();
            _deathLink.OnDeathLinkReceived += (deathLinkObject) => ReceiveDeathLink(deathLinkObject);

            string rawPath = Environment.CurrentDirectory;
            if (rawPath != null)
            {
                ArchipelagoConnection.WriteToFile(ConnectionInfo, rawPath + "/archipelago_last_connection.json");
            }
            else
            {
                Patcher.Logger.LogError("Could not write most recent connect info to file.");
            }

            LoginResult loginResult = Session.TryConnectAndLogin(
                "Monster Sanctuary",
                ConnectionInfo.slot_name,
                ItemsHandlingFlags.AllItems,
                new Version(AP_VERSION[0], AP_VERSION[1], AP_VERSION[2]),
                password: ConnectionInfo.password);

            if (loginResult is LoginSuccessful loginSuccess)
            {
                OnConnect(loginSuccess);
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

        static void OnConnect(LoginSuccessful loginSuccess)
        {
            Authenticated = true;

            State = ConnectionState.Connected;

            SlotData.LoadSlotData(loginSuccess.SlotData);

            if (SlotData.DeathLink)
                _deathLink.EnableDeathLink();

            GameData.LoadMinimap();
            Persistence.RebuildCheckCounter();
            Patcher.ClearModifiedShopItems();

            // Scout shops and progression item checks
            var allScouts = GameData.ShopChecks.Values.Union(GameData.ItemChecks.Values).ToArray();
            ScoutLocations(allScouts, false, HandleGlobalScoutCallback);

            // If the player opened chests while not connected, this get those items upon connection
            if (OfflineChecks.Count() > 0)
            {
                Patcher.Logger.LogInfo("Number of Locations Checked While Offline: " + OfflineChecks.Count());
                var ids = OfflineChecks.Select(loc => GameData.ItemChecks[loc]).ToArray();
                CheckLocations(ids);
            }

            Resync();
        }

        static void HandleGlobalScoutCallback(NetworkItem packet)
        {
            if (GameData.ShopChecksReversed.ContainsKey(packet.Location))
            {
                Patcher.AddShopItem(packet);
                return;
            }
            if (GameData.ItemChecksReversed.ContainsKey(packet.Location))
            {
                Patcher.AddProgressionItemChest(packet);
                return;
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
        }

        public static void InitiateDisconnect()
        {
            if (!APState.IsConnected)
                return;

            if (Session != null && Session.Socket != null && Session.Socket.Connected)
            {
                State = ConnectionState.Disconnected;
                Authenticated = false;
                _deathLink.DisableDeathLink();
                Session.Socket.DisconnectAsync();
                Session = null;
            }
        }
        public static void Disconnect()
        {
            if (!APState.IsConnected)
                return;

            Authenticated = false;
            State = ConnectionState.Disconnected;
            Session = null;
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
        #endregion

        #region Item Checks
        public static void Resync()
        {
            if (!APState.IsConnected)
                return;

            Patcher.Logger.LogInfo("Resync()");
            for (int i = 0; i < Session.Items.AllItemsReceived.Count();  i++)
            {
                var item = Session.Items.AllItemsReceived[i];
                
                if (Persistence.HasReceivedItem(i))
                    continue;

                Patcher.Logger.LogInfo("\t" + Session.Items.GetItemName(item.Item) + " - " + i);
                QueueItemTransfer(i, item);
            }
        }
        
        public static void ReceiveItem(ReceivedItemsHelper helper)
        {
            var item = helper.DequeueItem();
            string itemName = Session.Items.GetItemName(item.Item);
            // -1 here as helper.Index tracks the NEXT hypothetical index you'd receive
            // Or otherwise is considered the length of the array of items received
            // This is directly from Not Phar on the AP discord
            int index = helper.Index - 1; 

            //Patcher.Logger.LogInfo("RecieveItem(): " + itemName + "(" + index + ")");

            // If the item index is less than our saved index, we simply ignore it.
            // We've already received this item
            if (Persistence.HasReceivedItem(index))
            {
                //Patcher.Logger.LogInfo("\tAlready handled. Ignore");
                return;
            }

            // Removing this, as we're now tracking the index of all items received
            // If the next item index is more than 1 above what we expect, resync
            //int delta = index - Persistence.Instance.ItemsRecieved;
            //if (delta > 1)
            //{
            //    Patcher.Logger.LogInfo("Mismatched item index. Resyncing");
            //    Resync();
            //    return;
            //}

            QueueItemTransfer(index, item);
        }

        private static void QueueItemTransfer(int index, NetworkItem item)
        {
            var action = Session.ConnectionInfo.Slot == item.Player
                    ? ItemTransferType.Aquired // We found our own item
                    : ItemTransferType.Received; // Someone else found our item
            var classification = (ItemClassification)((int)item.Flags);
            Patcher.QueueItemTransfer(index, item.Item, item.Player, item.Location, classification, action);
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

            // Update the map and check counter that we've checked one or more locations
            // We do this on the front end of the request so that even if we've already received this item
            // we can still mark the location as checked
            Persistence.AddAndUpdateCheckedLocations(locationsToCheck);

            Task.Run(() =>
            {
                Session.Locations.CompleteLocationChecksAsync(
                    locationsToCheck.ToArray());
            }).ConfigureAwait(false);

            Task.Run(() => ShowMessageForSentItem(locationsToCheck.ToArray()));
        }

        public static void CheckLocation(long locationId)
        {
            CheckLocations(locationId);
        }

        private static async Task ShowMessageForSentItem(long[] locationsToCheck)
        {
            // Scout the location in case its another players' check
            var packet = await Session.Locations.ScoutLocationsAsync(false, locationsToCheck);
            foreach (var scout in packet.Locations)
            {
                if (Session.ConnectionInfo.Slot == scout.Player)
                {
                    continue;
                }
                var classification = (ItemClassification)((int)scout.Flags);
                // This needs to work without an index (because sent items never have an index.
                Patcher.QueueItemTransfer(null, scout.Item, scout.Player, scout.Location, classification, ItemTransferType.Sent);
            }
        }
        #endregion

        #region Game State
        public static void CompleteGame()
        {
            if (!APState.IsConnected)
                return;

            //Patcher.Logger.LogInfo("Complete()");
            var statusUpdatePacket = new StatusUpdatePacket();
            statusUpdatePacket.Status = ArchipelagoClientState.ClientGoal;
            Session.Socket.SendPacket(statusUpdatePacket);
            Completed = true;
        }

        public static bool ReadBoolFromDataStorage(string key)
        {
            if (!APState.IsConnected)
            {
                return true;
            }

            var value = Session.DataStorage[Scope.Slot, key].To<bool?>();
            return value.HasValue ? value.Value : false;
        }

        public static void SetToDataStorage(string key, DataStorageElement value)
        {
            if (!APState.IsConnected)
            {
                return;
            }

            Session.DataStorage[Scope.Slot, key] = value;
        }
        #endregion

        #region Scouting
        public static void ScoutLocations(string[] locationNames, bool scoutAsHint = false, System.Action<NetworkItem> action = null)
        {
            var locations = locationNames.Select(l => Session.Locations.GetLocationIdFromName("Monster Sanctuary", l));
            ScoutLocations(locations.ToArray(), scoutAsHint, action);
        }

        public static void ScoutLocations(long location, bool scoutAsHint = false, System.Action<NetworkItem> action = null)
        {
            ScoutLocations(new long[] { location }, scoutAsHint, action);
        }

        public static void ScoutLocations(long[] locations, bool scoutAsHint = false, System.Action<NetworkItem> action = null)
        {
            var task = Task.Run(() => ProcessScoutRequest(locations, scoutAsHint, action));
        }

        private static async Task ProcessScoutRequest(long[] locations, bool asHint, System.Action<NetworkItem> callback)
        {
            var packet = await Session.Locations.ScoutLocationsAsync(asHint, locations);
            foreach (var location in packet.Locations)
            {
                if (callback!= null)
                    callback(location);
            }
        }
        #endregion
    }

    class ScoutRequest
    {
        public long[] Locations { get; set; }
        public System.Action<NetworkItem> CallbackAction { get; set; }
        public bool ScoutAsHint { get; set; } = false;
    }
}
