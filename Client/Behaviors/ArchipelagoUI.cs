using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    internal class ItemHistoryEntry
    {
        public string Text { get; set; }
        public float Timer { get; set; } = 0;
        public float Alpha { get; set; } = 1;
    }

    public class ArchipelagoUI : MonoBehaviour
    {
        public int MaxItemHistory = 10;
        public int FontSize = 20;
        public int X = 16;
        public int Y = 50;
        public int Width = 300;
        public int OutlineOffset = 1;
        public bool DrawBox = false;
        public bool FadeOutEntries = true;
        public float FadeOutAfterSeconds = 8f;
        public float FadeOutTime = 2f;

        private GUIStyle _style = new() { richText = true };
        private List<ItemHistoryEntry> _itemHistory = new();

        public void Awake()
        {
            _style.normal.textColor = Color.white;
        }

        public void Update()
        {
            if (!FadeOutEntries)
                return;

            List<ItemHistoryEntry> toRemove = new();

            foreach (ItemHistoryEntry entry in _itemHistory)
            {
                entry.Timer += Time.deltaTime;
                
                // If we're not fading out yet, then we don't do anything
                if (entry.Timer < FadeOutAfterSeconds)
                    continue;

                // As timer approaches FadeOutTime, alpha goes from 1 to 0
                entry.Alpha = Mathf.Lerp(1, 0, (entry.Timer - FadeOutAfterSeconds) / FadeOutTime);

                // Update the color tags with the correct alpha
                var hex = FloatToHex(entry.Alpha);
                entry.Text = Regex.Replace(entry.Text, @"(?<=color=#[0-9a-f]{6})[0-9a-f]{2}", hex);

                if (entry.Alpha <= 0)
                    toRemove.Add(entry);
            }

            foreach (var entry in toRemove)
                _itemHistory.Remove(entry);
        }

        private string FloatToHex(float f)
        {
            // Convert float to int between 0 and 255 so that we have a better conversion to hex
            int val = (int)(f * 255); 
            return val.ToString("X2").ToLower();
        }

        public void AddItemToHistory(ItemTransfer itemTransfer)
        {
            var entry = new ItemHistoryEntry()
            {
                Text = GetEntryText(itemTransfer.PlayerName, itemTransfer.ItemName, itemTransfer.Classification, itemTransfer.Action)
            };

            if (string.IsNullOrEmpty(entry.Text))
                return;

            _itemHistory.Insert(0, entry);
            if (_itemHistory.Count > MaxItemHistory)
            {
                _itemHistory.RemoveRange(MaxItemHistory, _itemHistory.Count() - MaxItemHistory);
            }
        }

        private string GetEntryText(string playerName, string itemName, ItemClassification classification, ItemTransferType action)
        {
            var itemColor = Patcher.GetItemColor(classification);
            if (action == ItemTransferType.Aquired)
            {
                return $"<color=#{Colors.Self}ff>You</color> found your <color=#{itemColor}ff>{itemName}</color>";
            }
            else if (action == ItemTransferType.Received)
            {
                return $"<color=#{Colors.OtherPlayer}ff>{playerName}</color> sent you <color=#{itemColor}ff>{itemName}</color>";
            }
            else if (action == ItemTransferType.Sent)
            {
                return $"<color=#{Colors.Self}ff>You</color> sent <color=#{itemColor}ff>{itemName}</color> to <color=#{Colors.OtherPlayer}ff>{playerName}</color>";
            }

            return "";
        }

        void OnGUI()
        {
            int y = DisplayConnectionInfo();
            DisplayItemHistory(y);
        }

        private static int DisplayConnectionInfo()
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
            //if ((APState.Session == null || !APState.Authenticated) && APState.State != APState.ConnectionState.Connected)
            //{
            //    GUI.Label(new Rect(16, 36, 150, 20), "Host: ");
            //    GUI.Label(new Rect(16, 56, 150, 20), "PlayerName: ");
            //    GUI.Label(new Rect(16, 76, 150, 20), "Password: ");

            //    bool submit = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

            //    APState.ConnectionInfo.host_name = GUI.TextField(new Rect(150 + 16 + 8, 36, 150, 20),
            //        APState.ConnectionInfo.host_name);
            //    APState.ConnectionInfo.slot_name = GUI.TextField(new Rect(150 + 16 + 8, 56, 150, 20),
            //        APState.ConnectionInfo.slot_name);
            //    APState.ConnectionInfo.password = GUI.TextField(new Rect(150 + 16 + 8, 76, 150, 20),
            //        APState.ConnectionInfo.password);

            //    if (submit && Event.current.type == EventType.KeyDown)
            //    {
            //        // The text fields have not consumed the event, which means they were not focused.
            //        submit = false;
            //    }

            //    if ((GUI.Button(new Rect(16, 96, 100, 20), "Connect") || submit) && APState.ConnectionInfo.Valid)
            //    {
            //        APState.Connect();
            //    }

            //    return 120;
            //}

            return 40;
        }

        private void DisplayItemHistory(int y)
        {
            //y = Screen.height - FontSize - (FontSize * MaxItemHistory);
            _style.fontSize = FontSize;
            int height = FontSize * MaxItemHistory;

            if (DrawBox)
            {
                // The box will fade in and out with the most recent entry
                var alpha = _itemHistory.Max(i => i.Alpha);
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, alpha);
                GUI.Box(new Rect(X - 3, y + Y - 3, Width + 6, height + 6), "");
            }

            foreach (var entry in _itemHistory) 
            {
                // Set the alpha of this entry
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, entry.Alpha);
                _style.normal.textColor = new Color(GUI.color.r, GUI.color.g, GUI.color.b, entry.Alpha);

                // Print this entry
                DrawTextWithOutline(new Rect(X, y + Y, Width, height), entry.Text, _style, Color.black);

                y += FontSize;
            }
        }

        private void DrawTextWithOutline(Rect position, string text, GUIStyle style, Color borderColor)
        {
            var backupStyle = style;

            if (OutlineOffset > 0)
            {
                // Get rid of color tags
                var borderText = Regex.Replace(text, @"\<color=#[0-9a-f]+\>", "");
                borderText = Regex.Replace(borderText, @"\<\/color\>", "");

                var oldColor = style.normal.textColor;
                style.normal.textColor = borderColor;

                position.x -= OutlineOffset;
                GUI.Label(position, borderText, style);
                position.x += (OutlineOffset * 2);
                GUI.Label(position, borderText, style);
                position.x -= OutlineOffset;

                position.y -= OutlineOffset;
                GUI.Label(position, borderText, style);
                position.y += (OutlineOffset * 2);
                GUI.Label(position, borderText, style);
                position.y -= OutlineOffset;

                style.normal.textColor = oldColor;
            }

            GUI.Label(position, text, style);
            style = backupStyle;
        }
    }
}
