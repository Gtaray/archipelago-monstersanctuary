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

namespace Archipelago.MonsterSanctuary.Client
{
    public class APState
    {
        public enum ConnectionState
        {
            Disconnected,
            Connected
        }

        public static int[] AP_VERSION = new int[] { 0, 4, 4 };
        public static ArchipelagoUI UI;

        public static ConnectionState State = ConnectionState.Disconnected;
        public static bool IsConnected => State == ConnectionState.Connected;

        public static ArchipelagoConnectionInfo ConnectionInfo = new ArchipelagoConnectionInfo();
        public static ArchipelagoSession Session;
        public static bool Authenticated;
        public static HashSet<long> CheckedLocations = new HashSet<long>();

        private static DeathLinkService _deathLink;

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
                Authenticated = true;
                
                State = ConnectionState.Connected;

                SlotData.LoadSlotData(loginSuccess.SlotData);

                if (SlotData.DeathLink)
                    _deathLink.EnableDeathLink();

                GameData.LoadMinimap();
                Patcher.RebuildCheckCounter();

                // If the player opened chests while not connected, this get those items upon connection
                if (CheckedLocations != null)
                {
                    Session.Locations.CompleteLocationChecks(CheckedLocations.ToArray());
                }

                Resync();
            }
            else if (loginResult is LoginFailure loginFailure)
            {
                Authenticated = false;
                Patcher.Logger.LogError(String.Join("\n", loginFailure.Errors));
                Session = null;
            }

            return loginResult.Successful;
        }

        static void Session_PacketReceived(ArchipelagoPacketBase packet)
        {
            Debug.LogWarning("Packet Received: " + packet.ToString());
        }
        static void Session_SocketClosed(string reason)
        {
            Debug.LogError("Connection to Archipelago lost: " + reason);
            Disconnect();
        }
        static void Session_MessageReceived(LogMessage message)
        {
            Debug.Log("Session_MessageReceived");
            Debug.Log(message);
        }
        static void Session_ErrorReceived(Exception e, string message)
        {
            Debug.LogError("Session_ErrorReceived: " + message);
            if (e != null) Debug.LogError(e.ToString());
            Disconnect();
        }

        public static void Disconnect()
        {
            Authenticated = false;
            State = ConnectionState.Disconnected;
            if (Session != null && Session.Socket != null && Session.Socket.Connected)
            {
                Task.Run(() => { Session.Socket.DisconnectAsync(); }).Wait();
            }
            Session = null;
            _deathLink.DisableDeathLink();
        }

        public static void SendDeathLink()
        {
            if (!APState.IsConnected)
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
            foreach (NetworkItem item in Session.Items.AllItemsReceived)
            {
                var action = Session.ConnectionInfo.Slot == item.Player
                    ? ItemTransferType.Aquired // We found our own item
                    : ItemTransferType.Received; // Someone else found our item
                Patcher.QueueItemTransfer(item.Item, item.Player, item.Location, action);
            }
        }
        
        public static void ReceiveItem(ReceivedItemsHelper helper)
        {
            var item = helper.DequeueItem();
            var name = helper.GetItemName(item.Item);
            var action = Session.ConnectionInfo.Slot == item.Player
                ? ItemTransferType.Aquired // We found our own item
                : ItemTransferType.Received; // Someone else found our item

            Patcher.QueueItemTransfer(item.Item, item.Player, item.Location, action);
        }

        public static long CheckLocation(string location)
        {
            var id = APState.Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
            if (id < 0)
            {
                Patcher.Logger.LogError($"Location ID for '{location}' was -1");
                return -1;
            }

            APState.CheckLocation(id);

            return id;
        }

        public static void CheckLocation(long locationId)
        {
            if (CheckedLocations.Add(locationId))
            {
                var locationsToCheck = CheckedLocations.Except(Session.Locations.AllLocationsChecked);

                Task.Run(() =>
                {
                    Session.Locations.CompleteLocationChecksAsync(
                        locationsToCheck.ToArray());
                }).ConfigureAwait(false);

                Task.Run(() => ScoutLocation(locationsToCheck.ToArray()));

                Patcher.AddAndUpdateCheckedLocations(locationId);
            }
        }

        private static async Task ScoutLocation(long[] locationsToCheck)
        {
            var packet = await Session.Locations.ScoutLocationsAsync(false, locationsToCheck);
            foreach (var location in packet.Locations)
            {
                if (Session.ConnectionInfo.Slot == location.Player)
                    continue;

                Patcher.QueueItemTransfer(location.Item, location.Player, location.Location, ItemTransferType.Sent);
            }
        }

        public static void CompleteGame()
        {
            if (!APState.IsConnected)
                return;

            var statusUpdatePacket = new StatusUpdatePacket();
            statusUpdatePacket.Status = ArchipelagoClientState.ClientGoal;
            Session.Socket.SendPacket(statusUpdatePacket);
        }
    }
}
