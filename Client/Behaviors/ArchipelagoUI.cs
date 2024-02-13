using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public class ArchipelagoUI : MonoBehaviour
    {
        string RoomName = "";
        // key is chest id, string is item contained
        public Dictionary<int, string> Chests = new Dictionary<int, string>();
        // key is the encounter id, tuple is monster id and monster name
        public Dictionary<int, string> Monsters = new Dictionary<int, string>();
        public List<string> Connections = new List<string>();
        public Dictionary<int, string> Gifts = new Dictionary<int, string>();

        public void ClearData()
        {
            Chests.Clear();
            Monsters.Clear();
            Connections.Clear();
            Gifts.Clear();
        }

        public void AddChest(int id, string item)
        {
            if (Chests.ContainsKey(id)) return;
            Chests.Add(id, item);
        }

        public void AddMonster(int encounterID, string monsterName)
        {
            if (Monsters.ContainsKey(encounterID)) return;
            Monsters.Add(encounterID, monsterName);
        }

        public void AddConnection(string connection)
        {
            Connections.Add(connection);
        }

        public void AddGift(int id, string item)
        {
            if (Gifts.ContainsKey(id)) return;
            Gifts.Add(id, item);
        }

        void OnGUI()
        {
            int y = 0;
            string ap_ver = "Archipelago v" + APState.AP_VERSION[0] + "." + APState.AP_VERSION[1] + "." + APState.AP_VERSION[2];

            if (APState.Session != null)
            {
                if (APState.Authenticated)
                {
                    GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Connected");
                }
                else
                {
                    GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Authentication failed");
                }
            }
            else
            {
                GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Not Connected");
            }

            y = 36;

            // Login details
            if ((APState.Session == null || !APState.Authenticated) && APState.State != APState.ConnectionState.Connected)
            {
                GUI.Label(new Rect(16, 36, 150, 20), "Host: ");
                GUI.Label(new Rect(16, 56, 150, 20), "PlayerName: ");
                GUI.Label(new Rect(16, 76, 150, 20), "Password: ");

                bool submit = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

                APState.ConnectionInfo.host_name = GUI.TextField(new Rect(150 + 16 + 8, 36, 150, 20),
                    APState.ConnectionInfo.host_name);
                APState.ConnectionInfo.slot_name = GUI.TextField(new Rect(150 + 16 + 8, 56, 150, 20),
                    APState.ConnectionInfo.slot_name);
                APState.ConnectionInfo.password = GUI.TextField(new Rect(150 + 16 + 8, 76, 150, 20),
                    APState.ConnectionInfo.password);

                if (submit && Event.current.type == EventType.KeyDown)
                {
                    // The text fields have not consumed the event, which means they were not focused.
                    submit = false;
                }

                if ((GUI.Button(new Rect(16, 96, 100, 20), "Connect") || submit) && APState.ConnectionInfo.Valid)
                {
                    APState.Connect();
                }

                y += 80; // Height of each of the 3 elements
            }

            if (GameController.Instance == null || string.IsNullOrEmpty(GameController.Instance.CurrentSceneName))
            {
                return;
            }

# if DEBUG
            var scene = GameController.Instance.CurrentSceneName;
            if (RoomName != scene)
            {
                ClearData();
                RoomName = scene;
            }


            if (!string.IsNullOrEmpty(GameController.Instance.CurrentSceneName))
            {
                GUI.Label(new Rect(16, y, 100, 20), "Room Name:");
                GUI.Label(new Rect(16 + 100 + 8, y, 300, 20), GameController.Instance.CurrentSceneName);
                y += 20;
            }

            if (Connections.Count() > 0)
            {
                GUI.Label(new Rect(16, y, 200, 20), "CONNECTIONS");
                y += 20;

                foreach (var connection in Connections)
                {
                    GUI.Label(new Rect(16, y, 200, 20), connection);
                    y += 20;
                }
            }

            if (Chests.Count() > 0)
            {
                GUI.Label(new Rect(16, y, 60, 20), "CHESTS");
                y += 20;

                foreach (var chestdata in Chests)
                {
                    GUI.Label(new Rect(16, y, 100, 20), $"Chest ID {chestdata.Key}:");
                    GUI.Label(new Rect(16 + 100 + 8, y, 150, 20), chestdata.Value);
                    y += 20;
                }
            }

            if (Gifts.Count() > 0)
            {
                GUI.Label(new Rect(16, y, 60, 20), "GIFTS");
                y += 20;

                foreach (var giftdata in Gifts)
                {
                    GUI.Label(new Rect(16, y, 100, 20), $"Gift ID {giftdata.Key}:");
                    GUI.Label(new Rect(16 + 100 + 8, y, 150, 20), giftdata.Value);
                    y += 20;
                }
            }

            if (Monsters.Count() > 0)
            {
                GUI.Label(new Rect(16, y, 200, 20), "ENCOUNTERS");
                y += 20;

                foreach (var encounterdata in Monsters)
                {
                    GUI.Label(new Rect(16, y, 100, 20), $"Encounter ID {encounterdata.Key}:");
                    GUI.Label(new Rect(16 + 100 + 8, y, 300, 20), encounterdata.Value);
                    y += 20;
                }
            }
#endif
        }
    }
}
