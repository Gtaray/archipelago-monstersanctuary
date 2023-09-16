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
            }
            else if (loginResult is LoginFailure loginFailure)
            {
                Authenticated = false;
                Debug.LogError(String.Join("\n", loginFailure.Errors));
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
        }

        public static string CheckLocation(string location)
        {
            var id = APState.Session.Locations.GetLocationIdFromName("Monster Sanctuary", location);
            return APState.CheckLocation(id);
        }

        public static string CheckLocation(long location)
        {
            if (location <= 0)
            {
                return null;
            }

            Task.Run(() => {
                Session.Locations.CompleteLocationChecksAsync(location);
            }).ConfigureAwait(false);

            var info = Session.Locations.ScoutLocationsAsync(location).GetAwaiter().GetResult();
            var itemname = Session.Items.GetItemName(info.Locations[0].Item);

            return itemname;
            
        }
    }
}
