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

            Debug.Log($"Connecting to {ConnectionInfo.host_name} as {ConnectionInfo.slot_name}...");

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
                Debug.LogError("Could not write most recent connect info to file.");
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
                Debug.Log("SlotData: " + JsonConvert.SerializeObject(loginSuccess.SlotData));

                SlotData.ExpMultiplier = int.Parse(loginSuccess.SlotData["exp_multiplier"].ToString());
                SlotData.AlwaysGetEgg = bool.Parse(loginSuccess.SlotData["monsters_always_drop_egg"].ToString());
                switch(loginSuccess.SlotData["monster_shift_rule"].ToString())
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
            else if (loginResult is LoginFailure loginFailure)
            {
                Authenticated = false;
                Debug.LogError(String.Join("\n", loginFailure.Errors));
                Session = null;
            }

            // Pre-load all monster locations so we don't have to get them later
            Dictionary<long, string> ids = new Dictionary<long, string>();
            foreach (var location in Patcher.MonsterLocations)
            {
                var id = Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
                ids.Add(id, location);
            }
            var info = Session.Locations.ScoutLocationsAsync(ids.Keys.ToArray()).GetAwaiter().GetResult();

            foreach (var location in info.Locations)
            {
                Patcher.AddMonster(ids[location.Location], Session.Items.GetItemName(location.Item));
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

        public static string CheckLocation(string location)
        {
            var id = APState.Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
            var items = APState.CheckLocation(id);
            return items.Count() == 1 ? items[0] : null;
        }

        public static string[] CheckLocation(IEnumerable<string> locations)
        {
            return CheckLocation(locations.ToArray());
        }

        public static string[] CheckLocation(params string[] locations)
        {
            var ids = new List<long>();
            foreach (var location in locations)
            {
                var id = APState.Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
                // 970500 is the starting point for all location ids
                if (id < 970500)
                {
                    Debug.LogError($"Tried to get id for location '{location}' but it returned id '{id}'");
                    continue;
                }                    
                ids.Add(id);
            }
            return APState.CheckLocation(ids.ToArray());
        }

        public static string[] CheckLocation(params long[] locations)
        {
            if (locations.Count() <= 0)
            {
                return null;
            }

            Task.Run(() => {
                Session.Locations.CompleteLocationChecksAsync(locations);
            }).ConfigureAwait(false);

            var info = Session.Locations.ScoutLocationsAsync(locations).GetAwaiter().GetResult();

            var itemNames = new List<string>();
            foreach (var location in info.Locations)
            {
                itemNames.Add(Session.Items.GetItemName(location.Item));
            }

            return itemNames.ToArray();

        }
    }
}
