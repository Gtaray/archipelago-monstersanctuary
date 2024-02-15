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
        public static int MaxItemHistory = 20;
        public static int FontSize = 20;
        public static int X = 16;
        public static int Y = 100;
        public static int Width = 300;
        public static int Height = 500;

        private static GUIStyle _style = new() { richText = true, wordWrap = true };
        private static string _playerColor = "cyan";
        private static string _itemColor = "orange";
        private static string _otherPlayerColor = "magenta";
        private static List<ItemTransfer> _itemHistory = new();
        private static string _itemHistoryText;

        public void Awake()
        {
            _style.normal.textColor = Color.white;
        }

        public void AddItemToHistory(ItemTransfer itemTransfer)
        {
            _itemHistory.Insert(0, itemTransfer);
            if (_itemHistory.Count > MaxItemHistory)
            {
                _itemHistory.RemoveRange(MaxItemHistory, _itemHistory.Count() - MaxItemHistory);
            }
            _itemHistoryText = BuildItemHistoryText();
        }

        void OnGUI()
        {
            DisplayConnectionInfo();
            DisplayItemHistory();
        }

        private static void DisplayConnectionInfo()
        {
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
            }
        }

        private static void DisplayItemHistory()
        {
            _style.fontSize = FontSize;
            GUI.Box(new Rect(X - 3, Y - 3, Width + 6, Height + 6), "");
            GUI.Label(new Rect(X, Y, Width, Height), _itemHistoryText, _style);
        }

        private static string BuildItemHistoryText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _itemHistory.Count(); i++)
            {
                var item = _itemHistory[i];

                if (item.Action == ItemTransferType.Aquired)
                {
                    sb.Append($"<color={_playerColor}>You</color> found your <color={_itemColor}>{item.ItemName}</color>");
                }
                else if (item.Action == ItemTransferType.Received)
                {
                    sb.Append($"<color={_otherPlayerColor}>{item.PlayerName}</color> sent you <color={_itemColor}>{item}</color>");
                }
                else if (item.Action == ItemTransferType.Sent)
                {
                    sb.Append($"<color={_playerColor}>You</color> sent <color={_itemColor}>{item.ItemName}</color> to <color={_otherPlayerColor}>{item.PlayerName}</color>");
                }

                sb.Append("\n");
            }

            return sb.ToString();
        }
    }
}
