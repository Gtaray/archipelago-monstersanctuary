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

namespace Archipelago.MonsterSanctuary.Client
{
    public class APState
    {
        public enum ConnectionState
        {
            Disconnected,
            Connected
        }

        public static int[] AP_VERSION = new int[] { 0, 4, 2 };
        public static ArchipelagoUI UI;

        public static ConnectionState State = ConnectionState.Disconnected;
        public static bool IsConnected => State == ConnectionState.Connected;

        public static ArchipelagoConnectionInfo ConnectionInfo = new ArchipelagoConnectionInfo();
        public static ArchipelagoSession Session;
        public static bool Authenticated;
        public static HashSet<long> CheckedLocations = new HashSet<long>();

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
                new Version(0, 4, 2),
                password: ConnectionInfo.password);

            if (loginResult is LoginSuccessful loginSuccess)
            {
                Authenticated = true;
                State = ConnectionState.Connected;
                LoadSlotData(loginSuccess.SlotData);
            }
            else if (loginResult is LoginFailure loginFailure)
            {
                Authenticated = false;
                Patcher.Logger.LogError(String.Join("\n", loginFailure.Errors));
                Session = null;
            }

            LoadMonsterLocationData();

            Session.Items.ItemReceived += (receivedItemsHelper) =>
            {
                ReceiveItem(receivedItemsHelper);
            };

            // If the player opened chests while not connected, this get those items upon connection
            if (CheckedLocations != null)
            {
                Session.Locations.CompleteLocationChecks(CheckedLocations.ToArray());
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
        }

        public static void LoadSlotData(Dictionary<string, object> slotData)
        {
            Debug.Log("SlotData: " + JsonConvert.SerializeObject(slotData));
            SlotData.ExpMultiplier = int.Parse(slotData["exp_multiplier"].ToString());
                SlotData.AlwaysGetEgg = bool.Parse(slotData["monsters_always_drop_egg"].ToString());
                switch(slotData["monster_shift_rule"].ToString())
                {
                    case ("never"):
                        SlotData.MonsterShiftRule = ShiftFlag.Never;
                        break;
                    case ("after_sun_palace"):
                        SlotData.MonsterShiftRule = ShiftFlag.Normal;
                        break;
                    case ("any_time"):
                        SlotData.MonsterShiftRule = ShiftFlag.Any;
                        break;
                }
        }

        public static void LoadMonsterLocationData()
        {
            // Pre-load all monster locations so we don't have to get them later
            Dictionary<long, string> ids = new Dictionary<long, string>();
            foreach (var location in GameData.MonsterLocations)
            {
                var id = Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
                if (ids.ContainsKey(id))
                    Patcher.Logger.LogWarning("Duplicate location: " + location);
                ids.Add(id, location);
            }
            var info = Session.Locations.ScoutLocationsAsync(ids.Keys.ToArray()).GetAwaiter().GetResult();

            foreach (var location in info.Locations)
            {
                GameData.AddMonster(ids[location.Location], Session.Items.GetItemName(location.Item));
            }
        }
        
        public static void ReceiveItem(ReceivedItemsHelper helper)
        {
            Patcher.Logger.LogInfo("ItemReceived()");
            var item = helper.DequeueItem();
            var name = helper.GetItemName(item.Item);
            Patcher.Logger.LogInfo("Item Name: " + name);
            Patcher.ReceiveItem(name, item.Location);
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
                Patcher.Logger.LogInfo($"CheckLocation({locationId})");
                var locationsToCheck = CheckedLocations.Except(Session.Locations.AllLocationsChecked);
                Patcher.Logger.LogInfo("# of Locations Checked: " + locationsToCheck.Count());
                foreach (var location in locationsToCheck)
                {
                    Patcher.Logger.LogInfo("Location: " + location);
                }

                Task.Run(() =>
                {
                    Session.Locations.CompleteLocationChecksAsync(
                        locationsToCheck.ToArray());
                }).ConfigureAwait(false);
            }
        }
    }
}
